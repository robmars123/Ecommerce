using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ecommerce.Notifications.Api;

/// <summary>
/// Live order status (arch 4.13, D16). Authenticated connections only; each joins the group of its own user id on connect, so a
/// message reaches only the owner. The client never sends anything: a message is a hint to refetch, never the source of truth.
/// </summary>
[Authorize]
internal sealed class OrderStatusHub : Hub
{
    public const string Path = "/hubs/orders";
    public const string StatusChanged = "orderStatusChanged";

    public static string GroupOf(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out Guid userId))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(userId));
        await base.OnConnectedAsync();
    }
}
