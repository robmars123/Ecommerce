# Slice P2.64 — AdminCustomers to Minimal API (plan, Rob)

Tier **High** (auth, Support revenue hiding). Behaviour-only: same routes, policies, binding, 400/404 bodies, query count.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2.1 (D3, D4, D5, D8, D9) and §4 (standard steps) · this plan · `Admin/Api/AdminCustomersController.cs` · `Admin/Api/AdminSearchEndpoints.cs` + `AdminModule.cs` lines 29–34 (pattern) · `Catalog/Api/ProductEndpoints.cs` lines 20–72 (`ReadInt`, early return on parse errors) · `tests/Ecommerce.Api.IntegrationTests/Admin/AdminCustomersApiTests.cs` · lines 20–21 of `EndpointInventoryTests.cs`. Nothing else (no `Application/`, no other module).

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| C1 | One group from `app`: `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdminOrSupport)`, then `MapGet("customers", List)` and `MapGet("customers/{id:guid}", Get)` | Master D4/D5: two routes → group on the parent, leaf paths spell the resource; RawText matches inventory lines 20–21; no trailing slash | `MapGroup("api/v1/admin/customers").MapGet("")` (trailing slash, II11 red); two ungrouped `MapGet`s each with its own policy (two places to drift) |
| C2 | `search`, `page`, `pageSize` bound as `[FromQuery] string[]?`; handler uses `?.FirstOrDefault()`. One comment line says why (as in `AdminSearchEndpoints`) | MVC binds the **first** value of a repeated key; Minimal API joins a repeated `string` key as `"a,b"` (`?page=2&page=abc` would become a 400, `?search=ann&search=bob` would match nothing). Pinned by I11 | Plain `string?` (behaviour change); reading `HttpRequest.Query` by hand |
| C3 | Private `ReadInt(string? raw, string field, Dictionary<string, string[]> errors)` copied from `ProductEndpoints` into `AdminCustomersEndpoints` (blank/whitespace → null; `int.TryParse(…, NumberStyles.Integer, CultureInfo.InvariantCulture)`; else `errors[field] = ["The value '{raw}' is not valid."]`) | Master D9; Admin may not reference Catalog; MVC-exact message (P2.60). Only one consumer in Admin today, so no shared helper | A shared helper in SharedKernel (framework-free rule) or in Admin (no second consumer) |
| C4 | **Two-stage validation, as MVC did:** (1) parse `page`/`pageSize`; if any error → `TypedResults.ValidationProblem(errors)` and stop. (2) then the range and `search` length checks, collected into one dictionary → one `ValidationProblem` | `[ApiController]` returns binding errors **before** the action runs, so `?page=abc&pageSize=500` gives only `errors.page`; range errors are accumulated together (`ModelState`). Pinned by I9 and I12 | One combined dictionary (adds `pageSize` to the I9 body) |
| C5 | Error texts and keys unchanged: `page` "page must be 1 or greater.", `pageSize` "pageSize must be between 1 and 100.", `search` "search must be at most 100 characters."; `MaxPageSize = 100`, `MaxSearchLength = 100`, default page 1, size 20, `search` trimmed, blank → null — all moved as is | Pure transport change | Re-wording or moving rules into the service |
| C6 | 404 → `TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)` | Master D8; `ProblemDetailsDefaults` fill type `…rfc9110#section-15.5.5` and title "Not Found", the same body MVC's `[ApiController]` `NotFound()` produced. Pinned by I13 on MVC first | `TypedResults.NotFound()` (relies on `UseStatusCodePages` to write the body — implicit) |
| C7 | Role flag `user.IsInRole(IdentityRoles.Admin)` from `ClaimsPrincipal user`, passed to `GetAsync` exactly as today | P2.45 / decision-log row "Admin customers API": Support gets `totalSpent: null`; the guard is the role, not the client | Second policy per role |
| C8 | The 6 response records move unchanged into `AdminCustomersEndpoints.cs`. `MoneyResponse` stays in `AdminDashboardController.cs` (moves with P2.65) | Master §2.2 | New records file |
| C9 | `CustomerQueryService`, `IUserReader`, `IOrderReader`, `IRefundState` and their implementations are **not touched**. Over-fetching found there goes to `backlog.md` (§7), not this slice | Behaviour-only migration | Fixing the queries "while here" |

Handler signatures:
- `List([FromQuery] string[]? search, [FromQuery] string[]? page, [FromQuery] string[]? pageSize, [FromServices] CustomerQueryService customers, CancellationToken ct)` → `Task<Results<Ok<AdminCustomerListResponse>, ValidationProblem>>`; exactly **one** `await customers.ListAsync(term, currentPage, size, ct)`; mapping and `TotalPages` formula as today.
- `Get(Guid id, ClaimsPrincipal user, [FromServices] CustomerQueryService customers, CancellationToken ct)` → `Task<Results<Ok<AdminCustomerDetailResponse>, ProblemHttpResult>>`; exactly **one** `await customers.GetAsync(id, user.IsInRole(IdentityRoles.Admin), ct)`; mapping as today.

No migrations, no contracts, no packages, no docs (decision-log row "Minimal API migration" covers it).

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminCustomersEndpoints.cs` | **new** — `internal static class AdminCustomersEndpoints`, `Map`, `List`, `Get`, `ReadInt`, the 6 records (C8) |
| `.../Api/AdminCustomersController.cs` | **delete** (same change) |
| `.../AdminModule.cs` | `MapAdminEndpoints` adds `AdminCustomersEndpoints.Map(app);` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminCustomersApiTests.cs` | **add** I9–I13 only (step 2); I1–I8 unedited |

## 3. Build order

