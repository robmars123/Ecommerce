**Read:** app-context row 10.4 (line 111), §4.10 (lines 219–224), §4.8 "Low stock" bullet (line 204), §5 route `/admin/inventory` (line 261) · arch §9 admin inventory contract (09-api.md lines 49–51 only; `stockStatus` values on line 38) · §5.5 "Touch targets" and "Tests" bullets (05-frontend.md lines 63 and 65) · §11.3 (11-testing.md lines 46–57) · §13.2 row "Admin inventory screen" (grep, don't read the log) · quality-bar §1, §2, §4.
Code to read before editing: `features/admin-catalog/{api.ts, types.ts}`, `features/admin-catalog/hooks/{useAdminProductsQuery.ts, useUpdateCategoryMutation.ts}`, `features/admin-catalog/components/{AdminProductListView.tsx (page param + Pagination only), CategoryRow.tsx, AdminCategoriesView.test.tsx (lines 1–45 for the fetch-mock pattern)}`, `features/catalog/types.ts` (`StockStatus` only), `features/catalog/components/Pagination.tsx`, `lib/api-client.ts` (`ApiError`, the 409 parse near line 230, `userMessage`), `lib/api-client.test.ts` (the `available` cases near line 255), `lib/query-keys.ts`, `components/{ActionButton,TextField,ScreenTitle,EmptyState,ErrorState,LoadingState}.tsx`, `app/admin/inventory.tsx`, `tests/routes.test.tsx` (ROUTES table + `mockCatalogApi`), `tests/fixtures.ts` (one admin `make*Page` helper as a model), `tests/fetch-mock.ts`.

All paths below are under `frontend/apps/store/` unless they start with `docs/`.

# Slice 10.4 — Admin inventory screen (frontend)

Deliverable: app-context **10.4**. Builder: **Marco**. Mode: Autonomous. Out of scope: any backend change; row 10.3 (low-stock alerts — stays Unverified, this slice neither builds nor verifies it); a stock-history/audit view; search or filters; an admin nav bar; dashboard changes.

## Risk tier — Low

Adjust is a stock movement, but this slice adds **no server code**. Every stock guard — row lock, the `expectedOnHand` stale check (double-submit safety), the available ≥ 0 check, and the audit of both success and rejected attempts — lives in the Slice 2 Inventory service and is already integration-tested (10.1, 10.2 Done). The client can only move stock through those guards; the worst a client bug can do is send a request the server rejects, or show stale numbers. The frontend change is one screen plus one parsed field on `ApiError`. Review = Marco's tests + full `verify.sh` + Rob reads the diff (CLAUDE.md §5). **No mutations.** Rob still checks two things by reading: the body built from the displayed row (test 5) and the owner's no-refetch rule (test 5 GET count).

**Backend:** nothing missing. The list gives every field incl. `stockStatus`; both 409s already carry `onHand`, `reserved`, `available` (`AdminInventoryController.ConflictProblem`), and on the stale 409 `onHand` is the current value (≠ the sent `expectedOnHand`), on the insufficient 409 it equals it (`InventoryAdminService` lines 74/87). That is enough to tell the two apart without a backend change.

## 1. Decisions (recorded by Rob in §13.2 "Admin inventory screen"; app-context §5 route text updated — both done)

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | New feature `features/admin-inventory/`; route `app/admin/inventory.tsx` renders `AdminInventoryView`; title stays `Admin · Inventory` | Feature-folder rule (§5.2); title already pinned by the routes test | Putting it in `admin-catalog` (different module, different API) |
| D2 | **Owner rule.** On adjust 200: `queryClient.setQueriesData({ queryKey: queryKeys.admin.inventory.all }, patch)` where `patch` replaces the item whose `variantId` matches with the response item and returns the page object unchanged (same reference) when it is absent. **No** `invalidateQueries`, **no** `refetch`, on success or on settle. Dashboard and storefront queries are not touched either | Nathaniel: "don't reread the entire database". The 200 body is the full item shape. `setQueriesData` patches whichever cached page holds the row; ordering (product name, SKU, id) cannot change from a stock change | Invalidate the list (rereads every page the admin opens again); invalidate the dashboard (it is a projection with an as-of time, refreshed on its own) |
| D3 | List stays paged: `?page=` URL param parsed as in `AdminProductListView`, page size 20, `Pagination` component. Query key `queryKeys.admin.inventory.list(page, pageSize)` under `admin.inventory.all = ['admin', 'inventory']` | Same pattern as the product list; deep-linkable page | Infinite scroll; client-side paging of a full download |
| D4 | **Low stock = per-row flag** from the server's `stockStatus`: text `In stock` / `Low stock` / `Out of stock` / `Unknown` (and `Inactive` when `isActive` is false). `Low` uses the `warning` token, `Out` the `danger` token — always with the text, never colour alone. **No filter** | §4.10.1 asks for a per-variant flag. A client filter on a paged list silently hides low items on other pages; a server filter is a backend change nobody needs, because the dashboard "Low stock" section already lists every low variant store-wide and links here. The threshold is server config, so the client never recomputes the status | Low/Out filter chips (client-side: wrong on paged data; server-side: new backend param) |
| D5 | Adjust is **inline per row**, one row open at a time: button `Adjust <SKU>` opens `TextField`s `Quantity for <SKU>` (`keyboardType="number-pad"`) and `Reason for <SKU>`, plus `Add stock`, `Remove stock`, `Cancel`. Body = `{ variantId, delta: ±quantity, expectedOnHand: <the row's onHand as rendered when the button is pressed>, reason: reason.trim() }` | number-pad has no minus key on iOS/Android, so direction is a button, not a sign. `expectedOnHand` from the rendered row is what makes a double submit or a concurrent edit fail safely as stale (§9 contract) | A signed `Change` field; a modal; reading a fresh value just before sending (defeats the stale check) |
| D6 | Client validation mirrors the server's 400 rules only: quantity is digits only, 1–1,000,000; reason 1–500 chars after trim → inline field error, no request. **No** client check for "would go negative" | The server rejects that case **and audits the attempt** (§4.10.3); pre-blocking it in the client would skip the audit | Client-side available check |
| D7 | `ApiError` gains `onHand?: number`, parsed exactly like `available` (one type field + one line in the parse). Consumer: D8 | Needed to tell stale from insufficient; `title` is `Conflict` for both and `detail` is not parsed | Matching on `detail` text (brittle); a backend `type` per case (backend change) |
| D8 | Errors in the open row, `accessibilityRole="alert"`: **409 and `onHand !== sent expectedOnHand` (stale)** → `On hand is now {onHand}, not {expectedOnHand}. Someone else changed it. Reload to see the current numbers.` plus a `Reload` button that calls the current page query's `refetch()` (that one page only); form stays open, inputs kept, so the admin re-checks and presses again. **409 otherwise (insufficient)** → `Only {available} available; reserved stock cannot be removed.`, no reload. **400** → `fieldErrors.delta` under Quantity, `fieldErrors.reason` under Reason, anything else as the row alert. **404** → `This variant no longer exists. Reload the list.` + `Reload`. Anything else → `userMessage(error)` | Stale means the row on screen is wrong; a one-page refetch on the admin's tap is the honest fix. **Not** patching the row from the 409 body: it has counts but no `stockStatus` (threshold is server config), so the Low/Out flag would be wrong | Silent row patch from the 409 body; auto-refetch on every 409; reloading the whole list |
| D9 | On success the form closes and the row shows the response numbers. Both submit buttons use `ActionButton pending` while the mutation runs | Visible result is the patched row; `pending` stops a double tap, the stale check stops the rest | A toast (no toast component exists) |
| D10 | Route guard unchanged: Support on `/admin/inventory` → `/admin` (`adminRouteDecision`, already pinned by `admin-access.test.ts` PATHS). Server enforces `Admin` policy | Nothing to build | — |

## 2. Files

New (`features/admin-inventory/`):
- `types.ts` — `AdminInventoryItem` (the nine §9 fields; `stockStatus: StockStatus` imported from `features/catalog/types`), `AdminInventoryPage` (`items, page, pageSize, totalCount, totalPages`), `AdjustStockRequest`.
- `api.ts` — `fetchAdminInventory(page, pageSize, signal?)` → `apiGet` `/api/v1/admin/inventory` with `params: { page, pageSize }`; `adjustStock(body)` → `apiSend('POST', '/api/v1/admin/inventory/adjust', { body })` returning `AdminInventoryItem`.
- `api.test.ts`.
- `adjust-model.ts` — pure: `validateAdjust(quantity: string, reason: string)` → `{ quantity?: string; reason?: string }` errors (empty object = valid); `buildAdjustRequest(item, quantity, reason, direction: 'add' | 'remove')`; `patchInventoryPage(page | undefined, item)` (D2 updater, returns the same reference when nothing matches, `undefined` stays `undefined`); `conflictMessage(error, expectedOnHand)` → `{ message, stale: boolean }` (D8 409 rule).
- `adjust-model.test.ts`.
- `hooks/useAdminInventoryQuery.ts` — `useQuery` on D3 key.
- `hooks/useAdjustStockMutation.ts` — `useMutation(adjustStock)`, `onSuccess: setQueriesData(... patchInventoryPage)`. No `onSettled`.
- `components/AdminInventoryView.tsx` — title, list, Pagination, Empty/Error/Loading states, owns `openVariantId`.
- `components/InventoryRow.tsx` — one row: `productName · variantName`, `SKU <sku>`, `On hand N · Reserved N · Available N`, status text (D4), `Adjust <SKU>`, the inline form (D5–D9). Takes `onReload` (= list query `refetch`).
- `components/AdminInventoryView.test.tsx`.

Edited:
- `app/admin/inventory.tsx` — render `AdminInventoryView` (keep the screen thin).
- `lib/query-keys.ts` — `admin.inventory.{all, list(page, pageSize)}`.
- `lib/api-client.ts` (+ `lib/api-client.test.ts`) — D7.
- `tests/fixtures.ts` — `makeInventoryItem(overrides)` and `makeInventoryPage(items, overrides?)`.
- `tests/routes.test.tsx` — inventory row `built: true`; `mockCatalogApi` answers `GET /api/v1/admin/inventory` with `makeInventoryPage([])`.

Not touched: anything in `backend/`, `features/auth`, `features/admin-dashboard`, `ScreenStub` (still used by customers routes), docs (Rob's).

Style: `className` + tokens; every pressable `min-h-touch min-w-touch` (ActionButton already is); no hex, no `min-h-11`; no `Platform.OS`.

## 3. Build order

1. `lib/api-client.test.ts` case for `onHand` → red → D7 → green. Quote red.
2. `adjust-model.test.ts` (validation, body sign, patch identity, conflict split) → red → `adjust-model.ts`.
3. `types.ts`, `api.ts` + `api.test.ts`, query keys, hooks, fixtures.
4. `AdminInventoryView.test.tsx` test 5 (patch, no refetch) and tests 8–9 (stale/insufficient) **first** → red → components → green; then the rest.
5. Route + `routes.test.tsx`.
6. Full `bash scripts/verify.sh 2>&1 | tail -n 30` → `slice-plans/slice-10.4-marco-report.md`.

## 4. Required tests (TF = test-first, quote red)

`lib/api-client.test.ts`
- A1 (TF) 409 problem with `onHand: 7, available: 2` → `error.onHand === 7`; without `onHand` → `undefined`.

`adjust-model.test.ts`
- M1 `validateAdjust`: `''`, `'0'`, `'-1'`, `'1.5'`, `'1e3'`, `'1000001'` → quantity error; `'1'`, `'1000000'` ok; reason `''`/`'   '` and 501 chars → reason error; 500 chars ok.
- M2 `buildAdjustRequest` add → `delta: +q`, remove → `delta: -q`; `expectedOnHand` = `item.onHand`; reason trimmed.
- M3 (TF) `patchInventoryPage` replaces only the matching item, other items and paging fields unchanged; no match → **same reference**; `undefined` → `undefined`.
- M4 (TF) `conflictMessage`: `onHand ≠ expected` → `stale: true`, text names both numbers; `onHand === expected` → `stale: false`, text names `available`.

`api.test.ts`
- P1 list sends `page`/`pageSize` to `/api/v1/admin/inventory`; P2 adjust POSTs the exact body to `/api/v1/admin/inventory/adjust`.

`AdminInventoryView.test.tsx` (role/label only, fetch spy as in `AdminCategoriesView.test.tsx`)
1. Lists each row: product · variant, SKU, on hand / reserved / available, status text `Low stock`, `Out of stock`, `In stock`, and `Inactive` for an inactive variant.
2. Empty page → empty state; 3. GET 500 → error state, Retry refetches.
4. `Next page` requests `page=2`.
5. (TF, **owner rule**) Open `Adjust TS-1`, quantity `5`, reason `Restock`, `Add stock` → POST body exactly `{ variantId, delta: 5, expectedOnHand: <row onHand>, reason: 'Restock' }`; row then shows the 200 response's numbers and status; form closed; **GET `/api/v1/admin/inventory` called exactly once in the whole test**.
6. `Remove stock` sends a negative `delta`.
7. Quantity `0` or empty reason → field errors, **no POST**.
8. (TF) 409 with `onHand ≠ expectedOnHand` → stale alert text; `Reload` → GET count goes 1 → 2 with the same `page`; after reload the row shows the new on hand and the next `Add stock` sends the new `expectedOnHand`.
9. (TF) 409 with `onHand === expectedOnHand` → insufficient alert naming `available`; no `Reload` button; GET count stays 1.
10. 400 `errors.reason` → shown under Reason; 404 → alert + `Reload`.
11. Double tap on `Add stock` while pending → one POST.
12. Opening `Adjust` on a second row closes the first.

`tests/routes.test.tsx`: `/admin/inventory` renders built (stub note gone) for Admin. `admin-access.test.ts` unchanged (already covers Support → `/admin`).

Changed tests: only `routes.test.tsx` (row flips stub → built, one GET mock added) — a strengthening, not a weakening. No test deleted.

## 5. Risks

- **Refetch creep.** A future `onSettled: invalidate` would break the owner rule silently; test 5's GET count is the guard — do not loosen it to `toHaveBeenCalled()`.
- **Stale `expectedOnHand`.** Read it from the row prop at press time, never cache it in form state at open time; test 8 proves the post-reload value is sent.
- **409 misclassified.** Compare against the `expectedOnHand` actually sent in that mutation (`mutation.variables`), not the row's current value, which may have changed.
- **Dashboard lag.** The dashboard's low-stock list will not reflect an adjust until it refetches; acceptable (projection with as-of time, D2).
- **`refetchOnWindowFocus`** may reread the current page on focus; that is TanStack's default for every screen, one page only, and not this slice's concern.
