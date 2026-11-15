# Slice P2.63 — AdminSearch to Minimal API (plan, Rob)

Tracker: P2.63 "Admin search endpoint to Minimal API" · Module: Admin · Tier: **High** (auth policy + role-based data exclusion). Autonomous.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 · this plan · `Admin/Api/AdminSearchController.cs` · `Admin/Api/AdminSettingsEndpoints.cs` + `AdminModule.cs` (pattern) · `Catalog/Api/ProductEndpoints.cs` lines 1–45 (handler shape) · `Admin/Application/AdminSearchTerm.cs` · `tests/Ecommerce.Api.IntegrationTests/Admin/AdminSearchApiTests.cs` · the `admin/search` line of `EndpointInventoryTests.cs`. Nothing else.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| S1 | Single route, no group: `app.MapGet("api/v1/admin/search", Get).RequireAuthorization(AuthorizationPolicies.AdminOrSupport)` | Master D4 (single-route may skip the group), same as `AdminSettingsEndpoints`; no trailing slash | `MapGroup("api/v1/admin/search").MapGet("")` (trailing slash, II11 red) |
| S2 | Response records (`AdminSearchResponse` + 3 item records) move unchanged into `AdminSearchEndpoints.cs` | Master §2.2: records unchanged; one file per former controller | New file for records |
| S3 | `q` is bound as `[FromQuery] string[]? q` and the handler passes `q?.FirstOrDefault()` to `AdminSearchTerm.TryParse` — **only if step-1 pin I11 shows MVC uses the first value** (expected: MVC binds `FirstValue`; Minimal API binds a repeated `string` key as `"a,bb"`). One-line comment says why. If I11 shows MVC behaves like `"a,bb"`, bind plain `string? q` instead and tell me in the report | Behaviour must not change for `?q=a&q=bb` (MVC 400, naive Minimal 200 and a search for `a,bb`) | Plain `string? q` without checking; parsing `HttpRequest.Query` by hand |
| S4 | Validation 400: `TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["q"] = [error] })` | Master D8; same body as MVC `ValidationProblem(ModelState)` via `IProblemDetailsService` (P2.59 pin) | Shared helper (no second consumer here) |
| S5 | Role flag from `ClaimsPrincipal user`: `user.IsInRole(IdentityRoles.Admin)` passed to `SearchAsync` exactly as today | Support must never trigger the Catalog calls (app context 2) | Second policy/route per role |
| S6 | `AdminSearchService`, `AdminSearchTerm`, `IOrderSearch`, `ICatalogSearch` and their implementations are **not touched** | Pure transport change; the query layer is already reviewed (P2.8) | Any refactor "while here" |

