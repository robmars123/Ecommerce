# Minimal API migration — plan (Rob)

Status: **NEEDS NATHANIEL DECISION.** Nothing is built until Nathaniel approves (§6 below) and makes the doc edits in §1.1.
Scope: the 22 controller classes in 19 files under `backend/src/Modules/*/Ecommerce.*/Api/`, plus `Ecommerce.Api/Composition/ModuleControllerFeatureProvider.cs` and the MVC wiring in `Program.cs` / `ModuleComposition.cs`. 56 actions in total.

**Read (Marco, per slice):** this plan; arch §3.4 (`03-principles-hard-rules.md`), §6.2 (`06-backend-modules.md` lines 56–69), §9 (`09-api.md` lines 1–5, 40–50), §10.1 (`10-cross-cutting.md` lines 10–30), §11.2; the slice's controller files and the integration tests named in its row in §4.

---

## 1. Doc and rule conflicts

Minimal APIs are part of ASP.NET Core, so the **stack lock (§2, `02-stack-lock.md:7`) is not broken**. No 09-api or 10-cross-cutting rule depends on MVC; only the wording below names controllers.

### 1.1 Needs Nathaniel's own decision or edit (agents may not change these)

| File:line | Today | Proposed wording |
|---|---|---|
| `docs/architecture/03-principles-hard-rules.md:40` (hard rule 7) | **Thin controllers.** Validate, call an application service, map the response. No business logic. | **Thin endpoints.** An HTTP endpoint handler validates, calls an application service, and maps the response. No business logic. |
| `CLAUDE.md:20` (§2 hard rules) | `· thin controllers ·` | `· thin endpoints ·` |
| `docs/quality-bar.md:30` | `no logic in controllers` | `no logic in endpoint handlers` |
| `docs/quality-bar.md:51` | `thin controllers` | `thin endpoints` |
| `.claude/agents/marco.md:21`, `.claude/agents/shin.md:25` | rule list (check for "controllers") | same substitution, if present |

Rule 7 is a hard rule: changing its wording is an owner decision. **Until those lines change, a minimal API handler is technically outside rule 7's wording**, so slice P2.59 must not start before the edit.

### 1.2 Rob updates these in the slice that makes them stale (owning section, no owner decision)

- `06-backend-modules.md:24, 34, 41, 60` — "controller(s)" → "endpoints".
- `06-backend-modules.md:65` — add `Map<M>Endpoints` as a host-facing extra on `<M>Module`; replace the `AddModuleControllers`/`ModuleControllerFeatureProvider` sentence with `MapModuleEndpoints()`. `:69` — the D9 rule.
- `13.2-decision-log.md:16` (Module controllers) — update in place (D1/D2); `:39` "by controllers" → "by endpoint handlers"; one new row for the migration.
- `09-api.md:44` `[AllowAnonymous]` → `AllowAnonymous` (same metadata in both styles).

### 1.3 Architecture tests (`Ecommerce.ArchitectureTests`)

- `A_module_implementation_exports_only_its_module_class` — **unchanged, stays green**: `Map<M>Endpoints` is a method on `<M>Module`; endpoint classes stay `internal` (precedent `NotificationsModule.cs:42`).
- `Controllers_live_only_in_a_module_Api_namespace` (`ModuleVisibilityTests.cs:55–74`) — **must change**: `Assert.True(controllers > 0)` goes red once the last controller is gone. Replacement D9; reviewed as a possibly weakened invariant.
- `Host_composes_every_module_assembly` (`:77–85`) — `ModuleAssemblies` only feeds MVC application parts. Replaced in P2.67 (D10).
- `Api_types_never_depend_on_infrastructure_or_ef` — still applies **only if** handlers are methods of types in `Ecommerce.<M>.Api` (D3).

## 2. Target shape

