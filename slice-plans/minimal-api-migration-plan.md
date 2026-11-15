# Minimal API migration — plan (Rob, rev 2, 2026-11-14)

Status: **APPROVED by Nathaniel (2026-11-14): migrate every controller.** Option A (keep MVC) is dropped. Rules (CLAUDE.md §2/§5): one controller per slice, commit after each PASS, plain and minimal code, every review hunts N+1 queries, extra round-trips, over-fetching and bugs.
Hard rule 7 now reads "Thin endpoints" (`03-principles-hard-rules.md:40`). `docs/quality-bar.md` and `.claude/agents/*` still say "controllers": read that as "endpoint handlers".

Scope: 22 controller classes (Categories already converted in commit `d01ae3c`, unreviewed), `ModuleControllerFeatureProvider`, MVC wiring in `Program.cs` / `ModuleComposition.cs`. Stack lock unaffected: minimal APIs are part of ASP.NET Core (`02-stack-lock.md`).

**Per-slice `Read:` (Marco):** this plan §2–§4; the slice's own plan; arch §3.4 rule 7 (`03-principles-hard-rules.md:40`); §6.2 visibility (`06-backend-modules.md:63–69`); the controller file being converted and the integration tests that hit its routes (grep the route prefix under `backend/tests/Ecommerce.Api.IntegrationTests`).

---

## 1. Docs

| File | Change | When |
|---|---|---|
| `06-backend-modules.md:65` | `Map<M>Endpoints` listed as a host-facing extra; MVC discovery sentence marked transitional | done by Rob with this plan |
| `13.2-decision-log.md` | one row "Minimal API migration" (pattern, groups, JSON, CORS, slice order) | done by Rob with this plan |
| `06-backend-modules.md:24, 34, 41, 65` and `13.2:16` (Module controllers), `:39` | "controllers" → "endpoints"; drop the provider sentence; row 16 rewritten in place | Rob, in P2.81 (MVC removal) |
| `09-api.md:44` `[AllowAnonymous]` | → `AllowAnonymous()` | Rob, in P2.71 (Auth) |

## 2. Target shape

