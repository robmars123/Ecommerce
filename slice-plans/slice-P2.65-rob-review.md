# Slice P2.65 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 errors / 0 warnings, jest 272 suites / 2111 tests.

This is a clean transport swap. I checked the controller line by line against the handler: the records, the mapping and the error texts are the same. `QueryValues` is the old Customers `ReadInt` with the `FirstOrDefault()` moved inside it, and nothing else changed. I found no blocking issues.

### What I checked (evidence)

- **Same behaviour as MVC.** `Get` maps whitespace to null before `DashboardPeriod.Parse` (C5). You pinned this on MVC first with N1 (`?period=%20` gave 200 `today`). The 400 text and shape are pinned by N2. `RecentOrders` returns binding errors first and on their own, then collects the range errors into one response (C7, N3/N6). Defaults are `?? 1` and `?? 10` as before (N4). With a repeated key, the first value wins (N1, N5).
- **Customers switch.** The diff has two call sites and the deleted private method. `ReadInt(page?.FirstOrDefault(), …)` became `QueryValues.ReadInt(page, …)`, and the `FirstOrDefault()` is now the first line of the helper, so the logic is the same. I9–I11 are unedited and green.
- **`QueryValues`.** One static method, 15 lines, no options and no generics. It has two consumers in this slice. It is as plain as it can be.
- **Round-trips and N+1.** Each handler makes exactly one service call (`GetAsync`, `GetRecentAsync`). There is no loop around a call, and no `DbContext` or other module's service in the file. `Application/` and `Infrastructure/` are untouched, so the SQL count is unchanged. The existing N+1 and over-fetch problems are already in the backlog from the plan (lines 118–121).
- **Hard rules.** The handler builds no dates; `DateTimeOffset` appears only as record field types. Money is formatted by `MoneyResponse.From`, which was moved unchanged. The role guard is still `user.IsInRole(IdentityRoles.Admin)` on the server (D19).
- **Boundaries.** Everything stays `internal`. The file uses only `Admin.Application`, `Identity.Contracts` and `SharedKernel.Projections`. `AdminProjectionsController` is untouched, and its remaining usings are all still used.
- **Changed tests.** None. N1–N7 are appended only (the diff has no `-` lines in the test file).

### Mutations (I re-ran them: break → red → restore from scratch copy → `cmp` identical)

| # | Mutation | Result |
|---|---|---|
| M1 | role flag → `false` | RED, 8 failed (incl. `Dashboard_for_an_Admin_has_the_servers_KPIs…`, `Support_never_receives_the_previous_revenue…`) |
| M2 | role flag → `true` | RED, 4 failed (incl. `Support_gets_the_same_low_stock_count…`, `Support_never_receives…`) |
| M3 | group policy → `Admin` | RED, 8 failed (incl. `Recent_orders_…Support_may_read`, `EndpointInventoryTests.T1…`) |
| M4 | `FirstOrDefault()` → `string.Join(",", period ?? [])` | RED, 1 failed (`Dashboard_period_blank_whitespace_and_repeated_keys_follow_MVC`) |

These match your counts exactly.

### 1. N3 `?pageSize=1.5` checks the key, not the message — NOTE (`AdminDashboardApiTests.cs`, N3)
**What I saw:** the `fraction` request asserts only that `errors.pageSize` exists. It does not check `"The value '1.5' is not valid."`.
**Why it matters:** it is low risk, because the `abc` case already pins the message through the same `ReadInt`. Backlog.

### 2. N7 does not pin the `ordersByStatus` item names — NOTE
**What I saw:** N7 covers every response record except `StatusCount`, which is serialised straight from SharedKernel.
**Why it matters:** the type did not move in this slice, so there is no risk now. It is worth adding when P2.67 touches the shared JSON options. Backlog.

**What was good:** you wrote the pins on MVC before converting, and the plan's "MVC is the spec" rule actually decided C5 rather than an assumption. The handler mapping is a zero diff from the controller, which makes this review easy.
