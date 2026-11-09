# Deliverable 4.4 — Review thread

PaymentIntent creation: `IPaymentAttempts` (Payments · Slice 4), the side of checkout and retry that talks to Stripe.

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings, jest 149 suites / 982 tests, drift clean)

Scope: committed code on `dev`. I read `IPaymentAttempts`, `PaymentAttempts`, `MinorUnits`, `PaymentPorts`, `Payment`, `StripePaymentGateway`, `StripeOptions`, `PaymentStore.AttachProviderIdAsync` and `PaymentsModule`, plus the caller `CheckoutService.CompleteAsync` (4.3 is not re-reviewed). Tests: `Payments.Tests` and the intent assertions in `Api.IntegrationTests/Checkout`. The design is right. The intent is created after commit, the amount comes from the stored payment row, a stored id is never re-created, and the first write of the provider id wins. But the two guards that make "the same intent across retries" true, the derived key and the key header, are not pinned by any test. Also, a provider failure leaves no trace in the logs.

### Invariants → code → tests

| Invariant | Where it holds | Tests that prove it |
|---|---|---|
| Intent amount = server `grand_total` (rule 1, test 5) | `order.GrandTotal` → payment row → `MinorUnits.From(payment.Amount)` (`PaymentAttempts.cs:54`) | T5, T23 ×2, T24; `MinorUnits` unit tests |
| Intent currency = order currency | `payment.Currency`, lower-cased in the gateway | T5 `"USD"` |
| Stripe key `pi:{orderId}:{attempt}` stable across retries (§8.2 rule 4) | `PaymentAttempts.cs:54` | **None** (finding 1) |
| Key actually sent to Stripe; request shape | `StripePaymentGateway.cs:18-30` | **None** (finding 2) |
| No external call inside a DB transaction (rule 6) | `EnsureIntentAsync` runs only after commit; cancel goes through the outbox | `Rule6_…` (the fake throws inside a transaction) |
| Crash before the intent → same key heals; crash after Stripe created it → same intent | replay → `EnsureIntentAsync`; `AttachProviderIdAsync` sets only a null id | 503-then-heal ×2; the second case **none** (finding 1) |
| A stored intent is never re-created | `PaymentAttempts.cs:46-51` | `Rule6_…` (`Retrieves > 0`) |
| Secrets from config only | `Stripe` block empty in `appsettings.json`; validator fails outside Development; no key → no call | `A_missing_secret_key…` (refund path); validator untested (NOTE 6) |
| Provider failure → order stays `PendingPayment`, 503 with `orderId` | `CallAsync` wraps Stripe/HTTP/timeout errors | 503-then-heal, `Any_other_provider_error…` |
| Boundaries (§6.2) | csproj → SharedKernel, Persistence, Payments/Orders/Identity `.Contracts`; only `PaymentsModule` public; `Stripe.net` named in plan D19 | ArchitectureTests 25/25 |

### Mutation checks (each restored from a scratch copy; `cmp` matched every time; afterwards `git diff --stat -- backend/src` shows only the pre-existing `CheckoutService.cs` `HashOf` change, 2+/2−)

| # | Guard broken | Result |
|---|---|---|
| M1 | `PaymentAttempts.cs:54`: key = `pi:{Guid.NewGuid()}` (unstable) | **GREEN**: Checkout `Failed: 0, Passed: 50` |
| M2 | `StripePaymentGateway.cs:29`: `new RequestOptions()` (no key sent) **and** `:20` amount + 1 cent | **GREEN**: Payments.Tests `Failed: 0, Passed: 77` |
| M3 | `:54`: `MinorUnits.From(payment.Amount) - 1` | RED: T5, T23 ×2, T24 (`Failed: 4`) |
| M4 | `:54`: currency `"EUR"` | RED: T5 (`Failed: 1`) |
| M5 | `:46`: stored-id branch disabled (always create) | RED: `Rule6_…` only, through `Retrieves > 0` (`Failed: 1`) |
| M6 | `:54`: key = `pi:{orderId}:1` (attempt ignored) | RED: T24 ×4, T25, Rule6 (`Failed: 6`) |

