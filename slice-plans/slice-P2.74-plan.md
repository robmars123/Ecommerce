# Slice P2.74 — Customer return request to Minimal API (plan, Rob)

Tier **High** (state transition on the caller's own order, ownership by `sub`, double-submit guard). Mode Autonomous. Row: tracker `P2.74 | Customer return request endpoint to Minimal API | Orders`.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§4 (incl. D15) · this plan · `docs/architecture/03-principles-hard-rules.md:40` · `docs/architecture/06-backend-modules.md:63-69` · `backend/src/Modules/Orders/Ecommerce.Orders/Api/ReturnsController.cs:1-64` · `.../Api/OrderRequests.cs:45-69` · `.../Application/ReturnService.cs:11-25,46-103` (read only) · `.../OrdersModule.cs:78-82` · pattern: `backend/src/Modules/Cart/Ecommerce.Cart/Api/CartEndpoints.cs` (whole) · tests: `backend/tests/Ecommerce.Api.IntegrationTests/Refunds/ReturnTests.cs`, `.../Refunds/RefundEnv.cs:26-60,115-116`, `.../Orders/AdminOrderReadsContractTests.cs:36-40` (`AssertProblem`), `.../EndpointInventoryTests.cs:69`.

Do **not** touch `ReturnService`, `ReturnStore`/`OrderStores.cs`, Domain, Persistence, validators or DI. `AdminReturnsController` and `ControllerValidation` stay in `ReturnsController.cs` until P2.75.

## 1. Scope facts (checked by Rob)

- One route: `POST api/v1/orders/{id:guid}/return-request`, class `[Authorize]` (no policy), no CORS policy. Inventory row `:69` = `(default) | auth | -`. Host prefix convention (`Program.cs:140-144`) does not match `api/v1/orders`, so no CORS code; D15 `*/*` comes from the host group (Orders is already in `MapModuleEndpoints`, P2.73).
- **Order of checks (MVC, keep):** binding → `sub` TryParse (else 401) → FluentValidation (400) → one `ReturnService.RequestAsync` → map. So a bad body on someone else's order is **400, not 403** (validation runs before the service). Service order: not found 404 → not owner **403** → not `Delivered` 409 → duplicate/foreign/over-quantity line 400 (`items`) → already open 409 → domain reason 400 (`reason`) → insert, one `SaveChangesAsync`; unique-index race → 409.
- **Ownership:** the owner is the token's `sub`; the service compares `order.UserId`. Another customer's order is **403** `Forbidden` (not 404); an unknown order is 404 `Order not found`.
- **Eligibility:** only `Delivered` orders. **There is no return window** in code or docs (`ecommerce-app-context.md:38`, tracker 7.1): the admin decides on decline. Do not add one (behaviour change). `IClock` only stamps `created_at` and the v7 id.
- **Quantity over what was purchased:** service `Invalid("items", "Return 1 to {line.Quantity} of {line.Name}.")` → 400 keyed `items`.
- **Idempotency:** no `Idempotency-Key` on this route (none in MVC). The double-submit guard is the partial unique index `ux_return_requests_open_order_id` → `AlreadyOpen` 409 (existing `A_double_submit_creates_one_request`).
- **Side effects:** none. No outbox message, no money, no audit row on request (audit is on approve/decline, P2.75). Nothing moves to the handler.
- **Queries per request today** (unchanged here): happy = 1 order detail read (`OrderStores.cs:66`, order + items + history + shipments) + 1 open-return read + 1 `INSERT` in **one** `SaveChangesAsync`. 403/404/409-not-delivered/400-items stop after query 1. No N+1 (item check is in memory). Over-fetch noted in backlog, not fixed.

## 2. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| R1 | New `Api/ReturnsEndpoints.cs`, `internal static class ReturnsEndpoints`, `Map(app)`: `app.MapPost("api/v1/orders/{id:guid}/return-request", RequestReturn).RequireAuthorization();` — no group | D1; D4 allows a single route without a group; D5 bare `[Authorize]` → `RequireAuthorization()`. RawText stays the inventory row | `MapGroup("api/v1/orders")` (P2.77 maps its own group in its own file; two groups on one prefix is harder to read) |
| R2 | `OrdersModule.MapOrdersEndpoints` adds `ReturnsEndpoints.Map(app);` after `AdminOrderReadsEndpoints.Map(app);` | D2; Orders already composed | — |
| R3 | Handler `private static async Task<Results<Created<ReturnCreatedResponse>, UnauthorizedHttpResult, ValidationProblem, ProblemHttpResult>> RequestReturn(Guid id, [FromBody] ReturnRequestBody body, ClaimsPrincipal user, [FromServices] IValidator<ReturnRequestBody> validator, [FromServices] ReturnService returns, CancellationToken ct)` | D3; one service call | Injecting a store |
| R4 | Keep MVC's `if (!Guid.TryParse(user.FindFirst("sub")?.Value, out Guid userId)) return TypedResults.Unauthorized();` | MVC had this branch; same line as `AuthEndpoints.Me` (`Identity/Api/AuthEndpoints.cs:189`); `UseStatusCodePages` writes the problem body as before. Unreachable with our tokens, so no new test | `Guid.Parse` like Cart (Cart's MVC never had the branch; here it would turn 401 into 500) |
| R5 | Validate → `TypedResults.ValidationProblem(ToErrors(validation))`; `ToErrors` copied verbatim from `CartEndpoints.cs:128-131` | Same keys as `ControllerValidation.InvalidProblem` (first char lowered, grouped), e.g. `items`, `reason`, `items[0].Quantity` | Reusing `ControllerValidation` (MVC-only) |
| R6 | Build `List<ReturnItem>` exactly as the controller (`VariantId!.Value`, `Quantity!.Value`), pass `body.Reason!` untrimmed; `ReturnRequestResult result = await returns.RequestAsync(userId, id, items, body.Reason!, ct);` then `return result switch { … }`: `Created c` → `TypedResults.Created($"/api/v1/orders/{id}", new ReturnCreatedResponse(c.ReturnId))`; `Forbidden` → `Problem(statusCode: 403, title: "Forbidden")`; `NotDelivered` → `Problem(statusCode: 409, title: "Only a delivered order can be returned", type: "not-delivered")`; `AlreadyOpen` → `Problem(statusCode: 409, title: "This order already has an open return request", type: "return-already-open")`; `Invalid i` → `ValidationProblem(new Dictionary<string, string[]> { [i.Field] = [i.Message] })`; `_` → `Problem(statusCode: 404, title: "Order not found")` | Arm for arm the controller's switch, same titles/types, same `default` → 404 | Changing trim, adding `detail`, or making `NotFound` explicit with a throwing default |
| R7 | Move `ReturnCreatedResponse` (unchanged) from `ReturnsController.cs:12` to `Api/OrderResponses.cs` | Its only consumer leaves the file; P2.75 then deletes the file without moving records | Leaving it in a file scheduled for deletion |
| R8 | Accepted difference (13.2 row "Minimal API migration"; P2.72 C8): unreadable body (malformed, `null`, wrong type, unknown member such as `status`) → bare 400 problem; title/`errors` may differ. `{}` parses on both stacks (all members nullable) so its `errors` are pinned exactly | Already accepted | Hand-parsing the body |
| R9 | No change to services, stores, validators, DI, Contracts, migrations, packages; no `.Accepts` (D15) | §2.2 | — |

