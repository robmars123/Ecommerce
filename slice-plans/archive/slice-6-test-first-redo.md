# Slice 6 test-first redo (T26-T28, outbox processor)
Method: stub only the guard, run that test (Release, real Postgres), restore, `cmp` OK, re-run. P = Payments/Application, O = Orders/Application.

| Test | Guard (file:line) | Red line | Green | Changed |
|---|---|---|---|---|
| T26 ceiling | P/RefundService.cs:106 `CanCover` | Expected 2 Accepted, Actual 5 | pass | no |
| T26 parallel lock | P/../Infrastructure/PaymentStore.cs:~63 `FOR UPDATE OF p` | Expected 2, Actual 5 | pass | no |
| T26 Failed frees amount | RefundService.cs:103 (Pending sum; stub adds Failed) | Expected Accepted, Actual Conflict | pass | no |
| T26 audit row for losers | RefundService.cs:109 `refund.rejected` | Expected 3, Actual 0 | pass | no |
| T27 same key twice | RefundService.cs:60,87,151 Replay | Expected Accepted, Actual Conflict | pass | no |
| T27 different body | RefundService.cs:155 hash compare | Expected Conflict, Actual Accepted | pass | no |
| T27 parallel same key | RefundService.cs:87 in-lock replay + :69 unique-key catch | Stub :87 only = green (the catch backs it). Stub both = red, Accepted vs InternalServerError | pass | no |
| T27 consumer retry | PaymentConsumers.cs:16 `re:{refundId}` (random key) | key Strings differ, 2 of 2 | pass | no |
| T27 clean run once | same key guard, PaymentConsumers.cs:16 | Expected 1, Actual 0 | pass | no |
| T27 duplicate webhook | Refund.cs:42 `Succeed` Pending check | Expected 1, Actual 2 | pass | no |
| T27 duplicate event id | PaymentWebhookService.cs:~175 event-key check | Green when stubbed alone: redundant with Refund.cs:42 (red above) and the unique event key. | pass | no |
| T28 partial then PartiallyRefunded | Payment.cs:55 | Actual "Succeeded" | pass | no |
| T28 reach captured then Refunded | PaymentWebhookService.cs:~207 `succeededTotal >= payment.Amount` | Actual "Processing" / "Paid" (2 cases) | pass | no |
| T28 full on Shipped = 409 | RefundService.cs:146 | Expected Conflict, Actual Accepted | pass | no |
| T28 bad statuses = 409 | RefundService.cs:147 | Conflict vs Accepted (2 of 4 cases red) | pass | no |
| T28 ship/process blocked while full refund Pending | OrderFulfillmentService.cs:72 | Expected Conflict, Actual NoContent | pass | no |
| T28 ship works after Failed | RefundState.cs:17 (Pending sum; stub adds Failed) | Expected NoContent, Actual Conflict. | pass | no |
| T28 Delivered+return to Returned | OrderRefunds.cs:36 `ReturnInFull` | Expected OK, Actual InternalServerError | pass | no |
| T28 return becomes Refunded (full) | OrderRefunds.cs:43 | Actual "Approved" | pass | no |
| T28 return Refunded (partial) | OrderRefunds.cs:48 | Actual "Approved" | pass | no |
| T28 audit actor/reason | RefundService.cs:135 (reason) | Failed (reason null) | pass | no |
| T28 audit before/after | RefundService.cs:134 `pendingAfter` | Assert.Contains: sub-string not found | pass | no |
| T28 totals unchanged | none: no code writes totals. A negative assertion, so no guard to stub. | n/a | n/a | no |
| Outbox once + Done | OutboxProcessor.cs:66 `p.event_id IS NULL` | Expected 0, Actual 1 | pass | no |
| Outbox per consumer | :64 `p.consumer = {name}` | Failed | pass | no |
| Outbox type filter | :65 | Expected 0, Actual 1 | pass | no |
| Outbox late commit | :60-69 anti-join (stub: sequence watermark) | Expected 1, Actual 0 | pass | no |
| Outbox backoff | :114 `DelayAfter` | Expected 03:28:03, Actual 03:28:01 | pass | no |
| Outbox dead letter | :106 `MaxAttempts` | Expected DeadLetter, Actual Retrying | pass | no |
| Outbox handler outside tx | :89 (stub opens a tx) | Failed | pass | no |
| Outbox one worker | :134-136 advisory lock | Expected 1, Actual 2 | pass | no |

Result: no test was too weak; none changed. Two rows have layered guards (parallel same key, duplicate event id), so a single-layer stub stays green by design.
Production: all stubs restored, `cmp` OK. `git diff --stat -- backend/src` shows only RetryPaymentService.cs, which was already modified before this task.
