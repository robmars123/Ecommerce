# Slice P2.68 — AdminCategories to Minimal API (Rob, plan)

Mode: Autonomous · Tier: **High** (admin writes, auth, strict JSON) · Status: READY

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 · this plan · `docs/architecture/03-principles-hard-rules.md:40` · `docs/architecture/06-backend-modules.md:63–69` · `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminCategoriesController.cs` · `Api/Admin/AdminValidators.cs` (from line 155) · `Api/CategoryEndpoints.cs` · `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminCustomersEndpoints.cs:43–50` (group shape) · tests: `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/Admin/AdminCategoryApiTests.cs`, `AdminSamples.cs:1–55`, `HttpContractTests.cs:27–55` (problem-shape assertions to copy), `EndpointInventoryTests.cs` (rows 18, 44, 71), `Identity/IdentityTestEnv.cs:198–218` (`SendAsync` takes a raw string body). Decision log row "Minimal API migration" (grep, do not read the file).

Nothing else. No domain, service, store, migration, Contracts or package changes.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| A1 | New `Api/Admin/AdminCategoriesEndpoints.cs`, `internal static class AdminCategoriesEndpoints`, namespace `Ecommerce.Catalog.Api.Admin`. One group `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin)`; `MapPost("categories", Create)`, `MapPut("categories/{slug}", Update)`, `MapDelete("categories/{slug}", Delete)` | Master plan D1/D4/D5; inventory rows 18/44/71 stay byte-equal; same folder as the records and validators it uses | `MapGroup("api/v1/admin/categories")` + `MapPost("")` (trailing slash, D4) |
| A2 | Handlers: `[FromBody] CreateCategoryRequest request` / `[FromBody] UpdateCategoryRequest request`, `string slug` from the route, `[FromServices] IValidator<…>`, `[FromServices] AdminCategoryService`, `CancellationToken`. Each handler: validate once → 400, else **one** service call, then map. Records unchanged | Same calls as the controller (1 validator + 1 service). Explicit `[FromBody]` so a reader never has to know inference rules | Inferred body; `Task<IResult>` (loses the typed result list) |
| A3 | One private mapper `ToResult<TOk>(AdminResult<CategoryView> result, Func<CategoryView, TOk> onOk) where TOk : IResult` returning `Results<TOk, ValidationProblem, ProblemHttpResult>` via a switch expression: `Ok` → `onOk`; `Invalid` → `TypedResults.ValidationProblem(invalid.Errors)`; `Conflict` → `TypedResults.Problem(new HttpValidationProblemDetails(new Dictionary<string, string[]> { [conflict.Field ?? ""] = [conflict.Message] }) { Status = 409, Title = "Conflict" })`; `_` → `TypedResults.Problem(statusCode: 404, title: "Category not found")` | Line-for-line twin of the controller's `ToActionResult` (D8); one place for the 4 outcomes, used by all 3 handlers | A mapper per handler (3 copies) |
| A4 | Create → `TypedResults.Created((string?)null, view.ToResponse())`: **201 with no `Location`**; Update → `TypedResults.Ok(view.ToResponse())`; Delete → `TypedResults.NoContent()` | MVC used `StatusCode(201, body)`, which sets no `Location`. Adding one now would be unplanned behaviour | `CreatedAtRoute` / a Location to a GET that does not exist |
| A5 | Add `public static Dictionary<string, string[]> ToErrors(this ValidationResult result)` to `AdminValidationProblems` (`AdminValidators.cs`): camelCase key via the existing private `CamelCase`, messages grouped per key in failure order. `ToProblem(ControllerBase …)` stays untouched (still used by `AdminProductsController`; P2.69 deletes it) | D8: module keeps its own helper; same keys as `ToProblem` (`slug`, `name`, `parentSlug`) | Copy the camel-case code into the endpoint file; a SharedKernel helper |
| A6 | JSON body that cannot be read (malformed, unknown member, wrong type, empty) → minimal API's bare 400, which `UseStatusCodePages` + `AddProblemDetails` turn into `application/problem+json` with the same `type`/`traceId`/`correlationId`; **title becomes "Bad Request" and `errors` disappears** (MVC: "One or more validation errors occurred." + `$.member`/`request` keys). Accepted and recorded (13.2 row "Minimal API migration", updated by Rob in this slice). Pins assert only what both stacks share | The defence (mass assignment → 400, nothing written) is unchanged; no first-party client sends such a body; matching MVC would mean reading every body by hand | `ReadFromJsonAsync` + `catch (JsonException)` in each handler; `ThrowOnBadRequest` + a global exception mapper (host-wide change) |
| A7 | Omitted non-nullable member (`name` or `slug` missing): minimal binds `null`, the validator already rejects null (`Slug.IsValid(string?)`, `Must(n => !IsNullOrWhiteSpace(n) …)`) → 400 ValidationProblem. MVC key was `Name` (implicit `[Required]`), now `name`. No new validator rule (D9 only if the pin goes 500/200) | Same status, title and type; camelCase key matches every other validator key | Adding `NotNull()` rules that change nothing |
| A8 | Register in `CatalogModule.MapCatalogEndpoints`: `AdminCategoriesEndpoints.Map(app);` after `ImageEndpoints.Map(app);`. Delete `AdminCategoriesController.cs` in the same change. No `ModuleComposition` / `Program.cs` edit (Catalog already mapped, CORS group already wraps it) | D2, §4 step 2 (both mapped → `AmbiguousMatchException`) | — |