## 3. Files

| File | Change |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/ReturnsEndpoints.cs` | new (R1, R3–R6) |
| `.../Api/ReturnsController.cs` | delete class `ReturnsController` (lines 27–64) and the record (R7); drop now-unused usings only |
| `.../Api/OrderResponses.cs` | add `ReturnCreatedResponse` (R7) |
| `.../OrdersModule.cs` | R2 |
| `backend/tests/Ecommerce.Api.IntegrationTests/Refunds/ReturnRequestContractTests.cs` | new, step 2, green on MVC first |

## 4. Build order

1. **Baseline on MVC:** `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~ReturnTests|FullyQualifiedName~EndpointInventoryTests|FullyQualifiedName~ContentTypeContractTests|FullyQualifiedName~AdminOrderDetailTests" 2>&1 | tail -n 20`; quote counts.
2. **Pins first:** write `ReturnRequestContractTests.cs` (§5) against the controller; capture every title/message from the real MVC response, then assert it; green on MVC, quote the run. Use `RefundEnv` (`SeedOrderAsync`, `VariantOfAsync`, `TokenA/B`, `F.ScalarAsync`, `F.CountAsync`, `Env.Clock`), a local `AssertProblem(r, status)` (problem+json, `type`, non-empty `traceId`, `correlationId` = `id-test-1`) and `Keys(errors)`.
3. **Convert** (§3) in one change.
4. Step-1 filter + the new file: all green; `git diff --stat backend/tests` shows only the new file.
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote `VERIFY`.
6. Mutations (§6).

## 5. Required tests — `ReturnRequestContractTests.cs` (new, green on MVC)

Every rejecting case also asserts `SELECT count(*) FROM return_requests` = 0 (or unchanged) and the order status unchanged.

