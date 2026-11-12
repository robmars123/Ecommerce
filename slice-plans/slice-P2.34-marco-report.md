# Slice P2.34-35-40-33 — Marco report (continuation round)

Concerns:
- Prettier is not idempotent on a few files in one pass (objectWrap preserve): `npm run format` was run until `format:check` was clean. Reviewing 3 files (`LoginForm.test.tsx`, `RegisterForm.test.tsx`, `MergeResultNotice.test.tsx`) with the plan's method shows a 5-line member-chain diff vs `prettier(HEAD)`; these are format-only (a second prettier pass), no semantic change.
- Previous run had a tsc error in `AdminOrderListView.tsx` (`validateFilters({ ...draft, status })`; `validateFilters` takes `Pick<…,'from'|'to'>`). Fixed to `validateFilters(draft)`; no change to `order-actions.ts`.
- One `routes.test.tsx` test (`/products`, 5 s timeout) failed once in the full verify run under load; passes alone and in the re-run (see verify line).
- CLAUDE.md §7 `verify.sh` line does not list `format` (coordinator / Nathaniel's call). `.github/workflows/` untouched.
- Open for Nathaniel: manual check of plan step 8 (1280 px / 375 px, one title, no horizontal scroll, detail scrolls to refund form).

Bugs found: None
Bugs fixed: tsc error above (tsc clean; AdminOrderListView L1–L6 + existing 8 pass)
Bugs open: None

Step 0 (approved `git show HEAD:<path> > <path>` overwrite, this slice only): restored `app/admin/_layout.tsx`, `admin-orders/{api.ts,api.test.ts,hooks/hooks.ts,order-actions.ts,types.ts}`, `components/{AdminOrderDetailView.test.tsx,AdminReturnsView.tsx,AdminReturnsView.test.tsx,RefundForm.test.tsx,OrderActions.tsx,RefundForm.tsx}`, `InventoryRow.tsx`; `AdminOrderListView.test.tsx` = HEAD + `router`/`act` import + L1–L6 appended. className edits re-applied. Then prettier 3.10.0 (exact), `.prettierrc`, `.prettierignore`, scripts, `verify.sh` `format` step; whole-app `npm run format`; restyle finished; format again.

Self-check (review method): `OrderActions.tsx`, `RefundForm.tsx`: only `className="gap-md"` -> `gap-md rounded-card border border-border bg-surface p-lg`. `InventoryRow.tsx`: only adjust-form root `gap-md rounded-control bg-surface-raised p-md` and the status pill `rounded-pill bg-surface-raised px-sm text-small`. All other untouched files: empty diff vs `prettier(HEAD)` except the 3 noted above. No `style=`, hex or arbitrary `[...]` class in the changed views.

Reds (quoted by previous Marco, before implementation): L1 got `Paid` expecting `Shipped`; L4 got page 1 expecting 2; L2 status undefined (still sent); H1 `Expected (3,30) Actual (5,30)`.

Tests changed:
- `AdminOrderListView.test.tsx`: existing 8 byte-identical; appended L1–L6; import line adds `router`, `act`. (HEAD test file reformatted by Prettier only.)
- Backend (the four in the plan): `InventoryHostTests` renamed to `The_configured_settings_are_a_threshold_of_3_and_a_30_second_sweep`, asserts (3, 30) (was threshold 5); `StockStatusTests` T20 reader: stock 6 -> 4 (InStock), 5 -> 3 (Low); T20 reservation: 6 -> 4, after reserving 1 -> 3 (Low); `AdminDashboardApiTests.Low_stock_…`: `LOW-5`/5 -> `LOW-3`/3, `LOW-6`/6 -> `LOW-4`/4. Intent kept: threshold -> Low/listed, threshold+1 -> not. Backend: 72/72 pass.
- ~90 other frontend files are Prettier format-only (no semantic change).

New files: `order-list-params.ts` + test (P1–P5), `components/AdminPanel.tsx` + test (A1–A2), `.prettierrc`, `.prettierignore`.

Changed: backend `appsettings.json` (threshold 3) + 3 test files; frontend `AdminOrderListView.tsx` (+test), `AdminOrderDetailView.tsx`, `AdminInventoryView.tsx`, `InventoryRow.tsx`, `OrderActions.tsx`, `RefundForm.tsx`, `app/admin/orders/{index,[id]}.tsx`, `app/admin/inventory.tsx`, `package.json`/`package-lock.json` (prettier only), `scripts/verify.sh`; ~90 format-only files.

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 193 suites, 1503 tests; build 0 warnings.

Quality-bar §2: tests for new behaviour present (P, L, A, H); no secrets; no `var`; no money/stock/auth change; thin components; one package (prettier, plan-named); no weakened tests (existing frontend tests unchanged vs prettier(HEAD)); manual responsive check N/A to automation, listed open.
