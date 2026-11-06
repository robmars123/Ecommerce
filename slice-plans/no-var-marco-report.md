Concerns: None. Mechanical refactor only.
Bugs found/fixed/open: None
Tests changed: no test logic changed; only `var` replaced by explicit types in test files.
Changed: new root .editorconfig ([*.cs], the 4 settings only); Directory.Build.props (+EnforceCodeStyleInBuild=true); `dotnet format style Ecommerce.slnx --diagnostics IDE0008 --severity error` over backend/src and backend/tests (first pass reformatted 92 files; later passes finished the rest; exact file total not isolable from git because the tree already has uncommitted work). No Designer/snapshot/migration file was flagged or hand-edited.

`var` remaining: 14 lines, all accepted by IDE0008:
- 9 in Catalog Infrastructure (AdminCatalogReadStore.cs 49/68/83/123, CatalogReadStore.cs 45/69/77/93, ProductWriteStore.cs 43): LINQ queries projecting to anonymous types, or built on them.
- 4 in Api.IntegrationTests: AdminProductApiTests.cs 59/148/197 and AuthorizationTests.cs 42 are anonymous-type objects.
- 1 in ArchitectureTests/SolutionLayout.cs:68: `var` inside a list pattern (pattern variables, not declarations the analyzer flags).

Enforcement: the build now fails on `var` (IDE0008 = error). No other new errors or warnings surfaced.
verify.sh (full): VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); build 0 Error(s) 0 Warning(s).
Test counts (dotnet test, unchanged): Catalog 166, Identity 73, Architecture 25, Integration 120, Inventory 3, all passed. jest 45 suites / 280 passed.
Surprising: `dotnet format` needed to be re-run to converge (92 files on the first pass, 14 `var` lines left after the second, stable afterwards).

## Round 2 (zero var)
grep count of `var` declarations in backend .cs (excluding bin/obj): 0.

Changes (non-mechanical hunks are marked NON-MECHANICAL):
- NON-MECHANICAL: CatalogReadStore.cs, AdminCatalogReadStore.cs: anonymous projections replaced by private nested records (ListShape, DetailShape, VariantShape, ImageShape, CheapestShape; AdminListShape, AdminDetailShape, AdminPreviewShape and their variant/image shapes). Locals are now IQueryable<ListShape>, IOrderedQueryable<ListShape>, List<..>, and `X? row`. Ordering, tie-breakers, joins and filters are unchanged.
- Surprise: positional-ctor records broke translation (OrderBy on a member of a ctor-projected record gave "could not be translated", 21 integration tests failed). Fixed by making ListShape and AdminListShape property-initialized (`required ... init`) so EF binds members; the detail shapes stay positional (no ordering on them). All 120 integration tests pass again.
- NON-MECHANICAL: ProductWriteStore.cs: `new { c.Id, c.ParentId }` -> private record ParentRef(Guid Id, Guid? ParentId).
- AdminProductApiTests.cs (3) and AuthorizationTests.cs (1): `var x = new {...}` -> `object x` / `object? body`. JsonContent.Create(object) serializes the runtime type, so all properties still serialize. Assertions unchanged.
- SolutionLayout.cs:68: pattern `var module/folder/file` -> `string module/folder/file`.
- scripts/verify.sh step_hygiene: fails with "C# var found (explicit types only)" when `gg '(^|[^[:alnum:]_])var[[:space:]]+[@_[:alpha:]]' -- '*.cs'` matches; proved by planting a temp .cs with var (failed), then removed it.
- CatalogDevSeeder.cs: I made no hand edit to it in either round; any change there came from `dotnet format` (IDE0008 conversions). I can't separate it from earlier uncommitted work via git diff, so Rob should read it as a mechanical var-to-type hunk.

verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); build 0 Error(s) 0 Warning(s). Counts: Catalog 166, Identity 73, Architecture 25, Integration 120, Inventory 3, all passed; jest 280.
