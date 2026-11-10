**Read:** app-context row 9.7 (line 106) and §5 route `/admin/categories` (line 260) · arch §5.2 rules, §5.3 routes, §5.5 "Touch targets" and "Tests" bullets (05-frontend.md lines 60–64) · §9 row `POST /admin/categories · PUT … · DELETE …` (09-api.md line 23) · §11.3 (11-testing.md line 46) · §13.2 rows "Category edit and delete" and "Admin categories screen" (grep, don't read the log) · quality-bar §1, §2, §4.
Code to read before editing: `features/admin-catalog/{api.ts, api.test.ts, types.ts}`, `components/{CategoryCreateForm.tsx, CategoryCreateForm.test.tsx, AdminProductListView.tsx}`, `hooks/useCreateCategoryMutation.ts`, `features/catalog/hooks/useCategoriesQuery.ts`, `features/catalog/types.ts` (`Category`), `lib/api-client.ts` (`ApiError.fieldErrors`, `userMessage` only), `lib/query-keys.ts`, `components/{ActionButton,TextField,ScreenTitle,EmptyState,ErrorState,LoadingState}.tsx`, `app/admin/inventory.tsx` (route shape), `features/admin-dashboard/components/DashboardView.tsx` (`QUICK_ACTIONS` only) and its test's "offers the quick actions" case, `features/auth/admin-access.test.ts`, `tests/routes.test.tsx`, `tests/fixtures.ts` (`categories`). Pattern for an inline open/confirm flow: `features/admin-orders/components/OrderActions.tsx`.

All paths below are under `frontend/apps/store/` unless they start with `docs/`.

# Gap-fill 9.7 — Admin categories screen (frontend)

Deliverable: app-context **9.7**. Builder: **Marco**. Mode: Autonomous. Out of scope: any backend change, reparenting, a parent picker/dropdown, the 9.6 image-upload picker, admin nav bar.

## Risk tier — Low

Screen and layout over existing, already-reviewed endpoints (9.3, 9.4). No money, stock, payment, idempotency or state transition. Auth is enforced by the server (`[Authorize(Policy = Admin)]` on the controller, integration-tested in 9.4 IC3) and hidden in the UI by the existing `adminRouteDecision` (Support is not in `SUPPORT_PREFIXES`, so `/admin/categories` already redirects Support to `/admin` — pinned by one test below). Review = Marco's tests + full `verify.sh` + Rob reads the diff (CLAUDE.md §5). **No mutations.**

**Backend:** nothing missing. `GET /categories` gives `{slug, name, parentSlug}`, enough to show the parent's name by looking up `parentSlug` in the same list. `PUT` / `DELETE` and their 404/409 bodies (field keys `slug`, `products`, `subcategories`) are in place.

## 1. Decisions (recorded in §13.2 "Admin categories screen"; app-context §5 route row added — both by Rob, done)

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | One route `app/admin/categories.tsx` → `AdminCategoriesView`. Title `Admin · Categories` | All category management is short enough for one screen; matches `inventory.tsx` | `/admin/categories/[slug]` edit route (one more route + test for two text fields) |
| D2 | `CategoryCreateForm` **moves** to the categories screen, unchanged; `AdminProductListView` drops it and gains a `Categories` link (`/admin/categories`) beside `Add product` | One place to manage categories; the product list stops carrying an unrelated form. The link keeps the old path one tap away | Keeping the form in both places (two consumers of one form, two places to test) |
| D3 | Nav: add `{ href: '/admin/categories', label: 'Categories' }` to the dashboard `QUICK_ACTIONS` (widen its `href` union) | The admin area has no nav bar (`_layout.tsx` is a bare `Stack`); quick actions are the admin's nav | Adding an admin nav bar (unplanned feature) |
| D4 | List = public `useCategoriesQuery()` (existing). Row text: `name · slug` and `Top level` or `Under <parent name>` (parent name looked up by `parentSlug` in the same list; if not found, show the slug). Order as the server returns it | No new endpoint or query key; the parent is visible as the scope asks | A new admin GET; client-side tree sorting (not asked) |
| D5 | Edit is **inline per row**: `Edit` button opens `TextField`s `Category name` / `Category slug` prefilled, plus `Save` and `Cancel`. Parent shown as read-only text. Body sent is exactly `{ slug, name }` (trimmed) — **never** `parentSlug` (server answers 400 on it). Only one row is open at a time (edit or delete) | Parent is fixed at create (§13.2 "Category edit and delete") | Editable parent field |
| D6 | Delete is an inline two-step: `Delete` → text `Delete <name>? Products and subcategories must be moved first.` + `Confirm delete` + `Cancel`. No `Alert.alert` / `window.confirm` | `Alert.alert` is a no-op on React Native Web; inline works on every platform and is testable by role and label | Native alert; modal |
| D7 | Errors: edit 400/409 → `fieldErrors.slug` / `fieldErrors.name` under the matching field; delete 409 → the server's `fieldErrors.products[0]` or `fieldErrors.subcategories[0]` in an `accessibilityRole="alert"` text in that row; anything else (404, network, 500) → `userMessage(error)` alert in that row. The confirm stays open on error | The server's 409 messages are the user copy (they say what to do); `userMessage` for `conflict` would say "changed while editing", which is wrong here | Generic message for all 409s |
| D8 | On success: edit closes the row editor; delete removes the row (refetch). Mutations **invalidate on settle** (success or error): update → `queryKeys.categories.all`, `queryKeys.admin.products.all`, `queryKeys.products.all`; delete → `queryKeys.categories.all` | A 404 means another admin deleted or renamed it — refetching shows the truth. A rename changes the category slug/name embedded in cached product lists | Invalidating only on success (stale list after a 404) |
| D9 | Buttons use the existing `ActionButton` (already `min-h-touch min-w-touch`, disabled while pending so a double tap can't send twice). No new pressable styles, no new shared component | Touch-target rule §5.5 is met by reuse; nothing new to assert | A new `SecondaryButton` (no second consumer this slice) |

No package, no new query key, no backend, no new shared component.

## 2. Files and contracts

### API and types — `features/admin-catalog/`
- `types.ts` — `export type UpdateCategoryRequest = { slug: string; name: string };`
- `api.ts` — `const CATEGORIES = '/api/v1/admin/categories';` (use it in `createCategory` too)
  - `updateCategory(currentSlug: string, body: UpdateCategoryRequest): Promise<Category>` → `apiSend<Category>('PUT', \`${CATEGORIES}/${encodeURIComponent(currentSlug)}\`, { body })`
  - `deleteCategory(slug: string): Promise<void>` → `apiSend<void>('DELETE', …)` (204 → empty object, `api-client.ts:121`)

### Hooks — `features/admin-catalog/hooks/`
- **New** `useUpdateCategoryMutation.ts` — `useMutation({ mutationFn: ({ currentSlug, body }: { currentSlug: string; body: UpdateCategoryRequest }) => updateCategory(currentSlug, body), onSettled: … })` per D8. Input type is a named type, exported.
- **New** `useDeleteCategoryMutation.ts` — `mutationFn: deleteCategory`, `onSettled` per D8.

### Components — `features/admin-catalog/components/`
- **New** `AdminCategoriesView.tsx` — `ScrollView` (`gap-md p-lg`), `ScreenTitle "Admin · Categories"`, `CategoryCreateForm`, header `Categories`, then the list: loading → `LoadingState`; error → `ErrorState` with retry; empty → `EmptyState "No categories yet."`; else one `CategoryRow` per item. Holds `openSlug: string | null` and `mode: 'edit' | 'delete' | null` so only one row is open (D5).
- **New** `CategoryRow.tsx` — props `{ category: Category; parentName: string | null; mode: 'edit' | 'delete' | null; onOpen(mode); onClose() }`. Owns its edit field state (prefilled when opened) and calls the two mutation hooks. Renders per D4–D7. Button labels: `Edit <name>`, `Delete <name>` (accessible labels unique per row — pass `label` to `ActionButton` exactly that string), `Save`, `Cancel`, `Confirm delete`.
  - Field errors: `error instanceof ApiError ? error.fieldErrors ?? {} : {}` (same as `CategoryCreateForm`).
  - Reset the mutation (`mutation.reset()`) when the row closes, so a stale error doesn't show on reopen.
- `AdminProductListView.tsx` — remove the `CategoryCreateForm` import and render (line 128); add `<Link href="/admin/categories" …>Categories</Link>` with the same classes as the `Add product` link (D2).
- `CategoryCreateForm.tsx` — **no change.**

### Route
- **New** `app/admin/categories.tsx` — thin: `export default function AdminCategoriesScreen() { return <AdminCategoriesView />; }`.

### Dashboard
- `features/admin-dashboard/components/DashboardView.tsx` — D3 only.

### Tests (see §4)
- **New** `components/AdminCategoriesView.test.tsx`, `hooks` covered through it and through `api.test.ts`.
- `api.test.ts`, `DashboardView.test.tsx`, `AdminProductListView.test.tsx`, `features/auth/admin-access.test.ts`, `tests/routes.test.tsx` — extended.

### Docs (Rob, done)
- `docs/ecommerce-app-context.md` §5 — route row `/admin/categories` (Admin).
- `docs/architecture/13.2-decision-log.md` — row "Admin categories screen".
- App-context 9.7 status cell: coordinator after PASS (Marco does not edit docs).

## 3. Build order

1. `types.ts` + `api.ts` + `api.test.ts` cases (red → green).
2. Hooks.
3. `AdminCategoriesView.test.tsx` written first against the role/label contract in §4 (quote one red run — the import fails or `Admin · Categories` isn't found), then `CategoryRow` + `AdminCategoriesView` → green.
4. Route file + `routes.test.tsx` row; `admin-access.test.ts` case.
5. Move the form (D2) + link; dashboard quick action (D3); their test updates.
6. `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line in `slice-9.7-gapfill-marco-report.md`.

## 4. Required tests

No §11.2 numbered or F test belongs to 9.7. All below are required for PASS. Assert by role, label and text (§11.3); **no class-string assertions**; no new style assertions (D9 reuses `ActionButton`). Mock `fetch` with `jest.spyOn(globalThis, 'fetch')`, helpers from `tests/fetch-mock` and `tests/render-with-query` as `CategoryCreateForm.test.tsx` does. Fixture: `categories` from `tests/fixtures` (`clothing`, `tops` under `clothing`).

### `api.test.ts`
- A1 `updateCategory('tops', { slug: 'shirts', name: 'Shirts' })` → `PUT …/admin/categories/tops`, body exactly `{ slug, name }` (no `parentSlug` key).
- A2 `updateCategory` URL-encodes the current slug.
- A3 `deleteCategory('tops')` → `DELETE …/admin/categories/tops`, resolves on 204.

### `components/AdminCategoriesView.test.tsx` (★ = written first)
- V1 ★ lists both categories with parent: `Clothing` row shows `Top level`; `Tops` row shows `Under Clothing`.
- V2 shows `EmptyState` text for `{ items: [] }`; shows the error with a `Try again`-style retry (use `ErrorState`'s real label) that refetches.
- V3 the create form is on the screen (`Category name` field and `Add category` button present).
- V4 ★ edit happy: press `Edit Tops` → fields prefilled `Tops`/`tops`; change to `Shirts`/`shirts`; `Save` → one `PUT …/tops` with body `{ slug: 'shirts', name: 'Shirts' }` (assert `parentSlug` absent); editor closes; `invalidateQueries` called with `['categories']`, `['admin', 'products']` and `['products']`.
- V5 ★ edit 409 taken slug: server `problemResponse(409, { errors: { slug: ['That slug is already used.'] } })` → that text under the slug field; editor stays open with the typed values.
- V6 edit `Cancel` closes without a request; reopening shows no stale error.
- V7 ★ delete needs confirm: press `Delete Tops` → **no** request yet, confirm text and `Confirm delete` shown; `Cancel` → closed, still no request. Reopen → `Confirm delete` → one `DELETE …/tops`; `invalidateQueries` with `['categories']`.
- V8 ★ delete 409 products: `errors: { products: ['This category still has products. Move them to another category first.'] }` → that exact text as an `alert`; row and confirm stay.
- V9 ★ delete 409 subcategories: same, key `subcategories`, on `Delete Clothing`.
- V10 delete 404 → `userMessage` not-found text as an `alert`; categories refetched (`invalidateQueries` with `['categories']`).
- V11 only one row open: open edit on `Tops`, then press `Delete Clothing` → the `Tops` editor is gone.
- V12 double submit: while the PUT is pending (unresolved promise), `Save` is disabled (`accessibilityState.disabled`) and a second press sends nothing.

### Other
- `AdminProductListView.test.tsx`: `Categories` link present with `href` `/admin/categories`; `Add category` button **absent** (replace any test that asserted the form there — list it in the report as a moved test, not a deleted one).
- `DashboardView.test.tsx`: add `Categories` to the quick-actions label loop.
- `features/auth/admin-access.test.ts`: `adminRouteDecision('Support', '/admin/categories')` → `'/admin'`; `'Admin'` → `null`.
- `tests/routes.test.tsx`: row `['admin/categories.tsx', '/admin/categories', 'Admin · Categories', true, 'Admin']` (the `/api/v1/categories` mock already exists at line 48).
- `CategoryCreateForm.test.tsx`: unchanged; must stay green.

### Mutations
None (Low tier, §5).

## 5. Risks

- **Sending `parentSlug` on PUT → 400** (global unmapped-member rule). V4 and A1 pin the body shape.
- **Wrong 409 copy.** `userMessage` maps every 409 to "changed while you were editing". V5, V8, V9 pin that the server's field message is shown instead.
- **Stale caches after rename.** Product lists embed `category {slug, name}`; D8 invalidation covers them. Old `?category=old-slug` URLs give 200 empty by design (§13.2 "Category edit and delete").
- **Accessible label collisions.** Two rows with the same visible `Edit` label break `getByRole`; labels include the category name (`Edit Tops`). Category names aren't unique server-side — acceptable for v1 (tests use distinct names); if two categories share a name the buttons still work, only test lookup is ambiguous.
- **Dev seeder** re-creates renamed/deleted seeded categories on restart (accepted in 9.4) — a manual dev check may look like the edit "didn't stick".
