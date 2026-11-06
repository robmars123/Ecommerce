# Slice 0 — Foundation: Marco's build report

Verdict from the builder: **PASS (all plan commands green)**. Fix rounds used: **0 formal rounds** (two build-time compile errors and one test failure were corrected during the first build pass; details below). Nothing committed. Review by Rob is still pending.

## Concerns

1. **Architecture §8.8.3 still has the old health wording.** The line "Health endpoints: `/health/live` (process up) and `/health/ready` (Postgres + Redis reachable)..." now contradicts the new §10.3 text and D3. The plan allowed only the §10.3 and §13.2 edits, so I left it. Rob or Nathaniel should align it.
2. **Plan gap in test I11.** The plan requires the request log event for `GET /health/live`, but `ConfigureRequestLogging` logs `/health*` at Debug, and the Testing environment minimum level is Information. The event was never emitted, so the test failed (see Bugs found). I did not change app logging. I gave `ApiFactory` an optional third parameter, `settings`, and I11 passes `Serilog:MinimumLevel:Default=Debug`. This keeps the health-log-level behaviour from the plan.
3. **Small deviations from the plan's file list (no new packages):**
   - `<Using Include="Xunit" />` added to the three test csprojs, because xunit.v3 has no implicit `using Xunit`.
   - Added `apps/store/components/StubLink.tsx`, a 44pt-high `Link` wrapper, so the touch-target rule is applied in one place. It's used by Home and Not-found.
   - `dotnet new tool-manifest` wrote `dotnet-tools.json` at the repo root, so I moved it to `.config/dotnet-tools.json` as the plan's layout says.
   - `ScreenStub` accepts optional `children` so Home and Not-found can render links.
4. **`npm install` reported 11 moderate vulnerabilities in transitive dependencies.** I did not run `npm audit fix` (it would change versions). Needs a decision before release (quality-bar §6).
5. **Test count wording.** The plan says "15 tests". There are 15 test methods (D1, A1, A2, I1–I12). I10 is a theory with 3 cases, so the runner reports 17 test cases (1 + 2 + 14).
6. **CI workflow (`.github/workflows/ci.yml`) is not executable locally.** It mirrors commands I ran, but the YAML is untested until pushed.
7. **Leftovers on this machine:** I started Docker Desktop (D2). The compose Postgres container is **stopped** (not removed) and its volume `ecommerce_ecommerce-pg17-data` is kept. A local image `ecommerce-api:local` exists. The Testcontainers Ryuk helper container was still running at the end and removes itself. The API process was stopped. I did not use the Windows PG16/PG18 services.
8. `git diff --stat` shows huge diffs for `.gitignore`, `docs/*`, and `README.md`. This is CRLF/LF noise that was already there before my run (git warns about LF-to-CRLF). My real edits to the docs are the ones listed under "What changed".

## Bugs found

- `tests/Ecommerce.Api.IntegrationTests/CorrelationIdTests.cs` — `Request_log_event_carries_the_correlation_id` failed on its first run: the sink only held the 3 host-startup events. Cause: the request log for `/health/live` is Debug level, but the Testing environment minimum is Information. (This is a plan gap, not a production bug.)
- Compile errors on first build: xunit attributes unresolved (no implicit using), and `ConfigureTestServices` unresolved (needed `using Microsoft.AspNetCore.TestHost;`).
- csproj `ProjectReference` paths were mangled by my shell script on the first attempt and were caught by the sln-add errors before any build.

## Bugs fixed

- I11 now passes: the factory takes an optional settings dictionary and the test sets `Serilog:MinimumLevel:Default=Debug`. Test I11 passes in the final `dotnet test`.
- Test csprojs got `<Using Include="Xunit" />`; `ApiFactory.cs` got `using Microsoft.AspNetCore.TestHost;`. The solution builds with 0 errors.
- Test csproj project references rewritten correctly.

No test was weakened or deleted. All 15 plan tests exist with the plan's names.

## Bugs open

None.

## What changed

**Slice:** 0 — Foundation only.

**Backend (new):**
`global.json`, `Directory.Build.props`, `Directory.Packages.props`, `Ecommerce.slnx`, `.config/dotnet-tools.json`, `compose.yaml`, `Dockerfile`, `.dockerignore`, `.github/workflows/ci.yml`

`src/Ecommerce.Domain` (csproj, `DomainAssembly.cs`)
`src/Ecommerce.Application` (csproj, `ApplicationAssembly.cs`, `Abstractions/IClock.cs`)
`src/Ecommerce.Infrastructure` (csproj, `DependencyInjection.cs`, `Persistence/{DatabaseOptions, EcommerceDbContext, DesignTimeDbContextFactory, PostgresReadinessHealthCheck, HealthCheckTags}.cs`, `Persistence/Migrations/20261103043930_InitialBaseline{.cs,.Designer.cs}` + `EcommerceDbContextModelSnapshot.cs` (tool-generated, empty Up/Down), `Time/SystemClock.cs`)
`src/Ecommerce.Api` (csproj with `UserSecretsId` from `dotnet user-secrets init`, `Program.cs`, `Observability/{CorrelationId, CorrelationIdMiddleware, SerilogSetup}.cs`, `Health/HealthEndpoints.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`)
`tests/Ecommerce.Domain.Tests` (csproj, `DomainArchitectureTests.cs`)
`tests/Ecommerce.Application.Tests` (csproj, `ApplicationArchitectureTests.cs`, `ClockRuleTests.cs`)
`tests/Ecommerce.Api.IntegrationTests` (csproj, `PostgresFixture.cs`, `ApiFactory.cs`, `CollectingSink.cs`, `MigrationTests.cs`, `HealthTests.cs`, `StartupTests.cs`, `CorrelationIdTests.cs`, `ProblemDetailsTests.cs`)

