# Slice P2.54 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 268 suites / 2039 tests passed.

Low tier (static copy on a read-only screen), so no mutations beyond the J1 proof the coordinator asked for. The diff is exactly the plan: one pure function, one render line, one appended test block, one new test file, the tracker row and the decision-log row. Nothing extra.

### Checks
- **Only for Cancelled:** `totalsNote` in `features/orders/status-labels.ts` returns the const only for `'Cancelled'`, `null` otherwise. U1 covers all 8 `OrderStatus` values literally; J2 checks the 7 non-Cancelled statuses on screen.
- **Wording:** the string in `status-labels.ts`, both tests, and the 13.2 row is the plan §2.1 text, character for character. It is truthful against §8.2: rule 8 (late success on a `Cancelled` order is recorded and refunded automatically) and rule 12 (the duplicate success on an already-paid order gets the same handling, but that order is not `Cancelled`, so the note never shows there). "No payment is kept" holds in both cases, and U2 pins the absence of "never/not charged".
- **Independent of `statusNote`/`payment`:** the render depends only on `order.status` (`OrderDetailView.tsx` `Content`). J3 covers both nulls, and the case with the P2.52 card present (`Captured $0.00` shown alongside).
- **Existing tests unchanged:** `status-labels.test.ts` changed only by one added import name plus an appended `describe`. No other test file was touched. Every existing orders suite passes in the full jest run.
- **Tokens/a11y:** plain `Text` with `text-ui-sm text-muted`, the same as the P2.53 description. `TotalRow` and its labels are untouched.

### J1 proof (your report did not quote its red)
I copied `OrderDetailView.tsx` to scratch, replaced the note render with `{null}`, and ran `npx jest …totals-note.test.tsx -t J1`. Result: **red**, `getByText(NOTE)` failed at `OrderDetailView.totals-note.test.tsx:38` with `Tests: 2 failed, 18 skipped` (two Jest projects). After restoring from the scratch copy, `cmp` reported no difference (`CMP_OK`). J1 is a real test.

### 1. Pill vs. note on a late-refunded order — NOTE
**What I saw:** on a Cancelled order with an auto-refunded late success, the totals note says "No payment is kept" while the payment pill can still say "Payment: Paid" (the existing P2.52 backlog item).
**Why it matters:** the two statements are both true but read as a contradiction. This slice does not cause it, and it is now one more place where the pill disagrees.
**What I want:** nothing in this slice. I've added it to the existing backlog item.
**Lesson:** when copy depends on an edge case, check every other widget that describes the same money.

**What was good:** you kept the note's derivation in a pure, fully enumerated function, so a new status or a cancel-after-pay path fails U1 instead of silently showing wrong copy. J3's "with payment card" case ties this row to P2.52's `Captured $0.00`, which shows the one story across both widgets.
