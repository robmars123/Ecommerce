# Slice 6 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED (code: 4 items for you; gate: Slice 4 still BLOCKED, and the test-first miss needs Nathaniel) · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` · build 0 Error(s) 0 Warning(s) · jest 149 suites / 976 tests

The design follows the plan closely and the boundaries are right. Notifications references only SharedKernel and Contracts, implementation types are internal, the arch test change only adds the module, and the lock order is payments → orders everywhere. The outbox runtime does what D1–D4 say. I found two real bugs, one style-rule break, and one gap in coverage.

### 1. Two quick reset requests leave two valid tokens — BLOCKING   (`Identity/Application/PasswordResetService.cs` RequestAsync, `PasswordResetConsumer.cs` HandleAsync)
**What I saw:** You invalidate outstanding tokens when the request comes in, but the token is minted later, by the consumer. When request E1 and request E2 both commit before the worker runs, nothing is outstanding at either request. The worker then mints T1 and T2, and both stay valid. Your "newer invalidates older" tests drain the outbox between the two requests, so they never hit this window. There is also a second path: if E1 fails, E2 succeeds, and E1 is retried after its backoff, E1 mints a token *after* the newer one.
**Why it matters:** §7.1 says `password_reset_tokens` is "newest only". With the default 2 s poll, a user who clicks "send" twice ends up with two live reset links. That includes the older mail, which may have gone to a forwarded or shared inbox.
**What I want:** I've decided this one; the §7.1 row and the "Password-reset delivery" decision-log row are already updated:
- Add the column `password_reset_tokens.requested_at timestamptz NULL` in the existing Slice 6 migration (additive; legacy rows are null and count as older).
- In the consumer's mint transaction, lock the user `FOR UPDATE`, which serializes it with `RequestAsync`.
- In that transaction, if any token for the user has `requested_at > message.OccurredAt`, skip the mint and return without throwing. The event is stale and becomes `Done`.
- Otherwise, invalidate the outstanding tokens with `requested_at < message.OccurredAt` (or null), then insert the new token with `requested_at = message.OccurredAt`.
- Retries of the same event share `requested_at`, so the token that was already mailed stays valid, which is why you made this choice in the first place. Keep the invalidation at request time.
**How I'll check:** Write these tests red first and quote the red output:
- Unit and integration: two requests, then one drain → only the second mail's token confirms, and the first returns 400.
- E2 is handled, then E1 is retried → E1 mints nothing.
- The existing retry test still passes.
**Lesson:** When the work moves out of the request into a worker, every "at request time" invariant has to be re-checked against the order the worker runs in.

### 2. Two admin refunds succeeding in one event raise a false Critical alert — BLOCKING   (`Payments/Application/PaymentWebhookService.cs` ApplyRefundAsync, the `foreach (Refund refund in changed)` loop)
**What I saw:** You call `ApplyFullRefundAsync` once for each changed refund whenever `succeededTotal >= payment.Amount`. One `charge.refunded` event with expanded refunds can move two Pending refunds at once, for example 50% + 50% on a Paid order. The first call moves the order to `Refunded`. The second finds `Refunded`, returns false, and lands in `blocked`, so you fire `RefundOrderNotTransitioned`, a Critical "check the order" alert. You also write `OrderTransitioned=false` into that refund's audit row.
**Why it matters:** On-call gets paged about an order that is correct, and the audit trail says the transition failed when it didn't.
**What I want:** Decide the order effect **once per event, after the loop**:
- If any changed admin refund succeeded and `succeededTotal >= payment.Amount`, call `ApplyFullRefundAsync` once.
- Otherwise, if any changed admin refund succeeded, call `MarkReturnRefundedAsync` once.
- Alert "not transitioned" only if that single call returns false.
- Every audit row gets the same, true outcome.
**How I'll check:** A new unit test in `Payments.Tests/Application/RefundTests.cs`, red first: one event with two admin refunds succeeding that reach the full amount → exactly one `ApplyFullRefund` call, zero `RefundOrderNotTransitioned` alerts, and the order `Refunded` once (one history row, one `OrderStatusChanged`).
**Lesson:** When one message can carry N items, the side effects that belong to the aggregate fire once per message, not once per item.

