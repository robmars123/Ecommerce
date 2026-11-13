# Slice P2.52 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings · dotnet 1283 passed (IntegrationTests 547) · jest 264 suites / 1995 tests

The diff matches the plan: no migration, no endpoint, no package, no Stripe call. The whole backend change is one expression in `OrderQueryService.BuildAsync`, a constant, and a positional field. Each guard has a test that fails when I break it.

### Checks I ran myself

- **`captured` is webhook-only, and auto-refunded captures are out.** It comes from `RefundSummary.Captured`, which comes from `PaymentStore.GetPayingAsync`. That reads only Succeeded, PartiallyRefunded or Refunded payments that have no actor-less refund. Only `PaymentWebhookService` moves a payment to Succeeded. It creates the automatic refund in the same apply (`PaymentWebhookService.cs:140`), so a late capture is never counted, not even briefly. W2 shows that a bad signature (400) and a failed event leave captured at 0.00.
- **`amountDue` is computed on the server.** It is `GrandTotal` while the order is `PendingPayment`, else 0 (`OrderQueryService.cs:86-87`). The client only formats it. J2 feeds the client amounts that disagree with the totals and status, and it shows them unchanged.
- **No external call.** `BuildAsync` reuses the `GetLatestAsync` and `GetSummaryAsync` calls it already made. It adds no new query and calls no gateway. The gateway change is the same literal, now taken from `PaymentMethods.Card`.
- **No card data is exposed.** The only method value is the literal `"card"`. There is no brand, last4, intent id or client secret on the wire.
- **Ownership runs before any payment read.** `GetAsync` returns Forbidden at line 55, before it calls `BuildAsync`. R2 checks that the 403 body holds no `amountDue`, `captured` or `25.00`, and that a request with no token gets 401.
- **Boundaries.** Orders reaches Payments only through `Payments.Contracts` (`IPaymentAttempts`, `IRefundState`). `PaymentMethods` is `internal`, and the new records are `internal sealed`. The 25 ArchitectureTests are green.
- **W3's token fix is sound.** The +16 min clock jump expires `TokenA`'s JWT, which was the real cause of the first 401. Minting a fresh token for the same user `a@example.test` is the same approach `RetryAndExpiryTests` uses (lines 28, 102, …). The order still belongs to A, so ownership is still checked, and `Str("status") == "Cancelled"` would throw on a problem body. It does not hide a failure.
- **The `payment: null` fixture default** is the right choice. Existing tests keep rendering exactly what they rendered before (no card), and J3 pins that the card is hidden. A non-null default would have changed text in every other OrderDetailView suite.
- **Changed tests:** none. W1–W4 and R1–R3 are appended, J1–J3 are in a new file, and the fixture gains one field.

### Mutations (each one: scratch copy → break → run → restore → `cmp` OK)

| # | Break | Result |
|---|---|---|
| M1 | `if (false && order.UserId != userId)` | R2 failed (`Assert.Equal() Failure`); restored, `cmp` OK |
| M2 | `Captured = order.GrandTotal` | W1 failed (Expected `"0.00"`, Actual `"25.00"`); restored, `cmp` OK |
| M3 | dropped the actor-less refund clause in `GetPayingAsync` | W3 failed (Expected `"0.00"`, Actual `"25.00"`); restored, `cmp` OK |
| M4 | `AmountDue = order.GrandTotal` unconditionally | W1 and R1 failed (Expected `"0.00"`, Actual `"25.00"`); restored, `cmp` OK |

### 1. The pill contradicts the card after a late success — NOTE
**What I saw:** after a late success on an expired order, the pill shows `Payment: Paid` (`paymentStatusLabel('Succeeded')`, `OrderDetailView.tsx:206`), and the new card next to it shows Captured $0.00. The plan's backlog note says "Payment: Succeeded". The label the user actually sees is "Paid".
**Why it matters:** this is not a money bug. The API is right and the money was auto-refunded. But the two widgets now sit side by side and disagree, so a customer may contact support. The behaviour existed before this slice and is out of scope.
**What I want:** nothing in this slice. I corrected the wording of the backlog note to "Payment: Paid".
**Lesson:** when you add a second view of the same fact, check that every existing view of it still agrees.

**What was good:** you put `amountDue`/`captured` in one ternary that reuses the two contract calls `BuildAsync` already makes, and your W3 found the expired-token trap and fixed it with the existing fresh-token pattern instead of loosening the assertion.