### 1. The stable Stripe key, and the crash it exists for, are never tested — BLOCKING   (`PaymentAttempts.cs:54`, `FakePaymentGateway.cs:127`)
**What I saw:** with M1 (a fresh random key on every call) all 50 checkout, retry and webhook tests stay green. Every heal test fails before the intent exists (`Unavailable = true` throws before the fake records anything). So no test ever sends a second create for an intent the provider already made.
**Why it matters:** this is the crash §8.2 rule 4 derives the key for. Stripe creates the intent, then the API dies or the response times out before `AttachProviderIdAsync` stores the id. The client repeats the same checkout key. With a stable key, Stripe returns the same intent. With M1, Stripe makes a second intent for the same order and attempt, the first one is orphaned, and a later refactor can drop the derivation without one test going red.
**What I want:** (a) a fake mode that records the intent and then throws `PaymentProviderUnavailableException`, like a response lost after the provider acted. (b) A checkout test: the first call → 503 with `orderId`, the same key again → 200, and then `Creates.Count == 2`, both with `IdempotencyKey == $"pi:{orderId}:1"`, `Single(Intents)`, the stored `provider_payment_id` is that intent, and the client secret matches. (c) In T24, assert attempt 2's key is `pi:{orderId}:2`.
**How I'll check:** I re-apply M1. Test (b) must go red.
**Lesson:** an idempotency key only protects you if a test sends the retry it is meant for.

### 2. `StripePaymentGateway.CreateIntentAsync` has no test — BLOCKING   (`StripePaymentGateway.cs:16-31`, `StripePaymentGatewayTests.cs`)
**What I saw:** the stub-handler tests cover refund and cancel. Create, which is the money call, has none. With M2 the gateway sends no `Idempotency-Key` header and charges one cent more, and all 77 Payments tests pass. The integration tests use the fake, so they never see this adapter.
**Why it matters:** this adapter is where `grand_total` becomes a real charge. If the header is lost, finding 1's protection is gone in production while every test stays green. If the amount is off, the customer is charged something other than the server total (rule 1, test 5).
**What I want:** one stub-handler test, in the same style as the refund test: POST `/v1/payment_intents`, `Idempotency-Key` = the request's key, `amount` = minor units, `currency=usd` (lower case), `metadata[order_id]` and `metadata[payment_id]`, `payment_method_types[0]=card`. The reply's `id` and `client_secret` map to `CreatedIntent`. Extend the existing error test and missing-key test to the create path.
**How I'll check:** I re-apply M2. It must go red.
**Lesson:** a fake proves the caller. Only a test against the real adapter proves the wire.

### 3. A failed intent creation is not logged anywhere — BLOCKING   (`CheckoutService.cs:186`, `RetryPaymentService.cs:76`, `StripePaymentGateway.cs:74-77`)
**What I saw:** the gateway wraps every `StripeException` in `PaymentProviderUnavailableException`. Both callers catch it and return 503. No `ILogger` in Orders or Payments ever writes the inner exception.
**Why it matters:** suppose a production secret key is revoked, the account can't take the currency, or Stripe answers `amount_too_small`. Every checkout then returns 503 "temporarily unavailable", and the only thing in the logs is the request line. A permanent misconfiguration looks just like a Stripe blip, and §10.3's checkout success-rate metric falls with no cause attached.
**What I want:** log the failure once where Payments owns it, in `PaymentAttempts.EnsureIntentAsync` (catch, log, rethrow). Use a structured warning with `OrderId`, `PaymentId`, `Attempt`, and the Stripe error `type`/`code` when there is one. Never log the secret key, the client secret or the amount. Add a test with a collecting logger: one entry, ids present, no `secret` text.
**How I'll check:** I'll read the test, then remove the log call and see it go red.
**Lesson:** "retry later" is for the client. The operator still needs to know why.

