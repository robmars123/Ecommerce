# Slice P2.52 — Marco report

Concerns: None. (W3 first ran red for a test bug, not the feature: the access token expires after the +16 min clock jump, so GET was 401. Fixed by minting a fresh token as RetryAndExpiryTests does. Brand/last4 stay out per plan.)
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None. Existing tests untouched. Only `tests/order-fixtures.ts` got a default `payment: null` (plan-named).

Red before implementation (integration, `--filter P2_52`): 6 failed / 1 passed. W1, W2, W4, R1, R3 failed with `KeyNotFoundException` (no `payment` property); R2 passed pre-implementation (403 already held; it is guarded by M1). After: 7 passed.
Jest red before the panel: J1, J2 failed (`getByLabelText('Captured $12.34')` not found); J3 passed (hidden by default). After: green.

Mutations (break -> red -> restore from scratch copy -> cmp identical):
- M1 ownership check disabled in `OrderQueryService.GetAsync`: R2 `P2_52_another_customer_gets_403...` FAILED. restored, cmp OK.
- M2 `Captured = order.GrandTotal`: W1 FAILED. restored, cmp OK.
- M4 `AmountDue = order.GrandTotal` always: W1 FAILED and R1 `P2_52_partial_and_full...` FAILED. restored, cmp OK.
- M3 auto-refund exclusion removed in `PaymentStore.GetPayingAsync`: W3 FAILED. restored, cmp OK.

Changed:
- backend/src/Modules/Payments/Ecommerce.Payments/Domain/PaymentMethods.cs (new)
- .../Payments/Infrastructure/StripePaymentGateway.cs (uses `Domain.PaymentMethods.Card`, qualified because `Domain.Refund` clashes with `Stripe.Refund`)
- .../Payments.Contracts/IPaymentAttempts.cs (`Method`), .../Payments/Application/PaymentAttempts.cs (`ToInfo`)
- .../Orders/Application/OrderViews.cs, OrderQueryService.cs (added `using Ecommerce.Orders.Domain`), Api/OrderResponses.cs
- backend/tests/Ecommerce.Api.IntegrationTests/Checkout/WebhookTests.cs (W1–W4 appended), Refunds/RefundTests.cs (R1–R3 appended)
- frontend/apps/store: features/orders/types.ts, status-labels.ts, components/OrderDetailView.tsx (Payment panel, prettier applied), components/OrderDetailView.payment.test.tsx (new, J1–J3), tests/order-fixtures.ts
- docs/admin-dashboard-feature-tracker.md (P2.52 Done); AGENT_STATUS.md
No other construction sites of the positional records. No migration, package, endpoint or Stripe call.

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export) · build 0 warnings · jest 264 suites / 1995 tests

Quality-bar §2: all boxes met. Money computed server-side; captured comes from the webhook-confirmed payment only; no external call in the read path; no cross-module join (Orders uses Payments.Contracts); thin controller untouched; no `var`; tests cover happy, 401/403 and admin embed (400 N/A: GET without body, per plan); no secrets; no git writes.
