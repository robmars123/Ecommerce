## 11. Testing

### 11.1 Pyramid

- **Domain (fast, many):** state machines, money rounding, pricing order, expiry math with a fake clock.
- **Application (medium):** checkout orchestration and saga steps with fake gateway, email, and clock.
- **Integration (mandatory):** WebApplicationFactory + Testcontainers Postgres — real transactions, locks, constraints, and webhook signatures with a test secret.

### 11.2 Required tests for v1

| # | Scenario | Must prove |
|---|---|---|
| 1 | 10 parallel checkouts, 1 unit in stock | Exactly 1 reservation; 9 clean failures; available never < 0 |
| 2 | Same idempotency key twice | 1 order, 1 PaymentIntent |
| 3 | Same webhook event twice | 1 transition, 1 outbox event |
| 4 | Reservation passes TTL | Stock released, order `Cancelled`, new checkout succeeds |
| 5 | Client sends a lower total | Server total wins; intent amount = server total |
| 6 | Price changes after order | Order item snapshot unchanged |
| 7 | Customer A requests customer B's order | 403 on read, cancel, and refund |
| 8 | Paid, then API crashes before webhook processing | Webhook retry reconciles to `Paid`; no duplicate order |
| 9 | Email provider down | Order stays `Paid`; retry delivers; no duplicate email |
| 10 | Admin adjust below available 0 | Rejected; audit shows actor and attempt |
| 11 | `OrderPaid` twice → dashboard projector | Revenue counted once |
| 12 | `OrderPaid` twice → search projector | Index unchanged on second delivery |
| 13 | Dashboard projector down 10 min, restarted | Catches up from checkpoint; nothing lost or double-counted |
| 14 | Version 5 arrives before version 4 | No wrong status; holds or alerts on the gap |
| 15 | Crash after email send, before mark | At most one email, or the duplicate is proven harmless — state which |
| 16 | Fulfillment step fails finally | Exactly one refund; order `Refunded` with audit; no duplicate shipment or refund on retry |
| 17 | Search read model dropped and replayed | Results match pre-drop results on a fixed dataset |
| 18 | Dashboard projector stopped | Order still reaches `Paid` with correct detail; dashboard shows as-of time |
| 19 | Webhook succeeds for a `Cancelled` order | Order stays `Cancelled`; exactly one automatic refund; alert raised |
| 20 | Real stock indicator (Slice 2) | `IInventoryReader` replaces the stub: available = on_hand − reserved; > threshold `InStock`, 1 … `Inventory:LowStockThreshold` `Low`, ≤ 0 or no stock row `Out`; a reservation moves the indicator; `GET /products` and `/products/{slug}` never return `Unknown` |
| 21 | Cart quantity bounds (Slice 3) | `POST`/`PUT` with resulting quantity < 0, > cap, or a price field → 400; > available → 409 with `available`; inactive variant → 400; `PUT` of an absent line → 404; every rejection leaves the cart unchanged; `PUT` 0 and `DELETE` remove the line; response prices come from Catalog |
| 22 | Guest merge (Slice 3) | Same variant sums then caps at min(available, cap); inactive/unknown removed; every input line appears once in `results` with outcome and reason; the same `Idempotency-Key` twice → identical response, quantities not doubled |
| 23 | Shipping and tax server-calculated (Slice 4) | With a non-zero `Tax:RatePercent`, `shipping_total`, `tax_total` (rounding at a .005 boundary) and `grand_total` match §7.4; intent amount = `grand_total`; unknown method or country, or a client shipping/tax/total field → 400 and no order; address and method name snapshotted |
| 24 | Retry payment (Slice 4) | Same order, attempt n+1 with a new intent and the same amount; only from `PendingPayment`; reservation row and `expires_at` unchanged; after `expires_at` (fake clock, worker not yet run) → 409 and no new payment; same key twice → one attempt, same `client_secret`; previous intent gets a cancel request |
| 25 | Old attempt succeeds after the order is `Paid` (Slice 4) | Order stays `Paid` with one inventory commit; exactly one automatic refund of the extra payment; alert and audit raised |
| 26 | Refund ceiling (Slice 6) | Concurrent refunds whose sum exceeds the captured amount: total `Pending + Succeeded` never > captured; the losers get 409 and an audit row; a `Failed` refund frees its amount |
| 27 | Refund idempotency (Slice 6) | Same `Idempotency-Key` twice (double submit), a `RefundRequested` consumer retry, and a duplicate refund webhook → one refund row, one Stripe refund (`re:{refundId}`), one transition, one audit row |
| 28 | Refund states (Slice 6) | Partial → payment `PartiallyRefunded`, order status unchanged; refunds reaching the captured amount → payment `Refunded`, order `Refunded` from `Paid`/`Processing`; full refund on `Shipped` → 409; `ship` while a full refund is `Pending` → 409; order totals unchanged; audit has actor, before, after, reason |
| 29 | Order history, detail, timeline, tracking (Slice 5) | `GET /orders` returns only the caller's orders, newest first, paginated; detail has snapshots, address, method, payment status, a timeline row per transition in order, and the shipment with carrier, tracking number and `trackingUrl` |
| 30 | Illegal transitions (Slice 5) | Every transition not in §7.3 is rejected in the domain and → 409 at the API, with no history row; override accepts only a legal transition with a reason and audits it |

