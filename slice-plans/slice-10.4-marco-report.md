# Slice 10.4 - Admin inventory screen - Marco report

Concerns: (1) Low-stock text uses `text-warning` (#e0a43a) as the plan says; contrast on a light surface may be weak. The status is always text, never colour alone, so it is readable; Rob may want a darker warning-text token later. (2) 10.3 untouched, as planned.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed:
- `tests/routes.test.tsx` - `/admin/inventory` row `built: false` -> `true`; `mockCatalogApi` answers `GET /api/v1/admin/inventory` with `makeInventoryPage([])`; import adds `makeInventoryPage`. Strengthening: the stub note must now be gone.
- `lib/api-client.test.ts` - appended one new describe (A1, `onHand`); no existing case edited.
- `tests/fixtures.ts` - added `makeInventoryItem`, `makeInventoryPage` (+ one type import). No existing helper changed.
No test deleted, skipped or loosened.

## Changed (all under frontend/apps/store/)
New `features/admin-inventory/`: `types.ts`, `api.ts`, `api.test.ts`, `adjust-model.ts`, `adjust-model.test.ts`, `hooks/useAdminInventoryQuery.ts`, `hooks/useAdjustStockMutation.ts`, `components/AdminInventoryView.tsx`, `components/InventoryRow.tsx`, `components/AdminInventoryView.test.tsx`.
Edited: `app/admin/inventory.tsx` (renders the view), `lib/query-keys.ts` (`admin.inventory.{all,list}`), `lib/api-client.ts` (`onHand?` on `ApiError`, D7), plus the three test files above. No backend, doc, package or config change.

## Owner rule (no reread)
`useAdjustStockMutation` has only `onSuccess: setQueriesData({queryKey: admin.inventory.all}, patchInventoryPage)`. No `onSettled`, no invalidate, no refetch. View test 5 asserts the POST body `{variantId, delta: 5, expectedOnHand: 10, reason: 'Restock'}`, the row's new numbers and status, the form closed, the other row untouched, and `gets(spy)` `toHaveLength(1)` (exactly one inventory GET). Test 8: `Reload` takes the GET count 1 -> 2 with `page=1`; test 9 (insufficient) stays at 1.

## Red evidence (test-first)
- A1: `Expected: 7 / Received: undefined` at `lib/api-client.test.ts:286`; green after the D7 field and parse line.
- `adjust-model.test.ts` (M1-M4) and `AdminInventoryView.test.tsx` (incl. 5, 8, 9): both red with `Cannot find module './adjust-model'` / `'./AdminInventoryView'` before the code existed (a missing-module red, not a behavioural one); green after.

## Deviations / notes
- The adjust form is a child component `AdjustForm` inside `InventoryRow.tsx`, mounted only while open, instead of an effect that resets fields. Reason: a reset effect raced the test's typing. Same behaviour (empty fields, no stale error on each open), no extra file.
- Added one client message the plan did not spell out: a 409 with no `available` shows `This change conflicts with the current stock.`
- Empty-state text is `No variants yet.`; field-error texts are `Enter a whole number from 1 to 1,000,000.` / `Enter a reason of 1 to 500 characters.`
- Tests `await` each `fireEvent` (repo convention in `AdminCategoriesView.test.tsx`).
- Mutations: none (Low tier).

## verify.sh
VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)
PASS build 0 Error(s)|0 Warning(s); PASS drift; PASS tsc clean; PASS jest Test Suites: 167 passed, 167 total|Tests: 1205 passed, 1205 total; PASS export.

## Quality-bar section 2
- [x] Tests for the slice's behaviour (A1, M1-M4, P1-P2, view 1-12, routes). 
- [x] Owner rule enforced and tested (GET count 1).
- [x] verify.sh passes in full.
- [x] No secrets, no new packages, no git writes, no stray processes.
- [x] Touch targets via `ActionButton` (min-h-touch/min-w-touch); no hex, no Platform.OS.
- [ ] N/A: mutations (Low tier); backend/migration/docs (none changed).
