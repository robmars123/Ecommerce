# Module + Contracts refactor — Rob review

**Mode:** Review · **Date:** 2026-11-03 · **Verdict (initial): BLOCKED** (1 blocking item) · **Verdict after fix round 1: PASS** (see the end of this file)
Plan: `slice-plans/module-projects-plan.md` · Marco's report: `slice-plans/module-projects-marco-report.md`

Note on method: `backend/` is entirely untracked (the last commit predates Slice 0), so `git diff` cannot show the pre-refactor source and the deleted projects are gone. I reviewed by reading every new/moved file and verified "no behaviour change" through the artifacts Rob captured before the refactor: test names (`tests.txt`), the migration script (`migrate.sql`), and the 7 HTTP responses.

## Blocking failures

1. **`backend/tests/Ecommerce.ArchitectureTests/Rules.cs:233` — the Domain source-scan rule is weaker than the test it replaces and weaker than plan §5.1.**
   `CheckEcommerceName` returns `null` (allowed) for another module's `.Contracts` in **every** layer, Domain included. Plan §5.1: Domain files may name `Ecommerce.` only as `Ecommerce.SharedKernel` or `Ecommerce.<M>.Domain`. The signature walk (`LayerRules`, Domain case) does reject a Contracts type in a *signature*, but a Domain **method body** using `Ecommerce.Inventory.Contracts` (e.g. `using Ecommerce.Inventory.Contracts;` + `StockStatus.Out` inside a method) passes both views. The deleted `DomainArchitectureTests.Domain_references_no_project_or_infrastructure_assembly` would have failed that, because the Domain assembly could reference no project at all. A replacement architecture test must be at least as strong as the one it replaces (no test weakened). This is a gap, not a style point.
   **Fix:**
   - `Rules.cs:233`: when `second != module`, allow `third == "Contracts"` only if `layer != "Domain"`. Otherwise return a violation, e.g. `names {name}: the Domain layer may use only SharedKernel and its own Domain`. (Own-module Contracts in Domain is already rejected by the `"Domain" => ["Domain"]` list at line 237. Keep it that way.)
   - Self-test: in `RuleEvaluatorSelfTests` add `Source_scan_rejects_another_modules_contracts_in_a_domain_file`, asserting `NotEmpty` for `("Catalog", "Domain", …, ["using Ecommerce.Inventory.Contracts;"])` and `NotEmpty` for `("Catalog", "Domain", …, ["using Ecommerce.Catalog.Contracts;"])`, plus `Empty` for `("Catalog", "Application", …, ["using Ecommerce.Inventory.Contracts;"])` as the positive control. Do not change existing assertions.
   - Show-it-fails (d): add `using Ecommerce.Inventory.Contracts;` as line 1 of `Catalog/Domain/Slug.cs`, then paste the failing `LayerRuleTests.Layer_source_files_never_name_a_forbidden_namespace` message. Revert, paste green.
   - Re-run `dotnet build`, `dotnet test` (expect 199), and `--list-tests` name diff.

## Bugs found
- The rule gap above (counted as a bug: an architecture invariant is weaker than specified). Nothing else.

## Rulings on Marco's 7 Concerns