### 4. Permanent Stripe errors are reported as retryable — NOTE   (`StripePaymentGateway.cs:74`)
`amount_too_small` and invalid-currency errors also become 503 "repeat the same key", and repeating can never succeed. It's not blocking, because a server total under the provider minimum needs config that doesn't exist today. Once finding 3 is done, the log names the cause. Splitting out a permanent error would be a design change (a new result type), so raise it with me before you do it.

### 5. Orphan intents and the expiry race are money-safe — NOTE   (`PaymentAttempts.cs:54`)
(a) A crash after Stripe creates the intent but before the id is stored, followed by expiry, stages no cancel. The client never got the secret, so nobody can pay that intent. (b) An expiry between `CompleteAsync`'s status read and the create returns a secret for a `Cancelled` order. Paying it goes down the late-success path (rule 8, test 19). Neither needs a change. Don't "fix" either one with a Stripe call inside the expiry transaction. Separately, `:56` pairs the stored id with this call's secret. That is safe only while finding 1's stable key holds.

### 6. `StripeOptionsValidator` has no test — NOTE   (`StripeOptions.cs:17-31`)
The one guard that stops a Production start with no Stripe secrets is untested. Add two small unit tests: Production with empty values → `Fail`, Development → `Success` with a warning. Not blocking: it is configuration, not money movement.

### Decisions
- None new. Rulings: (a) the cancel of a superseded intent goes through the outbox (`PaymentIntentCancelRequested`), and that is correct for rule 6. (b) A missing secret key in Development → 503, and outside Development startup fails (`StripeOptionsValidator`). Both are consistent with §3.4 rule 10. (c) No intent is re-created after the 24 h key lifetime. The 15-minute reservation TTL and the `PendingPayment`-only guard in `CompleteAsync` make that path unreachable.

**What was good:** the fake gateway throws on any call inside a transaction, so every checkout, replay and retry test enforces rule 6. `AttachProviderIdAsync` is one conditional `UPDATE` and can't overwrite a webhook's status.

### Can 4.4 move to Done?
No. Findings 1–3 must be fixed and re-checked. `docs/ecommerce-app-context.md:74` stays **Review open**.

## Round 1 — Marco → Rob
All three findings fixed. Notes 4 to 6 skipped (not trivial, or need your call).

**1. Fixed.** `FakePaymentGateway.cs` gets `FailAfterCreate` (records the intent, then throws). New `CheckoutTests.A_response_lost_after_the_provider_made_the_intent_is_healed_by_the_same_key_with_the_same_intent`: first call 503, same key 200, `Creates.Count == 2` both `pi:{orderId}:1`, one intent, stored id and client secret match, one order. T24 now asserts the keys are `pi:{orderId}:1` then `pi:{orderId}:2` (`RetryAndExpiryTests.cs`). Your M1 (random key, `PaymentAttempts.cs:54`): Checkout run went `Failed: 2, Passed: 49` (the new test and T24). Restored from a scratch copy, `cmp` matched.

**2. Fixed.** `StripePaymentGatewayTests`: new `A_create_posts_the_minor_unit_amount_lower_case_currency_metadata_card_only_and_the_idempotency_key` (path, `Idempotency-Key`, `amount=4567`, `currency=usd`, both metadata ids, `payment_method_types[0]=card` and no `[1]`, id/secret mapping). The error test and the missing-key test now also cover `CreateIntentAsync`. Your M2 (no header and amount + 1): `Failed: 1, Passed: 78`. Restored, `cmp` matched.

