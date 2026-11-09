# Slice 5 gap-fill — Admin order list, filter screen, action buttons

Deliverables (app-context §3.1): **11.2** admin order list API with filter · **11.4** list screen with filter · **11.5** action buttons on the admin detail screen. Builder: **Marco**. Mode: Autonomous.

**Read:** app-context §4.11 and the §5 route table rows `/admin/orders*` · arch §3.4 (rules 7, 8, 9, 11) · §6.2 · §7.2 (orders indexes) · §7.3 Order row · §8.6 · §9 (header paging rule, the `GET /admin/orders` row, Catalog read contract paging bullet) · §11.3 · §13.2 rows "Admin order list and actions (Slice 5 gap-fill)" and "Admin order status override (Slice 5)" (grep, don't read the log) · quality-bar §1, §2, §4.
Code to read before editing: `Orders/Api/AdminOrdersController.cs`, `Orders/Api/ReturnsController.cs` (`AdminReturnsController.List` paging pattern, `AdminOrderDetailController`), `Orders/Application/OrderQueryService.cs`, `Orders/Application/OrderPorts.cs`, `Orders/Infrastructure/OrderStores.cs` (`OrderReadStore`), `Identity.Contracts/IUserReader.cs`, `Identity/Application/UserReader.cs`, `Identity/Application/Ports.cs`; frontend `features/admin-orders/*`, `features/auth/hooks/useSession.ts`, `lib/query-keys.ts`, `app/admin/orders/*`.

## Risk tier

- **11.2 API — High (auth only).** It exposes every customer's orders; the role policy is a rule-11 guard. Full review + **one mutation** (M1). No money, stock or transition moves.
- **11.4 / 11.5 frontend — Low.** Buttons call existing, already-tested 11.1 endpoints (tests 29/30 and the 401/403/404/400 suite in `OrdersFulfillmentApiTests`). No mutations.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | Placement: **Slice 5 gap-fill**, files `slice-5-gapfill-plan.md` / `-marco-report.md` / `-rob-review.md`. Set the app-context §3.1 "Slice" cell of 11.2/11.4/11.5 to `5` (Status stays for the coordinator after PASS) | §12.3 row 5 owns admin process/ship/deliver/overrides; the list and screens are its missing UI/read half | A new "5b" slice (implies a new §12.3 row and re-ordering) |
| D2 | Contract = §13.2 row "Admin order list and actions": filters `status`, `from`, `to`, `customer`, `orderNumber`, paging, newest first | One source of truth; §9 row points to it | Copying the contract into 09-api.md (file is over 10 KB) |
| D3 | `customer` = exact email → `IUserReader.FindIdByEmailAsync` (new contract method, consumer: Orders) → filter by `user_id`. Unknown email → 200 empty, `totalCount` 0, no Orders query | Rule 8: two reads, no cross-module join; uses `orders(user_id, created_at DESC)` | Partial/name search (needs an Identity search API — 12.1 territory); `customerId` GUID param (admins don't know ids) |
| D4 | `from`/`to` bind as `DateOnly` (`yyyy-MM-dd`), UTC days, inclusive; `from > to` → 400; malformed → 400 (model binding) | Matches dashboard UTC days (6b D10); no time-zone ambiguity | `DateTimeOffset` params (server-local offset on a bare date) |
| D5 | Validation via `AdminOrderListQuery` record (`[FromQuery]`) + FluentValidation `AdminOrderListQueryValidator`: `page` ≥ 1 (default 1), `pageSize` 1–100 (default 20), `status` exact `OrderStatus` name, `customer` trimmed 1–254, `orderNumber` trimmed 1–32, `from ≤ to`. Errors keyed by camelCase param name | Unit-testable 400 rules; same pattern as Ship/Override validators | Hand-rolled `ModelState` checks for six params in the controller |
| D6 | One store query: new `IOrderReadStore.ListForAdminAsync(AdminOrderFilter, page, pageSize)`; existing `ListRecentAsync` becomes a call to it with an empty filter (dashboard unchanged) | No second copy of the newest-first paging query | A separate parallel query (duplicate code) |
| D7 | Endpoint in the existing `AdminOrderDetailController`: route becomes `api/v1/admin/orders`, `Get` gets `[HttpGet("{id:guid}")]`, new `[HttpGet("")] List`. Rename the class to `AdminOrderReadsController` | Same policy (`AdminOrSupport`), same service; one read controller | A third admin-orders controller |
| D8 | Response `{ items: [{ id, orderNumber, customerEmail, createdAt, status, grandTotal: Money }], page, pageSize, totalCount, totalPages }`; emails via existing `IUserReader.GetContactsAsync` for the page's user ids; missing user → `customerEmail: null` | Admin needs to see who ordered; reuses the existing batch read | Returning `userId` only |
| D9 | Buttons per server status (UI only — the server is the guard): `Paid` → Process, Override→Processing · `Processing` → Ship (carrier code + tracking 1–64) · `Shipped` → Deliver, Override→Delivered · other statuses → none. Override needs a reason 1–500. Hidden unless `session.user.role === 'Admin'` (Support reads only) | §8.6, override edges per §13.2 "Admin order status override"; rule 11 is enforced by the API's Admin policy | Showing all buttons and relying on 409/403 |
| D10 | No optimistic status: after any action settles, invalidate `admin.orders.detail(id)` and `admin.orders.all`; show the server's 409 `detail` / 400 field messages via `userMessage` | Same as F13 discipline; a second click after success gets the server's 409 | Locally setting the new status |
| D11 | No `Idempotency-Key` on action calls | The transition itself is the idempotency guard (repeat → 409 illegal transition, test 30 parallel ship); §9 names no key for these | Adding keys (not in contract) |
| D12 | Ship carrier = free-text code input, server 400 shown on the field | No carriers endpoint exists in §9; backlog item | New `GET` carriers endpoint (unplanned API) |
| D13 | List screen reads the `status` route param as its initial filter (dashboard already links `/admin/orders?status=X`) | Existing dashboard link must land filtered | Ignoring the param |

No packages, no migration (indexes in §7.2 already cover every filter), no new module reference (Orders already references `Identity.Contracts`).

## 2. Files and contracts

### Backend
- `Identity.Contracts/IUserReader.cs` — add `Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct);` (doc comment: exact match, Identity normalisation, null if none).
- `Identity/Application/Ports.cs` + its repository implementation — add a **read-only** `FindIdByEmailAsync(string normalizedEmail, ct)` (`AsNoTracking`, projects `Id`; never the `ForUpdate` variant).
- `Identity/Application/UserReader.cs` — implement via `EmailAddress.Normalize(email)` (the same as `LoginService`).
- `Orders/Application/OrderPorts.cs` — `internal sealed record AdminOrderFilter(OrderStatus? Status, DateOnly? From, DateOnly? To, Guid? UserId, string? OrderNumber);` + `ListForAdminAsync`.
- `Orders/Infrastructure/OrderStores.cs` — `ListForAdminAsync`: `AsNoTracking`, apply each non-null filter (`From` → `created_at >= From 00:00Z`; `To` → `< To+1 00:00Z`, as `DateTimeOffset` with `TimeSpan.Zero`), count, `OrderByDescending(CreatedAt).ThenByDescending(Id)`, skip/take, return `RecentOrderPage` rows. `ListRecentAsync` delegates with an all-null filter.
- `Orders/Application/OrderQueryService.cs` — inject `IUserReader`; `ListForAdminAsync(AdminOrderListQuery-derived args)`: resolve `customer` (null id → empty page without querying orders), query, batch `GetContactsAsync`, build `AdminOrderListPage`. Views in `OrderViews.cs`.
- `Orders/Api/OrderRequests.cs` — `AdminOrderListQuery(int? Page, int? PageSize, string? Status, DateOnly? From, DateOnly? To, string? Customer, string? OrderNumber)` + validator; register like the others in `OrdersModule.cs` (check how validators are registered — follow it, no new mechanism).
- `Orders/Api/OrderResponses.cs` — `AdminOrderListItemResponse`, `AdminOrderListResponse` (named records, `MoneyResponse.From`).
- `Orders/Api/ReturnsController.cs` — D7 route change + `List` action (thin: validate → service → map).
- No `var`; `IClock` not needed (no "now" in this query).

### Frontend (`frontend/apps/store`)
- `features/admin-orders/types.ts` — `AdminOrderListItem`, `AdminOrderListPage`, `AdminOrderFilters { status: OrderStatus | 'all'; from: string; to: string; customer: string; orderNumber: string }`, `AdminOrderAction` union: `{ kind: 'process' } | { kind: 'ship'; carrierCode; trackingNumber } | { kind: 'deliver' } | { kind: 'override'; toStatus: 'Processing' | 'Delivered'; reason }`. Reuse `OrderStatus` from `features/orders/types`.
- `features/admin-orders/api.ts` — `ADMIN_ORDER_PAGE_SIZE = 20`; `fetchAdminOrders(filters, page, signal)` (empty strings and `'all'` omitted from params, values trimmed); `runOrderAction(orderId, action)` → `POST .../process|ship|deliver|status-override` with the §9 bodies.
- `features/admin-orders/order-actions.ts` (pure) — `actionsFor(status)` per D9; `validateFilters(filters)` → field errors (`yyyy-MM-dd` real date, `from ≤ to`); tracking/reason length checks. Server stays authoritative.
- `features/admin-orders/hooks/hooks.ts` — `useAdminOrdersQuery(filters, page)` (`keepPreviousData`); `useOrderActionMutation(orderId)` (D10).
- `lib/query-keys.ts` — `admin.orders.all`, `admin.orders.list(filters, page)` (normalised like `admin.products.list`); keep `detail`.
- `features/admin-orders/components/AdminOrderListView.tsx` — filters (status chips incl. All, from, to, customer email, order number), Apply (resets page to 1), rows (number, date, customer email, status label, total via `formatMoney`) linking to `/admin/orders/[id]`, pager, empty state, loading, error with retry. Prop `initialStatus?: string` (unknown value → `'all'`).
- `features/admin-orders/components/OrderActions.tsx` — D9/D10/D12; buttons disabled while pending; ship and override as inline forms.
- `features/admin-orders/components/AdminOrderDetailView.tsx` — render `<OrderActions detail={detail} />` above `RefundForm`.
- `app/admin/orders/index.tsx` — replace the stub; pass `useLocalSearchParams().status`.
- Styling per arch §5.5 (NativeWind tokens as in the existing views); no `Platform` checks.

### Docs (Rob, done)
- `docs/architecture/13.2-decision-log.md` — row "Admin order list and actions (Slice 5 gap-fill)".
- `docs/architecture/09-api.md` — `GET /admin/orders` row points to that row.
- Marco: app-context §3.1 Slice cell → `5` for 11.2, 11.4, 11.5 (D1). Nothing else in docs.

## 3. Build order

1. **Auth tests first (red):** `AdminOrderListApiTests` 401/403/Support-200 against the not-yet-existing route — quote the red output (404/405 counts as red).
2. Validator + its unit tests (red → green).
3. Identity `FindIdByEmailAsync` (contract → port → repo → reader).
4. Store query + `ListRecentAsync` delegation; run the existing dashboard recent-orders tests (Admin tests + integration) — must stay green unchanged.
5. Service, responses, controller route change + `List`. Re-run existing `AdminOrderDetail`/refund integration tests (route change must not break `GET /admin/orders/{id}`).
6. Frontend: types/api/api tests → `order-actions.ts` + tests → hooks/query keys → `OrderActions` + tests → `AdminOrderListView` + tests → route.
7. **M1**, then full `bash scripts/verify.sh`.

## 4. Required tests

No §11.2 numbered or F test is named for these rows (§12.3); these are the CLAUDE.md §3 endpoint + Jest requirements. **TF** = test-first.

### Integration — `backend/tests/Ecommerce.Api.IntegrationTests/Orders/AdminOrderListApiTests.cs` (own DB per test via `PostgresFixture`; extend `OrderSeed.InsertAsync` with optional `createdAt`/`orderNumber`/`status` only if it lacks them)
1. **TF** anonymous → 401; Customer → 403 Problem Details and no body items; Support → 200; Admin → 200.
2. Happy path, no filter: all seeded orders of two customers, `created_at DESC, id DESC` (include two orders with equal `created_at` to pin the tie-breaker), `customerEmail` correct, `grandTotal` `{ amount: "x.xx", currency }`, `totalCount`/`totalPages` right.
3. `status=Paid` returns only Paid; `status=paid` and `status=Bogus` → 400 with `status` key.
4. `from`/`to` inclusive UTC days: orders at `to` 23:59:59Z included, at `to+1` 00:00:00Z excluded, at `from` 00:00:00Z included; `from > to` → 400; `from=2026-13-01` → 400.
5. `customer` with different case/whitespace finds the customer's orders only; unknown email → 200, empty, `totalCount` 0.
6. `orderNumber` exact → one order; unknown → empty.
7. Combined `status` + `customer` → AND.
8. Paging: `page=0`, `pageSize=0`, `pageSize=101` → 400; past the last page → 200 empty with the real `totalCount`.
9. `customerEmail` null when the order's user id has no `users` row (seed an order with a random user id).
10. Regression: existing dashboard recent-orders tests and admin detail/refund tests unchanged and green.

### Unit — `backend/tests/Ecommerce.Orders.Tests` (beside the existing validator tests)
- `AdminOrderListQueryValidator`: each bound in D5 at edge (pageSize 100 ok / 101 not; orderNumber 32/33; customer 254/255; from = to ok).

### Jest (`features/admin-orders`)
- `api.test.ts`: list params omit empty/`'all'`, trimmed; each action hits the right path, method and body.
- `order-actions.test.ts`: `actionsFor` for every `OrderStatus`; `validateFilters` (bad date, Feb 30, from > to, empty ok).
- `OrderActions.test.tsx`: Admin sees D9 buttons per status; Support sees none; ship form blocks empty carrier/tracking and 65 chars; override blocks empty reason; button disabled while pending; a 409 shows the server's detail; after success the status shown is the refetched server status (no optimistic change).
- `AdminOrderListView.test.tsx`: renders rows and links; Apply sends the filters and resets to page 1; invalid date shows a field error and sends nothing; `initialStatus="Paid"` filters on first fetch; empty, error-with-retry and pager states.
- `AdminOrderDetailView.test.tsx`: existing tests stay green; add one that actions render for Admin.

### Mutation (Rob re-runs it in review)
- **M1:** on the `List` action/controller, replace the `AdminOrSupport` policy with plain `[Authorize]` → test 1 (Customer 403) must go red. Restore from a scratch copy, confirm with `cmp`, quote the red output.

## 5. Risks

- **Route change (D7)** could break `GET /admin/orders/{id}` or the anonymous-endpoint pin test — step 5 re-runs them; the ArchitectureTests must pass **unchanged** (any edit there is blocking).
- **Data exposure:** the list must never fall back to the customer-scoped `[Authorize]`; M1 proves the guard.
- **Email lookup:** using `FindByEmailForUpdateAsync` would take a row lock on a read path — use the new read-only method.
- **Date bounds:** compute `To+1` as a date before converting to `DateTimeOffset` at UTC; no server-local time.
- **Dashboard regression** from D6 delegation — covered by existing tests.
- **Scope creep:** no unfulfillable button, no carrier endpoint, no customer search (backlog). Unplanned code is blocking.