**Frontend (new, `apps/store`):** `package.json`, `package-lock.json`, `app.json`, `tsconfig.json`, `.gitignore`, `assets/` (from template), `components/ScreenStub.tsx`, `components/StubLink.tsx`, `app/_layout.tsx`, `app/+not-found.tsx`, `app/index.tsx`, and all 20 stub routes: `products/index`, `products/[slug]`, `cart`, `checkout`, `orders/index`, `orders/[id]`, `login`, `register`, `reset-password`, `admin/index`, `admin/products/index`, `admin/products/[id]`, `admin/inventory`, `admin/orders/index`, `admin/orders/[id]`, `admin/returns`, `admin/customers/index`, `admin/customers/[id]`.

**Docs (only the edits the plan assigned):**
- `docs/ecommerce-architecture.md`: §10.3 health bullet replaced; three rows added to §13.2 (Readiness vs Redis, DbContext layout, Local Postgres 17).
- `README.md`: "Local development" section rewritten; Status line set to "Slice 0 (Foundation) in review."

**Packages installed:**

- **NuGet** (versions pinned in `Directory.Packages.props`; all as in plan §3.1):
  - Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3
  - Microsoft.EntityFrameworkCore.Relational 10.0.14
  - Microsoft.EntityFrameworkCore.Design 10.0.14 (PrivateAssets=all)
  - Microsoft.Extensions.Diagnostics.HealthChecks 10.0.14
  - Serilog.AspNetCore 10.0.0
  - Microsoft.NET.Test.Sdk 18.11.0 (**the fallback 17.14.1 was NOT needed**; tests are discovered and run)
  - xunit.v3 3.2.2
  - xunit.runner.visualstudio 3.1.5
  - Microsoft.AspNetCore.Mvc.Testing 10.0.14
  - Testcontainers.PostgreSql 4.16.0
- **npm** (`apps/store`, exact versions, no `^`/`~`):
  - dependencies: expo 58.0.22, react 19.3.0, react-native 0.88.1, react-dom 19.3.0, react-native-web 0.21.4, expo-router 58.0.29, expo-linking 58.0.15, expo-constants 58.0.14, expo-status-bar 58.0.4, @expo/metro-runtime 58.0.16, react-native-screens 4.30.1, react-native-safe-area-context 5.10.1
  - devDependencies: typescript 6.0.3, @types/react 19.3.0
  - The template `expo-template-blank-typescript@58.0.31` was only packed and unpacked (via `npm pack` into the scratchpad), not installed as a dependency.
  - Transitive packages resolved by npm (529 total installed).
- **.NET tools:** dotnet-ef 10.0.14 (local tool, `.config/dotnet-tools.json`)
- **Container images:** postgres:17-alpine (pulled), mcr.microsoft.com/dotnet/sdk:10.0 and mcr.microsoft.com/dotnet/aspnet:10.0 (pulled by `docker build`), testcontainers/ryuk (pulled by Testcontainers). Nothing else.

## Command results

**Docker:** `docker info` initially failed (engine pipe missing). I started Docker Desktop; engine was up 10 s later: `Server Version: 26.1.4`, `OSType: linux`.

**`dotnet build`**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**`dotnet test`** (real Postgres 17 via Testcontainers)
```
Passed!  - Failed: 0, Passed:  1, Skipped: 0, Total:  1, Duration: 17 ms - Ecommerce.Domain.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed:  2, Skipped: 0, Total:  2, Duration: 18 ms - Ecommerce.Application.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration:  2 s - Ecommerce.Api.IntegrationTests.dll (net10.0)
```
Total 17 cases passed (15 test methods; I10 theory = 3 cases), 0 failed, 0 skipped. The first run had 13 of 14 passing in the integration project; the one failure was I11 (see Bugs found).

**Migration drift check:** `dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Ecommerce.Infrastructure` → `No changes have been made to the model since the last migration.`

**Manual done-when against compose Postgres 17** (`127.0.0.1:5434`, container healthy):
```
GET /health/ready BEFORE migrate -> HTTP/1.1 503 Service Unavailable, body "Unhealthy", X-Correlation-ID present
dotnet-ef database update        -> Applying migration '20261103043930_InitialBaseline'. Done.
GET /health/ready AFTER migrate  -> HTTP/1.1 200 OK, body "Healthy", X-Correlation-ID: ac7a70d9db28412a8733dc6bb75e815f
GET /health/live                 -> HTTP/1.1 200 OK, body "Healthy"
```
API process stopped afterwards (`curl` to :5080 returns connection refused / 000). `docker compose stop` run (no `down`, no volume deletion).

