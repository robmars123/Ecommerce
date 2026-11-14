# Slice P2.60 — Products endpoints (list, by slug) to Minimal API · Catalog

Mode: Autonomous · Tier: **Low** (anonymous read-only GETs; no money, stock, auth change) · Plan: Rob, 2026-11-14 · Status: READY

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 · this plan · arch `03-principles-hard-rules.md:40` (rule 7) · `06-backend-modules.md:63–69` · `Catalog/Api/ProductsController.cs` · `Catalog/Api/CategoryEndpoints.cs` (the pattern) · `CatalogModule.cs:64–68` · `Application/ProductSearchCriteria.cs` · tests: `Catalog/CatalogValidationTests.cs`, `HttpContractTests.cs` (T2, T3), `EndpointInventoryTests.cs:41–42`.
Do not read `CatalogBrowseService` / `CatalogReadStore`: this slice must not touch them.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| P1 | New `Api/ProductEndpoints.cs`, `internal static class ProductEndpoints`, `Map(app)`: one group `app.MapGroup("api/v1").AllowAnonymous()` → `MapGet("products", List)`, `MapGet("products/{slug}", Get)`. Delete `ProductsController.cs` in the same change. `CatalogModule.MapCatalogEndpoints` adds `ProductEndpoints.Map(app);` after `CategoryEndpoints.Map(app);` | Master D1/D4/D5. RawText stays `api/v1/products` and `api/v1/products/{slug}`, with no trailing slash | `MapGroup("api/v1/products").MapGet("")`: trailing slash, inventory goes red |
| P2 | `List` binds `[FromQuery] string? search, category, sort, page, pageSize` and `[FromServices] CatalogBrowseService catalog`. `page`/`pageSize` stay **strings** and are parsed in the handler (master D9) | Minimal API turns `int?` garbage into a bare 400 with no `errors`. T2 and `CatalogValidationTests` (`page=abc`, `pageSize=abc`) need `errors.<field>` | `int?` binding (breaks T2); a custom binder/filter (no second consumer yet) |
| P3 | One private helper `static int? ReadInt(string? raw, string field, Dictionary<string, string[]> errors)`: `string.IsNullOrEmpty(raw)` → `null` (MVC treats `page=` as absent). `int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, …)` → value. Otherwise add `errors[field]` with the **exact MVC message captured in step 1**, and return `null` | Same accepted inputs (sign, surrounding spaces) and the same invariant culture as MVC's query provider | Moving parsing into `ProductSearchCriteria.Create` (changes Application for a transport concern) |
| P4 | **Short-circuit like MVC.** If either `ReadInt` added an error, return `TypedResults.ValidationProblem(errors)` **without** calling `ProductSearchCriteria.Create`. Otherwise call `Create`; if it is invalid, return `TypedResults.ValidationProblem(parsed.Errors!)`. Otherwise make one `catalog.SearchAsync`, then `TypedResults.Ok(result.ToResponse())` | `[ApiController]` returned the binding 400 before the action ran, so `page=abc&pageSize=101` names only `page` | Merging both error sets (a behaviour change) |
| P5 | `Get(string slug, [FromServices] CatalogBrowseService catalog, CancellationToken ct)`: one `GetBySlugAsync`. `null` → `TypedResults.Problem(statusCode: 404, title: "Product not found")`. Otherwise → `TypedResults.Ok(product.ToResponse())`. Return types: `Results<Ok<ProductListResponse>, ValidationProblem>` and `Results<Ok<ProductDetailResponse>, ProblemHttpResult>` | Master D3/D8; T3 pins type, title and ids | — |
| P6 | **Accepted divergences** (no client sends these; the result is still a validated 200/400 and never a 500). (a) A duplicate key `?search=a&search=b`: MVC used the first value, minimal binds `"a,b"`; for `page` that becomes a 400. (b) Hex `page=0x2`: MVC's `Int32Converter` accepted it, now it is a 400 | Preserving them needs raw `HttpRequest.Query` reads or a converter: clever code for inputs nobody sends | Reading `request.Query[...]` by hand |
| P7 | No caching headers: the controller set none, and the endpoints set none | Behaviour-preserving; pinned in step 1 | — |

