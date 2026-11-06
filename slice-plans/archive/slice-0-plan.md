# Slice 0 — Foundation: Plan

Mode: Plan · Author: Rob · Date: 2026-11-02 · Fix-round cap for this run: **2** (Nathaniel's override of CLAUDE.md's 3)

---

## Summary

**Decision:** Build Slice 0 as a .NET 10 four-project solution (Domain, Application, Infrastructure, Api) with three test projects, one EF Core DbContext with an empty `InitialBaseline` migration, `/health/live` and `/health/ready` (Postgres reachable + no pending migrations), Serilog with correlation ID middleware, a GitHub Actions workflow file, a Dockerfile, a local Postgres 17 compose file, and an Expo SDK 58 app with every app-context §5 route stubbed. Postgres 17 runs **only in Docker**: Testcontainers for tests and Docker Compose for the local run. The local Windows PG 16 and PG 18 services are not used.

**Plan (summary):**
1. Start Docker Desktop. If the engine won't come up, stop: the slice is BLOCKED, and there is no fallback.
2. Scaffold `Ecommerce.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` (central pins), the `dotnet-ef` local tool, and 4 src + 3 test projects.
3. Infrastructure: `EcommerceDbContext` (no DbSets yet), design-time factory, `InitialBaseline` migration, `PostgresReadinessHealthCheck`, `SystemClock`, `AddInfrastructure`.
4. Api: `Program.cs` sets up DI validation, Serilog, `CorrelationIdMiddleware`, Problem Details, and the health endpoints.
5. Tests: 15 tests (2 architecture, 1 clock-rule scan, 12 integration on Testcontainers `postgres:17-alpine`).
6. Infra files: `compose.yaml` (PG17 on 127.0.0.1:5434), `Dockerfile`, `.dockerignore`, `.github/workflows/ci.yml`.
7. Expo: unpack template `expo-template-blank-typescript@58.0.31` into `apps/store`, pin exact versions, switch the entry to Expo Router, and stub 20 routes plus not-found.
8. Docs: architecture §10.3 wording and three §13.2 rows; the README "Local development" section.
9. Verify: `dotnet build`, `dotnet test`, the manual `/health/ready` 200 against compose PG17, `tsc --noEmit`, `expo export --platform web`, `docker build`, and the local secret scan.

**Why:** Slice 0 has to make every later slice testable on real Postgres 17. The stack lock (§2) demands PG17, and no local PG17 exists, so Docker is the only compliant source. A readiness check that also checks migrations enforces §10.5: no traffic before the schema is ready. Every choice below picks the smallest dependency set that the docs require.

**Tradeoff:**
- Tests and the local run depend on Docker Desktop. That is slower than a native DB, but it's the only way to get PG17 and the same engine CI uses.
- One DbContext makes module boundaries a matter of convention plus review, not something the compiler enforces. In return, checkout (Inventory + Orders + Payments + outbox) gets the single transaction §8.2 requires.
- Readiness excludes Redis, which diverges from the current §10.3 text. The doc is updated in this slice.

**Risks (top):**
- Docker Desktop 26.1.4 (2024) may not start, or may fail to pull images.
- The xunit v3 / Test SDK 18 / .NET 10 VSTest combination is unproven locally. A fallback is pre-approved below.
- Expo SDK 58 is one month old.
- The CI workflow can't be executed until Nathaniel pushes it.

Full list in §13.

**Needs my decision:** None.

**Verdict: READY**

---

## 1. Scope

**In:** solution structure, DI composition, Postgres + EF Core migrations pipeline, health endpoints, Serilog + correlation ID, Problem Details wiring, CI workflow file, Dockerfile, local PG17 compose, Expo scaffold with §5 routes stubbed, doc and README updates listed in §11.

**Out (any of these in the diff is a blocking failure: quality bar §1, "two slices at once"):** entities and tables, Identity/JWT, controllers, `/api/v1` routing, CORS, rate limits, Redis, OpenTelemetry, TanStack Query, Zustand, `lib/api-client.ts`, `lib/platform.ts`, auth guards, Stripe/email/Spaces config, EAS config, bundle IDs, deployment.

**Required §11.2 tests for Slice 0:** none (§12.3: tests 1–19 belong to Slices 2–6b). **Test-first gate:** N/A. Slice 0 owns no hard invariant. The 15 Slice 0 tests in §8 are still mandatory.

---

## 2. Decisions made (Rob, within the docs)

