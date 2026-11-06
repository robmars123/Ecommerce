using System.Net;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

public class HealthTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Live_returns_200_without_touching_the_database()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Ready_returns_200_when_postgres_is_up_and_migrated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string cs = await postgres.CreateDatabaseAsync(ct);
        await using ApiFactory factory = new ApiFactory(cs);
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Database.MigrateAsync(ct);
        }
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Ready_returns_503_when_postgres_is_unreachable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Ready_returns_503_when_migrations_are_pending()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string cs = await postgres.CreateDatabaseAsync(ct);
        await using ApiFactory factory = new ApiFactory(cs);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