**Test-first:** 1, 2, 3, 4, 11, 13, 16, 19, 23, 24, 25, 26, 27. If you can't write the test, you don't understand the invariant yet.

### 11.3 Frontend tests

Runner: Jest (`jest-expo` preset) + React Native Testing Library + `expo-router/testing-library` (§2). Tests run in Node against the RN component tree; they prove UI contracts, never money or stock — those are proven server-side by §11.2.

**What must be tested (every slice that touches the frontend):**

- **Pure logic** in `lib/` and `features/*/` (`api.ts`, `store.ts`, `types.ts` helpers, hooks): every exported function or hook has a co-located test.
- **Components with behavior** (props that change output, state, handlers, conditional rendering, navigation): a co-located test of the behavior and its accessible role/label. Purely static markup needs no test.
- **Routes:** every route in app context §5 renders under the real root layout at its URL (`tests/routes.test.tsx`). Screen-level behavior belongs in `features/` and is tested there; screens stay thin.
- **Network:** mock `fetch` with `jest.fn()` / `jest.spyOn(globalThis, 'fetch')`. No MSW or other mocking package in v1.
- **Platform splits:** when `lib/platform.native.ts` / `lib/platform.web.ts` land, the Jest config switches to per-platform projects (`jest-expo/ios`, `jest-expo/android`, `jest-expo/web`) in the same slice, so each split is tested on its platform.
- **Not in v1:** device E2E (Detox/Maestro) and browser E2E (Playwright). EAS release candidates are smoke-tested by hand on Expo Go and the web export.

| # | Slice | Scenario | Must prove |
|---|---|---|---|
| F1 | 0 (retro) | Route table | Every app context §5 route renders its screen at its URL under `RootLayout`; an unknown URL renders Not found |
| F2 | 1 | `lib/api-client.ts` | Sends the correlation ID and bearer token; maps ProblemDetails to a plain-language error with a retry action; never logs the token |
| F3 | 3 | Cart re-validation / merge | Server result wins: changed price or stock is shown to the user, the old value is never kept; merge result is shown, never dropped silently |
| F4 | 4 | Checkout idempotency key | One UUID per checkout attempt; double-submit and network retry reuse the same `Idempotency-Key`; a new attempt gets a new key |
| F5 | 4 | Checkout submit | No optimistic order: submit is disabled while pending; the success screen shows `PendingPayment` until the server reports `Paid` |
| F6 | 4 | Checkout request and total | The request body carries no price, total, or status field; the displayed total is the server's response value |
| F7 | 1 | Role-gated admin UI | Admin routes are hidden and redirect for a Customer (UI only — API test 7 is the real authorization) |
| F8 | 6 | Return request | No optimistic submit; server error shows a plain-language message with retry |
| F9 | 3 | Guest device cart | Lines persist across an app restart (storage mocked); edit quantity sets the line; 0 or remove deletes it; cleared only after a successful merge, kept when merge fails; merge reuses one `Idempotency-Key` on retry |
| F10 | 4 | Address and shipping step | Methods come from `GET /shipping-methods`; address errors are shown per field; the total is labelled an estimate until the checkout response replaces it |
| F11 | 4 | Payment confirm and retry | `clientSecret` goes to the `lib/platform` payment call (mocked per platform); a decline offers retry on the same order with one key per retry; `reservation-expired` sends the customer to check out again |
| F12 | 5 | Orders screens | `/orders` lists number, date, total, status, tracking link; `/orders/[id]` shows items, address, method, payment status, timeline in order, tracking link; errors show retry |
| F13 | 6 | Admin refund | One `Idempotency-Key` per refund attempt, reused on retry; amount sent as a decimal string; no optimistic status — shows the server's `Pending` and remaining amount |

**Frontend test-first:** F4, F5, F6 (they are the client side of hard rules 1, 2, and 5), and F13 (rule 2 for refunds).

**F2 split (Slice 1a/1b):** 1a proves the correlation ID, ProblemDetails → plain-language error mapping, and retry; 1b adds the bearer token and the "never logs the token" assertion, when tokens exist.

### 11.4 Enforcement in CI

- **Backend:** `dotnet test` fails when any test project discovers zero tests (`TreatNoTestsAsError` in `backend/tests/test.runsettings`). Coverage is collected (`Code Coverage`, cobertura) and uploaded as an artifact with TRX results — **no percentage gate** (quality bar §5).
- **Frontend:** `npm test -- --ci --coverage` (Jest fails on zero tests by default; `--passWithNoTests` is banned). Coverage is uploaded as an artifact — no percentage gate.
- **Hygiene step (frontend job):** fails on any test file under `app/` and on any focused test (`it.only`, `test.only`, `describe.only`, `fit`, `fdescribe`), because a focused test silently drops every other test.
- **Not gated:** lint and formatting (style never blocks, quality bar §3); skipped tests are a review check (quality bar §4.2). **One exception (owner decision, §13.2):** C# `var` is a build error (`.editorconfig` IDE0008 + `EnforceCodeStyleInBuild`), so `dotnet build` enforces explicit types.