1. **Baseline on MVC:** run `AdminCustomersApiTests`, `EndpointInventoryTests`, `AuthorizationTests` (filter by class); quote counts.
2. **Pins first, green on MVC, quote output** (MVC is the spec: if a pin disagrees with this plan, change the pin to what MVC does, follow MVC in the handler, and say so in the report):
   - **I9** `Bad_paging_values_are_400_with_the_binding_message_only`: `?page=abc` → 400, `errors.page[0] == "The value 'abc' is not valid."`; `?page=abc&pageSize=500` → `errors` has `page` and **no** `pageSize`; `?pageSize=1.5` → `errors.pageSize`.
   - **I10** `Blank_paging_and_search_use_the_defaults`: `?page=&pageSize=&search=` → 200, `page == 1`, `pageSize == 20`.
   - **I11** `Repeated_keys_use_the_first_value`: 2 customers; `?pageSize=1&page=2&page=abc` → 200, `page == 2`, one item; `?search=ann&search=bob` (customers `ann@…`, `bob@…`) → only `ann@…`.
   - **I12** `Range_errors_are_collected_with_exact_texts_and_shape`: `?page=0&pageSize=101&search=` + 101 × `a` → 400, `Content-Type` media type `application/problem+json`, `type` `…#section-15.5.1`, `title` "One or more validation errors occurred.", the three exact texts of C5, `traceId` non-empty.
   - **I13** `Unknown_detail_is_a_404_problem`: `Guid.NewGuid()` → 404, `application/problem+json`, `type` `…#section-15.5.5`, `title` "Not Found", `status` 404, `traceId` non-empty; `/not-a-guid` → 404 `application/problem+json`.
3. **Convert:** add `AdminCustomersEndpoints.cs`, register it, delete the controller in the same change.
4. I1–I13, inventory, II10/II11 green **unedited** since step 2. Full `bash scripts/verify.sh 2>&1 | tail -n 30`.
5. Mutations (§5), then the report.

## 4. Required tests

| Behaviour | Test | New? |
|---|---|---|
| 401 anonymous, 403 customer, 200 Admin and Support, both routes | I1 | existing |
| Policy `AdminOrSupport`, CORS `credentialed`, route text incl. `{id:guid}` | inventory lines 20–21; II11 | existing |
| Support `totalSpent: null`, rest identical; Admin sees the amount | I2, I3 | existing |
| Customers only, newest first, tie-breaker, paging, search semantics | I4, I5 | existing |
| `page=0`, `pageSize=0/101`, 101-char search → 400 | I6, I5 | existing |
| Unknown / staff / malformed id → 404 | I7 | existing |
| Detail counts and latest 10 | I8 | existing |
| Bad number → binding message, range checks skipped | I9 | **new, test-first (green on MVC)** |
| Blank values → defaults | I10 | **new (green on MVC)** |
| Repeated key → first value | I11 | **new, test-first (green on MVC)** |
| Exact range texts + 400 shape | I12 | **new (green on MVC)** |
| 404 problem body | I13 | **new (green on MVC)** |

## 5. Mutations (High) — break → red → restore from scratch copy → `cmp`

| # | Mutation | Expected red |
|---|---|---|
| M1 | Group policy `AdminOrSupport` → `Admin` | I1 (Support 403) + inventory lines 20–21 |
| M2 | Drop `.RequireAuthorization(...)` on the group | II11 + inventory + I1 (anonymous 200) |
| M3 | Role flag `user.IsInRole(IdentityRoles.Admin)` → `true` | I2 (Support sees `totalSpent`) |
| M4 | `pageSize is < 1 or > MaxPageSize` → `pageSize is < 1` | I6 (`pageSize=101`), I12 |
| M5 | `search?.FirstOrDefault()` → `string.Join(",", search ?? [])` | I11 |

## 6. What the reviewer checks (Rob)

- **Queries per request (unchanged, counted from the readers):** list = **2** (`COUNT` + page in `UserRepository.SearchCustomersAsync`). Detail = **1** on 404; else 1 (Identity) + 5 (Orders: all refs, paid sum, recent 10, returns count, recent returns) + 4 (Payments: count, actor sum, recent 10, payment→order map) = **10**, or **6** with no orders. Constant in the number of rows: no N+1. Handler: one service call, no loop, no other service. `git diff --stat` shows nothing under `Application/` or other modules.
- **Per-row lookups:** none on the list (no per-customer order counts or totals — decision-log row "Admin customers API" rejected them); detail mapping is in-memory `Select` over already-loaded lists; `numbers` lookup is a dictionary.
- **Projection:** response maps the same fields as the controller; no new fields; Support's `totalSpent` is `null`, not omitted (I2).
- **Paging limits:** `pageSize` 1–100, `page` ≥ 1, `search` ≤ 100 after trim, all enforced **before** the service call; detail lists capped by `CustomerQueryService.RecentTake = 10`.
- **Cross-module calls:** only `IUserReader`, `IOrderReader`, `IRefundState` contracts via the unchanged service; no csproj change, no foreign `DbSet`.
- **Behaviour:** binding (blank, bad, repeated), two-stage 400, texts, 404 body, policy, CORS identical; controller deleted; no dead `using`; no `var`.

## 7. Risks

| Risk | Mitigation |
|---|---|
| Repeated key changes meaning | C2, I11, M5 |
| Binding and range errors merged into one body | C4, I9 |
| 404 body differs from MVC's `[ApiController]` problem | C6, I13 pinned on MVC first |
| Controller and endpoints both mapped → `AmbiguousMatchException` | Delete in the same change |
| `int.TryParse` rejects hex/`#` forms that MVC's `Int32Converter` accepts (`?page=0x2`) | Accepted, same as P2.60 (`ProductEndpoints`); no pin. Backlog note |
| Existing over-fetch in the readers (not N+1) | Out of scope (C9); recorded in `backlog.md` as P2.64 plan notes |
