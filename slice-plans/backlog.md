# Backlog — non-blocking review notes

| From | Note | Suggested action |
|---|---|---|
| 5.2 R1 note 3 | A superseded payment attempt whose intent isn't created yet (a slow checkout call racing a retry, or an expiry racing a just-committed retry) gets no cancel request. `PaymentAttempts.StartAttemptAsync` and `RequestCancelOpenIntentAsync` cancel only the latest attempt, and only if it has a provider id. No money is lost: T19/T25 auto-refund. | In `EnsureIntentAsync`, refuse to create an intent for an attempt that is no longer the latest, or for an order that is no longer `PendingPayment`. |
- 5.2 guard comment 'No order lock' inaccurate (GetForUpdateAsync).

- 6b (Rob, plan): split `docs/architecture/13.2-decision-log.md` (~39 KB) per the ~10 KB file rule; `09-api.md` and `07-data-model.md` are also just over 10 KB.
- 6b (Rob, plan): Slice 6 outbox consumers keep the anti-join scan without a checkpoint; measure in the Slice 7 slow-query pass.
- 6b R1 (Rob): `ProjectionWorker.TickAsync` wraps all projectors in one try; an infrastructure exception from one projector ends the tick before the next runs. Wrap each `RunOnceAsync` loop in its own try/log.
- 6b R1 (Rob): `tests/app/admin.test.tsx` F7b Support-redirect filter uses `includes('/api/v1/admin/dashboard')`; tighten to an exact allow-list of the two dashboard paths.
- 5 gap-fill (Rob, plan): admin ship form takes a typed carrier code; a carrier picker needs a read of `Shipping:Carriers` (no §9 row yet).
- 5 gap-fill (Rob, plan): no "Unfulfillable" (6b saga) button on the admin order detail; the endpoint exists, the button is unscheduled.
- 5 gap-fill R1 (Rob): every list endpoint computes `Skip((page - 1) * pageSize)` in `int` with no upper bound on `page` (admin orders, catalog, admin products, returns); `page=2147483647` overflows to a negative OFFSET and answers 500. Fix once for all lists (cap `page`, or compute the offset in `long` and return an empty page past the end).
- 5 gap-fill R1 (Rob): `AdminOrderListView` shows the previous page's rows when a later fetch fails (`keepPreviousData` hides `isError` while `data` exists), and a page past the end shows "No orders match." with no pager to go back. Show an inline error with retry, and keep the pager when `totalPages > 0`.
- 9.4 gap-fill R1 (Rob): no unit test drives `AdminCategoryService.DeleteAsync` to `Ok`; the fake store's `Removed` list is never asserted (IC7 covers it over HTTP). Add one happy-delete unit test asserting `Removed` and one save.
- 9.4 gap-fill R1 (Rob): IC5 asserts only the 404 status, not the ProblemDetails body the plan named; assert `type`/`status` in the body.
- 9.5 gap-fill R1 (Rob): plan D13's success log line (product id, key, length) was dropped because Catalog Application may not reference Microsoft.Extensions.Logging. An upload is only traceable through the request log; add one Information line in `AdminProductsController.Upload` (or an Infrastructure decorator) if audit of uploads is wanted. Never log config values.
- 9.5 gap-fill R1 (Rob): a non-multipart POST to an `[Consumes]` action answers 404 (the anonymous `MapFallback` outranks MVC's 415 endpoint), here and on any future `[Consumes]` action. Harmless (nothing reaches the action), but the status is misleading; revisit if a client needs a 415.
- 9.5 gap-fill R1 (Rob): orphaned objects (uploaded, never saved on a product) need a periodic sweep; Slice 7 deploy smoke must upload one image to real Spaces and fetch it through the CDN URL (D11 checksum/chunk settings are unverified in CI).
