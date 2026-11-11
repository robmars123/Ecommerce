**Read:** app-context row 12.2 (line 120), §2 Support row (line 25), §4.12 (lines 234–236), §5 route `/admin/customers` (line 264) · arch §9 admin customers row (09-api.md line 28) · §5.2 and §5.4 (05-frontend.md lines 17–55) · §11.3 (11-testing.md lines 46–57) · §13.2 rows "Admin customers API" and "Admin customers screens" (grep, don't read the log) · quality-bar §1, §2, §4.
Code to read before editing: `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminCustomersController.cs` (the response records at the top only — they are the wire contract), `features/admin-orders/{api.ts, types.ts}`, `features/admin-orders/components/{AdminOrderListView.tsx, AdminOrderDetailView.tsx, AdminOrderListView.test.tsx (fetch-mock + render pattern)}`, `features/admin-catalog/components/AdminProductListView.tsx` (URL `search`/`page` params + `Pagination` + `SearchBar` only), `features/catalog/components/{SearchBar.tsx, Pagination.tsx}`, `features/orders/status-labels.ts`, `features/orders/types.ts` (`OrderStatus`, `RefundStatus`), `lib/{api-client.ts (apiGet, ApiError, userMessage), money.ts, query-keys.ts, query-keys.test.ts}`, `components/{ScreenTitle,ErrorState,LoadingState}.tsx`, `app/admin/customers/{index,[id]}.tsx`, `app/admin/orders/{index,[id]}.tsx`, `features/auth/admin-access.test.ts`, `tests/routes.test.tsx` (ROUTES + `mockCatalogApi`), `tests/admin-order-fixtures.ts`, `tests/fetch-mock.ts`.

All paths below are under `frontend/apps/store/` unless they start with `docs/` or `backend/`.

# Slice 12.2 — Admin customer list and detail screens (frontend)

Deliverable: app-context **12.2**, gap-fill (no §12.3 row, like 9.7 and 10.4). Builder: **Marco**. Mode: Autonomous.
Out of scope: any backend change; a dashboard "Customers" quick action (that is the `admin-dashboard` feature folder — one deliverable touches one feature folder, app-context line 51; backlog); a "see all orders for this customer" link (needs `AdminOrderListView` to accept `?customer=`, the `admin-orders` folder; backlog); editing, banning, impersonation (§4.12 read-only); links to `/admin/returns` (Admin only — Support would be bounced).

## Risk tier — Low

Two read-only screens, no server code, no write call, no money computed. The one role rule — **Support never sees total spent** (§9, §2 Support row) — is enforced by the server: `AdminCustomersController.Get` sends `totalSpent: null` for Support, pinned by 12.1 integration test I2 (test-first, mutation-checked in 12.1). The client cannot leak a value it never receives; the worst client bug is a crash or a stray "Total spent" label on null, which test C2 covers. Route access is `AdminGate` + `adminRouteDecision` (`/admin/customers` already in `SUPPORT_PREFIXES`), and the API blocks Customers by policy (12.1 I1). So CLAUDE.md §5 Low: Marco's tests + full `verify.sh`, Rob reads the diff, **no mutations**. Rob checks by reading: the client never derives a total from the order list (C1) and never reads the session role to hide money (D5).

**Backend:** nothing missing. The list and detail records in `AdminCustomersController.cs` carry every field the screens show.

## 1. Decisions (Rob records them as one §13.2 row, "Admin customers screens" — done)

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | New feature `features/admin-customers/`; `app/admin/customers/index.tsx` renders `AdminCustomerListView`, `app/admin/customers/[id].tsx` renders `AdminCustomerDetailView id={id}`. Titles stay **`Admin · Customers`** and **`Admin · Customer`** (already pinned by `tests/routes.test.tsx`) | Feature-folder rule (§5.4); screens thin (§11.3) | Putting it in `admin-orders` (different API, different module) |
| D2 | Wire types mirror the controller records exactly (§5.4 "named after the API resource"): `AdminCustomerListItem { id, email, displayName, createdAt }`, `AdminCustomerListPage { items, page, pageSize, totalCount, totalPages }`, `AdminCustomerOrder { id, orderNumber, createdAt, status: OrderStatus, grandTotal: Money }`, `AdminCustomerReturn { id, orderId, orderNumber, status: ReturnStatus, reason, createdAt, decidedAt: string \| null }`, `AdminCustomerRefund { id, orderId, orderNumber, amount: Money, status: RefundStatus, automatic: boolean, createdAt }`, `AdminCustomerDetail { id, email, displayName, createdAt, orderCount, totalSpent: Money \| null, recentOrders, returnCount, recentReturns, refundCount, recentRefunds }`. Reuse `Money` (catalog), `OrderStatus`/`RefundStatus` (orders), `ReturnStatus` (admin-orders) — type imports only, precedent in `admin-orders/types.ts` | One source of truth for each wire union; no new enums | Redeclaring the unions; `status: string` |
| D3 | List state lives in the URL: `?search=&page=`, parsed as `AdminProductListView` does (search trimmed and sliced to 100 chars; page digits only and ≥ 1, else 1). `SearchBar` (debounced) sets `search` and resets page to 1; `Pagination` moves the page. Page size 20 (`ADMIN_CUSTOMER_PAGE_SIZE`) | Deep-linkable, back button works; same pattern as the product list. 100 = server's 400 limit (12.1 D5) so the client never triggers it | Local `useState` filters (lost on back); an "Apply" button |
| D4 | **No `placeholderData: keepPreviousData`** on either query. Pager shown when `totalPages > 1 \|\| page > 1` | Avoids the known `AdminOrderListView` bug (backlog, 5 gap-fill R1): kept data hid `isError`, and a page past the end lost its pager | Copying the order-list hook |
| D5 | **Total spent is shown only when the server sends it**: `totalSpent !== null` → `Total spent {formatMoney(totalSpent)}`; `null` → the line is not rendered at all (no label, no "hidden", no placeholder). The view **never reads the session role** for this, and never sums `recentOrders`/`recentRefunds` | Server is the authority (§3.4 rule 11, hard rule 1); same pattern as `DashboardView` `kpis`. A role check in the client would be a second, divergent rule | Hiding by `session.user.role`; a "Hidden for Support" note; computing a total from the list |
| D6 | Detail layout (top to bottom): display name, email, `Customer since {formatOrderDate(createdAt)}`, `Orders {orderCount}`, total spent (D5); then three sections with `accessibilityRole="header"` titles **Recent orders**, **Recent returns**, **Recent refunds**. Each section, when its count > its list length, adds `Showing {n} of {count}`; empty → `No orders yet.` / `No returns.` / `No refunds.` | 12.1 D7: latest 10 next to full count; the admin must see the list is truncated | Paging the sub-lists (no endpoint) |
| D7 | Order rows: `Link` to `/admin/orders/[id]` (Admin and Support may open it), text `{orderNumber} · {formatOrderDate(createdAt)} · {statusLabel(status)} · {formatMoney(grandTotal)}`. Return rows: plain text `{orderNumber} · {status} · {reason}` + date. Refund rows: plain text `{orderNumber} · {formatMoney(amount)} · {refundStatusLabel(status)}` + `· Automatic` when `automatic` + date | Per-order amounts are already visible to Support (12.1 D9). `/admin/returns` is Admin-only, so no return link | Linking returns to `/admin/returns` |
| D8 | List rows: `Link` to `/admin/customers/[id]`, text `{displayName} · {email} · Joined {formatOrderDate(createdAt)}`. Empty → `No customers match.` | Mirrors the order list row; §4.12 search by name or email | A table component (none exists) |
| D9 | Errors: list error → `ErrorState` with retry (`refetch`). Detail **404** → `ErrorState message="We could not find that customer."` with no retry (unknown id, staff id and malformed id all answer 404, 12.1 D3); other errors → `userMessage(error)` + retry. Same shape as `AdminOrderDetailView` | Retrying a 404 is pointless | Redirecting to the list |
| D10 | Query keys: `queryKeys.admin.customers = { all: ['admin','customers'], list: (search: string \| undefined, page: number) => ['admin','customers','list', { ...(search ? { search } : {}), page }], detail: (id) => ['admin','customers','detail', id] }` | Single source (§5.1); empty search and undefined give one key | Inline keys in hooks |
| D11 | Route guard unchanged (`adminRouteDecision` already allows Support under `/admin/customers`). Add `'/admin/customers'` (the bare list path) to the `PATHS` table in `admin-access.test.ts` with Support → `null` | Pins that Support reaches the list, not only the detail; additive change | No test change |

## 2. Files

New (`features/admin-customers/`):
- `types.ts` — D2 types.
- `api.ts` — `ADMIN_CUSTOMER_PAGE_SIZE = 20`; `fetchAdminCustomers(search: string | undefined, page: number, signal?)` → `apiGet<AdminCustomerListPage>('/api/v1/admin/customers', { params: { search: search || undefined, page, pageSize: ADMIN_CUSTOMER_PAGE_SIZE }, signal })`; `fetchAdminCustomer(id, signal?)` → `apiGet<AdminCustomerDetail>(`/api/v1/admin/customers/${encodeURIComponent(id)}`, { signal })`.
- `api.test.ts`.
- `hooks/useAdminCustomersQuery.ts` — `useQuery` on `queryKeys.admin.customers.list(search, page)`. No placeholder (D4).
- `hooks/useAdminCustomerQuery.ts` — `useQuery` on `queryKeys.admin.customers.detail(id)`.
- `components/AdminCustomerListView.tsx` (+ `.test.tsx`) — D3, D4, D8, D9.
- `components/AdminCustomerDetailView.tsx` (+ `.test.tsx`) — D5–D7, D9. Small local `Section` component like `AdminOrderDetailView`'s (not exported).

Changed:
- `app/admin/customers/index.tsx`, `app/admin/customers/[id].tsx` — replace `ScreenStub` with the views (D1); route components keep their names `AdminCustomerListScreen` / `AdminCustomerDetailScreen`.
- `lib/query-keys.ts` (+ `lib/query-keys.test.ts`) — D10.
- `tests/admin-customer-fixtures.ts` (new) — `makeCustomerItem`, `makeCustomerPage(items, overrides)`, `makeCustomerDetail(overrides)` (Admin shape, `totalSpent` set), modelled on `tests/admin-order-fixtures.ts` (reuse its `usd`).
- `tests/routes.test.tsx` — the two customer rows flip `built` to `true`; `mockCatalogApi` answers `/api/v1/admin/customers` with an empty page and `/api/v1/admin/customers/3` with `makeCustomerDetail()`.
- `features/auth/admin-access.test.ts` — D11 (one path, one `null` in each expectation row at the matching index).
- `docs/ecommerce-app-context.md` row 12.2 — Status `Done` only after Rob's PASS (Rob or the coordinator; Slice column already `gap-fill`).

No new packages. No backend, `components/`, or other feature folder changes.

## 3. Build order

1. Types, fixtures, `api.ts` + `api.test.ts`, query keys + test.
2. **C1/C2 first** (the money-visibility contract): write them against a stub `AdminCustomerDetailView`, quote the red, then build the view.
3. Rest of the detail view and its tests; list view and its tests.
4. Wire the routes; flip `routes.test.tsx`; `admin-access.test.ts`.
5. `bash scripts/verify.sh 2>&1 | tail -n 30` (full), report.

## 4. Required tests

No §11.2 numbered or F test belongs to this row (gap-fill). §11.3 rules apply: every new exported function has a co-located test; components with behavior are tested by role/label; routes render. Hooks are thin `useQuery` wrappers covered through the view tests (same as 10.4). TF = write first, quote red.

`api.test.ts`
- A1 `fetchAdminCustomers('ann', 2)` requests `/api/v1/admin/customers?search=ann&page=2&pageSize=20` (assert path and each query param); `fetchAdminCustomers(undefined, 1)` and `('', 1)` send no `search` param.
- A2 `fetchAdminCustomer('a/b')` requests `/api/v1/admin/customers/a%2Fb`; a 404 rejects with `ApiError` status 404.

`lib/query-keys.test.ts`
- K1 `customers.list(undefined, 1)` equals `customers.list('', 1)`; list and detail keys start with `customers.all`.

`AdminCustomerDetailView.test.tsx`
- **C1 (TF) Server total only:** detail with `totalSpent: usd('110.00')` and two recent orders of `usd('100.00')` and `usd('40.00')` → shows `Total spent $110.00`; no text `$140.00` anywhere; profile (name, email, `Customer since …`) and `Orders 2` shown.
- **C2 (TF) Support shape:** same detail with `totalSpent: null` → no element matching `/Total spent/`; profile, counts and all three sections still render; per-order amounts still render (D7). Render while signed in as **Admin** (or with no session) to prove the view keys off the payload, not the role (D5).
- C3 Truncation and links: `orderCount: 12` with 10 recent orders → `Showing 10 of 12`; pressing an order row navigates to `/admin/orders/<id>` (or assert the link's `href`, following `AdminOrderListView.test.tsx`); counts equal to list length → no `Showing` line.
- C4 Returns and refunds: a refund with `automatic: true` shows `Automatic`, one with `false` doesn't; refund status uses `refundStatusLabel`; a return shows order number, status and reason.
- C5 Empty: all counts 0, empty lists, `totalSpent: usd('0.00')` → `No orders yet.`, `No returns.`, `No refunds.`, `Total spent $0.00`.
- C6 Errors: 404 → `We could not find that customer.` and no `Retry` button; 500 → plain-language message, `Retry` refetches (second GET counted).

`AdminCustomerListView.test.tsx`
- L1 Rows: two customers → each row text has name, email, `Joined …`; each links to `/admin/customers/<id>`.
- L2 Search: typing `ann` (advance the debounce with fake timers, as the product list test does) → next request has `search=ann&page=1`; whitespace only → no `search` param; a 120-char string → request `search` is 100 chars.
- L3 Paging: `totalPages: 3` → `Next` requests `page=2`; `totalPages: 1, page 1` → no pager; a URL `?page=5` with an empty page (`totalPages: 2`) still shows the pager (D4).
- L4 Empty → `No customers match.`; error → message + `Retry` that refetches; an error on page 2 after page 1 loaded shows the error, not page 1's rows (D4).
- L5 URL params: rendered at `/admin/customers?search=bob&page=2` → first request carries `search=bob&page=2`; `?page=abc` → `page=1`.

Route and access
- R1 `tests/routes.test.tsx`: both customer routes render their titles with the stub note gone (`built: true`).
- R2 `admin-access.test.ts`: D11.

Changed tests (Marco lists each in the report): `tests/routes.test.tsx` (two rows `false → true`, two mocks added — not a weakening: the stub assertion is replaced by the built-screen assertion), `features/auth/admin-access.test.ts` (one path added), `lib/query-keys.test.ts` (case added).

## 5. Risks

- **Revenue shown to Support** — impossible from the client alone (server sends null, 12.1 I2); C2 stops a crash or a stray label. If a later change adds a client role check, D5 says reject it.
- **Client-side money:** a "helpful" sum of recent orders would be wrong (only 10 rows, refunds, unpaid orders) and breaks hard rule 1 — C1 asserts the summed value is absent.
- **Huge `page` in the URL** → the server's `int` offset overflow answers 500 (backlog, 12.1 R1); the view shows the error with retry. Not fixed here (backend).
- **Unreachable screen:** no navigation entry until the dashboard quick action is added (backlog). Reachable by URL and from nowhere else in v1 until then.
- **Malformed id** (`/admin/customers/abc`) → server 404 → C6 message. No client GUID check (server is the rule).
