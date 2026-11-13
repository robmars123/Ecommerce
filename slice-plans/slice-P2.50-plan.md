# Slice P2.50 — Cancellation alert and pill note text (customer order detail)

Mode: Autonomous · Risk tier: **Low** (CLAUDE.md §5) — one additive, read-only field on `GET /orders/{id}`, derived on read. No state transition, no money, no stock, no auth change: the ownership check in `OrderQueryService.GetAsync` is untouched. Mutations: **none** (Low tier). Rob reads the diff; Marco's tests + full `verify.sh` are the gate.
Scope: tracker row P2.50 only (`docs/admin-dashboard-feature-tracker.md`). Design: `docs/design-refs/northline-order-detail.md` items 3 (`pill-note`) and 4 (`.alert`), CSS `.pills`, `.alert`. P2.51–P2.55 are out of scope. All files: **Marco**.

## 1. Read

- `docs/architecture/09-api.md` line 69 (`GET /orders/{id}` contract — Rob already added `statusNote`).
- `docs/architecture/13.2-decision-log.md` — grep `Order status note` (row added by this plan), `Customer order pages restyle`.
- `docs/architecture/05-frontend.md` §5.2, §5.5 (tests by role/label, tokens only).
- `docs/architecture/11-testing.md` §11.3.
- Backend code: `Orders/Domain/Order.cs` (`ReservationExpired`, `CancelForExpiry`, `RefundInFull`), `Domain/OrderStatusMachine.cs`, `Application/OrderViews.cs` (`TimelineRow`, `OrderDetailRow`), `Api/OrderResponses.cs` (`OrderDetailResponse`, `OrderResponseMapping.ToResponse(OrderDetailView)`).
- Backend tests: `Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` (T7 and T29 tests — the pattern), `Orders/OrderSeed.cs` (`SeedHistory` has `Reason`), `Ecommerce.Orders.Tests/Domain/OrderTests.cs` (`OrderIn` helper / how `Order.Place` is called).
- Frontend: `features/orders/components/OrderDetailView.tsx` (`Header` pills row, `Content`), `features/orders/types.ts`, `tests/order-fixtures.ts`, `tests/render-with-query.tsx`, `features/orders/components/OrderDetailView.copy.test.tsx` (setup to copy).

No migration, no new endpoint, no new package. §12.3 has no row for P2 tracker rows; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Derived server-side on read** from `status` + the reason on the timeline entry whose `toStatus` is the current status (`Cancelled`). Pure static `OrderStatusNotes.For(OrderStatus status, IReadOnlyList<TimelineRow> timeline)` in `Orders/Application` | The history row already stores the cause (`reservation-expired`, written by `CancelForExpiry`). Copy changes ship with a deploy, never a data fix | A stored note / `cancellation_reason` column: duplicates the history row, needs a migration and a backfill |
| **No migration.** No table or column touched | The field is computed from data the read already loads (`OrderDetailRow.Timeline`) | — |
| **Shape:** `statusNote: { pillNote: string, alert: { title: string, message: string } \| null } \| null`. Application records `OrderStatusNote(string PillNote, OrderAlert? Alert)` and `OrderAlert(string Title, string Message)` used directly in `OrderDetailResponse` (precedent: `ShippingAddress` is used directly) | Pill note and alert are two separate pieces of the design; `alert` null for statuses that need no red banner. No `cause` code: no consumer in this slice | Separate `*Response` twin records (pure duplication); a flat `string? note` (cannot carry both texts) |
| **Copy (exact strings — tests assert them):** see §2.1 | Correct under every path: a late webhook after expiry is auto-refunded (§8.2 rule 8), so the text never claims "you were not charged" | — |
| **Causes with no text:** "customer cancelled" and "admin cancelled" do **not** exist — `PendingPayment → Cancelled` is the only edge into `Cancelled`, written only by `CancelForExpiry`, and it is not overridable (Slice 5 D4). No copy for them; a future cancel path adds its reason constant and copy in its own slice | Minimal code: copy for a transition that cannot happen is dead code | Speculative copy keyed on reasons nobody writes |
| `Cancelled` with any reason other than `reservation-expired` (or null) → generic copy (§2.1). Not an error, not null | `For` must be total; a Cancelled order should never render without its banner. Covered by a unit test | Throwing, or returning null for an unknown reason |
| `Returned` and all other statuses → `null` | Tracker row names the cancellation alert and its pill note; refunded was requested as a cause. Returned copy is not in scope | Adding Returned copy now (scope creep → backlog if wanted) |
| **Called from `OrderResponseMapping.ToResponse(OrderDetailView)`** (`OrderStatusNotes.For(o.Status, o.Timeline)`), so `OrderQueryService`, `OrderDetailView` and the store are unchanged. Side effect: `GET /admin/orders/{id}` embeds the same `order` block and gains the field too | One mapping, the admin detail is "the customer shape" (D14). Admin UI ignores it; no admin test changes | A separate customer-only response type |
| **Ownership/auth unchanged:** the field is added after `GetAsync` returns `Ok`; 403/404/401 paths return before mapping. One new integration test proves a 403 body carries no note text | The endpoint keeps T7 behaviour; no new guard | — |
| **Client renders, never derives.** `statusNote` required in `OrderDetail` type (server always sends it, `null` when none); fixture default `null`. No client fallback copy when `status === 'Cancelled'` and `statusNote` is null | Tracker: "the API returns the text"; one source for web/iOS/Android | Client copy map keyed on status |
| **Alert a11y:** one `accessible` View with `accessibilityLabel={`${title}. ${message}`}` (one screen-reader stop), **no** `accessibilityRole="alert"` | It is a static fact on page load, not a live event; `role=alert` would also collide with the reorder failure panel (`alert`) in tests | Live-region alert |

