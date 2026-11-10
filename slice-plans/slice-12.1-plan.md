**Read:** app-context §2 (Support row), §4.12 · arch §3.4 · §6.1 (Admin and Identity rows) · §7.1 rows `users`, `orders`, `order_status_history`, `refunds`, `return_requests` · §7.4 `Store:Currency` · §9 table row `GET /admin/customers` + the "Catalog read contract" paging bullets (09-api.md lines 32–38) · §10.1 · §13.2 rows "Admin customers API" and "Slice 6b projections and saga" (grep, don't read the log) · quality-bar §1, §2, §4.
Code to read before editing: `Modules/Admin/Ecommerce.Admin/{AdminModule.cs, Api/AdminDashboardController.cs, Application/DashboardQueryService.cs}` · `Identity.Contracts/IUserReader.cs`, `Identity/Application/{UserReader.cs, Ports.cs}`, `Identity/Infrastructure/Repositories.cs`, `Identity/Domain/User.cs` · `Orders.Contracts/IOrderReader.cs`, `Orders/Application/OrderReader.cs` + the `IOrderReadStore` / `IReturnStore` ports and their EF implementations · `Payments.Contracts/IRefundState.cs`, `Payments/Application/RefundState.cs`, the `IPaymentStore` port and implementation · tests: `Api.IntegrationTests/Orders/{AdminOrderListApiTests.cs, OrderSeed.cs}`, `Projections/AdminDashboardApiTests.cs` (Support pattern), `Identity/IdentityTestEnv.cs`.

All backend paths are under `backend/src/` or `backend/tests/`.

# Slice 12.1 — Customer list and detail API (read-only)

Deliverable: app-context **12.1**. Builder: **Marco**. Mode: Autonomous. Out of scope: the screens (12.2), any write (edit, ban, impersonate: §4.12 says read-only), a migration, a new package, a projection.

**Precondition:** slice 10.4 is still uncommitted in the working tree. Marco starts only after Nathaniel commits 10.4 and its files move to `slice-plans/archive/` (quality-bar §1: no work from two slices at once).

## Risk tier — High

The slice adds no writes, but it adds a **role rule**: Support must not see the revenue figure (§9 row "Support sees no revenue KPIs"), plus a **money read**: total spent is a server-computed sum (rule 1). Full review with 2 mutations (§4).

## 1. Decisions (Rob records them as one §13.2 row, "Admin customers API")

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | The endpoints live in the **Admin** module (`AdminCustomersController` + `CustomerQueryService`). Identity owns the user data and exposes it through `IUserReader`; Orders and Payments expose per-customer reads through their existing contracts | §6.1: Admin "reads via other modules' contracts" (dashboard precedent). Admin already references Identity, Orders and Payments Contracts, so no csproj change. Identity "must never store order or payment data" and would need two new contract references | Controller in Identity (new cross-module references, composition outside the composing module) |
| D2 | `GET /api/v1/admin/customers?search=&page=&pageSize=` and `GET /api/v1/admin/customers/{id:guid}`, policy `AdminOrSupport` | §9 row | — |
| D3 | **Customer = a user with role `Customer`.** Staff (Admin, Support) never appear in the list; detail for a staff id, an unknown id or a malformed id → 404 | Staff accounts are not customers, and the screen must not become a staff directory | Listing all users with a role column |
| D4 | List paging follows the catalog read contract (§9): `page` ≥ 1 (default 1), `pageSize` 1–100 (default 20), out of range → 400; response `{ items, page, pageSize, totalCount, totalPages }`; past the last page → 200 empty. Order: `created_at DESC, id DESC` | One paging rule for the whole API | Clamping |
| D5 | `search` trimmed; empty → no filter; > 100 chars → 400. Case-insensitive **substring** on email OR display name: `EF.Functions.ILike(col, "%" + escaped + "%", "\\")`, with `\`, `%`, `_` escaped | §4.12 "search by name or email"; the escape stops `%` from matching everything | Full-text (names and emails are not prose); `pg_trgm` index (an extension = new DB dependency, not needed at v1 size, see risks) |
| D6 | List item `{ id, email, displayName, createdAt }`. No order count or total on the list | Keeps the list one Identity query; per-row totals would be N cross-module reads per page | Totals per row |
| D7 | Detail = `{ id, email, displayName, createdAt, orderCount, totalSpent, recentOrders, returnCount, recentReturns, refundCount, recentRefunds }`. Each `recent*` list is the **latest 10**, newest first, next to its full count. The full order history is the existing `GET /admin/orders?customer=<email>` | §9: lists are paginated; a detail with unbounded arrays breaks that. The admin order list already filters by exact customer email (§13.2 "Admin order list") | Unbounded arrays; three new paged sub-endpoints (no §9 row) |
| D8 | **`totalSpent` = Σ `grand_total` of the customer's orders that ever reached `Paid` (an `order_status_history` row with `to_status = 'Paid'`) − Σ `Succeeded` refunds with `actor_id` not null on those orders.** `{ amount, currency }` in `Store:Currency` (reuse `DashboardSettings.Currency`); 0 when none | Same definition as dashboard revenue (§13.2 "Slice 6b projections", D12: paid − actor refunds). Automatic refunds (late or duplicate captures) return money that was never part of a Paid grand total, so subtracting them would undercount. Strongly consistent read, not the projection: one customer, small | Captured payments − all refunds (counts duplicate captures); reading the dashboard projection (lag, and it has no per-customer total) |
| D9 | **Support gets `totalSpent: null`**; everything else identical. Decided in the controller from `User.IsInRole(IdentityRoles.Admin)`, passed to the service, as `AdminDashboardController` does. Order grand totals and refund amounts stay visible to Support | §9 "Support sees no revenue KPIs"; per-order amounts are already visible to Support in `GET /admin/orders` | Hiding all money from Support (inconsistent with the order list) |
| D10 | Item shapes: order `{ id, orderNumber, createdAt, status, grandTotal }`; return `{ id, orderId, orderNumber, status, reason, createdAt, decidedAt }`; refund `{ id, orderId, orderNumber, amount, status, automatic, createdAt }`. Money as `MoneyResponse` | `automatic` tells the admin why a refund has no actor; actor ids are never exposed (same rule as the order timeline) | Exposing `actor_id` |
| D11 | Three reads, no cross-module join (rule 8): Identity (profile) → Orders (orders, paid total, returns, all order ids) → Payments (refunds for those order ids). Payments is not called when the customer has no orders | Rule 8 | One SQL across schemas |

## 2. Contracts (all additions; existing members unchanged)

`Ecommerce.Identity.Contracts/IUserReader.cs`
- `record CustomerSummary(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt)`, `record CustomerPage(IReadOnlyList<CustomerSummary> Items, int TotalCount)`.
- `Task<CustomerPage> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct)` — role `Customer` only, D4/D5 order and match.
- `Task<CustomerSummary?> GetCustomerAsync(Guid userId, CancellationToken ct)` — null if missing or not role `Customer`.

`Ecommerce.Orders.Contracts/IOrderReader.cs`
- `record CustomerOrderRef(Guid Id, string OrderNumber)`, `record CustomerReturn(Guid Id, Guid OrderId, string OrderNumber, string Status, string Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt)`.
- `record CustomerOrders(int OrderCount, decimal PaidTotal, IReadOnlyList<CustomerOrderRef> All, IReadOnlyList<RecentOrder> Recent, int ReturnCount, IReadOnlyList<CustomerReturn> RecentReturns)` — reuse the existing `RecentOrder`.
- `Task<CustomerOrders> GetCustomerOrdersAsync(Guid userId, int recentTake, CancellationToken ct)`. `PaidTotal` = D8 first term, computed in Orders' store.

`Ecommerce.Payments.Contracts/IRefundState.cs`
- `record OrderRefundInfo(Guid RefundId, Guid OrderId, decimal Amount, string Currency, string Status, bool Automatic, DateTimeOffset CreatedAt)`, `record CustomerRefunds(decimal ActorSucceededTotal, int Count, IReadOnlyList<OrderRefundInfo> Recent)`.
- `Task<CustomerRefunds> GetForOrdersAsync(IReadOnlyCollection<Guid> orderIds, int recentTake, CancellationToken ct)` — refunds → payments → order ids, inside Payments. `ActorSucceededTotal` = D8 second term.

No other contract. Names must not collide with `CustomersResponse` already in `AdminDashboardController.cs` (use `AdminCustomer*Response`).

## 3. Files

Edited:
- `Identity.Contracts/IUserReader.cs`; `Identity/Application/UserReader.cs`; `Identity/Application/Ports.cs` (`IUserRepository` gains the two queries); `Identity/Infrastructure/Repositories.cs` (EF, `AsNoTracking`, ILike with escape).
- `Orders.Contracts/IOrderReader.cs`; `Orders/Application/OrderReader.cs`; the Orders read-store port + EF implementation (orders by `user_id` uses index `orders(user_id, created_at DESC)`; paid via `order_status_history`; returns via `return_requests.order_id`).
- `Payments.Contracts/IRefundState.cs`; `Payments/Application/RefundState.cs`; `IPaymentStore` + implementation.

New (Admin, all `internal`):
- `Admin/Application/CustomerQueryService.cs` — `ListAsync(search, page, pageSize)`, `GetAsync(id, includeTotal)`; computes `totalSpent = PaidTotal − ActorSucceededTotal`; maps refund order ids to order numbers from `CustomerOrders.All`. Registered in `AdminModule.cs`.
- `Admin/Api/AdminCustomersController.cs` — thin (rule 7): validate query (D4, D5), call service, map. Reuses `MoneyResponse`.

Tests (new):
- `Api.IntegrationTests/Admin/AdminCustomersApiTests.cs` (or beside the dashboard tests, Marco's choice of folder; reuse `OrderSeed` and `IdentityTestEnv`).
- Unit tests only where logic sits outside SQL: `Ecommerce.Admin.Tests/CustomerQueryServiceTests.cs` (total and order-number mapping with fake readers).

Not touched: frontend, migrations, `AuthorizationPolicies` (existing `AdminOrSupport`), Notifications.

## 4. Build order

1. **Test-first, quote red:** I1 (auth matrix), I2 (Support `totalSpent: null`), I3 (total spent money definition). Red because the endpoint returns 404.
2. Identity contract + repository → I4–I6 green.
3. Orders and Payments contract reads.
4. Admin service + controller → I1–I3 green, then the rest.
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30` → `slice-plans/slice-12.1-marco-report.md` (≤ 6 KB), including the red output for I1–I3 and both mutations.

