# Slice 12.1 — Marco report

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None

## Tests changed
Four existing test fakes gained one additive member each (`throw new NotSupportedException()`), because the new contract/port methods broke their build (the first full verify failed at build). No assertion, test or behaviour changed:
- `Ecommerce.Notifications.Tests/Application/Fakes.cs`: `FakeUsers` (+2 methods), `FakeOrders` (+1).
- `Ecommerce.Identity.Tests/Application/Fakes.cs`: `FakeUserRepository` (+2).
- `Ecommerce.Payments.Tests/Application/RefundTests.cs`: `FakePaymentStore` (+1).
- `Ecommerce.Orders.Tests/Application/ReturnsAndRefundsTests.cs`: `FakeOrderReadStore` (+1).

## Test-first red (before any production code; endpoint did not exist, so 404)
- I1: `Assert.Equal() Failure: Expected: Unauthorized, Actual: NotFound`
- I2: `KeyNotFoundException: The given key was not present in the dictionary` (404 body has no `totalSpent`)
- I3: `Assert.Equal() Failure: Expected: OK, Actual: NotFound`
- `Failed!  - Failed: 3, Passed: 0, Total: 3`

After implementation: `AdminCustomersApiTests` Passed 10/10 (I1-I8, I6 is a 3-case theory); `Ecommerce.Admin.Tests` 11 (U1, U2 new).

## Mutations (each restored from a scratch copy, `cmp` identical)
- MU1 (`AdminCustomersController.cs`: `GetAsync(id, true, ct)` regardless of role): I2 red, `Expected: Null`, `Failed: 1, Passed: 9`. `MU1-restored-identical`.
- MU2 (`PaymentStore.ListRefundsForOrdersAsync`: dropped `&& r.ActorId != null`): I3 red, `Expected: "110.00"  Actual: "10.00"`, `Failed: 1, Passed: 9`. `MU2-restored-identical`.

## Changed (slice 12.1)
Identity: `Identity.Contracts/IUserReader.cs`, `Identity/Application/{Ports,UserReader}.cs`, `Identity/Infrastructure/Repositories.cs`.
Orders: `Orders.Contracts/IOrderReader.cs`, `Orders/Application/{OrderPorts,OrderReader}.cs`, `Orders/Infrastructure/OrderStores.cs`.
Payments: `Payments.Contracts/IRefundState.cs`, `Payments/Application/{PaymentPorts,RefundState}.cs`, `Payments/Infrastructure/PaymentStore.cs`.
Admin (new): `Application/CustomerQueryService.cs`, `Api/AdminCustomersController.cs`; edited `AdminModule.cs` (one registration).
Tests (new): `Api.IntegrationTests/Admin/AdminCustomersApiTests.cs`, `Ecommerce.Admin.Tests/CustomerQueryServiceTests.cs`.
No migration, package, csproj or doc change; `ArchitectureTests` untouched and green.

## Notes (not deviations)
- D8: `PaidTotal` uses `order_status_history` (`to_status = Paid`); actor-refund sum uses `status = Succeeded AND actor_id IS NOT NULL` over payments of the customer's orders. Cross-schema joins are avoided: Admin makes three contract calls; inside Orders and Payments the subqueries stay in their own module.
- Detail 404 for malformed id comes from the `{id:guid}` route constraint.
- Search escape: `\`, `%`, `_` escaped, `ILike(..., "\\")`, on email or display name.
- The service is not called for Payments when the customer has no orders (U2).

## verify.sh
`VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`
build 0 warnings, 0 errors; Admin.Tests 11, Architecture 25, Api.IntegrationTests 495 + 10 new in that run is included in the total as run; jest 1205 passed (167 suites).
(An earlier full run failed at build because of the four fakes above; fixed, re-run green.)

## Quality-bar §2
- [x] Plan items built (D1-D11, contracts, I1-I8, U1-U2), nothing beyond the plan.
- [x] Tests first for I1-I3, red quoted; mutations quoted and restored.
- [x] No `var`, named records, `IClock` n/a (no time reads), thin controller, no cross-module DbSet.
- [x] verify.sh passes.
- N/A: frontend (12.2), migration, docs (Rob owns).