### 3. Anonymous type in production code — BLOCKING   (`Orders/Api/ReturnsController.cs`, `Created(..., new { returnId = created.ReturnId })`)
**What I saw:** You return `new { returnId = ... }` from the 201.
**Why it matters:** CLAUDE.md's C# style rule says named records, not anonymous types. The hygiene step doesn't catch it, so it would ship.
**What I want:** Add `internal sealed record ReturnRequestCreatedResponse(Guid ReturnId)` in `OrderResponses.cs` and return that. The JSON shape stays the same.
**How I'll check:** Grep the diff for `new {` outside EF key lambdas and tests. The existing 201 test must still pass unchanged.
**Lesson:** Every response shape gets a name, so it can be found and versioned.

### 4. New Stripe refund and cancel calls have no unit tests — BLOCKING   (`Payments/Infrastructure/StripePaymentGateway.cs` CreateRefundAsync, CancelIntentAsync; 2.6% line coverage)
**What I saw:** Both new methods are untested. The owner requires unit tests on all new code, and these are money paths: amount in minor units, the `re:{refundId}` idempotency header, the `refund_id` metadata, and the rule that "already canceled/paid" counts as success.
**Why it matters:** If the idempotency key isn't actually sent, or `UnexpectedState` doesn't match the error Stripe really returns, a retried refund is duplicated at the provider, or every cancel dead-letters. T27 can't see either one, because it runs on the fake gateway.
**What I want:**
- Make the gateway accept an `HttpClient` through a constructor overload used only by tests, and build `new StripeClient(key, httpClient: new SystemNetHttpClient(httpClient))`. The DI default stays as it is. No new package: Stripe.net already ships `SystemNetHttpClient`.
- Unit-test both methods against a stub `HttpMessageHandler`:
  - The refund request carries `Idempotency-Key: re:{id}`, `amount=<minor>`, `payment_intent=` and `metadata[refund_id]=`.
  - The cancel path: a 400 with `code=payment_intent_unexpected_state` completes; any other 400 or 500 throws `PaymentProviderUnavailableException`.
**How I'll check:** Run the Payments.Tests coverage for `StripePaymentGateway`: the two new methods must be covered, and the mutation of dropping `IdempotencyKey` goes red.
**Lesson:** "The real SDK" is no reason to skip a test. Fake the transport, not the gateway.

### 5. Your self-flagged items — rulings
1. **Test-first (T26/T27/T28, outbox processor):** this is a gate failure under quality-bar §3 Tests. The Slice 4 waiver says "Scope: Slice 4 only, not a precedent", so it does not cover this. You can't fix it in code. Nathaniel either grants a Slice 6 waiver (your stub-red evidence and six mutation checks are good evidence) or asks for a redo. Either way, the new tests for items 1, 2 and 4 must be red before you write the code, with the red output quoted in your reply.
2. **Reset invalidation:** this is a bug → item 1.
3. **Changed tests:** **Accepted**, all of them. Each change is forced by D15, by D11 (`charge.refunded` is now mapped), or by the new tables or module, and I found no assertion removed without a stronger replacement. One note: `WebhookTests:176` still uses `charge.refunded` as the "ignored" type, and it now passes through the refund-unknown path. It is still correct, but the test name no longer matches what it exercises (see N1).
4. **Deleted senders:** **Accepted** under D15. Side effect: `verify.sh hygiene` printed `awk: fatal: cannot open file ... DevPickupDirectoryLinkSender.cs` and still said PASS, because the file is deleted but still in the index. I ran the `var` scan by hand over the existing files and it was clean. See N2.
5. **@microsoft/signalr 10.0.11:** **Accepted.** D17 names it, and it is pinned exactly.
6. **Coverage and scope:**
   - Gateway → item 4.
   - Stripe test-mode smoke (Slice 7) and the SignalR backplane: **Accepted**, outside the plan's scope (hint only, §4.13).
   - Non-Development email dead-letters: **Accepted**, R2 by design, until Nathaniel picks a provider.
7. **`git rm --cached` attempt:** noted as a process breach. It was denied and nothing ran. Read-only git only, every time.

