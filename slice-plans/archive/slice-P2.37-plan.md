# Slice P2.37-38-39 — Products list, product create/edit, categories restyle

Tracker rows P2.37, P2.38, P2.39 (tier 1, "layout only"), one slice by owner decision; nothing outside these rows. Tracker footer: restyle rows keep existing tests unchanged; tier-1 rows use theme tokens only. Conventions carried from `slice-plans/archive/slice-P2.34-plan.md` (header hidden per route, breadcrumb replaces the back link, `ScrollView` for long content, `AdminPanel`, className-only for components with actions or forms).

Mode: Autonomous. **Risk tier: Low.** Layout and classes only. No endpoint, money, stock, auth rule or state transition changes. Prices stay server-validated: the price inputs (`VariantRowsEditor`), `form-model.ts` (`toCreateRequest`/`toUpdateRequest`) and the mutation hooks are untouched. Role gating is `AdminGate` in `app/admin/_layout.tsx` plus server policies — not in any file of this slice. Review: Marco's tests + full `verify.sh` + Rob reads the diff, no mutations. Line-by-line check of the "Form freeze" files.

## Read:

- This plan.
- `frontend/apps/store/features/admin-catalog/components/`: `AdminProductListView.tsx`, `AdminProductCreateView.tsx`, `AdminProductDetailView.tsx`, `ProductForm.tsx`, `VariantRowsEditor.tsx`, `ImageKeysEditor.tsx`, `AdminCategoriesView.tsx`, `CategoryCreateForm.tsx`, `CategoryRow.tsx`. Their `*.test.tsx`: test names and queries only (to know what must keep working).
- `frontend/apps/store/app/admin/products/index.tsx`, `products/new.tsx`, `products/[id].tsx`, `app/admin/categories.tsx`; `app/admin/orders/index.tsx` (the header-hiding pattern).
- `frontend/apps/store/components/AdminPanel.tsx`, `components/ScreenTitle.tsx`.
- `frontend/apps/store/features/admin-orders/components/AdminOrderListView.tsx` lines 70–150 (chip, row and container classes to copy).
- `frontend/apps/store/lib/design-tokens.js` (token names only).
- `docs/quality-bar.md` §1, §3, §4. Nothing else.

**Docs (Rob):** none needed — no new design decision (conventions are the P2.34 ones). Tracker rows set Done by the coordinator after PASS.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Hide the Stack header** on the four routes (`products/index`, `products/new`, `products/[id]`, `categories`): each route renders `<Stack.Screen options={{ headerShown: false }} />` before its view. `ScreenTitle` unchanged | Same as P2.34; the breadcrumb (P2.6, already maps all four paths incl. `New product`) replaces the back link | Editing `_layout.tsx` (affects unrestyled screens) or `ScreenTitle` |
| **Page container** on all four views: `contentContainerClassName="w-full max-w-content gap-lg self-center p-lg"` | Matches the restyled orders/inventory screens | A narrower form width (would need a new token) |
| **Products detail, loading/error branch:** keep the root `View`, same container classes as above. Do not turn it into a `ScrollView` | Short content; no behaviour change | — |
| **Reuse `AdminPanel`** as is. No new shared component, no change to `AdminPanel` or its tests | Already covers bordered block + optional header with the same header markup as the existing `Categories` heading | A new `AdminCard`/`FormPanel` (no second shape needed) |
| **Form freeze (className only):** `ProductForm.tsx`, `VariantRowsEditor.tsx`, `ImageKeysEditor.tsx`, `CategoryCreateForm.tsx`, `CategoryRow.tsx`. Only `className` string values change; no element, prop, handler, label, `keyboardType`, `editable`, error mapping or import is added or removed | User constraint: no change to validation, money, submit actions or role gating. A className-only diff is checkable line by line | Wrapping the product's basic fields in a new `View`/`AdminPanel` inside `ProductForm` (structural edit in a form) |
| The **product form sits in one `AdminPanel` (no title) in the view** (create view and detail edit tab); its inner variant/image cards become raised sub-cards. The detail preview tab content sits in the same panel position | Gives the form a themed surface without touching the form's structure | Panels inside `VariantRowsEditor`/`ImageKeysEditor` roots (nested bordered panels in a panel) |
| **Product list status chips** use the orders chip pattern (pill, `primary` when selected). Same `accessibilityRole`, `accessibilityState`, labels, handler | One chip style across admin lists | Keeping `border-2 border-selected` (inconsistent with orders) |
| **Shared storefront components stay untouched:** `SearchBar`, `CategoryFilter`, `Pagination`, `ProductDetailContent`, `EmptyState`, `ErrorState`, `LoadingState`, `ActionButton`, `TextField` | Used by the customer store; restyling them is not in these rows | Admin-only variants of them (speculative) |
| Restyle rules: RN primitives and token utilities only. No `style=`, no hex, no arbitrary `[...]` values, no new tokens. **No copy changes**: every visible string and accessible name byte-identical | Tier-1 "layout only"; tests query by text/role | — |
| **No new tests.** All existing catalog tests stay byte-identical (= `HEAD`, already Prettier-formatted) and green | Tracker footer for restyle rows; no new behaviour to prove. `AdminPanel` A1–A2 already exist | Snapshot tests of classes (brittle, prove nothing) |

