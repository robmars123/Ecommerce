# Slice P2.70 — AdminInventory to Minimal API (plan, Rob)

Tier **High** (stock adjust, Admin-only policy, audit actor). Mode Autonomous. Row: tracker `P2.70 | Admin inventory endpoints (list, adjust) to Minimal API | Inventory`.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 · this plan · `docs/architecture/03-principles-hard-rules.md:40` (rule 7) · `docs/architecture/06-backend-modules.md:63-69` · `backend/src/Modules/Inventory/Ecommerce.Inventory/Api/{AdminInventoryController,AdminInventoryRequests,AdminInventoryResponses}.cs` · `.../Ecommerce.Inventory/InventoryModule.cs` · `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs:30-31` · pattern: `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminCategoriesEndpoints.cs`, `.../Catalog/Api/QueryValues.cs`, `.../Catalog/Api/Admin/AdminValidators.cs:154-164` (`ToErrors`) · tests: `backend/tests/Ecommerce.Api.IntegrationTests/Inventory/{AdminInventoryListTests,AdminInventoryAdjustTests}.cs`, `.../Catalog/Admin/AdminCategoryContractTests.cs:1-40` (`AssertProblem`), `.../EndpointInventoryTests.cs:26,45`.

Do **not** read or touch `InventoryAdminService.cs`, `InventoryStore.cs`, the domain or persistence: the service is unchanged in this slice.

## 1. Scope facts (checked by Rob)

- Two routes: `GET api/v1/admin/inventory` (`page`, `pageSize`) and `POST api/v1/admin/inventory/adjust` (body `AdjustStockRequest`). Both `Admin` policy (Support is 403). Inventory rows `:26`, `:45` expect `Admin | auth | credentialed`.
- **There is no low-stock filter** on these routes (low stock lives on the Admin dashboard, converted in P2.65). Query binding is `page`/`pageSize` only.
- Hard rule §3.4 is held by the service, not the handler: `AdjustAsync` reads the variant (Catalog, outside the tx), then one `InTransactionAsync` = `EnsureRowAsync` (INSERT … ON CONFLICT DO NOTHING) → `LockAsync` (`ORDER BY product_variant_id FOR UPDATE`, one row) → domain `Adjust` → audit row → one `SaveChangesAsync` → commit. No external call, no outbox (audit only, same tx). The handler must not add any of these.
- Queries per request today: list = 3 (`CountAsync`, page `ToListAsync`, one `ANY(@ids)` stock read — no N+1); adjust = 1 Catalog read + ensure + lock + save. The conversion keeps these counts exactly.