## 5. Required tests (TF = test-first)

No §11.2 numbered test belongs to this row (gap-fill, no §12.3 row). New-endpoint rule: happy, 401/403, 400.

Integration (`AdminCustomersApiTests`, real Postgres):
- **I1 (TF) Auth:** both endpoints: anonymous → 401; Customer → 403; Admin → 200; Support → 200.
- **I2 (TF) Support guard:** the same customer detail as Admin has `totalSpent` with the amount, as Support `"totalSpent": null` (assert the JSON property is present and null); all other fields equal.
- **I3 (TF) Total spent:** one customer with: order A Paid 100.00; order B Paid 40.00 then refunded by an admin 30.00 (Succeeded); a pending admin refund 5.00 on A; a Failed admin refund 7.00 on A; an **automatic** Succeeded refund 100.00 on A (duplicate capture); order C `PendingPayment` 50.00; order D `Cancelled` 20.00 that was never Paid → `totalSpent.amount == "110.00"`, currency = `Store:Currency`. Another customer's paid order is not counted.
- I4 List: only role `Customer` users (seed one Admin and one Support: absent); newest first; tie on `created_at` broken by `id DESC`; `totalCount`/`totalPages` right; past the last page → 200 empty.
- I5 Search: substring of email and of display name, case-insensitive; `search=%` and `search=_` match only users whose email/name contain that literal character; whitespace-only = no filter; 101 chars → 400.
- I6 400s: `page=0`, `pageSize=0`, `pageSize=101`.
- I7 Detail 404: unknown id, an Admin user's id, malformed id (`/admin/customers/not-a-guid` → 404).
- I8 Detail lists: 12 orders → `orderCount 12`, `recentOrders` length 10 newest first; returns and refunds the same (count + latest 10, `orderNumber` filled, refund `automatic` true for the actor-less one); a customer with no orders → counts 0, empty arrays, `totalSpent.amount "0.00"`.

