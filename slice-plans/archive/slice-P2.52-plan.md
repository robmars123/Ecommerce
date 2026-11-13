# Slice P2.52 — Payment card: method, amount due, captured (customer order detail)

Mode: Autonomous · Risk tier: **High** (CLAUDE.md §5) — new money fields and payment state on `GET /orders/{id}` (and the admin detail that embeds it); "captured" must reflect only webhook-confirmed money and be visible only to the owner. Full review + §3 mutations M1–M4 (§5 below).
Scope: tracker row P2.52 only (`docs/admin-dashboard-feature-tracker.md`). Design: `docs/design-refs/northline-order-detail.md` item 5, right column, **Payment** card ("show it only if the data exists"). P2.53 out of scope. **No migration, no new endpoint, no new package, no Stripe call.** All files: **Marco**.

## 1. Read

- `docs/architecture/03-principles-hard-rules.md` §3.4 (rules 1 money, 3 webhook proof, 8 no cross-module join).
- `docs/architecture/09-api.md` line 69 (`GET /orders/{id}` — Rob already added `payment`).
- `docs/architecture/13.2-decision-log.md` — grep only: `Order payment card` (row added by this plan), `Order status note`.
- `docs/architecture/05-frontend.md` §5.5 (tokens). `docs/architecture/11-testing.md` §11.3.
- Backend: `Payments.Contracts/IPaymentAttempts.cs` (`PaymentAttemptInfo`), `Payments.Contracts/IRefundState.cs` (`RefundSummary.Captured`), `Payments/Application/RefundState.cs` (`GetSummaryAsync`), `Payments/Application/PaymentAttempts.cs` line 70 (`ToInfo`), `Payments/Infrastructure/PaymentStore.cs` lines 83–89 (`GetPayingAsync`), `Payments/Infrastructure/StripePaymentGateway.cs` line 22, `Orders/Application/OrderQueryService.cs` (`BuildAsync`), `Orders/Application/OrderViews.cs` (`OrderDetailView`), `Orders/Api/OrderResponses.cs` (`OrderDetailResponse`, its mapping).
- Backend tests: `Checkout/CheckoutEnv.cs` (`PlaceOrderAsync`, `WebhookAsync`, `WebhookRawAsync`, `BodyAsync`, `Sign`, `PayAsync`, `SweepAsync`, `OrderAsync`), `Checkout/WebhookTests.cs` (`PlacedAsync`, T19, T29 at line 405 — the pattern to copy), `Refunds/RefundEnv.cs` (`SeedOrderAsync`, `RefundAsync`, `RefundWebhookAsync`, `RefundIdOf`), one existing happy test in `Refunds/RefundTests.cs`.
- Frontend: `features/orders/types.ts`, `features/orders/status-labels.ts`, `features/orders/components/OrderDetailView.tsx` (`TotalRow`, `Content`), `tests/order-fixtures.ts`, `features/orders/components/OrderDetailView.items.test.tsx` (setup to copy).

