# Deliverable 4.1 — Review thread

Stock reservation: lock in variant-ID order, reserve, commit on Paid, expire (Inventory · Slices 2/4).

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings; jest 149 suites / 976 tests; drift clean)

Scope: the committed code on `dev` at `463c858`, read fresh. Inventory changed only in `a74fbd7`. `3063ccb` touched only `OrderPaymentHandler.cs` (OrderPaid payload fields and an `OrderStatusChanged` outbox row on expiry), and that does not change locking or stock. The reservation path holds the invariants it is meant to hold. Every guard I broke turned at least one test red. I found no bug and nothing blocking.

### Invariants → code → tests

| Invariant (arch §3.4 / §8.2) | Where it holds | Tests that prove it |
|---|---|---|
| Lock stock rows in variant-ID order, decided in SQL | `InventoryStore.cs:26` (one `SELECT … ORDER BY product_variant_id FOR UPDATE`) | `Stock_rows_are_locked_in_database_uuid_order…`, `T1_twenty_parallel_reservations_over_two_variants_in_opposite_line_order…` |
| No oversell; check every line, then change nothing unless all fit | `InventoryReserver.cs:29-49`; `InventoryStock.Reserve` refuses more than available; DB CHECK | **Test 1**: `T1_one_unit_ten_parallel…` (reserver), `CheckoutTests.T1_ten_users_race…POST_checkout…`; `A_partly_short_order_reserves_nothing…` |
| Reserve inside the caller's transaction, together with the order | `InventoryStore.RequireTransaction`; `CheckoutService.PlaceAsync` reserves, inserts the order and the payment in one `InTransactionAsync` | `Reserving_outside_a_transaction_throws`; **Test 2** (same key → 1 order, mutation-checked in Slice 4 round 2) |
| Commit only on Paid: signed webhook, event deduped, PendingPayment only | `PaymentWebhookService.cs:52-56` verifies the signature outside the transaction, `:103` dedupes; `OrderPaymentHandler.cs:41` checks the status, `:48` commits | **Tests 3** (×2, serial and 8 concurrent), **8**, **19**, **25**; `T4_an_order_paid_before_the_sweep…` |
| Commit once: reservations, then stock | `InventoryReserver.CommitAsync` locks Active rows only; `InventoryReservation.Commit` refuses non-Active | `AssertPaidOnceAsync` (on_hand −qty, reserved 0, 1 Committed row); `InventoryCommitTests` |
| Expiry: IClock-driven, one transaction per order, release once, deadline re-checked under the lock | `ReservationExpiryService.cs:24, 53-70`; `InventoryStore.cs:52` (`status = 'Active' … FOR UPDATE`) | **Test 4** (×3 in `RetryAndExpiryTests`); `Two_sweeps_at_the_same_time_release_each_reservation_exactly_once`; `A_reservation_whose_deadline_moved…`; `A_cancelled_order_with_nothing_due_fails…` |
| Late Paid after expiry → no resurrection, one refund | Expiry cancels the order and releases stock in one transaction. The webhook sees `Cancelled` and stages `auto:{paymentId}` through the outbox | **Test 19** (stock stays (10, 0); duplicate and second event → still 1 refund) |
| One global lock order: payments → orders → reservations → stock → carts | Webhook: payment, order, reservations, stock, cart. Sweep: order, reservations, stock, with payments read only (`PaymentStore.GetLatestAsync` is `AsNoTracking`, no lock). Retry: order, reservations | Covered by the decision-log row "Payments module and lock order". No path locks an order and then a payment |
| No external call inside a DB transaction | Intent cancel and refund are outbox rows (`PaymentAttempts.RequestCancelOpenIntentAsync`); `EnsureIntentAsync` runs after commit | `Rule6_the_gateway_never_sees_an_open_transaction…` |
| IClock, not `DateTime.UtcNow` | No `DateTime(Offset).UtcNow` in `Modules/Inventory` or `OrderPaymentHandler` (grep) | Fake clock in Tests 4, 19, 24 |
| Boundaries (§6.2) | `Ecommerce.Inventory` references SharedKernel, Persistence, and the Contracts of Inventory, Catalog, Identity and Orders. The only public type is `InventoryModule`. No other module touches `inventory_*` tables or entities (grep) | `Ecommerce.ArchitectureTests` (25 passed) |

### Mutation checks (each one broken, run, then restored from a scratch copy; `git diff --stat -- backend` is empty afterwards)