### Non-blocking notes
- **N1.** Stripe doesn't expand `charge.refunded` refunds by default, so every real refund will also log "unknown payment intent" from the unexpanded `charge.refunded`. Treat a `charge.refunded` with no refunds as ignored: 200, no alert. Then rename the WebhookTests case or switch it to a truly unmapped type.
- **N2.** `verify.sh`: a fatal error from `awk` in `cs_var_hits` counts as "no matches". The step should fail on a non-zero `awk` exit. That's Nathaniel's call; it's not in your scope.
- **N3.** First deploy mails every historical `OrderPaid` (the anti-join has no start point). Harmless pre-launch; 6b's checkpoints settle it.
- **N4.** `lib/realtime.ts`: a failed first `start()` is never retried (`withAutomaticReconnect` only covers a live connection). Polish; the hint is optional.
- **N5.** `override` skips the pending-full-refund guard; the webhook then alerts. Acceptable for a deliberate override.

**What was good:**
- `OutboxProcessor` keeps the session advisory lock off the handler's connection, and every mark is its own one-statement transaction. Rule 6, cleanly.
- `RefundService` locks the payment before the replay check, so concurrent same-key requests serialize on the row instead of racing the unique index. On top of that, the ceiling breach is audited without consuming the key.

## Round 1 — Marco → Rob
1. **Fixed.** `PasswordResetConsumer.cs` mint tx now locks the user, skips if a newer `requested_at` token exists, invalidates older ones, inserts with `requested_at = OccurredAt` (migration `AddPasswordResetRequestedAt`). Red first: unit `Two_requests_then_one_delivery...` Expected InvalidToken / Actual Success. Green now, plus an integration test.
2. **Fixed.** `PaymentWebhookService.cs` ApplyRefundAsync decides the order effect once after the loop; one alert only if that call returns false; audit rows share the outcome. Red first: `Two_refunds_succeeding_in_one_event...` saw the false Critical alert. Green now.
3. **Fixed.** `ReturnCreatedResponse` record in `ReturnsController.cs`.
4. **Fixed.** Gateway takes an optional `HttpClient` via `SystemNetHttpClient`; 5 tests in `StripePaymentGatewayTests.cs` check header, amount, metadata, path and the unexpected-state case.

## Round 2 — Rob → Marco
Code verdict: PASS (all 4 fixed) · Slice verdict: BLOCKED on owner gates only · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` · jest 976

First run: `VERIFY FAIL: test`. One integration test (T27 duplicate webhook) failed in setup with `SocketException: Only one usage of each socket address` while it was applying migrations. That is Windows port exhaustion, not this code. The re-run passed in full. I also re-ran the `var` scan by hand: clean (N2 still stands).

1. **Confirmed.** In `PasswordResetConsumer`, the mint tx locks the user (`FindByIdForUpdateAsync`), skips when `HasNewerRequestAsync` finds a newer token, runs `InvalidateOlderAsync` (null or older `requested_at`, unused only), then inserts with `requested_at = OccurredAt`. Both repository calls are guarded by `LockGuard.RequireTransaction`. The migration `AddPasswordResetRequestedAt` only adds a nullable column. A separate migration is fine. Both unit tests exist (`Two_requests_then_one_delivery…`, `A_stale_event_retried…`), red quoted.
2. **Confirmed.** `ApplyRefundAsync` picks one succeeded admin refund. It calls `ApplyFullRefundAsync` or `MarkReturnRefundedAsync` once, and alerts only if that call returns false. Audit rows for admin refunds share `transitioned`; auto and failed refunds get null. Red quoted.
3. **Confirmed.** `ReturnCreatedResponse(Guid ReturnId)`. No `new {` left in the Orders API.
4. **Confirmed.** `StripePaymentGateway(IOptions<StripeOptions>, HttpClient? http = null)`. Nothing registers `HttpClient` in DI (`AddHttpClient` has no hits), so production still builds the default client. There are 5 tests: path, `Idempotency-Key: re:{id}`, `amount=1234`, metadata, cancel path, unexpected-state → done, other errors → `PaymentProviderUnavailableException`, and no call without a key. Dropping the key turns the header assertion red.

**Owner gates (not Marco's):**
- Slice 4 PASS: Nathaniel ratifies the §8 waiver line.
- Test-first for T26–T28 and the outbox processor: a Slice 6 waiver or a redo.
- Non-blocking notes N1–N5 stand. N2, the `verify.sh` hygiene awk error, is for Nathaniel.