### 2.1 Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | **One file per former controller**: `Api/<Name>Endpoints.cs`, `internal static class <Name>Endpoints` with `public static void Map(IEndpointRouteBuilder app)`. `<M>Module.Map<M>Endpoints(this IEndpointRouteBuilder app)` (public, the only public addition) calls each `<Name>Endpoints.Map(app)` in turn and returns `app`. P2.59 renames the sample's `CatalogEndpoints` to `CategoryEndpoints` | 1:1 with the deleted controller, so each diff reads as "this controller became this file"; files stay small; no aggregator class per module | One `<M>Endpoints` class per module (Orders and Admin would be 300+ lines mixing four policies) |
| D2 | `ModuleComposition.MapModuleEndpoints` calls each `Map<M>Endpoints` explicitly (a module is added to it in the slice that converts its first controller). `Program.cs` calls it once, before `MapControllers` | Explicit, compile-checked; same shape as `AddEcommerceModules` | Assembly scanning / reflection |
| D3 | Handlers are `private static async Task<…>` **named methods** returning `TypedResults` (`Results<Ok<T>, NotFound, …>` when more than one). Services as `[FromServices]` parameters; `ClaimsPrincipal user`, `HttpRequest`/`HttpResponse`, `[FromHeader(Name = "Idempotency-Key")] string?` as needed | Readable, real stack traces; `[FromServices]` stops a service being inferred as the JSON body | Lambdas; implicit inference |
| D4 | **Routes and groups.** Each file builds **one** group from `app` (never from another group) on the **parent** of the controller's route, and maps leaf paths that spell the resource: `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin)` → `MapGet("products", …)`, `MapGet("products/{id:guid}", …)`. A single-route controller may skip the group: `app.MapGet("api/v1/categories", …).AllowAnonymous()` | `MapGroup("api/v1/categories").MapGet("")` registers RawText `api/v1/categories/` (trailing slash) — II11 went red in the sample. Groups only from `app` mean policies never stack (nested `RequireAuthorization` ANDs) | `MapGet("")` / `"/"` on a resource group; nested groups |
| D5 | Policy and anonymity exactly as the controller had: class `[Authorize(Policy=X)]` → group `.RequireAuthorization(X)`; bare `[Authorize]` → `.RequireAuthorization()`; `[AllowAnonymous]` → `.AllowAnonymous()` on exactly those routes. Proven by the P2.59 inventory test | Inventory pins policies per route | — |
| D6 | **JSON (P2.59):** one host method sets `JsonStringEnumConverter` + `UnmappedMemberHandling.Disallow` on both `Mvc.JsonOptions` (until P2.81) and `Http.Json.JsonOptions` (`ConfigureHttpJsonOptions`) | Minimal APIs ignore `AddJsonOptions`: without this enums become numbers and unknown members are silently accepted (mass-assignment defence lost) | Two copies of the settings |
| D7 | **CORS (P2.62, first admin slice):** move today's prefix convention (`Program.cs:117–128`) into one host local function that reads `RawText.TrimStart('/')`; apply it to `MapControllers()` and to a host group `app.MapGroup("")` that wraps `MapModuleEndpoints()` | Same policies on the same prefixes; modules never learn CORS policy names. Not in P2.59: Categories/Products/Images use the default policy, so it would have no consumer yet | Per-module `RequireCors(name)` |
| D8 | **Errors.** Validation → `TypedResults.ValidationProblem(errors)`; plain problems → `TypedResults.Problem(statusCode:, title:, detail:)`; a 409 with `errors` → `TypedResults.Problem(new HttpValidationProblemDetails(errors) { Status = 409, Title = "Conflict" })`. Each module keeps its own small helper where one exists today (`AdminValidators.cs`, `AuthValidators.cs`, `ControllerValidation` in `ReturnsController.cs`); the MVC-returning helper is deleted when its last caller converts | Same `type`/`title`/`errors`/`traceId`/`correlationId` (both go through `IProblemDetailsService`). SharedKernel stays framework-free | A shared helper in SharedKernel |
| D9 | **Binding quirks.** A query number that today yields `errors.<field>` on garbage (`page=abc`) is bound as `string?` and parsed in the handler into the same 400. A required `IFormFile` becomes `IFormFile?` → `errors.file`. A body DTO whose non-nullable member MVC rejected implicitly gets an explicit validator rule if the slice's "omit member" pin goes 500/200 | Minimal API binding failures are a bare 400 with no `errors`; MVC's implicit `[Required]` disappears | Editing tests to accept the new shape |
| D10 | **Arch rule (P2.59):** `Controllers_live_only_in_a_module_Api_namespace` becomes "HTTP endpoints are declared only in `Ecommerce.<M>.Api`": (a) today's `ControllerBase` check; (b) source scan: `.MapGet( .MapPost( .MapPut( .MapDelete( .MapPatch( .MapMethods( .MapGroup(` in module sources only under `Api/`; (c) non-vacuous: controllers + scanned endpoint files > 0. Pure check + self-test in `RuleEvaluatorSelfTests` | Holds through the transition and stays non-vacuous at zero controllers | Deleting the test at the end |
| D11 | **Upload (P2.69):** `.DisableAntiforgery()`, `.WithFormOptions(multipartBodyLengthLimit: …)`, `.WithMetadata(new RequestSizeLimitAttribute(…))` with today's numbers | Form binding demands antiforgery; bearer-only API has no CSRF surface (same as MVC today) | `AddAntiforgery` |
| D12 | **Auth no-store (P2.71):** the `[ResponseCache(NoStore)]` becomes one endpoint filter on the auth group that sets the same `Cache-Control`/`Pragma` headers | No `ResponseCache` in minimal APIs | Header code in every handler |
| D13 | **Webhook (P2.80):** no body parameter; the handler reads `HttpRequest.Body`; `.WithMetadata(new RequestSizeLimitAttribute(262144))` | Raw bytes for the signature check | Binding a DTO |
| D14 | **MVC removal (P2.81):** delete `AddControllers`, `MapControllers`, `AddModuleControllers`, `ModuleControllerFeatureProvider`, `ModuleAssemblies`, the Mvc JSON half of D6, the CORS call on `MapControllers`. D10(a) tightens to "no `ControllerBase` subtype anywhere"; `Host_composes_every_module_assembly` is replaced by a check that every module implementation with `Api/*Endpoints.cs` contributes ≥ 1 endpoint (handler `MethodInfo` metadata → declaring assembly) | Proves composition at runtime instead of a list only MVC used | Keeping a test-only manifest |
| D15 | **Content-Type (P2.82):** body routes need no per-route `.Accepts`; the host convention `AcceptAnyContentType` (`Program.cs`) adds `*/*` to every JSON-body route in `moduleEndpoints`, so binding answers 415/400 after auth instead of the anonymous 404 fallback. Multipart routes are left alone (the upload keeps MVC's 404, IP6/IP7/IU5). CT4 fails for any JSON-body route without `*/*` | One place covers P2.72–P2.79 | Per-route `.Accepts(..., "*/*")` |

### 2.2 Files per slice

Delete `Api/<Name>Controller.cs` (or the one class, when a file holds several); add `Api/<Name>Endpoints.cs`; edit `<M>Module.cs` (+ `ModuleComposition.cs` the first time a module converts); module error helper only if D8 needs it. Request/response records unchanged. No migrations, no `*.Contracts` changes, no packages.

## 3. Behaviours to preserve and their proof

Rule for every slice: **all existing tests pass unedited**; the only test edits allowed are P2.59 (D10) and P2.81 (D14), each listed and justified.

| Behaviour | Proof |
|---|---|
| Method, route text, policies, anonymity, CORS policy per route | **P2.59 `EndpointInventoryTests`**: pinned table from `EndpointDataSource` — `(method, RawText.TrimStart('/'), policies, anonymous, CORS policy)`. A stacked group policy, a lost `AllowAnonymous`, a lost CORS policy or a changed route goes red. Plus II10/II11 (`Identity/AuthorizationTests.cs`) |
| Route text: no trailing slash | D4. Inventory and II11 compare exact RawText (leading `/` trimmed only) |
| 400 validation shape (`type`, `title`, `errors`, `traceId`, `correlationId`, `application/problem+json`) | P2.59 pin + `CatalogValidationTests` |
| 404 / 409 Problem shape from a handler | P2.59 pin (404); 409 pins come with the slice that owns a 409 |
| 401/403 shape | II10 `AssertProblem` |
| Strict JSON, enums as strings | P2.59 options test (D6) + IA1 (`AdminProductApiTests`) + `auth/me` `role` string (II10) |
| Missing non-nullable body member → 400 | Per slice, step 1, for each body DTO it converts (status + problem+json) |
| Location headers, CSV `Content-Disposition`, ETag/304, upload limits, webhook limit, no-store | Existing tests named in the slice row; metadata the tests can't see is added to the slice's step-1 pins |
| Idempotency-Key handling | Existing CartMerge, Checkout, RetryAndExpiry, Refund tests |
| Rate limiting, OpenAPI, antiforgery middleware | Absent today; add none |

## 4. Slices — one controller each, lowest risk first

**Standard steps in every slice** (the slice plan only adds specifics):
1. Baseline: run the integration tests for the controller's routes + `EndpointInventoryTests` on MVC, quote counts. Add any missing step-1 pins (§3) — green on MVC, quoted.
2. Convert: add `<Name>Endpoints.cs`, register it in `<M>Module.Map<M>Endpoints`, delete the controller **in the same change** (both mapped → `AmbiguousMatchException`).
3. Existing tests + inventory unedited and green; full `verify.sh`; mutations (High only).

**Every review (any tier):** handler does exactly what the controller did — same service call count (no added query, no loop of service calls = N+1), same status codes, same headers; no dead helper left behind; plain code.

Tier: **Low** = anonymous read-only GET, no auth, money or stock change (Marco's tests + `verify.sh`, Rob reads the diff, no mutations). **High** = everything else (full review + the listed mutations: break → red → restore → `cmp`).

| Row | Controller (file) | Tier | Mutations (High) |
|---|---|---|---|
| P2.59 | Shared setup (inventory + contract pins, D6 JSON, D10 arch rule) + **Categories** (adopt commit `d01ae3c`) | High | see slice plan (inventory, D10, D6) |
| P2.60 | Products (`ProductsController`) | Low | — (D9 `page=abc` proven by `CatalogValidationTests`) |
| P2.61 | Images (`ImagesController`) | Low | — (ETag/304/immutable/nosniff by `ImageFileApiTests`) |
| P2.62 | AdminSettings + D7 CORS host wiring | High | policy Admin→AdminOrSupport → inventory red; drop CORS group convention → inventory red |
| P2.63 | AdminSearch | High | drop group policy → inventory/II11 red |
| P2.64 | AdminCustomers | High | policy AdminOrSupport→Admin → inventory red |
| P2.65 | AdminDashboard (`AdminDashboardController`) | High | pass `false` for the admin flag → `kpis` test red |
| P2.66 | AdminInsights (sibling group on `admin/dashboard`) | High | give it AdminOrSupport → Support-403 test red |
| P2.67 | AdminProjections (in `AdminDashboardController.cs`) | High | drop Admin policy → inventory red |
| P2.68 | AdminCategories | High | remove `Disallow` from HTTP JSON → unknown-member test red |
| P2.69 | AdminProducts (+ D11 upload) | High | drop `DisableAntiforgery` → IU1 red; drop size-limit metadata → inventory/IU red |
| P2.70 | AdminInventory (stock adjust) | High | map the concurrency result to 200 → 409 test red; policy → AdminOrSupport → Support-403 red |
| P2.71 | Auth (+ D12 no-store) | High | drop `AllowAnonymous` on refresh → II11 red; drop no-store filter → P2.59 pin red |
| P2.72 | Cart | High | skip Idempotency-Key check → merge-400 test red; fixed user id instead of `sub` → isolation test red |
| P2.73 | AdminOrderReads (in `ReturnsController.cs`) | High | AdminOrSupport→Admin → inventory red |
| P2.74 | Returns (customer return request) | High | skip ownership (`sub`) → other-customer test red |
| P2.75 | AdminReturns (approve/decline) | High | drop Admin policy → Support-403 red |
| P2.76 | AdminOrders (state transitions) | High | map writes with AdminOrSupport → Support-403 red |
| P2.77 | Orders (reads + retry-payment) | High | retry `Forbidden` → 200 → T7 red; skip Idempotency-Key → 400 test red |
| P2.78 | AdminRefund (money) | High | skip Idempotency-Key → refund-400 test red |
| P2.79 | **Checkout** | High | skip Idempotency-Key → checkout-400 test red; drop `RequireAuthorization` → inventory red |
| P2.80 | **StripeWebhook** (+ D13) | High | pass `null` instead of `Stripe-Signature` → signed-event tests red; drop size-limit → inventory red |
| P2.81 | Remove MVC (D14) + §1 doc edits | High | dummy `ControllerBase` in a module → D10 red; remove one `Map<M>Endpoints` call → D14 composition test red |

`ReturnsController.cs` holds three classes: each slice deletes only its class; the file and `ControllerValidation` go with the last (P2.75).

## 5. Risks

- **Policy drift** (stacked or lost group policy, lost `AllowAnonymous`): inventory test + D4 "groups only from `app`".
- **Strict JSON lost on minimal endpoints** (mass assignment): D6 + options test + IA1 mutation in P2.68.
- **MVC implicit `[Required]` gone**: step-1 "omit member" pins per body DTO (D9).
- **Bare binding 400s** (no `errors`): D9; pins in `CatalogValidationTests`, IU4.
- **Trailing-slash RawText**: D4.
- **CORS on admin/auth/cart lost**: D7 lands with the first admin slice, proven by the inventory CORS column.
- **Two stacks for 22 slices**: safe — routes never overlap because the controller is deleted in the same change; stop at any BLOCKED slice.
- **`MapGroup("")` RawText** (D7): if it changes anything beyond a leading `/`, Marco stops and reports; no improvised fix.