| # | Decision | Reason |
|---|---|---|
| D1 | **PostgreSQL 17 runs only in Docker:** image `postgres:17-alpine` for Testcontainers and for local Compose. Do not connect to, modify, or rely on the local Windows PG16 (5432) / PG18 (5433) services. | Stack lock §2 says PG17. No local PG17 exists, and the credentials for the local services are unknown. Using 16 or 18 would be a silent stack change. |
| D2 | **Marco may start the already-installed Docker Desktop** and wait up to 3 minutes for `docker info` to succeed. Installing, upgrading, or reconfiguring Docker is not allowed. If the engine doesn't start, the slice is BLOCKED. There is **no fallback** to in-memory EF, SQLite, or local PG16/18. | CLAUDE.md: "`dotnet test` needs Docker running". Architecture §2 and quality-bar §4.3 require Testcontainers, and in-memory EF does not count. Starting an installed app is an operational precondition, not a stack change. |
| D3 | **`/health/ready` = Postgres reachable AND no pending EF migrations → 200; otherwise 503. Redis is not checked in Slice 0.** When Redis arrives (Slice 7, caching §10.2), its check must be registered with `failureStatus: Degraded`, which maps to HTTP 200, so a Redis outage never pulls traffic. Marco updates the §10.3 text (see §11). | No Redis client exists in Slice 0, and adding one now would be an unneeded package. More importantly, §10.2 and rule 4 say losing Redis loses nothing and cache-aside falls back to Postgres, so making readiness fail on Redis contradicts them. The migration check enforces §10.5 ("migrations run before the new version takes traffic"). It is also rollback-safe: an older image sees no pending migrations of its own. |
| D4 | **Quality-bar §3 "previous slice reported PASS" is N/A for Slice 0** (there is no previous slice). The review records "N/A — Slice 0 is the first slice". The quality bar file is not edited, since no agent may edit it. | No predecessor exists. |
| D5 | **One `EcommerceDbContext`, one migration stream, in `Ecommerce.Infrastructure/Persistence`.** No DbSets in Slice 0. Later modules add `IEntityTypeConfiguration<T>` classes under `Infrastructure/<Module>/`, applied by `ApplyConfigurationsFromAssembly`. DbSets are never injected outside Infrastructure: Application sees module interfaces only. The default history table `__EFMigrationsHistory` stays. Snake_case naming and per-module schemas are deferred to the Slice 1 plan, before the first table exists. | §8.2 needs reservation + order + payment + outbox in **one** transaction. Multiple contexts would need shared-connection transaction plumbing on the most critical path. Rule 8 is enforced by Application never referencing Infrastructure, plus review. Recorded in §13.2. |
| D6 | **Migrations never auto-apply at API startup.** Locally they run via the `dotnet-ef` local tool. CI runs `has-pending-model-changes` (drift) and `migrations script --idempotent` (the deploy artifact). Integration test I1 proves they apply cleanly to an empty PG17. | §10.5: migrations are a separate, reviewed step before traffic. Quality-bar §4.4: "apply cleanly to an empty Postgres database". |
| D7 | **Solution format `Ecommerce.slnx`** (the .NET 10 default). **Central Package Management** via `Directory.Packages.props` (every version pinned in one file). **`global.json`** = `{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}`. | `.slnx` is the SDK 10 default. Central pinning makes "exact versions only" reviewable. `10.0.100` + `latestFeature` accepts the local 10.0.301, CI's `10.0.x`, and the SDK band inside `mcr.microsoft.com/dotnet/sdk:10.0`. Pinning `10.0.301` exactly would break the Docker build. |
| D8 | **xunit v3 3.2.2** (not v4.x) + VSTest adapter. | v3 is a stable, well-known API. v4 (Aug 2026) is new and its migration cost is unassessed. Revisit in Slice 7. |
| D9 | **Serilog:** sinks configured in code, not in appsettings (avoids array-merge bugs between appsettings files). Only `MinimumLevel` is read from config. Use `AddSerilog(..., preserveStaticLogger: true)` with **no bootstrap logger**. No static `Log.*` calls in app code. Console sink only (compact JSON outside Development). **OpenTelemetry is deferred** to the first slice that emits a metric or trace; Rob lists its packages then. | The bootstrap logger plus multiple `WebApplicationFactory` hosts throws "logger already frozen". Overwriting the static logger causes cross-test interference. Nathaniel scoped Slice 0 to "Serilog + correlation ID". |
| D10 | **Correlation ID:** header `X-Correlation-ID`. Accept the inbound value only if it is 1–64 chars and matches `^[A-Za-z0-9._:-]+$`; otherwise generate `Guid.NewGuid().ToString("N")`. Rejected inbound values are never logged. The ID is pushed as Serilog property `CorrelationId`, stored in `HttpContext.Items`, echoed on every response, and added as a `correlationId` extension on every Problem Details response. | §10.3. Validation prevents log and header injection. Problem Details `traceId` comes from ASP.NET by default. |
| D11 | **Not in Slice 0:** HTTPS redirection, HSTS, CORS, controllers, `/api/v1` prefix, auth. Health endpoints live at root `/health/live` and `/health/ready`, outside `/api/v1`. | No endpoint needs these yet, and adding them now would be dead code. TLS terminates at the platform (§10.5). Platform probes hit root paths. Controllers and versioning arrive with the first business endpoint (Slice 1). |
| D12 | **Local config:** Compose PG17 uses `POSTGRES_HOST_AUTH_METHOD=trust`, bound to `127.0.0.1:5434` only. So the local connection string contains **no secret** and lives in `appsettings.Development.json`. `appsettings.json` holds the placeholder `"Postgres": ""`. Production supplies `ConnectionStrings__Postgres` via env var at deploy (not in Slice 0). Startup **fails fast** (options validation, `ValidateOnStart`) if the value is empty. `dotnet user-secrets init` runs on the Api project now, as the home for future local secrets (JWT key in Slice 1, Stripe test keys in Slice 4); nothing is stored in Slice 0. Do not add Stripe/email/Spaces placeholders in Slice 0. | Rule 10: no secret ever enters git, chat, or a CLI argument that gets pasted into reports. Port 5434 is free (5433 is PG18 and 5432 is reserved for PG16). The password auth path is still exercised by Testcontainers. Unused config is dead config. |
| D13 | **Add a `Dockerfile` (multi-stage, non-root) and `.dockerignore`.** CI builds the image; nothing is pushed or deployed. | §2 "Docker image from CI" and quality-bar §4.6 "API Docker image builds" are non-optional CI checks. |
| D14 | **Secret scan = `git grep -E` regex step in CI** (no third-party action). | §4.5 patterns, with no new dependency. The regex is written so it doesn't match itself, so no file needs an exclusion. |
| D15 | **GitHub Actions pinned to current majors:** `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`. `permissions: contents: read`. No deploy job. | These were the latest major tags on 2026-11-02. SHA pinning is a Slice 7 hardening item. |
| D16 | **Expo SDK 58** (expo 58.0.22, RN 0.88.1, React 19.3.0). Scaffold by unpacking `expo-template-blank-typescript@58.0.31` (not `create-expo`, which brings an unpinned generator). Add Expo Router manually with exact pins. Set `web.output: "single"` (SPA). No `experiments.typedRoutes`. No `ios.bundleIdentifier` / `android.package`. | Expo Go on a physical phone (§2 local testing) runs only the latest SDK. The SPA output matches §5.2 ("RN Web is client-rendered") and avoids static-render needs for dynamic routes. Typed routes generate files that `tsc` in CI would need first. Bundle IDs are identifiers Nathaniel hasn't chosen, needed only at EAS time. |
| D17 | **Microsoft packages at 10.0.14** (latest patch, 2026-10-25), even though the local runtime is 10.0.9. Add a direct `Microsoft.EntityFrameworkCore.Relational` pin so Api and Infrastructure resolve the same EF version. | Latest patch for security (quality-bar §6). Without the direct pin, `Design` (PrivateAssets=all) would pull Infrastructure to 10.0.14 while Api resolves Npgsql's floor of 10.0.4. |
| D18 | **No auto-start of anything else.** Leave the Compose Postgres container running after verification, or `docker compose stop` it. Never `down -v` or delete volumes or databases. Integration tests create uniquely named databases inside throwaway Testcontainers and never drop anything. | CLAUDE.md: no dropping or deleting databases. |

