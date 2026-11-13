# Slice P2-tier4 — Marco report

Concerns:
1. **Plan gap (existing test vs D11 percent):** `DashboardView.test.tsx` (unchanged) asserts Support sees no `/%/` text. D11 shows a percent in the legend, so the percent is shown **to Admin only** (`showPercent={isAdmin}`); Support sees counts. Rob to confirm or relax that old assertion.
2. **Plan gap (existing tests vs the trend):** several untouched dashboard tests answer every non-recent-orders URL with the dashboard JSON, so `/trend` returned a body with no `buckets`. `RevenueTrendPanel` treats a body without a `buckets` array as an error (`Retry trend`) instead of crashing. The error line uses `accessibilityLiveRegion`, not role `alert`, because an existing test counts alerts.
3. `TrendCalculator.Compute` returns a small `TrendSeries(Buckets, Total, OrderCount)`; the plan left the return type open. `RevenueTrendView` is built by the service as planned.
4. S1 (Support) passed before the dashboard was wired (nothing rendered yet), so its real red is mutation M3 below. S1 uses an empty recent-orders list: Support legitimately sees order totals in Recent orders (existing behaviour), which would otherwise trip "no `$` on the page".
5. Full-suite jest run 1 had 3 files time out under load (`admin-shell-data`, `app/admin`, and `routes` which was a real miss: new route files). They pass in isolation and in run 2.

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed (each justified):
- `features/admin-shell/nav.test.ts` N1: Analytics and Settings now `ready: true` (D19).
- `features/admin-shell/components/AdminShell.test.tsx`: S8 link count 7 → 9; `notReadyLabels` now only `User Stories`; routes `admin/analytics` and `admin/settings` registered in the harness.
- `features/admin-shell/breadcrumb.test.ts` B3: removed `/admin/analytics` and `/admin/settings` from the "unknown path" list; they are real routes now (B7 covers ready items).
- `tests/routes.test.tsx`: two rows for the new route files.
- `lib/platform.test.{web,native}.tsx`: new `saveTextFile` cases appended; existing cases untouched.
- `DashboardView.test.tsx` untouched.

Tests added:
- Backend unit `Ecommerce.Admin.Tests/TrendTests.cs` (6: U1–U6). Integration `Projections/AdminInsightsApiTests.cs` (10 incl. theory rows: I1–I9). Settings 400 is N/A (D8).
- Jest: `lib/api-client.text.test.ts` (3, new file instead of editing the two api-client test files because the auth cases need handlers), `trend.test.ts`, `status-buckets.test.ts`, `DashboardInsights.test.tsx` (9 incl. S1), `AnalyticsView.test.tsx` (3), `AdminSettingsView.test.tsx` (3), platform web/native (1 each).

Red first:
- Auth tests (I1–I3) and the others, before any route existed: `Expected: Unauthorized / Actual: NotFound`, `Expected: OK / Actual: NotFound` (all 10 failed).
- `TrendTests` red = compile errors (`PaidAtRow`, `RefundAtRow`, `TrendSeries` not found) before `TrendCalculator`.

Mutations (break → red → restore from scratch copy → `cmp` identical):
- **M1** `AdminInsightsController` policy `Admin` → `AdminOrSupport`: red `Trend_without_a_token…Support_is_403…` and `Export_without_a_token…Support_is_403` (`Expected: Forbidden / Actual: OK`); Failed 2, Passed 8. Restored, `cmp` ok.
- **M2** `AdminSettingsController` → `AdminOrSupport`: red `Settings_without_a_token…Support_is_403` (`Expected: Forbidden / Actual: OK`); Failed 1, Passed 9. Restored, `cmp` ok.
- **M3** `isAdmin = true` in `DashboardView`: red `S1: Support never requests the trend…` (Jest, Failed 1, Passed 8). Restored, `cmp` ok.
- **M4** `TrendCalculator` refund subtraction `-= row.Amount` → `-= 0m`: red `TrendTests.An_actor_refund_lowers_its_own_bucket_only…` (`Expected: -4 / Actual: 0`) and `Trend_7d_sums_to_the_total…` (`Expected: 12.0000 / Actual: 15.00`). Restored, `cmp` ok.

Changed:
- Backend: `Admin/Api/{AdminInsightsController,AdminSettingsController,TrendCsv}.cs`, `Admin/Application/{RevenueTrend,RevenueTrendService,ProjectionAsOf}.cs` (new); `DashboardKpis.cs` (Granularity), `DashboardQueryService.cs` (ProjectionAsOf), `IDashboardStore.cs`, `DashboardStore.cs`, `AdminModule.cs`; Inventory `Contracts/IInventorySettingsReader.cs`, `Infrastructure/InventorySettingsReader.cs`, `InventoryModule.cs`. No migration.
- Frontend: `package.json`/lock (`react-native-svg` 15.16.0, exact), `lib/{api-client.ts,platform.d.ts,platform.web.tsx,platform.native.tsx,query-keys.ts}`, `features/admin-dashboard/{types,api,trend,status-buckets}.ts`, `hooks/hooks.ts`, `components/{PeriodSwitch,TopProductsPanel,RevenueTrendPanel,OrdersByStatusPanel,DashboardView}.tsx`, `features/admin-analytics/…`, `features/admin-settings/…`, `app/admin/{analytics,settings}.tsx`, `features/admin-shell/nav.ts`, `tests/admin-dashboard-fixtures.ts` (`makeRevenueTrend`).
- Tracker: P2.25–28, 42–45, 17 marked Done (`docs/admin-dashboard-feature-tracker.md`).
- Slice: P2-tier4.

verify.sh:
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 244 suites / 1873 tests passed.

Quality-bar §2: all boxes met. Money is server-computed (trend, total, CSV); labels are server strings, client parses amounts for chart y only (D10). Admin-only guards at class level, tested 401/403 plus Support. No new tables/migrations. N/A: settings 400 (no input), Playwright/e2e (not in plan).

## Round 1 fix
Blocker: AnalyticsView.test.tsx timed out in the full jest run (renderRouter('./app') cold-loads every route).
Fixed: AnalyticsView.test.tsx and AdminSettingsView.test.tsx now use renderRouterWithQuery with a small map (real admin/_layout, real screen, stub admin/index). All assertions kept; no timeout override needed.
Tests changed: those two files only (render helper swap).
Results: the two suites pass (12/12 in 4 projects); full jest x2: 244 suites / 1873 tests passed both times.
verify.sh changed: VERIFY PASS (secrets hygiene build test expo-check format tsc jest export)
