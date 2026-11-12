# Slice P2.41 — Customers list and detail restyle

Tracker row P2.41 "Customers list and detail" (`admin-customers`, tier 1, "layout only"); nothing outside this row. Tracker footer: restyle rows keep existing tests unchanged; tier-1 rows use theme tokens only. Conventions carried from `slice-plans/archive/slice-P2.36-plan.md` and `slice-plans/archive/slice-P2.37-plan.md`: header hidden on each route, `ScrollView` page container, `AdminPanel`, className-only edits next to guarded logic, no copy changes.

Mode: Autonomous. **Risk tier: Low.** Both screens are read-only. There is no action, form, role change, disable or mutation on either screen. The only guarded logic is display logic: the **total-spent withholding** (`customer.totalSpent !== null`, slice 12.2 D5: the server decides, the client never sums) and the PII lines (name, email), which come from the server as they are. That logic is **frozen**: the expressions and strings stay byte-identical, and only their containers and `className` values change. Role gating is `AdminGate` in `app/admin/_layout.tsx` plus the server policies, and this slice does not touch either. Review: Marco's tests, the full `verify.sh`, and Rob reads the diff. No mutations. If anything in the frozen list has to change, stop: the tier becomes High and needs a new plan.

## Read:

- This plan.
- `frontend/apps/store/features/admin-customers/components/AdminCustomerListView.tsx` and `AdminCustomerDetailView.tsx` (the only feature files that change). From `AdminCustomerListView.test.tsx` and `AdminCustomerDetailView.test.tsx`, read only the test names and the queries.
- `frontend/apps/store/app/admin/customers/index.tsx`, `app/admin/customers/[id].tsx`, and `app/admin/orders/index.tsx` (the header-hiding pattern).
- `frontend/apps/store/components/AdminPanel.tsx`.
- `frontend/apps/store/features/admin-orders/components/AdminOrderListView.tsx`, lines 74–152 (container, panel, row and pager classes to copy).
- `frontend/apps/store/lib/design-tokens.js` (token names only).
- `docs/quality-bar.md` §1, §3, §4. Nothing else.