---

## 3. Exact packages — the only ones Marco may install

### 3.1 NuGet (in `Directory.Packages.props`; csproj `PackageReference`s carry no version)

| Package | Version | Project(s) | Reason / replaces |
|---|---|---|---|
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Infrastructure | EF Core provider for PG17 (§2). Brings Npgsql 10.0.3 and EF Core. Replaces nothing. |
| Microsoft.EntityFrameworkCore.Relational | 10.0.14 | Infrastructure | Pins one EF version across all projects (D17). Already transitive. |
| Microsoft.EntityFrameworkCore.Design | 10.0.14 | Infrastructure, `PrivateAssets="all"` | Migrations tooling (design-time only, never in the runtime image). |
| Microsoft.Extensions.Diagnostics.HealthChecks | 10.0.14 | Infrastructure | `IHealthCheck` + `IHealthChecksBuilder` for the readiness check in a class library. Replaces a `FrameworkReference` to all of ASP.NET. |
| Serilog.AspNetCore | 10.0.0 | Api | Serilog (§2). Brings Serilog 4.3.0, Console sink, Settings.Configuration, Formatting.Compact, Extensions.Hosting. |
| Microsoft.NET.Test.Sdk | 18.11.0 | all 3 test projects | VSTest host. **Pre-approved fallback: 17.14.1**, only if tests are not discovered or run with 18.11.0. Report which was used. |
| xunit.v3 | 3.2.2 | all 3 test projects | xUnit (§2), D8. |
| xunit.runner.visualstudio | 3.1.5 | all 3 test projects | VSTest adapter for xunit v3. |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 | Api.IntegrationTests | `WebApplicationFactory` (§2). |
| Testcontainers.PostgreSql | 4.16.0 | Api.IntegrationTests | Real PG17 in tests (§2). |

**.NET local tool** (`.config/dotnet-tools.json`): `dotnet-ef` **10.0.14**, which runs migrations add/update/script/drift check.

**Not allowed** (examples of tempting extras): FluentAssertions, Shouldly, NetArchTest, Respawn, Serilog.Sinks.File, Serilog.Sinks.InMemory, OpenTelemetry.*, AspNetCore.HealthChecks.*, StackExchange.Redis, EFCore.NamingConventions, FluentValidation, Swashbuckle, Microsoft.AspNetCore.OpenApi, coverlet.

### 3.2 npm (`apps/store/package.json`, exact versions: no `^` or `~`)

Template source: `expo-template-blank-typescript@58.0.31` (unpacked, not installed as a dependency).

| dependencies | version | Why |
|---|---|---|
| expo | 58.0.22 | SDK 58 |
| react | 19.3.0 | SDK 58 pair |
| react-native | 0.88.1 | SDK 58 pair |
| react-dom | 19.3.0 | RN Web rendering (web export) |
| react-native-web | 0.21.4 | One codebase on web (§2) |
| expo-router | 58.0.29 | Expo Router (§2) |
| expo-linking | 58.0.15 | expo-router required peer |
| expo-constants | 58.0.14 | expo-router required peer |
| expo-status-bar | 58.0.4 | from template; used in root layout |
| @expo/metro-runtime | 58.0.16 | expo-router required peer (web runtime) |
| react-native-screens | 4.30.1 | expo-router required peer |
| react-native-safe-area-context | 5.10.1 | expo-router required peer |

| devDependencies | version |
|---|---|
| typescript | 6.0.3 |
| @types/react | 19.3.0 |

Do not add expo-router's optional peers (reanimated, gesture-handler, worklets, expo-symbols, @expo/ui). Transitive dependencies resolved by npm are fine. Commit-ready `package-lock.json` must exist (CI uses `npm ci`).

### 3.3 Container images (not packages; listed for transparency)

`postgres:17-alpine` (tests + compose) · `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0` (Dockerfile) · `testcontainers/ryuk` (pulled automatically by Testcontainers).

---

## 4. Repository layout (everything Marco creates)

```
/global.json
/Directory.Build.props
/Directory.Packages.props
/Ecommerce.slnx
/.config/dotnet-tools.json
/compose.yaml
/Dockerfile
/.dockerignore
/.github/workflows/ci.yml
/src/Ecommerce.Domain/Ecommerce.Domain.csproj
/src/Ecommerce.Domain/DomainAssembly.cs
/src/Ecommerce.Application/Ecommerce.Application.csproj
/src/Ecommerce.Application/ApplicationAssembly.cs
/src/Ecommerce.Application/Abstractions/IClock.cs
/src/Ecommerce.Infrastructure/Ecommerce.Infrastructure.csproj
/src/Ecommerce.Infrastructure/DependencyInjection.cs
/src/Ecommerce.Infrastructure/Persistence/DatabaseOptions.cs
/src/Ecommerce.Infrastructure/Persistence/EcommerceDbContext.cs
/src/Ecommerce.Infrastructure/Persistence/DesignTimeDbContextFactory.cs
/src/Ecommerce.Infrastructure/Persistence/PostgresReadinessHealthCheck.cs
/src/Ecommerce.Infrastructure/Persistence/HealthCheckTags.cs
/src/Ecommerce.Infrastructure/Persistence/Migrations/<timestamp>_InitialBaseline.cs (+ .Designer.cs, EcommerceDbContextModelSnapshot.cs — tool-generated, not hand-edited)
/src/Ecommerce.Infrastructure/Time/SystemClock.cs
/src/Ecommerce.Api/Ecommerce.Api.csproj
/src/Ecommerce.Api/Program.cs
/src/Ecommerce.Api/Observability/CorrelationIdMiddleware.cs
/src/Ecommerce.Api/Observability/CorrelationId.cs          (constants + validation)
/src/Ecommerce.Api/Observability/SerilogSetup.cs           (AddEcommerceSerilog / request-logging options)
/src/Ecommerce.Api/Health/HealthEndpoints.cs               (MapEcommerceHealthEndpoints)
/src/Ecommerce.Api/appsettings.json
/src/Ecommerce.Api/appsettings.Development.json
/src/Ecommerce.Api/Properties/launchSettings.json
/tests/Ecommerce.Domain.Tests/...
/tests/Ecommerce.Application.Tests/...
/tests/Ecommerce.Api.IntegrationTests/...
/apps/store/...                                             (§10)
```

