# Slice 9.4 gap-fill — Marco report (Category edit and delete, backend)

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none changed or removed. Added only (see Changed).

## Red output (test-first, before any production change)
`AdminCategoryApiTests` against unchanged code (no PUT/DELETE routes, so 404 not the 405 the plan guessed):
- IC8 `delete_is_409_while_even_an_inactive_product_uses_the_category` — Expected: Conflict / Actual: NotFound
- IC9 `delete_is_409_while_a_subcategory_exists_then_child_first_succeeds` — Expected: Conflict / Actual: NotFound
(IC1-IC7 were red the same way.)

Plan 3.1 check, D6 catch broken (`ProductsForeignKey` compared to "nope"), IC8 alone:
`Expected: Conflict / Actual: InternalServerError` -> `Failed! Failed: 1`. Restored from scratch copy, `cmp` identical.
This proves the FK translation is what yields the 409. Green after implementation: IC1-IC9 + IA1-IA7 = 22 passed.

## Changed (slice 9.4 gap-fill)
New: `SharedKernel/Persistence/ForeignKeyViolationException.cs`; `Api.IntegrationTests/Catalog/Admin/AdminCategoryApiTests.cs`.
Edited:
- `Ecommerce.Persistence/EfUnitOfWork.cs` (const 23503 + two Translate arms; nothing else)
- Catalog: `Domain/Category.cs` (Rename + Validate), `Application/Admin/{AdminPorts,AdminProductCommands,AdminCategoryService,AdminProductService}.cs`, `Infrastructure/ProductWriteStore.cs`, `Api/Admin/{AdminRequests,AdminValidators,AdminCategoriesController}.cs`, `CatalogModule.cs`
- Tests: `Catalog.Tests/Domain/CategoryTests.cs`, `Application/AdminCategoryAndCriteriaTests.cs`, `Application/AdminProductServiceTests.cs` (additions only)

## Deviations from the plan
1. IC10 not re-added: IA7 (POST happy + dup 409 + bad parents) covers it and stayed green after the controller refactor.
2. IC3 is a Theory with the role as `string` (UserRole is internal to Identity; a public test method cannot take it).
3. `CategoryFieldRules.Add<T>` takes the validator plus two expressions (generic, because Create and Update request types differ).
4. Unit tests were written after the service code (plan 3 step 2 asked red-first); IC8/IC9 were red-first as instructed.
5. No docs, migration, package or frontend touched.

## verify.sh
VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)
build 0 Warning(s) 0 Error(s); Catalog.Tests 179 passed; jest 1103 passed; drift clean.

## Quality-bar §2
- [x] Tests for every new endpoint (happy, 401/403, 400, 404, 409) and domain/service rules
- [x] Hard invariants test-first with red quoted (IC8, IC9)
- [x] verify.sh green, no warnings
- [x] Rules 3.4: thin controller, no cross-module DbSet, no `var`, IClock unchanged, no external call in a tx
- [x] Module boundaries: only SharedKernel/Persistence touched outside Catalog; types internal
- [N/A] Mutations: Low tier
- [N/A] Migration/drift: none needed, drift clean
- [N/A] Frontend: out of scope (9.7)

## Fix round 1
Blocker: AdminCategoryService Update/Delete let ConcurrencyConflictException escape (500). Now caught in both, returning NotFound (404).
Tests (AdminCategoryAndCriteriaTests): Update_of_a_category_deleted_mid_request_is_NotFound, Delete_of_a_category_deleted_mid_request_is_NotFound.
Red before fix: `Failed! - Failed: 2, Passed: 179, Total: 181` (ConcurrencyConflictException : x thrown from both).
Green after: `Passed! - Failed: 0, Passed: 181, Total: 181 - Ecommerce.Catalog.Tests.dll`.
VERIFY PASS (secrets hygiene build test drift)
Files: Application/Admin/AdminCategoryService.cs, tests/Ecommerce.Catalog.Tests/Application/AdminCategoryAndCriteriaTests.cs