| # | Ruling |
|---|---|
| 1 Leftover API (PID 1832, :5080) | Environment item, not a code defect. Still running at review time (start 16:43:35). It does not block the verdict, but the main session or Nathaniel must stop it (`Stop-Process -Id 1832`, and the `dotnet run` launcher, PID 2864). It is the only reason a Debug rebuild of `Ecommerce.Api` could fail after a source edit. |
| 2 Break (c) substitute | **Accepted.** Marco's analysis is correct: `CatalogBrowseService` exposes internal types in its constructor and members, so making it public is a CS0050/CS0051 compile error. The compiler already enforces it for any type with an internal type in its signature. The test is what stops leaf types the compiler allows, and the `Slug` break exercised exactly that test (`A_module_implementation_exports_only_its_module_class` failed with the right message). Plan §5.1 amended. |
| 3 Break (a) caught by csproj rule only | **Accepted, by design (D12).** An unused `ProjectReference` is not emitted into IL. That is why both views exist. |
| 4 dotnet-ef used the factory | Confirmed myself: `Using DbContext factory 'DesignTimeDbContextFactory'.` |
| 5 CS8602 warnings | **They appear in CI.** CI runs a clean `dotnet build -c Release`; my `--no-incremental` Release build shows exactly these 2 (`CatalogDevSeeder.cs(58,31)`, `(66,17)`). They do **not** fail CI: there is no `TreatWarningsAsErrors`. They are not new: the seeder body moved unchanged. They are nullable flow-analysis false positives (`!TryGetValue(…, out var product)`, then `product` is assigned on the `isNew` branch, so it is non-null on both paths). No runtime bug. Quality bar §4.1: only *new* warnings are a review note before Slice 7, so they are not blocking. Do not suppress them in this refactor (no behaviour or body changes). They must be fixed when warnings-as-errors lands in Slice 7, or the next time the seeder is touched, by restructuring the branch, not with `!`. The Slice 1a "0 Warning(s)" claims came from incremental builds, which don't re-report warnings. Reviews now run a `--no-incremental` Release build. |
| 6 Dev seeder ran on API start | Accepted. Idempotent, Development only, same as the baseline capture. `database update` is a no-op (re-verified). |
| 7 `Accepts_explicit_sorts(string wire, object expected)` | **Accepted, not weakened.** `ProductSort` is internal, xUnit1000 forbids an internal test class, and a public method can't expose it (CS0051). `[InlineData]` values are unchanged and the display names are byte-identical to baseline (verified in my name diff, e.g. `expected: Newest`). `Assert.Equal((ProductSort)expected, …)` still fails on a wrong value, and a non-`ProductSort` argument throws `InvalidCastException`, which also fails. |

## Checks against plan D1–D15 and quality-bar §3

| Gate | Result |
|---|---|
| Stack lock / no new package (D7, D12) | `Directory.Packages.props` untouched; only pinned packages referenced; architecture tests use System.Reflection + System.Xml.Linq only. ✔ |
| Project references (modules → SharedKernel, Persistence, Contracts; Contracts → SharedKernel only) | All 7 src csproj read: exactly as plan §2. Enforced in csproj (`Csproj_project_references_follow_the_module_rules`) and IL (`Compiled_assembly_references_follow_the_module_rules`). ✔ |
| Internal visibility, one public type per module (D5) | Grepped every top-level type: only `CatalogModule`, `InventoryModule` public in impl projects; `IInventoryReader`, `StockStatus` public in Contracts. Tested via `GetExportedTypes()`. ✔ |
| InternalsVisibleTo limits (D8, D15) | Catalog → `Ecommerce.Catalog.Tests`, `Ecommerce.Api.IntegrationTests`; Inventory → `Ecommerce.Inventory.Tests`. Explicit per csproj. Test asserts no src assembly is ever granted IVT. ✔ |
| Single `EcommerceDbContext` + `IModuleDbModel` (D1, D2) | As §3.2; empty-module guard; `CatalogDbModel` filters by its namespace; `TryAddEnumerable` (idempotent). `ModuleModelOwnershipTests` meaningful (non-vacuous asserts). ✔ |
| Migrations unchanged (D3) | Drift clean; idempotent script **byte-identical** (`cmp`); runtime-model test + migration-discovery test present; dev DB "already up to date". ✔ |
| Design-time factory in host (D3) | `Ecommerce.Api/Composition/DesignTimeDbContextFactory.cs`, same `AddEcommercePersistence` + `AddEcommerceModules` as runtime, non-secret local connection only. ✔ |
| Controllers internal + feature provider (D6) | As §3.5. HTTP diff identical proves discovery; `Controllers_live_only_in_a_module_Api_namespace` non-vacuous. ✔ |
| Clock rule widened (D4) | Scans SharedKernel + Persistence (incl. Migrations) + Modules, with non-vacuous asserts. Only clock read in src: `Ecommerce.Api/Time/SystemClock.cs:7`. ✔ |
| Architecture tests meaningful + self-tests (D12, D13) | All §5.1 tests present by name, six self-tests with positive controls, three show-it-fails pasted. **Except blocking item 1.** ✘ |
| No test weakened, by name vs `tests.txt` | My own `--list-tests` diff: 172 → 198; `comm -23` = exactly the 3 planned replacements; 29 new. Moved tests read; only the planned using/visibility/helper edits seen. ✔ |
| Dockerfile / CI / slnx | Restore layer lists all 7 src csproj; runtime stage unchanged; `.dockerignore` unchanged; CI dotnet-ef steps updated, rest unchanged; slnx matches plan (empty `/src/Modules/` folder parses). ✔ |
| Frontend untouched | No file under `frontend/` (excluding node_modules/dist/.expo) is newer than the baseline capture (16:22). ✔ |
| Thin controllers, no cross-module DbSet/join (rules 7–8) | Controllers moved verbatim; entities internal, so cross-module `Set<T>` cannot compile. ✔ |

