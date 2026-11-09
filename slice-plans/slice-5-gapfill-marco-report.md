# Slice 5 gap-fill — Marco report (11.2 admin order list API, 11.4 list screen, 11.5 action buttons)

Concerns:
- Plan said the filter validation errors are keyed camelCase. `ModelState` merges keys case-insensitively and keeps the PascalCase key of an already-bound query value (`Status`). I added `ModelState.Clear()` before `InvalidProblem` in `AdminOrderReadsController.List` (ReturnsController.cs). Rob may want the same in other `[FromQuery]` endpoints (not touched).
- 409 text in `OrderActions` is the server's Problem `title` (ApiError has no `detail`); safe, no new field added.
- Support never sees action buttons (UI only); the API's Admin policy on the four POSTs is unchanged.

Bugs found: key-casing above (caught by the status 400 test) | none else
Bugs fixed: key-casing, `AdminOrderListApiTests.Status_filter_..._400` now green
Bugs open: None

Tests changed (stub replaced by the real screen, nothing weakened):
- `frontend/apps/store/tests/routes.test.tsx`: `/admin/orders` row moved from stub (`Admin · Orders`, `built=false`) to built (`Orders`, `built=true`); the fetch mock answers `GET /api/v1/admin/orders`.
- `frontend/apps/store/tests/app/admin.test.tsx`: Support header name `Admin · Orders` to `Orders`.
- Fakes got the new interface members (throw `NotSupportedException`): Identity.Tests `FakeUserRepository`, Notifications.Tests `FakeUsers`, Orders.Tests `FakeOrderReadStore`.
- Existing `AdminOrderDetailView.test.tsx` and `api.test.ts` only had tests added.

Test-first (red): `AdminOrderListApiTests` run before the route existed: `Failed! - Failed: 11, Passed: 0, Total: 11` (route 404). After implementation: `Passed! - Failed: 0, Passed: 11`.

Mutation M1 (`AdminOrderReadsController` class policy `AdminOrSupport` to plain `[Authorize]`):
`Anonymous_is_401_customer_is_403_without_data_and_support_and_admin_are_200 [FAIL]  Expected: Forbidden  Actual: OK` / `Failed! - Failed: 1, Passed: 0`.
Restored from scratch copy; `cmp` identical (RESTORED).

Regression re-run (integration): OrdersFulfillmentApiTests, Refund*, Dashboard*, OrdersReadApiTests, Projections: green (173 + the 11 new, after the key fix). `GET /admin/orders/{id}` unchanged (route now `[HttpGet("{id:guid}")]` on the renamed `AdminOrderReadsController`). ArchitectureTests unchanged: `Passed 25`.

Changed (Slice 5 gap-fill):
- Identity: `IUserReader.FindIdByEmailAsync`, `IUserRepository.FindIdByEmailAsync` (AsNoTracking, `Take(1)`), `UserReader`, `Repositories.cs`.
- Orders: `OrderPorts.cs` (`AdminOrderFilter`, `ListForAdminAsync`), `OrderStores.cs` (`ListRecentAsync` now delegates), `OrderQueryService.cs`, `OrderViews.cs`, `OrderRequests.cs` (`AdminOrderListQuery` + validator), `OrderResponses.cs`, `ReturnsController.cs`, `OrdersModule.cs`.
- Tests: `Orders/AdminOrderListApiTests.cs` (11), `Orders.Tests/Application/AdminOrderListQueryValidatorTests.cs` (16).
- Frontend: `features/admin-orders/{types,api,order-actions,hooks/hooks}.ts`, `components/{AdminOrderListView,OrderActions,AdminOrderDetailView}.tsx`, `app/admin/orders/index.tsx`, `lib/query-keys.ts`, plus Jest: `api.test.ts`, `order-actions.test.ts`, `OrderActions.test.tsx`, `AdminOrderListView.test.tsx`, `AdminOrderDetailView.test.tsx`, `tests/admin-order-fixtures.ts`.
- Docs: `docs/ecommerce-app-context.md` §3.1 Slice cell `5` for 11.2, 11.4, 11.5 (Status left as Not started for the verdict).

verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`; build 0 warnings; jest 159 suites / 1099 tests; drift clean.

Quality-bar §2: all boxes met. No `var`, no new packages, no migration (indexes already cover the filters), `IClock` not needed, no cross-module `DbSet` (rule 8: email lookup via `IUserReader`, contacts via `GetContactsAsync`), thin controller, no secrets, no unplanned API. N/A: no money/stock movement; the 11.5 buttons call existing tested endpoints.
