# Slice 9.7 gap-fill — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run (not a PASS round; your report quotes `VERIFY PASS`, jest 161 suites / 1137 tests)

The diff matches D1–D9: thin route, form moved with a link left behind, PUT body exactly `{ slug, name }`, server 409 copy shown as sent, settle-time invalidation, `ActionButton` reuse, no class assertions. The five changed tests are additions only — I checked each hunk: `admin-access` inserts `/admin/categories` into `PATHS` with a matching `'/admin'` in the Support row at the same index; nothing was loosened. Two things block: one logic bug in the row lifecycle, and the duplicate field labels you raised.

### 1. A deleted or renamed row leaves `open` set, so a re-created category appears already open — BLOCKING   (`features/admin-catalog/components/CategoryRow.tsx:45-50`, `AdminCategoriesView.tsx:18,42`)
**What I saw:** You close the row through the `mutate(..., { onSuccess: onClose })` callback. In TanStack Query v5 the hook-level `onSettled` runs and is awaited **before** the success action reaches the observer (`query-core/.../mutation.js:196-199`), and your `onSettled` awaits `invalidateQueries`, i.e. the refetch. After a delete (or a slug rename), that refetch drops the row, the `CategoryRow` unmounts, the observer has no listeners, and the mutate-level `onSuccess` is skipped (`mutationObserver.js:158`). `onClose` never runs; `open` stays `{ slug: 'tops', mode: 'delete' }`.
**Why it matters:** Parent is fixed at create, so "delete Tops, re-create `tops` under another parent" is the expected way to move a category, and the create form is on this same screen. The new `tops` row renders with "Delete Tops? … Confirm delete" already open; one stray tap deletes the category the admin just created. Same for a rename `tops`→`shirts` followed by creating a new `tops`: it appears with the editor open. Your tests don't catch it because the GET mock always returns the same fixture, so the row never unmounts.
**What I want:** Close the row in a way that doesn't depend on the row still being mounted — e.g. `update.mutateAsync(...).then(onClose, () => undefined)` and the same for delete (the error stays on the mutation state for display; `onClose` is the parent's `setOpen(null)`, which is still mounted). Any equivalent is fine if it's tested.
**How I'll check:** A new test where the GET mock returns the list without `tops` after the DELETE, then with `tops` again on a later refetch (e.g. `client.invalidateQueries({ queryKey: ['categories'] })`): `Delete Tops` button present, `Confirm delete` absent. One like it for a slug rename (`Edit Tops` present, no `Save`). Quote the red run against the current code.
**Lesson:** Mutate-level callbacks are tied to the component that called `mutate`; if your own success path can unmount that component, put the state change somewhere that outlives it.

### 2. Edit fields share the create form's labels — BLOCKING   (`CategoryRow.tsx:67-68`, test `AdminCategoriesView.test.tsx:37-39`)
**What I saw:** With an editor open, the screen has two inputs named "Category name" and two named "Category slug". The tests pick the editor's with `getAllByLabelText(...).at(-1)!`. This label choice is my plan's error (D5), not yours, and you were right to flag it.
**Why it matters:** A screen-reader user listing form controls (VoiceOver rotor, TalkBack controls) hears "Category name" twice with nothing saying which one renames Tops and which one creates a new category — typing into the wrong one creates a stray category instead of renaming. The tests also stop proving *which* field they drive: quality-bar §5 asks for queries by role and label, and "the last match" is a query by position.
**What I want:** Label the editor fields per row, the same way the buttons are: `New name for <category.name>` and `New slug for <category.name>` (the server's current name, so the label doesn't change while typing). Replace the `.at(-1)` helpers with `getByLabelText('New name for Tops')` etc.; V3's `findByLabelText('Category name')` then becomes unambiguous too. No other change to `CategoryCreateForm`.
**How I'll check:** `grep -n "at(-1)" AdminCategoriesView.test.tsx` returns nothing; V4/V5/V6 green with the new labels.
**Lesson:** Give every control a name that is unique on its screen; if a test needs element order to find a control, so does the user.

### NOTEs (to backlog.md)
- Update error-path invalidation (D8 "on settle") is not asserted; only the success path is.
- Empty line left in `AdminProductListView.tsx` where the form was removed.

**What was good:** The `parentSlug` absence is pinned at both layers (A1 and V4 parse the body), and the 409 tests use the server's exact copy for both `products` and `subcategories`, so `userMessage`'s generic conflict text can't creep back in. The pending-promise test for double submit is the right shape.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (jest 161 suites, 1141 tests)

### 1. Unmount-safe callback — Confirmed
`CategoryRow.tsx:47,51` now use `mutateAsync(...).then(onClose, () => undefined)`. The promise settles whether or not the row is still mounted, and errors still show through `update.error`/`remove.error`, so the swallowed rejection loses nothing. The fix is correct.

### 2. Test never went red — Accepted as NOTE
React Query's per-call `mutate` callbacks are skipped once the observer unmounts. Jest's batching (setTimeout) commits the unmount later than a real browser does, so the old code could pass. My round-1 diagnosis was right and Jest hides the symptom. This is UI, not a hard invariant, and the fix is structurally sound, so a test that never went red is acceptable. NOTE: the it.each guards the happy path but does not prove the unmount race.

### 3. Label ambiguity — Confirmed
Labels are `New name for <name>` / `New slug for <name>`. The test uses `getByLabelText` (lines 37-38) and no `.at(-1)` remains.

**What was good:** the comment at line 46 says why `mutateAsync` is used.
