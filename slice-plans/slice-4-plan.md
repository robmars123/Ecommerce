# Slice 4 — Checkout + Payments — plan (Rob)

Mode: Autonomous · Baseline: `slice-plans/slice-4-baseline.txt` (review = paths new or changed since it).

## 0. Read, and rules

**Read:** arch §2, §3.4 · §6.2 · §7.1 rows `orders`, `payments`, `refunds`, `idempotency_keys`, `outbox_events` · §7.3 · §7.4 · §8.2 · §8.4 · §8.5 (first and "Automatic refunds" bullets) · §9 auth, checkout, orders contracts · §10.1 · §11.2 rows 1–8, 19, 23–25 · §11.3 · §5.2, §5.4, §5.5 · §13.2 grep: `Stripe client SDKs`, `Retry payment and multiple intents`, `lock order`, `Idempotency store`, `(Slice 4)` · app context §4.5 · quality bar §1, §2, §4.

**Marco:**
- Read-only git only.
- New csproj (Payments, Payments.Contracts, Payments.Tests) → `Ecommerce.slnx` **and** `Dockerfile` COPY lines, then `bash scripts/verify.sh docker`.
- Smoke only on :5090 against `ecommerce_smoke`; stop **your** process before verify (a leftover one locked the Release DLLs last time). Never touch :5080/:8081/:5081/:8082.
- No literal anywhere (code, tests, report) matching the secrets scan. Test Stripe secrets are random per run, like the JWT key in `ApiFactory`.
- For files already dirty in the baseline, quote each edit (old → new) in your report.

## 1. Scope

**In:** `POST /checkout`, `GET /shipping-methods`, `POST /orders/{id}/retry-payment`, `POST /webhooks/stripe`; Payments module; outbox table + inserts; Paid + inventory commit; expiry → `Cancelled`; auto-refund **row + outbox event + audit + alert**; `paymentStatus` on order detail; `/checkout` with Stripe on web and native via `lib/platform`.
**Out:** all outbox **consumers** (Stripe cancel/refund, email, SignalR) and `consumer_checkpoints`/`processed_events` → Slice 6 (D9). Admin refund and admin screens. Rate limits (7). Saved addresses. Customer cancel (no §9 endpoint — Test 7 "cancel" is a doc gap).

