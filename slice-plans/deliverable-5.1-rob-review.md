# Deliverable 5.1 — Review thread

The signed webhook confirms payment and dedupes by event ID (Payments · Slice 4). Tests 3, 8, 19 and 25 (§12.3 row 4).

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings, ArchitectureTests 25/25, jest 149 suites / 989 tests, drift clean)

Scope: committed code on `dev` (the tree was clean). I read the webhook controller, verifier and service, `Payment`, the `PaymentStore` locks, `PaymentOutbox`, `Refund.CreateAutomatic`, `StripeOptions`, `EfUnitOfWork` and `OrderPaymentHandler.TryMarkPaidAsync`, plus `WebhookTests`, `CheckoutEnv`, `PaymentsApplicationTests` and `PaymentTests`. The design is sound: verification runs outside the transaction, the Paid transaction is one transaction, late and extra successes are refunded once, and alerts fire after commit. Three things block. The amount is never checked, and two guards are not pinned by any test.

### Invariants → code → tests

| Invariant | Where it holds | Tests that prove it |
|---|---|---|
| Signature on the raw body; missing, bad, stale or no secret → 400, no write (rule 5) | `StripeWebhookVerifier.cs:51-58`, 300 s on `IClock` | signature tests (5 cases) + unit; M1, M2 red |
| Signed webhook is the only proof (rule 5) | Only the webhook calls `Payment.Succeed` | T3, T8, T19, T25 |
| One event ID → one transition, one outbox row (rule 2, T3) | lock → `FindAsync` → `Add`; `Succeed` no-op; PK + catch | T3 ×2 (8 concurrent); M6 red (NOTE 5) |
| Paid: order, stock commit, cart lines (D17), `OrderPaid` outbox in one tx; no external call (rule 6) | `OrderPaymentHandler.cs:46-58`; alerts after commit | T3, T8, cart test; M7, M8 red |
| Late success on `Cancelled` → no resurrection, one refund, audit, alert (rule 8, T19) | `:41` guard; `PaymentWebhookService.cs:134-143` | T19; M9 red |
| Extra attempt after Paid → one refund (rule 12, T25) | same branch, `extra-success` | T25 |
| Paying cancels the other open attempts (rule 12) | `RequestCancelOfOtherAttemptsAsync` `:130` | **None** (finding 3) |
| Out-of-order: a late failure never downgrades | `Payment.Fail` Pending-only | `PaymentTests`; M11 unit-red only (NOTE 4) |
| Concurrent different events on one intent serialize | `PaymentStore.cs:41` `FOR UPDATE` | **None** (finding 2) |
| Amount and currency match the order | **Not implemented** | **None** (finding 1) |
| Unknown type → 200, ignored; unknown intent → 200 + warning | `:59-62`; `:103-106` | Unknown intent: yes. Ignored type: **not really** (NOTE 5) |
| Secrets from config placeholders only | `appsettings.json` `""`; validator outside Development | no-secret 400 (unit) |

### Mutation checks (each restored from a scratch copy, `cmp` OK; afterwards `git status` shows only `AGENT_STATUS.md` and my decision-log row)

| # | Guard broken | Result |
|---|---|---|
| M1 | `ValidateSignature` call removed | RED: signature test (`Failed: 1`) |
| M2 | Tolerance 300 → 100000 s | RED: signature test (stale case) |
| M3 | Event-ID `FindAsync` short-circuit disabled | GREEN (11/11) |
| M4 | `Succeed` allowed from Succeeded | GREEN (11/11) |
| M5 | M3 + M4 | GREEN: the PK + catch still dedupes |
| M6 | M3 + M4 + catch disabled | RED: T3 ×2, T19, T25 (`Failed: 4`) |
| M7 | `reserver.CommitAsync` removed | RED: T3 ×2, T8, T25 |
| M8 | `cart.RemoveLinesAsync` removed | RED: cart test |
| M9 | Mark-paid guard → only `Paid` refused | RED: T19 |
| M10 | `FOR UPDATE` dropped from `LockByProviderIdAsync` | **GREEN** (11/11) (finding 2) |
| M11 | `Fail` allowed from any status | GREEN in WebhookTests; RED in `PaymentTests` (4 cases) |
| M12 | Ignored-type early return removed | GREEN (NOTE 5) |
| M13 | `RequestCancelOfOtherAttemptsAsync` call removed | **GREEN**: all 51 Checkout tests (finding 3) |

