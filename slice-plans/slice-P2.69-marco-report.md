Concerns: (1) M6 stayed green, so per plan I deleted `.Accepts<IFormFile>(...)`; inferred metadata is already non-optional. IP6/IP7 still guard it. (2) P2.69 plan is silent on the 404 hidden-by-fallback wording of IP7; kept 404 as quoted. No stop condition hit (IU8 green, IP7 404 unchanged, RawText matches the inventory).
Bugs found: AdminValidators.cs, null `variants`/`images` -> 500 after conversion (IP4, expected by plan P9)
Bugs fixed: P9 `.Cascade(CascadeMode.Stop)` on the four Variants/Images rules -> IP4 green
Bugs open: None
Tests changed: none existing edited. Added: AdminProductContractTests.cs (IP1-IP7), B6 in ProductQueryBindingTests.cs.

Step 1 baseline on MVC (AdminProductApi, AdminProductImageApi, ProductQueryBinding, EndpointInventory): Passed 33/33.
IU4 "none" text on MVC (used in P8): 400, errors.file[0] = "The file field is required."
IP1-IP7 on MVC: 8 run (incl. probe, since removed), IP1-IP7 all green. IP7 status = 404 on MVC.
B6 red first: `Expected: OK / Actual: BadRequest` (ProductQueryBindingTests.cs:91). Green after P6.
IP4 red after convert (before P9): `Expected: BadRequest / Actual: InternalServerError`. Green after P9.

Changed (slice P2.69):
- new Catalog/Api/Admin/AdminProductsEndpoints.cs (8 routes, ToResult<T,TOk>, upload conventions)
- deleted Catalog/Api/Admin/AdminProductsController.cs (plain rm)
- new Catalog/Api/QueryValues.cs (copy of Admin's, namespace Ecommerce.Catalog.Api)
- Catalog/Api/ProductEndpoints.cs (P6 only)
- Catalog/Api/Admin/AdminValidators.cs (P10 ToProblem + Mvc using removed, ToErrors summary reworded; P9 cascade x4)
- Catalog/CatalogModule.cs (one line)
- tests: Catalog/Admin/AdminProductContractTests.cs (new), Catalog/ProductQueryBindingTests.cs (B6)
- AGENT_STATUS.md

Mutations (each: scratch copy, break, red, restore, cmp OK):
- M1 policy Admin->AdminOrSupport: red EndpointInventoryTests.T1, IU3 (2 failed)
- M2 drop RequestSizeLimit metadata: red IP6 (1 failed)
- M3 Conflict arm -> 400: red IP1, IA2 x3, IA1, IA3, IA4 (7 failed)
- M4 Create validation return deleted: red IP3, IP4, IA1 (3 failed)
- M5 drop DisableAntiforgery: red IU1, IU4 x7, IF2, IF3, IP6 and more (500s)
- M6 drop .Accepts: stayed green (137 passed). Line deleted per plan; re-ran Catalog+inventory+auth tests: 142 passed.

verify.sh (full, before M6 line deletion): VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 272 suites / 2111 tests.
After M6 deletion: `verify.sh changed` VERIFY PASS (secrets hygiene build test); integration subset 142 passed.

Quality-bar §2: tests green; no var / anonymous types; module boundaries unchanged; thin handlers (one service call each); no secrets, packages, migrations or doc edits; no git writes; not committed, tracker not marked.
