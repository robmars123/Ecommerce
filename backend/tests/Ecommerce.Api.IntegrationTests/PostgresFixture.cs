using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(Ecommerce.Api.IntegrationTests.PostgresFixture))]
// xunit v3 runs one test class per CPU in parallel (24 here). Each host's pool holds up to 8 connections, so the container's
// 100-connection limit (and the Docker port proxy) was exhausted. 8 classes x 8 = 64 leaves room for lingering idle pools.
[assembly: CollectionBehavior(MaxParallelThreads = 8)]

namespace Ecommerce.Api.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync(CancellationToken ct)
    {
        string name = $"t_{Guid.NewGuid():N}";
        await using (NpgsqlConnection admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync(ct);
            await using NpgsqlCommand cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{name}\"";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name, MaxPoolSize = 8, ConnectionIdleLifetime = 2, ConnectionPruningInterval = 1 };
        // One shared container serves every test database; pooled idle connections (one pool per database) would exhaust
        // its 100-connection limit, so each pool is small and its idle connections are pruned after 2 seconds.
        return builder.ConnectionString;
    }
}
