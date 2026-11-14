Concerns: None. Hygiene prints an awk "cannot open" note for the deleted controller (git still lists it); the step still PASSes.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None edited. New `ProductQueryBindingTests.cs` (B1-B5), green on MVC (5/5) before the conversion, green after.
Changed: Catalog/Api/ProductEndpoints.cs (new), Catalog/Api/ProductsController.cs (deleted), CatalogModule.cs (+1 line), tests/Ecommerce.Api.IntegrationTests/Catalog/ProductQueryBindingTests.cs (new). Slice P2.60.
MVC message captured for page=abc: "The value 'abc' is not valid." (includes the raw value; hard-coded in ReadInt as $"The value '{raw}' is not valid.").
Re-run after conversion, unedited: Catalog, HttpContract, EndpointInventory, Authorization filter = 129 passed, 0 failed.
verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); build 0 warnings; jest 2111.
Quality-bar §2: all met. No mutations (Low tier). No Application/Infrastructure edits.

## Round 2

Fixed Rob's blocker (ProductEndpoints.cs ReadInt, whitespace). Added test B1b (`?page=%20&pageSize=%20` -> 200, page 1, pageSize 20).
- MVC check (controller restored from HEAD, products endpoints unmapped, converted files restored and cmp-identical): B1b PASSED on MVC (200 with defaults). Rob was right.
- Minimal API with `IsNullOrEmpty`: B1b RED (`Assert.Equal() Failure` at ProductQueryBindingTests.cs:28, status 400).
- Changed `ReadInt` to `string.IsNullOrWhiteSpace`: B1b green.
- `dotnet test Api.IntegrationTests --filter "ProductQueryBinding|Catalog"`: Passed 120, Failed 0.
- `verify.sh changed`: VERIFY PASS (secrets hygiene build test).
Concerns: None. Bugs open: None. Tests changed: B1b added only.
