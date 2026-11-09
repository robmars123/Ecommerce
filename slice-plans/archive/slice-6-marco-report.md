# Slice 6 — Notifications + Returns — Marco report (2026-11-07)

```
Concerns: [1] Round 0: T26/T27/T28 code was written before its tests; red was shown by stubbing RefundService (19 of 19 red). Nathaniel's call.
[2] SignalR has no backplane: with 2+ replicas a push reaches only the replica's own clients. A hint only (arch 4.13); Slice 7.
[3] charge.refunded yields refunds only if Stripe expands them; refund.updated is the reliable event. Subscribe to both (placeholder, Nathaniel).
[4] Outside Development every email dead-letters with a Fatal alert (R2, deliberate). Crash after send before mark = one duplicate mail (R1; T15 is 6b).
[5] Dev config key moved: Identity:PasswordReset:DevPickupDirectory -> Notifications:DevPickupDirectory.
[6] I tried `git rm --cached` once; denied, nothing ran. The two old sender files were removed with plain rm (in HEAD).
[7] Baseline first run: 12 integration tests hit Npgsql 53300 "too many clients" (parallel projects, shared container). Not seen since.
Bugs found: Rob's 4 items (fixed below). Bugs open: None
Tests changed (none weakened): Identity PasswordResetServiceTests + Fakes (sender seam gone, D15); PasswordResetTests, IdentityHostTests, IdentityTestEnv (recording email sender + DrainOutboxAsync); PaymentsApplicationTests ignored-event case charge.refunded -> charge.succeeded (D11); CatalogSchemaTests table list (+2 tables); ApiFactory, FakePaymentGateway, SolutionLayout; frontend routes.test, order-fixtures, jest-setup (mock lib/realtime).
Changed: Slice 6 (see git status): Notifications module, Persistence/Outbox, SharedKernel IOutboxConsumer, Orders/Payments/Identity changes, migrations AddOutboxRuntimeAndReturns + AddPasswordResetRequestedAt, Dockerfile, frontend (lib/realtime, ReturnRequestForm, features/admin-orders, @microsoft/signalr 10.0.11 exact).
Mutation checks (restored): no FOR UPDATE -> T26 "Expected 2/Actual 5"; random refund key -> T27 key mismatch; no ship guard -> T28 "Expected Conflict/Actual NoContent"; no Done mark -> T9 "Expected (Done,3)/Actual (Retrying,2)"; no owner check -> T7 "Expected Forbidden/Actual Created"; tx around handler -> rule 6 test red; F13 renew on every error -> key-reuse test red.
Tests (baseline -> now): Orders 171->201; Payments 23->77; Identity 73->77; Notifications 18 new; Integration 301->378; Jest 133/850 -> 149/976; others unchanged.
Coverage (line %, no gate): outbox runtime 87; Notifications 96; refund service/webhook 94-95; ReturnService 97; reset service+consumer 100; frontend new files 92-100.
verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export) · 0 Warning(s)
```

## Quality-bar §2
- [x] Plan D1-D18 built; numbered tests T3 (untouched), T7, T9, T26-T29, F8, F12, F13 present; new endpoints have happy/401/403/400 tests.
- [x] Rule 6 (no call in a tx, tested); module boundaries (ArchitectureTests pass); no secrets, no new package beyond the plan.
- N/A: docs edits (Rob).

## Fix round 1 (Rob's 4 blocking items)
1. **Reset, two valid links — Fixed.** Migration `AddPasswordResetRequestedAt` adds nullable `password_reset_tokens.requested_at`. `PasswordResetConsumer` now mints in one tx: lock user FOR UPDATE; if a token with newer `requested_at` exists, mint nothing (event becomes Done); else `InvalidateOlderAsync` then insert with `requested_at = message.OccurredAt`. Request-time invalidation kept. Red before the fix (Identity unit): `Two_requests_then_one_delivery...` Expected InvalidToken / Actual Success; `A_stale_event_retried...` also failed. Now green, plus an integration test (two requests, one drain: first link 400, second 204). Retry test still passes.
2. **Refund webhook false alert — Fixed.** `ApplyRefundAsync` decides the order effect once per event after the loop (one `ApplyFullRefundAsync` or one `MarkReturnRefundedAsync`); all audit rows carry the same outcome; alert only if that call returns false. Red: `Two_refunds_succeeding_in_one_event...` "Assert.DoesNotContain() Failure: Filter matched" (the Critical alert). Now green.
3. **Anonymous type — Fixed.** `ReturnCreatedResponse(Guid ReturnId)` record in ReturnsController.
4. **Gateway tests — Fixed.** `StripePaymentGateway` takes an optional `HttpClient` (wrapped in `SystemNetHttpClient`; null in production). 5 new tests: refund path, `Idempotency-Key` `re:{id}`, amount 1234, `metadata[refund_id]`; cancel path; `payment_intent_unexpected_state` is done; other errors and a missing key -> ProviderUnavailable, no call.