**Docs (Rob):** none needed. There is no new design decision. The breadcrumb already maps `/admin/customers` → `Customers` and `/admin/customers/<id>` → `Customer details` (`features/admin-shell/breadcrumb.ts`). The coordinator sets the tracker row to Done after PASS.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Hide the Stack header** on both routes. `index.tsx`: `<><Stack.Screen options={{ headerShown: false }} /><AdminCustomerListView /></>`. `[id].tsx`: the same, with `<AdminCustomerDetailView id={id} />`, and `Stack` added to the existing `expo-router` import. `useLocalSearchParams` is unchanged | Same as P2.34/P2.36/P2.37. The breadcrumb replaces the Stack back link | Editing `_layout.tsx` or `ScreenTitle` |
| **Page container** on both views: `contentContainerClassName="w-full max-w-content gap-lg self-center p-lg"`. Both roots are already `ScrollView`s, so only the class changes | Matches orders, returns, products and categories | — |
| **Titles unchanged**: `Admin · Customers` and `Admin · Customer` | The brief says no copy changes. The other restyled screens use plain titles (`Orders`), so the mismatch is logged as a backlog NOTE for a later copy row | Renaming here (copy change) |
| **List: search in one `AdminPanel` (no title)** holding `<SearchBar …/>` with the same props | Same as the products list (P2.37) | Leaving it bare (inconsistent) |
| **List: results in a second `AdminPanel` (no title)** holding the existing data / `EmptyState` / `ErrorState` / `LoadingState` branch, with the same order and conditions. The inner list `View` `gap-xs` becomes a `View` with no className (as in orders). Each row `Text`: `min-h-touch py-md text-body text-foreground` → `min-h-touch border-b border-border py-md text-body text-foreground`. Same `Link` `key`, `href`, `accessibilityRole`, `asChild`, and template string | Same as the orders list | One panel per customer (nested borders, heavier) |
| **List: `Pagination` stays outside, under the results panel.** Its condition `data && (data.totalPages > 1 \|\| page > 1)` and all four props stay byte-identical | Matches the orders/products pager position. The condition covers the "page past the end" case (L3), so it must not change | Moving it into the panel; aligning it with the orders condition (that would drop the past-the-end pager) |
| **Detail: the profile block goes in one `AdminPanel` (no title).** The inner `View className="gap-xs"` and its five children (name, email, `Customer since`, `Orders n`, and the `totalSpent !== null` line) are unchanged, including the class values. Only the wrapper is added | Profile data must not change. Wrapping it is checkable at a glance | Restyling the profile lines (no layout gain, more lines next to the D5 guard) |
| **Detail: `Section` renders `<AdminPanel title={title}>`** in place of its root `View` plus its own header `Text`. `AdminPanel` renders the same header markup (`accessibilityRole="header"`, `text-title font-semibold text-foreground`), so `getByRole('header', { name })` still holds. The `shown === 0` and `count > shown` expressions and their strings stay byte-identical. The `Section` props and signature are unchanged | One themed surface per list, with no duplicated header markup | Wrapping `Section` in a panel and keeping its header (two nested titles in a bordered box) |
| **Detail rows**: order link `Text` adds `border-b border-border` (same string, `href`, role). Return and refund row `View`s: `flex-row flex-wrap justify-between gap-sm` → `flex-row flex-wrap justify-between gap-sm border-b border-border py-sm`. Text nodes are unchanged | Matches the list row style | Status pills (they would split or change text nodes) |
| **Detail: `Content` root `gap-lg`** stays as it is | It already spaces the panels like the page container | — |
| **Error/loading branches unchanged**: the 404 check (`ApiError && status === 404`), both messages, and `onRetry` | C6 covers them; no layout need | — |
| **Reuse `AdminPanel` as is.** Add no new component and change no shared component (`SearchBar`, `Pagination`, `EmptyState`, `ErrorState`, `LoadingState`, `ScreenTitle`) | Shared components also serve the storefront | An admin-only `Pagination` (speculative) |
| Restyle rules: RN primitives and token utilities only. No `style=`, no hex, no arbitrary `[...]` values, no new tokens | Tier-1 theme rule | — |
| **No new tests.** Both test files stay byte-identical to `HEAD` and green | Tracker footer for restyle rows; no new behaviour. `AdminPanel` A1–A2 already exist | Class snapshot tests (brittle) |

## Files

All paths are under `frontend/apps/store/`.

| File | Change |
|---|---|
| `app/admin/customers/index.tsx` | Header hidden (see Decisions). Import `Stack` from `expo-router`. |
| `app/admin/customers/[id].tsx` | Header hidden (see Decisions). |
| `features/admin-customers/components/AdminCustomerListView.tsx` | Container class, search panel, results panel, row class. Import `AdminPanel` from `@/components/AdminPanel`. Keep `View` (the list wrapper) and `Text`. `parseParams`, `change`, `RawParams`, and the `useAdminCustomersQuery(search, page)` call are unchanged. |
| `features/admin-customers/components/AdminCustomerDetailView.tsx` | Container class, `Section` → `AdminPanel title`, profile wrapper panel, row classes. Import `AdminPanel`. `Text` and `View` are still used, so leave no unused import. |

Nothing else changes: no backend, no migration, no new package.

## Freeze list (checked by Rob in review)

These files must have an empty `git diff`: `features/admin-customers/hooks/**`, `features/admin-customers/api.ts`, `api.test.ts`, `types.ts`, both `*.test.tsx`, `components/AdminPanel.tsx`, `app/admin/_layout.tsx`, `features/admin-shell/**`, `features/auth/**`, `features/catalog/components/{SearchBar,Pagination}.tsx`, `features/orders/status-labels.ts`, `lib/money.ts`, `lib/api-client.ts`, and the shared components listed in Decisions.

Inside the changed files, these stay byte-identical to `HEAD`:
- **List:** `parseParams`, `change`, the `SearchBar` `onSearch` arrow, the row template string and `Link` props, the `Pagination` condition and props.
- **Detail:** the `totalSpent !== null` conditional and its string (D5), every template string (`Customer since …`, `Orders …`, `Showing … of …`, the order, return and refund lines, including `refund.automatic ? ' · Automatic' : ''`), the `shown === 0` and `count > shown` conditions, the `Link` `href`s, the 404 branch, and `onRetry`.

