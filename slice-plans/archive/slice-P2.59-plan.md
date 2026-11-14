# Slice P2.59 — Minimal API: shared setup + Categories (Rob)

Tier: **High** (changes an architecture test, the mass-assignment JSON defence, and adds the auth/CORS inventory every later slice relies on). Full review + 3 mutations.

**Read:** this plan · `slice-plans/minimal-api-migration-plan.md` §2.1 rows D1, D4, D6, D10 and §4 "Standard steps" · `git -c safe.directory='*' show d01ae3c -- backend/` (the sample) · `backend/src/Ecommerce.Api/Program.cs:32–40` · `backend/tests/Ecommerce.Api.IntegrationTests/Identity/AuthorizationTests.cs:80–125` (II11 pattern) · `backend/tests/Ecommerce.ArchitectureTests/ModuleVisibilityTests.cs:55–75`, `LayerRuleTests.cs:20–45`, `RuleEvaluatorSelfTests.cs:40–75` · `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/CatalogValidationTests.cs:1–40` · `ProblemDetailsTests.cs`. Nothing else unless a step names it.

Note: the Categories sample is **already committed** (`d01ae3c`, unreviewed). This slice finishes it; Rob reviews `git show d01ae3c -- backend/` together with the working-tree diff.

## 1. Decisions (slice-specific; shared ones are D1–D14 in the migration plan)

