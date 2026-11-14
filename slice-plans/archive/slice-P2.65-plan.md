# Slice P2.65 — AdminDashboard to Minimal API (plan, Rob)

Tier **High** (role-gated revenue, Support hiding P2.45/D19). Mode Autonomous. Tracker row P2.65 (`docs/admin-dashboard-feature-tracker.md:84`), master plan row P2.65 (`slice-plans/minimal-api-migration-plan.md:82`).

**Read:** master plan §2–§4 (`slice-plans/minimal-api-migration-plan.md`) · this plan · `03-principles-hard-rules.md:40` (rule 7) · `06-backend-modules.md:63–69` · `Admin/Ecommerce.Admin/Api/AdminDashboardController.cs` · `Api/AdminCustomersEndpoints.cs` (the pattern) · `AdminModule.cs` · `tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs:1–175` · `Projections/ProjectionEnv.cs:58–66` · `EndpointInventoryTests.cs:20–30`. Nothing else.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| C1 | One group from `app`: `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdminOrSupport)`, then `MapGet("dashboard", Get)` and `MapGet("dashboard/recent-orders", RecentOrders)` | Master D4/D5; RawText matches inventory lines 22 and 24; no trailing slash. `dashboard/export` and `dashboard/trend` (Admin policy, still MVC, P2.66) do not collide | `MapGroup("api/v1/admin/dashboard").MapGet("")` (trailing slash, II11 red) |
| C2 | Delete **only** the `AdminDashboardController` class. `AdminProjectionsController` stays in `AdminDashboardController.cs` until P2.67; trim that file's `using`s to what it still needs | Master: one controller per slice | Converting Projections here (scope creep) |
| C3 | Move every response record (`MoneyResponse` … `RecentOrdersResponse`, lines 11–49) unchanged into `AdminDashboardEndpoints.cs` | They belong to the dashboard; `MoneyResponse` is also used by Customers and Insights (same namespace, still `internal`), so it must not leave with P2.67's file delete | Leaving them in the controller file (P2.67 would have to move them anyway) |
| C4 | `period`, `page`, `pageSize` bound as `[FromQuery] string[]?`; first value used. One comment line why (copy the Customers one) | MVC binds the first value of a repeated key; Minimal API joins `string` as `"a,b"` → `?period=7d&period=30d` would turn 200 into 400 | Plain `string?` |
| C5 | `period`: `string? raw = period?.FirstOrDefault();` then `DashboardPeriod.Parse(string.IsNullOrWhiteSpace(raw) ? null : raw)` — **only if N1 shows MVC gives 200 `today` for `?period=%20`**; if MVC gives 400 there, call `Parse(raw)` | MVC's string binder turns whitespace into `null` (ConvertEmptyStringToNull); `Parse(" ")` is `null` → 400. MVC is the spec; N1 decides | Changing `DashboardPeriod.Parse` (Application change; also used by Insights) |
| C6 | Unknown period → `errors["period"] = ["period must be one of: today, 7d, 30d."]` → `TypedResults.ValidationProblem(errors)`. Exact text unchanged | Master D8; N2 pins text and shape | — |
| C7 | `recent-orders`: two-stage validation exactly as Customers: (1) `QueryValues.ReadInt` for `page`, `pageSize`; any error → `ValidationProblem` and stop; (2) `page is < 1`, `pageSize is < 1 or > MaxPageSize` (50) collected into one dictionary → one `ValidationProblem`. Texts unchanged. Defaults `page ?? 1`, `pageSize ?? 10` | `[ApiController]` returned binding errors before the action ran; range errors were accumulated in `ModelState`. N3, N6 pin it | One combined dictionary |
| C8 | **`QueryValues` now.** New `Api/QueryValues.cs`: `internal static class QueryValues` with one method `public static int? ReadInt(string[]? values, string field, Dictionary<string, string[]> errors)` — body = today's Customers `ReadInt` with `string? raw = values?.FirstOrDefault();` as its first line. Customers calls become `QueryValues.ReadInt(page, "page", errors)` / `(pageSize, "pageSize", errors)`; its private `ReadInt` is deleted. Catalog's copy untouched | Backlog line 115 asks for this at the third Admin user. Admin's remaining controllers (Insights, Projections) have no integer query, so Admin ends at **two** users and a third never comes: extract now or carry two byte-identical copies forever. Two consumers in this slice | A second private copy (duplicate 15 lines in one module); a SharedKernel helper (framework-free, cross-module) |
| C9 | Role flag: `user.IsInRole(IdentityRoles.Admin)` from a `ClaimsPrincipal user` parameter, passed straight to `GetAsync`. Comment line kept ("Support never sees money figures (D19): the guard is the role, not the client.") | Same guard, same place | Reading roles from a claim string by hand |
| C10 | Handlers: `private static async Task<Results<Ok<DashboardResponse>, ValidationProblem>> Get([FromQuery] string[]? period, ClaimsPrincipal user, [FromServices] DashboardQueryService dashboard, CancellationToken ct)` and `RecentOrders([FromQuery] string[]? page, [FromQuery] string[]? pageSize, [FromServices] DashboardQueryService dashboard, CancellationToken ct)` → `Results<Ok<RecentOrdersResponse>, ValidationProblem>`. Mapping bodies copied verbatim from the controller (lines 71–89, 109–111) | Master D3; a mapping diff of zero is the easiest thing to review | Re-shaping the mapping |
| C11 | No caching headers: the controller has no `ResponseCache`/`Cache-Control`; add none | Preserve as-is | — |
| C12 | No change in `Application/`, `Infrastructure/`, any `*.Contracts`, migrations, packages, docs | Pure transport conversion; existing N+1/over-fetch go to backlog (§6) | Fixing queries here |