No changes to: Application/Infrastructure, `*.Contracts`, response records, `Program.cs`, `ModuleComposition.cs` (Catalog already wired in P2.59), docs, packages, or migrations. CORS stays the default policy (inventory column `-`).

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/ProductEndpoints.cs` | new (≈ 60 lines) |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/ProductsController.cs` | deleted |
| `backend/src/Modules/Catalog/Ecommerce.Catalog/CatalogModule.cs` | +1 line |
| `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/ProductQueryBindingTests.cs` | new: step-1 pins (no edits to existing test files) |

## 3. Build order

1. **Baseline on MVC (test-first pins):** run `CatalogValidationTests`, `HttpContractTests`, `EndpointInventoryTests`, and every `Catalog/*` integration test that hits `api/v1/products`. Quote the counts. Add `ProductQueryBindingTests` (below) and get it **green on MVC**. Quote the `page=abc` message MVC returns, then hard-code it in P3.
2. Convert: add `ProductEndpoints.cs`, register it, delete the controller.
3. Re-run step 1's set, unedited and green. Then run the full `bash scripts/verify.sh 2>&1 | tail -n 30` and write `slice-P2.60-marco-report.md`.

## 4. Required tests

Existing, unedited, green: `CatalogValidationTests` (all 10 theory rows + 3 facts), HttpContract T2/T3, inventory rows `GET api/v1/products | - | anon | -` and `…/{slug}`, `CatalogSearchTests`, `CatalogPaginationTests`, `CatalogDetailTests`, `CatalogVisibilityTests`, `CatalogStockAndImageTests`, `CatalogCorsTests`, `AuthorizationTests` II10/II11.

New pins in `ProductQueryBindingTests` (green on MVC before the conversion):
- **B1** `page=&pageSize=` → 200, and the page equals the default (`page` 1, `pageSize` 20).
- **B2** `page=abc` → `errors.page[0]` equals the exact MVC message.
- **B3** `page=abc&pageSize=101` → 400, `errors` has `page` and **not** `pageSize` (P4 short-circuit).
- **B4** `page=%202%20` → 200 with page 2 (whitespace accepted, same as `Int32Converter`).
- **B5** list and detail 200 responses carry no `Cache-Control` header (P7).

There are no mutations (Low tier). An invalid slug and an unknown slug → 404 are already covered by T3 and `CatalogDetailTests`.

## 5. What the reviewer checks (Rob reads the diff)

- **N+1 / round-trips:** `List` makes exactly **one** `catalog.SearchAsync` call, and only after validation passes. `Get` makes exactly **one** `catalog.GetBySlugAsync`. There are no loops and no extra service or DbContext calls in handlers. `git diff --stat` shows **no** file under `Application/` or `Infrastructure/`. The service keeps its baseline: list = paged projection + count + one batched stock-status call; detail = one projection + one related search + one batched stock-status call.
- **Over-fetching:** handlers inject only `CatalogBrowseService`, with no `DbContext` or `HttpContext`. Responses still go through the existing `ToResponse()` mappers, with no new fields.
- **Bugs:** an empty value is treated as absent (B1). The parse uses invariant culture. There is no `int.Parse` (it throws, so a 500). `Create` is never called after a parse error (B3). `TypedResults.ValidationProblem`/`Problem` are used, never `Results.Json` (the shape is pinned by T2/T3). There is no `var`.
- **Wiring:** `AllowAnonymous` on the group, no `RequireAuthorization`, and the group is built from `app`. The controller is deleted, so nothing throws `AmbiguousMatchException`.
- **Plain:** no dead `using`, one helper, no comments restating the code.

## 6. Risks

- The MVC message text in B2 is unknown until step 1. Capture it; don't guess.
- If `ConfigureHttpJsonOptions` differs from the MVC options in anything beyond D6, list or detail JSON could drift. `CatalogDetailTests` and `CatalogSearchTests` would catch it. If they go red, stop and report.
- P6 divergences are accepted on purpose. If a test pins either one, stop and report. Don't edit the test.
