# Slice P2.64 — Marco report

Concerns: None. I9-I13 matched MVC as the plan said; no pin changed.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none edited. Added I9-I13 to `AdminCustomersApiTests.cs` (I1-I8 untouched).

## Steps
1. Pins first: with I9-I13 added, on MVC, `AdminCustomersApiTests` + `EndpointInventoryTests` + `AuthorizationTests`: `Passed! Failed: 0, Passed: 21, Total: 21`.
2. Converted: new `Admin/Api/AdminCustomersEndpoints.cs` (group `api/v1/admin`, policy AdminOrSupport, `customers` and `customers/{id:guid}`, `string[]?` query values with `FirstOrDefault`, `ReadInt` copied from ProductEndpoints, two-stage validation, `TypedResults.Problem(404)`); controller deleted; `AdminModule.cs` calls `AdminCustomersEndpoints.Map(app)`.
3. Same 3 classes after convert, nothing edited: `Passed! Failed: 0, Passed: 21, Total: 21`.

## Mutations (break, red, restore from scratch copy, `cmp` clean each time)
- M1 group policy -> `Admin`: red `T1` (inventory), I1, I2 (3 failed).
- M2 drop `.RequireAuthorization`: red `T1`, II11, I1 (3 failed).
- M3 role flag -> `true`: red I2 (1 failed).
- M4 `pageSizeNumber is < 1` only: red I6 (`?pageSize=101`), I12 (2 failed).
- M5 `string.Join(",", search ?? [])`: red I11 (1 failed).

Changed: `Admin/Api/AdminCustomersEndpoints.cs` (new), `Admin/Api/AdminCustomersController.cs` (deleted), `Admin/AdminModule.cs`, `tests/Ecommerce.Api.IntegrationTests/Admin/AdminCustomersApiTests.cs`. Slice P2.64.

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); build 0 warnings; jest 272 suites, 2111 tests passed.

## Quality-bar §2
- [x] Behaviour-only: same routes, policy, binding, 400/404 bodies, one service call per handler; nothing under Application/ or other modules.
- [x] Tests: I1-I13 green, new pins test-first on MVC, none weakened.
- [x] Mutations M1-M5 red and restored.
- [x] Rules §3.4: thin handlers, no `var`, no new packages, no migrations, no docs.
- N/A: Nathaniel-drives files (none), tracker (not marked per instruction), commit (not made).