## Non-blocking notes
- The clock rule excludes the whole host. A `DateTime.UtcNow` in host middleware would pass. That matches the plan and is strictly wider than before. Consider narrowing to "host except `Time/SystemClock.cs`" in Slice 7.
- `Program.cs` registers `IClock` after persistence and modules (the plan listed it first). Registration order doesn't matter for these singletons; no change needed.
- AGENT_STATUS `Updated` times (18:55, 19:45) don't match file mtimes (16:2x–16:4x local). Use the real local clock.
- When Slice 2 adds `inventory_stock`, `CatalogSchemaTests`' exact table list must be scoped to Catalog-owned entities (plan §8; carry into the Slice 2 plan).

## Risks still open
- Dev API PID 1832 / launcher 2864 still running (Concern 1). It must be stopped before the next Debug build of `Ecommerce.Api`.
- 2 pre-existing CS8602 warnings become build failures when Slice 7 enables warnings-as-errors.

## Commands (my own runs, real output)
```
dotnet build Ecommerce.slnx                       Build succeeded. 0 Warning(s) 0 Error(s)   (incremental; Debug not locked: up to date)
dotnet build Ecommerce.slnx -c Release --no-incremental
                                                  Build succeeded. 2 Warning(s) 0 Error(s)  (CS8602 CatalogDevSeeder.cs 58,31 / 66,17)
dotnet test Ecommerce.slnx --no-build             Inventory 3 · Catalog 89 · Architecture 24 · Integration 82 — all Passed (198)
dotnet test Ecommerce.slnx --no-build -c Release  same 3/89/24/82, all Passed
--list-tests name diff vs artifacts/refactor-baseline/tests.txt
                                                  198 vs 172; missing = exactly the 3 planned replacements; 29 new
dotnet-ef has-pending-model-changes (CI form, Release --no-build, --verbose)
                                                  Using DbContext factory 'DesignTimeDbContextFactory'. / No changes have been made to the model since the last migration.
dotnet-ef migrations script --idempotent (CI form) 164 lines; cmp vs baseline: BYTE-IDENTICAL
dotnet-ef database update (dev DB 5434)           No migrations were applied. The database is already up to date.
HTTP diff (running API, started 16:43:35 > last source edit 16:42:26 / DLL 16:42:28; traceId normalised)
                                                  products, products-search, products-category, categories, product-blue-shirt,
                                                  products-400 (baseline-1), product-404 (baseline-2): all IDENTICAL
                                                  /health/live 200 · /health/ready 200 · CORS allow-origin + exposed X-Correlation-ID unchanged
docker build -t ecommerce-api:refactor-review .   succeeded (sha256:3e65106a…, same digest as Marco's)
frontend: npx tsc --noEmit                        exit 0
frontend: npm test -- --ci                        Test Suites: 26 passed · Tests: 174 passed
frontend: npx expo export --platform web          Exported: dist
CI secret scan (git grep --untracked, CI pattern) clean
CI hygiene greps                                  no tests under app/ · no focused tests
```

## Doc sync (done by Rob in this review)
- `docs/ecommerce-architecture.md` §6.2 *Visibility*: corrected who adds the application parts (`ModuleComposition.AddModuleControllers()` adds parts and registers `ModuleControllerFeatureProvider`).
- §6.2 *Inside a module*: Domain may not use any Contracts (own or other). Application may use Contracts. This matches the intended rule that blocking item 1 restores.
- `slice-plans/module-projects-plan.md` §5.1: break (c) substitute accepted; break (d) added.
- README, §6.2 layout, §13.2 rows: every name and path checked against the tree. No other mismatch.

## Final §10 text (FINAL — apply verbatim; names and every anchor string re-verified against the tree in the re-review)

### 10.1 `docs/quality-bar.md`
§2, replace the first checkbox:
> `- [ ] Module boundaries respected — no cross-module `DbSet` or join, no logic in controllers (rules 7–8).`

