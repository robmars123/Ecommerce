using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Catalog;
using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.Identity.Infrastructure;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace Ecommerce.Api.IntegrationTests.Identity;

/// <summary>Records every reset link instead of sending it. Registered in place of the real sender.</summary>
internal sealed class RecordingLinkSender : IPasswordResetLinkSender
{
    private readonly List<(Guid UserId, string Email, string? Link)> _sent = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<(Guid UserId, string Email, string? Link)> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(Guid userId, string email, string? link, CancellationToken ct)
    {
        lock (_gate)
        {
            _sent.Add((userId, email, link));
        }
        return Task.CompletedTask;
    }

    public string TokenOf(int index) => Sent[index].Link![(Sent[index].Link!.IndexOf("token=", StringComparison.Ordinal) + 6)..];
}

/// <summary>Counts real password verifications (delegates to the real hasher).</summary>
internal sealed class CountingPasswordHashing(IPasswordHashing inner) : IPasswordHashing
{
    private int _verifies;
    public int Verifies => Volatile.Read(ref _verifies);

    public string Hash(string password) => inner.Hash(password);

    public PasswordVerification Verify(string hash, string password)
    {
        Interlocked.Increment(ref _verifies);
        return inner.Verify(hash, password);
    }

    public void VerifyAgainstDummy(string password) => inner.VerifyAgainstDummy(password);
}

/// <summary>
/// A migrated database, a real HTTP client (no automatic cookies), a fake clock that starts at the real time (so JWT
/// lifetimes line up) and users built through the domain and the real hasher, never raw SQL.
/// </summary>
internal sealed class IdentityTestEnv : IAsyncDisposable
{
    public const string Password = "correct horse battery";
    private readonly IServiceScope _scope;

    private IdentityTestEnv(ApiFactory factory, HttpClient client, IServiceScope scope, FakeClock clock, RecordingLinkSender sender, CollectingSink logs)
    {
        Factory = factory;
        Client = client;
        _scope = scope;
        Clock = clock;
        Sender = sender;
        Logs = logs;
        Db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
    }

    public ApiFactory Factory { get; }
    public HttpClient Client { get; }
    public FakeClock Clock { get; }
    public RecordingLinkSender Sender { get; }
    public CollectingSink Logs { get; }
    public EcommerceDbContext Db { get; }
    public CountingPasswordHashing? Counter { get; private set; }

    public static async Task<IdentityTestEnv> StartAsync(
        PostgresFixture postgres,
        bool countHashing = false,
        IReadOnlyDictionary<string, string>? settings = null,
        Action<IServiceCollection>? configure = null)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string cs = await postgres.CreateDatabaseAsync(ct);
        // Whole seconds: Postgres stores microseconds, so a sub-microsecond clock would make "unchanged" values differ after a
        // round trip and hide whether a guard (such as forcing updated_at into the UPDATE) is really what protects a write.
        DateTimeOffset start = DateTimeOffset.UtcNow;
        FakeClock clock = new FakeClock(start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)));
        RecordingLinkSender sender = new RecordingLinkSender();
        CollectingSink logs = new CollectingSink();
        CountingPasswordHashing? counter = null;
        Dictionary<string, string> all = new Dictionary<string, string>
        {
            ["Identity:PasswordReset:LinkBaseUrl"] = "http://localhost/reset-password",
            ["Identity:RefreshTokens:Cookie:Secure"] = "false",
        };
        foreach ((string? k, string? v) in settings ?? new Dictionary<string, string>())
        {
            all[k] = v;
        }
        ApiFactory factory = new ApiFactory(cs, services =>
        {
            services.AddSingleton<IClock>(clock);
            services.AddSingleton<ILogEventSink>(logs);
            services.AddSingleton<IPasswordResetLinkSender>(sender);
            if (countHashing)
            {
                ServiceDescriptor real = services.Single(d => d.ServiceType == typeof(IPasswordHashing));
                services.Remove(real);
                counter = new CountingPasswordHashing(new Ecommerce.Identity.Infrastructure.IdentityPasswordHashing());
                services.AddSingleton<IPasswordHashing>(counter);
            }
            configure?.Invoke(services);
        }, all);
        IServiceScope scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Database.MigrateAsync(ct);
        HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        return new IdentityTestEnv(factory, client, scope, clock, sender, logs) { Counter = counter };
    }

    public async Task<User> AddUserAsync(string email, UserRole role = UserRole.Customer, string password = Password)
    {
        IdentityPasswordHashing hashing = new Ecommerce.Identity.Infrastructure.IdentityPasswordHashing();
        string stamp = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        User user = User.Register(Guid.CreateVersion7(Clock.UtcNow), email.ToLowerInvariant(), "Test User", hashing.Hash(password), stamp, Clock.UtcNow);
        Db.Add(user);
        if (role != UserRole.Customer)
        {
            Db.Entry(user).Property(u => u.Role).CurrentValue = role;
        }
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    public async Task<Api> SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null, params (string Name, string Value)[] headers)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpRequestMessage request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Correlation-ID", "id-test-1");
        if (body is not null)
        {
            request.Content = body is string raw
                ? new StringContent(raw, System.Text.Encoding.UTF8, "application/json")
                : JsonContent.Create(body);
        }
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        foreach ((string? name, string? value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
        HttpResponseMessage response = await Client.SendAsync(request, ct);
        string text = await response.Content.ReadAsStringAsync(ct);
        return new Api(response, JsonDocument.Parse(text.Length == 0 ? "{}" : text), text);
    }

    public Task<Api> PostAsync(string url, object? body = null, string? bearer = null, params (string, string)[] headers) =>
        SendAsync(HttpMethod.Post, url, body ?? new { }, bearer, headers);

    public Task<Api> GetAsync(string url, string? bearer = null) => SendAsync(HttpMethod.Get, url, null, bearer);

    public Task<Api> LoginAsync(string email, string password = Password) =>
        PostAsync("/api/v1/auth/login", new { email, password });

    /// <summary>Logs in and returns the access token (fails the test if login is not 200).</summary>
    public async Task<string> AccessTokenAsync(string email, string password = Password)
    {
        Api r = await LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, r.Status);
        return r.Str("accessToken");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _scope.Dispose();
        await Factory.DisposeAsync();
    }

    internal sealed record Api(HttpResponseMessage Response, JsonDocument Doc, string Text)
    {
        public HttpStatusCode Status => Response.StatusCode;
        public JsonElement Json => Doc.RootElement;
        public string Str(string name) => Json.GetProperty(name).GetString()!;
    }
}
