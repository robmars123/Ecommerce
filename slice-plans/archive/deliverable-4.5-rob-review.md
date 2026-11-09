# Deliverable 4.5 — Review thread

Checkout screen: address, shipping method, estimate, server total, Stripe payment on web and native (`features/checkout`, `lib/platform.*` · Slice 4).

## Round 1 — Rob → Marco (for Nathaniel)
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (jest 149 suites / 982 tests, tsc clean, export OK)

Scope: committed code on `dev`. I read `features/checkout/*` (api, estimate, types, the four hooks, `CheckoutView`, `PaymentStep`, `AddressForm`), `lib/platform.d.ts`, `.web.tsx`, `.native.tsx` and their tests, against arch §5.2 and F4, F5, F6, F10, F11 (§11.3). The working-tree `TopNav` and `useCartCount` changes are out of scope. The structure is right: the key is made lazily and kept until a definitive answer, the order id goes in the URL, payment success is only a hint followed by polling, and the platform split is checked by `tsc`. Three behaviours are wrong, though, and one of them can create a second order.

### Invariants → code → tests

| Invariant | Where it holds | Tests |
|---|---|---|
| Body has no price, total or status (rule 1, F6) | `toBody` → `CheckoutBody` (address + method code) | F6 test: exact keys, regex for no money words |
| Displayed total = server reply; estimate only before it (§5.2, F6, F10) | `PaymentStep` `placedTotal ?? order.grandTotal`; `estimateTotal` labelled, display only | F10 estimate test, F6 `Total $54.60`, refresh test `Total $45.00` |
| One key per attempt; double tap, network failure and 503 reuse it; a definitive 4xx or a success renews it (F4) | `useAttemptKey`, `isDefinitiveFailure`, `inFlight` ref | hooks ×4, view F4, outage, accept-new-prices |
| No optimistic order or paid state (F5, rule 5) | button `pending`; `confirming` polls `GET /orders/{id}` | F5 ×2, processing, redirect_status |
| Server 400 field errors per field (F10) | `fieldMessages` → `shippingAddress.<f>` (matches `CheckoutController` `CamelPath`) | F10 field-error test |
| 409 lines named; price change → new attempt | `lineText`, `acceptNewPrices` | rejected-lines ×2 |
| Secret → platform form; decline → retry on the same order, one key per retry; `reservation-expired` → check out again (F11) | `PaymentStep`, `useRetryPaymentMutation` | F11 ×3, cancelled, paid |
| Refresh never places a second order | `orderId` in URL → `PaymentStep` → retry-payment | refresh test (0 `POST /checkout`) |
| Stripe confirm: web `confirmPayment(if_required)`, native PaymentSheet; publishable key from env, empty in `.env.example` | `platform.web.tsx`, `platform.native.tsx`, `getStripePublishableKey` | `platform.test.web.tsx` ×6, `.test.native.tsx` ×6 |
| Rule 17: platform code only in `lib/platform.*` | `platform.conformance.ts` (tsc) | tsc clean |

### Mutation checks (Jest `features/checkout`, ios + android projects, so each failing test counts twice; each file restored from a scratch copy; `cmp` matched; afterwards `git diff --stat -- frontend` shows only `TopNav.tsx` / `TopNav.test.tsx`, with `useCartCount.ts` untracked)

| # | Guard broken | Result |
|---|---|---|
| FM1 | `useAttemptKey.ts:16`: new key on every `current()` | RED: `14 failed, 44 passed` |
| FM2 | `usePlaceOrderMutation.ts:20`: renew on every error | RED: `8 failed` (network retry, 503 retry ×2, view F4) |
| FM3 | `:20`: never renew after a definitive 4xx | RED: `6 failed` (view F4, accept-new-prices, hook 409) |
| FM4 | `PaymentStep.tsx:39`: total = `order.subtotal` | RED: `4 failed` (F6, refresh) |
| FM5 | `CheckoutView.tsx:100`: body gains `grandTotal: estimate` | RED: `2 failed` (F6) |

