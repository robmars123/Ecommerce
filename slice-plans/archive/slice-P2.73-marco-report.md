# Slice P2.73 - Marco report

Concerns:
- MVC capture (step 1), all 400 `application/problem+json`, binding errors alone with PascalCase keys:
  `?page=abc` -> `{"Page":["The value 'abc' is not valid."]}`; `?pageSize=1.5` -> `PageSize` "The value '1.5' is not valid."; `?from=2026-13-01` -> `From`; `?to=x` -> `To`; `?page=abc&pageSize=0&status=paid` -> only `Page`.
  Validator (`?status=paid&pageSize=0`) -> `{"status":["Choose a valid order status."],"pageSize":["Page size must be 1 to 100."]}` plus type/title/status/traceId/correlationId.
  Messages equal the Admin copy's, so `Orders/Api/QueryValues.cs` takes the same `ReadInt(values, field, errors)` plus `ReadDate`; no label parameter needed.
  Also captured: `?from=%202026-06-10` and `?from=6/10/2026` are 200 on MVC; `DateOnly.TryParse` accepts both. `?page=1&page=abc` -> 200 (first value).
- One tiny MVC quirk NOT reproduced: `?page=abc&page=1` -> MVC message `'abc,1'` (joined); ours says `'abc'` (first value). Still 400, same key; not pinned.
- Rob's M1 names `HubAndResetTests.The_admin_detail_is_open...`; that test is actually `RefundTests.The_admin_detail_is_open_to_Admin_and_Support_only_and_unknown_is_404` (RefundTests.cs:418). Under M1 the inventory and list-401/403 tests went red; the RefundTests one was not in my mutation filter run, so not quoted.
- P9: the order uses seeded random variant ids (no catalog variant), so colour/image are null on both sides; the existing P2.51 tests cover catalog fields. Equality of admin `order` JSON vs customer JSON still pins parity.

Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none edited. New: `Orders/AdminOrderReadsContractTests.cs` (P1-P9, 16 cases), all green on MVC before conversion (16/16) and after.

Baseline (MVC) of AdminOrderListApiTests, HubAndResetTests, OrdersReadApiTests, RefundTests, EndpointInventoryTests + pins: `Passed: 85, Failed: 0`. After conversion: same, `Passed: 85`.

Mutations (broken -> red -> restored from scratch copy -> `cmp` clean):
- M1 group policy AdminOrSupport -> Admin: `Failed: 2` - `EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors`, `AdminOrderListApiTests.Anonymous_is_401_customer_is_403_without_data_and_support_and_admin_are_200`.
- M2 `.RequireAuthorization(...)` removed: `Failed: 2` - same two tests.

Changed (slice P2.73):
- new `backend/src/Modules/Orders/Ecommerce.Orders/Api/AdminOrderReadsEndpoints.cs`
- new `backend/src/Modules/Orders/Ecommerce.Orders/Api/QueryValues.cs`
- `backend/src/Modules/Orders/Ecommerce.Orders/Api/ReturnsController.cs` (AdminOrderReadsController class removed, lines 134-169 only)
- `backend/src/Modules/Orders/Ecommerce.Orders/OrdersModule.cs` (MapOrdersEndpoints)
- `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` (chain)
- new `backend/tests/Ecommerce.Api.IntegrationTests/Orders/AdminOrderReadsContractTests.cs`
No change under Application/Infrastructure/Domain/Contracts/OrderResponses.cs. No `.Accepts` (D15).

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); build 0 warnings/0 errors; jest 272 suites / 2111 tests passed.

Quality-bar §2: tests for new behaviour present and green (P1-P9); mutations run for both auth guards; no secrets; no new packages; module boundaries respected (Orders refs only Identity.Contracts for policy names); no existing test weakened; no git writes; tracker not marked; not committed.