### 2.1 Copy (`OrderStatusNotes`, private consts)

| Case | `pillNote` | `alert.title` | `alert.message` |
|---|---|---|---|
| `Cancelled`, reason `Order.ReservationExpired` | `Payment not completed in time` | `This order was cancelled` | `We held your items while you paid, but payment was not completed before the hold ran out, so we released them and cancelled the order. Nothing will ship. If a payment went through after that, we refund it automatically.` |
| `Cancelled`, any other / null reason | `Order cancelled` | `This order was cancelled` | `This order was cancelled and will not ship.` |
| `Refunded` | `Refunded in full to your original payment method` | — (`alert: null`) | — |
| any other status | `statusNote: null` | | |

Match the reason with `Order.ReservationExpired` (the constant), ordinal comparison. Pick the **last** timeline entry with `ToStatus == Cancelled` (`LastOrDefault`).

Docs (done by Rob in this step): `09-api.md` line 69 lists `statusNote`; decision-log row "Order status note (P2.50, Rob)".

## 3. Files (all Marco)

| File | Change |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderStatusNotes.cs` | **new** — `internal static class OrderStatusNotes` with `For(...)`, records `OrderStatusNote`, `OrderAlert` (internal sealed) |
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/OrderResponses.cs` | `OrderDetailResponse` gains last positional param `OrderStatusNote? StatusNote`; `ToResponse(OrderDetailView)` passes `OrderStatusNotes.For(o.Status, o.Timeline)`. Checkout response untouched |
| `backend/tests/Ecommerce.Orders.Tests/Application/OrderStatusNotesTests.cs` | **new** — U1–U4 |
| `backend/tests/Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` | **append** I1–I4 (existing tests unchanged) |
| `frontend/apps/store/features/orders/types.ts` | `export type OrderAlert = { title: string; message: string }`; `export type StatusNote = { pillNote: string; alert: OrderAlert \| null }`; `OrderDetail.statusNote: StatusNote \| null` |
| `frontend/apps/store/tests/order-fixtures.ts` | `makeOrderDetail` default `statusNote: null` (only change) |
| `frontend/apps/store/features/orders/components/OrderDetailView.tsx` | pill note in `Header` pills row; `CancellationAlert` in `Content` |
| `frontend/apps/store/features/orders/components/OrderDetailView.status-note.test.tsx` | **new** — S1–S5 |
| `docs/admin-dashboard-feature-tracker.md` | P2.50 `Todo` → `Done` after verify passes |

**UI contract** (`OrderDetailView.tsx`, tokens only, §5.5):
- Pills row (`Header`, the `border-t` row): after the payment pill, `{order.statusNote ? <Text className="self-center text-ui-sm text-muted">{order.statusNote.pillNote}</Text> : null}`.
- `function CancellationAlert({ alert }: { alert: OrderAlert })`: `<View accessible accessibilityLabel={`${alert.title}. ${alert.message}`} className="gap-xs rounded-panel border border-danger-border bg-danger-subtle px-lg py-md">` with title `<Text className="text-ui font-bold text-danger">` and message `<Text className="text-ui text-foreground">`. Decorative icon: none.
- In `Content`, between `<Header … />` and the two-column `View`: `{order.statusNote?.alert ? <CancellationAlert alert={order.statusNote.alert} /> : null}` (the column's `gap-lg` gives the mock's 16 px).
- No `console.*`. No other layout change.

