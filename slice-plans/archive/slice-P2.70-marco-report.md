# Slice P2.70 — Marco report

Concerns: (1) Plan P3/IV6 said MVC reports `page` and `pageSize` together for `page=abc&pageSize=0`. Capture shows MVC returns only `page` (the `[ApiController]` model-state filter stops before the range checks). Per plan step 2 I pinned MVC's value and made the list handler two-stage (parse errors return alone, then range checks, which still report both keys: `page=0&pageSize=0`). Rob to confirm. (2) Log category is now `AdminInventoryEndpoints` (P5).
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None edited. New: `backend/tests/Ecommerce.Api.IntegrationTests/Inventory/AdminInventoryContractTests.cs` (IV1-IV7).

## MVC capture (before conversion, bodies trimmed of traceId)
- Stale 409: `{type …15.5.10, title "Conflict", status 409, detail "The on-hand count changed. Reload and try again.", correlationId, onHand 5, reserved 0, available 5}`
- Insufficient 409: same, detail "The change would leave less available than is reserved.", 5/3/2
- Unknown 404: `{type …15.5.5, title "Variant not found", status 404}`
- `{}` 400: title "One or more validation errors occurred.", errors delta ["Enter a change."], reason ["Enter a reason of 1 to 500 characters."], variantId ["'Variant Id' must not be empty."], expectedOnHand ["Enter the on-hand count you saw."]
- All-invalid 400: delta ["The change must not be 0 and must be within 1,000,000 either way."], reason [...500 characters.], variantId ["Choose a variant."], expectedOnHand ["The on-hand count cannot be negative."]
- `page=abc&pageSize=0` 400: errors `{page: ["The value 'abc' is not valid."]}` only
- `page=&pageSize=` 200: page 1, pageSize 20
- `page=2&page=x&pageSize=2` 200: page 2 (first value wins)
- Bad bodies (P8, differ on minimal API, asserted loosely): malformed/empty/null/wrong type each 400 with `errors` keys like `request`, `$.delta`.

## Results
- MVC baseline `~Inventory`: 43 passed. IV1-IV7 on MVC: 7 passed.
- After conversion `~Inventory`: 50 passed, 0 failed (43 existing unedited + 7 new).
- Controller deleted with `rm`. Service, domain, persistence untouched.

## Mutations (each restored from scratchpad copy, `cmp OK`)
- M1 Insufficient arm returns 200: Failed 3: IV2, T10, concurrent adjust/reserve.
- M2 Stale arm returns 200: Failed 2: IV1, `A_stale_expectedOnHand_is_409…`.
- M3 validation return skipped: Failed 8: IV4, `An_invalid_adjust_is_400_and_writes_nothing` x6, `A_501_character_reason…`.
- M4 policy to AdminOrSupport: Failed 2: `Both_endpoints_are_403_for_support…`, `T1_every_endpoint_has_the_expected_methods_auth_and_cors`.
- M5 actorId null: Failed 2: T10, `A_successful_adjust…` (row.ActorId).

Changed: AdminInventoryEndpoints.cs (new), Inventory/Api/QueryValues.cs (new, copy), AdminInventoryController.cs (deleted), InventoryModule.cs, ModuleComposition.cs, AdminInventoryContractTests.cs (new). Slice P2.70.

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); build 0 warnings; jest 272 suites / 2111 tests passed.

Quality-bar §2: all boxes met. No migration, no new package, no doc edits, no var, named records.