with:
> `- [ ] Module boundaries respected (rules 7–8, architecture §6.2): a module references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts` — never another module's implementation project; implementation types are `internal` (the module's only public type is `<Module>Module`); no cross-module `DbSet` or join; no logic in controllers; new module code has tests in `Ecommerce.<Module>.Tests` and `Ecommerce.ArchitectureTests` is green.`

§3 Blocking, replace:
> `- [ ] **Boundaries:** module contracts only; thin controllers; dependency direction Api → Application → Domain.`

with:
> `- [ ] **Boundaries:** module contracts only — a module project references only SharedKernel, Persistence, and other modules' `*.Contracts`, never another module's implementation or the host; implementation types `internal`, `InternalsVisibleTo` only to the module's test project (and `Ecommerce.Api.IntegrationTests`); thin controllers; inside a module, dependency direction Api → Application → Domain (folders/namespaces), Domain uses only SharedKernel and itself; per-module tests live in `Ecommerce.<Module>.Tests`; architecture tests (`Ecommerce.ArchitectureTests`) exist for every rule, are green, and were not weakened.`

§4 item 2, after "`dotnet test` (domain + application)." insert:
> ` This includes the per-module test projects and `Ecommerce.ArchitectureTests` (module reference, visibility, layer, and clock rules — architecture §6.2).`

§4 item 4, append:
> ` Command: `dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`.`

### 10.2 `CLAUDE.md`
In "Rules you're most likely to break", keep the line
> `- Thin controllers. No cross-module `DbSet` or join. `IClock`, never `DateTime.UtcNow`.`

