# Slice P2.66 — AdminInsights to Minimal API (plan, Rob)

Tier **High** (Admin-only revenue: P2.45 Support 403 on trend/export). Mode Autonomous. Tracker row P2.66 (`docs/admin-dashboard-feature-tracker.md:85`), master plan row P2.66 (`slice-plans/minimal-api-migration-plan.md:83`).

**Read:** master plan §2–§4 · this plan · `03-principles-hard-rules.md:40` (rule 7) · `06-backend-modules.md:63–69` · `Admin/Ecommerce.Admin/Api/AdminInsightsController.cs` · `Api/AdminDashboardEndpoints.cs` (the pattern; `MoneyResponse` lives there) · `Api/QueryValues.cs` (not called here, see I4) · `Api/TrendCsv.cs` · `AdminModule.cs:29–35` · `tests/Ecommerce.Api.IntegrationTests/Projections/AdminInsightsApiTests.cs` (all, 173 lines) · `Projections/AdminDashboardApiTests.cs:449–480` (P2.65 pin pattern, `AssertValidationProblem`) · `EndpointInventoryTests.cs:20–30`. Nothing else.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| I1 | New `Api/AdminInsightsEndpoints.cs`: one group from `app`: `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin)`, then `MapGet("dashboard/trend", Trend)` and `MapGet("dashboard/export", Export)`. Register with `AdminInsightsEndpoints.Map(app);` after `AdminDashboardEndpoints.Map(app);` in `AdminModule.MapAdminEndpoints` | Master D4/D5: sibling group from `app`, never from Dashboard's `AdminOrSupport` group (nested policies AND; reusing it would let Support in). RawText matches inventory lines 23, 25 | Hanging trend/export off Dashboard's group; `MapGroup("api/v1/admin/dashboard")` |
| I2 | Delete `AdminInsightsController.cs` (whole file, one class) in the same change | Master §4 step 2 (both mapped → ambiguous match) | — |
| I3 | Move `TrendBucketResponse` and `RevenueTrendResponse` byte-identical into the new file. `MoneyResponse` stays in `AdminDashboardEndpoints.cs` | Response shape unchanged; P5 pins it | Re-declaring or renaming records |
| I4 | `period` bound as `[FromQuery] string[]? period` in both handlers. One private helper `ReadPeriod(string[]? period)` → `DashboardPeriod?`: `string? raw = period?.FirstOrDefault(); return DashboardPeriod.Parse(string.IsNullOrWhiteSpace(raw) ? null : raw);` — **whitespace → null only if P1 shows MVC gives 200 `today` for `?period=%20`** (P2.65 proved it does for the same binder; P1 confirms here). Same one-line comment as Dashboard on why `string[]` | MVC binds the first value of a repeated key and turns blank/whitespace into null; Minimal API joins `string` as `"a,b"` | Plain `string?`; `QueryValues` (int only; adding a period reader there means editing P2.65's file for no new consumer) |
| I5 | Unknown period → one private helper `InvalidPeriod()` returning `TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["period must be one of: today, 7d, 30d."] })`. Text unchanged | Master D8; two callers in this file; P2 pins text + shape | Copying the dictionary into each handler |
| I6 | `Trend([FromQuery] string[]? period, [FromServices] RevenueTrendService trend, CancellationToken ct)` → `Results<Ok<RevenueTrendResponse>, ValidationProblem>`. Mapping copied verbatim from controller lines 32–34 | Master D3; zero mapping diff | Re-shaping |
| I7 | `Export([FromQuery] string[]? period, [FromServices] RevenueTrendService trend, [FromServices] IClock clock, CancellationToken ct)` → `Results<FileContentHttpResult, ValidationProblem>`; body = controller lines 46–48 with `TypedResults.File(Encoding.UTF8.GetBytes(TrendCsv.Write(view)), "text/csv; charset=utf-8", fileName)` | Same bytes (no BOM), same content type string, same `fileDownloadName` → same `Content-Disposition` (`attachment; filename=…; filename*=UTF-8''…`); P3 pins all three | `Results.Text` / writing to `HttpResponse` (drops the attachment header) |
| I8 | Filename date stays `clock.UtcNow` read in the handler (a second clock read after the service's) | Preserve behaviour exactly; switching to `view.To` is a fix, not a conversion (backlog NOTE) | `view.To` now |
| I9 | No change in `Application/`, `Infrastructure/`, `TrendCsv.cs`, `QueryValues.cs`, `AdminDashboardEndpoints.cs`, any `*.Contracts`, migrations, packages, docs | Pure transport conversion | Fixing the health N+1 here |
| I10 | No caching headers (controller has none) | Preserve | — |

No decision-log row: master D1–D9 cover the pattern.

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminInsightsEndpoints.cs` | **new**: two records (I3) + `internal static class AdminInsightsEndpoints` (`Map`, `Trend`, `Export`, `ReadPeriod`, `InvalidPeriod`) |
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminInsightsController.cs` | **delete** |
| `backend/src/Modules/Admin/Ecommerce.Admin/AdminModule.cs` | one line in `MapAdminEndpoints` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminInsightsApiTests.cs` | **append** P1–P5 under a `// ---- P2.66 pins … written on MVC before the Minimal API conversion ----` header; existing tests unedited |

`AdminDashboardController.cs` (holds `AdminProjectionsController`, P2.67) untouched.

## 3. Build order

1. **Baseline on MVC.** Run `AdminInsightsApiTests` + `EndpointInventoryTests` (filter), quote pass counts.
2. **Pins on MVC, test-first.** Add P1–P5 (§4); run on MVC and quote green. Any pin that is red on MVC: MVC is the spec — fix the pin's expectation to what MVC does, report it, never the code.
3. **Convert** (I1–I7): add the file, register it, delete the controller.
4. Same filter green, all existing tests unedited; full `bash scripts/verify.sh`, quote `VERIFY` line.
5. Mutations M1–M3 (§5), quote red for each, restore from scratch copy, `cmp`.

## 4. Required tests

| Behaviour | Test | Status |
|---|---|---|
| 401 anon, 403 Customer, 403 Support (no `amount` in body) — trend; 401/403/403 — export | `Trend_without_a_token…`, `Export_without_a_token…` | existing |
| Policy `Admin`, CORS `credentialed`, exact RawText | inventory lines 23, 25; II11 | existing |
| Unknown period 400 with `errors.period`, both routes | `Trend_and_export_with_an_unknown_period…` | existing |
| Hourly/daily buckets, Σ = KPI revenue, 30 buckets | I6/I7 existing facts | existing |
| CSV rows equal trend, media type, `attachment`, filename | `Export_7d_is_a_csv_attachment…` | existing |
| **P1** blank `?period=` and whitespace `?period=%20` → 200 `period: "today"` (trend); `?period=7d&period=bogus` → 200 `7d` (trend) and export filename `dashboard-7d-…`; `?period=bogus&period=7d` → 400 `errors.period` (both routes) | new | **test-first (green on MVC)** |
| **P2** unknown period, both routes: `application/problem+json`, `type` rfc9110 15.5.1, title "One or more validation errors occurred.", non-empty `traceId`, exact text `period must be one of: today, 7d, 30d.` (local copy of `AssertValidationProblem`) | new | green on MVC |
| **P3** export headers: raw `Content-Type` == `text/csv; charset=utf-8`; `ContentDisposition.FileNameStar` == the filename; first body byte is `b` (no BOM) | new | **test-first** |
| **P4** Support on export: 403 body has no `amount` and is not `text/csv` | new | green on MVC |
| **P5** trend property names exactly `asOf, buckets, currency, granularity, orderCount, period, total`; bucket `orders, revenue, start`; `total` and bucket `revenue` `amount, currency` | new | green on MVC |

## 5. Mutations (High) — break → red → restore from scratch copy → `cmp`

| # | Mutation (in `AdminInsightsEndpoints.cs`) | Expected red |
|---|---|---|
| M1 | group policy `Admin` → `AdminOrSupport` (master row) | `Trend_without_a_token…Support_is_403…`, `Export_without_a_token…` (Support 200) + inventory lines 23/25 |
| M2 | `period?.FirstOrDefault()` → `string.Join(",", period ?? [])` | P1 (`7d&bogus` → 400) |
| M3 | content type `"text/csv; charset=utf-8"` → `"text/csv"` | P3 |

No extras.

## 6. What Rob checks in review (name, then evidence)

- **Queries per request:** exactly **one** `await trend.GetAsync(...)` per handler; no other service, `DbContext` or loop in the file. Diff touches nothing under `Application/`/`Infrastructure/`, so SQL is unchanged by construction. Today per trend or export request: `ConsumerHealthReader.GetProjectorsAsync` = 3 × projectors (2 registered: Dashboard, CatalogSales) = **6**, plus `ReadRevenueEventsAsync` = **2** (paid rows, refund rows) → **8 round-trips**, no per-row query.
- **Aggregates SQL vs memory:** bucketing and totals are in memory (`TrendCalculator.Compute`) over two narrow projections; SQL bucketing was rejected on purpose (13.2 row "Admin dashboard tier 4", "Gave up … SQL bucketing"). Unchanged here; growth risk is a backlog NOTE.
- **Date bounds via `IClock`:** window `[period.From(now), now]` with `now = clock.UtcNow` inside `RevenueTrendService:10–12`. The endpoint file's only clock use is the export filename (`clock.UtcNow`); `grep -n "DateTime\.\|UtcNow" AdminInsightsEndpoints.cs` shows only that line and record field types. No `DateTime.UtcNow`.
- **Projection:** store selects `PaidAtRow(PaidAt, GrandTotal)` / `RefundAtRow(SucceededAt, Amount)` with `AsNoTracking` (`DashboardStore.cs:28–33`), no full entities. Response records byte-identical; mapping identical to controller 32–34; P5 green.
- **Cross-module contract calls:** none. The file uses `Ecommerce.Admin.Application`, `Identity.Contracts` (policy name only) and `SharedKernel.Time`. `IConsumerHealthReader` is SharedKernel, implemented in Persistence, called by the service, not the handler.
- **Role:** no `ClaimsPrincipal` needed — the whole group is Admin-only; Support never reaches a handler.

## 7. Backlog NOTEs (appended to `slice-plans/backlog.md` with this plan)

1. Health N+1 (existing P2.65 line) also hits trend and export: each pays 6 queries only to compute `asOf`.
2. Trend loads every paid and refund row of the window (30d) into memory; accepted by decision, revisit if row counts grow.
3. Export reads the clock twice; the filename date can differ from the window at 00:00 UTC; use `view.To`.

## 8. Risks

| Risk | Guard |
|---|---|
| Insights mapped on Dashboard's `AdminOrSupport` group → Support sees revenue | I1, M1, existing Support-403 tests, inventory |
| Repeated/blank period changes meaning | I4, P1, M2 |
| `TypedResults.File` header drift (charset, `filename*`, BOM) | I7, P3, M3 |
| Both controller and endpoints mapped → `AmbiguousMatchException` | I2, delete in the same change |
| Whitespace period differs from MVC | P1 on MVC first; I4 follows the observed MVC result |
