# Slice 6 — Notifications + Returns — Plan (Rob, 2026-11-07)

Mode: Autonomous · Author: Rob · Builder: Marco

**Read:** arch §2, §3.4, §6.1–6.2, §7.1 rows `refunds`, `return_requests`, `outbox_events`, `processed_events`, `idempotency_keys` · §7.3 (order, payment, refund, return) · §8.4 · §8.5–8.6 (`08.5-refund-fulfillment-admin.md`) · §9 rows for `/orders/{id}/return-request`, `/admin/orders*`, `/admin/returns*`, `/auth/password-reset`, and the "Refund contract" paragraph · §10.1, §10.3 · §11.2 rows 3, 7, 9, 26–29 · §11.3 rows F8, F12, F13 · §12.3 row 6 + its carry-ins · decision log (grep): `Password-reset delivery`, `Admin refund ceiling`, `Payments module and lock order`, `Slice 6 outbox runtime` · `slice-plans/slice-4-plan.md` D2, D8, D9 · quality-bar §1, §2, §4.

## 0. Preconditions and order

- **Slice 4 gate.** Slice 4 is BLOCKED only on Nathaniel's quality-bar §8 waiver line, and no Slice 4 code is open. You may **build** Slice 6 now. I will **not PASS** Slice 6 until Slice 4 is PASS. If Nathaniel refuses the waiver, Slice 4's tests are redone test-first in `Payments`/`Orders`, which this slice also edits. In that case stop and rebase your work on that round.
- **Archive due.** Once Slice 4 is PASS, `slice-plans/slice-4-*` (plan, baseline, report, review) move to `slice-plans/archive/`. Nobody moves them while Slice 4 is open. Not your task.
- **Baseline.** Before you start, save `verify.sh` test counts to `slice-plans/slice-6-baseline.txt`.

## 1. Scope

**In:**
- The outbox runtime (dispatcher, retry, dead letter) and these consumers:
  - Stripe cancel and refund (they close Slice 4 D9)
  - order emails (Paid confirmation, Shipped)
  - SignalR live status
  - password reset
- Admin refund endpoint and the refund webhook.
- The returns flow: customer request; admin list, approve, decline.
- `GET /admin/orders/{id}` (the F13 screen needs it).
- Password-reset delivery moves to the outbox.
- Slice 5 carry-ins: Test 7 refund and return parts, Test 29 `refunds[]`, F12 refund rows.
- Frontend:
  - On `/orders/[id]`: return request (F8), refunds list, live status.
  - On `/admin/orders/[id]`: detail + refund form (F13).
  - On `/admin/returns`: list, approve, decline.

**Out:**
- The `/admin/orders` list, filter, and process/ship/deliver/override UI (feature 11).
- `/admin/inventory` (10) and `/admin/customers` (12). See §8.
- `consumer_checkpoints`, projectors, gap handling, replay, Tests 11–18 (6b).
- A real email provider: it stays a placeholder (§2 open decision). No provider package and no keys.
- Customer cancel: no §9 endpoint (Slice 4 doc gap stands).
- Rate limits (7).

