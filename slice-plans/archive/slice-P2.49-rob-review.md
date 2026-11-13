# Slice P2.49 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 258 suites / 1973 tests

The diff is what the plan asked for and nothing more. It has no backend, package or `app/` change. No existing test was edited. The hook is 70 lines, the UI adds two small components, and every guard has a test that fails when the guard is removed. I re-ran M1–M4 myself. Out-of-slice changes (`CLAUDE.md`, `.claude/`, the P2.48 archive moves) were not reviewed, as instructed.

### What I checked, and how

**1. The merge body never carries prices. Confirmed.** (`useReorder.ts:17-27`)
You build `reorderLines` explicitly as `{ variantId, quantity }` and never spread the `OrderItem`. H1 uses `toEqual` on the parsed body and also checks for the absence of `unitPrice`, `lineTotal`, `price`, `sku` and `name`. **M3 (mine):** I added `unitPrice: item.unitPrice` to the pushed line. H1 and H4 went red (4 failed across 2 jest projects). I restored the file and `cmp` showed it identical. The server would also reject the extra field with a 400 (T22 bad-bodies test), so a leak breaks the feature loudly. It can never price anything.

**2. Ownership cannot be bypassed. Confirmed.** (`OrderDetailView.tsx:304-330`)
`Header`, and so `useReorder(order.items)`, only renders from `query.data` of `useOrderQuery(id)`, which is `GET /orders/{id}`. A 403 or 404 renders `ErrorState`, with no `Header` and no button. The hook takes no id and does no other fetch. The items it can send are exactly the lines the server returned to this user. Those are also only `{variantId, quantity}`, which the user may already send via `POST /cart/items`, so this adds no new capability. **M4 (mine):** `OrderQueryService.cs:52` `order.UserId != userId` → `false`. T7 went red (`Expected: Forbidden / Actual: OK`, Failed 1 of 2). I restored the file and `cmp` showed it identical.

**3. Key reuse and drop. Confirmed.** (`useReorder.ts:46, 56-58`)
`keyRef.current ??= newIdempotencyKey()` keeps the key across network, timeout and 5xx failures. Only `ApiError.kind === 'conflict'` (409 → `conflict`, `api-client.ts:105`) clears it. H3 covers all three steps in one test: network failure, then a retry with the same key and body, then a 409, then a new key. S4 covers the same retry through the real UI with a 500. **M2 (mine):** I changed `??=` to `=`. H3 went red (2 failed). I restored the file and `cmp` showed it identical. I checked the 409 drop by reading the code: without it, `keysSent[2]` would equal `keys[0]`, and H3's last assertion would fail.

**4. Double-tap guard. Confirmed.** (`useReorder.ts:42`)
The `inFlight` ref is set synchronously before `setState`, so a second press before re-render returns early. `done` is a ref too, so it holds after success without waiting for state. **M1 (mine):** I made it stricter than the plan and removed only `inFlight.current ||`, leaving the `done` check in place. H2 went red (`Expected number of calls: 1, Received: 2`, 2 failed). This proves H2 tests the in-flight guard itself, not the done guard. I restored the file and `cmp` showed it identical. The visible half also works: S2 and S7 assert `toBeDisabled()` while pending and after done.

**5. PendingPayment hiding. Confirmed.** (`OrderDetailView.tsx:181`)
The rule is "not PendingPayment", not an allow-list, as planned, and S5 covers it. The hook stays mounted, so an order that turns Paid on screen gets the button without losing state. That is correct.

**6. Your three accessibility concerns. Accepted, all three.**
- (1) `summary` on an inner `accessible` View, with "View cart" outside it. Correct. Grouping the link inside an `accessible` View would hide it from TalkBack and VoiceOver as a separate control.
- (2) `accessibilityRole="alert"` on the message `Text` matches the app's existing pattern (`ErrorState`, `TextField`, admin forms), so it is consistent.
- (3) One `useReorder` in `Header`, shared by the button and the panel, is the right place. It is one state source, and the panel can never disagree with the button.

### 7. Expired-then-cancelled order can stack on leftover cart lines — NOTE
If a `PendingPayment` order expires to `Cancelled`, its lines may still be in the cart (§8.2). Reorder then sums onto them. The per-line result shows the true total, so nothing is hidden. This is the plan's decision to show Reorder for Cancelled. Logged in `backlog.md`.

### 8. Result panel is not a live region — NOTE
On Android the "Added to your cart" panel is not announced, and only the button label change is heard. Logged in `backlog.md`.

**What was good:** Your M1 broke more than the in-flight check (H6 also went red). My narrower M1 shows H2 still catches the in-flight guard on its own, so that test is sharp. Also, S7 holds the merge on a deferred promise instead of using fake timers, so the pending state is tested for real.

Mutations (mine): M1 red · M2 red · M3 red · M4 red; each restored from a scratch copy, `cmp` identical. `git status` shows no backend file changed.