Module folders (`Catalog/`, `Orders/`, …) are **not** created empty; each slice adds its own. Edits to existing files: `docs/ecommerce-architecture.md` (§11 text only) and `README.md` (Local development section only). Do not touch `.gitignore`. It already covers `bin/ obj/ artifacts/ .env node_modules/ .expo/ dist/ apps/store/android/ apps/store/ios/ expo-env.d.ts`.

---

## 5. Solution, projects, references

Create with the CLI (`dotnet new sln --name Ecommerce --format slnx`, `dotnet new classlib|webapi|...`), then trim the template noise: delete `Class1.cs`, the `WeatherForecast` sample, `.http` files, and any OpenAPI registration plus its package reference (the `webapi` template adds `Microsoft.AspNetCore.OpenApi`, which is **not** approved). Prefer `dotnet new web` for the Api to avoid that.

**`Directory.Build.props`:** `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`. Do not set `TreatWarningsAsErrors` (quality bar: warnings are review notes until Slice 7).

**`Directory.Packages.props`:** `ManagePackageVersionsCentrally=true`, and `PackageVersion` entries exactly as in §3.1.

| Project | SDK | References |
|---|---|---|
| Ecommerce.Domain | Microsoft.NET.Sdk | none |
| Ecommerce.Application | Microsoft.NET.Sdk | Domain |
| Ecommerce.Infrastructure | Microsoft.NET.Sdk | Application; packages per §3.1 |
| Ecommerce.Api | Microsoft.NET.Sdk.Web | Application, Infrastructure (composition root only); Serilog.AspNetCore; `<UserSecretsId>` from `dotnet user-secrets init` |
| Ecommerce.Domain.Tests | Microsoft.NET.Sdk, `OutputType Exe` (xunit v3) | Domain; test packages |
| Ecommerce.Application.Tests | same | Application; test packages |
| Ecommerce.Api.IntegrationTests | same | Api; test packages + Mvc.Testing + Testcontainers.PostgreSql |

Direction: Api → Application → Domain; Infrastructure → Application. **Api may use Infrastructure only via `AddInfrastructure()` / `AddPostgresReadinessCheck()` and the `HealthCheckTags` constant.** Api must never inject `EcommerceDbContext`. Review checks this.

Marker types: `public static class DomainAssembly` / `ApplicationAssembly`, each with `public static Assembly Reference => typeof(X).Assembly;`. The architecture tests use them.

---

## 6. Backend design

### 6.1 Application

`Abstractions/IClock.cs`: `public interface IClock { DateTimeOffset UtcNow { get; } }`. This is the rule 9 abstraction (§3.2 D). Its only Slice 0 consumers are the DI test and future slices. This is intentional and not dead code.

### 6.2 Infrastructure

- **`DatabaseOptions`:** `ConnectionString` (string). Registered with `AddOptions<DatabaseOptions>().Configure<IConfiguration>((o, c) => o.ConnectionString = c.GetConnectionString("Postgres") ?? "")`, plus `.Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "ConnectionStrings:Postgres is not configured.")` and `.ValidateOnStart()`. The error message must never include the value.
- **`EcommerceDbContext`:** `public sealed`, primary-ctor `DbContextOptions<EcommerceDbContext>`. `OnModelCreating` calls `ApplyConfigurationsFromAssembly(typeof(EcommerceDbContext).Assembly)`. No DbSets.
- **`AddInfrastructure(this IServiceCollection)`:**
  - the options above
  - `AddDbContext<EcommerceDbContext>((sp, o) => o.UseNpgsql(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString))`. Resolved **lazily** so `WebApplicationFactory` settings always win.
  - `services.AddSingleton<IClock, SystemClock>()`
  - No `EnableRetryOnFailure` (it conflicts with explicit transactions; Slice 2/4 decides).
  - No `EnableSensitiveDataLogging`.
- **`AddPostgresReadinessCheck(this IHealthChecksBuilder)`:** `AddCheck<PostgresReadinessHealthCheck>("postgres", HealthStatus.Unhealthy, tags: [HealthCheckTags.Ready], timeout: TimeSpan.FromSeconds(5))`.
- **`HealthCheckTags`:** `public const string Ready = "ready";`
- **`PostgresReadinessHealthCheck`:** injects `EcommerceDbContext`. The whole body is in a try/catch:
  - `if (!await db.Database.CanConnectAsync(ct))` → `Unhealthy("Postgres unreachable")`
  - `if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())` → `Unhealthy("Pending migrations")`
  - otherwise `Healthy()`
  - catch (except `OperationCanceledException` when `ct` is cancelled) → `Unhealthy("Postgres check failed", ex)`
  - Descriptions are fixed strings: no connection string, host, or exception text in the HTTP body. The default writer prints only the status word.
- **`DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EcommerceDbContext>`:** `UseNpgsql("Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce")`. This is the non-secret local Compose DB. `dotnet ef ... --connection` overrides it. It is never used at runtime.
- **`SystemClock`:** `public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow(); }`. This is the one allowed system-clock read, in Infrastructure.