| # | Guard broken | Result |
|---|---|---|
| M1 | `InventoryStore.cs:26`: removed `ORDER BY product_variant_id` | RED: `Stock_rows_are_locked_in_database_uuid_order…` — `Collections differ`, rows came back in heap (insert) order. `Failed: 1, Passed: 7` |
| M2 | `InventoryReserver.cs:29`: locked one row per line in the caller's order | RED: `T1_twenty_parallel…opposite_line_order…` — `Npgsql.PostgresException : 40P01: deadlock detected`. `Failed: 1, Passed: 7` |
| M3 | `OrderPaymentHandler.cs:41`: PendingPayment guard disabled (`if (false && …)`) | RED: T19, T25. `Failed: 2, Passed: 19` |
| M4 | `OrderPaymentHandler.cs:48`: removed `reserver.CommitAsync` | RED: T3 ×2, T8, T25, `T4_an_order_paid_before_the_sweep…`. `Failed: 5, Passed: 16` |
| M5 | `InventoryStore.cs:52`: removed `status = 'Active'` from the reservation lock | RED: `Two_sweeps_at_the_same_time_release_each_reservation_exactly_once`. `Failed: 1, Passed: 24` |
| M6 | `ReservationExpiryService.cs:55`: removed the under-lock `ExpiresAt <= now` re-check | RED: `A_reservation_whose_deadline_moved…`, `A_cancelled_order_with_nothing_due…`. `Failed: 2, Passed: 26` |

### 1. The opposite-order test alone does not pin the ORDER BY — NOTE   (`ReserveConcurrencyTests.cs:47`)
**What I saw:** with M1 applied, the 20-way opposite-order test stayed green. One statement locks rows in heap order, and heap order is the same for every caller, so it cannot deadlock. The uuid-order test is the one that catches it.
**Why it matters:** it doesn't today, because the pair of tests covers both failure modes (M1 and M2). If someone deletes the uuid-order test as "redundant", the ORDER BY is no longer pinned.
**What I want:** nothing now. Keep both tests. The uuid-order test is the guard on the ORDER BY.

### 2. Paid after `expires_at`, before the sweep, has no test — NOTE   (`OrderPaymentHandler.cs:41-48`)
**What I saw:** a success webhook for a `PendingPayment` order whose reservation is overdue but not yet swept commits the reservation and marks the order Paid. That is correct. The stock was never released, so nothing is oversold. It also matches §8.2 rules 3 and 7 and the decision-log row "Webhook success and auto-refund": the deciding fact is the order's status, not the clock, and only the worker expires. No test pins it, though. Test 24 pins only the retry side of "before the sweep".
**What I want (next touch of this area):** one integration test. Advance the clock past the TTL, deliver the webhook, then expect Paid, (8, 0), 1 Committed, and 0 from the next sweep.

### 3. Sweep vs. webhook race is proven serially, not concurrently — NOTE
**What I saw:** both orders are tested: Test 19 (sweep first) and `T4_an_order_paid_before_the_sweep` (webhook first). Both paths lock the order row first (`OrderPaymentHandler.cs:36, 64`), so they cannot interleave. A concurrent test would add little. I'm not asking for one.

### 4. Sweep starvation on a poison batch — NOTE   (`InventoryStore.cs:40-47`, `ReservationExpiryService.cs:24`)
**What I saw:** the sweep takes the first 100 due order IDs in ID order. If 100 or more orders failed on every tick, no later order would ever be reached. That would need corrupt data, such as `quantity_reserved` below a reservation's quantity, which the CHECK constraint and the domain methods prevent. The failures are logged as an `AggregateException`. Not a bug today. Revisit if expiry starts failing in operation.

### Decisions
- None new. "A Paid webhook after the TTL but before the sweep commits" follows from the existing rules: §8.2 rule 7 (only the worker expires), rule 11 (only retry checks the clock), and the webhook row (only the status decides). No decision-log row added.
- Ruling on the Slice 4 test-first waiver: in Slice 4 round 3, my only open item was that Nathaniel ratify the §8 line by committing it or editing it himself. `git blame docs/quality-bar.md:130-132` shows the "Granted:" line in commit `3063ccb` by `robmars123`, the owner account, and its expiry "end of Slice 4" has not passed. I count that as ratified. The waiver covers the test-first order only. Every numbered test above exists and passes, and each mutation went red.

**What was good:** the lock order is decided in SQL, with a comment explaining why (uuid and Guid sort differently), and a test fixes the uuid order. The expiry transaction cancels the order and releases stock together, and it throws to roll back if it ever cancels an order with nothing due. That one choice is what makes "late Paid" safe.

### Can 4.1 move to Done?
Yes. The code review is PASS, `verify.sh` passes, no bug is open, and the waiver item from Slice 4 round 3 is closed by the owner commit. Updating `docs/ecommerce-app-context.md:71` to **Done** is Nathaniel's call; I have not edited it.
