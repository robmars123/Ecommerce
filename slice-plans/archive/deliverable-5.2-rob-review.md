# Deliverable 5.2 — Review thread

Retry payment on the same order (Orders · Slice 4). Test 24 and the retry part of test 7 (§12.3 row 4; arch §8.2 rule 11, §9).

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: BLOCKED · verify.sh: not run (I run the full gate only in a PASS round). The targeted suite `RetryAndExpiryTests` was green on the unmutated tree: `Passed: 10, Failed: 0`.

Scope: committed code on `dev` (the tree was clean). I read `RetryPaymentService`, the `retry-payment` action in `OrdersController`, `PaymentAttempts.StartAttemptAsync`/`EnsureIntentAsync`, `Payment.HasCancellableIntent`, the `payments` unique indexes, `CheckoutService.CompleteAsync` (to compare), and `RetryAndExpiryTests` plus the 401 case at `CheckoutTests.cs:327`. The fresh path is right: the key is flushed first, the order is locked, status and the clock are checked under the lock, the amount is the stored `grand_total`, the cancel goes into the outbox in the same transaction, and Stripe is called only after commit with `pi:{orderId}:{n}`. Two things block. The replay path skips every guard, and the concurrent duplicate is never tested.

**Slice 4 owner gate: satisfied.** The quality-bar §8 waiver (line 132) is in Nathaniel's commit `3063ccb`, which meets my slice-4 round-3 condition.

### Invariants → code → tests

| Invariant | Where it holds | Tests that prove it |
|---|---|---|
| Owner only: 401 / 403 / 404 / 400 | `[Authorize]` + `sub` check; `RetryPaymentService.cs:41-49`; key check in the controller | 401 `CheckoutTests.cs:327`; T7 part (403, 404, 400); M3 red |
| Only from `PendingPayment` (fresh call) | `:95-99` under the order lock | T24 not-pending; M1 red |
| Not after `expires_at` by `IClock`, before the sweep (fresh call) | `:100-104` | T24 past-expiry; M2 red |
| **Same guards on a replayed key** | **Not implemented** (`:61-65`, `:86-88` → `:73`) | **None** (finding 1) |
| Attempt n+1, key `pi:{orderId}:{n}` | `PaymentAttempts.cs:23`, `:57`; `ux_payments_order_id_attempt` | T24 happy path (asserts both keys) |
| Amount = stored `grand_total`, never re-priced | `:106` passes `order.GrandTotal` | T24 happy path (2500 minor, 25.00); M4 red |
| Reservation and `expires_at` unchanged | retry only reads `GetActiveExpiryAsync` | T24 happy path |
| Superseded intent cancelled through the outbox, no external call in the tx | `PaymentAttempts.cs:18-21`; Stripe only at `:73` after commit | T24 happy path + same-key; M6 red |
| Same key twice → one attempt, same secret | short-circuit `:86-88`; PK + catch `:61-65` | sequential only; M5 green (the catch covers it) |
| **Concurrent same-key retries → one attempt** | PK + catch `:61-65` | **None** (finding 2) |
| Interaction with the 5.1 auto-refund | old attempt pays first → Paid + cancel others; a second success → T25 refund | T19, T25 (5.1, Done) |

### Mutations (each restored from a scratch copy; `cmp` clean after every one)

| # | Guard broken | Result |
|---|---|---|
| M1 | `Status != PendingPayment` check disabled (`:96`) | RED: `T24_a_retry_is_only_allowed_while_the_order_is_PendingPayment` |
| M2 | expiry check disabled (`:101`) | RED: `T24_past_the_reservation_expiry_…` |
| M3 | `owner != userId` check disabled (`:46`) | RED: `T7_part_another_customer_retrying_gets_403…` |
| M4 | amount `GrandTotal + 1` (`:106`) | RED: `T24_a_retry_adds_attempt_2…` |
| M5 | seen-key short-circuit disabled (`:86`) | GREEN: the PK + catch still returns the same attempt (expected; finding 2 is about the catch having no test of its own) |
| M6 | previous-intent cancel disabled (`PaymentAttempts.cs:18`) | RED: two T24 tests (`Failed: 2`) |

### 1. A replayed key creates an intent on an expired or cancelled order — BLOCKING   (`RetryPaymentService.cs:61-65`, `:86-88`, `:73`)
**What I saw:** when the key was seen before, you return `Started.Attempt(existing)` and go straight to `EnsureIntentAsync`. Neither path re-checks the order's status or the reservation clock. `EnsureIntentAsync` creates a new Stripe intent whenever the attempt has no provider id yet.
**Why it matters:** Stripe is down during a retry at minute 14, so you return 503 and attempt 2 is committed with no intent. At minute 16 the sweep cancels the order and releases the stock. The expiry's `RequestCancelOpenIntentAsync` cancels nothing, because attempt 2 has no provider id. The client repeats the same key, and you create a live, chargeable intent for a `Cancelled` order and return its `client_secret`. The customer pays for stock that is gone. Test 19 then refunds it and raises an alert, but rule 11 says "otherwise 409 and nothing is created". This is the Slice 4 standing challenge "payment without an order". The same thing happens on a `Paid` order: the customer gets charged twice, then refunded. Checkout already guards its replay (`CheckoutService.CompleteAsync` checks `PendingPayment`), so retry is the odd one out.
**What I want:** before `EnsureIntentAsync` on a replayed attempt (both the `seen` branch and the catch branch), read the order without a lock. Return `NotPending` if it isn't `PendingPayment`, and `ReservationExpired` if `GetActiveExpiryAsync` is null or ≤ `clock.UtcNow`. Don't touch the fresh path, because it already checked under the lock.
**How I'll check:** a new test. Gateway unavailable → retry → 503. Advance the fake clock 16 min. Same key → 409 `reservation-expired`, and `f.Gateway.Intents.Count` is unchanged. Add a second case with the sweep run and the order `Cancelled` → 409 `not-pending-payment`. Then I'll remove your new check and expect RED.
**Lesson:** an idempotent replay has to re-check every guard that comes before a side effect it can still trigger.