## Files per row

All paths under `frontend/apps/store/`.

### P2.37 — Products list

| File | Change |
|---|---|
| `features/admin-catalog/components/AdminProductListView.tsx` | Container classes (decision). Directly under `ScreenTitle`, a `View className="flex-row flex-wrap gap-sm"` holds the two existing `Link`s (`Add product`, `Categories`), same `href`, role, text; their `Text` class becomes `min-h-touch rounded-control border border-border bg-surface px-md py-md text-body text-foreground`. One `AdminPanel` (no title) holds `SearchBar`, the chip row and `CategoryFilter` (same conditional). Chips: `min-h-touch min-w-touch justify-center rounded-pill border px-md` + selected `border-primary bg-primary-subtle` / unselected `border-border bg-surface`; chip `Text` selected `text-body font-semibold text-primary-text`, unselected `text-body text-foreground`. A second `AdminPanel` (no title) holds the results branch (list / `EmptyState` / `ErrorState` / `LoadingState`, same order and conditions); each row `Text` adds `border-b border-border`, same string. `Pagination` stays outside, under the results panel. `parseParams`, `change`, `STATUSES`, `PAGE_SIZE` unchanged. |
| `app/admin/products/index.tsx` | `<><Stack.Screen options={{ headerShown: false }} /><AdminProductListView /></>` (import `Stack` from `expo-router`). |

### P2.38 — Product create and edit

| File | Change |
|---|---|
| `features/admin-catalog/components/AdminProductCreateView.tsx` | Container classes. Wrap `<ProductForm …/>` in `<AdminPanel>` (no title). Props and `onSubmit` body unchanged. Add `keyboardShouldPersistTaps="handled"` to the `ScrollView` **only if** a form test needs it — default: do not add. |
| `features/admin-catalog/components/AdminProductDetailView.tsx` | Container classes on both branches (decision). Summary block: product name `Text`, the `NOT_VISIBLE_COPY` notice and the button row go into one `AdminPanel` (no title); the name `Text` keeps its class and has no header role (unchanged). Notice class → `self-start rounded-pill border border-border bg-surface-raised px-md py-xs text-body font-semibold text-foreground`. The `publishMessage` alert stays directly under that panel, unchanged. The tab content (edit `ProductForm` / preview / error / loading, same conditions and `key={product.version}`) goes inside a second `AdminPanel` (no title). Button row, handlers, `publish.mutate(!product.isActive)`, `update.mutate(toUpdateRequest(state, product.version))`, `onReload` unchanged. |
| `features/admin-catalog/components/ProductForm.tsx` | **className only.** Stale-version box: `gap-sm rounded-control border border-border p-md` → `gap-sm rounded-control border border-danger bg-surface-raised p-md`. Root `gap-md` → `gap-lg`. Nothing else. |
| `features/admin-catalog/components/VariantRowsEditor.tsx` | **className only.** Per-row card `gap-sm rounded-control border border-border p-md` → `gap-sm rounded-control border border-border bg-surface-raised p-md`. Nothing else (price inputs, `keyboardType`, error paths `price.amount`/`compareAtPrice.amount` untouched). |
| `features/admin-catalog/components/ImageKeysEditor.tsx` | **className only.** Per-row card: same change as `VariantRowsEditor`. |
| `app/admin/products/new.tsx` | Header hidden (as P2.37). |
| `app/admin/products/[id].tsx` | Add `<Stack.Screen options={{ headerShown: false }} />` before the view; keep `useLocalSearchParams`. |

### P2.39 — Categories

