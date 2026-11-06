# Slice 0 — Foundation: Rob's review

Mode: Review · Date: 2026-11-02 · Fix round: 1 of 2 · **Verdict: BLOCKED** (one bug)

## Blocking failures

1. **Serilog request-completion events are never written (bug).**
   - Where: `src/Ecommerce.Api/Program.cs:29` (`app.UseSerilogRequestLogging(SerilogSetup.ConfigureRequestLogging)`) together with `src/Ecommerce.Api/Observability/SerilogSetup.cs:31` (`preserveStaticLogger: true`).
   - Cause: `RequestLoggingOptions.Logger` defaults to the static `Serilog.Log` (Serilog.AspNetCore 10.0.0 XML doc: "The default is to use the static Serilog.Log class"). D9 keeps the static logger silent, so every `HTTP {Method} {Path} responded {StatusCode}` event is dropped. This is my plan's gap, but it is still a bug.
   - Evidence: I ran the API in Development (minimum level Debug) and sent `/health/ready`, `/health/live`, and `/nope` (404, which should log at Information). The console showed host and health-check Debug lines with the correlation ID, and **zero** request-completion lines (`grep -c HTTP` returned 0).
   - Fix: point the options at the DI Serilog logger that `AddSerilog` registers. For example, in `Program.cs`: `app.UseSerilogRequestLogging(o => { SerilogSetup.ConfigureRequestLogging(o); o.Logger = app.Services.GetRequiredService<Serilog.ILogger>(); });`. Any equivalent works if it keeps `preserveStaticLogger: true` and does not use static `Log.*`.

2. **I11 is a false positive, so this is also a missing test.**
   - Where: `tests/Ecommerce.Api.IntegrationTests/CorrelationIdTests.cs:73-90`.
   - Cause: I11 matches any event that has `CorrelationId` and `RequestPath`. ASP.NET's hosting scope adds `RequestPath` to *every* event, so the `DefaultHealthCheckService` Debug events satisfy it. That is why it only passed after Marco set the minimum level to Debug.
   - Fix:
     - Narrow the I11 predicate to the request-completion event: `SourceContext == "Serilog.AspNetCore.RequestLoggingMiddleware"` and the event has a `StatusCode` property. Keep the Debug setting; it is legitimate, see decision 3.
     - Add **I13** `CorrelationIdTests.Unknown_route_request_log_is_information_with_correlation_id`. With no level override, `GET /does-not-exist` with header `log-check-2` must produce exactly one request-completion event at `Information`, with `CorrelationId` = `log-check-2` and `StatusCode` = 404. This proves the default-level path that production actually uses.
   - Both tests must fail before fix 1 and pass after it.

## Bugs found

The request-logging bug and the I11 false positive above. Nothing else.

## Decisions on Marco's open items

1. **§8.8.3 health wording.** No action is needed. The working-tree architecture doc has no "Postgres + Redis reachable" line. That text existed only in the committed HEAD version, and Marco's §10.3 replacement (line 482) already supersedes it. I did not edit the doc.
2. **11 moderate npm audit findings: defer to Slice 7.** They are all in Expo build-time tooling (`glob`, `minimatch`, `@expo/config-plugins`, `@expo/fingerprint`, `@expo/metro-config`). The only "fix" npm offers is `--force` to expo 51, which would break the SDK 58 lock. Quality-bar §6 requires known-vulnerable packages to be fixed "before release", and Slice 7 is the release-hardening slice. This is tracked as an open risk.
3. **I11 settings parameter: accepted.** Health requests at Debug is intended (D9 / plan §6.4: probe noise). A test-only level override is the right way to observe it. The `ApiFactory` `settings` parameter is a clean seam. The assertion itself must be narrowed (blocking item 2).

## Checked and fine

- **Stack and dependencies:** only the plan §3 packages, exact pins, CPM. `package-lock.json` root dependencies and devDependencies match §3.2 exactly. Expo routes cover all of app-context §5. No `Platform.OS`, no `fetch`, no admin links on Home, no `android/` or `ios/` folders.
- **Boundaries:** Domain and Application reference nothing outward. Api touches Infrastructure only via `AddInfrastructure`, `AddPostgresReadinessCheck`, and `HealthCheckTags`. The migration is tool-generated and empty, and there is no auto-migrate on startup.
- **Security:**
  - Correlation ID is validated and rejected values are not logged.
  - Health bodies are the status word only.
  - The 404 Problem Details response has `traceId` and `correlationId` and no stack trace.
  - Compose is bound to loopback.
  - The secret scan is clean.
- **Docs:** §10.3, the three §13.2 rows, and the README Local development section and Status line are as planned.
- **Marco's deviations:** `Using Xunit`, `StubLink`, the `.config` manifest move, and ScreenStub `children` are all acceptable.

## Non-blocking notes

- `src/Ecommerce.Api/Ecommerce.Api.csproj:1` has a BOM and mangled formatting (`<Project ...><PropertyGroup>  <UserSecretsId>`). Reformat it during the fix.
- D1/A1 use `GetReferencedAssemblies()`, which lists only the assemblies that are actually used. The tests are still valid for this rule, but csproj review remains the primary guard.
- I5 doesn't assert the body. Optionally add `Unhealthy`.
- Startup logs the EF warning "No instantiatable types implementing IEntityTypeConfiguration". It goes away in Slice 1.

## Risks still open

- The CI workflow has never run until it is pushed.
- The npm audit findings are deferred to Slice 7.
- Docker Desktop 26.1.4 is old.

## Commands (run by Rob)

- `docker info` → `26.1.4 linux`
- `dotnet build` → `Build succeeded. 0 Warning(s) 0 Error(s)`
- `dotnet test`:
  - Domain: Passed 1/1
  - Application: Passed 2/2
  - Api.IntegrationTests: Passed 14/14
  - 0 failed, 0 skipped
- `npx tsc --noEmit` (apps/store) → exit 0, no output
- Secret scan (`git grep --untracked`, plan §9 pattern) → no matches, rc=1
- Manual run against compose PG17:
  - `/health/ready` → `200 Healthy`, `X-Correlation-ID` present
  - `/health/live` → 200
  - `/nope` → 404 `application/problem+json` with `traceId` and `correlationId: rob-1`
  - Afterwards the API process was stopped and `docker compose stop` was run (container exited, volume kept).
- `npm audit` → 11 moderate, all transitive Expo tooling.

## Re-review after fix round 1

**Verdict: PASS**

- Blocking 1 (silent static logger): fixed. `src/Ecommerce.Api/Program.cs` `UseSerilogRequestLogging` applies `SerilogSetup.ConfigureRequestLogging(o)` and sets `o.Logger = app.Services.GetRequiredService<Serilog.ILogger>()`; `preserveStaticLogger: true` kept, no static `Log.*` use.
- Blocking 2 (I11 passing for the wrong reason): fixed. `CorrelationIdTests.cs:100-103` `IsRequestCompletion` requires SourceContext `Serilog.AspNetCore.RequestLoggingMiddleware` and a `StatusCode` property. I13 (`:81-98`) has no level override, asserts 404, exactly one completion event at Information, with CorrelationId `log-check-404` and StatusCode 404. Marco's report shows both tests failing against the old code first.
- Scope: only Program.cs and CorrelationIdTests.cs changed under src/tests since the first review (mtime check). Nothing new introduced.
- Commands: `dotnet build` -> 0 Warning(s), 0 Error(s). `dotnet test` -> Domain 1/1, Application 2/2, Api.IntegrationTests 15/15, Failed 0.
- Live check skipped (optional); I13 covers it in-process through the real pipeline.
- Bugs: None. Non-blocking: None new.
