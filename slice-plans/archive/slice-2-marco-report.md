# Slice 2 — Inventory · Marco report

Concerns:
1. **Layer rules moved two plan items.** ArchitectureTests forbid Domain naming Contracts and Application naming Infrastructure or `Microsoft.Extensions`. So `StockLevel` lives in `Application/` (not `Domain/`), and the threshold reaches Application as `StockSettings(int)`, a singleton the module builds from `IOptions<InventoryOptions>` (same pattern as Orders `ShippingCarriers`).
2. **Fix round 1 (Rob's blocking item):** dev seed now skips every 8th product (by `ProductId`) entirely, so the list shows product-level `Out`. Still audited, still only when `inventory_stock` is empty. Host test asserts rows are not for every variant and at least one visible product has no rows.
3. **ORDER BY mutation is not red against the plan's deadlock test** (Postgres locks in scan order for every caller). I added a deterministic test (lock order vs insertion order) that does go red.
4. The expiry C# re-check (`ExpiresAt <= now`) cannot be reached by the integration double-sweep test (the `status = 'Active'` filter already hides expired rows). Added a unit test with a fake store (deadline moved after the search) that goes red without it.
5. Lock helper detaches only *Unchanged* tracked rows before a locked read (no stale values in a reused scope); pending entries stay, so Slice 4 can add the order before reserving.
6. A refused adjust on a variant with no row still commits an empty 0/0 row.

Bugs found: two in-slice, fixed before finishing: `ProductReader` filtered through a constructor projection (500; now filters first, `AdminInventoryAdjustTests`) and the layer-rule violations in concern 1 (ArchitectureTests).
Bugs fixed: both.
Bugs open: None

Tests changed:
- Deleted `Inventory.Tests/UnknownInventoryReaderTests.cs` (3 tests): the stub is deleted (planned).
- `CatalogStockAndImageTests.With_the_default_registration_...`: renamed, expects `Out` instead of `Unknown` (§7.4).
- `StockStatusRollupTests`: three cases `[] / [U] / [U,U]` now expect `Out`; test renamed. All other cases unchanged.
- `CatalogBrowseServiceTests` lines 64 and 135: expected `Unknown` to `Out`; one test renamed.
- `AdminFakes.CountingInventory`: returns `Out` not `Unknown`.
- `CatalogSchemaTests.Every_table_and_column_name_...snake_case`: expected table list gained `inventory_reservations`, `inventory_stock`.

## Test-first red output
- **Test 1** (reserver stub threw): `System.NotImplementedException : The method or operation is not implemented.` `Failed! Failed: 7, Passed: 2` (2 = schema tests). Green after implementing: 9/9.
- **Test 10** (no endpoint yet): `Expected: Conflict / Actual: NotFound`, `Expected: Unauthorized / Actual: NotFound`, 400 cases `Expected: BadRequest / Actual: NotFound`. Green: 23/23 Inventory-folder tests.
- **Test 20** (reader returning Unknown; I had already written the real reader, so I swapped in the stub line temporarily and restored it): `Expected: InStock / Actual: Unknown`, `Expected: Low / Actual: Unknown`; `Failed: 4, Passed: 0`. Restored: 4/4.

## Mutation checks (each restored, verified by grep)
1. Remove `FOR UPDATE`: RED. `T1_one_unit_ten_parallel...` and `T1_twenty_parallel...` fail (2).
2. Remove `ORDER BY` from lock query: plan's deadlock test stayed GREEN (not deterministic). New `Stock_rows_are_locked_in_database_uuid_order...` RED.
3. Remove `expectedOnHand` check: RED, `A_stale_expectedOnHand_is_409...`.
4. Drop rejected-audit `Add`: RED, `T10_an_adjust_that_would_drop...`.
5. Admin policy replaced by plain `[Authorize]`: RED, `Both_endpoints_are_401_for_anonymous_and_403_for_a_customer`.
6. Expiry guard: dropping `AND status = 'Active'` from the lock SQL: RED, `Two_sweeps_at_the_same_time...`. Dropping the C# re-check: integration GREEN, unit `A_reservation_whose_deadline_moved...` RED.

## Smoke (own API on :5090, Development, fresh DB `ecommerce_smoke` on the dev Postgres, left in place; process stopped)
`GET /api/v1/products?pageSize=30`: 25 items, `{'Out': 4, 'InStock': 16, 'Low': 5}`, no `Unknown`. 39 stock rows (7 with on hand 3).

## Config lines added
- `appsettings.json` (the Shipping block above it was already dirty before Slice 2; file kept CRLF): added
  `  "Inventory": {` / `    "LowStockThreshold": 5,` / `    "ReservationSweepSeconds": 30` / `  },` after `"Shipping"`.
- `appsettings.Development.json`: untouched.

Changed (Slice 2):
- Contracts: `IInventoryReserver.cs`, `IProductReader.cs`.
- Inventory module: csproj, `InventoryModule.cs`, Domain (3), Application (7), Infrastructure (store, reader, options, worker, 3 persistence, dev seed), Api (3). Deleted `UnknownInventoryReader.cs`.
- Catalog: `ProductReader.cs`, `CatalogModule.cs`, `CatalogBrowseService.cs`, `AdminProductService.cs`, `StockStatusRollup.cs`.
- Identity: `AdminBootstrapHostedService.cs` (audit row). Host: `Program.cs` (`AddInventoryDevSeed()`), `appsettings.json`.
- Persistence: migration `AddInventory` (+ Designer, snapshot). No xmin DDL (system column, as products).
- Tests: `Api.IntegrationTests/Inventory/*` (8 files), `Inventory.Tests/{Domain,Application}/*` (4 files), changes above.
- No csproj added or renamed; Dockerfile unchanged.

verify.sh (full, run after the report was written):
- `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`; build 0 warnings; jest 335; drift clean.
- Counts: Inventory.Tests 19 · Catalog.Tests 166 · Orders.Tests 157 · Identity.Tests 73 · Architecture 25 · Integration 226.
- `VERIFY PASS (docker)` (round 0; Dockerfile unchanged).
- Hygiene prints an awk warning for the deleted `UnknownInventoryReader.cs`; it passes.

Quality-bar §2: all boxes checked. Tests added and passing for every money/stock movement, state transition and auth rule; test 1, 10, 20 first (red above); mutation checks done; no `var`, `IClock` used, thin controller, module boundaries intact (ArchitectureTests pass); no secrets, no new packages; no git writes (round 0 had one failed stray `git rm --cached`; none since). Unchecked: none. N/A: frontend items (no frontend change).
