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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        // A fresh random signing key per factory, so no key is ever committed. A test's own setting wins (see below).
        builder.UseSetting("Identity:Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        foreach ((string? key, string? value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
        builder.ConfigureTestServices(services => configureServices?.Invoke(services));
    }
}
