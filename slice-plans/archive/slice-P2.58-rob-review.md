# Slice P2.58 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`. Jest 272 suites / 2111 tests, build 0 warnings.

This is a small diff that follows the plan. `hasChanges` reuses the private `common(…, true)` normaliser, so "dirty" means exactly "the PUT would send something different". `ProductForm` captures `baseline` once and gates only edit mode (`isEdit &&`). There are no backend, package or doc changes. No existing test was edited or deleted.

### Required tests: all present
- `form-model.test.ts` `hasChanges`: same product loaded twice gives false; a name change gives true, but surrounding spaces only give false; price `49.0` is compared as text and gives true; an `isActive` toggle gives true; image reorder gives true; a url-only difference gives false; a removed image gives true.
- `AdminProductDetailView.test.tsx` `save gate` test 1: Save is disabled on a pristine form, **pressing disabled Save sends no PUT** (`calls(spy,'PUT','/a-1')` has length 0), editing the name enables Save, and typing the original name back disables it again.
- `save gate` test 2: **Save is disabled again after a save that bumps the version** (PUT 200, the refetch returns v101, `waitFor` sees Save disabled, and Name shows `My edit`).
- `AdminProductCreateView.test.tsx`: `Create product` is enabled on a blank form.

### Check I ran: is test 2 a real check?
`ActionButton` disables itself while `pending`, so test 2's `waitFor(toBeDisabled)` could have passed during the in-flight PUT without ever proving the re-baseline. I copied the test to a temporary probe file whose refetch always returns v100 (no version bump, so no remount) and ran it: **it failed** at line 161 (`toBeDisabled`). The test therefore checks the remount and not just the pending state. I deleted the probe file afterwards; `git status` shows no stray files.

### 1. Double press before refetch completes — NOTE (already in plan Risks)
This window existed before the slice. The server's version check returns 409, so it is safe. Nothing new to add to the backlog.

**What was good:** the dirty check reuses the request normaliser instead of a field-by-field deep compare, so random row keys and trailing spaces cannot mark the form as dirty. The re-baseline needed no new code because the existing `key={product.version}` remount already does it.