## 2. Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | New **Payments** module (`Ecommerce.Payments`, `.Contracts`, `Ecommerce.Payments.Tests`): owns `payments`, `refunds`, Stripe gateway, webhook. Refs SharedKernel, Persistence, Payments.Contracts, Orders.Contracts | §6.1: Orders never calls Stripe | Payments inside Orders |
| D2 | Contracts, one consumer each: `IPaymentAttempts` (Orders); `IOrderPayments` (Payments), `IOrderExpiry` (Inventory); `IInventoryReserver` + `CommitAsync`, `GetActiveExpiryAsync` (Orders); `ICartReader`, `ICartWriter` (Orders). `IPaymentGateway` stays **internal** to Payments | Minimal surface | Public gateway, no outside consumer |
| D3 | **Lock order:** `payments` → `orders` → `inventory_reservations` → `inventory_stock` (asc) → `carts`. Checkout/retry flush their `idempotency_keys` row first (D4). Retry and expiry only *read* payments | Webhook must re-read its payment under lock before deciding; no cycle | Order-first webhook (two-step contract) |
| D4 | **Checkout idempotency:** first in the tx, `IIdempotencyStore.Add(checkout, key, user, hash, ResultRef = new order id)` then `IUnitOfWork.SaveChangesAsync` (flush). A concurrent duplicate blocks on the PK, gets `UniqueConstraintViolationException` → replay. `orders.idempotency_key` UNIQUE NOT NULL backstop. Other user or body hash → 409. Replay re-reads rows by `ResultRef`; no `response_json`. Store reused unchanged | Without the flush a duplicate waits on the stock lock and gets 409. Slice 3 note: checkout/retry replay current state; cart-merge records stay additive-only | Stored HTTP body |
| D5 | **Intent after commit:** `EnsureIntentAsync(paymentId)` — provider id stored → retrieve client secret (never re-create; Stripe keys last 24 h); else create with key `pi:{orderId}:{attempt}`, metadata `order_id`, `payment_id`, card only, amount = `grand_total` × 100 (must be integral), save provider id. Gateway failure → 503 `payment-provider-unavailable` + `orderId`; same-key repeat heals | Rule 6; invariant 2 | Storing the secret |
| D6 | **No `orders.stripe_payment_intent_id`**: the latest attempt is the current intent; `payments.provider_payment_id` UNIQUE holds invariant 2 | A copy drifts; no reader | Old §7.1 column |
| D7 | **Webhook:** Stripe.net signature check outside any tx (bad/missing/stale or no secret → 400); read only `id`, `type`, `data.object.id`, no API-version throw. Handle `payment_intent.succeeded`/`.payment_failed`/`.canceled`; others → 200, no write. Tx: lock payment by provider id (unknown → 200 + Warning) → `FindAsync(stripe-event)` seen → 200 → `Add` → apply. Unique violation at commit → 200. Other exception → 500 (Stripe retries, Test 8) | §8.2 r6, r12 | Typed events |
| D8 | **Success:** payment `Pending`/`Failed` → `Succeeded` (already → no-op). `TryMarkPaidAsync`: order `PendingPayment` → `Paid` (history, actor null), `CommitAsync`, `RemoveLinesAsync(user, variants)`, outbox `OrderPaid`; then cancel requests for the order's other attempts with an intent and not `Succeeded`. Any other order status → **auto-refund**: refund `Pending`, full payment amount, key `auto:{paymentId}`, actor null; audit `payment.auto-refund` (reason `late-success`/`extra-success`); outbox `RefundRequested`; `LogCritical` EventId `PaymentAlert`. `payment_failed`/`canceled` → `Pending` → `Failed`, order unchanged | §8.2 r8, r12. `Failed → Succeeded`: PaymentSheet lets the customer retry a declined intent; §7.3 edited | Refusing it (money, no order) |
| D9 | **Outbox insert only:** `PaymentIntentCancelRequested` and `RefundRequested` are written now; their Stripe consumers come with Slice 6's runtime. Tests 19/25 prove one refund row + one event; Slice 6 Test 27 one Stripe call | §12.3 row 4 "outbox insert" | Consumers now |
| D10 | Platform table `outbox_events`; SharedKernel `IOutbox.Add(OutboxMessage)` stages like `IAuditLog`. `aggregate_version` = order `Version` (Order events), else 1. Payloads internal until Slice 6 | Rule 6 | Per-module outbox |
| D11 | **Expiry** stays in Inventory. Per order, in its tx, call `IOrderExpiry.CancelExpiredAsync` **first** (locks the order): `PendingPayment` → `Cancelled` (actor null, reason `reservation-expired`) + `RequestCancelOpenIntentAsync`; absent/other status → false. Then existing lock/recheck/expire/release; cancelled but nothing due → throw. `SweepAsync` catches per order and continues (carry-in) | Slice 2 D5; its tests stay | Worker moved to Orders |
| D12 | **Retry** (Orders): 404 unknown, 403 not owner, before any tx. Tx: flush key (`retry-payment`, hash = order id, ResultRef = new payment id) → lock order → not `PendingPayment` → 409 `not-pending-payment`; `GetActiveExpiryAsync` null or ≤ `IClock` now → 409 `reservation-expired` → `StartAttemptAsync` (n+1, stored `grand_total`, cancel request for the previous attempt if it has an intent and isn't `Succeeded`). After commit `EnsureIntentAsync` → 200 `{ orderId, attempt, clientSecret }` | §8.2 r11 | Re-pricing; TTL extension |
| D13 | **Lines** (in tx, before reserve): empty cart → 409 `cart-empty`; unknown/inactive → `Unavailable`; price ≠ cart `display_unit_price` → `PriceChanged`; other currency → 400 (§7.4); then `ReserveAsync` unchanged → `OutOfStock` + `available`. 409 type `checkout-lines-rejected`, `lines: [{ variantId, reason, available? }]`. SKU/names from `GetVariantsAsync`, price from `GetVariantSnapshotsAsync` | App context §4.5.3; no Catalog contract change | Widening `VariantSnapshot` |
| D14 | Orders `CheckoutOptions` (`ValidateOnStart`): `Shipping:Methods`, `Shipping:Countries`, `Tax:RatePercent` (0), `Store:Currency`, `Checkout:ReservationTtlMinutes` (15, 1–120). Empty lists → startup Warning (like Carriers). Concrete `ShippingMethods`, no `IShippingRateProvider` | §7.4; no invented values | Seeded table |
| D15 | Order number: sequence `order_number_seq` → `ORD-{yyyy}-{n:D6}` (`IClock`) | Lock-free | `max()+1` |
| D16 | `POST /checkout` → 200, first call and replay; `clientSecret` null unless `PendingPayment`. Checkout, shipping-methods, retry `[Authorize]` (any signed-in, Slice 3 D1); webhook `[AllowAnonymous]` | Identical replay | 201/200 |
| D17 | Cart lines removed on **Paid** (D8) | Expiry keeps the cart (§4.5.7) | At checkout |
| D18 | `Stripe:SecretKey`, `Stripe:WebhookSecret` = `""` in `appsettings.json`; required outside Development; Development without them → Warning, checkout 503, webhook 400. `EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY` empty in `.env.example`; missing → config error in the payment step | §2 open decision | Fallback keys |
| D19 | **Packages** (rule 16): `Stripe.net` (Payments only, latest stable, exact pin in `Directory.Packages.props`) — official SDK for intents + signature check; replaces nothing. Front, per §13.2 "Stripe client SDKs": `@stripe/stripe-react-native` (`npx expo install`, `app.json` plugin, no options), `@stripe/stripe-js`, `@stripe/react-stripe-js` (exact). Pin `zustand`, `expo-crypto` exactly. `npx expo install --check` green | Stack lock §2 | Hand-rolled HMAC/REST; WebView |
| D20 | **Platform split:** `lib/platform.ts` → `platform.native.tsx` + `platform.web.tsx` + `platform.d.ts` (what tsc and callers see; type-only conformance check per split). Exports: refresh-token API (unchanged) + `PaymentForm({ clientSecret, returnPath, onResult })` → `succeeded | processing | failed | canceled` (+ message). Native: `StripeProvider` + PaymentSheet, `merchantDisplayName` = app.json name, `returnURL` via `expo-linking`. Web: `loadStripe` once, `Elements` + `PaymentElement`, `confirmPayment({ redirect: 'if_required', return_url: origin + returnPath })`. Jest → projects `jest-expo/ios`, `/android`, `/web` (§11.3) | Rule 17; card data never in our code | — |
| D21 | **Screen:** `RequireSignIn(returnTo '/checkout')` → address + method + **estimate** (cart subtotal + method price; "tax calculated at checkout") → submit (disabled while pending) → response total replaces estimate → `PaymentForm` → `succeeded`/`processing`: poll `GET /orders/{id}` every 2 s until `Paid`/`Cancelled` · `failed`/`canceled`: "Retry payment" · 409 `reservation-expired`: "Check out again". `orderId` goes in the URL (`/checkout?orderId=`) once the order exists, so a web refresh resumes there, never a second order. 409 lines per line; `PriceChanged` → "Accept new prices" (existing `PUT /cart/items`, same quantity), then a new attempt | §4.5, F4–F11 | Persisting address/key on device |
| D22 | Detail `paymentStatus` = latest attempt (`GetLatestAsync`); null if none | §9; Test 29/F12 | — |

Rob added four §13.2 rows tagged "(Slice 4)" and updated the lock-order row; §7.1, §7.3, §7.4, §8.2 r11, §9 edited to match.

## 3. Contracts, files, migration

**Public contracts** (named records):
- `IPaymentAttempts`: `StartAttemptAsync(paymentId, orderId, amount, currency)` → `PaymentAttemptInfo(PaymentId, Attempt, Status, ProviderPaymentId?)` (caller's tx) · `RequestCancelOpenIntentAsync(orderId)` (tx) · `GetLatestAsync(orderId)` → info? · `EnsureIntentAsync(paymentId)` → `PaymentIntentHandle(ProviderPaymentId, ClientSecret)` (**no tx open**).
- `IOrderPayments.TryMarkPaidAsync(orderId)` → `MarkPaidOutcome(bool Paid, string OrderStatus)`; `IOrderExpiry.CancelExpiredAsync(orderId)` → bool. Both lock the order `FOR UPDATE`.
- `IInventoryReserver.CommitAsync(orderId)` (lock Active reservations, then stock asc; `Committed`; on_hand −= q, reserved −= q; none Active → `InvalidOperationException`) · `GetActiveExpiryAsync(orderId)` → `DateTimeOffset?`.
- `ICartReader.GetLinesAsync(userId)` → `CartLine(VariantId, Quantity, DisplayUnitPrice, Currency)`; `ICartWriter.RemoveLinesAsync(userId, variantIds)` (locks the carts row, caller's tx).
- SharedKernel `IOutbox.Add(OutboxMessage(EventId, EventType, AggregateType, AggregateId, AggregateVersion, PayloadJson, OccurredAt))`.

**Internal:** Payments — `Payment` (`Succeed`, `Fail`), `Refund.CreateAutomatic`, `Application/PaymentAttempts`, `StripeWebhookService`, ports `IPaymentStore`, `IPaymentGateway` (`CreateIntentAsync`, `GetClientSecretAsync`); `StripePaymentGateway`, `StripeWebhookVerifier`, `StripeOptions`, `PaymentStore`, EF configs; `StripeWebhookController` (raw body). Orders — `Order.Place` (+ history `null → PendingPayment`), `MarkPaid`, `CancelForExpiry` (actor `Guid?`), `OrderItem.Create`, pure `OrderTotals`; `CheckoutService`, `RetryPaymentService`, `OrderPaymentHandler` (both Orders contracts), `ShippingMethods`; `CheckoutOptions` (+validator), store `Add`/`LockAsync`/`NextOrderNumberAsync`; `CheckoutController`, retry action on `OrdersController`, FluentValidation (§9 address rules). Inventory — `Commit` on stock and reservation, reserver additions, expiry change, + Orders.Contracts ref. Cart — one class for both contracts. Orders + Payments/Inventory/Cart/Catalog Contracts refs.

**Migration `AddCheckoutAndPayments`** (additive): `orders.idempotency_key varchar(100) NOT NULL` + unique (table empty outside tests, Slice 5 D11) · sequence `order_number_seq` · `payments` (§7.1; CHECK status `Pending|Succeeded|Failed|PartiallyRefunded|Refunded`, `attempt ≥ 1`, `amount ≥ 0`; unique `(order_id, attempt)`, unique `provider_payment_id`; **no FK to orders**, cross-module) · `refunds` (§7.1; CHECK status; unique `idempotency_key`, `provider_refund_id`; FK → payments) · `outbox_events` (§7.1; identity PK, unique `event_id`, jsonb). Drift stays green.

**Host/config:** `ModuleComposition` + Payments; Api csproj ref. `appsettings.json`: `Stripe` placeholders, `Shipping:Methods: []`, `Shipping:Countries: []`, `Tax:RatePercent: 0`, `Checkout:ReservationTtlMinutes: 15`. Development: one method (`standard`, "Standard (dev placeholder)", `"5.00"`, 5 days), countries `["US"]`.

**Frontend:** `features/checkout/` — `api.ts`, `types.ts`, `hooks/useShippingMethodsQuery.ts`, `usePlaceOrderMutation.ts`, `useRetryPaymentMutation.ts`, `useAttemptKey.ts` (lazy key, kept until `renew()` after a definitive outcome), `components/CheckoutView`, `AddressForm`, `ShippingMethodPicker`, `PaymentStep` (+ tests). `app/checkout.tsx` thin. `lib/api-client.ts` parses 409 `lines`; `lib/config.ts` `getStripePublishableKey()`; `lib/query-keys.ts` `shippingMethods`; orders detail shows `paymentStatus`; `/cart` "Checkout" link if missing; `.env.example` key line, empty.

## 4. Build order (★ test-first: quote the red)

1. ★ Domain: `OrderTotals` (e.g. subtotal 12.05 at 10 % → tax 1.21; banker's gives 1.20), order transitions via `OrderStatusMachine`, `Payment` (incl. `Failed → Succeeded`), `InventoryStock.Commit`.
2. Outbox + `IOutbox`, migration, `OrderSeed` update.
3. ★ Integration red: 2, 3, 4, 19, 23, 24, 25.
4. Checkout + shipping-methods → 1, 2, 5, 6, 23. 5. Payments + webhook → 3, 8, 19, 25. 6. Retry → 24, 7 part. 7. Expiry → 4.
8. Anonymous list, `SolutionLayout`, composition; `verify.sh changed`.
9. ★ F4, F5, F6; then platform split + Jest projects, F10, F11, F12 part.
10. Mutation checks; full `verify.sh`; `verify.sh docker`; smoke on :5090 (checkout → 503 without keys is expected).

## 5. Required tests

Integration: `ApiFactory` + `FakePaymentGateway` (records calls + Stripe keys; **fails if `Database.CurrentTransaction` is not null**, rule 6), a settable test clock, real signature check (tests HMAC-sign `t.body` with a random per-run secret).

| # | Must prove |
|---|---|
| 1 | Via `POST /checkout`: 10 users, 1 unit → one 200, nine 409 `OutOfStock` naming the variant; one Active reservation; available never < 0 |
| 2 ★ | Same key, sequential **and** concurrent → 1 order, 1 payment, 1 intent, identical bodies; other body/user → 409 |
| 3 ★ | Same event, sequential **and** concurrent → one `PendingPayment→Paid` row, one `OrderPaid`, on_hand down once, both 200 |
| 4 ★ | Clock past TTL, sweep → reservation `Expired`, stock released, order `Cancelled` + history, cancel event, new checkout succeeds; unit: one failing order doesn't stop the sweep |
| 5 | Body with `grandTotal`/price/tax/status → 400, no order; gateway amount = server total in minor units |
| 6 | Admin price change leaves item snapshot and totals unchanged |
| 7 part | B retries A's order → 403, no payment row |
| 8 | Test decorator throws once inside the webhook tx → 500, nothing committed; redelivery → `Paid`, one order, one outbox row |
| 19 ★ | Success on a `Cancelled` order (+ duplicate + second event id) → stays `Cancelled`, payment `Succeeded`, one refund `auto:{paymentId}` actor null, one `RefundRequested`, one audit row, `PaymentAlert` in `CollectingSink` |
| 23 ★ | Non-zero rate: §7.4 totals incl. the .005 case; intent = grand total; unknown method, foreign country, client shipping/tax/total → 400, no order; address + method name snapshotted |
| 24 ★ | Attempt 2, new intent, same amount; only from `PendingPayment`; reservation + `expires_at` unchanged; past `expires_at` (no sweep) → 409 `reservation-expired`, no payment; same key twice → one attempt, same secret; cancel event for attempt 1 |
| 25 ★ | Attempt 1 pays, attempt 2 succeeds → `Paid`, one commit, one refund of attempt 2, audit + alert |
| 29 part | `paymentStatus` = latest attempt, before and after Paid |
| Webhook | Missing/bad/stale signature → 400, no change; unknown intent → 200, no change |
| Endpoints | 401 anonymous (3 endpoints); 400 missing key; 400 address rules; 409 `cart-empty`; 409 `PriceChanged`/`Unavailable`; 400 other currency |
| Rule 6 | Fake gateway never sees an open tx (checkout, replay, retry) |
| F4 ★ | One key per attempt; double submit → one request; network retry reuses; new attempt after a 409 fix → new key |
| F5 ★ | Submit disabled while pending; confirming/`PendingPayment` until the polled order is `Paid` |
| F6 ★ | Body keys exactly `shippingAddress`, `shippingMethodCode`; shown total = response `grandTotal` |
| F10 | Methods from `GET /shipping-methods`; per-field server errors; "estimate" until the response |
| F11 | `clientSecret` reaches the mocked `PaymentForm` per platform project; decline → retry, one key per retry (reused on network retry); `reservation-expired` → check out again |
| F12 part | Detail shows payment status |
| Platform | `lib/platform.test.web.tsx` / `.test.native.tsx` run only in their projects; refresh-token assertions carried over |

**Mutation checks** (break → red → restore, report each): skip event dedupe → 3; drop the early key flush → concurrent 2; skip the `expires_at` check → 24; skip the owner check → 7; drop `auto:` uniqueness or the order-status check → 19/25; gateway call inside the tx → rule-6 test.

**Expected test changes** (justify each): `AuthorizationTests.ExpectedAnonymous` + webhook; `SolutionLayout.ExpectedModules` + Payments; `OrderSeed` SQL + `idempotency_key`; `ReservationExpiryServiceTests` constructor + fake `IOrderExpiry`; `lib/platform.test.ts` split per platform. Any other existing-test change blocks unless justified.

## 6. Risks

- Cancels and auto-refunds sit in the outbox until Slice 6: never deploy Slice 4 alone.
- `stripe listen` events may use another API version (D7 reads three fields).
- Jest projects triple the run; tests that stubbed `Platform.OS` move, never weaken.
- `@stripe/stripe-react-native` must match the Expo SDK (`npx expo install`) for Expo Go.
- A web refresh **during** the first checkout request loses the key; a second order is possible and expires unpaid.
- Minor units assume a 2-decimal currency; zero-decimal fails loudly (D5).
- `idempotency_keys` grows (Slice 7).

## 7. Open items for Nathaniel

- **Stripe test keys, local only:** `Stripe__SecretKey`, `Stripe__WebhookSecret` (env or user secrets), `EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY` in `.env.local`. Local webhooks: Stripe CLI forwarding to `http://localhost:5080/api/v1/webhooks/stripe`; its printed signing secret is the webhook secret.
- Real `Shipping:Methods`, `Shipping:Countries`, `Tax:RatePercent`.
- Test 7 "cancel": no customer cancel endpoint in §9 — decide or strike.
- **Unpasted app-context edit (Slice 5 review):** line 55 (§4.1 item 1) → `1. **Shop (first screen):** \`/\` opens the product list; best sellers, categories and search live there.` · line 163 (§5 `/` row) → `| \`/\` | Public | Redirects to \`/products\` (Shop) |`
- `13.2-decision-log.md` (~34 KB) needs a split + index change.