**3. Fixed, with one design deviation.** `Application` may not name `Stripe` or `Microsoft.Extensions` (ArchitectureTests `Layer_source_files_never_name_a_forbidden_namespace` and `Application_types_depend_only_on_...` went red when I first put an `ILogger` in `PaymentAttempts`). So `EnsureIntentAsync` (`PaymentAttempts.cs`, catch of `PaymentProviderUnavailableException`) calls a new `IPaymentAlerts.IntentCreationFailed(orderId, paymentId, attempt, innerException)` and rethrows. `LoggingPaymentAlerts` writes one Warning: `OrderId`, `PaymentId`, `Attempt`, `StripeErrorType`, `StripeErrorCode`; the exception message and the exception itself are not logged. Tests (`PaymentAttemptsTests`): alert called once with ids and rethrow; collecting logger has one entry with ids, type/code, no "secret", "sk_" or amount; a non-Stripe cause leaves the code empty. Red first: stubbed method logged nothing, `Assert.Single() Failure: The collection was empty` (2 failed). Green after. Mutation: replacing the alert call made the first test fail (line 75). Restored.

Existing behaviour untouched; no contract change, no package.

## Round 2 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings, jest 149 suites / 987 tests; run at the start and again after all mutations were restored)

All three round 1 findings are fixed, and every mutation I asked for now goes red. One new problem is blocking: a flaky assertion in the new log test. Scope check first: `git diff --stat` is the same 21 files (348+/86−) before and after my mutations. Outside your list it shows only the pre-existing changes (the `HashOf` change, TopNav, `useCartCount`, and the 4.2/4.3 fix-round tests plus the `PostgresFixture` thread cap, which are already reviewed in those threads). No other test run was using Postgres.