§12.3 has no row for P2 tracker rows; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Wire shape:** `OrderDetailResponse` gains last `OrderPaymentResponse? Payment` = `{ method: string, amountDue: Money, captured: Money }`; **null when the order has no payment attempt** (`IPaymentAttempts.GetLatestAsync` returns null). Admin detail embeds it unchanged. `paymentStatus`, `refunds` untouched | Design: show the card only if data exists. Legacy/seeded orders without a `payments` row → no card | Always-present object with zeros (would show a card for orders that never had a payment) |
| **Data source, no new query path:** `BuildAsync` already calls `payments.GetLatestAsync` and `refunds.GetSummaryAsync` (Payments contracts, after the ownership check). Reuse both results | Rule 8: Orders reads Payments only through `*.Contracts`; no join, no `DbSet`. Zero extra DB calls | A new `IPaymentReader` contract (no consumer beyond what the two existing calls give) |
| **`captured` = `RefundSummary.Captured`** (the paying payment's amount: status Succeeded/PartiallyRefunded/Refunded, excluding payments with an automatic refund; 0 when none) | Only a verified webhook calls `Payment.Succeed` (rule 3), so this is webhook-confirmed money; never client data or a Stripe call. Auto-refunded late/duplicate/mismatched captures were never the order's money (same rule as the admin refund ceiling) | Latest attempt's amount (counts unpaid intents); live Stripe retrieve (external call in request path) |
| **`captured` is gross** — refunds do not lower it | Refunds are already listed in the Refunds panel and `paymentStatus` shows Partially refunded / Refunded; a net figure would double-report | Captured net of refunds |
| **`amountDue` = `GrandTotal` if order status is `PendingPayment`, else 0** | `Paid` is set only by the webhook, in the same transaction as `Payment.Succeed` (`OrderPaymentHandler`), so a pending order has nothing captured. Cancelled (expired, never paid) owes nothing. Computed in `OrderQueryService` (server, rule 1) | `GrandTotal - Captured` (wrong for a mismatched capture, which is auto-refunded while the order still owes); client-side derivation |
| **`method` = `"card"`**, from a new internal constant `PaymentMethods.Card` in Payments, used by **both** `StripePaymentGateway` (`AllowedPaymentMethodTypes = [PaymentMethods.Card]`) and `PaymentAttempts.ToInfo`. `PaymentAttemptInfo` gains last positional `string Method` | The PaymentIntent allows card only, so any capture is a card. One source: changing the allowed types forces revisiting the display | Storing a method column (migration, same value for every row); returning the provider name "stripe" (not a payment method) |
| **No card brand / last4** | Not in the `payment_intent.succeeded` payload (the intent references the payment method/charge by id; webhook payloads are not expanded). Getting them needs a live Stripe call (banned on read) or a new `charge.succeeded` handler + columns. No card data is stored today, so none is exposed (PCI: nothing beyond the literal `"card"`). Backlog note added | Fetching from Stripe in the request or in the webhook transaction (rule 6) |
| **No migration.** Existing orders: with a payment row → card computed from it; without → `payment: null`, card hidden | Every value derives from existing `orders`/`payments`/`refunds` columns | — |
| **Currency:** both amounts use the order's currency (like `refunds` today) | The paying payment's currency equals the order's (a mismatch is excluded from `Captured`) | `RefundSummary.Currency` (null when nothing captured) |
| **Client only formats:** `formatMoney(payment.amountDue)` / `formatMoney(payment.captured)`; method label via `paymentMethodLabel` (`card` → `Card`, else the raw value) | Rule 1 | — |
| **UI:** new `Panel title="Payment"` between Totals and Shipping, only when `order.payment` is non-null: a method row with the same classes as the Shipping method row (text only, no icon), then `TotalRow label="Amount due"` and `TotalRow label="Captured"` | Mirrors the mock-up order (Totals, Payment, Shipping); reuses `TotalRow` (a11y label `"Amount due $25.00"`, tabular-nums) | A new row component |

Status matrix (what the API returns; W/R/J tests below cover each):

| Order / payment state | `payment` | `amountDue` | `captured` |
|---|---|---|---|
| No attempt | null | — | — |
| `PendingPayment`, attempt Pending or Failed | `card` | grand total | 0 |
| Paid / Processing / Shipped / Delivered / Returned | `card` | 0 | payment amount |
| Partially refunded or Refunded | `card` | 0 | payment amount (gross) |
| Cancelled before payment (expiry/admin) | `card` | 0 | 0 |
| Cancelled, then a late success (auto-refunded) | `card` | 0 | 0 |

Docs (done by Rob in this step): `09-api.md` line 69 lists `payment`; decision-log row "Order payment card (P2.52, Rob)"; two backlog notes (brand/last4; pill after a late success).

## 3. Files (all Marco)

| File | Change |
|---|---|
| `backend/src/Modules/Payments/Ecommerce.Payments/Domain/PaymentMethods.cs` | **new** `internal static class PaymentMethods { public const string Card = "card"; }` with a one-line summary (the only type the PaymentIntent allows) |
| `backend/src/Modules/Payments/Ecommerce.Payments/Infrastructure/StripePaymentGateway.cs` | `AllowedPaymentMethodTypes = [PaymentMethods.Card]` (no behaviour change) |
| `backend/src/Modules/Payments/Ecommerce.Payments.Contracts/IPaymentAttempts.cs` | `PaymentAttemptInfo` last param `string Method` |
| `backend/src/Modules/Payments/Ecommerce.Payments/Application/PaymentAttempts.cs` | `ToInfo` passes `PaymentMethods.Card` |
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderViews.cs` | new `internal sealed record OrderPaymentView(string Method, decimal AmountDue, decimal Captured);` `OrderDetailView` last param `OrderPaymentView? Payment` |
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderQueryService.cs` | in `BuildAsync`: `OrderPaymentView? payment = latest is null ? null : new OrderPaymentView(latest.Method, order.Status == OrderStatus.PendingPayment ? order.GrandTotal : 0m, summary.Captured);` pass to `OrderDetailView` |
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/OrderResponses.cs` | `internal sealed record OrderPaymentResponse(string Method, MoneyResponse AmountDue, MoneyResponse Captured);` `OrderDetailResponse` last param `OrderPaymentResponse? Payment`; mapping uses `o.Currency`. Checkout mapping untouched |
| `backend/tests/Ecommerce.Api.IntegrationTests/Checkout/WebhookTests.cs` | **append** W1–W4 |
| `backend/tests/Ecommerce.Api.IntegrationTests/Refunds/RefundTests.cs` | **append** R1–R3 |
| `frontend/apps/store/features/orders/types.ts` | `PaymentMethod = 'card'`; `OrderPayment = { method: PaymentMethod; amountDue: Money; captured: Money }`; `OrderDetail.payment: OrderPayment \| null` |
| `frontend/apps/store/features/orders/status-labels.ts` | `paymentMethodLabel(method: string): string` (`card` → `Card`, else raw) |
| `frontend/apps/store/tests/order-fixtures.ts` | default `payment: null` (only change; existing tests unaffected) |
| `frontend/apps/store/features/orders/components/OrderDetailView.tsx` | Payment panel per §2 UI row |
| `frontend/apps/store/features/orders/components/OrderDetailView.payment.test.tsx` | **new** — J1–J3 |
| `docs/admin-dashboard-feature-tracker.md` | P2.52 `Todo` → `Done` after verify passes |

Any other construction site of `PaymentAttemptInfo` / `OrderDetailView` / `OrderDetailResponse` fails the build — fix only that call, list it in the report.

## 4. Build order

1. `PaymentMethods`, gateway constant, `PaymentAttemptInfo.Method`, `ToInfo`; build green.
2. Write W1, W3, R2 → run → quote red (no `payment` property).
3. `OrderViews`, `OrderQueryService`, `OrderResponses` → W1–W4, R1–R3 green; existing T29, T19, P2_50, P2_51, refund and checkout tests green unchanged.
4. Frontend types, label, fixture; write J1–J3 → quote red; panel → green.
5. Mutations M1–M4 (§5), each: copy file to scratch → break → run named test → quote red → restore from copy → `cmp` → report.
6. Full `bash scripts/verify.sh`; tracker row → Done; report.

## 5. Required tests

Integration — `Checkout/WebhookTests.cs` (CheckoutEnv: real checkout, signed webhooks; amounts read from the same response's `grandTotal.amount`):

| # | Test | Asserts |
|---|---|---|
| W1 (test-first) | `P2_52_the_payment_card_shows_the_total_due_before_the_webhook_and_captured_only_after_it` | Placed order, owner GET → `payment.method == "card"`, `amountDue.amount == grandTotal.amount`, `captured.amount == "0.00"`, currency = order's. `PayAsync` → GET → `status == "Paid"`, `amountDue.amount == "0.00"`, `captured.amount == grandTotal.amount` |
| W2 | `P2_52_an_unsigned_or_failed_payment_event_never_shows_as_captured` | `WebhookRawAsync(valid succeeded body, bad signature)` → 400; then a signed `payment_intent.payment_failed` → 200; GET → `captured == "0.00"`, `amountDue == grandTotal`, `paymentStatus == "Failed"` |
| W3 (test-first) | `P2_52_a_late_success_after_expiry_is_not_shown_as_captured_or_due` | Advance clock 16 min, `SweepAsync` → Cancelled; signed success for the intent → 200; GET → `status == "Cancelled"`, `amountDue == "0.00"`, `captured == "0.00"` |
| W4 | `P2_52_an_order_with_no_payment_attempt_has_a_null_payment` | `OrderSeed.InsertAsync(..., "Paid", ...)` (no payments row, as in T29) → `payment` is JSON null |

Integration — `Refunds/RefundTests.cs` (RefundEnv: seeded Paid order with a Succeeded payment of `RefundEnv.Captured`):

| # | Test | Asserts |
|---|---|---|
| R1 | `P2_52_partial_and_full_refunds_keep_captured_gross_and_nothing_due` | Refund 10.00 → refund webhook Succeeded → owner GET: `captured == "25.00"`, `amountDue == "0.00"`, `paymentStatus == "PartiallyRefunded"`. Refund the remaining 15.00 → Succeeded → GET: `captured == "25.00"`, `amountDue == "0.00"`, `paymentStatus == "Refunded"` |
| R2 (test-first) | `P2_52_another_customer_gets_403_without_payment_data_and_no_token_is_401` | `TokenB` GET A's order → 403 and raw body contains neither `amountDue` nor `captured` nor `25.00`; no token → 401 |
| R3 | `P2_52_admin_order_detail_embeds_the_payment_card` | Admin GET `/api/v1/admin/orders/{id}` → `order.payment.captured.amount == "25.00"`, `order.payment.method == "card"` |

400: not applicable — `GET /orders/{id:guid}` has no body and a non-GUID id is a route miss (404), unchanged by this slice.

Jest — `OrderDetailView.payment.test.tsx`, `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' })`, setup copied from `OrderDetailView.items.test.tsx`:

| # | Test | Asserts |
|---|---|---|
| J1 (test-first) | shows method, amount due and captured | `payment: { method: 'card', amountDue: 40.00 USD, captured: 0.00 USD }` → text `Payment` (panel title), text `Card`, elements labelled `Amount due $40.00` and `Captured $0.00` |
| J2 | shows the server's amounts verbatim | `status: 'Paid'`, grandTotal 40.00, `amountDue: 0.00`, `captured: 12.34` → `Captured $12.34` and `Amount due $0.00` (the client does not derive them from totals or status) |
| J3 | hides the card when payment is null | default fixture → no `Card` text, no `Amount due` / `Captured` labels, no exact `Payment` title (the pill text `Payment: …` stays) |

Existing `OrderDetailView*.test.tsx`, admin order tests and checkout tests stay unchanged and green.

**Mutations (High tier; one per guard):**

| # | Guard | Break | Must go red |
|---|---|---|---|
| M1 | Ownership | `OrderQueryService.GetAsync`: delete the `order.UserId != userId` → Forbidden branch | R2 |
| M2 | Captured is webhook-only | `BuildAsync`: `Captured` = `order.GrandTotal` instead of `summary.Captured` | W1 (before the webhook) |
| M3 | Auto-refunded captures excluded | `PaymentStore.GetPayingAsync`: remove the `&& !db.Set<Refund>().Any(r => r.PaymentId == p.Id && r.ActorId == null)` clause | W3 |
| M4 | Nothing due once paid | `BuildAsync`: `AmountDue` = `order.GrandTotal` unconditionally | W1 (after the webhook) and R1 |

## 6. Risks

- **Late success pill:** after W3's late success, `paymentStatus` (latest attempt) reads Succeeded while the card shows captured 0. Existing behaviour, out of scope; backlog note added.
- **`GetSummaryAsync` lists refunds** even for the customer path; already called today, no new query.
- **Positional record changes** (`PaymentAttemptInfo`, `OrderDetailView`, `OrderDetailResponse`): missed sites fail the build. Check `Ecommerce.Orders.Tests` / `Ecommerce.Payments.Tests` for fakes constructing `PaymentAttemptInfo` — compile fixes only, listed as changed tests.
- **RefundEnv order total:** if `OrderSeed`'s grand total differs from `RefundEnv.Captured`, R1 still holds (captured = payment amount, due 0 because Paid); do not change the seed.
- **Admin frontend** consumes `OrderDetail`; the type widens only, admin UI unchanged — `tsc` must stay clean.
- **Mutation M3 is in Payments**; it also turns some refund-ceiling tests red — expected, run only W3 for the red quote, then restore and `cmp`.