### 6.3 Migration

```
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.14
dotnet tool run dotnet-ef migrations add InitialBaseline --project src/Ecommerce.Infrastructure --output-dir Persistence/Migrations
```

Generated `Up`/`Down` are empty. **Do not hand-edit generated migration files.** Apply locally (no secret in the string):

```
dotnet tool run dotnet-ef database update --project src/Ecommerce.Infrastructure --connection "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce"
```

### 6.4 Api `Program.cs` (order matters)

```
builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; });   // all environments
builder.Services.AddEcommerceSerilog(builder.Configuration, builder.Environment);   // D9
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    { if (ctx.HttpContext.Items[CorrelationId.ItemKey] is string id) ctx.ProblemDetails.Extensions["correlationId"] = id; });
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks().AddPostgresReadinessCheck();

app.UseMiddleware<CorrelationIdMiddleware>();   // 1 — outermost, so every log line and error carries the ID
app.UseSerilogRequestLogging(SerilogSetup.ConfigureRequestLogging);   // 2
app.UseExceptionHandler();                      // 3 — Problem Details, no stack trace outside Development
app.UseStatusCodePages();                       // 4 — empty 4xx/5xx bodies become Problem Details
app.MapEcommerceHealthEndpoints();              // /health/live, /health/ready
app.Run();

public partial class Program;                   // for WebApplicationFactory
```

**`AddEcommerceSerilog`:**

```
services.AddSerilog((sp, lc) => lc
    .ReadFrom.Configuration(configuration)      // MinimumLevel only
    .ReadFrom.Services(sp)                      // lets tests inject an ILogEventSink
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Ecommerce.Api")
    .WriteTo.Console(...),                      // see below
  preserveStaticLogger: true)
```

- Development console: `outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}"`.
- Elsewhere: `new RenderedCompactJsonFormatter()`.

**`ConfigureRequestLogging`:** `GetLevel` returns:
- Error if there is an exception or status ≥ 500
- Debug if the path starts with `/health` and status < 400
- Information otherwise

Do not add headers or query strings to the diagnostic context.

**`appsettings.json`:**

```json
{
  "ConnectionStrings": { "Postgres": "" },
  "Serilog": { "MinimumLevel": { "Default": "Information", "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning", "System.Net.Http.HttpClient": "Warning" } } },
  "AllowedHosts": "*"
}
```

**`appsettings.Development.json`:** `"ConnectionStrings": { "Postgres": "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce" }`, plus `"Serilog": { "MinimumLevel": { "Default": "Debug" } }`.

**`launchSettings.json`:** a single `http` profile, `applicationUrl` `http://localhost:5080`, `ASPNETCORE_ENVIRONMENT=Development`, `launchBrowser: false`. No https profile.

### 6.5 Correlation ID middleware

- `CorrelationId` constants: `HeaderName = "X-Correlation-ID"`, `ItemKey = "CorrelationId"`, `LogProperty = "CorrelationId"`, `MaxLength = 64`.
- `CorrelationId.IsValid(string?)` checks length 1–64 and `^[A-Za-z0-9._:-]+$` (precompiled/`GeneratedRegex`).
- Middleware flow:
  1. Read the header (first value only).
  2. If valid, use it; otherwise use `Guid.NewGuid().ToString("N")`.
  3. Set `context.Items[ItemKey]`.
  4. `context.Response.OnStarting` sets the response header.
  5. `using (LogContext.PushProperty(LogProperty, id)) await next(context);`
- Never log the rejected inbound value.

### 6.6 Health endpoints

`MapEcommerceHealthEndpoints`:
- `/health/live`: `new HealthCheckOptions { Predicate = _ => false }`. This is process-only and never touches the DB.
- `/health/ready`: `Predicate = r => r.Tags.Contains(HealthCheckTags.Ready)`.

Both use the default writer (status word only) and default status mapping (Healthy/Degraded → 200, Unhealthy → 503). Both are anonymous. When auth arrives in Slice 1, they must stay `AllowAnonymous`; the Slice 1 plan will restate this.

---

## 7. Local run, `compose.yaml`, Dockerfile

**`compose.yaml`:**

```yaml
name: ecommerce
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: ecommerce
      POSTGRES_DB: ecommerce
      POSTGRES_HOST_AUTH_METHOD: trust      # local dev only, loopback-bound (D12)
    ports:
      - "127.0.0.1:5434:5432"
    volumes:
      - ecommerce-pg17-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ecommerce -d ecommerce"]
      interval: 5s
      timeout: 3s
      retries: 20
volumes:
  ecommerce-pg17-data:
```

