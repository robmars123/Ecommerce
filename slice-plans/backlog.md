# Backlog — non-blocking review notes

| From | Note | Suggested action |
|---|---|---|
| 5.2 R1 note 3 | A superseded payment attempt whose intent isn't created yet (a slow checkout call racing a retry, or an expiry racing a just-committed retry) gets no cancel request. `PaymentAttempts.StartAttemptAsync` and `RequestCancelOpenIntentAsync` cancel only the latest attempt, and only if it has a provider id. No money is lost: T19/T25 auto-refund. | In `EnsureIntentAsync`, refuse to create an intent for an attempt that is no longer the latest, or for an order that is no longer `PendingPayment`. |
- 5.2 guard comment 'No order lock' inaccurate (GetForUpdateAsync).
