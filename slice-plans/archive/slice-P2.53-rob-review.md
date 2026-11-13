# Slice P2.53 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 errors / 0 warnings, jest 266 suites / 2001 tests, drift clean.

Low tier, so no mutations; I read the whole slice diff and the new Jest file. It matches the plan: one additive, server-derived field, built only from constants, with the ownership path untouched. I found no blocking issue and no bug.

### What I checked

1. **Reuse, no duplicated text** (`OrderStatusNotes.cs`). `Describe` sits next to `For`. You pulled out one private `IsReservationExpiry`, and `For` and `Describe` both call it, so the ordinal compare with `Order.ReservationExpired` exists in one place only. `CancelledGeneric` is a single const used by both the P2.50 alert (`CancelledOther`) and the generic `Cancelled` description. Its value is unchanged, so P2.50's U/I tests still pass without edits. The switch covers all 8 statuses, and the discard arm throws, as the plan says.
2. **Reason is only compared, never output.** In `Describe`, `reason` appears only as the argument to `IsReservationExpiry`. Every arm returns a literal or a const. U3 runs every status with `"Internal: VIP, skip fraud check"` and checks both that "VIP" is absent and that the result equals `Describe(s, null)`. The equality check is the stronger proof: no reason can change the output except the expiry code. I3 proves the same over the wire with a real override row (`Reason` plus a `ChangedBy` Guid). U2 pins ordinal matching (`RESERVATION-EXPIRED` → generic).
3. **Real-domain guard.** U4 is split into two facts, `CancelForExpiry` and `MarkPaid → RefundInFull`, and both use `order.History.Last()`. If someone renames `reservation-expired`, U4 goes red. I accept the split: it is clearer than one fact covering two paths.
4. **403 carries no description** (I4). B's order has a Paid history row. A's GET returns 403, and the body does not contain `"Your payment was confirmed."`. The mapping runs only after `GetAsync` returns Ok, so this is the right shape of proof.
5. **Client only renders server text.** `OrderTimeline.tsx` adds one `<Text className="text-ui-sm text-muted">{entry.description}</Text>` (tokens only) and has no copy map. `types.ts` makes the field required. J3 renders `'Text only the server knows.'` verbatim, which proves the client derives nothing.
6. **Changed test data weakens nothing.**
   - O9 (`OrderDetailView.test.tsx`): the two literals only gained `description`, and Prettier reflowed them onto more lines. No assertion, query or other value changed, and `reason: 'payment_timeout'` is still there.
   - `order-fixtures.ts`: the 4 default entries gained the §2.1 literals and nothing else changed.
   - No other existing test was edited (I checked `git diff --stat`).
7. **Boundaries.** Only Orders files changed. `TimelineEntryResponse` stays `internal`. There are no csproj changes, no migration (drift is clean), and no new package.
8. **Docs.** The `09-api.md` line 69 contract, the decision-log row and the backlog entry match the code.

### 1. Duplicated `Order.Place` setup in the test file — NOTE   (`OrderStatusNotesTests.cs:248`)
**What I saw:** You added a `NewOrder()` helper, but the existing P2.50 test (`Expired_cancellation_from_the_real_domain_gives_the_expiry_note_and_alert`) still builds its order inline with the same five lines.
**Why it matters:** It doesn't break anything. It is just two copies of the setup to update if `Order.Place` changes signature. The plan said to leave existing tests unchanged, so you were right not to touch it.
**What I want:** Nothing in this slice. It is logged in the backlog.
**Lesson:** When a slice adds a test helper, note any older copies it could replace, and leave the change for a later slice.

### Out of scope, as briefed
The raw `reason` red tag that can show admin override free text was already there before this slice. It is in the backlog (P2.53 plan entry), and this slice doesn't widen it: I3 and U3 cover that. I ignored `CLAUDE.md`, `.claude/` and the P2.52 archive moves.

**What was good:** U3 checks that `Describe(s, reason)` equals `Describe(s, null)` instead of only checking that "VIP" is absent. That proves the reason can't affect the output for any input, not just that one string doesn't appear. Using one `IsReservationExpiry` for both the status note and the timeline also means the alert and the timeline can never disagree about whether an order expired.
