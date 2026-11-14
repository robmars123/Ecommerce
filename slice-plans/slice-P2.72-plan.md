# Slice P2.72 — Cart to Minimal API (plan, Rob)

Tier **High** (customer money display, ownership by `sub`, merge idempotency, stock 409). Mode Autonomous. Row: tracker `P2.72 | Cart endpoints to Minimal API | Cart`.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 (incl. D15) · this plan · `docs/architecture/03-principles-hard-rules.md:40` · `docs/architecture/06-backend-modules.md:63-69` · `backend/src/Modules/Cart/Ecommerce.Cart/Api/{CartController,CartRequests,CartResponses}.cs` · `.../Ecommerce.Cart/CartModule.cs` · `.../Application/CartResults.cs` (outcome records only) · `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs:30-31` · pattern: `backend/src/Modules/Inventory/Ecommerce.Inventory/Api/AdminInventoryEndpoints.cs` (whole file) · tests: `backend/tests/Ecommerce.Api.IntegrationTests/Cart/{CartEnv,CartApiTests,CartMergeTests}.cs`, `.../Inventory/AdminInventoryContractTests.cs:20-50` (`AssertProblem`, `Keys`), `.../EndpointInventoryTests.cs:19,36,65,66,73`.

Do **not** read or touch `CartService`, `CartMergeService`, `CartViewBuilder`, `CartStore`, Domain or Persistence: unchanged in this slice. Do not read `AuthEndpoints.cs` (Inventory's file has every pattern you need).

## 1. Scope facts (checked by Rob)

- Five routes, class `[Authorize]` (no policy), CORS policy `cart` (host prefix convention, D7): `GET api/v1/cart`, `POST api/v1/cart/items`, `PUT|DELETE api/v1/cart/items/{variantId:guid}`, `POST api/v1/cart/merge`. Inventory rows `:19,:36,:65,:66,:73` = `(default) | auth | cart`.
- **Ownership:** the owner is always the token's `sub`; no route or body carries a cart id or user id. `{variantId}` is a catalog id, not an owned resource: another user's line id simply isn't in the caller's cart → `PUT` 404 "Item not in cart", `DELETE` 204 no-op (existing `T21_one_users_writes_never_appear…`). There is **no** server guest cart and **no** cart-id cookie or header: the guest cart lives on the device and enters only through `merge` (Idempotency-Key, keyed per user + body hash; another user's key → 409). Nothing to pin for cookie flags; CC7 pins that no `Set-Cookie` appears.
- **Money:** no request DTO has a price/total/user member (`CartRequests.cs:5`); strict JSON (D6) turns one into 400 (existing T21 price test, T22 merge price line). All money is computed in `CartViewBuilder` from Catalog snapshots and formatted by `MoneyResponse.From` (`"0.00##"`, string). The handler computes nothing; it maps service records with the existing `ToResponse()` extensions. Checkout re-prices independently (not this slice).
- **No version/ETag concurrency** on cart: writes serialise on the cart row lock (`FOR UPDATE`) inside the service. The only 409s are stock (`Insufficient`, with `available`) and merge key reuse.
- **Merge on login is this controller** (`POST merge`): key check first, then body validation, one `MergeAsync`.
- **Queries per request today** (unchanged by this slice; all lookups batched with `ANY(@ids)`, **no N+1**): GET = 1 cart+items read + 1 snapshots + 1 availability (empty cart: 1). Add/Set = 1 tx: insert-if-absent + `SELECT … FOR UPDATE` + items load + 1 snapshot + 1 availability + view (1 snapshot + 1 availability) + **one** `SaveChangesAsync` (inside `EfUnitOfWork.InTransactionAsync`). Delete = tx: 3 lock queries + 1 save. Merge = tx: 3 lock + 1 key lookup + 1 snapshots + 1 availability + view 2 + 1 save. The view re-reading the just-written variant is a redundant round-trip pair, recorded as a backlog NOTE (not fixed here).

## 2. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| C1 | New `Api/CartEndpoints.cs`, `internal static class CartEndpoints`, one group `app.MapGroup("api/v1").RequireAuthorization()`; `MapGet("cart", Get)`, `MapPost("cart/items", Add)`, `MapPut("cart/items/{variantId:guid}", Set)`, `MapDelete("cart/items/{variantId:guid}", Remove)`, `MapPost("cart/merge", Merge)`. Delete `CartController.cs` in the same change | D1, D4 (group on the parent, never `MapGet("")`), D5 (bare `[Authorize]` → `RequireAuthorization()`). RawText stays exactly the inventory rows | `MapGroup("api/v1/cart").MapGet("")` (trailing slash) |
| C2 | `CartModule.MapCartEndpoints(this IEndpointRouteBuilder app)` calls `CartEndpoints.Map(app)`, returns `app`. `ModuleComposition.MapModuleEndpoints` appends `.MapCartEndpoints()` | D2; first Cart slice; inside the host group so the `cart` CORS policy and D15 `*/*` apply with no per-route code | `.Accepts(...)` per route (D15 forbids); `RequireCors("cart")` in the module |
| C3 | Owner id: `private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirst("sub")!.Value);` — the controller's line, with its comment | Same behaviour; `RequireAuthorization()` guarantees a validated token with `sub` | `TryParse` + 401 branch (behaviour MVC never had; no test) |
| C4 | Handlers (named `private static async Task<…>`): **Get** `(ClaimsPrincipal user, [FromServices] CartService cart, CancellationToken ct)` → `Ok<CartResponse>`. **Add** `([FromBody] AddCartItemRequest request, ClaimsPrincipal user, [FromServices] IValidator<AddCartItemRequest> validator, [FromServices] CartService cart, ct)`. **Set** `(Guid variantId, [FromBody] SetCartItemRequest request, …)`. **Remove** `(Guid variantId, ClaimsPrincipal user, [FromServices] CartService cart, ct)` → `NoContent` (service result ignored, as today). **Merge** `([FromBody] MergeCartRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, ClaimsPrincipal user, [FromServices] IValidator<MergeCartRequest> validator, [FromServices] CartMergeService merge, ct)` | D3. Exactly one service call per handler, same arguments as the controller (incl. `VariantId!.Value`, `Quantity!.Value`, `idempotencyKey!.Trim()`) | Injecting `CartViewBuilder` or a store (handler must not read data itself) |
| C5 | Add/Set: validate → `TypedResults.ValidationProblem(ToErrors(validation))`, else `ToResult(await cart.AddAsync/SetAsync(…))`. `private static Results<Ok<CartResponse>, ValidationProblem, ProblemHttpResult> ToResult(CartWriteResult result)` is a switch expression: `Ok` → `TypedResults.Ok(result.Cart!.ToResponse())`; `NotFound` → `TypedResults.Problem(statusCode: 404, title: "Item not in cart")`; `Insufficient` → `TypedResults.Problem(statusCode: 409, title: "Not enough stock", detail: $"Only {result.Available} available.", extensions: new Dictionary<string, object?> { ["available"] = result.Available })`; `_` → `TypedResults.ValidationProblem(new Dictionary<string, string[]> { [result.Field ?? ""] = [result.Message ?? "Invalid request."] })` | Same bodies as the controller's `Map`: plain `ProblemDetails` for 404/409 (no `errors`), `ValidationProblem` for service `Invalid` with the service's field key | Catalog's `HttpValidationProblemDetails` 409 (adds `errors`, wrong template — pinned by CC1) |
| C6 | Merge: (1) `!MergeCartRequestValidator.IsValidKey(idempotencyKey)` → `ValidationProblem({ ["Idempotency-Key"] = [$"Send an Idempotency-Key header of 1 to {MergeCartRequestValidator.MaxKeyLength} characters."] })`; (2) validate body → `ValidationProblem(ToErrors(…))`; (3) build `List<MergeLine>` exactly as the controller; (4) one `MergeAsync`; `Ok(reply.Result!.ToResponse())` or `Problem(409, "Conflict", "This Idempotency-Key was already used for a different request.")`. Return type `Results<Ok<MergeResponse>, ValidationProblem, ProblemHttpResult>` | Same order and messages as MVC (key before body validation; binding errors still come first on both stacks) | Validating body first (changes which `errors` key a double-bad request reports — CC5) |
| C7 | `ToErrors(ValidationResult)` `private static`, verbatim from `AdminInventoryEndpoints.cs` (first char lower-cased, grouped) | One consumer per module; keys match the controller's `InvalidProblem` (`lines`, `quantity`, `variantId`) | Shared helper across modules (boundary) |
| C8 | Accepted difference (13.2 row "Minimal API migration", P2.68 A6 / P2.70 P8 / P2.71 D-I): an unreadable body (malformed, empty, `null`, wrong type, unknown member incl. `price`) is a bare 400 problem — same status/`type`/`traceId`/`correlationId`, title/`errors` may differ. `{}` parses and reaches FluentValidation on both stacks (all request members are nullable), so its `errors` are pinned exactly | Already accepted; validators reject every null | Hand-parsing the body |
| C9 | No change to request/response records, validators, services, DI registrations other than C2, `*.Contracts`, migrations, packages | §2.2 | — |

## 3. Files

| File | Change |
|---|---|
| `backend/src/Modules/Cart/Ecommerce.Cart/Api/CartEndpoints.cs` | new (C1–C7) |
| `backend/src/Modules/Cart/Ecommerce.Cart/Api/CartController.cs` | delete |
| `backend/src/Modules/Cart/Ecommerce.Cart/CartModule.cs` | add `MapCartEndpoints` (+ `using Microsoft.AspNetCore.Routing;`) |
| `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` | append `.MapCartEndpoints()` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Cart/CartContractTests.cs` | new, step 1 (§5), green on MVC first |

## 4. Build order

1. **Baseline on MVC:** `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~.Cart.|FullyQualifiedName~EndpointInventoryTests|FullyQualifiedName~CartCorsTests" 2>&1 | tail -n 20`; quote counts.
2. **Pins first:** write `CartContractTests.cs` (§5) against the controller; capture every message/title from the real MVC response, then assert it; green on MVC, quote the run. Reuse `CartEnv` helpers; a local `AssertProblem`/`Keys` like `AdminInventoryContractTests`.
3. **Convert** (§3) in one change; controller deleted.
4. Step-1 filter again: same counts + the new file, all green, existing tests unedited (`git diff --stat backend/tests` = only the new file).
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote `VERIFY`.
6. Mutations (§6): break → red (quote) → restore from scratch copy → `cmp`.

## 5. Required tests — `CartContractTests.cs` (new, step 1, green on MVC)

Every problem response: `AssertProblem` (problem+json, `type`, non-empty `traceId`, `correlationId` = `id-test-1`). Every write-rejecting case also asserts the cart is unchanged via `QuantitiesAsync`.

| # | Request | Expected |
|---|---|---|
| CC1 **stock** | variant stock 4, add 1, then add 4 and PUT 5 | 409, type `…section-15.5.10`, title `Not enough stock`, detail `Only 4 available.`, `available` 4, **no `errors`**; quantity still 1 |
| CC2 **ownership** | A adds variant v (2); B `PUT items/{v}` qty 1; B `DELETE items/{v}`; B `GET` | B PUT → 404, type `…15.5.5`, title `Item not in cart`; B DELETE → 204; B GET empty; A still has v = 2 |
| CC3 validator 400 | Add `{"variantId":"00000000-0000-0000-0000-000000000000","quantity":0}`; Add `{}`; PUT `{}`; PUT `{"quantity":1001}` | 400, type `…15.5.1`, title `One or more validation errors occurred.`; `errors` keys exactly `{variantId, quantity}` / `{variantId, quantity}` / `{quantity}` / `{quantity}` with the captured messages (the `{}` variantId message is FluentValidation's default — see NOTE; pin what MVC returns) |
| CC4 service 400 | Add unknown variant (`Guid.NewGuid()`, qty 1); add 8 then add 3 (over cap) | `errors` exactly `{variantId: ["This item is not available."]}`; `{quantity: ["A line can hold 1 to <cap> of an item."]}` (captured cap); nothing written |
| CC5 merge 400 / 409 | No key + body `{"lines":[]}`; key + `{"lines":[]}`; reuse key with another body | first: `errors` keys exactly `{Idempotency-Key}` (key checked first), message captured; second: `errors` keys `{lines}`; third: 409, type `…15.5.10`, title `Conflict`, detail `This Idempotency-Key was already used for a different request.`, no `errors` |
| CC6 unreadable | POST items, PUT, merge each with: malformed `{"quantity":`, literal `null`, `{"quantity":"abc"}` (plus `variantId` where needed) | each 400 `AssertProblem` only (C8); cart unchanged; the merge key is still free afterwards (valid merge with it → 200) |
| CC7 **money / shape** | GET with no cart; add v (2) with no image; merge one unknown variant | empty GET raw body exactly `{"items":[],"subtotal":{"amount":"0.00","currency":"USD"}}`; line property names exactly `{variantId, productSlug, productName, variantName, image, quantity, unitPrice, lineTotal, stockStatus, issues}`, `image` present as `null`, `unitPrice`/`lineTotal`/`subtotal` each exactly `{amount, currency}` with `amount` a JSON **string** (`"10.00"`, `"20.00"`, `"20.00"`); merge body keys `{cart, results}`, result keys `{variantId, outcome, quantity, reason}`, `outcome` `"Removed"`, `reason` `"Unavailable"`; `Set-Cookie` absent on GET, add and merge |

Existing coverage that must stay green **unedited**: all of `CartApiTests` (T21 incl. anonymous 401 × 5, price-in-body 400, isolation, concurrent adds), `CartMergeTests` (T22 incl. replay, parallel same key, 100 lines, bad bodies), `CartSupportTests`, `CartCorsTests`, inventory rows above, `ContentTypeContractTests` (D15 CT4 now covers the three cart body routes).

## 6. Mutations (one per guard; quote red, restore from scratch copy, `cmp`)

| # | Break (in `CartEndpoints.cs` unless stated) | Red |
|---|---|---|
| M1 **ownership** | `UserId` returns a fixed `Guid` instead of reading `sub` | `T21_one_users_writes_never_appear…` + CC2 |
| M2 **auth** | drop `.RequireAuthorization()` from the group | inventory rows `:19,:36,:65,:66,:73` (T21 401 may stay green through the fallback policy; inventory is the proof) |
| M3 **idempotency** | skip the `IsValidKey` return | `T22_a_missing_or_overlong_key…` + CC5 |
| M4 **idempotency** | merge `Conflict` → `TypedResults.Ok(...)` path (e.g. return 200 with the empty cart) | `T22_the_same_key_with_a_different_body…` + CC5 |
| M5 **stock** | `Insufficient` arm without the `available` extension | `T21_a_quantity_above_available_is_409…` + CC1 |
| M6 **validation** | Add skips the validation return | `T21_an_add_whose_resulting_quantity…(-1)` (2 + −1 = 1 → 200) + CC3 |
| M7 **money** | `CartRequests.cs`: add `decimal? UnitPrice` to `AddCartItemRequest` (handler unchanged) | `T21_a_body_carrying_a_price_is_400…` — proves the no-price DTO + strict JSON is the guard, not the handler |

## 7. Reviewer checklist (Rob, review round)

- **One service call per handler**; no `DbContext`, `InTransactionAsync`, `SaveChanges`, `IProductReader`/`IInventoryReader` or loop in `CartEndpoints.cs`. Single `SaveChangesAsync` stays in `EfUnitOfWork` (untouched). `git diff --stat` shows **nothing** under `Application/`, `Infrastructure/`, `Domain/`.
- **Queries per request unchanged** (§1 counts); no per-line lookup added (N+1). The handler maps records only — no extra projection or fetch.
- **No money in the handler:** no `decimal`, arithmetic or formatting in `CartEndpoints.cs`; money reaches the wire only through `CartResponses.cs` (unchanged).
- **Ownership:** every handler passes `UserId(user)` and nothing from the request as the owner.
- 404/409 are plain `ProblemDetails` (no `errors`); service `Invalid` → `ValidationProblem` keyed by `result.Field`. Merge key check precedes body validation.
- No `.Accepts` (D15); no CORS code; controller deleted; no dead helper; `ToErrors` matches Inventory's.
- Existing tests unedited; only `CartContractTests.cs` added.

## 8. Risks

- **Problem-body drift** on 409 Insufficient (MVC built it with `ProblemDetailsFactory` + extension): CC1 pins type/title/detail/`available`/no `errors`/`traceId`/`correlationId`.
- **Serializer drift** (MVC vs HTTP JSON options) on enums, null `image`/`reason`, money strings: CC7 pins raw shape. If a mismatch appears, fix the host options (D6), never the DTOs or the test.
- **Merge order** (key vs body errors): CC5.
- **Group on `api/v1`** with `RequireAuthorization()`: safe only because it is built from `app` (D4); never reuse it for another module.
- **Existing, not fixed here** (backlog NOTEs added by Rob): view re-reads the just-written variant (2 extra round-trips per add/set/merge); `AddCartItemRequestValidator` `VariantId` `WithMessage` binds to `NotEqual` only; `SetCartItemRequestValidator` says "cannot be negative" for > 1000.