### 1. The form stays editable under an unresolved key, so an edit leads to a second order — BLOCKING   (`CheckoutView.tsx:92,152,165`, `usePlaceOrderMutation.ts:20`)
**What I saw:** after a network failure or a 503, `busy` is false, so the address and method fields are editable again, but the key is kept. If the customer fixes a typo and taps Place order, the same key goes out with a different body.
**Why it matters:** after a 503 the server has definitely committed order 1 (its `orderId` is in the reply), and after a lost response it probably has. The edited resend gets 409 "That changed while you were editing" (KeyConflict, kind `conflict`), and `isDefinitiveFailure` renews the key. The next tap places order 2. Now there are two `PendingPayment` orders and double reserved stock. On the last unit, the customer's own order 1 makes order 2 fail "sold out" for 15 minutes. This is the Slice 4 "double order" challenge, and every test sends the same body twice, so none of them reaches it.
**What I want:** freeze the body with the key. While the last attempt ended in a non-definitive failure, show the address and method read-only, have the button resend the identical body with the same key, and unfreeze only on a definitive answer or success. The alternative is to bind a new key to a changed body. Don't do that: it still double-orders. A Jest test: network failure → the fields are not editable → tap → the second request has the same key and a body deep-equal to the first.
**How I'll check:** I'll read the test, then make the fields editable again. It must go red.
**Lesson:** an idempotency key promises "same request". Once the key is out, the UI must not let the request change.

### 2. "Nothing was charged" is false after a late success — BLOCKING   (`PaymentStep.tsx:69`)
**What I saw:** every `Cancelled` order shows "Nothing was charged."
**Why it matters:** the customer pays in the last seconds, and the webhook lands after the expiry worker. The order stays `Cancelled` and the card is charged, then refunded automatically (§8.2 rule 8, test 19). The screen, while it is polling for that same payment, tells them nothing was charged. That is a false statement about their money, at the moment they're looking at their bank app.
**What I want:** copy that doesn't claim what the client can't know, e.g. "This order was cancelled because the payment was not completed in time. If a payment went through, it is refunded automatically." Assert the new text in the cancelled test.
**How I'll check:** I'll read the assertion.
**Lesson:** the UI states only what the server told it.

### 3. On web, an incomplete card field is treated as a decline — BLOCKING   (`platform.web.tsx:49-50`)
**What I saw:** any `error` from `confirmPayment` → `onResult({ status: 'failed' })`. `PaymentStep` then unmounts the form and offers "Retry payment".
**Why it matters:** Stripe returns `error.type === 'validation_error'` when, say, the CVC is empty, and it makes no API call. Here, a missing CVC tears the form down, and "Retry payment" posts `retry-payment`. That creates attempt n+1, stages a cancel for a perfectly good intent, and makes the customer type the whole card again. Each typo costs a payment attempt and two Stripe calls.
**What I want:** on `validation_error`, keep the form mounted and don't call `onResult`. The Payment Element already shows the message inline. Add a test in `platform.test.web.tsx`: `confirmPayment` resolves `{ error: { type: 'validation_error' } }` → `onResult` not called.
**How I'll check:** I'll run that test against the current line 49. It must be red first.
**Lesson:** "the provider said no" and "the form isn't filled in" are different outcomes. Only the first one is a payment attempt.

### 4. Cart emptying: settled in the plan, not in the docs — NOTE (question for Nathaniel)
My 4.3 note 5 was wrong to say no doc covers this. Slice 4 plan D17 decides it: on Paid the ordered lines are removed, in the webhook transaction (`OrderPaymentHandler.cs:49`, tested by `WebhookTests.Paying_removes_the_ordered_lines_from_the_cart`), and expiry keeps the cart (app context §4.5.7). It isn't BLOCKING, because no arch or app-context rule requires it. But D17 isn't in §13.2 or app context §4.5. **Open for Nathaniel:** confirm D17 and say whether it gets a decision-log row. I didn't add one. Client side: `PaymentStep` doesn't invalidate `queryKeys.cart` when the order turns `Paid`, so `/cart` and the nav count show the old lines until they refetch.

### 5. Mixed rejected lines offer only "Accept new prices" — NOTE   (`CheckoutView.tsx:183-191`)
If one line is `PriceChanged` and another `OutOfStock`, the screen shows only "Accept new prices", with no cart link. The resubmit fails again on the sold-out line. Show the "Edit your cart" link whenever any line isn't `PriceChanged`.

### Decisions
- None new. Rulings: (a) the estimate (cart subtotal + method price) is client arithmetic, and it is allowed. §5.2 and F10 require an estimate labelled as one. It is never sent (F6, FM5), and the reply total replaces it. (b) A web refresh loses the in-memory key and the address. That was accepted in plan D21 ("persisting address/key on device" was rejected). The URL `orderId` covers the refresh after placing.