## 2. Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | **Outbox runtime in `Ecommerce.Persistence`:** `OutboxProcessor.RunOnceAsync(consumer)` plus a hosted `OutboxWorker` registered by the host. `IOutboxConsumer { string Name; IReadOnlySet<string> EventTypes; Task HandleAsync(OutboxMessage, CancellationToken) }` lives in SharedKernel, and modules register implementations. | One runtime for every module, next to the table it reads (§6.2 platform tables). | A dispatcher per module; a broker (rule 15). |
| D2 | **Selection by anti-join, no checkpoint yet:** events of the consumer's types with no `processed_events` row in `Done`/`DeadLetter`, and `next_attempt_at` ≤ now if a row exists. Ordered by `sequence`, batch 50. | Correct when a lower sequence commits late; 6b adds checkpoints with projectors. | Checkpoint advance now (gap bug without 6b's gap logic). |
| D3 | **One worker per consumer:** take a session-level `pg_try_advisory_lock(hashtext('outbox:'+name))` on a dedicated connection for the cycle. No transaction is held across the handler. Skip the cycle if the lock is not taken. | Replicas never double-send concurrently; a session lock is not a tx (rule 6). | `FOR UPDATE SKIP LOCKED` (needs a tx open across the external call). |
| D4 | **Handle, then mark:** call `HandleAsync` with no tx open, then one short tx upserts `processed_events` `Done`. On a throw: `attempts++`, `next_attempt_at = now + 2^attempts s` (cap 5 min), `last_error` truncated with no secrets, status `Retrying`. Once `attempts > 5` → `DeadLetter` + `LogCritical` EventId `OutboxDeadLetter`. Dead letters never touch order state. | §8.4. `Retrying` makes the doc's retry-scan index (`status <> 'Done'`) meaningful. I edit §7.1 to add it. | Status-less retry rows. |
| D5 | **Event set:** one outbox event per order transition. `OrderPaid` (exists), **`OrderShipped`** (new, ship), and **`OrderStatusChanged`** (every other transition: process, deliver, expiry cancel, refund → `Refunded`/`Returned`, override). Payload records `OrderEventPayload(OrderId, UserId, FromStatus, ToStatus, OccurredAt)` go in `Ecommerce.Orders.Contracts/Events`, and Payments' `RefundRequested`/`PaymentIntentCancelRequested` payloads stay internal to Payments (own consumers). Every transition site in Orders adds its event through **one** internal helper. | Test 3 says 1 transition = 1 outbox event. Email reads `OrderPaid`/`OrderShipped`; SignalR reads all three. | A second event per transition. |
| D6 | **`Ecommerce.Notifications` + `.Contracts` (new module).** Contracts: `IEmailSender.SendAsync(EmailMessage(To, Subject, TextBody, IdempotencyKey))`. Implementation: `OrderEmailConsumer` (`OrderPaid` → confirmation, `OrderShipped` → carrier + tracking + `trackingUrl`), `OrderStatusHubConsumer`, `OrderStatusHub`, and two senders: `DevPickupDirectoryEmailSender` (Development, `artifacts/dev-mail`, one file per idempotency key so a resend overwrites) and `NoProviderEmailSender` (other environments: throws `EmailProviderUnavailableException`, so mail retries and then dead-letters with an alert, never silent). | §6.1 Notifications row; the provider is a placeholder (§2). The idempotency key `"{consumer}:{eventId}"` is handed to the provider for receiver-side dedupe (§8.4). | A provider package now; inventing a provider. |
| D7 | **New contracts, one consumer each:** `IUserReader.GetContactAsync(userId)` → `UserContact(Email, DisplayName)?` (Identity.Contracts, for Notifications). `IOrderReader.GetNotificationViewAsync(orderId)` → order number, total, lines, latest shipment (Orders.Contracts, for Notifications). `IOrderRefunds` (Orders.Contracts, for Payments): `LockForRefundAsync(orderId)` → `(Status, UserId, HasApprovedReturn)?` (caller tx, `FOR UPDATE`), and `ApplyFullRefundAsync(orderId, refundId, actorId?)` (caller tx: `Paid`/`Processing` → `Refunded`, `Delivered` → `Returned`, history row, `OrderStatusChanged`). `IRefundState.HasPendingFullRefundAsync(orderId)` (Payments.Contracts, for Orders `process`/`ship`). | Minimal surface (§6.1); no cross-module DbSet or join (rule 8). | Payments reading `orders` directly. |
| D8 | **Refund lock order:** `payments` (paying row, `FOR UPDATE`) → `orders` (`FOR UPDATE` via `IOrderRefunds`) → refunds insert. `process`/`ship` keep `orders FOR UPDATE` and then **read** refunds without locking `payments`. | Matches the "Payments module and lock order" row. A refund holding the order lock makes `ship` wait and then see `Pending`, and the two never deadlock. | Ship locking payments (reverse order). |
| D9 | **Admin refund (`POST /admin/orders/{id}/refund`, Admin)** follows §8.5 steps 1–3 and the §9 contract exactly. Idempotency scope `refund`: the request hash is over `{orderId, amount, currency, reason}`, and the stored response is replayed. "Full" = Σ(Pending + Succeeded) + amount == captured. The controller lives in **Payments** under that route. A ceiling breach writes an audit row (`refund.rejected`) in its own tx and returns 409. | §8.5; the ceiling row in the decision log. | Controller in Orders (Orders must never call Stripe paths, §6.1). |
| D10 | **`RefundRequested` consumer (Payments):** `IPaymentGateway.CreateRefundAsync(providerPaymentId, amount, idempotencyKey "re:{refundId}", metadata refund_id)`, then a short tx stores `provider_refund_id` if it is null. **`PaymentIntentCancelRequested` consumer:** cancel the intent; "already succeeded/canceled" counts as success (no throw). | §8.5 step 4; closes Slice 4 D9. | — |
| D11 | **Refund webhook:** add `charge.refunded` and `refund.updated` to the verifier mapping. Find the refund by `metadata.refund_id`, falling back to `provider_refund_id`. Dedupe by event ID with the existing `stripe-event` scope. One tx: refund `Succeeded`/`Failed` (terminal: a second terminal event is a no-op), payment → `PartiallyRefunded`/`Refunded` from Σ Succeeded, a full refund calls `ApplyFullRefundAsync`, and an approved return → `Refunded` (D13). `Failed` → `LogCritical` `PaymentAlert`. Order totals never change. | §8.5 step 5; §7.3 refund and payment machines. | Trusting the consumer's Stripe response. |
| D12 | **Return request (`POST /orders/{id}/return-request`, owner):** body `{ items: [{ variantId, quantity }], reason }` (reason 1–500). The order must be `Delivered` and the caller its owner (other customer → 403, absent → 404). Items must be ⊆ the order's lines with quantity ≤ ordered and ≥ 1, else 400. One open request (`Requested`/`Approved`) per order, else 409. There is no `Idempotency-Key`: the one-open rule makes a double submit a 409, and F8 has no optimistic submit. | §9, §4.7. | An idempotency scope that §7.1 doesn't list. |
| D13 | **Admin returns:** `GET /admin/returns?status=&page=` (Admin), and `POST /admin/returns/{id}/approve` or `decline` with `{ reason }` (1–500; it fills `admin_note`). Only `Requested` → else 409. Both write `audit_log`. Approval moves no money. `Approved → Refunded` happens in the refund webhook when a refund on that order succeeds; when the order reaches `Returned`, it becomes full. Orders owns `return_requests`; the transition goes through `IOrderRefunds.ApplyFullRefundAsync`, plus `MarkReturnRefundedAsync` for partial. | §7.3 return machine; §4.7. | Linking the refund body to a return ID (not in §9). |
| D14 | **`GET /admin/orders/{id}` (Admin, Support):** the customer detail shape plus `userId`, `refunds[]` (all statuses), `refundedTotal` (Succeeded), `pendingTotal`, `remaining` (computed by Payments via an `IRefundState` read), and `returnRequests[]`. | F13 must show the server's `Pending` and the remaining amount. | Computing remaining on the client. |
| D15 | **Password reset via outbox (Identity):** `RequestAsync` inserts `PasswordResetRequested(userId)` in the same tx as nothing else (known user only; unknown → no row, same 202). `PasswordResetConsumer` (Identity) mints the token: it stores the hash in its own short tx, then sends through `IEmailSender` with no tx open. A retry mints a fresh token. **Delete** `IPasswordResetLinkSender`, `DevPickupDirectoryLinkSender`, and `NoProviderLinkSender`, and move their tests to the D6 senders: these are **changed tests**, list them. | Decision log row "Password-reset delivery (Slice 1b → 6)": plaintext is never stored. | Keeping the 1b sender seam. |
| D16 | **SignalR:** hub `/hubs/orders` with `[Authorize]`, and the client joins group `user:{sub}` on connect. The consumer sends `orderStatusChanged { orderId, status }` to the owner's group: a hint only. The JWT is read from `access_token` **only** for `/hubs/*` paths (Identity `JwtBearerEvents.OnMessageReceived`). No `/hubs/*` query string in logs. | Rule 10: no full JWT in logs. §4.13: "a refresh is always correct without it". | Per-order groups. |
| D17 | **Packages (rule 16):** frontend `@microsoft/signalr`, exact pin. Official SignalR client (web + React Native); replaces nothing. Backend: none. SignalR server is in the ASP.NET shared framework. | Only new dependency. | Raw WebSocket. |
| D18 | **Frontend:** `lib/realtime.ts` (one connection, `accessTokenFactory` from `api-client`, auto-reconnect) and `useOrderLiveStatus(orderId)`, which invalidates the order queries on a message. Under `features/orders`: `ReturnRequestForm` (Delivered only; F8), and refund rows on `OrderDetailView`. New `features/admin-orders`: `AdminOrderDetailView` + `RefundForm` (F13: one key per attempt from `lib/idempotency-key`, reused on retry, amount sent as a decimal string, no optimistic status) and `AdminReturnsView`. Route files `app/admin/orders/[id].tsx` and `app/admin/returns.tsx` swap their `ScreenStub` for these views. `app/admin/orders/index.tsx` stays a stub. | F8, F12, F13; §5.2 feature folders. | Building the admin list now (feature 11, §8). |