| # | Request | Expected |
|---|---|---|
| RR1 **ownership** | A's Delivered order: B posts a valid body; A posts to `Guid.NewGuid()`; anonymous posts | B → 403, type `…section-15.5.4`, title `Forbidden`, no `errors`; unknown → 404, type `…15.5.5`, title `Order not found`; anonymous → 401 problem+json; 0 rows |
| RR2 **check order** | B posts `{}` to A's order | 400 with `errors` keys `{items, reason}` (validation runs before ownership, as MVC) |
| RR3 **eligibility 409** | order `Shipped`, valid body; then a Delivered order with an open request, second valid post | 409, type exactly `not-delivered`, title `Only a delivered order can be returned`; 409, type exactly `return-already-open`, title `This order already has an open return request`; neither has `errors` or `detail`; row count 0 / 1 |
| RR4 validator 400 | `{}`; `{"items":[{}],"reason":"x"}`; `{"items":[{"variantId":v,"quantity":0}],"reason":"   "}` | type `…15.5.1`, title `One or more validation errors occurred.`; `errors` keys exactly `{items, reason}` / the captured child keys (expect `items[0].VariantId`, `items[0].Quantity`) / `{items[0].Quantity, reason}`, messages captured from MVC |
| RR5 **eligibility 400 (service)** | quantity 2 of a 1-unit line; a variant not in the order; the same variant twice | `errors` exactly `{items: ["Return 1 to 1 of <captured name>."]}` / `{items: ["Only items from this order can be returned."]}` / `{items: ["List each item once."]}` |
| RR6 **happy shape** | A posts a valid body with the env clock at a known instant | 201; `Location` exactly `/api/v1/orders/{order}`; body property names exactly `{returnId}`; row `status` `Requested`, `created_at` = `Env.Clock.UtcNow` (IClock), `reason` as sent |
| RR7 unreadable | malformed `{"items":`, literal `null`, `{"items":"x","reason":"y"}` | each 400 problem (R8); 0 rows; a valid post afterwards → 201 |

Existing coverage that must stay green **unedited**: all of `ReturnTests` (T7 403/401/404, not-Delivered 409 × 4, bad bodies incl. `status` member, second-open 409, double submit), `RefundTests:239,260`, `AdminOrderDetailTests` (`HubAndResetTests.cs:194`), inventory row `:69`, `ContentTypeContractTests` (CT4 now covers this route).

## 6. Mutations (one per guard; break → red (quote) → restore from scratch copy → `cmp`)

| # | Break | Red |
|---|---|---|
| M1 **ownership** | `ReturnService.cs:53`: remove the `order.UserId != userId` check | `T7_another_customers_order_is_403…` + RR1 |
| M2 **ownership mapping** | `ReturnsEndpoints`: `Forbidden` arm → 404 `Order not found` | RR1 (and T7) |
| M3 **eligibility mapping** | `NotDelivered` arm → `type: "return-already-open"` | RR3 |
| M4 **eligibility mapping** | `Invalid` arm keyed `""` instead of `i.Field` | RR5 |
| M5 **auth** | drop `.RequireAuthorization()` | inventory row `:69` (401 stays green through the fallback policy, `IdentityModule.cs:146`; inventory is the proof) |
| M6 **validation** | skip the validator return | RR4 / `Bad_items_quantities_or_reasons_are_400` (`{}` → 500) |

## 7. Reviewer checklist (Rob, review round)

- **One service call**, no `DbContext`, store, `SaveChanges`, `IClock` or loop over orders in `ReturnsEndpoints.cs`; `git diff --stat` shows nothing under `Application/`, `Infrastructure/`, `Domain/`. One `SaveChangesAsync` stays in `ReturnService.RequestAsync`.
- **Queries per request unchanged** (§1: 2 reads + 1 insert on happy path); no new projection or lookup.
- **No logic in the handler** beyond sub → validate → map body to `ReturnItem` → one call → switch.
- Owner comes only from `sub`; nothing from the body or route is used as the user.
- Titles, `type` values and status per arm match the deleted controller exactly; `_` → 404.
- No `.Accepts`, no CORS code, no group; `ReturnCreatedResponse` moved unchanged; no dead helper; `AdminReturnsController` untouched.
- Existing tests unedited; only `ReturnRequestContractTests.cs` added.

## 8. Risks

- **409 `type` drift**: MVC kept the custom `type`; `TypedResults.Problem(type:)` must too. RR3 pins it exactly.
- **Check order**: moving validation after the service would turn RR2 into 403. RR2 pins it.
- **Location / created shape**: RR6.
- **Existing, not fixed here** (backlog NOTEs added by Rob): `OrderStores.GetAsync` loads totals, address, history and shipments just to check owner, status and lines; `ReturnStore.GetOpenAsync` is tracked though only existence is used; no server return window (product decision, not a bug).