## 4. Build order

1. U1–U4 first (red: `OrderStatusNotes` does not exist → compile error; quote it). Implement `OrderStatusNotes` → green.
2. I1–I4 (red: `statusNote` property missing → `KeyNotFoundException` on I1; quote it). Wire `OrderResponses.cs` → green.
3. Frontend types + fixture default; `tsc` must stay green with every existing test untouched.
4. S1–S5 red (text not rendered), then UI → green.
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; tracker row → Done; report.

## 5. Required tests

No §11.2 numbered test is named for this row. Test-first: U1–U4, I1 (marked ★).

**Unit — `OrderStatusNotesTests.cs`**
- ★U1 `Expired_cancellation_from_the_real_domain_gives_the_expiry_note_and_alert` — build an order with the real `Order.Place` (or `OrderTests`-style helper), call `CancelForExpiry(now)`, map `order.History` to `TimelineRow(FromStatus, ToStatus, CreatedAt, Reason)`, call `For(order.Status, rows)`; assert the three exact strings of §2.1 row 1. (Proves the domain's reason and the lookup agree.)
- ★U2 `[Theory]` `Cancelled_with_another_or_no_reason_gives_the_generic_note_and_alert` — reasons `null`, `"something-else"`, `"RESERVATION-EXPIRED"` (ordinal) → row 2 strings, alert not null.
- ★U3 `Refunded_gives_the_pill_note_and_no_alert`.
- ★U4 `[Theory]` `Other_statuses_have_no_note` — PendingPayment, Paid, Processing, Shipped, Delivered, Returned → `null`.

**Integration — `OrdersReadApiTests.cs`** (seed with `OrderSeed.InsertAsync(..., "Cancelled", …, history: [new SeedHistory(null, "PendingPayment", t0), new SeedHistory("PendingPayment", "Cancelled", t0.AddMinutes(16), "reservation-expired")])` — match the existing named-arg style)
- ★I1 `P2_50_an_expired_order_returns_the_cancellation_alert_and_pill_note` — owner GET → 200; `statusNote.pillNote`, `statusNote.alert.title`, `statusNote.alert.message` equal the §2.1 literals.
- I2 `P2_50_a_refunded_order_returns_a_pill_note_and_a_null_alert` — `alert` is `JsonValueKind.Null`.
- I3 `P2_50_a_paid_order_has_a_null_status_note` — `statusNote` present and `JsonValueKind.Null`.
- I4 `P2_50_another_customers_cancelled_order_is_403_without_the_note` — 403, `Assert.DoesNotContain("Payment not completed in time", r.Text)`. (401 for anonymous stays covered by the unchanged T7 test.)

**Jest — `OrderDetailView.status-note.test.tsx`**: `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' })`, `signInAs('Customer')`, fetch mocked with `makeOrderDetail({...})` (copy the setup of `OrderDetailView.copy.test.tsx`). Query by text/label, never by class.
- S1 Cancelled + note with alert → pill note text shown; `getByLabelText('<title>. <message>')` present; title and message texts present.
- S2 Refunded + `alert: null` → pill note shown; no element labelled with the title (`queryByText('This order was cancelled')` null).
- S3 default fixture (`statusNote: null`, Shipped) → neither pill note nor alert.
- S4 `status: 'Cancelled'`, `statusNote: null` → no alert, no "This order was cancelled" text (client never invents copy).
- S5 Cancelled + alert, then the Items panel and Timeline still render (alert does not replace content): `getByText('Black tee, M')` present.

Existing tests: **unchanged** (`OrderDetailView*.test.tsx`, `CheckoutView.test.tsx`, admin-orders tests, T7/T29). If any existing backend/admin test breaks from the new field, stop and report it — do not edit it.

## 6. Risks

- **Admin detail gains `order.statusNote`** (shared mapping). Harmless and intentional; verify admin integration tests stay green unchanged.
- **Reason drift:** if a future change renames `reservation-expired`, U1 (real domain) goes red — that is the guard. The integration seed uses the literal; keep it the literal so the wire value is pinned.
- **Live updates:** `useOrderLiveStatus` invalidates the detail query, so a PendingPayment → Cancelled change refetches and picks up the server note (no local patching). No change needed.
- `09-api.md` is ~11.9 KB (over the ~10 KB guideline before this slice); split is a backlog item, not this slice.
- Returned copy, and a customer/admin cancel flow, are out of scope (decision log row says why).
