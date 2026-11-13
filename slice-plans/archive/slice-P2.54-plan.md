# Slice P2.54 — Totals note under the Total (customer order detail)

Mode: Autonomous · Risk tier: **Low** (CLAUDE.md §5) — static display copy on a read-only screen. No API, money computation, stock, payment, state transition or auth change. Mutations: **none** (Low tier). Rob reads the diff; Marco's tests + full `verify.sh` are the gate.
Scope: tracker row P2.54 only (`docs/admin-dashboard-feature-tracker.md`). Design: `docs/design-refs/northline-order-detail.md` item 25 "Totals card … The note under it is [feature]" and the list entry "Totals note ("never charged …")". Slice 1 of 2 (P2.54, P2.55); P2.55 (top bar) is **out of scope**. All files: **Marco**.

## 1. Read

- `docs/architecture/13.2-decision-log.md` — grep `Order totals note` (added by this plan), `Order status note`, `Order payment card`.
- `docs/architecture/08-workflows.md` §8.2 rules 7, 8, 12 (grep `Late success`) — why the copy says what it says.
- `docs/architecture/05-frontend.md` §5.2, §5.5 (tests by role/label/text, tokens only).
- Frontend: `features/orders/status-labels.ts` + `status-labels.test.ts` (you append), `features/orders/components/OrderDetailView.tsx` (`TotalRow`, the Totals `Panel`, ~line 275), `features/orders/types.ts` (`OrderStatus`), `features/orders/components/OrderDetailView.payment.test.tsx` (setup to copy), `tests/order-fixtures.ts` (`makeOrderDetail`).

No backend change, no migration, no new endpoint, no new package. §12.3 has no row for P2 tracker rows; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Static client copy**, from a pure `totalsNote(status: OrderStatus): string \| null` in `status-labels.ts` | The row's module is `orders` (frontend) only, and the sentence depends on `status` alone — no server state is needed to make it true | A server `totalsNote` field (an API + backend change the row does not name; P2.50's server copy was chosen because that row said "the API returns the text") |
| **Shown only for `Cancelled`**; `null` for the other 7 statuses | `Cancelled` is reachable only from `PendingPayment` by expiry (`OrderStatusMachine`: one edge, not overridable; 13.2 "Order status note"), so it always means "the order was never paid for". `Refunded`/`Returned` are already explained by the P2.50 pill note and the Refunds panel; `Paid`→`Delivered` were charged | A note on `PendingPayment` ("not charged yet"): the card capture can land at Stripe before our webhook arrives, so the claim is not guaranteed (§3.4: the webhook is the only proof) |
| **Wording does not say "never charged"** (see 2.1) | A `payment_intent.succeeded` can arrive after expiry: it is captured, recorded and auto-refunded (§8.2 rules 8 and 12). "Never charged" would then be false. What the system guarantees is that no payment is *kept* | The reference's "never charged …" text verbatim |
| **Consistent with P2.50 / P2.52**: same promise as the P2.50 expiry alert ("we refund it automatically"), no timing promise (no "within N days"), no figure (the P2.52 card shows `Captured`, which already excludes auto-refunded late payments, so it stays at 0 and agrees with the note) | One story across alert, totals and payment card | Repeating amounts or referencing the payment card in the note |
| **Independent of `statusNote` and `payment`**: rendered whenever `status === 'Cancelled'`, even if `statusNote` or `payment` is null | No coupling to server fields that may be absent; simplest rule | Gating on `statusNote.alert` (couples two unrelated rows) |
| **Placement**: inside the Totals `Panel`, directly after the `Total` row | Design: "the note under it" | A separate panel |
| **No change to totals, `TotalRow`, labels or a11y labels** of the existing rows | Existing tests (`Total $…` labels) stay unchanged | — |

Docs (done by Rob in this step): decision-log row "Order totals note (P2.54, Rob)". No other doc change (09-api unchanged: no wire field).

### 2.1 Copy (one exported-by-function private const in `status-labels.ts`; tests assert the exact string)

`No payment is kept for this order. It was cancelled before payment was confirmed, and any payment that went through late is refunded automatically.`