No decision-log row: the pattern is master-plan D1–D9; C8 is a backlog follow-up.

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminDashboardEndpoints.cs` | **new**: records (C3) + `internal static class AdminDashboardEndpoints` (`MaxPageSize = 50`, `Map`, `Get`, `RecentOrders`) |
| `…/Api/AdminDashboardController.cs` | delete the records and the `AdminDashboardController` class; `AdminProjectionsController` untouched |
| `…/Api/QueryValues.cs` | **new** (C8) |
| `…/Api/AdminCustomersEndpoints.cs` | two call sites → `QueryValues.ReadInt(...)`; delete private `ReadInt`; drop `using System.Globalization` if now unused (`MoneyResponse` lives elsewhere). Nothing else |
| `…/AdminModule.cs` | add `AdminDashboardEndpoints.Map(app);` after `AdminCustomersEndpoints.Map(app);` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs` | **append** N1–N7 only; every existing test unedited |

## 3. Build order

1. Baseline on MVC: `AdminDashboardApiTests`, `AdminInsightsApiTests`, `AdminCustomersApiTests`, `EndpointInventoryTests`, `AuthorizationTests` — quote counts.
2. **Pins first, green on MVC, quote output.** MVC is the spec: if a pin disagrees with this plan, change the pin to MVC, follow MVC in the handler, and say so in the report.
   - **N1** `Dashboard_period_blank_whitespace_and_repeated_keys_follow_MVC`: `?period=` → 200 `period == "today"`; `?period=%20` → record MVC's answer (expected 200 `today`; decides C5); `?period=7d&period=bogus` → 200 `"7d"`; `?period=bogus&period=7d` → 400 with `errors.period`.
   - **N2** `Unknown_period_is_a_400_problem_with_the_exact_text`: `?period=1d` → 400, media type `application/problem+json`, `type` `…rfc9110#section-15.5.1`, `title` "One or more validation errors occurred.", `errors.period[0] == "period must be one of: today, 7d, 30d."`, `traceId` non-empty.
   - **N3** `Recent_orders_bad_numbers_are_400_with_the_binding_message_only`: `?page=abc` → 400, `errors.page[0] == "The value 'abc' is not valid."`; `?page=abc&pageSize=500` → 400, `errors` has `page`, **no** `pageSize`; `?pageSize=1.5` → 400 `errors.pageSize`.
   - **N4** `Recent_orders_blank_values_use_the_defaults`: `?page=&pageSize=` → 200, `page == 1`, `pageSize == 10`.
   - **N5** `Recent_orders_repeated_keys_use_the_first_value`: two orders; `?pageSize=1&page=2&page=abc` → 200, `page == 2`, one item = the older order.
   - **N6** `Recent_orders_range_errors_are_collected_with_exact_texts_and_shape`: `?page=0&pageSize=51` → 400, problem+json, `title` as N2, `errors.page[0] == "page must be 1 or greater."`, `errors.pageSize[0] == "pageSize must be between 1 and 50."`, `traceId` non-empty.
   - **N7** `Response_property_names_are_unchanged`: one paid order + one low-stock variant, drained; Admin `today`: exact sorted property-name sets of the root, `kpis`, `kpis.customers`, `kpis.averageOrderValue`, `needsAttention`, one `lowStock` row; Support: same root set with `kpis` null; recent-orders: root set and one item's set (`id, orderNumber, customerEmail, createdAt, grandTotal, status`). Pins the projection (`averageOrderValue`, `pendingReturns`, `orderNumber`, `createdAt` have no test today).
