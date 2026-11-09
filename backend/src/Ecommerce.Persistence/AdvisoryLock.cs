using Npgsql;

namespace Ecommerce.Persistence;

/// <summary>Session-level advisory locks on a dedicated connection: one worker per consumer or projector. Not a transaction.</summary>
internal static class AdvisoryLock
{
    public static async Task<bool> TryLockAsync(NpgsqlConnection connection, string key, CancellationToken ct)
    {
        await using NpgsqlCommand cmd = new("SELECT pg_try_advisory_lock(hashtext(@key))", connection);
        cmd.Parameters.AddWithValue("key", key);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public static async Task UnlockQuietlyAsync(NpgsqlConnection connection, string key)
    {
        try
        {
            await using NpgsqlCommand cmd = new("SELECT pg_advisory_unlock(hashtext(@key))", connection);
            cmd.Parameters.AddWithValue("key", key);
            await cmd.ExecuteScalarAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // A broken connection drops its session locks anyway.
        }
    }
}