| File | Change |
|---|---|
| `features/admin-catalog/components/AdminCategoriesView.tsx` | Container classes. `CategoryCreateForm` stays where it is. Replace the `Text accessibilityRole="header"` "Categories" + the results branch with `<AdminPanel title="Categories">` wrapping the same branch (list / empty / error / loading, same conditions). The inner list `View` is unchanged. Remove the now-unused `Text` import (no dead imports). |
| `features/admin-catalog/components/CategoryCreateForm.tsx` | **className only.** Root `gap-md` → `gap-md rounded-card border border-border bg-surface p-lg` (the `AdminPanel` classes; same move as `OrderActions` in P2.35). Nothing else. |
| `features/admin-catalog/components/CategoryRow.tsx` | **className only.** Delete-confirm block `gap-md` → `gap-md rounded-control bg-surface-raised p-md`; edit block the same. Root row keeps `border-b border-border py-md`. Nothing else (`useEffect`, `save`, `confirmDelete`, labels untouched). |
| `app/admin/categories.tsx` | Header hidden (as P2.37). |

## Form freeze (checked by Rob in review)

Untouched (`git diff` empty): `features/admin-catalog/form-model.ts`, `api.ts`, `types.ts`, every file in `hooks/`, every `*.test.ts(x)` in `features/admin-catalog/`, `components/AdminPanel.tsx`, `app/admin/_layout.tsx`, `features/auth/**`, the shared components listed in Decisions. In the five className-only files, every changed line is a `className=` value. Price inputs keep label, `keyboardType="decimal-pad"` and error mapping; product price still goes through `toCreateRequest`/`toUpdateRequest` and the server validates it.

## Required tests

No new tests (decision). The gate is that these existing suites are byte-identical and green:

| Suite | Must still prove (as today) |
|---|---|
| `AdminProductListView.test.tsx` | URL-driven search/status/category/page; empty state |
| `AdminProductCreateView.test.tsx` | Create submit, field errors incl. `Price is not valid.`, slug conflict |
| `AdminProductDetailView.test.tsx` | Not-visible notice, publish error, stale-version reload, preview |
| `AdminCategoriesView.test.tsx`, `CategoryCreateForm.test.tsx` | Title, list, create/edit/delete flows and errors |
| `form-model.test.ts`, `api.test.ts` | Unchanged money/request mapping |
| `components/AdminPanel.test.tsx` (A1–A2) | Unchanged |

If any existing test fails after a layout change, fix the layout, never the test. If a test truly cannot pass with a planned layout (e.g. a duplicate-text collision), stop and report it with the failure output — do not edit the test.

## Build order

1. Set yourself BUSY. Run `npx jest features/admin-catalog components/AdminPanel 2>&1 | tail -n 20` on the untouched tree; quote the pass count (baseline).
2. Route files (4): header hidden.
3. P2.37 `AdminProductListView`. Run the catalog Jest suites.
4. P2.38 views, then the className-only edits in `ProductForm`, `VariantRowsEditor`, `ImageKeysEditor`. Run the suites.
5. P2.39 `AdminCategoriesView`, then className-only `CategoryCreateForm`, `CategoryRow`. Run the suites; the pass count equals the baseline.
6. `npm run format` from `frontend/apps/store` (repo config only; never ad-hoc `prettier`). Then `git -c safe.directory='*' status --short` — only the slice's files may be modified; if `format` touched any other file, report it and stop (do not revert by git).
7. Grep your diff for `style=`, `#[0-9a-fA-F]`, `-\[` (arbitrary classes): none. `git diff` on the "Form freeze" untouched list: empty.
8. Full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote `VERIFY`. Report `slice-plans/slice-P2.37-marco-report.md` (≤ 6 KB): baseline and final Jest counts, files, confirmation of the freeze checks.
9. Manual check (open for Nathaniel): at 1280 px and 375 px, `/admin/products`, `/admin/products/new`, `/admin/products/<id>` (edit and preview), `/admin/categories` show one title (no Stack header), no horizontal scroll, and the long product form scrolls to `Create product` / `Save changes`.

## Review method (Rob)

Low tier: `git diff --stat`; read the four views and route files; for the five className-only files confirm every changed line is a `className=` value; confirm the freeze list has no diff and no test file changed; full `verify.sh` in the PASS round. No mutations.

## Risks

- **`getByText` collisions:** the `Categories` link (list) and the `Categories` panel header (categories screen) are on different screens; no new text is added anywhere, so no new collision.
- **Header hidden on native:** breadcrumb + OS back replace the back button; report a dead end on iOS, don't touch `_layout.tsx`.
- **Keyboard on long forms:** the `ScrollView` already wraps the form; no new keyboard handling (out of scope).
- **Scope creep:** no new copy, no restyle of shared storefront components, no change to URL params, validation, publish/unpublish or category flows.
- One `verify.sh`/Jest run at a time.
