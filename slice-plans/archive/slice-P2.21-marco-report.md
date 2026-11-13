Concerns: verify.sh expo-check FAILS and is not caused by this slice. `expo@58.0.24 - expected version: ~58.0.25` ("Found outdated dependencies"). Fixing it needs a package bump, which is outside the plan and CLAUDE.md §1. Needs Nathaniel/Rob. Marco therefore left BUSY in AGENT_STATUS.md (rule: AVAILABLE only if verify passes).
Bugs found: None
Bugs fixed: None
Bugs open: None (expo-check dependency drift above, environmental)
Tests changed (3 existing, per plan; all other existing blocks byte-identical apart from the added `within` import):
- `shows the figures exactly as the server returned them...`: four flat-line assertions became per-card `within(getByLabelText(...))` assertions of value and secondary line (stronger: value tied to label, secondary checked). As-of, top-product, low-stock lines untouched.
- `asks the server again when the period changes...`: load anchor `Revenue: $1,234.50` -> `$1,234.50`.
- `shows the error with a retry...`: final assertion same replacement.
Tests added: K1-K5 (`kpi-cards.test.ts`), D1 (Admin and Support), D2, D3 (`DashboardView.test.tsx`).
Changed:
- NEW frontend/apps/store/features/admin-dashboard/kpi-cards.ts
- NEW frontend/apps/store/features/admin-dashboard/kpi-cards.test.ts
- frontend/apps/store/features/admin-dashboard/components/DashboardView.tsx (KpiCards replaces Kpis; Recent orders and Low stock to AdminPanel with row border classes)
- frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx
- AGENT_STATUS.md. Backend diff empty (M1 restored, cmp OK).
Jest (features/admin-dashboard + tests/admin-phone-width): 54 before -> 76 after (counts both jest projects). Full jest: 208 suites, 1586 tests.

M1 (role check line 66 -> `true`), red:
  Failed ...Dashboard_for_an_Admin_has_the_servers_KPIs_and_for_Support_has_kpis_null [5 s]
  Assert.Equal() Failure: Values differ / Expected: Null / Actual: Object  (AdminDashboardApiTests.cs:46)
  Failed! - Failed: 1, Passed: 0, Total: 1
Restore from scratchpad copy; `cmp` -> no output (CMP_OK). Green re-run of that test is covered by the full verify run (test step PASS).

Red, step 3 (K1-K5 before kpi-cards.ts): "Cannot find module './kpi-cards'" (Resolver._throwModNotFoundError); Test Suites: 2 failed. After: 14 passed (2 projects).
Red, step 4 (before KpiCards): 10 failed / 18 passed (5 tests x 2 projects: the 3 edited existing tests, D2, D3). After: all green.

verify.sh:
PASS secrets, hygiene, format, build (0 warnings), test, drift, tsc, jest (208 suites / 1586 tests), export
FAIL expo-check (expo@58.0.24, expected ~58.0.25)
VERIFY FAIL: expo-check

Quality-bar §2: tests green and test-first shown [x]; no secrets [x]; no var / token classes only, no style=, hex or arbitrary values [x]; API unchanged [x]; docs/tracker not edited (Rob/coordinator mark rows) [x]; verify.sh fully green [ ] expo-check, environmental, see Concerns.
Manual checks, pending Nathaniel: (1) /admin at 375 px light and dark, two cards per row, no horizontal scroll; (2) Support: no KPI cards, recent orders and low stock render; (3) Today -> 30 days: phrases match the figures on screen during load.

## Round 1b — expo bump

Ran `npx expo install expo@~58.0.25` in `frontend/apps/store`. Only `expo` changed: 58.0.24 -> 58.0.25 (`package.json` pin is now `~58.0.25`, was exact `58.0.24`; lock file updated for the same package). No other packages bumped.

verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — expo-check "Dependencies are up to date", jest 1586 passed.

## Round 2 — Marco
Fixed the BLOCKING item: `expo` pinned exact at `58.0.25` in `frontend/apps/store/package.json:25` and `package-lock.json:18` (root `packages[""]`). No other dependency range changed.
verify.sh changed: expo-check PASS (Dependencies are up to date). VERIFY PASS (secrets hygiene expo-check format tsc jest export); jest 1586 passed.