## Required tests

There are no new tests (see Decisions). The gate is that the existing suites stay byte-identical and green, and keep proving what they prove today:

| Suite | Must still prove |
|---|---|
| `features/admin-customers/components/AdminCustomerListView.test.tsx` | L1 row text and link to detail; L2 debounced search, page reset, blank → none, 100-char cap; L3 Next, no pager on one page, pager kept past the end; L4 empty, error + `Try again`, error not stale rows on page 2; L5 URL params and junk page |
| `features/admin-customers/components/AdminCustomerDetailView.test.tsx` | C1 server total only; C2 no total line when null, the three section headers by role; C3 `Showing n of m` only when truncated, order link; C4 return/refund lines, `Automatic` once; C5 empty messages, zero total; C6 404 without retry, 500 with retry |
| `features/admin-customers/api.test.ts`, `components/AdminPanel.test.tsx` (A1–A2) | Unchanged |

If a test fails after a layout change, fix the layout, never the test. If a test cannot pass with the planned layout, stop and report it with the failure output. Do not edit the test.

## Build order

1. Set yourself BUSY. From `frontend/apps/store`, on the untouched tree, run `npx jest features/admin-customers components/AdminPanel 2>&1 | tail -n 20` and quote the pass count (baseline).
2. Both routes: hide the header.
3. `AdminCustomerDetailView`: `Section` → `AdminPanel title`, the profile wrapper, row classes, the container. Run the suite.
4. `AdminCustomerListView`: container, the two panels, the row class. Run the suite. The pass count must equal the baseline.
5. Run `npm run format` from `frontend/apps/store` (repo config only). Then run `git -c safe.directory='*' status --short`: only the four slice files (plus the plan and the report) may be modified or new. If `format` touched any other file, report it and stop; do not revert with git.
6. Grep your diff for `style=`, `#[0-9a-fA-F]` and `-\[`: none should match. `git diff` on the freeze list: empty. `git diff -U0` on both views: every changed line is a className value, an added `AdminPanel` open/close or import, or the removed `Section` root/header lines. No changed line is inside a frozen expression.
7. Run the full `bash scripts/verify.sh 2>&1 | tail -n 30` and quote the `VERIFY` line. Write the report to `slice-plans/slice-P2.41-marco-report.md` (≤ 6 KB): the baseline and final Jest counts, the files, the freeze checks with their output, and the `VERIFY` line. Set yourself AVAILABLE.
8. Manual check (open for Nathaniel): at 1280 px and 375 px, `/admin/customers` and `/admin/customers/<id>` each show one title (no Stack header) and no horizontal scroll, the long email/row strings wrap, a customer with `totalSpent: null` shows no total line, and the order link opens the order.

## Review method (Rob)

Low tier. Run `git diff --stat` and read the four changed files in full. Confirm the following:
- The freeze list has no diff, and neither test file changed.
- The `totalSpent !== null` line, the `Section` conditions, every template string, the `Pagination` condition and `parseParams`/`change` are identical to `HEAD`.
- `Section` still exposes each title as a `header` role, through `AdminPanel`.
- No `style=`, hex or arbitrary values are used.

Run the full `verify.sh` in the PASS round. No mutations: no money, stock, auth or state logic changes, and the D5 guard is covered by C1/C2/C5 as they stand.

## Risks

- **D5 drift:** any edit to the `totalSpent` conditional or a computed total on the client is BLOCKING (a hard rule: the server computes all money, arch §3.4).
- **Header duplication:** if `Section` keeps its own header `Text` inside `AdminPanel title`, `getByRole('header', { name })` finds two matches and C2 fails. Remove the old header; don't change the test.
- **Pager condition:** this list deliberately differs from orders (it shows the pager past the end). Copying the orders condition breaks L3.
- **`getByText` collisions:** no text is added. `No customers match.` appears once, as today.
- **Keyboard on native:** `SearchBar` sits in a `ScrollView`, as today. Don't add `keyboardShouldPersistTaps` (the backlog NOTE from P2.36 still applies).
- **Scope creep:** no title copy change, no status pills, no avatar, no customer actions, no shared-component restyle.
- Run one `verify.sh`/Jest run at a time.
