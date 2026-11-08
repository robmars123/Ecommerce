using System.Net;
using System.Security.Cryptography;
using Ecommerce.Identity.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog.Core;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests.Identity;

// II-13 .. II-17: logs stay clean, startup validation, admin bootstrap, CORS for credentials, schema.
public class IdentityHostTests(PostgresFixture postgres)
{
    private const string Allowed = "https://store.test.invalid";

    [Fact]
    public async Task II13_no_password_or_token_appears_in_any_log_event_across_all_auth_flows()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test");
        List<string> secrets = new List<string>();
        const string wrong = "wrong secret phrase 1";
        const string fresh = "fresh secret phrase 2";
        secrets.AddRange([IdentityTestEnv.Password, wrong, fresh]);

        IdentityTestEnv.Api registered = await env.PostAsync("/api/v1/auth/register", new { email = "bo@example.test", password = "bo secret phrase 3", displayName = "Bo" });
        secrets.Add("bo secret phrase 3");
        secrets.Add(registered.Str("refreshToken"));
        secrets.Add(registered.Str("accessToken"));
        await env.LoginAsync("ana@example.test", wrong);
        IdentityTestEnv.Api login = await env.LoginAsync("ana@example.test");
        string access = login.Str("accessToken");
        string refresh = login.Str("refreshToken");
        secrets.AddRange([access, refresh]);
        IdentityTestEnv.Api rotated = await env.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        secrets.Add(rotated.Str("refreshToken"));
        await env.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh }); // reuse
        await env.GetAsync("/api/v1/auth/me", access);
        await env.GetAsync("/api/v1/auth/me", "tampered.bearer.value");
        secrets.Add("tampered.bearer.value");
        await env.PostAsync("/api/v1/auth/password-reset", new { email = "ana@example.test" });
        await env.DrainOutboxAsync();
        string reset = env.Sender.TokenOf(0);
        secrets.Add(reset);
        await env.PostAsync("/api/v1/auth/password-reset/confirm", new { token = reset, newPassword = fresh });
        await env.PostAsync("/api/v1/auth/logout", new { refreshToken = rotated.Str("refreshToken") });

        Assert.NotEmpty(env.Logs.Events);
        foreach (LogEvent e in env.Logs.Events)
        {
            string text = e.RenderMessage() + " " + string.Join(" ", e.Properties.Values.Select(v => v.ToString())) + " " + e.Exception;
            foreach (string secret in secrets)
            {
                Assert.DoesNotContain(secret, text);
            }
        }
    }

    private static string AllMessages(Exception? ex)
    {
        List<string> parts = new List<string>();
        for (; ex is not null; ex = ex.InnerException)
        {
            parts.Add(ex.Message);
        }
        return string.Join(" | ", parts);
    }

    [Fact]
    public void II14_startup_fails_naming_the_key_when_it_is_empty_or_shorter_than_32_bytes()
    {
        foreach (string? key in new[] { "", Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) })
        {
            using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, settings: new Dictionary<string, string> { ["Identity:Jwt:SigningKey"] = key });
            Exception? ex = Record.Exception(() => factory.CreateClient());
            Assert.NotNull(ex);
            Assert.Contains("Identity:Jwt:SigningKey", AllMessages(ex));
        }
    }

    [Fact]
    public void II14_a_dev_pickup_directory_outside_development_fails_startup()
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, settings: new Dictionary<string, string> { ["Notifications:DevPickupDirectory"] = "dev-mail" });
        Exception? ex = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(ex);
        Assert.Contains("DevPickupDirectory", AllMessages(ex));
    }

    private async Task<(string Cs, string Email, string BootstrapSecret)> MigratedDatabaseAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string cs = await postgres.CreateDatabaseAsync(ct);
        await using (ApiFactory migrator = new ApiFactory(cs))
        {
            using IServiceScope scope = migrator.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Database.MigrateAsync(ct);
        }
        return (cs, "root@example.test", Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)));
    }

    private static Dictionary<string, string> Bootstrap(string email, string secret) => new()
    {
        ["Identity:Bootstrap:AdminEmail"] = email,
        ["Identity:Bootstrap:AdminPassword"] = secret,
    };

    [Fact]
    public async Task II15_bootstrap_creates_the_admin_once_and_a_second_start_is_a_no_op()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (string? cs, string? email, string? secret) = await MigratedDatabaseAsync();

        for (int i = 0; i < 2; i++)
        {
            await using ApiFactory factory = new ApiFactory(cs, settings: Bootstrap(email, secret));
            using IServiceScope scope = factory.Services.CreateScope();
            List<User> users = await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Set<User>().AsNoTracking().ToListAsync(ct);
            User admin = Assert.Single(users);
            Assert.Equal(UserRole.Admin, admin.Role);
        }
    }

    [Fact]
    public async Task II15_bootstrap_never_promotes_an_existing_customer_and_logs_an_error_without_email_or_password()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (string? cs, string? email, string? secret) = await MigratedDatabaseAsync();
        CollectingSink sink = new CollectingSink();
        await using (ApiFactory seed = new ApiFactory(cs))
        {
            using IServiceScope scope = seed.Services.CreateScope();
            EcommerceDbContext db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
            db.Add(User.Register(Guid.CreateVersion7(), email, "Squatter", "hash", "00112233445566778899aabbccddeeff", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        await using ApiFactory factory = new ApiFactory(cs, s => s.AddSingleton<ILogEventSink>(sink), Bootstrap(email, secret));
        using IServiceScope check = factory.Services.CreateScope();
        User user = await check.ServiceProvider.GetRequiredService<EcommerceDbContext>().Set<User>().AsNoTracking().SingleAsync(ct);

        Assert.Equal(UserRole.Customer, user.Role);
        LogEvent error = Assert.Single(sink.Events, e => e.Level == Serilog.Events.LogEventLevel.Error && e.MessageTemplate.Text.Contains("not an Admin"));
        string text = error.RenderMessage();
        Assert.Contains(user.Id.ToString(), text);
        Assert.DoesNotContain(email, text);
        Assert.DoesNotContain(secret, text);
    }

    [Fact]
    public async Task II15_bootstrap_with_a_password_shorter_than_8_creates_nothing_and_logs_an_error_naming_the_key()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (string? cs, string? email, string _) = await MigratedDatabaseAsync();
        const string weak = "abc1234"; // 7 characters
        CollectingSink sink = new CollectingSink();

        await using ApiFactory factory = new ApiFactory(cs, s => s.AddSingleton<ILogEventSink>(sink), Bootstrap(email, weak));
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.Empty(await scope.ServiceProvider.GetRequiredService<EcommerceDbContext>().Set<User>().AsNoTracking().ToListAsync(ct));
        LogEvent error = Assert.Single(sink.Events, e => e.Level == Serilog.Events.LogEventLevel.Error && e.MessageTemplate.Text.Contains("AdminPassword"));
        string text = error.RenderMessage();
        Assert.Contains("Identity:Bootstrap:AdminPassword", text);
        Assert.DoesNotContain(weak, text);
        Assert.DoesNotContain(email, text);
        Assert.All(sink.Events, e => Assert.DoesNotContain(weak, e.RenderMessage()));
    }

    [Fact]
    public async Task II16_credentialed_cors_applies_to_auth_and_admin_for_configured_origins_only()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, settings: new Dictionary<string, string> { ["Cors:AllowedOrigins:0"] = Allowed });
        using HttpClient client = factory.CreateClient();

        foreach ((string? url, string? method) in new[] { ("/api/v1/auth/login", "POST"), ($"/api/v1/admin/products/{Guid.NewGuid()}", "PUT") })
        {
            using HttpRequestMessage preflight = new HttpRequestMessage(HttpMethod.Options, url);
            preflight.Headers.Add("Origin", Allowed);
            preflight.Headers.Add("Access-Control-Request-Method", method);
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-auth-transport");
            HttpResponseMessage response = await client.SendAsync(preflight, ct);

            Assert.Equal(Allowed, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            string[] methods = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods"))
                .Split(',', StringSplitOptions.TrimEntries).Order().ToArray();
            Assert.Equal(["GET", "POST", "PUT"], methods);

            using HttpRequestMessage other = new HttpRequestMessage(HttpMethod.Options, url);
            other.Headers.Add("Origin", "https://evil.test.invalid");
            other.Headers.Add("Access-Control-Request-Method", method);
            Assert.False((await client.SendAsync(other, ct)).Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task II17_schema_has_citext_the_identity_tables_indexes_checks_and_restrict_foreign_keys()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        async Task<List<string>> Q(string sql) => await env.Db.Database.SqlQueryRaw<string>(sql).ToListAsync(ct);

        Assert.Contains("citext", await Q("SELECT extname AS \"Value\" FROM pg_extension"));
        Assert.Contains(await env.Db.Database.GetAppliedMigrationsAsync(ct), m => m.EndsWith("_AddIdentity", StringComparison.Ordinal));
        List<string> tables = await Q("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'");
        Assert.All(new[] { "users", "refresh_token_families", "refresh_tokens", "password_reset_tokens" }, t => Assert.Contains(t, tables));

        string indexes = string.Join("\n", await Q("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'"));
        foreach (string? name in new[]
        {
            "ux_users_email", "ix_refresh_token_families_user_id", "ix_refresh_tokens_family_id", "ux_refresh_tokens_token_hash",
            "ix_password_reset_tokens_user_id", "ux_password_reset_tokens_token_hash",
        })
        {
            Assert.Contains(name, indexes);
        }
        string checks = string.Join("\n", await Q("SELECT conname AS \"Value\" FROM pg_constraint WHERE contype = 'c'"));
        Assert.Contains("ck_users_role", checks);
        Assert.Contains("ck_users_access_failed_count", checks);
        List<string> fkActions = await Q(
            "SELECT confdeltype::text AS \"Value\" FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid " +
            "WHERE c.contype = 'f' AND t.relname IN ('refresh_token_families','refresh_tokens','password_reset_tokens')");
        Assert.NotEmpty(fkActions);
        Assert.All(fkActions, a => Assert.Equal("r", a));
        // products.version is the xmin system column: the identity migration must not have added a real column.
        Assert.DoesNotContain("version", await Q("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'products'"));

        // The database refuses a role outside the CHECK even if the domain were bypassed.
        User user = await env.AddUserAsync("ana@example.test");
        env.Db.Entry(user).Property(u => u.Role).CurrentValue = (UserRole)99;
        DbUpdateException ex = await Assert.ThrowsAsync<DbUpdateException>(() => env.Db.SaveChangesAsync(ct));
        Assert.Equal("ck_users_role", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }
}