**`Dockerfile`** (repo root, context = root):
- Build stage `mcr.microsoft.com/dotnet/sdk:10.0`:
  1. Copy `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `src/**/*.csproj`.
  2. `dotnet restore src/Ecommerce.Api/Ecommerce.Api.csproj`.
  3. Copy `src/`.
  4. `dotnet publish src/Ecommerce.Api/Ecommerce.Api.csproj -c Release -o /app/publish --no-restore`.
- Runtime stage `mcr.microsoft.com/dotnet/aspnet:10.0`: `WORKDIR /app`, copy the publish output, `USER $APP_UID`, `EXPOSE 8080`, `ENTRYPOINT ["dotnet","Ecommerce.Api.dll"]`.
- No secrets, no `ENV ConnectionStrings__*`.

**`.dockerignore`:** `**/bin`, `**/obj`, `.git`, `.vs`, `.vscode`, `.claude`, `apps/`, `docs/`, `tests/`, `slice-plans/`, `**/node_modules`, `**/*.local.json`, `.env*`, `artifacts/`.

---

## 8. Tests (all mandatory; exact names)

Conventions:
- Use xunit `Assert` only.
- Pass `TestContext.Current.CancellationToken` to async calls (xunit v3 analyzer).
- No `Skip`, no retries, no `Thread.Sleep`.

### 8.1 `Ecommerce.Domain.Tests`

| # | Test | Proves |
|---|---|---|
| D1 | `DomainArchitectureTests.Domain_references_no_project_or_infrastructure_assembly` | `DomainAssembly.Reference.GetReferencedAssemblies()` has no name starting `Ecommerce.`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `Npgsql`, `Serilog`, `Stripe`, `StackExchange.Redis` (§6.2 "Domain depends on nothing"). |

### 8.2 `Ecommerce.Application.Tests`

| # | Test | Proves |
|---|---|---|
| A1 | `ApplicationArchitectureTests.Application_references_only_Domain_among_project_assemblies` | `Ecommerce.*` references ⊆ {`Ecommerce.Domain`}; none of the framework prefixes in D1. |
| A2 | `ClockRuleTests.Domain_and_Application_source_never_read_the_system_clock` | Walks up from `AppContext.BaseDirectory` to the folder containing `Ecommerce.slnx`, then scans `src/Ecommerce.Domain/**/*.cs` and `src/Ecommerce.Application/**/*.cs` (excluding `bin/` and `obj/`) for `DateTime.UtcNow`, `DateTime.Now`, `DateTime.Today`, `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`, `TimeProvider.System`. Zero hits (rule 9). It fails with file:line on a hit, and fails if the solution root isn't found (it must not pass vacuously). It also asserts that at least one `.cs` file was scanned. |

### 8.3 `Ecommerce.Api.IntegrationTests`

**Infrastructure:**
- **`PostgresFixture`:** an xunit v3 **assembly fixture** (`[assembly: AssemblyFixture(typeof(PostgresFixture))]`) with one `postgres:17-alpine` container per test run. Use whichever **non-obsolete** Testcontainers 4.16.0 builder API sets the image; do not suppress obsolete warnings. `CreateDatabaseAsync(ct)` runs `CREATE DATABASE "t_<guid N>"` on the admin connection and returns the container connection string with `Database` swapped (via `NpgsqlConnectionStringBuilder`). Each test gets its own database, so test classes can run in parallel. Nothing is ever dropped.
- **`ApiFactory : WebApplicationFactory<Program>`:** constructed with a connection string and an optional `Action<IServiceCollection>`. Sets `UseEnvironment("Testing")` and `UseSetting("ConnectionStrings:Postgres", cs)`, and applies the action in `ConfigureTestServices`.
- `Unreachable` connection string constant: `Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=2`.
- **`CollectingSink : ILogEventSink`:** a thread-safe list, registered with `services.AddSingleton<ILogEventSink>(sink)`. It's picked up by `ReadFrom.Services`.

| # | Test | Setup → Assert |
|---|---|---|
| I1 | `MigrationTests.Migrations_apply_cleanly_to_an_empty_postgres_17_database` | Fresh DB → `MigrateAsync` through a scope from the factory's `Services` → `GetPendingMigrationsAsync` empty; applied list contains an id ending `_InitialBaseline`; `SHOW server_version` starts with `17.` (stack-lock guard). |
| I2 | `HealthTests.Live_returns_200_without_touching_the_database` | Unreachable conn → `GET /health/live` → 200, body `Healthy`. |
| I3 | `HealthTests.Ready_returns_200_when_postgres_is_up_and_migrated` | Fresh DB + `MigrateAsync` → `GET /health/ready` → 200, body `Healthy`. **The Slice 0 done-when, automated.** |
| I4 | `HealthTests.Ready_returns_503_when_postgres_is_unreachable` | Unreachable conn → 503; body is exactly `Unhealthy` (no host, port, or exception text). |
| I5 | `HealthTests.Ready_returns_503_when_migrations_are_pending` | Fresh DB, **not** migrated → 503. |
| I6 | `StartupTests.Startup_fails_fast_when_connection_string_is_missing` | `UseSetting("ConnectionStrings:Postgres", "")` → creating the client throws; the exception (or an inner one) message contains `ConnectionStrings:Postgres is not configured`. |
| I7 | `StartupTests.Container_resolves_IClock_as_SystemClock` | Factory `Services.GetRequiredService<IClock>()` is `SystemClock`; `ValidateOnBuild` passed (host built). |
| I8 | `CorrelationIdTests.Generates_and_echoes_id_when_header_absent` | `GET /health/live` → response `X-Correlation-ID` present and matches `^[a-f0-9]{32}$`. |
| I9 | `CorrelationIdTests.Echoes_valid_inbound_id` | Send `abc-123._:XYZ` → same value echoed. |
| I10 | `CorrelationIdTests.Replaces_invalid_inbound_id` (Theory) | Inputs: 65 × `a`; `has space`; `semi;colon` → echoed value ≠ input and matches `^[a-f0-9]{32}$`. |
| I11 | `CorrelationIdTests.Request_log_event_carries_the_correlation_id` | With `CollectingSink`, send header `log-check-1` to `GET /health/live` → at least one event has property `CorrelationId` = `"log-check-1"` and `RequestPath` = `/health/live`. |
| I12 | `ProblemDetailsTests.Unknown_route_returns_problem_details_with_trace_and_correlation_ids` | `GET /does-not-exist` with header `pd-check-1` → 404, `Content-Type` `application/problem+json`, JSON has `status` 404, a non-empty `traceId`, `correlationId` = `pd-check-1`, and no `exception`/`stack` members. |

Total: **15 tests** (I10 counts as one theory with 3 cases).

---

## 9. CI — `.github/workflows/ci.yml` (file only; nothing is pushed or run by agents)

```yaml
name: CI
on:
  push:
    branches: [master]
  pull_request:
