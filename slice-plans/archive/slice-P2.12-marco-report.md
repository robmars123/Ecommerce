# Slice P2.12 (Part B) - Needs attention card - Marco report

Concerns: (1) Plan lists two fakes for `CountCancelledAsync`; a third was needed: `FakeOrderReadStore` in `tests/Ecommerce.Orders.Tests/Application/ReturnsAndRefundsTests.cs` implements the `IOrderReadStore` port, which D7 extends. One additive line, build would not compile otherwise. (2) IB1/IB2 sign in again after moving the clock (access token lifetime is 15 min), via `e.Env.AccessTokenAsync`; first draft got 401 for that reason.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: Existing tests byte-identical except 3 additive fake members (Admin.Tests `CustomerQueryServiceTests` FakeOrders, Notifications.Tests `Fakes.cs` FakeOrders, Orders.Tests `ReturnsAndRefundsTests` FakeOrderReadStore) and fixture default `cancelledCount: 0` (named in plan). Appended only: AdminDashboardApiTests (IB1, IB2), AdminShell.test.tsx (C1-C3), admin-shell-data.test.tsx (L5). New: attention.test.ts (A1-A3).

Changed:
- Backend: Orders.Contracts/IOrderReader.cs, Orders/Application/{OrderPorts,OrderReader}.cs, Orders/Infrastructure/OrderStores.cs (`CountCancelledAsync`: distinct OrderId, ToStatus Cancelled, from <= CreatedAt <= to); Admin DashboardQueryService.cs (window `period.From(clock.UtcNow)` to `now`), AdminDashboardController.cs (`CancelledCount` after `LowStockCount`). No migration, no `var`, no cross-module DbSet.
- Frontend: features/admin-dashboard/types.ts, tests/admin-dashboard-fixtures.ts, features/admin-shell/attention.ts (new), components/AdminShell.tsx (props-only `attention`, card inside Sidebar ScrollView, `AdminPanel`, token classes), app/admin/_layout.tsx (ready / unavailable / omitted per D9).
- Role matrix: Admin sees all five; Support sees cancelled, failed events, lag (A1/A2, L5).

Red before code:
- IB1: `System.Collections.Generic.KeyNotFoundException : The given key was not present in the dictionary.` (no `cancelledCount` in the response). IB2 was first red for my own 401 (token expiry), fixed in the test; its red-for-the-right-reason is the self-check below.
- A1-A3: `Test suite failed to run` (attention.ts missing). C1: `Unable to find an element with text: Needs attention`; C2: `...text: All clear.`; C3: `...text: Alerts unavailable right now.`

Self-check (not a mutation): scratch copy `scratchpad/OrderStores.cs.orig`; dropped `h.CreatedAt >= from` ->
`Failed ...Cancelled_count_leaves_the_period_when_the_cancellation_is_older_and_a_Paid_order_never_counts` `Assert.Equal() Failure: Values differ  Expected: 0  Actual: 1`; `Failed!  - Failed: 1, Passed: 1, Total: 2`. Restored, `cmp` identical.

Test counts: AdminDashboardApiTests 21 -> 23 (+IB1, IB2). Jest after: 220 suites, 1712 tests (+7 test cases: A1-A3, C1-C3, L5, per platform project). Full before-count for jest not taken before editing.

Existing tests stay green: S1-S29, DashboardView frozen, routes.test, admin-phone-width*, existing AdminDashboardApiTests 401/403/400/happy (Dashboard_without_a_token..., Dashboard_for_an_Admin..., Dashboard_with_an_unknown_period_is_400).

verify.sh (one run, no flake): `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; build 0 warnings; drift clean; tsc clean; jest 220 suites / 1712 tests. `npm run format` run.

Quality-bar §2: all boxes met as for Part A. N/A: no money/stock movement, no new auth rule, no migration (read-only counts, Low tier, no §3 mutations).

## Round 2 — Marco

Fixed the one BLOCKING item (attention links did not close the phone menu).
- Fix: `AdminShell.tsx` passes `onNavigate` Sidebar -> AttentionCard -> AttentionRow; the row Pressable uses it as `onPress`.
- Test C4 (`AdminShell.test.tsx`, appended in the attention describe, placed before C3): Admin at 390 px opens Menu, presses the low-stock link, expects "Inventory page" and no "Close menu". The shell renders in a `_layout` so it survives navigation (in `index` the navigation unmounts the shell and the test is falsely green).
- Red before fix: `Found multiple elements with accessibility label: Close menu` (ios and android). Green after.
- C4 sits before C3 because C3's `first.unmount()` leaves the router rendering null for the next test (existing test interaction, untouched).
- Prettier applied to the test file.
- Jest admin-shell: 16 suites, 216 tests pass.
- verify.sh changed: `VERIFY PASS (secrets hygiene build test format tsc jest export)`