### 2.1 Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | Each module has one `internal static class <M>Endpoints` in `Ecommerce.<M>.Api` (more only where one module has more than one policy group, e.g. `AdminOrderEndpoints`); `<M>Module` gets `public static IEndpointRouteBuilder Map<M>Endpoints(this IEndpointRouteBuilder app)` that calls it | Keeps "only `<M>Module` public"; same shape as `MapNotificationsHub` | Public endpoint classes (leak); reflection discovery of `IEndpointModule` types (an abstraction for one caller) |
| D2 | Host: `ModuleComposition.MapModuleEndpoints(this IEndpointRouteBuilder)` calls the eight (seven with HTTP) `Map<M>Endpoints` explicitly, like `AddEcommerceModules`. `Program.cs` maps them inside one host group that carries the CORS convention (D7) | Explicit, compile-checked, no reflection | Assembly scanning (needs a public marker or reflection on internals) |
| D3 | Handlers are `private static async Task<Results<…>>` **named methods**, not lambdas. Services come in as `[FromServices]` parameters; `ClaimsPrincipal user`, `HttpRequest`/`HttpResponse` where needed; `IClock` injected where the controller used it (`AdminInsights`) | Named methods keep the layer rule and stack traces meaningful; explicit `[FromServices]` stops RDF from inferring an unregistered service as a JSON body | Lambdas (closure types, harder to scan); implicit service inference |
| D4 | Policies: **one** `RequireAuthorization(policy)` per route group that mirrors exactly one of today's controller classes; groups that share a URL prefix but differ in policy (`admin/orders` Admin vs AdminOrSupport, `admin/dashboard` AdminOrSupport vs insights Admin) are **sibling** groups, never nested. `.AllowAnonymous()` on exactly the routes that have `[AllowAnonymous]` today | Nested `RequireAuthorization` calls AND together; a wrong nesting silently grants or removes Support access | A single `/api/v1/admin` group with a default policy |
| D5 | JSON: one host method configures both `Mvc.JsonOptions` (while any controller remains) and `Http.Json.JsonOptions` via `ConfigureHttpJsonOptions`: `JsonStringEnumConverter` + `UnmappedMemberHandling.Disallow` | Minimal APIs ignore `AddControllers().AddJsonOptions`; without this, enums become numbers and **unknown members are silently accepted (mass-assignment defence D13 lost)** | Duplicated settings |
| D6 | Validation results use `TypedResults.ValidationProblem(dict)`; a 409 with `errors` uses `TypedResults.Problem(new HttpValidationProblemDetails(dict) { Status = 409, Title = "Conflict" })`. Each module keeps one internal `ToProblem(ValidationResult)` helper (they exist per module today; SharedKernel may not reference ASP.NET Core) | Same `type`/`title`/`errors`/`traceId`/`correlationId` as MVC (both go through `IProblemDetailsService`) | A shared helper in SharedKernel (breaks its framework-free rule) |
| D7 | CORS: extract today's `MapControllers().Add(…)` prefix convention into one host-private method and apply it to both the controller builder and the module group until P2.67 | Same policies on the same prefixes; no CORS policy names leak into modules | Per-module `RequireCors(name)` (host constants become module knowledge) |
| D8 | Query numbers that are `int?` today and must yield `errors.<field>` on garbage (`page=abc`, `CatalogValidationTests:15–16`) are bound as `string?` and parsed in the handler into the existing 400 shape. Required `IFormFile` becomes `IFormFile?` with `errors.file` "The file field is required." | Minimal API binding failures are a bare 400 Problem with no `errors` map, so `CatalogValidationTests` and `IU4 "none"` would go red | Accepting the shape change (edits tests) |
| D9 | Arch rule replacement (in P2.59): "HTTP endpoints are declared only in `Ecommerce.<M>.Api`": (a) no `ControllerBase` subtype outside `Ecommerce.<M>.Api` (today's check), (b) source scan: `MapGet|MapPost|MapPut|MapDelete|MapMethods|MapGroup` appears in module sources only under `Api/`, (c) non-vacuous: controllers + endpoint files > 0. P2.67 tightens (a) to "no `ControllerBase` subtype anywhere". Evaluator gets a self-test (§6.2 requires one per rule) | Holds across the transition and ends stricter than today | Deleting the test at the end |
| D10 | P2.67 deletes `ModuleAssemblies`, `AddModuleControllers`, `ModuleControllerFeatureProvider`, `AddControllers`, `MapControllers`; `Host_composes_every_module_assembly` is replaced by an integration test that every module with an `Api` endpoint class contributes at least one endpoint (endpoint `MethodInfo` metadata → declaring assembly) | Proves composition at runtime instead of a list nobody uses | Keeping a test-only manifest |
| D11 | Upload endpoint: `.DisableAntiforgery()`, `.WithFormOptions(multipartBodyLengthLimit: ImageFormat.MaxBytes + 1)`, `.WithMetadata(new RequestSizeLimitAttribute(ImageFormat.MaxBytes + 64 * 1024))`; content type is inferred from the `IFormFile` parameter (`multipart/form-data` accepts metadata) | Form binding adds antiforgery metadata and throws without the middleware; bearer-only, so no CSRF surface — same as MVC `[ApiController]` today | Adding `AddAntiforgery` (new behaviour, cookie/token plumbing) |
| D12 | `AuthController`'s `[ResponseCache(NoStore, Location=None)]` becomes a group endpoint filter on the auth group that sets the same headers before `next` | No `ResponseCache` filter in minimal APIs | Per-handler header code |

### 2.2 Files

- Per module: delete `Api/*Controller.cs`; add `Api/<M>Endpoints.cs` (or one per policy group); edit `<M>Module.cs` (add `Map<M>Endpoints`); edit the module's `ToProblem`/`InvalidProblem` helpers (`AdminValidators.cs:158`, `AuthValidators.cs:83`, the `ControllerBase` extension in `ReturnsController.cs:16`) to return typed results. Request/response records unchanged.
- Host: `Program.cs` (JSON D5, group + CORS D7, `MapModuleEndpoints`), `Composition/ModuleComposition.cs`; P2.67 deletes `Composition/ModuleControllerFeatureProvider.cs`.
- Tests: new `Ecommerce.Api.IntegrationTests/EndpointInventoryTests.cs` and `HttpContractCharacterizationTests.cs` (P2.59); `ModuleVisibilityTests.cs` + `RuleEvaluatorSelfTests.cs` (D9).
- No migrations. No contract (`*.Contracts`) changes.

## 3. Behaviours to preserve and how each is proven

Rule for every slice: **all existing integration tests pass unedited.** P2.59 adds characterization tests, green against the MVC controllers first, that stay unedited to the end.

| Behaviour | Today | Proof |
|---|---|---|
| Per-route policies, `[AllowAnonymous]`, fallback policy | class/action attributes (`IdentityModule.cs:134–139`) | **New `EndpointInventoryTests` (P2.59):** pinned table of all 56 `(method, route, policies, anonymous)` rows from `EndpointDataSource`; a stacked group policy shows as an extra `IAuthorizeData` row → red. Plus existing II10, II11 (`AuthorizationTests.cs:31–125`), per-module 401/403 tests |
| Route text (`api/v1/...`, constraints `{id:guid}`, `{**key}`) | attribute routes | II11 compares `RawText.TrimStart('/')`; root routes use `""` not `"/"` (else a trailing slash breaks II11). Inventory test pins every route |
| 400 Problem shape on validation (`errors`, `traceId`, `correlationId`, `application/problem+json`) | `ValidationProblem(ModelState)` | `CatalogValidationTests` (incl. `page=abc`, D8), `ProblemDetailsTests`, T23 (`CheckoutTests.cs:271`), IU4 |
| Strict JSON (unknown member → 400) and enums as strings | `AddJsonOptions` (`Program.cs:32–39`) | IA1 (`AdminProductApiTests.cs:33`), T23 money case; every response test that reads an enum string |
| Missing non-nullable member → 400 (MVC implicit `[Required]`, `Nullable=enable`) | `[ApiController]` | **New characterization (P2.59):** for each body DTO, omit each non-nullable member → 400 problem+json (status + content type only). Catches a validator that would NRE (500) once MVC stops pre-validating |
| Malformed JSON / unparsable route or query → 400 | `[ApiController]` | Characterization asserts status + problem+json only. **Known delta:** the bare-binding 400 loses MVC's `errors["$.x"]` map; §9 requires only Problem Details. Rob decision, recorded in 13.2 |
| Upload: multipart only, form limit, size limit, antiforgery | `AdminProductsController.cs:103–107` | IU1–IU9; IU5 stays 404 (the `{*path:nonfile}` fallback survives the accepts policy, as in MVC); inventory pins accepts, form and size-limit metadata |
| Images catch-all, ETag/304, immutable cache, nosniff | `ImagesController.cs` | IF1 + 304 test (`ImageFileApiTests.cs:31–74`); `TypedResults.File(bytes, type, lastModified, entityTag)` keeps conditional-request handling |
| Webhook raw body + signature, 256 KB limit | `StripeWebhookController.cs` | `WebhookTests`. Handler binds **no** body parameter, reads `HttpRequest.Body`; inventory pins `MaxRequestBodySize = 262144` |
| Idempotency-Key (cart merge, checkout, retry, refund) | `[FromHeader(Name="Idempotency-Key")]` | CartMerge, Checkout, RetryAndExpiry, Refund tests |
| Rate limiting, OpenAPI, antiforgery | **none in `src`** (`AddRateLimiter`, `MapOpenApi`, `AddAntiforgery` absent) | Nothing to preserve; add none. Antiforgery: D11 + IU1. Rate limiting is a §10.1 gap that pre-dates this plan (backlog NOTE) |
| CORS (default / cart / credentialed by prefix) | `Program.cs:117–128` | `CartCorsTests`, `CatalogCorsTests`, `IdentityHostTests` |
| Location headers | `Created(uri, …)` literals; no `CreatedAtAction` exists | `AdminProductApiTests.cs:26`, Returns create test |
| Auth no-store | `[ResponseCache]` on `AuthController` | `AuthFlowTests.cs:25`; **new (P2.59):** exact `Cache-Control` and `Pragma` on every auth route, including a 400 |
| Cookie refresh transport | `AuthController` + `transport` | existing Identity cookie tests |
| CSV export (Content-Disposition) | `File(bytes, type, name)` | `AdminInsightsApiTests.cs:150–151` |
| Problem `type`/`title` per status, `traceId`, `correlationId` | MVC `ProblemDetailsFactory` | **New (P2.59):** one test per shape — 400 validation, 404 Problem, 409 with `errors`, 409 with `available` (cart), 401/403, empty 404 (images) |

## 4. Slices (one module per slice, lowest risk first)

Every slice is **High** under CLAUDE.md §5: each one changes auth policy wiring, and most move stock, money or idempotency. That is part of the cost (§6). Each slice: full review, the mutations listed, full `verify.sh` on build and in Rob's PASS round.

Proposed tracker section header (`docs/admin-dashboard-feature-tracker.md`, after section **F**; not edited by this plan):

`| **G** | **Minimal API migration: controllers → per-module endpoint groups, behaviour unchanged (plan: slice-plans/minimal-api-migration-plan.md)** | | |`

| Row | Slice | Tier | Proof (existing tests unedited + new) | Mutations (break → red → restore → `cmp`) |
|---|---|---|---|---|
| P2.59 | Groundwork: D5 JSON for both stacks, D7 CORS method, `MapModuleEndpoints` stub, D9 arch rule + self-test, `EndpointInventoryTests`, characterization tests (§3). **No controller converted** | High | All green on MVC; arch tests green | (1) `AdminSettingsController` policy Admin → AdminOrSupport → inventory red. (2) a `MapGet` in a module `Application/` file → D9 rule red |
| P2.60 | Admin: Customers, Dashboard + Projections, Insights, Search, Settings | High | Admin*ApiTests, Projections tests, II10/II11, inventory | (1) give the Insights group AdminOrSupport instead of Admin → inventory + Insights Support-403 red. (2) pass `false` for the admin flag on dashboard → `kpis` test red |
| P2.61 | Catalog: Products, Categories, Images, AdminProducts (upload), AdminCategories | High | Catalog*, AdminProduct*, AdminCategory*, ImageFile*, Catalog CORS, IU1–IU9 | (1) drop `.AllowAnonymous()` on images → II11 red. (2) drop `.DisableAntiforgery()` → IU1 red. (3) remove `Disallow` from HTTP JSON options → IA1 red. (4) drop form-limit metadata → inventory red |
| P2.62 | Inventory: AdminInventory (list, adjust) | High | AdminInventoryAdjustTests (incl. 403 at `:152–162`), inventory | (1) group policy → AdminOrSupport → Support-403 red. (2) map the concurrency result to 200 → 409 test red |
| P2.63 | Identity: Auth (register, login, refresh, logout, reset, confirm, me) | High | AuthFlow, Identity cookie/host tests, II10–II12, new no-store test | (1) drop `.AllowAnonymous()` on refresh → II11 red. (2) drop the no-store filter → AuthFlowTests red. (3) force body transport → cookie tests red |
| P2.64 | Cart | High | CartApi, CartMerge, CartCors, cart isolation tests | (1) drop group `RequireAuthorization()` → inventory/II11 red. (2) skip the Idempotency-Key check → merge 400 test red. (3) use a fixed user id instead of `sub` → isolation test red |
| P2.65 | Orders: Orders (incl. retry-payment), Returns + AdminReturns + AdminOrderReads, AdminOrders, **Checkout** | High | Checkout*, RetryAndExpiry, Orders*, AdminOrderList, Return*, FulfillmentSaga | (1) skip checkout Idempotency-Key check → checkout 400 test red. (2) map AdminOrders writes in the AdminOrSupport read group → Support-403 red. (3) map retry `Forbidden` → 200 → T7 red |
| P2.66 | Payments: AdminRefund, **StripeWebhook** | High | WebhookTests, RefundTests, FulfillmentSaga, inventory | (1) pass `null` instead of the `Stripe-Signature` header → signed-event tests red. (2) skip refund Idempotency-Key check → refund 400 test red. (3) drop webhook size-limit metadata → inventory red |
| P2.67 | Remove MVC: `AddControllers`, `MapControllers`, `AddModuleControllers`, `ModuleControllerFeatureProvider`, `ModuleAssemblies`, Mvc JSON options; D9 tightened; D10 composition test; §1.2 doc edits | High | Full suite; arch tests; D10 test | (1) add a dummy `ControllerBase` in a module → tightened D9 red. (2) remove one `Map<M>Endpoints` call → D10 red |

### Build order inside each slice

1. Run the slice's existing tests + inventory on MVC (green baseline, quote counts).
2. Add the module's `Map<M>Endpoints` and endpoint class(es); delete the controllers in the **same** change (never both mapped: duplicate routes → `AmbiguousMatchException`).
3. Full `verify.sh`; then the mutations.

### Required tests (§11.2 numbering is unchanged; no new numbered rows)

- Test-first (red quoted in P2.59): the D9 rule self-test (red on a violating input); inventory and characterization tests are characterization, so they are green on MVC by design — Marco proves each can fail by mutation (1) of P2.59.
- No existing test may be edited in P2.60–P2.66. P2.67 changes only `ModuleVisibilityTests` (D9 tighten, D10 replace), listed and justified.

## 5. Packages

**None.** `Microsoft.AspNetCore.App` (already referenced by each module) contains minimal APIs, `TypedResults`, `RouteGroupBuilder`, endpoint filters, form options and antiforgery. `FluentValidation` stays as is. No OpenAPI, validation or rate-limiting package is added.

## 6. Risks and recommendation

### Risks

The migration-specific risks are the rows of §3 marked "New" or "Known delta" plus D4, D5, D8, D11: lost strict JSON (mass assignment), AND-ed or lost group policies, MVC's implicit `[Required]` disappearing (validator NRE → 500), binding 400s without `errors`, form binding throwing without antiforgery, size-limit metadata ignored, a root `"/"` route breaking II11, and two stacks configured in parallel for seven slices. Review cost: nine High slices, ~25 mutations, ~1,600 lines rewritten.

### Is it worth it?

**No, not now.** CLAUDE.md says "smallest change that meets the docs; nothing speculative". The docs are met today: controllers are internal, thin, discovered by a 17-line provider, and every behaviour above is tested. The migration delivers **no user-facing or correctness gain**; its benefits (slightly less per-request overhead, typed results, no feature provider, AOT readiness) have no consumer — the stack lock has no AOT target and there is no measured latency problem. Its cost is nine High-risk slices, a hard-rule wording change, two architecture test changes, and a known 400-shape delta for malformed JSON.

### Options

- **A (recommended): keep MVC controllers.** Record "Controllers stay MVC; minimal API migration declined — no consumer for its benefits" as a 13.2 row. Zero risk; no doc edits. Optional: take P2.59's `EndpointInventoryTests` alone; it strengthens auth coverage either way.
- **B: full phased migration (this plan).** Do it only if Nathaniel wants it for a reason not in the docs (e.g. consistency with a future AOT target or learning). Requires the §1.1 edits first; P2.59 → P2.67 in order; stop at any BLOCKED slice — the two stacks coexist safely.
- **C (rejected): hybrid** (new endpoints minimal, old stay MVC): two styles and two JSON configs forever.
- **D (rejected): big bang** in one slice: 56 actions and every money path in one review breaks the CLAUDE.md §5 caps.
