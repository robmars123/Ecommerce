# Slice P2.74 — Marco report

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None. Existing tests unedited; `git diff --stat backend/tests` empty. New file only: `Refunds/ReturnRequestContractTests.cs` (RR1-RR7, 9 tests).

Changed (slice P2.74):
- new `backend/src/Modules/Orders/Ecommerce.Orders/Api/ReturnsEndpoints.cs` (one route, no group, `.RequireAuthorization()`, no `.Accepts`/CORS; sub -> validate -> map items -> one `ReturnService.RequestAsync` -> switch)
- `Api/ReturnsController.cs`: `ReturnsController` class and `ReturnCreatedResponse` removed (AdminReturnsController and ControllerValidation untouched)
- `Api/OrderResponses.cs`: `ReturnCreatedResponse` moved, unchanged
- `OrdersModule.cs`: `ReturnsEndpoints.Map(app);`
- new `backend/tests/Ecommerce.Api.IntegrationTests/Refunds/ReturnRequestContractTests.cs`

## MVC capture (real responses, pre-conversion; all carry traceId and correlationId `id-test-1`)
- B on A's order: 403 problem+json, type `...#section-15.5.4`, title `Forbidden`
- unknown order: 404, `...15.5.5`, `Order not found`
- anonymous: 401, `...15.5.2`, `Unauthorized`
- `{}` (as B or A): 400 `...15.5.1`, `One or more validation errors occurred.`, errors `items`: "Choose 1 to 100 items to return.", `reason`: "Enter a reason of 1 to 500 characters."
- `{"items":[{}],...}`: keys `items[0].VariantId` ("Each item needs a variantId."), `items[0].Quantity` ("'Quantity' must not be empty.")
- quantity 0 + blank reason: `reason`, `items[0].Quantity` ("Each item needs a quantity of 1 or more.")
- service: `items`: "Return 1 to 1 of Black tee, M." / "Only items from this order can be returned." / "List each item once."
- Shipped: 409 `type:"not-delivered"`, title "Only a delivered order can be returned"; open: 409 `type:"return-already-open"`, title "This order already has an open return request"; no errors/detail
- happy: 201 `application/json`, `Location: /api/v1/orders/{order}`, body `{"returnId":...}`
- unreadable (R8 accepted): malformed / `null` / `{"items":"x"}` -> 400 problem with `body` plus `$.items` or `""` keys

## Green on MVC before converting
`Passed! - Failed: 0, Passed: 37, Total: 37` (new 9 + ReturnTests, EndpointInventory, ContentTypeContract, AdminOrderDetail).
After conversion (also + RefundTests): `Passed! - Failed: 0, Passed: 72, Total: 72`.

## Mutations (break -> red -> restore from scratch copy -> `cmp` OK each time)
- M1 ReturnService.cs:53 ownership check removed: Failed 2/21: RR1, ReturnTests.T7_another_customers_order_is_403...
- M2 Forbidden arm -> 404: Failed 2/21: RR1, T7
- M3 NotDelivered type -> return-already-open: Failed 1/21: RR3
- M4 Invalid arm keyed "": Failed 1/21: RR5
- M5 `.RequireAuthorization()` dropped: Failed 1/10: EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors
- M6 validator return skipped (`if (false)`): Failed 2/21: RR4, RR2

## verify.sh
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; build 0 Warning(s) 0 Error(s); jest 272 suites / 2111 tests passed.

## Quality-bar §2
- [x] Tests: new pins first, red for each mutation, restored
- [x] verify.sh full green
- [x] No secrets, no new packages, no migration (drift clean)
- [x] Money/stock/outbox: N/A, none moves here; service, stores, validators, DI untouched
- [x] Thin handler: one service call, no DbContext/IClock
- Not committed, tracker not marked (per instruction).
