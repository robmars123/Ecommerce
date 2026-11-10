# Slice 10.4 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 warnings, jest 167 suites / 1205 tests.

Low tier as planned: I read the whole frontend diff and every new file in `features/admin-inventory/`, no mutations. The diff matches the plan (D1–D10) with no backend change and no extra abstraction; the owner rule holds in the code, not only in your report.

### What I checked by reading
- **Adjust body** (`adjust-model.ts` `buildAdjustRequest`, called from `InventoryRow.tsx` `submit`): `{ variantId, delta: ±Number(quantity), expectedOnHand: item.onHand, reason: reason.trim() }`. `item` is the row prop at press time, not form state captured at open, so after `Reload` the new on-hand is sent — test 8 pins `expectedOnHand: 14`. Test 5 pins the exact body with `toEqual`; test 6 the negative sign; P2 the wire body.
- **Owner rule** (`useAdjustStockMutation.ts`): only `onSuccess: setQueriesData({ queryKey: admin.inventory.all }, patchInventoryPage)`. No `onSettled`, no `invalidateQueries`, no `refetch`. `patchInventoryPage` returns the same reference on no match (M3 `toBe(page)`), so other cached pages don't re-render. The only refetches are the user's `Reload`/`Try again` taps, wired to the current page's `refetch()`.
- **GET counts:** test 5 `gets(spy)` `toHaveLength(1)` (exact, not `toHaveBeenCalled`); test 8 goes 1 → 2 with `page=1`; test 9 stays 1. `gets` filters by method and exact pathname, so the POST never inflates it.
- **409 split** uses `adjust.variables?.expectedOnHand` (the value actually sent), as the plan's risk asked, not the row's current value.
- **Changed tests:** `routes.test.tsx` flips the row to `built: true` (now asserts the stub note is gone) and adds a GET mock; `api-client.test.ts` appends A1 without touching existing cases; `fixtures.ts` adds two helpers. None weakened. No test deleted or skipped.

### Your deviations
1. **AdjustForm mounted only while open — Accepted.** Simpler than a reset effect: no effect, no race, and unmount also drops the old mutation's error. Same file, no new abstraction.
2. **Fallback text for a 409 without `available` — Accepted.** `available` is optional on `ApiError`, so the branch must say something; one ternary, no new path.
3. **Missing-module red for M1–M4 and the view tests — Accepted at Low tier.** No server guard is involved, A1 had a real behavioural red, and the guards that matter (GET count, `expectedOnHand` from the row) are pinned by exact assertions. At High tier I would want a behavioural red (e.g. test 5 red against an `onSettled` invalidate); see NOTE 2.
4. **`text-warning` contrast — Accepted for this slice.** Status is always text, so colour is not the only signal; a darker warning-text token is a theme change for the backlog (NOTE 3).

### 1. Test 9 fixture sends a 409 the server cannot send — NOTE   (`AdminInventoryView.test.tsx`, insufficient case)
**What I saw:** your insufficient 409 returns `onHand: 10, reserved: 2, available: -1`. The server's `available` is the current `OnHand − Reserved` (`InventoryAdminService`), so it would be `8`, and the UI would read "Only 8 available".
**Why it matters:** the test passes either way, but it documents a message ("Only -1 available") a user never sees and hides the real copy.
**What I want:** `available: 8` and the expected text `Only 8 available; reserved stock cannot be removed.` → backlog.

### 2. Test 5 never seen red against a refetch — NOTE
The GET-count assertion is the owner-rule guard. One-time check worth doing when this file is next touched: add `onSettled: () => queryClient.invalidateQueries(...)` locally, confirm test 5 goes red, restore. → backlog.

### 3. Warning text contrast — NOTE
`text-warning` on the light surface may fall under 4.5:1; consider a `warning-text` token. → backlog.

**What was good:** classifying the 409 against `mutation.variables` instead of the row, and keeping `expectedOnHand` out of form state — both are the two places this screen could have silently defeated the server's stale check.
