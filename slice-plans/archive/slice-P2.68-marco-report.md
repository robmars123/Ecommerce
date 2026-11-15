# Slice P2.68 — Marco report

Concerns: None. The switch-expression `ToResult<TOk>` mapper compiled as planned (no per-handler copies).
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None edited. New: `AdminCategoryContractTests.cs` (IC11-IC15), all green on MVC first.

## Step 1: MVC bodies captured (traceId shortened)

- Unknown member `isActive` (POST): 400, title "One or more validation errors occurred.", errors `request` ("The request field is required.") + `$.isActive` ("...could not be mapped to any .NET member...").
- Malformed `{"slug":`: 400, same title, errors `request` + `$.slug` ("Expected depth to be zero...").
- Empty body: 400, same title, errors `""` ("A non-empty request body is required.") + `request`.
- Omitted `name`: 400, same title, errors `Name` ("The Name field is required.").
- Conflict (DELETE parent with child): 409, type 15.5.10, title "Conflict", errors `subcategories` ["This category has subcategories. Delete them first."].
- 404 (PUT `nope`): 404, type 15.5.5, title "Category not found", no errors.
All carry `type` 15.5.x, `traceId`, `correlationId":"id-test-1"`.

## Step 2-3: pins on MVC
Filter (AdminCategory|EndpointInventory|AdminProductApiTests|HttpContract) with the 5 new pins on MVC: `Passed: 32, Failed: 0`.

## Step 4-5: after conversion
Same filter: `Passed: 32, Failed: 0`. Arch tests: `Passed: 27`. No inventory row changed.

## Mutations (each restored from /tmp/p268 copy, `cmp` identical)
- M1 (remove `ConfigureHttpJsonOptions` line): `Failed: 3` - HttpContractTests.T5, AdminCategoryContractTests.IC12, AdminCategoryApiTests.IC4.
- M2 (group policy AdminOrSupport): `Failed: 2` - EndpointInventoryTests.T1, AdminCategoryApiTests.IC3 (Support/Forbidden).
- M3 (Update skips validator): `Failed: 1` - AdminCategoryApiTests.IC4.

Changed:
- new `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminCategoriesEndpoints.cs`
- deleted `.../Api/Admin/AdminCategoriesController.cs` (plain rm)
- `.../Api/Admin/AdminValidators.cs` (+`ToErrors`)
- `.../Ecommerce.Catalog/CatalogModule.cs` (+1 Map line)
- new `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/Admin/AdminCategoryContractTests.cs`
Tracker row and decision log not touched (Rob / coordinator).

verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; build 0 warnings; jest 272 suites / 2111 tests passed.

Quality-bar §2: all boxes met. Tests exist for every new behaviour (IC11-IC15); no secrets; no new packages; no migration/drift; one query path unchanged (1 validator + 1 service call per handler, no extra reads); docs/tracker N/A (Rob/coordinator).
