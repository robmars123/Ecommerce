# Slice P2.73 — Admin order reads (list, detail) to Minimal API — plan (Rob)

Tracker row P2.73 (Orders). Master plan `slice-plans/minimal-api-migration-plan.md` (D1–D15). Tier **High** (auth policy, Support/Admin parity). First Orders conversion: adds `MapOrdersEndpoints`.

**Read:** master plan §2–§4 · this plan · `03-principles-hard-rules.md:40` · `06-backend-modules.md:63–69` · `Orders/Api/ReturnsController.cs:1–25, 135–169` · `Orders/Api/OrderRequests.cs:90–117` · `Admin/Api/AdminCustomersEndpoints.cs` · `Admin/Api/QueryValues.cs` · `Cart/Api/CartEndpoints.cs:17–27, 128–131` · `Cart/CartModule.cs:49–53` · `Ecommerce.Api/Composition/ModuleComposition.cs:30–31` · tests: `Orders/AdminOrderListApiTests.cs`, `Notifications/HubAndResetTests.cs:160–200`, `Orders/OrdersReadApiTests.cs` (P2_51 admin test only), `Refunds/RefundTests.cs:418–432`, `EndpointInventoryTests.cs:27–28`.
Do not read `OrderQueryService`, stores or `OrderResponses.cs` beyond the type names you call: this slice must not touch them.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| A1 | New `Orders/Api/AdminOrderReadsEndpoints.cs`: one group `app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdminOrSupport)`, `MapGet("orders", List)`, `MapGet("orders/{id:guid}", Get)`. Delete only the `AdminOrderReadsController` class (`ReturnsController.cs:135–169`); `ControllerValidation`, `ReturnsController`, `AdminReturnsController` stay (P2.74/P2.75) | D1, D4, D5; the file split rule in master §4 | Group on `api/v1/admin/orders` with `MapGet("")` (trailing slash, D4) |
| A2 | `OrdersModule.MapOrdersEndpoints(this IEndpointRouteBuilder app)` (public, returns `app`) calls `AdminOrderReadsEndpoints.Map(app)`; append `.MapOrdersEndpoints()` to the chain in `ModuleComposition.cs:31` | D1, D2 (first Orders slice) | Scanning |
| A3 | `List` binds every query key as `[FromQuery] string[]?` (`page`, `pageSize`, `status`, `from`, `to`, `customer`, `orderNumber`) and reads the **first** value; a blank or whitespace-only value is `null` | MVC's simple-type binder takes the first value and turns blank strings into null; a `string` parameter would join repeats as `"a,b"` (P2.64 pattern) | `[AsParameters]` record (join/blank semantics differ) |
| A4 | Numbers and dates parsed in the handler (D9) through a new Orders copy `Orders/Api/QueryValues.cs`: `ReadInt` copied from the Admin copy, plus `ReadDate` (`DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out …)`). Binding errors are returned **alone**, before the validator, with the **key and message captured from MVC in step 1** (expected: `Page`/`PageSize`/`From`/`To`, "The value 'x' is not valid for Page." — MVC binds a complex `[FromQuery]` record, so keys and messages differ from the customers copy). If the capture differs from the Admin copy's message, the Orders copy takes the field label as a parameter; say so in your report | MVC's `[ApiController]` filter returned 400 with binding errors only, before the action ran | Minimal binding (`int?`/`DateOnly?` parameters → bare 400 without `errors`) |
| A5 | Then build `AdminOrderListQuery` from the parsed values, run the injected `IValidator<AdminOrderListQuery>` unchanged, map errors with a private `ToErrors` (camelCase first letter, grouped, copy of `CartEndpoints.cs:128–131`) → `TypedResults.ValidationProblem`. Rest of the handler is the controller body verbatim: status parse, `AdminOrderFilter`, **one** `orders.ListForAdminAsync(...)`, `TypedResults.Ok(page.ToResponse())` | Same validator, same messages, same keys as `InvalidProblem` after `ModelState.Clear()` | A new validator; reshaping messages |
| A6 | `Get(Guid id, [FromServices] OrderQueryService orders, CancellationToken ct)` → `Results<Ok<AdminOrderDetailResponse>, ProblemHttpResult>`; null → `TypedResults.Problem(statusCode: 404, title: "Order not found")`; else `TypedResults.Ok(view.ToResponse())`. **No `ClaimsPrincipal`, no role branch** | Today both roles get the same body; P2.45 "Support never sees revenue" applies to KPIs/`totalSpent`, not per-order totals (09-api `/admin/orders` Admin+Support read; 13.2 rows "Admin customers API", "Admin search") | Adding a Support redaction (behaviour change, no row asks) |
| A7 | No `.Accepts`, no CORS call, no `.AllowAnonymous` | D15 (GET, no body), D7 host group gives `credentialed` | Per-route metadata |

