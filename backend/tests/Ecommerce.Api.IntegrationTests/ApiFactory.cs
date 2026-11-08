using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

public sealed class ApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string Unreachable = "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=2";

    /// <summary>The webhook signing secret this host was started with: random per factory, so no secret is ever committed. Tests sign with it.</summary>
    public string WebhookSecret { get; } = settings is not null && settings.TryGetValue("Stripe:WebhookSecret", out string? given)
        ? given
        : Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        // A fresh random signing key per factory, so no key is ever committed. A test's own setting wins (see below).
        builder.UseSetting("Identity:Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        // Random placeholders so a non-Development host starts. Tests replace the gateway with a fake; nothing calls the real provider.
        builder.UseSetting("Stripe:SecretKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));
        builder.UseSetting("Stripe:WebhookSecret", WebhookSecret);
        // The outbox worker is off in tests: they drive OutboxProcessor themselves so every run is deterministic.
        builder.UseSetting("Outbox:Worker:Enabled", "false");
        foreach ((string? key, string? value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
        builder.ConfigureTestServices(services => configureServices?.Invoke(services));
    }
}
