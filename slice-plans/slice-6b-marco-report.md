# Slice 6b — Marco report

Concerns: see Deviations below. None blocks the slice.
Bugs found: None
Bugs fixed: None
Bugs open: None

## Changed (slice 6b)
- SharedKernel `Projections/` (IProjector, replay, health). Persistence `Projections/`: ProjectionProcessor, replayer, health reader, lag monitor, worker, checkpoint/version entries, options; `AdvisoryLock` extracted from OutboxProcessor (no behaviour change); migration `AddProjectionsAndSaga` (6 tables).
- New Admin module (+ empty Admin.Contracts, Admin.Tests, slnx, ArchitectureTests module list): DashboardProjector, DashboardStore, DashboardQueryService, KPI/period maths, dashboard + recent-orders + replay controllers.
- Orders: `OrderPaidPayload/Line` moved to Contracts (D11), FulfillmentSaga + service + `unfulfillable` endpoint, ship guard, saga Compensated on full refund, `ListRecentAsync`/`CountPendingReturnsAsync`.
- Payments: `RefundSucceeded` (actor refunds only, v2), `FulfillmentCompensationConsumer` (key `saga:{id}:Fulfill`).
- Catalog: `catalog.search` projector + sales reader, `sort=bestselling`, `related`. Inventory `ListLowStockAsync`, Identity `GetContactsAsync`.
- Frontend: `features/admin-dashboard/*`, `/admin` screen, query keys, catalog `bestselling` sort + related section. No package added. Docs: none edited (Rob's edits were already in the tree).

## Tests (red first, quoted)
- Runtime (stub threw): `ProjectionRuntimeTests` 6 tests: `Failed! Failed: 6 ... System.NotImplementedException`; then `Passed! 6`. Covers T11 runtime, T13 runtime, gap-commit, window, T14 (1,3,2 / dead letter / stale).
- T11/T13 real projector: `Failed: 2` (`Sequence contains no matching element` / `Filter not matched`), then `Passed: 2`.
- RefundSucceeded: `Expected: 1 Actual: 0`, then green (35 refund tests pass).
- T16 saga: `Failed: 13, Passed: 1` (`Expected: OK Actual: NotFound`), then `Passed: 14`.
- Written after the code (not test-first): dashboard/recent-orders/replay role+400 tests (AdminDashboardApiTests, 19), T12/T17/bestselling/related (CatalogSalesTests, 4), T15, T14 end-to-end, T18, KPI unit tests (9). Their guards are proven by mutations 6 to 8.
- F tests: none for 6b. Jest added: dashboard view (10), api (2), sort picker, list-params, related section.

## Mutation checks (each restored from scratch copy, `cmp` identical)
1. Dedupe mark moved after commit: red T11 runtime `Assert.False() Failure Actual: True`, T11 dashboard `RefundSucceeded was applied a second time`, T12.
2. Checkpoint ignores window/non-terminal: red gap-commit `checkpoint passed the uncommitted lower sequence`, window test `Expected: 0 Actual: 1`, T13 (young-row step I added so T13 detects it).
3. Fresh GUID saga key: red T16 `Strings differ ... Expected: "saga:..."`.
4. Existing-saga check dropped: red T16 `Expected: OK Actual: InternalServerError`.
5. Ship guard removed: red T16 `Not found: "order-unfulfillable"`; Processing-order test `Expected: Conflict Actual: NoContent`.
6. Support kpis null removed: red `Dashboard_for_an_Admin... Support_has_kpis_null`.
7. Dashboard policy `[Authorize]`: red Customer-403 tests (dashboard, recent orders).
8. Admin+Support on unfulfillable and on replay: red `Expected: Forbidden Actual: OK` / `Actual: Accepted`.

## Tests changed (additive, no assertion weakened)
- Fakes got `NotSupportedException` members for grown interfaces: Notifications.Tests FakeUsers/FakeOrders, Identity.Tests FakeUserRepository, Catalog/Cart/Inventory/Orders unit fakes, CatalogStockAndImageTests fake. `ReturnsAndRefundsTests` passes a `FakeSagaStore` (OrderRefunds ctor grew).
- `CatalogSchemaTests`: exact table list gained the 6 new tables.
- `CatalogStockAndImageTests`: detail still makes ONE inventory call; it now carries 4 ids (page variants + related cards) instead of 2, plus related status asserts.
- `tests/app/admin.test.tsx` (F7b): Support redirect test ignores `/api/v1/admin/dashboard`, the page it lands on now loads (Support may use it).
- `routes.test.tsx`: `/admin` is a built screen, mocks added. `ApiFactory`: `Projections:Worker:Enabled=false`.

## Deviations
- Money columns `numeric(19,4)` like `orders` (plan said 12,2).
- One `ProjectionWorker` loops all projectors (plan: one per projector).
- Bestselling tie-break: units desc, then newest, then id (serves "then newest" for related).
- No separate health/lag unit test; lag, as-of, dead letters are covered by T13, T14 end-to-end, T18.
- Projector and DB-reading code lives in `Infrastructure` behind `IDashboardStore` (ArchitectureTests forbid EF in Application).
- Admin.Contracts is an empty project (plan D8).

verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export). test: Admin 9, Notifications 18, Cart 19, Inventory 28, Catalog 166, Payments 86, Identity 77, Orders 218, Architecture 25, Api.Integration 448. drift: no changes. tsc clean. jest: 153 suites, 1025 tests.

## Quality-bar §2
- [x] Plan built, nothing beyond it. [x] Hard rules: outbox in same tx, no external call in tx, IClock, no `var`, module boundaries (ArchitectureTests green). [x] Test-first for T11, T13, T16, gap-commit; other tests listed above. [x] Mutations: exactly the 8, restored. [x] New endpoints: happy, 401/403, 400, 404/409. [x] Jest for logic and screens. [x] Full verify.sh green. [x] No git writes, no secrets, no new package, quality-bar/workflows/.claude untouched.
