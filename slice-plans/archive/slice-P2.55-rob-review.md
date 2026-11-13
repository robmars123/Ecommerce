# Slice P2.55 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` · jest `Tests: 2069 passed, 2069 total` · build `0 Warning(s)`

Low tier (CLAUDE.md §5): I read the diff and the three new files, no mutations. The change matches the plan: one pure helper, one display change in `AccountBar`, one invalidation effect in `PaymentStep`, and tests T1–T15. No backend, package, route or `lib/` change. No blocking findings.

### What I checked

1. **Badge = total quantity, hidden while loading/error.** `useCartCount` is unchanged; it sums `line.quantity` and returns `null` until `server.data` exists. T8 (2 + 1 → 3, `null` first), T9 (500 → stays `null`; the test client has `retry: false`), T12 (`"Cart, 3 items"`). Confirmed.
2. **PaymentStep refetch once per transition, never writes the cart.** `PaymentStep.tsx:45–49`: deps are `[client, status]`; a poll that returns the same status does not re-run the effect. It calls `invalidateQueries` only, no `setQueryData`, so the server cart wins (§5.2). T14 was red first (quoted in your report); T15 covers the `PendingPayment` side. Any non-pending status (Paid, Cancelled, …) refetches the cart once, which is correct: it is a read of the server's answer.
3. **Avatar rule.** `avatar-initial.ts`: `displayName.trim() || email`, `Array.from(...)[0]`, `toUpperCase()`, `'?'` fallback. T1–T4 cover each branch, T3 the surrogate pair. The `restoring` branch still returns `null` and the signed-out branch is untouched (T7). Confirmed.
4. **Accessibility.** One `accessible` View labelled `Signed in as {name}` wraps the initial and the name; Admin and Log out stay outside the group, so they remain separate stops. Cart link labels unchanged. T5/T6 query the label and find the initial inside it.
5. **Non-behavioural test edits.** `TopNav.test.tsx` `routes(client = createTestQueryClient())`: every existing call passes no argument, so each still gets a fresh client per call, exactly as before. `CheckoutView.test.tsx`: import lines only, plus a separate `renderWith` helper; the existing `render` and all F4/F5/F11 tests are untouched. Neither weakens an existing assertion. Accepted.
6. **`shownName` export.** The visible name changes only when `displayName` is blank or padded: before, a blank name rendered an empty `Text`; now it shows the email, as the plan specified (same rule as `SidebarFooter`). The existing "Customer User" assertion is unchanged and green. Accepted.
7. **Tokens only.** `rounded-pill bg-primary-subtle text-primary-text text-small font-bold`, `h-[28px] w-[28px]`; no hex.

### 1. T9 asserts `null` without proving the query reached the error state — NOTE   (`features/cart/hooks/useCartCount.test.tsx:43`)
**What I saw:** you wait for `fetch` to be called, flush one `act`, then assert `null`. That would also pass if the response had not been processed yet.
**What I want (later):** assert `client.getQueryState(queryKeys.cart)?.status === 'error'` in a `waitFor` before the `null` check. Backlog.

**What was good:** keeping `shownName` and `avatarInitial` in one module so the label, the name and the initial cannot drift apart; and T13, which proves the real end-to-end path (invalidate → refetch → badge gone) on the mounted header rather than only the hook.
