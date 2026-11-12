# Marco report — P2.30 + P2.24 + P2.32 (Low tier)

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none edited. Added only: S1 (13 lines, AdminInventoryAdjustTests.cs, inserted after the Customer 403 test) and Q1, Q2 (it.each), Q3, B1-B4 in DashboardView.test.tsx (86 lines). `git diff --numstat`: 13/0 and 86/0, no removed lines. `git diff --stat -- backend/src` empty.

Changed:
- frontend/apps/store/features/admin-dashboard/components/DashboardView.tsx (Top products -> AdminPanel with row classes; private QuickActions({role}) filtered by adminRouteDecision, in AdminPanel; low-stock banner inline, D2-D4; `role` from useSession)
- frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx (Q1-Q3, B1-B4 appended)
- backend/tests/Ecommerce.Api.IntegrationTests/Inventory/AdminInventoryAdjustTests.cs (S1)
- AGENT_STATUS.md

## Evidence
Jest baseline (features/admin-dashboard + tests/admin-phone-width): 76 passed. Final: 92 passed (10 suites). Full jest: 1602 / 208 suites. (Counts include both jest projects, so each new test counts twice.)

S1 green (before mutation): Passed 1/1.
M1 (AdminInventoryController.cs:15 Admin -> AdminOrSupport), red:
`Failed ...Both_endpoints_are_403_for_support_and_write_nothing` / `Assert.Equal() Failure: Values differ  Expected: Forbidden  Actual: OK` / `Failed! - Failed: 1, Passed: 0`
Restored from scratch copy; `cmp` -> CMP_OK (no diff); backend/src diff empty. S1 re-run: `Passed! - Failed: 0, Passed: 1`.

Step 3 red (Q1, Q2[Support]): `expect(received).toBeNull() Received: <Text ... href="/admin/products/new" ...>Add product</Text>`; Q2: `expect(received).toEqual(expected) Received +4`. Tests: 4 failed (2 tests x 2 jest projects), 32 passed. After D6/D9: 36 passed.
Step 4 red (B1): `Unable to find an element with role: link, name: Stock is running low. Review inventory`; 2 failed (x2 projects). After D2-D4: 44 passed.
B3 self-check (role clause removed from banner condition): `● DashboardView › B3 hides the banner from Support even when stock is low`, 2 failed; restored, `cmp` identical.

Existing tests byte-identical (additions only). No hard-coded 'Admin'/'Support' in DashboardView.tsx (grep empty). No new token, package, or style=.

verify.sh:
VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export) — build 0 warnings; jest 1602 passed / 208 suites.

Quality-bar §2: all boxes met for a Low-tier frontend + one test slice; no API, money, stock or migration change (N/A). Manual checks pending Nathaniel: (1) /admin at 375 px light/dark, banner wraps, no horizontal scroll; (2) Support: no banner, only Orders action; (3) Admin with no low stock: no banner.