permissions:
  contents: read
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  secret-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - name: Scan tracked files for secrets
        run: |
          pattern='sk_(live|test)_[A-Za-z0-9]{10,}|rk_(live|test)_[A-Za-z0-9]{10,}|whsec_[A-Za-z0-9]{10,}|-----BEGIN ([A-Z]+ )?PRIVATE KEY-----|[Pp]assword=[^<;"[:space:]]'
          if git grep -nE "$pattern"; then
            echo "::error::Possible secret found (see matches above)"; exit 1
          fi

  backend:
    needs: secret-scan
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore Ecommerce.slnx
      - run: dotnet build Ecommerce.slnx --no-restore -c Release
      - name: Test (unit + Testcontainers integration)
        run: dotnet test Ecommerce.slnx --no-build -c Release
      - run: dotnet tool restore
      - name: Migration drift check
        run: dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Ecommerce.Infrastructure --configuration Release --no-build
      - name: Idempotent migration script
        run: dotnet tool run dotnet-ef migrations script --idempotent --project src/Ecommerce.Infrastructure --configuration Release --no-build --output artifacts/migrations/migrate.sql
      - name: Docker image builds (not pushed)
        run: docker build -t ecommerce-api:ci .

  frontend:
    needs: secret-scan
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: apps/store
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-node@v7
        with:
          node-version: '24'
          cache: npm
          cache-dependency-path: apps/store/package-lock.json
      - run: npm ci
      - run: npx expo install --check
      - run: npx tsc --noEmit
      - run: npx expo export --platform web
```

The secret-scan regex is self-safe: it cannot match its own source text. No deploy job, no secrets referenced, no `workflow_dispatch` deploys.

---

## 10. Expo app — `apps/store`

**Scaffold:**
1. `npm pack expo-template-blank-typescript@58.0.31 --pack-destination <scratchpad>`.
2. Extract it; copy `package/*` into `apps/store/`, renaming `gitignore` to `.gitignore`. Drop `LICENSE`, `App.tsx`, and `index.ts`.
3. Rewrite `package.json`:
   - `"name": "store"`, `"private": true`, `"version": "0.1.0"`, `"main": "expo-router/entry"`
   - dependencies and devDependencies **exactly** as in §3.2
   - scripts: `start`, `android`, `ios`, `web` (from the template), plus `"typecheck": "tsc --noEmit"` and `"export:web": "expo export --platform web"`
4. Run `npm install` from `apps/store`. Then `npx expo install --check` must report no mismatches; if it does, stop and report (do not "fix" to other versions).

**`app.json`** (edit the template's):
- `name` "Ecommerce Store", `slug` "ecommerce-store", `scheme` "ecommerce-store", `version` "0.1.0", `userInterfaceStyle` "automatic"
- `web`: `{ "bundler": "metro", "output": "single", "favicon": "./assets/favicon.png" }`
- `plugins`: `["expo-router"]`
- Keep the template icon, adaptive-icon, and `ios.supportsTablet` entries.
- **No** `ios.bundleIdentifier`, `android.package`, `owner`, `extra.eas`, `updates`, or `experiments`.

**`tsconfig.json`:** the template's (`extends expo/tsconfig.base`, `strict: true`), plus `"include": ["**/*.ts", "**/*.tsx", "expo-env.d.ts", ".expo/types/**/*.ts"]`.

**Files:**

```
apps/store/components/ScreenStub.tsx   // props: { title: string; detail?: string } — renders <Stack.Screen options={{ title }} />, a header Text (accessibilityRole="header"), detail text, and "Not built yet."; RN View/Text/StyleSheet only
apps/store/app/_layout.tsx             // <StatusBar style="auto" /> + <Stack />
apps/store/app/+not-found.tsx          // ScreenStub "Not found" + <Link href="/">Go home</Link>
apps/store/app/index.tsx               // ScreenStub "Home" + Links to /products, /products/example, /cart, /checkout, /orders, /orders/example, /login, /register, /reset-password (NO admin links — §5.3)
apps/store/app/products/index.tsx      // "Products"
apps/store/app/products/[slug].tsx     // "Product" — detail shows slug from useLocalSearchParams<{ slug: string }>()
apps/store/app/cart.tsx                // "Cart"
apps/store/app/checkout.tsx            // "Checkout"
apps/store/app/orders/index.tsx        // "Orders"
apps/store/app/orders/[id].tsx         // "Order" — detail shows id
apps/store/app/login.tsx               // "Log in"
apps/store/app/register.tsx            // "Register"
apps/store/app/reset-password.tsx      // "Reset password"
apps/store/app/admin/index.tsx         // "Admin dashboard"
apps/store/app/admin/products/index.tsx   // "Admin · Products"
apps/store/app/admin/products/[id].tsx    // "Admin · Product" — detail shows id
apps/store/app/admin/inventory.tsx        // "Admin · Inventory"
apps/store/app/admin/orders/index.tsx     // "Admin · Orders"
apps/store/app/admin/orders/[id].tsx      // "Admin · Order" — detail shows id
apps/store/app/admin/returns.tsx          // "Admin · Returns"
apps/store/app/admin/customers/index.tsx  // "Admin · Customers"
apps/store/app/admin/customers/[id].tsx   // "Admin · Customer" — detail shows id
```

**Rules:**
- Default-export function components.
- Touch targets ≥ 44pt for Links (padding).
- No `Platform.OS` anywhere (rule 17).
- No `fetch`, no money, no state libraries.
- Admin route guarding is **Slice 1** (requires auth). Slice 0 admin stubs render no data.

---

## 11. Doc and README updates Marco makes (exact intent)

1. **`docs/ecommerce-architecture.md` §10.3**, the health bullet becomes:
   > `/health/live` (process only) and `/health/ready` (Postgres reachable **and** no pending migrations → 200, else 503). Redis, once added (Slice 7), reports `Degraded` only — still 200 — because Redis is never truth (§10.2, rule 4). Traffic routes on Ready.
2. **§13.2** add three rows (Decision | Chose | Gave up | Why):
   - *Readiness vs Redis* | Postgres + migrations gate Ready; Redis only degrades | Early warning that cache is down via readiness | A Redis outage must not pull traffic when cache-aside still serves correct data
   - *DbContext layout* | One `EcommerceDbContext`, one migration stream, module configurations by folder | Compiler-enforced per-module persistence | §8.2 needs inventory + order + payment + outbox in one transaction; boundaries enforced by project references + review
   - *Local Postgres 17* | Docker only (Testcontainers + Compose on 127.0.0.1:5434, trust auth) | Native-DB speed | Stack lock is PG17; CI and local use the same engine; no secret in local config