### Replies to your round 1
**1. Confirmed.** I re-applied M1 (`pi:{Guid.NewGuid()}` at `PaymentAttempts.cs:57`). Checkout went `Failed: 2, Passed: 49`, red on `A_response_lost_after_the_provider_made_the_intent_is_healed_by_the_same_key_with_the_same_intent` and T24. The new test is what I asked for. `FailAfterCreate` records the intent and then throws, both creates carry `pi:{orderId}:1`, there is a single intent, and the stored id, the client secret and the single order all match. The T24 key assertion (`pi:{orderId}:1`, then `:2`) is an added check, so it is stronger, not weaker.
**2. Confirmed.** I applied M2 as two separate mutations, so each one has to fail on its own. No `Idempotency-Key` gave `Failed: 1, Passed: 80`. Amount + 1 gave `Failed: 1, Passed: 80`. Both failed in `A_create_posts_the_minor_unit_amount_…`. Restored, and `cmp` matched.
**3. Confirmed, and I accept the design deviation.** My ruling is below. Removing the `alerts.IntentCreationFailed` call → `Failed: 1` (`A_failed_intent_creation_reports_…_and_rethrows`). Replacing `throw;` with a swallowed `return new PaymentIntentHandle("", "", …)` → `Failed: 1`, the same test. (Deleting `throw;` outright won't compile, CS0165, so I used the swallow version.) My own extra mutation: passing `providerError` as the log's exception → `Failed: 1` in `The_logged_intent_failure_…_no_secret_or_amount`, through `HasException`. All restored, and `cmp` matched.

### Ruling on the `IPaymentAlerts` port
Accepted. It fits §6.2. `IPaymentAlerts` is an existing `internal` Application port. It already serves `PaymentWebhookService`, and `PaymentsModule.cs:32` already registers it as a singleton, so DI needs no change. `Microsoft.Extensions.Logging` and `Stripe` appear only in Infrastructure, where `StripePaymentGateway` already uses Stripe. ArchitectureTests pass 25/25. The Warning logs only `OrderId`, `PaymentId`, `Attempt` and Stripe `type`/`code`. It leaves out the exception message, the exception object, the amount, the key and the client secret. The method has no amount parameter, so it can't leak one. The port takes `Exception?` rather than a Stripe type, so Application stays free of Stripe. That is right. Changed tests: the renamed error test keeps cancel and refund and adds create. The missing-key test adds create, and `Assert.Empty(handler.Seen)` still covers all three calls. Neither one is weakened.

### 1. The "no amount" assertion is flaky — BLOCKING   (`PaymentAttemptsTests.cs:98`)
**What I saw:** `Assert.DoesNotContain("123", everything)`, where `everything` includes two `Guid.NewGuid()` values formatted as text.
**Why it matters:** a random GUID has about 22 three-character runs inside its hex groups, so two GUIDs contain `123` about 1% of the time (44/4096). Then the gate goes red on code that is correct. Roughly one in a hundred `verify.sh` runs fails for no reason. Each such run teaches people to rerun until it goes green, which is how a real red gets ignored.
**What I want:** use fixed GUIDs with no `123` in them, e.g. `Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")`, or assert on `"123.45"` and `"12345"` (the decimal and minor-unit forms). Keep the `secret` and `sk_` checks as they are.
**How I'll check:** I'll read the line, then re-run the extra mutation (log the exception) and see it stay red.
**Lesson:** an assertion on random data must be unable to match by chance.

### 2. A missing key logs as an anonymous failure — NOTE   (`StripePaymentGateway.cs:67`)
`PaymentProviderUnavailableException("Stripe:SecretKey is not configured.")` has no inner exception, so the Warning shows an empty type and code, the same as a network blip. That can only happen in Development, because `StripeOptionsValidator` stops a Production start. Not blocking. If you're already in this file for item 1, you may add a `Reason` field (`not-configured` / `provider-error`). Don't log the message.

Round 1 notes 4–6 stay open as notes. None blocks.

**What was good:** the port keeps Application free of logging without a new abstraction, because it reuses the existing alert seam. The `FailAfterCreate` test models the exact crash the derived key exists for.

### Can 4.4 move to Done?
Not yet, and only because of item 1. It's a one-line fix. `docs/ecommerce-app-context.md:74` stays **Review open**.

## Round 2 — Marco → Rob
**1. Fixed.** `PaymentAttemptsTests.cs:~84-98`: the GUIDs are now fixed (`aaaaaaaa-0000-…-0001`, `bbbbbbbb-0000-…-0002`), so no random data. `DoesNotContain("123")` became `"123.45"` and `"12345"`. The `secret` and `sk_` checks are unchanged. Mutation: I added `providerError?.Message` to the log, and the test went red (`Sub-string found`, 1 failed, 2 passed). Restored with `cmp` identical, then 3/3 green. The exception message holds `sk_` and `123.45`, so the `sk_` check fired first. The amount checks are direct and no weaker than before. Note 2 (`Reason` field) not done: out of scope. Full `verify.sh`: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`.

## Round 3 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (build 0 errors / 0 warnings, jest 149 suites / 989 tests; run after all mutations were restored)

I checked only round 2 item 1. Scope: `git diff --stat` shows 22 files (383+/86−). That is the round 2 set of 21 files plus `hooks.test.tsx` (the 4.5 fix), and `PaymentAttemptsTests.cs` is still untracked. Nothing else changed.

### Replies to your round 2
**1. Confirmed.** I read `PaymentAttemptsTests.cs:81-100`. Both GUIDs are fixed (`aaaaaaaa-…-0001`, `bbbbbbbb-…-0002`), so the test can no longer match by chance. Neither GUID contains `123.45` or `12345`. The `secret` and `sk_` checks are unchanged, and so is `HasException`.

You said your mutation went red on `sk_` first, so I proved the amount checks on their own. I added an `Amount={Amount}` field to the Warning in `LoggingPaymentAlerts.cs:29` and passed a literal, with no secret and no exception message:
- `"123.45"` → `Assert.DoesNotContain() Failure: Sub-string found`, `Failed: 1, Passed: 0`.
- `"12345"` → the same failure, `Failed: 1, Passed: 0`. Here only the `12345` check can match, because the log text contains no `secret`, `sk_` or `123.45`.

Both restored from a scratch copy, and `cmp` matched. The test now guards the decimal amount and the minor-unit amount, each on its own.

Note 2 (`Reason` field) stays open as a note, as do round 1 notes 4–6. None blocks.

**What was good:** a fixed GUID was the right fix. Remove the randomness and keep the assertion strict, rather than loosening it.

### Can 4.4 move to Done?
Yes. `docs/ecommerce-app-context.md:74` is set to **Done**.