### What the reviewer checks (per request, must equal MVC)

| Route | DB round trips | SaveChanges | Outbox |
|---|---|---|---|
| POST | 0–1 parent `SELECT` (only when `parentSlug` sent) + 1 `INSERT` | 1 | none (category writes publish no events) |
| PUT | 1 tracked `SELECT` + 1 `UPDATE` + 0–1 parent-slug `SELECT` (child only; existing, backlog NOTE P2.68) | 1 | none |
| DELETE | 1 tracked `SELECT` + 1 `DELETE` (FK refusal → 409, no pre-check, 13.2 "Category edit and delete") | 1 | none |

Handler adds no query, no loop, no extra service call, no read before validation. No uniqueness pre-check (the unique index → 409 stays the only check). No concurrency `version`/412 exists: a row deleted between read and save → `ConcurrencyConflictException` → 404 in the service; the handler just maps `NotFound`. Validation 400 happens before any DB access (same as MVC).

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminCategoriesEndpoints.cs` | **new** (A1–A4) |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminCategoriesController.cs` | **delete** |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/Admin/AdminValidators.cs` | add `ToErrors` (A5) |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/CatalogModule.cs` | one `Map` line (A8) |
| `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/Admin/AdminCategoryContractTests.cs` | **new**, step-1 pins (§4) |
| `docs/admin-dashboard-feature-tracker.md` row P2.68 | `Todo` → `Done` after PASS |
| `docs/architecture/13.2-decision-log.md`, `slice-plans/backlog.md` | done by Rob with this plan |

`AdminCategoryApiTests.cs`, `AdminSamples.cs`, `EndpointInventoryTests.cs`, `HttpContractTests.cs`: **unedited**.

## 3. Build order

1. **Capture MVC (before any code).** In a scratch copy of the new test file, send to MVC: unknown member (`POST {"slug":"a","name":"A","parentSlug":null,"isActive":true}`), malformed (`POST {"slug":`), empty body (`POST ""`), omitted `name` (`POST {"slug":"a"}`), Conflict (DELETE parent with child), 404 (PUT `nope`). Print each `r.Text` (temporary `Assert.Fail(r.Text)` is fine) and quote all six bodies in the report. Delete the scratch print.
2. **Write the pins (§4), run on MVC, all green, quote the count.** Any pin red on MVC → fix the pin to MVC's truth (it is new), not the code.
3. **Baseline:** `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~AdminCategory|FullyQualifiedName~EndpointInventory|FullyQualifiedName~AdminProductApiTests|FullyQualifiedName~HttpContract"` → quote counts.
4. Add `ToErrors` (A5), the endpoints file, the `Map` line; delete the controller — one change.
5. Re-run step 3's filter + the arch tests (`Ecommerce.ArchitectureTests`) → all green, same counts + pins.
6. Mutations (§5), then full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line.