3. **`README.md` "Local development"** section only. Replace the commands with the Slice 0 reality:
   - Prerequisites (keep)
   - `docker compose up -d postgres` (Postgres 17 only; Redis arrives in Slice 7)
   - `dotnet tool restore`
   - the `database update` command from §6.3
   - `dotnet run --project src/Ecommerce.Api --launch-profile http`
   - `curl http://localhost:5080/health/ready`
   - `cd apps/store && npm install && npx expo start`
   - Keep the user-secrets and `stripe listen` lines, labelled "from Slice 4".
   - Tests line unchanged.
   - Change the Status line to "Slice 0 (Foundation) in review".
4. Do **not** edit `docs/quality-bar.md`, `CLAUDE.md`, `.gitignore`, or `docs/ecommerce-app-context.md`.

---

## 12. Build order and verification (Marco pastes real output for each)

1. **Docker:** `docker info`. If it fails: `Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"`, then poll `docker info` every 5 s for up to 180 s. Still failing → stop, report BLOCKED (D2).
2. Backend scaffold (§5–6), then the migration (§6.3).
3. Tests (§8).
4. `dotnet build` (repo root) → 0 errors. List warnings.
5. `dotnet test` (repo root) → 15 passed, 0 failed, 0 skipped.
6. `dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Ecommerce.Infrastructure` → "No changes".
7. **Manual done-when:**
   - `docker compose up -d postgres` → wait healthy (`docker compose ps`).
   - Start the API (`dotnet run --project src/Ecommerce.Api --launch-profile http`, background).
   - `curl -i http://localhost:5080/health/ready` **before** migrating → expect 503.
   - Run `database update` (§6.3).
   - `curl -i http://localhost:5080/health/ready` → **200 `Healthy`** with an `X-Correlation-ID` header.
   - `curl -i http://localhost:5080/health/live` → 200.
   - Stop the API. Leave Postgres running or `docker compose stop` (never `down -v`).
8. `docker build -t ecommerce-api:local .` → succeeds (not pushed).
9. Expo (§10): `cd apps/store && npx expo install --check`, `npx tsc --noEmit`, `npx expo export --platform web` → all succeed; export lists the routes.
10. **Local secret scan:** `git grep --untracked -nE '<pattern from §9>'` → no matches (read-only git command; allowed).
11. Quality-bar §2 self-check, all items answered. N/A items need reasons:
    - "money/stock tests": N/A, no money or stock in Slice 0
    - "outbox": N/A, no external state change
    - "server-side totals": N/A

Fix rounds: **max 2**. If Docker or an image pull fails for environmental reasons, that is BLOCKED, not a fix round.

---

## 13. Risks (open)

| Risk | Impact | Mitigation |
|---|---|---|
| Docker Desktop 26.1.4 engine won't start (WSL2/Hyper-V state), or is in Windows-containers mode | `dotnet test`, compose, and `docker build` all fail → BLOCKED | D2: start it once; no fallback. Nathaniel may need to start it or update Docker himself. Linux-containers mode required. |
| Image pulls (`postgres:17-alpine`, `testcontainers/ryuk`, dotnet sdk/aspnet ≈ 1 GB) slow or blocked | First test run slow or time-out | Pre-pull with `docker pull postgres:17-alpine` before `dotnet test`. |
| xunit v3 3.2.2 + Test SDK 18.11.0 + .NET 10 VSTest discovery mismatch | 0 tests found | Pre-approved fallback Test.Sdk 17.14.1 (§3.1). Any other change → report, don't improvise. |
| Expo SDK 58 / RN 0.88 / TypeScript 6.0 are recent | Template or tsc incompatibilities | Exact pins; `expo install --check`; stop and report on mismatch rather than swapping versions. |
| CI workflow not executable until pushed | YAML or step errors surface only on Nathaniel's first push | Workflow mirrors commands Marco runs locally; Rob reviews line by line. Flagged in the final report. |
| Ubuntu runner and `sdk:10.0` image SDK band vs `global.json` | Restore fails in CI or Docker | `10.0.100` + `latestFeature` (D7); `docker build` is verified locally. |
| Windows connect-refused on 127.0.0.1:1 takes ~2 s | I2/I4 slower | `Timeout=2` in the string + 5 s check timeout keeps it bounded. |
| Single DbContext lets future code reach across modules | Rule 8 drift | Application never references Infrastructure; Slice 1 plan decides naming/schemas and adds a boundary test once module tables exist. |
| Microsoft packages 10.0.14 vs local runtime 10.0.9 | Unlikely binding issue | Same major/minor assembly versions; tests run on the local runtime will show it. |

**Carried forward (not Slice 0, not blocking):** Slice 4/5 doc issues from the earlier review: retry-payment double charge, expiry-vs-webhook race, test 7 wording.

---

## 14. What Rob will check in review

- Stack and dependencies: only §3 packages, exact versions, nothing extra (including in `package-lock.json` top level).
- Slice order: Slice 0. The previous-slice gate is N/A (D4).
- Boundaries:
  - Domain and Application have no outward references (D1/A1 tests + csproj read).
  - Api never injects the DbContext.
  - No logic in `Program.cs` beyond composition.
- Data: migration is tool-generated and empty; no auto-migrate on startup.
- Security:
  - nothing secret anywhere; no connection string in logs or health bodies
  - correlation input validated
  - no stack traces outside Development
  - Compose bound to loopback only
- Frontend:
  - every §5 route exists
  - RN components only
  - no `Platform.OS`, no admin links on home
  - no `android/` or `ios/` folders
- Tests: all 15 exist with the names above and pass; none skipped.
- Docs: §10.3 and §13.2 updated as in §11; README section updated.
- I re-run `dotnet build`, `dotnet test`, `tsc --noEmit`, `expo export --platform web`, and the secret scan myself.
