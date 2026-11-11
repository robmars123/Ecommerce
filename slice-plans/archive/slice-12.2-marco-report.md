Concerns:
- verify.sh FAILS at `test` for an environment reason, not code: the Docker daemon is not running (`docker info` shows a client but no server; log shows Testcontainers `NamedPipeClientStream.ConnectInternal` / `DockerEndpointAuthenticationProvider`). Ecommerce.Api.IntegrationTests: Failed 495 / Total 495, all at container start. Every other .NET project passes (Notifications 18, Identity 77, Inventory 28, Cart 19, Catalog 202, Payments 86, Admin 11, Orders 234, Architecture 25). This slice changes no backend or persistence code. Needs Docker Desktop started, then one `verify.sh` re-run. I did not start Docker myself.
- `tests/routes.test.tsx`: with both customer routes built, no stub rows remain, and `it.each([])` throws "`.each` called with an empty Array". The plan did not foresee this. Safe minimum: the stub test is wrapped in `if (STUB_ROUTES.length > 0)`. It is not skipped or deleted; it runs again as soon as a stub route exists. Rob to confirm.
- Slice-plan retry label: the plan says "Retry"; `ErrorState` renders "Try again", so tests use that.

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed:
- tests/routes.test.tsx: two customer rows `false -> true`; two mock routes added; stub `it.each` wrapped in a non-empty guard (above). Built-screen assertion (title, no "Not built yet.") now covers both customer routes (R1).
- features/auth/admin-access.test.ts: `/admin/customers` added to PATHS; Support expectation gets a `null` at that index (R2). Additive.
- lib/query-keys.test.ts: customers describe block added (K1). Additive.

TDD (C1/C2 first): with a stub `AdminCustomerDetailView` rendering only "stub", the detail test file ran red: `Tests: 16 failed, 16 total` (8 tests x android/ios projects), e.g. `Unable to find an element with text: Total spent $110.00` (C1), `Unable to find an element with text: Ann Lee` (C2), `Unable to find an element with text: Showing 10 of 12`, `... We could not find that customer.`. Then the view was built; the same file is green. C1 asserts `$140.00` (sum of the two recent orders) is absent; C2 renders as Admin with `totalSpent: null` and asserts no `/Total spent/`, with profile, three section headers and per-order amounts present.

Targeted run (features/admin-customers, admin-access, query-keys, routes): `Test Suites: 12 passed, 12 total | Tests: 126 passed`.

Changed (slice 12.2, frontend only, no backend, no new packages):
New: features/admin-customers/{types.ts, api.ts, api.test.ts, hooks/useAdminCustomersQuery.ts, hooks/useAdminCustomerQuery.ts, components/AdminCustomerListView.tsx (+.test), components/AdminCustomerDetailView.tsx (+.test)}; tests/admin-customer-fixtures.ts.
Edited: app/admin/customers/index.tsx, app/admin/customers/[id].tsx, lib/query-keys.ts, lib/query-keys.test.ts, tests/routes.test.tsx, features/auth/admin-access.test.ts.
Not done by design: docs/ecommerce-app-context.md row 12.2 (Done only after Rob's PASS).

Design notes: no `keepPreviousData` (D4); pager shown when `totalPages > 1 || page > 1`, with total pages clamped to at least the current page; total spent drawn only when `totalSpent !== null`, session role never read (D5). L2 uses real timers with `waitFor` timeout 3000, as `ProductListView.test` does.

verify.sh:
PASS secrets · PASS hygiene · PASS build (0 warnings, 0 errors) · FAIL test (Docker daemon down, integration tests only) · PASS drift · PASS expo-check · PASS tsc · PASS jest (173 suites, 1249 tests) · PASS export
VERIFY FAIL: test

Quality-bar §2: code boxes (typing, tests co-located, no `var`, no secrets, a11y roles/labels, loading/error/empty states, no client money) checked. The "full verify.sh passes" box is NOT checked: the Docker outage above. AGENT_STATUS.md left BUSY, as verify did not pass.
