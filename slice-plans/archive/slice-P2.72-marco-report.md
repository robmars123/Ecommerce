# Slice P2.72 — Cart to Minimal API (Marco report)

Concerns: None. The CC pins are green on MVC and on Minimal API with no change to host JSON options. NOTE: the plan's CC3 comment about the `{}` variantId message is confirmed: MVC returns FluentValidation's default `'Variant Id' must not be empty.` (the `WithMessage("Choose an item.")` binds to `NotEqual` only). Pinned as is.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None edited. New: `backend/tests/Ecommerce.Api.IntegrationTests/Cart/CartContractTests.cs` (CC1-CC7). No existing test file touched.

Changed:
- new `backend/src/Modules/Cart/Ecommerce.Cart/Api/CartEndpoints.cs` (one group `api/v1` + `RequireAuthorization()`, 5 routes, one service call per handler, no `.Accepts`)
- deleted `.../Cart/Ecommerce.Cart/Api/CartController.cs` (plain `rm`)
- `.../Cart/Ecommerce.Cart/CartModule.cs` (`MapCartEndpoints` + `using Microsoft.AspNetCore.Routing;`)
- `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` (`.MapCartEndpoints()`)
- new `CartContractTests.cs` (above)
Nothing under Application/, Infrastructure/, Domain/, Persistence/.

## Step 1 — MVC captures (pinned in CC1-CC7, run on the controller)
- 409 stock: type `...section-15.5.10`, title `Not enough stock`, detail `Only 4 available.`, `available` 4, no `errors`.
- 404: type `...15.5.5`, title `Item not in cart`.
- CC3 errors: add {empty guid, qty 0} -> `quantity=Quantity must be 1 to 1000. | variantId=Choose an item.`; add `{}` -> `quantity=Enter a quantity. | variantId='Variant Id' must not be empty.`; PUT `{}` -> `quantity=Enter a quantity.`; PUT 1001 -> `quantity=Quantity cannot be negative.`
- CC4: `variantId=This item is not available.`; `quantity=A line can hold 1 to 10 of an item.`
- CC5: no key -> `Idempotency-Key=Send an Idempotency-Key header of 1 to 100 characters.`; empty lines -> `lines=Send 1 to 100 lines.`; reused key -> 409 `Conflict`, detail `This Idempotency-Key was already used for a different request.`, no `errors`.
- CC6: malformed, `null`, wrong type: each a 400 problem on all three body routes; key still free.
- CC7: empty GET `{"items":[],"subtotal":{"amount":"0.00","currency":"USD"}}`, line/money/merge key sets and string amounts, no `Set-Cookie`.

Runs (filter `.Cart.|EndpointInventoryTests|CartCorsTests`):
- MVC with new file: first run `Failed: 1 (CC3 {} variantId message, my guess was wrong), Passed: 44, Total: 45`; after fixing the expected text `Passed!  - Failed: 0, Passed: 45, Total: 45` (38 existing + 7 CC).
- After conversion (+ `ContentTypeContractTests`): `Passed!  - Failed: 0, Passed: 56, Total: 56`.

## Mutations (each: break, red, `cp` from scratch copy, `cmp` equal, no output)
- M1 `UserId` fixed Guid: `Failed: 3` — `T21_one_users_writes_never_appear_in_another_users_cart`, `CC2_another_users_line...`, `T22_the_same_key_with_a_different_body_or_from_another_user_is_409...`.
- M2 drop `.RequireAuthorization()`: `Failed: 1` — `EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors`.
- M3 skip `IsValidKey`: `Failed: 2` — `CC5_a_merge_checks_the_key_first...`, `T22_a_missing_or_overlong_key_and_bad_bodies_are_400_and_write_nothing`.
- M4 conflict -> `Ok`: `Failed: 2` — `CC5...`, `T22_the_same_key_with_a_different_body_or_from_another_user_is_409...`.
- M5 no `available` extension: `Failed: 2` — `T21_a_quantity_above_available_is_409_with_available_and_changes_nothing`, `CC1...`.
- M6 Add skips validation: `Failed: 3` — `CC3...`, `T21_an_add_whose_resulting_quantity...(quantity: 0)` and `(quantity: -1)`.
- M7 `decimal? UnitPrice` on `AddCartItemRequest`: `Failed: 1` — `T21_a_body_carrying_a_price_is_400_and_changes_nothing`.

## verify.sh
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 272 suites / 2111 tests passed (no unrelated failure).

## Quality-bar §2
- [x] Tests for every money/stock/auth rule: CC1-CC7 plus existing T21/T22, inventory.
- [x] Hard invariants pinned before converting (green on MVC, quoted).
- [x] Mutations M1-M7 red, restored, `cmp` equal.
- [x] No existing test edited or weakened.
- [x] No `var`, no anonymous types, no `.Accepts`, no money or arithmetic in handlers.
- [x] No secrets, packages, migrations, contract changes.
- [x] verify.sh passed in full.
- N/A: no frontend change; Jest ran unchanged.