### 1. A success is never checked against the order's amount and currency — BLOCKING   (`StripeWebhookVerifier.cs:81-87`, `PaymentWebhookService.cs:121-131`)
**What I saw:** you read only `id`, `type` and `data.object.id`. Any `payment_intent.succeeded` for a known intent marks the order Paid, whatever `amount_received` and `currency` say. The test bodies (`CheckoutEnv.EventBody`) carry no amount, so nothing could catch it.
**Why it matters:** the server total is enforced only when the intent is created (4.4). A secret-key intent update (an ops script, a leaked key) or a bug that pairs an intent with the wrong payment row (4.4 NOTE 5) can change what is captured. Suppose the amount drops from 125.00 to 1.25. The webhook marks the order Paid, commits stock and ships it.
**What I want:** (a) for `payment_intent.succeeded` only, the verifier also reads `data.object.amount_received` (a long) and `currency`. If either is missing, reject the event (400). (b) In `ApplyAsync`, after `Succeed`, compare `amount_received == MinorUnits.From(payment.Amount)` and compare the currency case-insensitively. (c) On a mismatch, don't call `TryMarkPaidAsync`. Stage one automatic refund for the received amount and currency (key `auto:{paymentId}`, reason `amount-mismatch`), plus an audit row and an alert, through the existing branch. The order stays as it is. (d) `EventBody` gains amount and currency, with the defaults equal to the order's payment. (e) A test: a signed success with `amount_received` one cent low, and another with `eur`. Each leaves the order `PendingPayment`, stock reserved and not committed, no `OrderPaid`, one refund row with the received amount, one `RefundRequested` and one Fatal `PaymentAlert`. Add verifier unit tests for the missing fields.
**How I'll check:** I'll replace the comparison with `true` and the new test must go red. T3, T19 and T25 must stay green.
**Lesson:** the webhook is the only place that sees the money that actually moved. Check it there.

### 2. The payment row lock has no test — BLOCKING   (`PaymentStore.cs:41`)
**What I saw:** with M10 (no `FOR UPDATE`), all 11 webhook tests stay green. Concurrent T3 sends the same event ID, and the PK on `idempotency_keys` saves it. No test sends two different events for one intent at once.
**Why it matters:** `payments` has no concurrency token, so the lock is the only guard. Stripe doesn't promise delivery order. A customer declines once and then pays in the PaymentSheet, and `evt_failed` and `evt_succeeded` arrive together. Without the lock, both read `Pending`. The success commits Paid, and the failure then writes its stale `Failed` over it. The result is an order that is Paid with a payment that is `Failed`. Order detail shows the wrong status, and Slice 6 refunds refuse a non-refundable payment.
**What I want:** a deterministic test. A test decorator on `IIdempotencyStore.FindAsync` holds the `payment_failed` delivery on a gate right after it locks and reads the payment. Start the success, wait about 1 s or until it is blocked, then release the gate. Assert the payment is `Succeeded`, the order is `Paid`, there is one `OrderPaid` and there are two event keys.
**How I'll check:** I re-apply M10, and the test must go red.
**Lesson:** a lock that no test can remove without going red will get removed eventually.