Unit (`CustomerQueryServiceTests`): U1 `totalSpent` = paid − actor total; `includeTotal: false` → null. U2 Payments reader is not called when the customer has no orders.

Architecture: `Ecommerce.ArchitectureTests` must stay unchanged and green (no new references).

**Mutations (§3, one per guard; Marco quotes red, restores with `cmp`):**
- MU1 (Support guard): pass `includeTotal: true` regardless of role → I2 red.
- MU2 (money definition): drop the `actor_id is not null` filter from `ActorSucceededTotal` → I3 red.

Changed tests: none expected. Any change to an existing test is listed with its reason.

## 6. Risks

- **Search scans `users`.** `ILIKE '%x%'` cannot use a B-tree index; acceptable at v1 customer counts. If the list exceeds ~100k users, a `pg_trgm` GIN index is a stack-adjacent change for Nathaniel (backlog, not this slice).
- **Paid detection.** Reading `orders.status` instead of the history would miss Paid orders now in `Refunded`/`Returned`, or count none of them; I3's order B guards this.
- **Large customers.** `CustomerOrders.All` and the `orderIds` passed to Payments grow with one customer's order count; fine at v1, noted.
- **Currency.** All amounts are in `Store:Currency` (§7.4); the service does not sum across currencies. If a row in another currency is ever found, it is a data error, not a case to handle here.
- **Role drift.** A user promoted from Customer to Support disappears from the list; that is D3, not a bug.