No migrations, no Contracts, no packages, no change under `Application/`, `Infrastructure/`, `Domain/` or `OrderResponses.cs`. Existing tests unedited.

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/AdminOrderReadsEndpoints.cs` | new (A1, A3–A7) |
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/QueryValues.cs` | new (A4) |
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/ReturnsController.cs` | delete lines 135–169 only; drop a `using` only if the build says it is unused |
| `backend/src/Modules/Orders/Ecommerce.Orders/OrdersModule.cs` | add `MapOrdersEndpoints` (+ `using Microsoft.AspNetCore.Routing`) |
| `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` | `.MapOrdersEndpoints()` appended |
| `backend/tests/Ecommerce.Api.IntegrationTests/Orders/AdminOrderReadsContractTests.cs` | new step-1 pins (§4) |

## 3. Build order

1. Baseline on MVC: run `AdminOrderListApiTests`, `HubAndResetTests`, `OrdersReadApiTests`, `RefundTests`, `EndpointInventoryTests` (`--filter`), quote counts.
2. Write the §4 pins, run them **on MVC**, record the exact captured keys/messages in the asserts, quote green. Any pin that cannot go green on MVC: stop and report, do not adapt the code to a guess.
3. Convert (A1–A7) and delete the controller class in the same change.
4. All of step 1 + pins green unedited; full `bash scripts/verify.sh`; mutations §5.

## 4. Required tests (step-1 pins, all green on MVC first)

Use the existing `OrdersEnv`/`OrderSeed`/`RefundEnv` helpers; seed with `OrderSeed.InsertAsync`.

| # | Request (Admin token unless stated) | Assert |
|---|---|---|
| P1 | `?page=abc`, `?pageSize=1.5`, `?from=2026-13-01`, `?to=x` (theory) | 400, `application/problem+json`, exactly one `errors` key with the captured name and message |
| P2 | `?page=abc&pageSize=0&status=paid` | 400 with only the binding key (validator errors absent) |
| P3 | `?status=&customer=&orderNumber=&page=&pageSize=&from=&to=` and `?status=%20&customer=%20%20` | 200, every seeded order, `page` 1, `pageSize` 20 |
| P4 | `?status=Paid&status=Shipped`; `?customer=a%40example.test&customer=b%40example.test`; `?page=1&page=abc` | first value wins (Paid only; A's orders only; 200) — or whatever MVC returns, captured |
| P5 | `?status=paid&pageSize=0` | 400; `type`, `title`, `traceId`, `correlationId` present; `errors.status` and `errors.pageSize` (camelCase) with the validator's messages |
| P6 | `?pageSize=100`; `?from=2026-06-10&to=2026-06-10` | 200 (upper bound and same-day range accepted) |
| P7 | `GET admin/orders/{unknown}` | 404, `application/problem+json`, `title` = "Order not found" |
| P8 | Paid order with a refund: list and detail as Admin and as Support | Support body text == Admin body text (both routes) — proves no role branch was added or lost |
| P9 | Cancelled order with history (P2.50/P2.53) and an item variant (P2.51), with payment (P2.52): `GET admin/orders/{id}` as Admin vs `GET orders/{id}` as owner | admin `order` JSON == customer body JSON; `order.statusNote` non-null; every `timeline[].description` non-empty; `order.payment` present. Do **not** seed an admin override reason (see N6) |

Existing tests that already cover 401/403/Support-200, status/date/customer/orderNumber filters, paging 400s, empty page, P2.51, P2.52: unchanged.

## 5. Mutations (High) — break → red → restore from scratch copy → `cmp`

| M | Break | Expected red |
|---|---|---|
| M1 | Group policy `AdminOrSupport` → `Admin` | `EndpointInventoryTests` (rows 27–28) and `HubAndResetTests.The_admin_detail_is_open_to_Admin_and_Support_only…` |
| M2 | Remove `.RequireAuthorization(...)` from the group | `EndpointInventoryTests` and `AdminOrderListApiTests.Anonymous_is_401_customer_is_403…` |

## 6. What the reviewer checks (name for name)

- **Queries per request — unchanged, because no query code changes.** `git diff --stat` must show no file under `Orders/Application`, `Orders/Infrastructure`, `*.Contracts`, `Payments`, `Identity`, `Catalog`. Baseline from the code (read by Rob): **list** = count + page + contacts = 3 (+1 `FindIdByEmailAsync` when `customer` is set; an unknown email stops at 1); **detail** = order projection 1 + latest payment 1 + refund summary 2× (1–2 each) + variant snapshots 2 (0 with no items) + returns 3 ≈ 9–11; unknown id = 1.
- **Per-row lookups:** the handler awaits exactly one service method per route (`ListForAdminAsync` / `GetForAdminAsync`), no `foreach`/`Select` that calls a service, no second injected service besides the validator.
- **Projection:** response mapping stays `page.ToResponse()` / `view.ToResponse()`; no new fields, no re-shaping, no `AdminOrderDetailView` fields read in the handler.
- **Paging limits:** the validator's `DefaultPageSize` 20 / `MaxPageSize` 100 used unchanged; P3, P6, existing out-of-range theory.
- **Contract-only cross-module calls:** the new file references only `Ecommerce.Orders.*` and `Ecommerce.Identity.Contracts` (policy names); `ArchitectureTests` green.
- Policy/route/CORS via inventory, unedited. 404/400 shapes via P5/P7. Support parity via P8. Detail fields via P9 + existing P2.51/P2.52.

## 7. Backlog NOTEs (Rob adds to `backlog.md` with this plan; not fixed here)

- N1 duplicate round-trip: `GetForAdminAsync` calls `refunds.GetSummaryAsync(orderId)` again after `BuildAsync` already did (`OrderQueryService.cs:73` and `:83`) — 1–2 duplicate Payments queries per detail.
- N2 over-fetch: admin list materialises full `Order` entities (address, all money columns) for ≤ 100 rows to build 7-field `RecentOrder` (`OrderStores.cs:112–116`); project in SQL.
- N3 cartesian: order detail projects items × history × shipments in one statement (`OrderStores.cs:66–82`); `AsSplitQuery()` or separate projections.
- N4 returns rows for one order re-read the order number and all item names in 2 extra queries, even with zero returns (`OrderStores.cs:196–202`); the per-item name lookup is an in-memory `FirstOrDefault` per line (not SQL N+1).
- N5 list contacts load full `User` rows (password hash included) for ≤ 100 ids (`Identity/Infrastructure/Repositories.cs:47–48`); same fix as the P2.64 note.
- N6 timeline `reason`: **customer-visible** — `GET /api/v1/orders/{id}` maps `t.Reason` raw (`OrderResponses.cs:95`), the same mapping the admin detail embeds. Unchanged by P2.73; existing item (backlog P2.53 line) still open.

## 8. Risks

- Captured MVC keys are PascalCase (`Page`) for binding errors but camelCase for validator errors — reproduce both exactly; P1/P5 pin each.
- `DateOnly` parsing differences (formats, whitespace) between `DateOnlyConverter` and `TryParse`: P1/P6 pin the cases; if another format matters, capture it on MVC first.
- Leaving `AdminOrderReadsController` mapped too → `AmbiguousMatchException`: delete in the same change.
- `MapOrdersEndpoints` missing from the composition chain → all admin order GETs 404: inventory red.
