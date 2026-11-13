# Slice P2-tier4 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY FAIL: jest` (Test Suites: 1 failed, 243 passed, 244 total · Tests: 3 failed, 1870 passed, 1873 total; secrets, hygiene, format, build, test, drift, expo-check, tsc, export passed)

The product code is right. The trend is Admin-only at class level on both controllers. Its window and predicates match `ReadKpisAsync` exactly, so Σ buckets = `kpis.revenue`. Money labels are server strings, and Support never requests revenue. All four mutations go red. The one blocker is a test suite you added: it fails the gate when the whole Jest run is under load.

### 1. `AnalyticsView.test.tsx` times out in the full Jest run — BLOCKING   (`features/admin-analytics/components/AnalyticsView.test.tsx:37,48,61`)
**What I saw:** In my full `verify.sh` run, the first case ("shows the trend and the top products…") hit `Exceeded timeout of 5000 ms`. The other two failed straight after it: `toHaveSearchParams` got `{}` instead of `{ period: '30d' }`, and `toHavePathname` got `/admin/analytics` instead of `/admin`. This happened in both projects (`artifacts/verify/jest.log:236–353`). On its own the suite passes (12/12 together with settings). In your report, run 1 also timed out on three other files. Each case calls `renderRouter('./app', …)`, which loads every route file on a cold start. `AdminSettingsView.test.tsx` does the same, so it is one load spike away from the same failure.
**Why it matters:** The gate is `verify.sh`. If it fails one run in two, nobody can trust a green or a red. The next slice either reruns until it passes or starts ignoring jest failures, and that is how a real Support-revenue regression gets through. Both your runs and mine have now gone red.
**What I want:** Make both new suites lean, as plan §7 asks. Render only the routes each test needs with `renderRouterWithQuery` (route map), the way `DashboardInsights.test.tsx` does: the real `app/admin/_layout` (so the Support redirect is still the real gate), the real `admin/analytics` or `admin/settings` screen, and a stub for `admin/index`. Keep every assertion as it is, including Support "lands on `/admin` with no `/trend` or `/admin/settings` request". If the real layout can't be mounted in a route map, the fallback is `jest.setTimeout(15_000)` at the top of both files, which `tests/routes.test.tsx:20` already does. Say which one you used and why. Don't change `scripts/verify.sh` or any other test file.
**How I'll check:** a full `bash scripts/verify.sh` with `VERIFY PASS`, then the diff of the two test files to confirm no assertion was removed or loosened.
**Lesson:** A test that only passes when run alone is a flaky gate. Mount the smallest router that still contains the guard under test.

### Mutations (re-run by me, restored from scratch copies, `cmp` identical)
- **M1** `AdminInsightsController` `Admin` → `AdminOrSupport`: red `Trend_…Support_is_403_with_no_amount` and `Export_…Support_is_403` (`Expected: Forbidden / Actual: OK`).
- **M2** `AdminSettingsController` → `AdminOrSupport`: red `Settings_…Support_is_403` (`Expected: Forbidden / Actual: OK`).
- **M4** `-= row.Amount` → `-= 0m`: red `TrendTests.An_actor_refund_lowers_its_own_bucket_only…` (`Expected: -4 / Actual: 0`) and integration `Trend_7d_sums_to_the_total…` (`Expected: 12.0000 / Actual: 15.00`).
  M1, M2 and M4 ran in one integration build: 4 failed, 6 passed. Each test reaches only one controller or the calculator, so each red belongs to one mutation.
- **M3** `isAdmin = true` in `DashboardView`: red `Support gate (D9) › S1…` in both Jest projects (2 failed, 16 passed).

### Your concerns
1. **Percent for Admin only:** **Accepted.** `DashboardView.test.tsx:82` checks that Support sees no `%`. Keeping that invariant test as it is matters more than D11's percent for Support. Support still gets every count. I added this to the §13.2 "Admin dashboard tier 4" row.
2. **`buckets` guard in `RevenueTrendPanel`:** **Accepted for this slice.** It was the only way to avoid editing `DashboardView.test.tsx`, which my plan forbade. The live-region error line also keeps the existing alert count. Removing the guard later is in the backlog.
3. **`TrendSeries` return type:** **Accepted.** The plan left it open, and it has one consumer.
4. **S1 red only through M3:** **Accepted.** M3 shows S1 catches a missing gate. The empty recent-orders list is right, because Support legitimately sees order totals there.
5. **Jest timeouts:** **Not yet.** See finding 1. Two of the timed-out files are yours.

### Changed tests (checked as possibly weakened invariants)
- `nav.test.ts` N1, Analytics/Settings `ready: true`: justified by D19, and the route gate is unchanged (`admin-access.ts` still allows Support only `/admin`, orders, customers).
- `AdminShell.test.tsx`, S8 link count 7 → 9, `notReadyLabels` now only `User Stories`, two route stubs added: justified. The count went up and nothing was relaxed.
- `breadcrumb.test.ts` B3: the two paths are real routes now, and B7 asserts the crumb for every ready item, so they are still covered.
- `routes.test.tsx`: two rows added (Admin role) and none removed.
- Platform tests: cases appended only.

### 2. Minor — NOTE (in `slice-plans/backlog.md`)
Remove the `buckets` guard once the old mock has a `/trend` route · revoke the object URL on a `setTimeout` for Safari · show `Export failed:` outside the `data` branch · fix the "exactly once" comment wording in `status-buckets.ts` · `AnalyticsView` sets `Stack.Screen` twice.

**What was good:** `ReadRevenueEventsAsync` uses the same `>= from && <= to` predicates as `ReadKpisAsync`, and I6/I7 compare against the live dashboard response, not against a constant. The trend and export guard is one class-level policy in its own controller, so no method can silently miss it.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; jest `Test Suites: 244 passed, 244 total · Tests: 1873 passed, 1873 total`.

### 1. Jest timeout in the two new suites — Confirmed
Both files now use `renderRouterWithQuery` with a three-entry route map: the real `admin/_layout`, the real screen, and a stub `admin/index`. The Support redirect is still the real gate. No timeout override was used, and that is the option I preferred. I read both files in full. The cases are the same as before: 3 in Analytics (trend and top products for the URL period; period written to the URL and requested; Support lands on `/admin` with no `/trend` request) and 3 in Settings (values and read-only copy; error then retry; Support lands on `/admin` with no `/admin/settings` request). No assertion is removed or loosened. Both files are untracked, so I could not diff them, but they match the plan and my round-1 description. `git status` shows no change to `scripts/verify.sh`. The only test files touched are the tier-4 files and the dashboard and shell files already in the working tree. No mutation re-run: no guard code changed.

**What was good:** the Support gate tests still go through the real layout, so M3-style regressions stay red.