## 3. Files

**Backend, new:**
- `Modules/Notifications/Ecommerce.Notifications{,.Contracts}`: `NotificationsModule`, `Application/OrderEmailConsumer`, `OrderStatusHubConsumer`, `EmailTemplates`, `Infrastructure/*EmailSender`, `Api/OrderStatusHub`.
- Persistence: `Outbox/OutboxProcessor`, `ProcessedEventEntry` (+ config), the worker registration hook. SharedKernel: `IOutboxConsumer`.
- Orders:
  - `Domain/ReturnRequest`, `ReturnStatus`
  - `Application/ReturnService`, `OrderEvents` (D5 helper), `OrderRefunds` (`IOrderRefunds`), `OrderReader` (`IOrderReader`)
  - `Api/ReturnsController` (customer) and `AdminReturnsController`
  - `GET {id}` on `AdminOrdersController`
- Payments:
  - `Application/RefundService`, `RefundRequestedConsumer`, `IntentCancelConsumer`, `RefundState` (`IRefundState`)
  - `Api/AdminRefundController`
- Identity: `Application/PasswordResetConsumer`, `UserReader` (`IUserReader`).

**Backend, edited:**
- `OrderFulfillmentService` (events; pending-full-refund guard)
- `OrderPaymentHandler` and the expiry path (events through the helper)
- `OrderQueryService` (`refunds[]` via `IRefundState`)
- `PaymentWebhookService` + `StripeWebhookVerifier` (refund events)
- `StripePaymentGateway` (refund, cancel)
- `PasswordResetService`
- Identity JWT events
- host `Program.cs` (map hub, worker, Notifications module)
- `Ecommerce.Api` request logging
- `ArchitectureTests` module list (adding Notifications only; **no rule loosened**)