**`docker build -t ecommerce-api:local .`** → succeeded (image `sha256:0ca3369b…`, "naming to docker.io/library/ecommerce-api:local"). Not pushed.

**`npx expo install --check`** (apps/store) → `Dependencies are up to date`

**`npx tsc --noEmit`** (apps/store) → no output, exit 0

**`npx expo export --platform web`** (apps/store) → exported to `dist` (gitignored); 24 web bundles including `[id]`, `[slug]`, `+not-found`, `cart`, `checkout`, `inventory`, `login`, `register`, `reset-password`, `returns`, `_layout`, `index`; ends with `Exported: dist`.

**Secret scan** (`git grep --untracked -nE '<plan §9 pattern>'`) → no matches (`rc=1`).

## Needs my decision

- Align architecture §8.8.3 (old "Postgres + Redis reachable" health wording) with the new §10.3 (Concern 1).
- Whether to deal with the 11 moderate `npm audit` findings now or in Slice 7 (Concern 4).
- Whether a debug-level override for I11 via an `ApiFactory` settings parameter is acceptable, or the plan wants the health request-log level changed instead (Concern 2).

## Quality-bar self-check, Section 2

- [x] Module boundaries respected — Domain/Application have no outward references (tests D1/A1 pass). Api uses Infrastructure only through `AddInfrastructure()`, `AddPostgresReadinessCheck()`, and `HealthCheckTags`; Api never injects `EcommerceDbContext`. No controllers, no DbSets, no logic in `Program.cs` beyond composition.
- [x] Required tests exist and pass — N/A for §11.2 tests (none belong to Slice 0 per §12.3). All 15 Slice 0 plan tests exist with the planned names and pass on real Postgres 17.
- [x] `IClock`, not `DateTime.UtcNow`, in domain and application code — `IClock` defined in Application, `SystemClock` is the only clock read (Infrastructure). Test A2 scans Domain/Application source and passes (non-vacuous).
- [ ] N/A: Prices and totals server-side — no money exists in Slice 0.
- [ ] N/A: Idempotency and outbox for external state changes — no external state change exists in Slice 0 (no payments, email, or stock).
- [x] Commands in §4 run, actual output pasted — `dotnet build`, `dotnet test`, `tsc --noEmit`, `expo export --platform web`, plus `expo install --check`, migration drift check, manual `/health/ready`, `docker build`, and the secret scan.
- [x] Open decisions left as config placeholders — no Stripe, email, domain, or bucket values invented. The only connection string is the non-secret local Compose one (trust auth, loopback). `appsettings.json` holds `"Postgres": ""` and startup fails fast if it is empty (test I6).
- [x] Docs updated — §10.3, §13.2 rows, README Local development and Status. Remaining mismatch flagged: §8.8.3 (Concern 1).
- [x] Every change explained in plain language — see "What changed" and Concerns.

## Fix round 1

**Concerns:** None.

**Bugs found:**
- src/Ecommerce.Api/Program.cs:28 — request logging wrote to static `Log.Logger` (silent with `preserveStaticLogger: true`), so no completion events were emitted. Caught by new I13 and the narrowed I11.
- tests/Ecommerce.Api.IntegrationTests/CorrelationIdTests.cs — I11 passed for the wrong reason (matched health-check Debug events).

**Bugs fixed:**
- Program.cs: `UseSerilogRequestLogging` now calls `SerilogSetup.ConfigureRequestLogging(o)` then sets `o.Logger = app.Services.GetRequiredService<Serilog.ILogger>()`. `preserveStaticLogger: true` kept; no static `Log.*` use. I11 and I13 now pass.
- I11 narrowed to SourceContext `Serilog.AspNetCore.RequestLoggingMiddleware` + a `StatusCode` property (helper `IsRequestCompletion`). Added I13 `Unknown_route_request_log_is_information_with_correlation_id` (no level override; 404; exactly one Information completion event with CorrelationId and StatusCode 404).

**What changed:**
- src/Ecommerce.Api/Program.cs
- tests/Ecommerce.Api.IntegrationTests/CorrelationIdTests.cs
- AGENT_STATUS.md
- Packages: No new packages. (Api.csproj BOM formatting not touched.)

**Command results:**
- Failing first (tests only, old code): `Failed! - Failed: 2, Passed: 13, Total: 15`. I13: "Assert.Single() Failure: The collection did not contain any matching items" (only Microsoft.Hosting.Lifetime events). I11: "Assert.Contains() Failure: Filter not matched in collection".
- After fix: `dotnet build` -> Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test`: Domain.Tests Passed 1/1; Application.Tests Passed 2/2; Api.IntegrationTests Passed 15/15, Failed 0.

**Needs my decision:** None.

**Quality-bar §2 deltas:** None; the logging checks now actually verify request completion events. No frontend changes, so tsc/expo are N/A.