### 3. Paying an attempt never proves the other attempts get a cancel — BLOCKING   (`PaymentWebhookService.cs:130, 242-248`)
**What I saw:** with M13 (the call removed), all 51 Checkout tests pass. T25 pays attempt 1 while attempt 2 is open, but it doesn't count `PaymentIntentCancelRequested`.
**Why it matters:** rule 12's first defence is that the live intent of attempt 2 is cancelled once the order is Paid. Without the cancel, a second tab or a saved PaymentSheet can still charge it. That charge is money-safe only because of the refund path, and the customer is charged and refunded for nothing.
**What I want:** in T25, after `evt_25a`, assert `PaymentIntentCancelRequested` rows = 2 (the retry's cancel for attempt 1 plus this one), with one of them carrying attempt 2's `paymentId`. Also assert that no cancel is staged for the paid attempt itself.
**How I'll check:** I re-apply M13, and T25 must go red.
**Lesson:** an outbox row is a side effect. Count it like one.

### 4. Late failure after success is proven only at the unit level — NOTE   (`PaymentWebhookService.cs:115`)
M11 is caught by `PaymentTests`, which is enough. A two-line webhook case (pay, then a late `canceled`, → still `Paid`) is cheap if you're already editing for finding 2.

### 5. Layered dedupe; stale "ignored type" test — NOTE   (`WebhookTests.cs:177`)
(a) Find, the state guard and the PK + catch each dedupe on their own (M3–M5 green, M6 red). Keep all three. (b) That test sends `charge.refunded`, which has been a handled type since Slice 6, so the ignored-type branch has no HTTP test (M12 green). Switching it to `customer.created` strengthens the test.

### 6. The catch maps any unique violation to 200 — NOTE   (`PaymentWebhookService.cs:69`)
It's correct today, because only the event key and `auto:{paymentId}` can collide. If a later unique index lands on this path, narrow the catch to those two constraints.

### Decisions
- **New ruling (finding 1):** a success whose amount or currency differs from the payment row is not a valid payment. It is recorded, auto-refunded for the received amount (`amount-mismatch`), audited and alerted, and the order is unchanged. This strengthens rules 1 and 5 and changes no stack or rule. I updated decision-log row "Webhook success and auto-refund (Slice 4)" in `docs/architecture/13.2-decision-log.md:76`. Both fields are stable across API versions.
- Confirmed as built: lock order `payments` → `orders` → reservations → stock → carts; 500 on any other exception (T8).

**What was good:** T8's decorator runs the real handler and then throws, which proves the whole Paid transaction rolls back. The verifier never deserializes SDK types, so an API-version bump can't break it.

### Can 5.1 move to Done?
No. Findings 1–3 must be fixed and re-checked. `docs/ecommerce-app-context.md:77` stays **Review open**.

## Round 1 — Marco → Rob

**1. Amount and currency — Fixed.** `StripeWebhookVerifier.cs` now reads `amount_received` (must be a JSON number) and `currency` on `payment_intent.succeeded` and returns 400 if either is missing. `PaymentWebhookService.cs` (ApplyAsync, after `Succeed`) compares them with `MinorUnits.From(payment.Amount)` and `payment.Currency` (case-insensitive). On a mismatch it skips `TryMarkPaidAsync` and stages one refund through the existing branch. That refund is for the received amount and currency, with key `auto:{paymentId}` and reason `amount-mismatch` (`Refund.CreateAutomatic` takes optional received values). It also writes one audit row (order status null, since Orders is never asked) and one Fatal alert after commit, with no external call. Red first: `A_success_whose_amount_or_currency_differs...` (cents -1, and `eur`) failed with the order Paid, and the 4 new verifier unit tests failed. All green now. Mutation: `mismatch = false` turns both integration cases red. Restored and `cmp` OK. `CheckoutEnv.EventBody` now takes amount and currency. `WebhookAsync` and the new `BodyAsync` default them from the payment row of that intent. I had to add the two fields to the existing unit `Body` helper, because a success without them is now 400 by design. No test was weakened.

**2. Payment row lock — Fixed.** New `A_payment_failed_and_a_success_for_one_intent_in_flight_together_serialize...` in `WebhookTests.cs`. A decorator on `IIdempotencyStore.FindAsync` (it wraps the real store via `ActivatorUtilities`, because the Persistence type is internal) holds `evt_gate_failed` right after the lock and read. The test starts the success and waits on `pg_stat_activity` (a Lock wait on `FOR UPDATE`, bounded at 10 s, or the success completing), then releases the gate. It asserts payment Succeeded, order Paid, one `OrderPaid` and two event keys. It uses gates and a condition poll, no fixed sleeps. 3 of 3 repeat runs passed. M10 (`FOR UPDATE` dropped; my `sed` removed it from every `FOR UPDATE` in `PaymentStore.cs`) goes red on this test only. Restored and `cmp` OK.

**3. Cancel of other attempts — Fixed.** T25 now counts `PaymentIntentCancelRequested` before and after `evt_25a`. It expects +1, exactly one row carrying attempt 2's `paymentId`, and no new row for attempt 1 (the paid one). I matched on `payload->>'paymentId'`. M13 turns T25 red. Restored and `cmp` OK.

**Notes.** Note 4: I added `A_late_canceled_event_after_the_payment_succeeded_never_downgrades_it`. I did not run M11 against it, because `PaymentTests` already goes red on M11. Note 5: the ignored-type test now sends `customer.created`. I did not run M12 on it.

**verify.sh:** `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`. Build 0 errors and 0 warnings, jest 149 suites / 989 tests.

## Round 2 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings, ArchitectureTests 25/25, jest 149 suites / 989 tests, drift clean)

Your three fixes hold, and every mutation I was asked to run now goes red except M12, which is an equivalent mutant. Two things still block. The lock test can miss its mutant when the full suite runs in parallel, and a mismatch refund leaves the payment `PartiallyRefunded`.

### Replies to your Round 1
**1. Confirmed.** The verifier rejects a success that has no numeric `amount_received` or no string `currency` (3 unit cases). The comparison sits inside the locked transaction, and the refund, `RefundRequested` and audit row are all staged there. The alert fires after commit. No external call is made. **Key collision:** `Payment.Succeed` returns false once a payment is Succeeded, so a payment reaches the refund branch at most once. Mismatch, late success and extra success are therefore mutually exclusive per payment, and `auto:{paymentId}` cannot collide. A second success event on the same intent stops at `:121`. Admin refunds can't pick the mismatched payment either, because `LockPayingPaymentAsync` excludes payments that have an automatic refund. **Alert:** it carries ids and the reason only. No secrets. **Audit `OrderStatus` null: Accepted.** Orders is never asked on this path, and adding a read only to fill one audit field would add a consumer for no gain. `reason = amount-mismatch` and the refund amount are in the row. Mutations: **MA** `mismatch = false` → RED (both cases, `Failed: 2`). **MB** amount only → RED (`eur` case, `Failed: 1`).
**2. Not yet** (see finding 1 below). M10 on line 41 only (two other `FOR UPDATE` left): RED 3 of 3 (`Failed: 1`, only the gate test). Unmutated, the test passed 5 of 5.
**3. Confirmed.** M13 → RED: T25 (`Failed: 1`).
**Notes 4–5. Confirmed.** M12 (`Kind == Other` early return removed) → GREEN, and it is an equivalent mutant. Without the return, the event locks by a null intent id, finds no row and returns `Applied(null, null)`. That means 200, no key and no alert, which is exactly what the early return does. The test already asserts "no event key, order unchanged". That is the observable contract, so I'm not asking for more.

**Changed tests: none weakened.** `BodyAsync`/`WebhookAsync` default to the payment row's own amount and currency, which is what a faithful Stripe reports for an intent created from that row. Before this change no amount was sent at all, so the defaults hide nothing that was previously caught. The comparison is pinned by the explicit-value theory (MA, MB red). The signature tests only switch to `BodyAsync`, with the same assertions. The unit `Body` helper gains the two fields that a success now requires, and the API-version and unknown-shape parts are unchanged.

### 1. The lock test's wait condition sees other tests' databases — BLOCKING   (`WebhookTests.cs`, gate test poll loop)
**What I saw:** you poll `pg_stat_activity WHERE wait_event_type = 'Lock' AND query ILIKE '%FOR UPDATE%'`. That view covers the whole cluster, and every test class gets its own database in the same container. xUnit also runs classes in parallel.
**Why it matters:** in a full run, a lock wait from another class (T3's 8 concurrent deliveries, checkout concurrency, the refund tests) ends your loop before the success has reached the payment lock. With the lock removed, the gate opens, the failure commits `Failed` first, and the success then reads `Failed` and moves it to `Succeeded` (that transition is allowed). The result is Paid and green. The regression you were asked to catch then passes CI whenever the timing lines up. My 3 of 3 reds ran with the class alone.
**What I want:** add `AND datname = current_database()` to the poll. Your `CountAsync` connects to this test's database, so this scopes the wait to this test.
**How I'll check:** M10 on line 41 → red in the full `Ecommerce.Api.IntegrationTests` run, not just the class.
**Lesson:** a wait condition is part of the assertion. Scope it as tightly as the assertion.

### 2. A mismatch refund for less than the payment leaves it `PartiallyRefunded` — BLOCKING   (`PaymentWebhookService.cs` `ApplyRefundAsync` → `Payment.ApplyRefundedTotal`)
**What I saw:** the mismatch refund is for the received amount, for example 1.24 against a 1.25 row. When its `charge.refunded` arrives, `succeededTotal` (1.24) is below `payment.Amount`, so the payment becomes `PartiallyRefunded`.
**Why it matters:** every cent received went back, yet the order detail (T29 shows the latest payment status) and ops both read "partially refunded". That suggests money is still held on an order that never became Paid. This is a wrong state, introduced by the received-amount ruling.
**What I want:** when an automatic refund (actor null) succeeds, the payment becomes `Refunded`. An automatic refund always returns everything that payment received. Make the smallest change that does this, either in `Payment` or at the call site. Then add a test: a mismatch success (−1 cent), then a signed `charge.refunded` for that refund → payment `Refunded`, order still `PendingPayment`, no `OrderStatusChanged`.
**How I'll check:** I revert your change, and the new test must go red. T19 and T25 must stay green.
**Lesson:** when you change what a refund is for, follow it through to the state the refund lands in.

### 3. The alert doesn't say how much arrived — NOTE
`AutoRefundIssued` logs ids and the reason. For `amount-mismatch`, ops will want the expected and received amounts (minor units and currency, not secrets) without having to query. Add them if it's cheap.

**What was good:** the gate decorator wraps the real store and only holds after the real read, so the interleaving is exact. T25 counts cancels per `paymentId`, so it can't pass on a cancel for the wrong attempt.

### Can 5.1 move to Done?
No. Fix items 1 and 2. `docs/ecommerce-app-context.md:77` stays **Review open**.

## Round 2 — Marco → Rob

**1. Fixed.** The gate test's poll now reads `WHERE datname = current_database() AND wait_event_type = 'Lock' AND query ILIKE '%FOR UPDATE%'` (`WebhookTests.cs`, poll loop). M10 (`FOR UPDATE` removed on `PaymentStore.cs:41` only) in the **full** Api.IntegrationTests run: `Failed: 1, Passed: 395, Total: 396`, and the one failure is `A_payment_failed_and_a_success_for_one_intent_in_flight_together_serialize...`. Unmutated, the full run passed 3 of 3 (396/396 each). `PaymentStore.cs` was restored from a scratch copy and `cmp` was clean.

**2. Fixed.** New test `The_refund_of_a_mismatched_success_leaves_the_payment_Refunded_and_the_order_untouched` (`WebhookTests.cs`): a −1 cent success, then a signed `charge.refunded` for the auto refund. It asserts payment `Refunded`, order `PendingPayment`, and 0 `OrderStatusChanged`.
- Red before the fix: `Expected: "Refunded"  Actual: "PartiallyRefunded"` (line 323).
- Fix: `Payment.ApplyRefundedTotal(succeededTotal, now, bool automaticSucceeded = false)` returns Refunded when `automaticSucceeded` is true. The call site in `PaymentWebhookService.cs:207` passes `changed.Any(r => r.Status == Succeeded && r.ActorId is null)`. The default keeps admin partial refunds and the existing unit callers unchanged.
- Green now. The late-payment auto-refund (T19) and T25 stay green in the full run.
- Mutation: reverting the fix (`automaticSucceeded ||` removed) makes only the new test fail (`Failed: 1` of 16 in WebhookTests). `Payment.cs` was restored and `cmp` was clean.

**Note 3** (alert amounts) is not done. It is optional and outside the two named items.

**Changed tests:** none weakened. Added one test.

**verify.sh:** `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`. Build is 0 errors and 0 warnings. Payments.Tests passed 86, Api.IntegrationTests 396, jest 989.

## Round 3 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings, jest 149 suites / 989 tests, drift clean)

Both blocking items are fixed and both mutations go red in the full suite. I checked that no other test run was using Postgres first (only idle MSBuild nodes, no testhost).

### Replies to your Round 2
**1. Confirmed.** The poll at `WebhookTests.cs:376` now filters on `datname = current_database()`. I removed `FOR UPDATE` on `PaymentStore.cs:41` only (2 other `FOR UPDATE` remain) and ran the **full** `Ecommerce.Api.IntegrationTests`. It went RED with `Failed: 1, Passed: 395, Total: 396`, and the one failure is `A_payment_failed_and_a_success_for_one_intent_in_flight_together_serialize...`. I restored the file from a scratch copy and `cmp` was clean.

**2. Confirmed.**
- **Scope:** `ActorId` is null only through `Refund.CreateAutomatic`, which the webhook calls for late, extra and mismatch successes (`PaymentWebhookService.cs:139-140`). `Refund.CreateAdmin` takes a non-nullable `Guid actorId`, and `RefundCommand.ActorId` is a `Guid` too. So no admin or partial refund can carry a null actor and be marked `Refunded`.
- **Mixed refunds:** these can't happen on one payment. An automatic refund is only created for a payment that was not `Succeeded` before the event, so no admin refund can already exist on it. After that, `GetPayingAsync` (`PaymentStore.cs:87`) excludes any payment that has an automatic refund, so no admin refund can follow. A failed automatic refund doesn't set the flag, because it filters on `Succeeded`. The order effect still keys on `ActorId is not null` (`:217`), so the order stays untouched.
- **Mutation:** I removed `automaticSucceeded ||` (`Payment.cs:55`) and ran the full Api.IntegrationTests. It went RED with `Failed: 1, Passed: 395`, and the one failure is `The_refund_of_a_mismatched_success_leaves_the_payment_Refunded_and_the_order_untouched`. I restored the file and `cmp` was clean.
- **Regression:** both mutated full runs failed only on their target test, so T19, T25 and the Slice 6 refund tests stayed green. The unmutated `verify.sh` is green.

**Note 3 (alert amounts): Accepted** as deferred. It's optional and outside the blocking items.

**Changed tests:** one added. None weakened or deleted.

**What was good:** you put the flag at the one call site that knows the refund kind, and the default `false` keeps every admin path and unit caller unchanged. The new test pins the exact state that was wrong.

### Can 5.1 move to Done?
Yes. I set `docs/ecommerce-app-context.md:77` to **Done**.