| Decision | Reason | Alternative rejected |
|---|---|---|
| Rename `Api/CatalogEndpoints.cs` → `Api/CategoryEndpoints.cs`, class `CategoryEndpoints`, method `public static void Map(IEndpointRouteBuilder app)`; `CatalogModule.MapCatalogEndpoints` calls `CategoryEndpoints.Map(app)` and returns `app` | D1: one file per former controller; Products/Images/Admin files join in later slices | One `CatalogEndpoints` class growing to five controllers |
| Single route, no group: `app.MapGet("api/v1/categories", List).AllowAnonymous();` handler renamed `List`; drop `.WithName("ListCategories")` | D4; `WithName` has no consumer (no link generation anywhere) | Keeping the `api/v1` group for one route |
| Shared JSON: a host local function `static void UseStrictJson(JsonSerializerOptions json)` in `Program.cs` (string enums + `UnmappedMemberHandling.Disallow`, keep the D13 comment), called from `AddJsonOptions(o => UseStrictJson(o.JsonSerializerOptions))` and `builder.Services.ConfigureHttpJsonOptions(o => UseStrictJson(o.SerializerOptions))` | D6. Only other `Http.Json` consumer today is none: health checks write plain text, no `ReadFromJsonAsync`/`WriteAsJsonAsync` in `src` (Rob grepped) | Two copies |
| Inventory rows are plain strings `"<METHODS> <route> | <policies> | <anon|auth> | <cors>"`, sorted, compared with `Assert.Equal(expected, actual)` | Readable diff on failure; no record type needed | A record + custom comparer |
| Arch rule: rename `Controllers_live_only_in_a_module_Api_namespace` → `Http_endpoints_are_declared_only_in_a_module_Api_namespace` and extend it (D10); the scan is a pure function in `Rules.cs` with a self-test | Same guard, plus minimal endpoints; non-vacuous at zero controllers | Second separate test (two tests for one rule) |
| CORS (D7) is **not** in this slice | Categories uses the default policy; no consumer until P2.62 | — |

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/CatalogEndpoints.cs` | delete (replaced by the next row) |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/CategoryEndpoints.cs` | new: `internal static class CategoryEndpoints` — `Map` + `private static async Task<Ok<CategoryListResponse>> List([FromServices] CatalogBrowseService catalog, CancellationToken ct)` (body unchanged from the sample) |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/CatalogModule.cs` | `MapCatalogEndpoints` calls `CategoryEndpoints.Map(app); return app;` |
| `backend/src/Ecommerce.Api/Program.cs` | `UseStrictJson` + `ConfigureHttpJsonOptions` (D6). `app.MapModuleEndpoints()` stays where it is |
| `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` | unchanged from the sample |
| `backend/tests/Ecommerce.Api.IntegrationTests/EndpointInventoryTests.cs` | new (§4 T1) |
| `backend/tests/Ecommerce.Api.IntegrationTests/HttpContractTests.cs` | new (§4 T2–T5) |
| `backend/tests/Ecommerce.ArchitectureTests/Rules.cs` | new pure check `EndpointRules.Violations(string pathInModule, IReadOnlyList<string> lines)` |
| `backend/tests/Ecommerce.ArchitectureTests/ModuleVisibilityTests.cs` | rename + extend the controller test (D10) — **changed test, justify in report** |
| `backend/tests/Ecommerce.ArchitectureTests/RuleEvaluatorSelfTests.cs` | 2 new self-tests |

No migrations, no packages, no `*.Contracts`, no frontend.

## 3. Module contracts

- Public surface added to `Ecommerce.Catalog`: `CatalogModule.MapCatalogEndpoints(this IEndpointRouteBuilder)` only (already in the sample). `CategoryEndpoints` is `internal`. `A_module_implementation_exports_only_its_module_class` must stay green unedited.
- `EndpointRules.Violations`: input = path relative to the module implementation directory (e.g. `Application/Foo.cs`) + file lines; output = one message per line matching `\.Map(Get|Post|Put|Delete|Patch|Methods|Group)\(` when the first path segment is not `Api`. Text scan, comments included (plain; no false negatives).

## 4. Build order and required tests

Run integration tests with Docker Postgres up (CLAUDE.md §8). One integration run at a time.

**Step 0 — baseline.** `dotnet test backend/tests/Ecommerce.ArchitectureTests -c Release` and the integration project filtered to `Catalog|Authorization|ProblemDetails` on HEAD; quote counts.

**Step 1 — test-first (quote red for each):**
- **T5 JSON options (red first).** In `HttpContractTests`, no DB (`ApiFactory.Unreachable`): resolve `IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>`; assert `SerializerOptions.UnmappedMemberHandling == Disallow` and a `JsonStringEnumConverter` is present. Red before `ConfigureHttpJsonOptions`.
- **D10 self-tests (red first).** `RuleEvaluatorSelfTests`: (a) `Application/Foo.cs` with a `app.MapGet(` line → one violation; (b) `Api/FooEndpoints.cs` with the same line and `CatalogModule.cs` with `CategoryEndpoints.Map(app)` → none. Write them against a stub returning `[]`, quote red, then implement.

**Step 2 — implement:** `UseStrictJson` + `ConfigureHttpJsonOptions`; `EndpointRules`; rename/extend the arch test: (a) the existing `ControllerBase` check unchanged; (b) for every `ModuleImpl` project, scan `SolutionLayout.SourceFiles(project.Directory)` with `EndpointRules.Violations(Path.GetRelativePath(project.Directory, file), File.ReadAllLines(file))`; count Api files that contain a mapping; (c) `Assert.True(controllers + endpointFiles > 0, "…vacuous…")`. Rename the sample file to `CategoryEndpoints` per §1.

**Step 3 — characterization (green on today's code; prove by mutation):**
- **T1 `EndpointInventoryTests`** (no DB, II11 pattern): every `RouteEndpoint` in `EndpointDataSource` → `"{methods} {RawText.TrimStart('/')} | {policies} | {anon|auth} | {cors}"` where methods = `HttpMethodMetadata.HttpMethods` joined `,` (none → `*`); policies = ordered `IAuthorizeData.Policy ?? "(default)"` joined `+` (none → `-`); anon = `IAllowAnonymous` present; cors = `IEnableCorsAttribute.PolicyName ?? "-"`. Expected = a literal sorted list of every row (health, hub, fallback included). Generate the first draft from a failing run, then **check every row against the controller attributes by hand** and say so in the report (it is a pin of today's behaviour, not of whatever the code emits). Categories must read `GET api/v1/categories | - | anon | -`.
- **T2** `GET /api/v1/products?page=abc` → 400, `application/problem+json`, exact `type` and `title` as MVC emits today, `errors.page`, `traceId`, `correlationId`.
- **T3** `GET /api/v1/products/no-such-slug` → 404 problem+json, exact `type` and `title`, `traceId`, `correlationId`.
- **T4** Auth headers: `POST /api/v1/auth/login` with an invalid body (400) and `GET /api/v1/auth/me` with a token (200) → exact `Cache-Control` and `Pragma` values as emitted today.
T2–T4 use the existing `CatalogTestEnv` / `IdentityTestEnv`; if an existing test already asserts the exact value, cite it instead of duplicating.

**Step 4 — full `bash scripts/verify.sh 2>&1 | tail -n 30`**, quote the `VERIFY` line. Then mutations.

**Existing tests:** all unedited except the renamed/extended arch test. `CatalogDetailTests` (categories body), II11 and `A_module_implementation_exports_only_its_module_class` must pass as they are.

## 5. Mutations (break → red → restore from scratch copy → `cmp`)

1. `CategoryEndpoints.cs`: `.AllowAnonymous()` → `.RequireAuthorization(AuthorizationPolicies.Admin)` → T1 red (and II11).
2. Add a line `// app.MapGet("x", () => 1);` to any Catalog `Application/*.cs` → D10 arch test red.
3. `Program.cs` `UseStrictJson`: remove the `Disallow` line → T5 red and IA1 (`AdminProductApiTests`) red.

## 6. Review focus (Rob)

The sample's diff and this slice's together: no trailing-slash RawText; nothing public beyond `MapCatalogEndpoints`; inventory rows match attributes (spot-check every Admin vs AdminOrSupport row and the anonymous set); the arch test is not weaker than before (controller check intact, vacuity guard holds); `GetCategoriesAsync` makes the same single query as before; no dead code (`WithName`, unused usings).

## 7. Risks

- Inventory pinned to whatever the code emits rather than the attributes → hand check required (step 3).
- `ConfigureHttpJsonOptions` changing an existing minimal-endpoint output: none found today; if verify shows one, stop and report.
- Hub endpoints in the inventory may vary between runs (negotiate routes) — if they do, filter out routes under `OrderStatusHub.Path` and say so.
