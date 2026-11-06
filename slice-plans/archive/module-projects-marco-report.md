# Module + Contracts refactor — Marco build report

Plan: `slice-plans/module-projects-plan.md` (READY, D1-D15). Not a slice; 1b not started. No git write commands used (plain `mv`/`rm`/`sed`). No package added (`Directory.Packages.props` untouched; D7 reuses the pinned `Microsoft.EntityFrameworkCore.Design`). Frontend: no file edited.

## Concerns

1. **Leftover process (needs the main session / Nathaniel).** I started the API for the response diff (`dotnet run --project backend/src/Ecommerce.Api --launch-profile http --no-build`). Stopping it was denied by the auto-mode classifier, so it is still running on `http://localhost:5080` (apphost `Ecommerce.Api` PID 1832, launcher `dotnet run` PID 2864, both started 16:43). It locks `backend/src/Ecommerce.Api/bin/Debug/...`, so a Debug rebuild of the Api project after a source change would fail until it is stopped. Final `dotnet build` / `dotnet test` below ran with it up (no source change after the last build). Please stop it (Ctrl+C in that shell, or `Stop-Process -Id 1832`).
2. **Show-it-fails break (c) cannot be done as written.** Making `CatalogBrowseService` public is rejected by the compiler (CS0050/CS0051: its constructor and members expose internal types), so the module cannot even build. I demonstrated the visibility rule with a leaf type instead (`Domain/Slug` made public). The compiler already enforces the original break; the test covers leaf types the compiler allows.
3. **Break (a) is caught by the csproj rule, not the IL rule** (an unused `ProjectReference` is not emitted into the compiled assembly's references). This is the reason the plan has both views; noted so the review is not surprised.
4. **dotnet-ef `--verbose`**: `Found IDesignTimeDbContextFactory implementations... Using DbContext factory 'DesignTimeDbContextFactory'.` The factory is used, so no Concern there.
5. **Two pre-existing CS8602 warnings** in `CatalogDevSeeder.cs` (lines 58, 66), carried over unchanged from `Infrastructure/Catalog/DevSeed`. Not touched (no behaviour change). Appear only on a clean/Release build.
6. **Starting the API runs the Development dev seeder** against the dev DB (`Catalog:DevSeed:Enabled` in appsettings.Development), exactly as when the baseline was captured. It is idempotent; nothing else touched the dev DB (`database update` was a no-op).
7. `ProductSearchCriteriaTests.Accepts_explicit_sorts` needed a signature tweak (see Deviations). Test name and display names are unchanged.

## Bugs found / fixed / open

- Bugs found: None
- Bugs fixed: None
- Bugs open: None

## Deviations from the plan (names/paths are otherwise exactly as planned)

| Item | Plan | As built | Why |
|---|---|---|---|
| `ProductSearchCriteriaTests.Accepts_explicit_sorts` | `ProductSort` parameter | parameter is `object expected`; assertion is `Assert.Equal((ProductSort)expected, r.Criteria!.Sort)` | `ProductSort` is now `internal`; a public test method cannot expose it (CS0051), and xUnit1000 forbids internal test classes. `[InlineData(..., ProductSort.X)]` is unchanged, so the three display names in the test list are identical to baseline. Assertion semantics unchanged. |
| Test helpers made `internal` | `CatalogTestEnv`, `VariantSpec`, `ImageSpec` | exactly these three; `FakeClock` stays public | as the plan's test-compile rules said |
| `SolutionLayout.SourceFiles` | not specified | returns empty for a missing folder | Inventory has no `Domain/`, `Application/`, `Api/` folders; the non-empty guards are asserted for Catalog only |
| Extra assertions in self-tests | listed | a positive control added inside `Reference_rules_accept_...` and `Layer_rules_reject_...` and `Source_scan_rejects_...` | prove the evaluators also accept legal input |
| Order | Persistence phase moved the factory using `AddInfrastructure` | as planned | no deviation |

Temporary allowances added to the old architecture tests (phases 1 and 2), now deleted with those tests: `DomainArchitectureTests` allowed `Ecommerce.SharedKernel`; `ApplicationArchitectureTests` allowed `Ecommerce.SharedKernel` then `Ecommerce.Inventory.Contracts`.

## Changed

### Old to new path table (as built, paths under `backend/`)

| Old | New |
|---|---|
| `src/Ecommerce.Application/Abstractions/IClock.cs` | `src/Ecommerce.SharedKernel/Time/IClock.cs` (`Ecommerce.SharedKernel.Time`) |
| `src/Ecommerce.Domain/Common/DomainException.cs` | `src/Ecommerce.SharedKernel/Domain/DomainException.cs` |
| `src/Ecommerce.Domain/DomainAssembly.cs`, `src/Ecommerce.Application/ApplicationAssembly.cs` | deleted (replaced by the architecture tests) |
| `src/Ecommerce.Infrastructure/Persistence/EcommerceDbContext.cs` | `src/Ecommerce.Persistence/EcommerceDbContext.cs` (rewritten per plan 3.2) |
| — | `src/Ecommerce.Persistence/IModuleDbModel.cs` (new) |
| `.../Persistence/DatabaseOptions.cs` | `src/Ecommerce.Persistence/DatabaseOptions.cs` |
| `.../Persistence/HealthCheckTags.cs` | `src/Ecommerce.Persistence/Health/HealthCheckTags.cs` |
| `.../Persistence/PostgresReadinessHealthCheck.cs` | `src/Ecommerce.Persistence/Health/PostgresReadinessHealthCheck.cs` (internal) |
| `.../DependencyInjection.cs` | split: `src/Ecommerce.Persistence/PersistenceServiceCollectionExtensions.cs`, `src/Modules/Catalog/Ecommerce.Catalog/CatalogModule.cs`, `src/Modules/Inventory/Ecommerce.Inventory/InventoryModule.cs`, `Program.cs` (`IClock`); deleted |
| `.../Persistence/DesignTimeDbContextFactory.cs` | `src/Ecommerce.Api/Composition/DesignTimeDbContextFactory.cs` (rewritten) |
| `.../Persistence/Migrations/*` (5 files) | `src/Ecommerce.Persistence/Migrations/*` (ns/using changed; entity strings `Ecommerce.Domain.Catalog.` to `Ecommerce.Catalog.Domain.` in AddCatalog designer + snapshot) |
| `src/Ecommerce.Infrastructure/Time/SystemClock.cs` | `src/Ecommerce.Api/Time/SystemClock.cs` |
| `src/Ecommerce.Application/Inventory/{IInventoryReader,StockStatus}.cs` | `src/Modules/Inventory/Ecommerce.Inventory.Contracts/` |
| `src/Ecommerce.Infrastructure/Inventory/UnknownInventoryReader.cs` | `src/Modules/Inventory/Ecommerce.Inventory/Infrastructure/UnknownInventoryReader.cs` (internal) |
| — | `src/Modules/Inventory/Ecommerce.Inventory/InventoryModule.cs`, both Inventory csproj files (new) |
| `src/Ecommerce.Domain/Catalog/*.cs` (5) | `src/Modules/Catalog/Ecommerce.Catalog/Domain/` (internal; `Domain.Slug.IsValid`; `Ecommerce.SharedKernel.Domain`) |
| `src/Ecommerce.Application/Catalog/*.cs` (8) | `src/Modules/Catalog/Ecommerce.Catalog/Application/` (internal) |
| `src/Ecommerce.Infrastructure/Catalog/{CatalogImageOptions,CatalogReadStore,ConfiguredImageUrlResolver}.cs` | `.../Ecommerce.Catalog/Infrastructure/` (internal) |
| `.../Catalog/Persistence/*Configuration.cs` (4) | `.../Ecommerce.Catalog/Infrastructure/Persistence/` + new `CatalogDbModel.cs` |
| `.../Catalog/DevSeed/*.cs` (3) + `src/Ecommerce.Api/Catalog/CatalogDevSeedHostedService.cs` | `.../Ecommerce.Catalog/Infrastructure/DevSeed/` (internal; hosted service adds `Microsoft.Extensions.*` usings, no Web SDK) |
| `src/Ecommerce.Api/Catalog/{CatalogResponses,CategoriesController,ProductsController}.cs` | `.../Ecommerce.Catalog/Api/` (internal; `ProductsController` adds `using Microsoft.AspNetCore.Http;`) |
| — | `.../Ecommerce.Catalog/CatalogModule.cs`, both Catalog csproj files; `src/Ecommerce.Api/Composition/{ModuleComposition,ModuleControllerFeatureProvider}.cs`; `src/Ecommerce.SharedKernel/Ecommerce.SharedKernel.csproj`; `src/Ecommerce.Persistence/Ecommerce.Persistence.csproj` (all new) |
| `tests/Ecommerce.Domain.Tests/Catalog/*.cs` (5) | `tests/Ecommerce.Catalog.Tests/Domain/` (ns `Ecommerce.Catalog.Tests.Domain`) |
| `tests/Ecommerce.Application.Tests/Catalog/*.cs` (3) | `tests/Ecommerce.Catalog.Tests/Application/` (ns `Ecommerce.Catalog.Tests.Application`) |
| `tests/Ecommerce.Domain.Tests/DomainArchitectureTests.cs`, `Application.Tests/ApplicationArchitectureTests.cs`, `Application.Tests/ClockRuleTests.cs` | replaced by `tests/Ecommerce.ArchitectureTests/` (8 files: `SolutionLayout`, `Rules`, `ProjectReferenceRuleTests`, `AssemblyReferenceRuleTests`, `ModuleVisibilityTests`, `LayerRuleTests`, `ModuleModelOwnershipTests`, `ClockRuleTests`, `RuleEvaluatorSelfTests`) |
| — | `tests/Ecommerce.Inventory.Tests/` (csproj + `UnknownInventoryReaderTests.cs`, 3 tests); `tests/Ecommerce.Api.IntegrationTests/ModuleCompositionTests.cs` (2 tests) |
| `tests/Ecommerce.Api.IntegrationTests/**` | same paths; usings only, plus `CatalogSchemaTests.CatalogModelHost` body (resolves context from `ApiFactory`) and the 3 helper types made `internal` |

Deleted projects: `Ecommerce.Domain`, `Ecommerce.Application`, `Ecommerce.Infrastructure`, `Ecommerce.Domain.Tests`, `Ecommerce.Application.Tests` (sources moved first; bin/obj removed with them). Directory moves were plain `mv`; no permission-denied lock occurred, so the copy-and-checksum fallback was not needed.

Build files: `Ecommerce.slnx` (new folders incl. `/src/Modules/`; the empty folder element parsed fine), `Dockerfile` (restore layer lists all 7 src projects), `.github/workflows/ci.yml` (two dotnet-ef steps now `--project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`). `.dockerignore` unchanged. `AGENT_STATUS.md` updated. README/architecture paths grep-checked against the tree: no mismatch found.

## Commands (real output)

### Per phase (`dotnet build Ecommerce.slnx` + `dotnet test Ecommerce.slnx`)

- Phase 1 SharedKernel: build 0 errors; tests Domain 44, Application 48, Integration 80 all passed.
- Phase 2 Inventory: build 0 errors; Domain 44, Application 48, Inventory 3, Integration 80 passed.
- Phase 3 Persistence: build 0 errors; same 44/48/3/80 passed. Drift: `Using DbContext factory 'DesignTimeDbContextFactory'.` / `No changes have been made to the model since the last migration.`; idempotent script identical to baseline.
- Phase 4 Catalog + architecture tests: build 0 errors; Inventory 3, Catalog 89, Architecture 24, Integration 80 passed. Drift clean; script identical.
- Phase 5 rules bite + composition tests: build 0 errors; Inventory 3, Catalog 89, Architecture 24, Integration 82 passed.
- Phase 6 build files: docker build succeeded; CI paths updated (below).

### Final

```
dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3 - Ecommerce.Inventory.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    89, Skipped:     0, Total:    89 - Ecommerce.Catalog.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    24, Skipped:     0, Total:    24 - Ecommerce.ArchitectureTests.dll (net10.0)
Passed!  - Failed:     0, Passed:    82, Skipped:     0, Total:    82 - Ecommerce.Api.IntegrationTests.dll (net10.0)
```
Total 198 (baseline 172).

### Test-name proof (artifacts/refactor-after/tests.clean.txt vs artifacts/refactor-baseline/tests.txt)

Namespace prefix stripped from both (`Ecommerce.<TestProject>.[Catalog|Domain|Application].`), then `comm`:
- `comm -23 baseline after` (baseline tests missing from after) returned exactly the three rewritten tests, each with a named replacement:
  - `ApplicationArchitectureTests.Application_references_only_Domain_among_project_assemblies` replaced by `LayerRuleTests.Application_types_depend_only_on_domain_application_contracts_and_SharedKernel` + `AssemblyReferenceRuleTests.Compiled_assembly_references_follow_the_module_rules`
  - `DomainArchitectureTests.Domain_references_no_project_or_infrastructure_assembly` replaced by `LayerRuleTests.Domain_types_depend_only_on_System_SharedKernel_and_their_own_domain` + `AssemblyReferenceRuleTests.SharedKernel_and_contracts_reference_no_infrastructure_framework`
  - `ClockRuleTests.Domain_and_Application_source_never_read_the_system_clock` replaced by `ClockRuleTests.No_source_outside_the_host_reads_the_system_clock`
- 169 of 172 baseline names carried over unchanged (Theory display names included, e.g. `Accepts_explicit_sorts(wire: "newest", expected: Newest)`); 29 new = 24 ArchitectureTests + 3 Inventory.Tests + 2 ModuleCompositionTests; 169 + 29 = 198.

### Migrations / dev DB

```
dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --verbose
  Found IDesignTimeDbContextFactory implementations...
  Found IDesignTimeDbContextFactory implementation 'DesignTimeDbContextFactory'.
  Using DbContext factory 'DesignTimeDbContextFactory'.
  No changes have been made to the model since the last migration.

CI steps exactly (Release build first, --configuration Release --no-build):
  has-pending-model-changes -> No changes have been made to the model since the last migration.
  migrations script --idempotent -> artifacts/migrations/migrate.sql (164 lines)
  diff artifacts/refactor-baseline/migrate.sql artifacts/migrations/migrate.sql -> empty; cmp -> byte identical

database update --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --connection "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce"
  No migrations were applied. The database is already up to date.
```

### API response diff (dev DB, 7 URLs, traceId stripped)

products, products-search, products-category, categories, product-blue-shirt, products-400 (`X-Correlation-ID: baseline-1`), product-404 (`baseline-2`): all IDENTICAL to `artifacts/refactor-baseline/*.json`. `/health/ready` 200, `/health/live` 200. Captured in `artifacts/refactor-after/`.

### Docker

`docker build -t ecommerce-api:refactor .` succeeded (image sha256:3e65106a...).

### Frontend (untouched, run for quality bar section 4)

- `npx tsc --noEmit` (frontend/apps/store): no output, clean.
- `npm test -- --ci`: Test Suites: 26 passed, 26 total; Tests: 174 passed, 174 total.
- `npx expo export --platform web`: `Exported: dist`.

### Prove-the-rules-bite (D12), each reverted from backups and re-verified green

(a) `Ecommerce.Inventory.csproj` gained `<ProjectReference ... Ecommerce.Catalog.csproj>`:
```
Failed Ecommerce.ArchitectureTests.ProjectReferenceRuleTests.Csproj_project_references_follow_the_module_rules
  Ecommerce.Inventory (module implementation) may reference only SharedKernel, Persistence and *.Contracts, but references Ecommerce.Catalog (ModuleImpl)
Failed!  - Failed: 1, Passed: 23, Total: 24
```
(b) `using Microsoft.EntityFrameworkCore;` added as line 1 of `Catalog/Domain/Slug.cs`:
```
Failed Ecommerce.ArchitectureTests.LayerRuleTests.Layer_source_files_never_name_a_forbidden_namespace
  backend\src\Modules\Catalog\Ecommerce.Catalog\Domain\Slug.cs:1 names Microsoft.EntityFrameworkCore in the Catalog Domain layer
Failed!  - Failed: 1, Passed: 23, Total: 24
```
(c) as planned, `CatalogBrowseService` public: does not compile (CS0050/CS0051, see Concern 2). Substitute: `Domain/Slug` public:
```
Failed Ecommerce.ArchitectureTests.ModuleVisibilityTests.A_module_implementation_exports_only_its_module_class
  Ecommerce.Catalog exports [Ecommerce.Catalog.CatalogModule, Ecommerce.Catalog.Domain.Slug], expected only [Ecommerce.Catalog.CatalogModule]
Failed!  - Failed: 1, Passed: 23, Total: 24
```
After reverting all three: Architecture 24/24 passed, full solution green (above). The pure evaluators are also proven by the six `RuleEvaluatorSelfTests`.

## Quality-bar section 2

(The quality-bar text itself was not changed; Rob's proposed text is plan section 10. Boxes read against the existing wording.)

- [x] Module boundaries respected: csproj references and IL checked by `Ecommerce.ArchitectureTests`; implementation types internal; no cross-module DbSet/join; controllers thin (moved verbatim).
- [x] No new package or service; stack lock unchanged (`Directory.Packages.props` untouched).
- [x] Server-owns-money / stock / webhook / outbox rules: N/A, no money, stock, payment or outbox code touched; behaviour unchanged (7 responses byte-identical).
- [x] `IClock`, never `DateTime.UtcNow`: only read remains `Ecommerce.Api/Time/SystemClock.cs`; clock rule widened to SharedKernel, Persistence, Modules.
- [x] Tests: every baseline test preserved by class+method (3 rewritten ones replaced by named stronger tests), assertions unchanged; count 172 to 198. Hard-invariant test-first: N/A (no new money/stock behaviour); the new architecture rules were shown failing on real violations, then reverted.
- [x] Migrations: drift clean, idempotent script byte-identical, dev DB `database update` no-op, `/health/ready` 200.
- [x] No secrets in code/logs; connection string in the design-time factory is the same non-secret local Compose value as before.
- [x] Docker build green; CI paths updated.
- [x] Frontend: untouched; tsc, jest (174), expo export all green.
- [x] No git write command; nothing committed.
- [ ] Clean shutdown of everything I started: NOT done. The dev API is still running (Concern 1) because stopping it was denied.

AGENT_STATUS.md: Marco set BUSY at start, AVAILABLE at the end.

## Fix round 1

Blocking item (Rob review): `CheckEcommerceName` in `backend/tests/Ecommerce.ArchitectureTests/Rules.cs` allowed another module's `.Contracts` in every layer, including Domain.

- Fix: for `second != module`, `.Contracts` is allowed unless `layer == "Domain"` (message: "the Domain layer may not use any .Contracts"). Own-module Contracts in Domain was already rejected (Domain allow-list is `["Domain"]`); unchanged.
- Test-first: added `RuleEvaluatorSelfTests.Source_scan_rejects_another_modules_contracts_in_a_domain_file` (NotEmpty for Domain + `using Ecommerce.Inventory.Contracts;`, NotEmpty for Domain + `using Ecommerce.Catalog.Contracts;`, Empty for Application + `using Ecommerce.Inventory.Contracts;`).
  - Red before fix: `Assert.NotEmpty() Failure: Collection was empty` at RuleEvaluatorSelfTests.cs:66 (Failed 1).
  - Green after fix: Passed 1.
- Show-it-fails (d): added `using Ecommerce.Inventory.Contracts;` to Catalog `Domain/Slug.cs` (Catalog references Inventory.Contracts, so it compiles):
  `Failed LayerRuleTests.Layer_source_files_never_name_a_forbidden_namespace` -> `backend\src\Modules\Catalog\Ecommerce.Catalog\Domain\Slug.cs:1 names Ecommerce.Inventory.Contracts: the Domain layer may not use any .Contracts`. Reverted (file restored from copy); re-run: Passed 1.
- Full run (Release): `dotnet build -c Release --no-incremental` 0 errors (2 pre-existing CS8602 warnings in CatalogDevSeeder); `dotnet test -c Release`: Inventory 3, Catalog 89, Architecture 25, Integration 82 = 199 passed, 0 failed.
- Test-name diff vs baseline (namespace-stripped): missing = exactly the 3 planned replacements (Application_references_only_Domain_among_project_assemblies, Domain_references_no_project_or_infrastructure_assembly, Domain_and_Application_source_never_read_the_system_clock); 30 new (169 + 30 = 199).
- Drift: `has-pending-model-changes` -> "No changes have been made to the model since the last migration."
- Files: Rules.cs, RuleEvaluatorSelfTests.cs (tests project only; no production code touched).