Stop and report (no improvised fix) if: a pin that was green on MVC goes red after the move; the unreadable-JSON 400 is not `application/problem+json`; the inventory rows change.

## 4. Required tests (new file `AdminCategoryContractTests.cs`, `IdentityTestEnv` + `AdminSamples.AdminTokenAsync`; every one green on MVC first)

Numbering continues the IC series (IC10 is IA7).

- **IC11 create is 201, no Location, exact body.** Create `tops`, then `tees` with `parentSlug = "tops"` → 201, `Response.Headers.Location` is null, content type `application/json`, body `slug`/`name`/`parentSlug` (`null` for `tops`, `"tops"` for `tees`).
- **IC12 unreadable JSON is a 400 problem and writes nothing** (the strict-JSON / mass-assignment pin, test-first for M1). For POST unknown member `isActive`, PUT `shirts` with unknown member `parentSlug` (IC4's case), malformed `{"slug":`, and empty `""`: each 400, `application/problem+json`, `type` = `…rfc9110#section-15.5.1`, non-empty `traceId`, `correlationId` = `id-test-1`. Afterwards `GET /api/v1/categories` contains `shirts` and none of the posted slugs. Do **not** assert `title` or `errors` (A6).
- **IC13 validation 400 shape.** POST `{"slug":"Bad Slug","name":""}` → 400, `title` "One or more validation errors occurred.", `type` 15.5.1, `errors.slug` and `errors.name` present, `correlationId`. POST `{"slug":"a"}` (omitted `name`) → 400, same title, `errors` has a key equal to `name` ignoring case (A7; MVC `Name`, minimal `name`). POST with `parentSlug = "missing"` (service `Invalid`) → 400, same title, `errors.parentSlug` with message "That parent category does not exist.".
- **IC14 404 shape.** PUT and DELETE `nope` → 404, `type` `…section-15.5.5`, `title` "Category not found", `traceId`, `correlationId`.
- **IC15 409 shape.** DELETE a parent that has a child → 409, `application/problem+json`, `type` `…section-15.5.10`, `title` "Conflict", `errors.subcategories[0]` = "This category has subcategories. Delete them first.", `traceId`, `correlationId`.

Already covered, unedited: 401/403 for Customer and Support on PUT/DELETE (IC3); POST create paths and POST 409/400 (IA7 in `AdminProductApiTests:263–268`); validation keys (IC4); 409 on slug (IC6); 204 then 404 (IC7); FK 409s (IC8, IC9); inventory policy/CORS rows.

Gap noted, not added: no test sends POST as Customer/Support. IC3 covers the group policy for PUT/DELETE, and the inventory row pins `POST … | Admin`; M2 proves both.

## 5. Mutations (High tier; break → red → restore from scratch copy → `cmp`)

| # | Guard | Mutation | Must go red |
|---|---|---|---|
| M1 | Strict JSON on minimal endpoints | `Program.cs`: remove the `ConfigureHttpJsonOptions(...)` line only (MVC half stays) | IC4 (`parentSlug` member), IC12, T5 |
| M2 | Admin-only policy | group `.RequireAuthorization(AuthorizationPolicies.AdminOrSupport)` | IC3 Support rows, inventory rows 18/44/71 |
| M3 | Validation before the service | `Update` skips the validator call | IC4 (`slug`/`name` keys) |

Quote each red (test names + `Failed: n`) and the `cmp` result.

## 6. Risks

- **Problem shape drift on unreadable JSON** (A6): pinned to the shared fields only; the dropped `title`/`errors` is a recorded decision, not a bug. If the body is not problem+json, stop.
- **Results union compile** (A3): if the generic `Results<TOk, …>` target-typed switch does not compile, write the same switch in each handler (three copies, no other change) and say so in the report.
- **Key case on omitted members** (A7): only the omitted-member case changes `Name` → `name`; the frontend reads camelCase keys, so this is not a regression.
- **AdminProductsController still MVC** and still uses `ToProblem`: do not touch it or the shared records.
