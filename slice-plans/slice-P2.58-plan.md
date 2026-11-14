# Slice P2.58 — Save changes disabled until the edit form has changes

Tracker: `docs/admin-dashboard-feature-tracker.md` row P2.58 (Module `admin-catalog`). Mode: Autonomous.
**Risk tier: Low.** Client-only UI gate; the server's version-checked `PUT /admin/products/{id}` and its tests are untouched. No money/stock/auth logic changes → no mutations (CLAUDE.md §5). Marco's Jest tests + full `verify.sh`.

## Read:
- This plan.
- `frontend/apps/store/features/admin-catalog/form-model.ts` (+ `form-model.test.ts`)
- `frontend/apps/store/features/admin-catalog/components/ProductForm.tsx`
- `frontend/apps/store/features/admin-catalog/components/AdminProductDetailView.tsx` (lines 74–90: `key={product.version}` remount) and its test file
- `frontend/apps/store/components/ActionButton.tsx` (already has `disabled`)
- `docs/quality-bar.md` §1, §3, §4 (definition of done). No architecture doc needed: no backend, no new endpoint.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Dirtiness = compare the **normalised request payload** of the current state with that of the baseline: new exported `hasChanges(baseline, state): boolean` in `form-model.ts`, implemented as `JSON.stringify(common(baseline, true)) !== JSON.stringify(common(state, true))` | "No changes" means "the PUT would send the same thing". Reuses the existing normaliser, so trimming, upper-casing currency, `''`→`null` for size/colour/compare-at, and dropping row `key` and image `url` are all consistent with what Save sends. Key order of `common` is fixed by construction, so stringify is deterministic | Field-by-field deep compare of `ProductFormState` (would flag the random row `key`s from `toFormState` and trailing-space edits that send an identical body) |
| Prices compared as trimmed **text**, never parsed: `49.00` → `49.0` counts as a change | form-model contract: prices are never parsed or rounded on the client | Numeric compare (introduces parsing into the client) |
| Image list **order counts** (reorder = change); thumbnail `url` ignored | Order is part of the request body; `url` is never sent | Set comparison |
| Baseline captured once in `ProductForm`: `const [baseline] = useState(initial);` alongside the existing `useState(initial)` | Baseline must be the exact snapshot the state started from; the parent rebuilds `initial` on every render | Comparing to the `initial` prop each render (works today but ties correctness to parent render behaviour) |
| Button: `disabled={isEdit && !hasChanges(baseline, state)}` on the existing `ActionButton` | `ActionButton` already takes `disabled` and sets `accessibilityState.disabled` + `Pressable disabled` + `opacity-50`. No component change | New prop / new button component |
| **Create mode unaffected** (`isEdit &&` guard) | A blank create form must stay submittable so the server returns field errors (existing create tests rely on that) | Gating Create too (out of scope, changes create spec) |
| **Re-baseline after save** = existing behaviour: `onSuccess` invalidates `admin.products.all` → refetch returns the bumped `version` → `key={product.version}` remounts `ProductForm` → new state and new baseline → Save disabled. No new code | Already wired; the slice only adds a test proving it | Manual `setBaseline(state)` on success (duplicates the remount; would also diverge from server-normalised values) |
| Failed save (400/409) keeps the form dirty and Save enabled; Reload remounts on the new version and disables Save | Unchanged error flow | — |

## Files

- `features/admin-catalog/form-model.ts` — add exported `hasChanges(baseline: ProductFormState, state: ProductFormState): boolean` (uses the private `common`). ~3 lines.
- `features/admin-catalog/components/ProductForm.tsx` — baseline `useState`, import `hasChanges`, `disabled` on the Save/Create `ActionButton`. ~3 lines.
- Tests: `form-model.test.ts`, `AdminProductDetailView.test.tsx`, `AdminProductCreateView.test.tsx` (one assertion).
- No backend, no migration, no package, no doc change.

## Existing tests (no spec change)

All three edit-mode tests that press Save edit first: line 90 changes Name, line ~166 uploads an image, line 201 presses Remove. None presses Save on a pristine form, so none changes; the upload and Remove tests become the "image change enables Save" guards. The stale test's Reload remounts on version 101 — unaffected. **No existing test may be edited or deleted**; if one fails, stop and report it.

## Build order (red first — quote the red output in the report)

1. Write the new tests below; run `npx jest features/admin-catalog` and quote the red (pristine Save enabled; `hasChanges` not exported).
2. Add `hasChanges` → form-model tests green.
3. Wire `ProductForm` → component tests green.
4. Full `bash scripts/verify.sh`.

## Required tests

`form-model.test.ts` — **test-first**, `describe('hasChanges')`:
- two `toFormState(product)` calls of the same product (different row keys) → `false`
- Name changed → `true`; Name changed only by surrounding spaces → `false`
- a variant price `49.00` → `49.0` → `true` (text, not parsed); variant `isActive` toggled → `true`
- images swapped in order → `true`; only an image `url` different → `false`; image removed → `true`

`AdminProductDetailView.test.tsx` — **test-first**:
- pristine load: `Save changes` `toBeDisabled()`; pressing it sends no PUT
- change Name → `toBeEnabled()`; type the original name back → `toBeDisabled()`
- successful save re-baselines: PUT 200, detail refetch returns `version` 101 with the new name → `waitFor` Save `toBeDisabled()` and Name shows the new value

`AdminProductCreateView.test.tsx`:
- on a blank create form `Create product` is `toBeEnabled()` (Create mode unaffected)

## Risks

- Between PUT success and the refetch completing, the form is still dirty with the old version, so a fast second press sends a PUT that gets 409 (stale). Pre-existing, server-safe (version check); not in scope → NOTE for backlog if Marco sees it.
- `weightGrams` non-integer text normalises to `0`: typing `abc` over `0` reads as unchanged. Matches what Save would send; acceptable.