3. Write `QueryValues.cs`, switch Customers to it, run `AdminCustomersApiTests` (I1–I13 unedited, green).
4. Convert: new endpoints file, `AdminModule` line, delete the controller class **in the same change**.
5. All step-1 suites + N1–N7 green unedited since step 2. Full `bash scripts/verify.sh 2>&1 | tail -n 30`.
6. Mutations (§5), then the report.

## 4. Required tests

| Behaviour | Test | Status |
|---|---|---|
| 401 anon, 403 Customer, 200 Admin + Support, both routes | `Dashboard_without_a_token…`, `Recent_orders_without_a_token…` | existing |
| Policy `AdminOrSupport`, CORS `credentialed`, exact RawText | inventory lines 22, 24; II11 | existing |
| Support `kpis: null`, no revenue text, no previous revenue | `Dashboard_for_an_Admin…`, `Support_never_receives_the_previous_revenue…`, `Support_gets_the_same_low_stock_count…` | existing |
| Unknown period 400; 7d/30d accepted | existing theories | existing |
| Page/pageSize range 400 | `Recent_orders_with_a_bad_page…` | existing |
| Period blank/whitespace/repeated | N1 | **new, test-first (green on MVC)** |
| Period 400 text + shape | N2 | new (green on MVC) |
| Bad number → binding message only | N3 | **new, test-first** |
| Blank paging → defaults | N4 | new |
| Repeated paging → first value | N5 | **new, test-first** |
| Range texts + shape | N6 | new |
| Projection field names | N7 | new |
| `QueryValues` in Customers | `AdminCustomersApiTests` I9–I11 | existing, unedited |

## 5. Mutations (High) — break → red → restore from scratch copy → `cmp`

| # | Mutation (in `AdminDashboardEndpoints.cs`) | Expected red |
|---|---|---|
| M1 | `user.IsInRole(IdentityRoles.Admin)` → `false` (master row) | `Dashboard_for_an_Admin_has_the_servers_KPIs…` (Admin `kpis` null) |
| M2 | role flag → `true` | same test + `Support_never_receives_the_previous_revenue_while_Admin_does` (Support sees revenue) |
| M3 | group policy `AdminOrSupport` → `Admin` | `Recent_orders_…Support_may_read` (403) + inventory lines 22/24 |
| M4 | `period?.FirstOrDefault()` → `string.Join(",", period ?? [])` | N1 (`7d&bogus` → 400) |

No extras.

## 6. What Rob checks in review (name, then evidence)

- **Queries per request:** exactly **one** `await dashboard.GetAsync(...)` in `Get` and **one** `await dashboard.GetRecentAsync(...)` in `RecentOrders`; no other service or `DbContext` in the file; no loop around a call. Diff touches nothing in `Application/`/`Infrastructure/`, so SQL count is unchanged by construction (today: Admin `GetAsync` = 3 per registered projector [2 today] + 15, +1 when low stock is non-empty; Support 6 fewer; `GetRecentAsync` = 3).
- **Aggregates in SQL vs memory:** unchanged by this slice (C12). Existing in-memory KPI arithmetic is a backlog NOTE, not a fix here.
- **Date bounds via `IClock`:** handler computes no dates; `grep -n "UtcNow\|DateTime" AdminDashboardEndpoints.cs` returns only the record field types. Bounds stay in `DashboardQueryService` (`clock.UtcNow`, `period.From/PreviousFrom/PreviousTo`).
- **Projection:** records moved byte-identical; mapping lines identical to controller 71–89 / 109–111; N7 green.
- **Cross-module contract calls:** none added in the endpoint file (it uses only `Ecommerce.Admin.Application` + `Identity.Contracts` for roles/policies).
- **QueryValues:** Customers diff is the two call sites + deleted method; `ReadInt` body identical except the first-value line.

## 7. Backlog NOTEs (Rob appends to `slice-plans/backlog.md` with this plan)

N+1 / over-fetch found while planning; not fixed here (evidence in each line).

## 8. Risks

| Risk | Guard |
|---|---|
| Whitespace `period` turns 200 into 400 | C5, N1 on MVC first |
| Repeated key changes meaning | C4, N1, N5, M4 |
| Binding and range errors merged | C7, N3 |
| Support sees revenue | C9, M2 |
| `MoneyResponse` lost with P2.67's file delete | C3 |
| Customers behaviour drifts in the refactor | C8, I9–I11 unedited |
| Hex `0x2` accepted by MVC, 400 here | Accepted divergence (backlog line 114), no pin |
