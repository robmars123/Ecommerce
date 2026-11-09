# Backlog — non-blocking review notes

| From | Note | Suggested action |
|---|---|---|
| 5.2 R1 note 3 | A superseded payment attempt whose intent isn't created yet (a slow checkout call racing a retry, or an expiry racing a just-committed retry) gets no cancel request. `PaymentAttempts.StartAttemptAsync` and `RequestCancelOpenIntentAsync` cancel only the latest attempt, and only if it has a provider id. No money is lost: T19/T25 auto-refund. | In `EnsureIntentAsync`, refuse to create an intent for an attempt that is no longer the latest, or for an order that is no longer `PendingPayment`. |
- 5.2 guard comment 'No order lock' inaccurate (GetForUpdateAsync).

- 6b (Rob, plan): split `docs/architecture/13.2-decision-log.md` (~39 KB) per the ~10 KB file rule; `09-api.md` and `07-data-model.md` are also just over 10 KB.
- 6b (Rob, plan): Slice 6 outbox consumers keep the anti-join scan without a checkpoint; measure in the Slice 7 slow-query pass.
- 6b R1 (Rob): `ProjectionWorker.TickAsync` wraps all projectors in one try; an infrastructure exception from one projector ends the tick before the next runs. Wrap each `RunOnceAsync` loop in its own try/log.
- 6b R1 (Rob): `tests/app/admin.test.tsx` F7b Support-redirect filter uses `includes('/api/v1/admin/dashboard')`; tighten to an exact allow-list of the two dashboard paths.