Avoids the words "Pending", "requested", "Refunded" (capital), "Captured", "Amount due" and "Total" so it cannot collide with existing text/regex queries on the detail screen.

## 3. Files (all Marco)

| File | Change |
|---|---|
| `frontend/apps/store/features/orders/status-labels.ts` | add `const CANCELLED_TOTALS_NOTE = '…2.1…'` and `export function totalsNote(status: OrderStatus): string \| null` returning it only for `'Cancelled'`. One-line doc comment citing 13.2 "Order totals note" |
| `frontend/apps/store/features/orders/status-labels.test.ts` | **append** a `describe('totalsNote')` block (U1, U2); existing blocks unchanged |
| `frontend/apps/store/features/orders/components/OrderDetailView.tsx` | import `totalsNote`; in the Totals `Panel`, after the `Total` `TotalRow`: compute `const note = totalsNote(order.status)` in `Content` (typed, no inline anonymous objects) and render `{note ? <Text className="text-ui-sm text-muted">{note}</Text> : null}`. Nothing else |
| `frontend/apps/store/features/orders/components/OrderDetailView.totals-note.test.tsx` | **new** — J1–J3 |
| `docs/admin-dashboard-feature-tracker.md` | P2.54 `Todo` → `Done` after verify passes (P2.55 untouched) |

**UI contract** (tokens only, §5.5): plain `Text`, `text-ui-sm text-muted` (same tokens as the P2.53 timeline description); no new token, no `accessible` wrapper (screen readers read the text in order after the Total row), no `console.*`. Do not touch `TotalRow`.

## 4. Build order

1. U1–U2 red (`totalsNote` is not exported → TS/Jest failure; quote it). Implement → green.
2. J1–J3 red (note text not found in J1; quote it). Render in `OrderDetailView` → green.
3. Run the whole `features/orders` Jest folder: every existing test green **unchanged**.
4. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; tracker row → Done; report ≤ 6 KB.

## 5. Required tests

No §11.2 numbered test is named for this row. Test-first: U1, J1 (★).

**Unit — append to `status-labels.test.ts`**
- ★U1 `it.each` over all 8 `OrderStatus` values (list them literally, typed `OrderStatus[]`): `totalsNote('Cancelled')` equals the 2.1 literal; every other status returns `null`.
- U2 the Cancelled text contains neither `never charged` nor `not charged` (case-insensitive) — pins the truthfulness decision.

**Jest screen — `OrderDetailView.totals-note.test.tsx`** (new): copy the setup of `OrderDetailView.payment.test.tsx` — `signInAs('Customer')`, fetch mocked with `jsonResponse(200, makeOrderDetail(overrides))`, `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' })`, await the `header` `Order ORD-2026-000001`. Query by text/label, never by class.
- ★J1 `status: 'Cancelled'` → the 2.1 text is on screen, and the `Total $…` labelled row is still there (by label, value from the fixture's `grandTotal`).
- J2 `it.each` over `PendingPayment`, `Paid`, `Processing`, `Shipped`, `Delivered`, `Refunded`, `Returned` → `queryByText(/No payment is kept/)` is null.
- J3 `status: 'Cancelled'` with `statusNote: null` and `payment: null` → the note is still shown (independent of server fields); and with the P2.52 card present (`payment: { method: 'card', amountDue: 0.00, captured: 0.00 }`) both the note and `Captured $0.00` are shown.

Existing tests: **unchanged**. If any existing Jest test breaks because of the new text (e.g. a broad regex on a Cancelled fixture), stop and report it — do not edit it.

## 6. Risks

- **Future cancel-after-payment path** would make the sentence false. Mitigated: the decision-log row says that slice must revisit it; U1 pins Cancelled as the only status with the note.
- **Late refund failure:** if the automatic refund fails, ops are alerted (§8.5); the note's "refunded automatically" is the same promise P2.50 already makes — no new commitment.
- **Text collision in Jest:** copy avoids the words used by existing queries; report rather than edit if one appears.
- `13.2-decision-log.md` is ~73 KB, far over the ~10 KB guideline (pre-existing); the split stays a backlog item.
