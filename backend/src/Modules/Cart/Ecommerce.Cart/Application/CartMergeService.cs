using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;
using CartAggregate = Ecommerce.Cart.Domain.Cart;

namespace Ecommerce.Cart.Application;

/// <summary>
/// Merges a device cart into the server cart (arch 8.1, D6, D7). Under the cart lock it looks the key up first: a repeat
/// of the same user and body returns the stored reply and changes nothing; another body or user is a conflict. It never
/// lowers an existing server line, and answers every input line once, in input order.
/// </summary>
internal sealed class CartMergeService(
    ICartStore store,
    IProductReader products,
    IInventoryReader inventory,
    CartViewBuilder views,
    IIdempotencyStore keys,
    IUnitOfWork unitOfWork,
    IClock clock,
    CartSettings settings)
{
    public const string Scope = "cart-merge";
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<MergeReply> MergeAsync(Guid userId, string key, IReadOnlyList<MergeLine> lines, CancellationToken ct)
    {
        string hash = HashOf(lines);
        try
        {
            return await unitOfWork.InTransactionAsync(token => ApplyAsync(userId, key, hash, lines, token), ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // Another user (or request) stored this key between our lookup and commit.
            return new MergeReply(MergeStatus.Conflict);
        }
    }

    private async Task<MergeReply> ApplyAsync(Guid userId, string key, string hash, IReadOnlyList<MergeLine> lines, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        CartAggregate cart = await store.EnsureAndLockAsync(userId, now, ct);

        IdempotencyEntry? seen = await keys.FindAsync(Scope, key, ct);
        if (seen is not null)
        {
            return seen.UserId == userId && seen.RequestHash == hash && seen.ResponseJson is not null
                ? new MergeReply(MergeStatus.Ok, JsonSerializer.Deserialize<MergeResult>(seen.ResponseJson, Json))
                : new MergeReply(MergeStatus.Conflict);
        }

        List<Guid> ids = lines.Select(l => l.VariantId).ToList();
        Dictionary<Guid, VariantSnapshot> snapshots = (await products.GetVariantSnapshotsAsync(ids, ct)).ToDictionary(s => s.VariantId);
        IReadOnlyDictionary<Guid, StockAvailability> stock = await inventory.GetAvailabilityAsync(ids, ct);

        List<MergeLineResult> results = [];
        foreach (MergeLine line in lines)
        {
            int server = cart.QuantityOf(line.VariantId);
            if (!snapshots.TryGetValue(line.VariantId, out VariantSnapshot? snapshot) || !snapshot.IsActive || snapshot.Currency != settings.Currency)
            {
                results.Add(new MergeLineResult(line.VariantId, MergeOutcome.Removed, server, MergeReason.Unavailable));
                continue;
            }

            int available = stock[line.VariantId].Available;
            int wanted = server + line.Quantity;
            int target = Math.Min(wanted, Math.Min(settings.MaxQuantityPerLine, available));
            MergeReason bound = available < settings.MaxQuantityPerLine ? MergeReason.OutOfStock : MergeReason.LimitReached;

            if (target >= wanted)
            {
                cart.Set(line.VariantId, target, snapshot.Price, snapshot.Currency, now);
                results.Add(new MergeLineResult(line.VariantId, MergeOutcome.Kept, target, null));
            }
            else if (server < target)
            {
                cart.Set(line.VariantId, target, snapshot.Price, snapshot.Currency, now);
                results.Add(new MergeLineResult(line.VariantId, MergeOutcome.Capped, target, bound));
            }
            else
            {
                // Never lower an existing server line: it stays as it is.
                results.Add(new MergeLineResult(line.VariantId, MergeOutcome.Removed, server, bound));
            }
        }

        MergeResult result = new(await views.BuildAsync(cart.Items, ct), results);
        keys.Add(new IdempotencyEntry(Scope, key, userId, hash, null, JsonSerializer.Serialize(result, Json), now, now + KeyLifetime));
        return new MergeReply(MergeStatus.Ok, result);
    }

    /// <summary>SHA-256 over the lines in input order (the reply order depends on it).</summary>
    private static string HashOf(IReadOnlyList<MergeLine> lines)
    {
        string text = string.Join(';', lines.Select(l => $"{l.VariantId:D}:{l.Quantity}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