**What was good:** `isDefinitiveFailure` is one small, well-named rule, and FM2 and FM3 show both of its sides are tested. `mockApi` answers 599 to any unmocked call, so a stray request fails loudly instead of passing silently.

### Can 4.5 move to Done?
No. Findings 1–3 must be fixed and re-checked. `docs/ecommerce-app-context.md:75` stays **Review open**.

## Round 1 — Marco → Rob
All three findings fixed, test-first. Notes 4 and 5 skipped (4 is a question for Nathaniel, 5 is not blocking).

**1. Fixed.** `usePlaceOrderMutation.ts` now freezes the first body together with the key: `submit` sends the locked body if one exists, and the lock clears only on success or `isDefinitiveFailure` (the same place the key renews). It returns `locked`; `CheckoutView.tsx:93,153,166` sets `editable={!busy && !frozen}` on the address and method fields. Tests in `CheckoutView.test.tsx`: network failure then 503 then success: fields `editable === false`, a `changeText` in between is ignored, all three POSTs have a deep-equal body and the same `Idempotency-Key`; a 409 unlocks the fields. Red before: `4 failed` (this test and the cancel test, two jest projects each; `Expected: false, Received: true`). Green: `62 passed`. Mutation: removing `&& !frozen` gave `2 failed, 60 passed`; restored, `cmp` matched.

**2. Fixed.** `PaymentStep.tsx:69` now reads "...not completed in time. If a payment went through, it is refunded automatically." The cancelled test asserts the full text and that "Nothing was charged" is absent.

**3. Fixed.** `platform.web.tsx:49`: `error?.type === 'validation_error'` returns without `onResult` (the `finally` still clears the busy state). New `platform.test.web.tsx` test, red first: `Expected number of calls: 0, Received number of calls: 1`. Mutation: guard replaced with `false` gave `1 failed, 7 passed`; restored, `cmp` matched. The declined-card test (no type) still reports `failed`.

Jest `features/checkout` + `lib/platform`: 82 passed. No package added; `HashOf`, TopNav and `useCartCount` untouched.

## Round 2 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (jest 149 suites / 987 tests; run at the start and again after all mutations were restored)

All three round 1 findings are fixed, and the three mutations I asked for go red. One gap is left. The hook's own body freeze, the line that binds the body to the key, has no test.

### Replies to your round 1
**1. Confirmed for the screen, Not yet for the hook** (item 1 below). Removing `&& !frozen` at both `editable` props → `2 failed, 60 passed` (the new F4 lock test, in both projects). Making the key renew and unlock on every error (`if (true)` at `usePlaceOrderMutation.ts:31`) → `10 failed, 52 passed`. The "definitive answer unlocks" test pins the other side. Restored, and `cmp` matched.
**2. Confirmed.** `PaymentStep.tsx:69` has the neutral wording. The cancelled test asserts the full sentence exactly, and that "Nothing was charged" is absent. That is stronger than the old `/This order was cancelled/` regex, not weaker.
**3. Confirmed.** Replacing the `validation_error` guard with `false` → `1 failed, 19 passed` (web project). The declined-card test, which has no `type`, still reports `failed`. The `finally` clears busy, so the button comes back.

### Second-order check (409 → new key → edited body)
That path is closed in the shipped UI. The key renews in only two places, `onSuccess` and the definitive branch of `onError`, and both also unlock. So for the whole life of a key, the fields are read-only and `submit` sends `lockedRef.current`. A KeyConflict 409 needs the same key with a different body, and this client can't produce one. The only renewing 409s are definitive answers (cart-empty, lines-rejected). For those the server has created no order under that key, so the new key starts the first order, not a second one. One path is still open: remount or refresh drops the in-memory key while order 1 is `PendingPayment`. That was accepted in plan D21 (round 1 ruling b), and the 15-minute expiry bounds it. I'm not reopening it.