and add directly after it:
> `- A module (`backend/src/Modules/<M>/Ecommerce.<M>`) references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts`. Implementation types are `internal`; the only public type is `<M>Module`. `Ecommerce.ArchitectureTests` enforces it (architecture §6.2).`

In "Commands", add after the `dotnet test` line:
> `dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`

### 10.3 `~/.claude/agents/marco.md`
In "How you work", replace the end of the **Rules** bullet, "…thin controllers, module boundaries, `IClock`.", with:
> `…thin controllers, module boundaries (architecture §6.2: a module references only SharedKernel, Persistence, and other modules' `*.Contracts`; implementation types `internal`; unit tests in `Ecommerce.<Module>.Tests`), `IClock`.`

### 10.4 `~/.claude/agents/rob.md`
In "Review mode", add after "Review the working-tree diff…":
> `- Boundaries (architecture §6.2): check csproj references and `Ecommerce.ArchitectureTests` — a module references only SharedKernel, Persistence, and other modules' `*.Contracts`; implementation types are `internal`; an architecture test changed in the diff gets the same scrutiny as a weakened invariant test. Run `dotnet build -c Release --no-incremental` to see the warnings CI sees.`

## Re-review checklist (fix round)
Rules.cs Domain branch fixed · new self-test present (199 tests) · show-it-fails (d) pasted and reverted · existing assertions untouched · `dotnet build` / `dotnet test` / name diff re-run.

## Files reviewed / changed
Reviewed: every csproj under `backend/`; `Ecommerce.slnx`; `Dockerfile`; `.dockerignore`; `.github/workflows/ci.yml`; `backend/src/Ecommerce.Api/{Program.cs,Composition/*,Time/SystemClock.cs,Health/HealthEndpoints.cs}`; all of `backend/src/Ecommerce.Persistence/*.cs` + `Health/*`; `backend/src/Ecommerce.SharedKernel/**`; `backend/src/Modules/Inventory/**`; `backend/src/Modules/Catalog/Ecommerce.Catalog/{CatalogModule.cs,Infrastructure/Persistence/CatalogDbModel.cs,Infrastructure/DevSeed/CatalogDevSeeder.cs}`; type declarations of every Catalog file; migration entity strings; all of `backend/tests/Ecommerce.ArchitectureTests/*`; `backend/tests/Ecommerce.Inventory.Tests/*`; `ModuleCompositionTests.cs`; `CatalogSchemaTests.CatalogModelHost`; `ProductSearchCriteriaTests.Accepts_explicit_sorts`; README; architecture §6.1, §6.2, §13.2.
Changed by Rob: `docs/ecommerce-architecture.md` (§6.2, two sentences); `slice-plans/module-projects-plan.md` (§5.1 amendment); `slice-plans/module-projects-rob-review.md` (this file); `AGENT_STATUS.md`.

## Re-review after fix round 1

**Verdict: PASS** · 2026-11-03 · Blocking failures: None · Bugs found: None

### Blocking item 1: resolved
- `Rules.cs:231-239`: when `second != module`, a non-Contracts name is still rejected. A `.Contracts` name now returns `names {name}: the Domain layer may not use any .Contracts` when `layer == "Domain"`, and is allowed otherwise. Own-module Contracts in Domain is still rejected by the unchanged `"Domain" => ["Domain"]` list. The Application and Api lists are unchanged. The regex `\bEcommerce(\.[A-Za-z_][A-Za-z0-9_]*)+` matches fully qualified names in method bodies as well as `using` lines, so `Ecommerce.Inventory.Contracts.StockStatus.Out` inside a Domain method is now caught. The Domain source scan is now at least as strong as the deleted `Domain_references_no_project_or_infrastructure_assembly` for Contracts.
- `RuleEvaluatorSelfTests.Source_scan_rejects_another_modules_contracts_in_a_domain_file` is meaningful. Assertion 1 (Domain + other module's Contracts) is the regression: the old code returned `null` for it, so it was red before the fix. That matches Marco's pasted `Collection was empty`. Assertion 2 (own Contracts) pins the existing rejection. Assertion 3 (Application + other Contracts → Empty) is the positive control, so a rule that rejected everything would fail. The existing self-tests are unchanged: read in full, and the 6 earlier facts and their assertions are as reviewed.
- Show-it-fails (d): Marco's failure message names `Slug.cs:1` with the new rule text. Accepted.
- `Slug.cs` is reverted: the content is the original `internal static partial class Slug` with no `using Ecommerce.*`. It uses CRLF and has no BOM, the same as its sibling `Domain/*.cs` files (`Category.cs`, `Product.cs`). No stray backup copy is in the repo.

### Nothing else changed
`find -newer` against this review file (16:55:43), across `backend/`, `.github/`, `Dockerfile`, `Ecommerce.slnx`, `docs/`, `frontend/` (excluding bin/obj/node_modules/dist/.expo), and the repo root, shows only `Rules.cs` (16:56:51), `RuleEvaluatorSelfTests.cs` (16:56:40), `Slug.cs` (16:57:05, reverted, content verified), `AGENT_STATUS.md`, and Marco's report. No production code changed.

### Commands (my own runs, real output; Release because PID 1832 locks Api/bin/Debug)
```
dotnet build Ecommerce.slnx -c Release --no-incremental
    Build succeeded. 2 Warning(s) 0 Error(s)   (pre-existing CS8602 CatalogDevSeeder.cs 58,31 / 66,17; ruled non-blocking)
dotnet test Ecommerce.slnx -c Release --no-build
    Inventory 3 · Catalog 89 · Architecture 25 · Integration 82 — Failed 0 (199 passed)
--list-tests name diff vs artifacts/refactor-baseline/tests.txt (namespace-normalised)
    199 vs 172; missing = exactly the 3 planned replacements
    (Application_references_only_Domain_among_project_assemblies, Domain_and_Application_source_never_read_the_system_clock,
     Domain_references_no_project_or_infrastructure_assembly); 30 new (29 + the new self-test)
dotnet-ef migrations has-pending-model-changes (CI form, Release --no-build)
    No changes have been made to the model since the last migration.
dotnet-ef migrations script --idempotent (CI form) → 164 lines; cmp vs baseline: BYTE-IDENTICAL
CI secret scan (git grep --untracked, CI pattern)   clean
CI hygiene greps (frontend/apps/store)              no tests under app/ · no focused tests
```
Not re-run (no frontend or host change since the first review, verified by mtime): HTTP diff, docker build, tsc, Jest, expo export. They passed in the first review.

### §10 text
Final, and it needs no change from the fix. Every anchor string it replaces or appends to exists verbatim: quality-bar §2 line 30, §3 line 51, §4 item 2 line 78, §4 item 4 ("(drift check)."); CLAUDE.md line 65 and the `dotnet test` line 83; marco.md line 21 ("…module boundaries, `IClock`."); rob.md line 31 ("Review the working-tree diff…"). The main session applies it. Rob does not edit CLAUDE.md or agent files.

### Still open (non-blocking, carried)
- Dev API PID 1832 / launcher 2864 must be stopped before the next Debug build of `Ecommerce.Api`.
- 2 CS8602 warnings must be fixed (by restructuring, not with `!`) before Slice 7 warnings-as-errors.
- Slice 2 plan: scope `CatalogSchemaTests`' exact table list to Catalog-owned entities.
