# Slice P2.13 Part A (P2.13 greeting + P2.11 Inventory "N low" badge) - Marco

Concerns:
- Plan gap: three more `IInventoryStore` fakes (`Ecommerce.Inventory.Tests/Application/{InventoryStockReaderTests,ReservationExpiryServiceTests,ReservationExpirySweepTests}.cs`) stopped compiling once the port gained `CountLowAsync` (CS0535). The plan listed only four `IInventoryReader` fakes. Safe minimum: one additive member each (`CountLowAsync ... => throw new NotSupportedException();`). Rob: confirm.
- `DashboardView.test.tsx` gained one import line (`DashboardView`) for the appended G3/G4 describe; needed to pass the `now` prop. No existing line changed otherwise.
- Flake: full `jest` failed 3 of 4 times in the first two verify runs / one bare run on `tests/routes.test.tsx` "index.tsx renders at /products" (5000 ms timeout, ios, first test cold) and once on `tests/app/admin.test.tsx`. Passes alone (also with `--no-cache`) and the next two full runs were green. Machine load, not this change; frozen file untouched. Backlog candidate.
- Plan risks unchanged (UTC "today" vs device-local greeting).

Bugs found: None (my first IA1 draft used stock 0 rows, which VariantAsync does not create; test fixed before green, not a product bug)
Bugs fixed: None
Bugs open: None

Tests (written first, red quoted)
- IA1 `Low_stock_count_counts_every_low_variant_while_the_list_stops_at_20` and IA2 `Support_gets_the_same_low_stock_count_as_Admin_and_kpis_null`: red `System.Collections.Generic.KeyNotFoundException : The given key was not present in the dictionary.` (no `lowStockCount`) -> `Failed: 2, Passed: 19, Total: 21`. Green after build: `Passed: 21`.
- Frontend red: `Cannot find module './badges'` / `'./greeting'`; `Tests: 12 failed, 134 passed` (B4 x2, G3, G4, L1, L4 failing on missing UI/props, G1/G2/B1-B3 on missing modules).
- Green: G1 8 cases, G2 4, B1-B3, B4 (wide + narrow), B5, B6, G3, G4, L1-L4 all pass.
- Existing 401/403, 400 and happy tests cited as coverage: `Dashboard_without_a_token_is_401_and_as_a_Customer_is_403`, `Dashboard_with_an_unknown_period_is_400`, `Dashboard_for_an_Admin_has_the_servers_KPIs_and_for_Support_has_kpis_null`, all unchanged and green.

Self-checks (evidence only, no mutations)
1. Scratch copy of `InventoryStore.cs`; `CountLowAsync` changed to `LowStock(threshold).Take(20).CountAsync(ct)`:
   `Expected: 21 / Actual: 20` ; `Failed: 1, Passed: 1, Total: 2`. Restored from scratch, `cmp` OK, `Passed: 2`.
2. Scratch copy of `features/admin-shell/hooks.ts`; `refetchInterval` line removed: `Unable to find an element with text: 5 low` (L4 red), `Tests: 1 failed, 3 passed`. Restored, `cmp` OK, `4 passed`.

Tests changed (existing files, additive only)
- Seven fakes, one added member each: `CatalogStockAndImageTests.cs`, `CartMergeServiceTests.cs`, `AdminFakes.cs`, `CatalogBrowseServiceTests.cs` (`CountLowStockAsync` on `IInventoryReader` fakes) and the three Inventory.Tests fakes above (`CountLowAsync`). Reason: new interface members.
- `tests/admin-dashboard-fixtures.ts`: default `lowStockCount: 1` (named edit; matches the single default `lowStock` row).
- `AdminDashboardApiTests.cs` +33 lines (IA1, IA2 appended), `AdminShell.test.tsx` +53 (B4-B6 appended), `DashboardView.test.tsx` +24 (G3, G4 appended, plus the import above). numstat shows 0 deletions on every test file.

Changed (Part A only)
Backend: `IInventoryReader.cs` (+`CountLowStockAsync`), `InventoryPorts.cs` (+`CountLowAsync`), `InventoryStore.cs` (one private `LowStock(threshold)` predicate shared by list and count), `InventoryStockReader.cs`, `DashboardQueryService.cs` (`LowStockCount`), `AdminDashboardController.cs` (`LowStockCount`). No migration, no policy/route change, no `var`, no anonymous types, no cross-module DbSet.
Frontend: `features/admin-dashboard/{types.ts, greeting.ts, greeting.test.ts, components/DashboardView.tsx}`, `features/admin-shell/{badges.ts, badges.test.ts, hooks.ts, components/AdminShell.tsx}`, `app/admin/_layout.tsx`, `tests/admin-shell-data.test.tsx` (new). `npm run format` run; freeze check: `git status` shows only plan-listed files plus the three Inventory fakes. Part B not started.

verify.sh (third full run, after two jest flakes above):
VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)
build 0 warnings; jest 218 suites, 1698 tests (3 projects; was 1623, +25 per project); backend test projects green (Admin 23, Inventory 28, Cart 19, Catalog 202, Api.IntegrationTests incl. 21 in AdminDashboardApiTests, +2 new).

Quality-bar §2: tests for the new behaviour written first and green [x]; no existing test weakened/deleted [x]; verify.sh PASS [x]; no secrets/packages/migrations [x]; IClock untouched (no new time use) [x]; module boundaries kept (Admin reads Inventory via Contracts only) [x]; N/A: mutations (Low tier, per plan); Rob's review pending.