## 2. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| P1 | New `Api/AdminInventoryEndpoints.cs`, `internal static class`, one group `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin)`, `MapGet("inventory", List)`, `MapPost("inventory/adjust", Adjust)`. Delete `AdminInventoryController.cs` in the same change | Master plan D1, D4, D5; inventory rows unchanged | `MapGroup("api/v1/admin/inventory").MapGet("")` (trailing slash, D4) |
| P2 | `InventoryModule.MapInventoryEndpoints(this IEndpointRouteBuilder app)` calls `AdminInventoryEndpoints.Map(app)`, returns `app`. `ModuleComposition.MapModuleEndpoints` becomes `app.MapCatalogEndpoints().MapAdminEndpoints().MapInventoryEndpoints()` | D2; first Inventory slice. Inside the host CORS group, so `credentialed` holds (D7) | Reflection |
| P3 | **List** (corrected in review): bind `string[]? page, pageSize`; `QueryValues.ReadInt` both, return `ValidationProblem` if any parse error; then the two range checks with the controller messages and a second return; then one `ListAsync(page ?? 1, pageSize ?? 20, ct)` | D9 + backlog P2.63. MVC capture: `[ApiController]` returns binding errors before the action runs, so `page=abc&pageSize=0` reports `page` only; two stages match it | One combined return (the original P3; reports a key MVC never did) |
| P4 | Copy Catalog's `QueryValues.cs` to `Inventory/Api/QueryValues.cs` (namespace `Ecommerce.Inventory.Api`), unchanged | Per-module copy convention; modules can't share an `Api` type | Referencing Catalog (boundary breach) |
| P5 | **Adjust**: `[FromBody] AdjustStockRequest request, ClaimsPrincipal user, [FromServices] IValidator<AdjustStockRequest> validator, [FromServices] InventoryAdminService inventory, [FromServices] ILoggerFactory loggers, CancellationToken ct` → `Results<Ok<InventoryItemResponse>, ValidationProblem, ProblemHttpResult>`. Validate → `ValidationProblem(ToErrors(validation))`; `Guid actorId = Guid.Parse(user.FindFirst("sub")!.Value)` (the controller's line); **one** `AdjustAsync` call with the controller's arguments (incl. `Reason!.Trim()`); the same log line via `loggers.CreateLogger(typeof(AdminInventoryEndpoints))`; then a `switch` expression on `result.Outcome` | Thin: validate, one service call, map. A static class can't be `ILogger<T>`'s argument; the log category changes from `…AdminInventoryController` to `…AdminInventoryEndpoints` (no test reads it) | `ILogger<InventoryAdminService>` (misleading category); dropping the log line (loses the "answerable from logs" line) |
| P6 | Outcome map: `Ok` → `TypedResults.Ok(result.Item!.ToResponse())`; `NotFound` → `TypedResults.Problem(statusCode: 404, title: "Variant not found")`; `Stale` / `Insufficient` → one private `Conflict(AdjustResult result, string detail)` returning `TypedResults.Problem(statusCode: 409, title: "Conflict", detail: detail, extensions: { onHand, reserved, available })` with the controller's two detail strings | Same body as MVC: plain `ProblemDetails`, **no `errors`**. The Catalog `ToResult` 409 (`HttpValidationProblemDetails`) is the wrong template here — pinned by IV1/IV2 | Copying Catalog's conflict mapper. Arms keep the controller's shape: `Ok`, `NotFound`, `Stale` explicit, `_` = insufficient |
| P7 | `ToErrors(ValidationResult)` as a `private static` method in the endpoints file: group by the controller's key expression (`PropertyName` first char lower-cased), `ToDictionary(..., ToArray())` | One consumer; property names are flat (no paths). Same keys MVC's `ModelState` produced | Copying Catalog's path-aware helper (no consumer for paths) |
| P8 | Body binding failures (malformed, empty, `null`, wrong type, unknown member) → minimal API bare 400 problem: same `type`/`traceId`/`correlationId`, **title/errors change** | Accepted in P2.68 A6 (13.2 row "Minimal API migration"); pins assert only what both stacks share | Hand-reading the body |

No migrations, no `*.Contracts` change, no packages, no doc edits. Validator registration in `InventoryModule` stays.

## 3. Files

| File | Change |
|---|---|
| `backend/src/Modules/Inventory/Ecommerce.Inventory/Api/AdminInventoryEndpoints.cs` | **new** (P1, P3, P5–P7) |
| `.../Ecommerce.Inventory/Api/QueryValues.cs` | **new**, copy (P4) |
| `.../Ecommerce.Inventory/Api/AdminInventoryController.cs` | **deleted** |
| `.../Ecommerce.Inventory/InventoryModule.cs` | add `MapInventoryEndpoints` (P2) |
| `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` | chain `.MapInventoryEndpoints()` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Inventory/AdminInventoryContractTests.cs` | **new**, step-1 pins (§5) |

`AdminInventoryRequests.cs`, `AdminInventoryResponses.cs`, all existing tests: **unedited**.

## 4. Build order

1. **Capture MVC (before any code).** Baseline: `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~Inventory" | tail -n 20` (matches the Inventory folder and `EndpointInventoryTests`); quote counts. In a scratch copy of the new test file, print `r.Text` on MVC for: Stale 409, Insufficient 409, unknown variant 404, `{}` body, all-invalid body (IV4), `page=abc&pageSize=0`, `page=&pageSize=`. Quote each body in the report; delete the prints.
2. **Write IV1–IV7, green on MVC**, quoted. Any value the capture shows differently from §5 → pin MVC's value and say so; never change it to suit the conversion.
3. **Convert** (§3 files) in one change. `dotnet build` clean (no `var`, named records).
4. Re-run the step-1 filter: all existing tests + IV1–IV7 + inventory **unedited and green**. If any pin goes red, stop and report; no improvised fix.
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line.
6. Mutations (§6): break → red (quote) → restore from scratch copy → `cmp`.

## 5. Required tests — `AdminInventoryContractTests.cs` (new, step 1, green on MVC)

Use a local `AssertProblem(r, status, type)` like `AdminCategoryContractTests` (problem+json, `type`, non-empty `traceId`, `correlationId` = `id-test-1`). Each test starts its own env with `InventoryEnv.StartAsync` and an Admin token.

| # | Request | Expected |
|---|---|---|
| IV1 | Stale: stock 5, adjust `delta 1, expectedOnHand 4` | 409 problem, type `…rfc9110#section-15.5.10`, title `Conflict`, detail `The on-hand count changed. Reload and try again.`, `onHand 5`, `reserved 0`, `available 5`, **no `errors` property**; stock still (5, 0) |
| IV2 | Insufficient: stock 5, reserve 3, adjust `-3, 5` | 409, same type/title, detail `The change would leave less available than is reserved.`, `onHand 5`/`reserved 3`/`available 2`, no `errors` (T10 already pins the audit side) |
| IV3 | Unknown variant (`Guid.NewGuid()`) | 404 problem, type `…section-15.5.5`, title `Variant not found`; `inventory_stock` count 0 |
| IV4 | Body `{"variantId":"00000000-…","delta":0,"expectedOnHand":-1,"reason":"  "}`; and body `{}` | 400 problem, type `…section-15.5.1`, title `One or more validation errors occurred.`, `errors` keys exactly `{variantId, delta, expectedOnHand, reason}` with the validator's messages (quote from capture); nothing written. `{}` is the D9 "omit member" pin |
| IV5 | Unreadable bodies: malformed `{"delta":`, empty `""`, literal `null`, wrong type `"delta":"abc"`, unknown member (existing line 198 shape) | each 400, `AssertProblem` only (no `title`/`errors`, P8); `inventory_stock` count 0 |
| IV6 | `GET ?page=abc`; `?page=abc&pageSize=0`; `?page=0&pageSize=0` | 400, title as IV4; `errors.page` = `The value 'abc' is not valid.`; second request has only the `page` key (MVC capture); third has both range keys |
| IV7 | `GET ?page=2&page=x&pageSize=2` (3 variants); `GET ?page=&pageSize=` | first: 200, `page 2`, one item (first value wins); second: 200, `page 1`, `pageSize 20` (or MVC's captured result) |

Existing coverage that must stay green unedited: Admin/Support/Customer/anonymous per route (`Both_endpoints_are_401…`, `…403_for_support…`), T10, stale double-submit, 404, range 400s, 501-char reason, concurrent adjust vs reserve, inventory rows `:26`, `:45`.

## 6. Mutations (one per guard; quote red, restore, `cmp`)

| # | Break (in `AdminInventoryEndpoints.cs`) | Red |
|---|---|---|
| M1 **stock** | `Insufficient` arm returns `TypedResults.Ok<InventoryItemResponse>(null!)` (200) | T10 + IV2 |
| M2 **stock** | `Stale` arm returns 200 the same way | `A_stale_expectedOnHand_is_409…` + IV1 |
| M3 | skip the validation return (call the service with an invalid body) | `An_invalid_adjust_is_400_and_writes_nothing` + IV4 |
| M4 | group policy `Admin` → `AdminOrSupport` | `Both_endpoints_are_403_for_support…` + inventory `:26`/`:45` |
| M5 | pass `null` as `actorId` | T10 (`row.ActorId`) |

## 7. Reviewer checklist (Rob, review round)

- Handler thin: one service call per request, no `DbContext`, no `InTransactionAsync`, no `SaveChanges`, no Catalog call, no loop of service calls. `git diff --stat` shows **no** change under `Application/`, `Infrastructure/`, `Domain/`.
- Query count unchanged (list 3, adjust 1 read + 1 tx); lock order and single `SaveChanges` remain in the untouched service.
- Over-fetch: handler maps the service's records only; no extra projection.
- 409 bodies are `ProblemDetails` with the three extensions and no `errors`.
- Controller deleted; no dead helper; `QueryValues` is a verbatim copy.
- Existing tests unedited (`git diff --stat backend/tests` shows only the new file).

## 8. Risks

- **Copying Catalog's 409 template** adds `errors` and drops `onHand/reserved/available`: the admin UI reads `available` (backlog 10.4 R1). IV1/IV2.
- **List validation order** must match MVC: parse errors alone, then range errors together. IV6.
- **`ILogger<T>` with a static class** won't compile; P5 says `ILoggerFactory`.
- **Existing, not fixed here** (backlog NOTE added): `page` large enough that `(page - 1) * pageSize` overflows `int` in `ProductReader.ListVariantsAsync` → negative OFFSET → 500, on MVC too.