### 1. The hook's body freeze is untested — BLOCKING   (`usePlaceOrderMutation.ts:43`)
**What I saw:** I replaced `const sent = lockedRef.current ?? body;` with `const sent = body;`. All 62 checkout tests stayed green. The F4 view test's `changeText` on a read-only field is ignored by the test renderer, so the body never differs and the test can't tell whether the hook froze it.
**Why it matters:** that line is the guarantee your comment promises, "the next submit resends the first body". The `editable` prop is only the UI half. If a later change lets the body change while the key is held, a refactor can drop the line with nothing going red, and the round 1 double order is back. Examples: a shipping-method default derived from a refetched list, browser autofill on web, or a second caller of the hook. Code on the double-order path that no test pins is the same as no guard.
**What I want:** one hook test in `usePlaceOrderMutation.test.ts`. `submit(A)` → network failure → `submit(B)` → the second request's body deep-equals A, with the same `Idempotency-Key`, and `locked` equals A. Then a definitive 409 → `submit(B)` sends B with a new key, and `locked` is B.
**How I'll check:** I re-apply `const sent = body;`. The new test must go red.
**Lesson:** test a guard where it lives. A test that only reaches it through a disabled input proves the input, not the guard.

### 2. Native card-sheet validation — NOTE
I checked only `platform.web.tsx`, as asked. The native payment sheet does its own field validation before it returns, so I expect no equivalent bug there. I haven't verified it. Not blocking.

Round 1 notes 4 (D17 cart emptying, for Nathaniel) and 5 (mixed rejected lines) stay open as notes.

**What was good:** `locked` and the key renew and clear in the same two branches, so they can't drift apart. The F4 test checks all three POSTs for both body and key, across a network failure and a 503.

### Can 4.5 move to Done?
Not yet, only item 1. That's one test. `docs/ecommerce-app-context.md:75` stays **Review open**.

## Round 2 — Marco → Rob
**1. Fixed.** New test `usePlaceOrderMutation body freeze (F4)` in `features/checkout/hooks/hooks.test.tsx` (the existing hook test file; no separate file). Sequence: `submit(A)` network failure, `submit(B)` sends A with the same key and a 503, `locked` equals A, `submit(B)` again sends A and gets a 409, `locked` clears, `submit(B)` sends B with a new key. I used a 503 for the middle step because a 409 unlocks at once, so `locked` could not be observed.
Red under `const sent = body;` (ios and android): `Expected shippingMethodCode "standard" / Received "express"` at the body-equals-A assertion, 2 failed, 10 passed. Restored from a scratch copy, `cmp` identical, then 12/12 green. No production change. Full `verify.sh`: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`. Note 2 is acknowledged, not changed.

## Round 3 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` (jest 149 suites / 989 tests; run after all mutations were restored)

I checked only round 2 item 1. Scope: `git diff --stat` shows 22 files (383+/86−). That is the round 2 set plus `hooks.test.tsx` (+35, test only). The 4.4 test file is still untracked. No production file changed.

### Replies to your round 2
**1. Confirmed.** I read `hooks.test.tsx:100-133`. The test drives the hook itself, with no screen and no read-only input. It runs `submit(A)` → network failure → `submit(B)` sends A with the same key → 503 → `locked` equals A → `submit(B)` sends A → definitive 409 `cart-empty` → `locked` clears → `submit(B)` sends B with a new key. That covers what I asked for, plus one extra resend.

I re-applied `const sent = body;` at `usePlaceOrderMutation.ts:43`. The result was `Tests: 2 failed, 10 passed, 12 total` (ios and android projects), with an Expected/Received diff on the body. Restored from a scratch copy, `cmp` matched, then `12 passed`.

**The 503 is sound.** `isDefinitiveFailure` (`useAttemptKey.ts:31`) renews the key only for `validation`, `conflict`, `forbidden`, `not-found` and `client`, so a 503 keeps both the key and the lock. That is the correct client behaviour. A `payment-provider-unavailable` 503 carries an `orderId`, which means the server has already created the order and only the intent failed. Under the same key, the 4.4 heal path replays that order and its intent `pi:{orderId}:1`, so no second order is made. Renewing on a 503 would create a second order. You were also right that a 409 can't serve as the middle step. It unlocks at once, so the test could never observe the held lock.

Note 2 (native sheet validation) and round 1 notes 4–5 stay open as notes. None blocks.

**What was good:** the test checks the lock state between steps as well as the bodies sent. A future change that sends the right body but drops `locked` would also go red.

### Can 4.5 move to Done?
Yes. `docs/ecommerce-app-context.md:75` is set to **Done**.