### 2. Concurrent same-key retries have no test — BLOCKING   (`RetryPaymentService.cs:61-65`; `RetryAndExpiryTests.cs:51`)
**What I saw:** `T24_the_same_key_twice…` sends its two calls one after the other, so the second always takes the `seen` short-circuit at `:86`. Nothing reaches the `UniqueConstraintViolationException` catch, which is the only code that handles a real double-click. M5 shows the catch works when it's forced, but no test pins it.
**Why it matters:** with a double tap on "Retry", the second request waits on the key insert, then hits the unique violation. If someone narrows or removes that catch (or renames `KeyConstraint`), that request returns 500 and the client shows an error for a payment that is actually open. The suite stays green. Rule 2 is a test-first invariant, and decision-log row "Checkout idempotency and intent" promises that "a concurrent duplicate waits on the key, then replays" for retry as well.
**What I want:** a test that sends N (≥ 4) concurrent retries with the same key, released together. Assert that all return 200 with the same `attempt` and `clientSecret`, that there are exactly 2 payment rows, one `PaymentIntentCancelRequested` and one created intent for attempt 2. Use the same concurrency helper as the checkout double-submit test.
**How I'll check:** I'll disable the catch `when` clause and expect RED, then restore it.
**Lesson:** a catch that exists only for concurrency needs a concurrent test, or it's dead code to the suite.

### 3. A superseded attempt with no intent yet is never cancelled — NOTE (in `backlog.md`)
Both `StartAttemptAsync` and `RequestCancelOpenIntentAsync` cancel only the latest attempt, and only when it already has a provider id. An intent still being created (a slow checkout call racing a retry) stays live. T25 refunds it, so no money is lost. Finding 1 closes the replay route. The leftover race is listed in the backlog.

**Changed tests:** none. Nothing weakened or deleted.

**What was good:** you flush the key before you take the order lock, so a duplicate never waits behind stock locks. Rolling the key back on rejection (`Rejected` thrown inside the transaction) means a 409 never burns the key. The happy-path test asserts the exact Stripe idempotency keys and the unchanged reservation row, not just a count.

## Round 1 — Marco → Rob

**1. Fixed.** `RetryPaymentService.cs` (`RetryAsync`, new `ReplayGuardAsync`): both replay routes (seen branch and unique-violation catch) now set `Attempt.Replay`. Before `EnsureIntentAsync` a replay reads the order and returns `NotPending` if it isn't `PendingPayment`, or `ReservationExpired` if `GetActiveExpiryAsync` is null or <= `clock.UtcNow`. The fresh path is untouched. Tests `T24_a_replayed_key_after_the_reservation_expired_...` and `..._after_the_order_was_cancelled_...` (503, then clock +16 min, same key; the second case runs the sweep first). Red before the fix: `Expected: Conflict / Actual: OK`, 2 failed of 13. Green after: 13/13 passed.
   *Deviation:* `GetActiveExpiryAsync` locks reservations and needs a transaction (my first attempt without one returned 500). So the order read is lock-free, but the guard runs in a short `InTransactionAsync` that locks the reservations briefly, as the sweep does. No external call is inside it, and the lock order (reservations only) can't invert. A true lock-free expiry read would need a new Inventory contract method, which I did not add (not in the plan).

**2. Fixed.** `T24_concurrent_retries_with_one_key_...`: 6 callers released by a `Barrier` (no sleeps). It asserts all return 200 with identical body (same attempt 2 and secret), 2 payments, 1 `PaymentIntentCancelRequested`, 2 gateway intents. 3 of 3 runs passed.
   *Mutation (M5 plus catch):* the seen short-circuit was disabled (`seen is not null && key.Length == 0`) and the catch was narrowed (`ConstraintName == "mutated"`). Result RED: `Expected: OK / Actual: InternalServerError`. Restored from a scratch copy; `cmp` identical. (My first attempt used `if (false)`, which only failed the build on the unreachable-code warning, so I did not count it.)

**Tests changed:** none weakened or deleted; 3 added.
**Runs:** `RetryAndExpiryTests` Passed 13/13. `verify.sh changed` is running in the background (it timed out at 120s in the foreground); I'll quote it in the hand-back.

## Round 2 — Rob
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`, jest 989/989.

**1. Replay guard — Confirmed.** Lock order: the guard takes the order row, then the reservation rows via `GetActiveExpiryAsync`. The fresh path in `StartAsync` takes them in the same order (order, then reservations), so the guard adds no new ordering and cannot invert. No external call runs inside the transaction; `EnsureIntentAsync` runs after it commits. The transaction is short. Accepted without a new Inventory contract: the existing contract method is the one the fresh path already uses. Mutation (replay re-check disabled): RED, 2 of 13 failed (`Expected: Conflict / Actual: OK`). Restored, `cmp` identical.

**2. Concurrent same-key test — Confirmed.** It uses `Barrier` and has no sleeps or delays. Mutation (seen short-circuit off and catch narrowed): RED, 2 tests failed with `InternalServerError`. Restored, `cmp` identical.

NOTE (backlog): the comment "No order lock" in `RetryAsync` is wrong. The guard calls `GetForUpdateAsync`, whose lock behaviour I did not verify, so fix the comment or the call.
