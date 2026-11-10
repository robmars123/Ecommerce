# Slice 12.2 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 warnings · test 1195 passed, 0 failed (incl. 495 Api.IntegrationTests, Docker up) · jest 173 suites / 1249 tests

Low tier (plan §Risk tier): I read the whole diff and every untracked file, ran no mutations. The screens follow the plan closely: thin routes, URL-held list state, no `keepPreviousData`, and the money line comes only from the server's payload.

### 1. Money visibility (plan C1 / D5) — Confirmed
**What I saw:** `AdminCustomerDetailView.tsx` draws `Total spent` only when `customer.totalSpent !== null` and formats the server's value. Nothing in `features/admin-customers/` reads the session or a role (grep for `session|useSession|role` hits only test setup and `accessibilityRole`), and nothing sums `recentOrders`/`recentRefunds` (no `reduce`, no arithmetic on `Money`). C1 serves `totalSpent: 110.00` with orders of 100 + 40 and asserts `$140.00` is absent; C2 signs in as **Admin** with `totalSpent: null` and asserts no `/Total spent/`, so the view is keyed off the payload, not the role. You quoted the red from a stub view (16 failed, C1 and C2 among them).
**Lesson:** the server owns who sees revenue; the client just renders what it's given.

### 2. `tests/routes.test.tsx` stub-route guard — Confirmed, not a weakening
**What I saw:** you wrapped the stub `it.each` in `if (STUB_ROUTES.length > 0)` because no route is `built: false` any more.
**Why it's sound:** Jest's `it.each([])` throws "Error: `.each` called with an empty Array of table data", so the old shape would fail the suite with no stub left. The filter still comes from `ROUTES`, so any future `built: false` row runs the stub assertion with no edit. The two customer rows now go through the built-route test, which is stricter: it waits for the title header **and** asserts `Not built yet.` is absent, with real API mocks for `/admin/customers` and `/admin/customers/3`. No route lost coverage.
**Lesson:** a guard that only takes effect when the table is empty, with the rows still coming from the same source, is not a skip.

### 3. Edited existing tests — Confirmed, both additive
- `features/auth/admin-access.test.ts`: `'/admin/customers'` added to `PATHS` and `null` added to the Support row at the matching index (8th of 9). `none`, `Customer` and `Admin` are mapped over `PATHS`, so they gain the case too. No expectation changed or removed; it now pins that Support reaches the list, not only the detail (D11).
- `lib/query-keys.test.ts`: a new `describe` only; no existing case touched. It checks `list(undefined,1)` equals `list('',1)`, that a real search gives a different key, and that list and detail keys start with `customers.all` (K1).

### 4. Contract and plan conformance — Confirmed
`types.ts` matches the controller records field for field (`AdminCustomerItemResponse`, `ListResponse`, `Order`/`Return`/`Refund`/`DetailResponse`); unions are type-imported, not redeclared. `api.ts` sends no `search` for `''`/`undefined` and encodes the id (A1, A2). The list parses `page` digits-only ≥ 1 and slices `search` to 100 on read (L2, L5). The pager shows on `totalPages > 1 || page > 1` (L3 past-the-end case), and L4 proves a failed page 2 shows the error, not page 1's rows. Detail 404 has no retry; 500 retries and refetches (C6, GET counted). Routes keep their component names. No backend, no package, no other feature folder touched. The §13.2 row "Admin customers screens" is present.

### 5. Customer search box says "Search products" — NOTE   (`features/catalog/components/SearchBar.tsx:52-53`)
**What I saw:** the reused `SearchBar` hard-codes its placeholder and `accessibilityLabel` as "Search products", so the customers screen shows that text and a screen reader announces it. It's my plan's gap (D3 said reuse it as is), not your error. Fixing it touches the `catalog` folder, so it goes to the backlog: add an optional `label` prop (default unchanged) and pass "Search customers". It's copy only: the search still works, and no data or rule is affected.

**What was good:** C2 renders as Admin with a null total. That proves the view doesn't use the role, which a Support-session test could not show. And you explained in a comment why the route-test guard is there, so a later reader won't take it for a skip.