No migrations, no `*.Contracts` change, no packages, no doc change (decision log row "Minimal API migration" covers it).

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSearchEndpoints.cs` | **new** — `internal static class AdminSearchEndpoints`, `public static void Map(IEndpointRouteBuilder app)`, one `private static async Task<Results<Ok<AdminSearchResponse>, ValidationProblem>> Get(string[]? q, ClaimsPrincipal user, [FromServices] AdminSearchService search, CancellationToken ct)` + the 4 records (S2) |
| `.../Api/AdminSearchController.cs` | **delete** (same change, master §4 step 2) |
| `.../AdminModule.cs` | `MapAdminEndpoints` adds `AdminSearchEndpoints.Map(app);` |
| `tests/.../Admin/AdminSearchApiTests.cs` | **add** I11, I12 only (step 1); existing tests unedited |

Contract of `Get` (≈ 15 lines): TryParse → 400, else **one** `await search.SearchAsync(term, user.IsInRole(IdentityRoles.Admin), ct)` → map the view with three `Select(...).ToList()` into the response, exactly as the controller does.

## 3. Build order

1. **Baseline on MVC** (controller still in place): run `AdminSearchApiTests` + `EndpointInventoryTests` + `AuthorizationTests`; quote counts.
2. **Add pins, green on MVC, quote output:**
   - `I11_Repeated_q_uses_the_first_value`: `SearchAsync("a&q=bb")` → 400 with `errors.q`; `SearchAsync("bb&q=a")` → 200.
   - `I12_Error_messages_are_exact`: no `q` → `errors.q[0] == "q is required."`; `?q=` → same; `?q=a` → `"q must be between 2 and 100 characters."`; response `Content-Type` is `application/problem+json`.
   If I11 is not as expected on MVC, adjust the pin to what MVC does (it is the spec) and apply S3's fallback.
3. **Convert:** add `AdminSearchEndpoints.cs`, register it, delete the controller in the same change.
4. All search, inventory and II10/II11 tests green **unedited** (except the two new pins, unchanged since step 2). Full `bash scripts/verify.sh 2>&1 | tail -n 30`.
5. Mutations (§5), then the report.

## 4. Required tests

| Behaviour | Test | New? |
|---|---|---|
| 401 anonymous, 403 customer, 200 Admin and Support | I1, I2 | existing |
| Policy `AdminOrSupport`, CORS `credentialed`, route text | `EndpointInventoryTests` row `GET api/v1/admin/search \| AdminOrSupport \| auth \| credentialed`; II11 | existing |
| Missing / empty / blank / short / 101-char `q` → 400 with `errors.q`; 100 chars → 200 | I3 (both) | existing |
| Repeated `q` key binds the first value | I11 | **new, test-first (green on MVC)** |
| Exact error texts + problem content type | I12 | **new (green on MVC)** |
| Response shape (`orders/products/skus`, field names, no money/email) | I4, I8 | existing |
| Support gets no products/SKUs, still orders | I6 | existing |
| ≤ 5 per group, documented order; LIKE escaping; case-insensitive | I7, I9, I10 | existing |

## 5. Mutations (High) — break → red → restore from scratch copy → `cmp`

| # | Mutation | Expected red |
|---|---|---|
| M1 | Drop `.RequireAuthorization(AdminOrSupport)` | II11 (undeclared endpoint) + inventory row + I1/I2 |
| M2 | Policy `AdminOrSupport` → `Admin` | I2 (Support 403) + inventory row |
| M3 | Role flag `user.IsInRole(IdentityRoles.Admin)` → `true` | I6 (Support sees products/SKUs) |
| M4 | `q?.FirstOrDefault()` → `string.Join(",", q ?? [])` | I11 |

## 6. What the reviewer checks (Rob)

- **Queries per request:** handler calls `SearchAsync` once; no other service, no loop. Admin = 3 DB queries (orders, products, SKUs, run sequentially on one scoped DbContext); Support = 1 (Catalog never called). `git diff --stat` must show no change under `Application/`, `Orders/`, `Catalog/`.
- **Projection:** unchanged: `OrderSearch` / `CatalogSearch` select only `Id, OrderNumber, Status` / `Id, Name` / `Sku, ProductId, ProductName`, `AsNoTracking`; handler maps those fields only (I8).
- **Limits:** `MaxPerGroup = 5` passed to every query as `Take(max)`; `q` 2–100 chars after trim enforced **before** any query (I3, I12).
- **Cross-module calls:** only via `IOrderSearch` / `ICatalogSearch` contracts; no new csproj reference, no `DbSet` from another module.
- **Behaviour:** policy, binding (blank, repeated key), 400 messages and shape, 200 shape identical; controller deleted; no dead `using`; no `var`.

## 7. Risks

| Risk | Mitigation |
|---|---|
| Repeated `q` silently changes meaning under Minimal API | I11 pinned on MVC first, S3, M4 |
| `string[]?` binding of a missing key gives an empty array, not null | `FirstOrDefault()` returns null either way → "q is required." (I12) |
| Same latent repeated-key difference in P2.60 Products (`search`, `category`, `sort`, `page`) | Out of scope; Rob adds a NOTE to `backlog.md` |
| Both controller and endpoint mapped → `AmbiguousMatchException` | Delete in the same change (step 3) |