**Migration** (one, additive, rule 18):
- `processed_events` (status CHECK `Retrying`/`Done`/`DeadLetter`, partial index per §7.2)
- `return_requests` (status CHECK `Requested`/`Approved`/`Declined`/`Refunded`, FK to `orders` inside the module, partial unique index on `order_id WHERE status IN ('Requested','Approved')`)
- Check whether `refunds` already exists from Slice 4. Add only the missing columns or indexes (`provider_refund_id` unique nullable). Never drop.

**Frontend:** as listed in D18, plus `features/orders/api.ts` + types, `features/admin-orders/{api,types,hooks}.ts`, and `lib/query-keys.ts` entries.

## 4. Build order (security and invariant tests first; quote each red)

1. Migration + `processed_events`. Write the `OutboxProcessor` tests red, then make them green. Cover: dedupe, retry/backoff with `IClock`, dead letter at > 5 attempts, no tx open during the handler (a Rule6-style test like Slice 4's), and two processors racing one consumer → one handle.
2. **T26, T27, T28 red** → `RefundService`, `IOrderRefunds`, `IRefundState`, the refund controller, the consumers, and the webhook refund path → green.
3. **T7 refund and return parts red** (customer → `/admin/orders/{id}/refund` 403; customer B → `/orders/{A}/return-request` 403) → controllers → green.
4. Return flow (D12, D13) with its tests.
5. **T9 red** → D5 events, `OrderEmailConsumer`, senders → green. Then `OrderShipped` email content.
6. D15 password reset; the moved sender tests.
7. SignalR (D16) and its auth/logging tests.
8. `GET /admin/orders/{id}`; T29 `refunds[]`.
9. Frontend: F8, F13, the F12 refund rows, the realtime hook. Jest first for the F tests.
10. `verify.sh changed` in the loop, full `verify.sh` once before your report. Mutation checks (§5) go in the report.

## 5. Required tests (★ = test-first, hard invariant; quote the red)

| Test | Proves |
|---|---|
| T26 ★ | §11.2 row 26. Parallel refunds (e.g. 5 × 40% of the captured amount): Σ Pending + Succeeded ≤ captured, the losers get 409 plus an audit row, and a `Failed` webhook frees its amount for a new refund. Integration test on real Postgres. |
| T27 ★ | Row 27, three paths: the same key twice; a consumer retry (fake gateway records exactly one call with key `re:{refundId}`); a duplicate webhook. Result: one refund row, one transition, one audit row. Same key with a different body → 409. |
| T28 ★ | Row 28 in full, including `ship` → 409 while a full refund is `Pending`, and `Delivered` + approved return → `Returned` + return `Refunded`. Totals unchanged. Audit has actor, before, after, reason. |
| T9 ★ | Sender throws twice, then works: the order stays `Paid`; exactly one sent message per idempotency key; `processed_events` `Done`, attempts = 3. Six failures → `DeadLetter`, alert in `CollectingSink`, order still `Paid`. |
| T7 ★ (Slice 5 carry-in) | Refund as Customer or Support → 403. Return request on another customer's order → 403. Anonymous → 401. |
| T29 (carry-in) | Detail `refunds[]` lists `{ amount, status, createdAt }` in order; `GET /admin/orders/{id}` shows `remaining`. |
| T3 (regression) | Still one outbox event per transition now that D5 adds events. Re-run; it must not be edited. |
| Outbox | Dedupe per `(consumer, event_id)`; two consumers get the same event independently; an event that commits late (lower sequence) is still handled; race → one handle. |
| Returns | Not `Delivered` → 409; bad items or quantity → 400; second open request → 409; approve/decline only from `Requested`, with an audit row; 401/403/400 per endpoint. |
| Reset | Known email → one `PasswordResetRequested` + 202; unknown → no row + 202; consumer stores a hash only (no plaintext in the DB or any log); a retry after a send failure mints a new token; the old 1b token rules (expiry, single use, family revoke) still pass. |
| SignalR | Unauthenticated connect rejected; a message goes only to the owner's group; logs from a `/hubs` request contain no `access_token`. |
| Arch | Notifications follows §6.2 (only `NotificationsModule` public; references only allowed projects). |
| F8 | No optimistic submit; a server error shows plain text + retry; the form only shows on `Delivered`. |
| F13 | One key per attempt, reused on retry; decimal-string amount; shows the server's `Pending` + `remaining`; 409 shows the reason. |
| F12 (carry-in) | Refund rows render; live status message → refetch (mock `lib/realtime`). |

**Mutation checks (report each `Failed!` line):**
- Drop `FOR UPDATE` on the payment row → T26.
- Drop the `processed_events` dedupe → T9 / T27.
- Change `re:{refundId}` to a random key → T27.
- Remove the pending-full-refund guard on `ship` → T28.
- Remove the owner check on return-request → T7.
- Add one tx around the handler → the no-tx outbox test.

## 6. Risks

- **R1.** A worker crash after the provider accepts but before the mark → one duplicate email. Mitigated by the provider idempotency key. The full test is T15 (6b). Note it in the report.
- **R2.** Without a real provider, every non-Development email dead-letters. Deliberate and alerting; provider is Nathaniel's call (§8.3).
- **R3.** The anti-join scan grows with the retained outbox. That's acceptable at launch volume; 6b's checkpoints bound it.
- **R4.** The SignalR token in the query string: the logging test is mandatory. Check the reverse proxy on staging (Slice 7).
- **R5.** The Slice 4 waiver dependency (§0).

## 7. Doc upkeep (Rob, this slice)

- Decision log: one row, "Slice 6 outbox runtime" (D1–D5).
- §7.1 `processed_events`: status values gain `Retrying`.

## 8. Decisions for Nathaniel

1. **Admin screens for features 10, 11, 12** (`/admin/inventory`, the `/admin/orders` list and actions, `/admin/customers`) have no §12.3 row. Most of the backend exists (`/admin/inventory`, order actions). `GET /admin/orders` (list) and `/admin/customers*` do not. **Recommendation:** a new row **"6a Admin console"** after 6 and before 6b, covering the inventory, order list/actions, and customers screens plus the two missing GET endpoints. 6b is already the heaviest correctness slice, and 7 is hardening. Slice 6 builds only `/admin/orders/[id]` + `/admin/returns`, because F13 and feature 7 name them.
2. **Slice 4 waiver line** (§0) before Slice 6 can PASS.
3. **Email provider** (§2 open decision) before staging; Slice 6 ships placeholders only.
