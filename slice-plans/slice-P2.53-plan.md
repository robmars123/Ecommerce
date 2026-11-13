# Slice P2.53 — Timeline entry description text (customer order detail)

Mode: Autonomous · Risk tier: **Low** (CLAUDE.md §5) — one additive, read-only field on each `timeline` entry of `GET /orders/{id}`, derived on read from server constants. No money, stock, payment, state transition or auth change; the ownership check in `OrderQueryService.GetAsync` is untouched. It does **not** expose internal data: the text is built only from constants, never from the stored `reason` (proved by U3 and I3). Mutations: **none** (Low tier). Rob reads the diff; Marco's tests + full `verify.sh` are the gate.
Scope: tracker row P2.53 only (`docs/admin-dashboard-feature-tracker.md`). Design: `docs/design-refs/northline-order-detail.md` item "Timeline card" (line 23) and its `.tl-*` CSS. Slice 6 of 6 (P2.48–P2.53). All files: **Marco**.

## 1. Read

- `docs/architecture/09-api.md` line 69 (`GET /orders/{id}` — Rob already added `description` to the timeline entry).
- `docs/architecture/13.2-decision-log.md` — grep `Timeline entry description` (added by this plan) and `Order status note`.
- `docs/architecture/05-frontend.md` §5.2, §5.5 (tests by role/label/text, tokens only).
- Backend: `Orders/Application/OrderStatusNotes.cs` (the P2.50 pattern you extend), `Application/OrderViews.cs` (`TimelineRow`), `Domain/Order.cs` (`ReservationExpired`, `CancelForExpiry`, `RefundInFull`, `ReturnInFull`, `Override`), `Api/OrderResponses.cs` (`TimelineEntryResponse`, `ToResponse(OrderDetailView)`).
- Backend tests: `Ecommerce.Orders.Tests/Application/OrderStatusNotesTests.cs` (you append here), `Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` (the P2_50 tests and `ExpiredHistory` helper, lines ~190–260), `Orders/OrderSeed.cs` (`SeedHistory(From, To, At, Reason, ChangedBy)`, default history = one row).
- Frontend: `features/orders/components/OrderTimeline.tsx`, `features/orders/types.ts` (`TimelineEntry`), `tests/order-fixtures.ts`, `features/orders/components/OrderDetailView.status-note.test.tsx` (setup to copy), `features/orders/components/OrderDetailView.test.tsx` lines 91–130 (O9 literal).

No migration, no new endpoint, no new package. §12.3 has no row for P2 tracker rows; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Derived server-side on read**, not stored. `OrderStatusNotes.Describe(OrderStatus to, string? reason) : string` | The target status (plus the expiry reason code) fully determines the sentence; copy ships with a deploy, never a data fix | A `description` column on `order_status_history`: migration + backfill for text the status already determines |
| **No migration.** No table or column touched | Computed from `OrderDetailRow.Timeline`, which the read already loads | — |
| **Keyed on `ToStatus` (+ reason only for `Cancelled`)**; `FromStatus` not used | Every target status has one customer meaning: `Refunded` from `Paid` or from `Processing` reads the same. No consumer for a `(from, to)` key | `(from, to)` table: more copy, same text |
| **Reuse P2.50, don't duplicate:** `Describe` lives in `OrderStatusNotes` next to `For`. Extract one private `IsReservationExpiry(string? reason)` (ordinal compare with `Order.ReservationExpired`) and use it from both `For` and `Describe`. The generic cancel sentence is one private const shared by the P2.50 alert message and the description | One file owns all customer-facing order copy and one owns the expiry check; P2.50's behaviour stays identical (its U1–U4 and I1–I4 stay green, unchanged) | A new `TimelineDescriptions` class repeating the reason check and the cancel copy |
| **No leak:** `Describe` returns only constants; the `reason` argument is used only as an equality key against `Order.ReservationExpired`. An admin override (`Paid→Processing`, `Shipped→Delivered`) gets the same sentence as the normal edge and never reveals it was an override. Return `admin_note` and refund reasons are not in the timeline and not read | Override reasons are admin free text (1–500 chars) | Echoing or summarising `reason` in the text |
| **Total:** switch expression over `OrderStatus`, all 8 values; the discard arm throws `ArgumentOutOfRangeException` (only reachable for an undefined enum value; required to avoid CS8524). U1 runs every `Enum.GetValues<OrderStatus>()` so a new status goes red until it has a sentence | Every timeline row must have text; the client has no fallback | Nullable description (client would need fallback copy) |
| **Shape:** `TimelineEntryResponse` gains last positional param `string Description`; wire `timeline: [{ fromStatus, toStatus, at, reason, description }]`. Admin detail embeds the same block and gains it too (harmless; admin UI ignores it) | One mapping (D14, as P2.50) | Customer-only response type |
| **Ownership/auth unchanged:** mapping runs after `GetAsync` returns `Ok`; 401/403/404 return before. I4 proves a 403 body carries no description | — | — |
| **Client renders, never derives.** `TimelineEntry.description: string` (required); no client copy map | Tracker: text from the API; one source for web/iOS/Android | Client copy keyed on status |
| **Existing `reason` field and red tag unchanged** (contract in 09-api line 69, test O9). The fact that it shows admin override free text is pre-existing; logged in `slice-plans/backlog.md` (P2.53 plan) for an owner decision | Fixing it splits the customer/admin timeline mapping and changes O9 — not this tracker row | Narrowing `reason` in this slice (scope creep, changes an existing test) |

### 2.1 Copy (`OrderStatusNotes`, private consts — tests assert these exact strings)

| `toStatus` (reason) | `description` |
|---|---|
| `PendingPayment` | `We received your order and held your items for payment.` |
| `Paid` | `Your payment was confirmed.` |
| `Processing` | `We started preparing your order.` |
| `Shipped` | `Your order was handed to the carrier.` |
| `Delivered` | `Your order was delivered.` |
| `Cancelled`, reason `Order.ReservationExpired` (ordinal) | `Payment was not completed before the hold on your items ran out, so we released them and cancelled the order.` |
| `Cancelled`, any other / null reason | `This order was cancelled and will not ship.` (the same const as P2.50's generic alert message — its value does not change) |
| `Refunded` | `We refunded the full amount to your original payment method.` |
| `Returned` | `Your return was refunded in full to your original payment method.` |

Past tense: each entry describes the moment it happened, so an old `PendingPayment` row still reads correctly after payment. No timing promises (refund arrival days), no "you were not charged" (a late payment after expiry is auto-refunded, §8.2 rule 8). The words "Pending" and "requested" are avoided on purpose (existing Jest regexes `/Pending/`, `/requested/`, `/Return requested/`).

Docs (done by Rob in this step): `09-api.md` line 69 adds `description`; decision-log row "Timeline entry description (P2.53, Rob)"; backlog note on the existing `reason` exposure.

## 3. Files (all Marco)

| File | Change |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderStatusNotes.cs` | add `public static string Describe(OrderStatus to, string? reason)`, the 2.1 consts, private `IsReservationExpiry`; `For` uses `IsReservationExpiry`; the generic alert message uses the shared const. Update the class summary to name P2.53 |
| `backend/src/Modules/Orders/Ecommerce.Orders/Api/OrderResponses.cs` | `TimelineEntryResponse(..., string? Reason, string Description)`; mapping passes `OrderStatusNotes.Describe(t.ToStatus, t.Reason)`. Nothing else |
| `backend/tests/Ecommerce.Orders.Tests/Application/OrderStatusNotesTests.cs` | **append** U1–U4 (existing tests unchanged) |
| `backend/tests/Ecommerce.Api.IntegrationTests/Orders/OrdersReadApiTests.cs` | **append** I1–I4 (existing tests unchanged; reuse `ExpiredHistory`) |
| `frontend/apps/store/features/orders/types.ts` | `TimelineEntry.description: string` |
| `frontend/apps/store/tests/order-fixtures.ts` | the 4 default timeline entries gain `description` with the 2.1 literals (data only) |
| `frontend/apps/store/features/orders/components/OrderDetailView.test.tsx` | **data only, for `tsc`:** the two O9 timeline literals (lines ~96–97) gain `description` (`'We received your order and held your items for payment.'`, `'This order was cancelled and will not ship.'`). No assertion, query or other line changes — list it in the report as a changed test |
| `frontend/apps/store/features/orders/components/OrderTimeline.tsx` | render the description |
| `frontend/apps/store/features/orders/components/OrderDetailView.timeline.test.tsx` | **new** — J1–J3 |
| `docs/admin-dashboard-feature-tracker.md` | P2.53 `Todo` → `Done` after verify passes |

**UI contract** (`OrderTimeline.tsx`, tokens only, §5.5): in the right-hand column, directly after the title/date row and before the reason tag, `<Text className="text-ui-sm text-muted">{entry.description}</Text>`. Always rendered (field is required). No other layout, a11y or class change; no `console.*`. Update the component doc comment by one clause if needed.

## 4. Build order

1. U1–U4 first (red: `Describe` does not exist → compile error; quote it). Implement in `OrderStatusNotes` → green; P2.50 tests in the same file stay green unchanged.
2. I1–I4 (red: `description` property missing → `KeyNotFoundException` in I1; quote it). Wire `OrderResponses.cs` → green. Run the whole `OrdersReadApiTests` class and the admin order integration tests: all green, none edited.
3. Frontend type + fixture + O9 data; `npx tsc` green with no other test touched.
4. J1–J3 red (description text not found), then `OrderTimeline` → green.
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; tracker row → Done; report ≤ 6 KB.

## 5. Required tests

No §11.2 numbered test is named for this row. Test-first: U1–U4, I1, I3 (★).

**Unit — append to `OrderStatusNotesTests.cs`**
- ★U1 `[Theory, MemberData]` `Every_status_has_a_non_empty_description` — over `Enum.GetValues<OrderStatus>()`, `Describe(s, null)` is not null/whitespace.
- ★U2 `[Theory]` `Each_status_has_its_exact_description` — `InlineData` per row of 2.1 (status, reason, expected), including `Cancelled` with `"reservation-expired"`, `null`, `"something-else"` and `"RESERVATION-EXPIRED"` (ordinal → generic).
- ★U3 `[Theory, MemberData]` `A_reason_is_never_echoed_into_the_description` — for every status, `Describe(s, "Internal: VIP, skip fraud check")` does not contain `"VIP"` and equals `Describe(s, null)`.
- ★U4 `Expired_cancellation_from_the_real_domain_is_described_as_expiry` — `Order.Place` as in the existing U1 helper, `CancelForExpiry`, take the last `History` row, `Describe(row.ToStatus, row.Reason)` equals the expiry literal. Also `RefundInFull` path: an order moved `MarkPaid` → `RefundInFull` → last row described with the `Refunded` literal (its stored reason "Refunded in full" is not echoed).

**Integration — append to `OrdersReadApiTests.cs`** (named-arg seed style as P2_50)
- ★I1 `P2_53_each_timeline_entry_has_its_description_in_order` — owner GET on a `Shipped` order seeded with history `null→PendingPayment`, `PendingPayment→Paid`, `Paid→Processing`, `Processing→Shipped`; `timeline[*].description` equals the four 2.1 literals in that order.
- I2 `P2_53_an_expired_cancellation_entry_is_described_as_expiry` — `ExpiredHistory`; the `Cancelled` entry's description equals the expiry literal.
- ★I3 `P2_53_an_admin_override_reason_never_appears_in_a_description` — `Processing` order, history `null→PendingPayment`, `PendingPayment→Paid`, `Paid→Processing` with `Reason: "Internal: VIP, skip fraud check"`, `ChangedBy: <some Guid>`; the `Processing` entry's description equals `We started preparing your order.` and no entry's `description` contains `VIP`. (Do not assert on `reason` — its exposure is the backlog item.)
- I4 `P2_53_another_customers_order_is_403_without_descriptions` — 403; `Assert.DoesNotContain("Your payment was confirmed.", r.Text)` with B's order seeded with a Paid history. 401 stays covered by the unchanged T7 test; no new input, so the existing 400/404 paths are unchanged.

**Jest — `OrderDetailView.timeline.test.tsx`** (new screen test): `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen }, { initialUrl: '/orders/o-1' })`, `signInAs('Customer')`, fetch mocked with `makeOrderDetail({...})` — copy the setup of `OrderDetailView.status-note.test.tsx`. Query by text, never by class.
- J1 default fixture → each of the four fixture descriptions is shown.
- J2 a `Cancelled` entry with `reason: 'reservation-expired'` and the expiry description → both the description and the `reservation-expired` tag are shown.
- J3 the client shows the server's text verbatim: an entry with `description: 'Text only the server knows.'` renders exactly that (no client copy map).

Existing tests: **unchanged** except the O9 data-only addition above. If any other backend, admin or Jest test breaks from the new field or text, stop and report it — do not edit it.

## 6. Risks

- **Pre-existing leak, not fixed here:** `reason` on the customer timeline includes admin override free text and renders as a red tag. Backlog note added; owner decides. P2.53 must not widen it (U3, I3).
- **Admin detail gains `timeline[].description`** via the shared mapping. Intentional; admin integration and Jest tests stay green unchanged.
- **Reason drift:** renaming `reservation-expired` turns U4 red (real domain) — the guard. The integration seed keeps the literal so the wire value stays pinned.
- **Text collisions in Jest:** the copy avoids the words used by existing regex queries; if a collision appears anyway, report it rather than edit the old test.
- `09-api.md` is ~12.3 KB (over the ~10 KB guideline before this slice); the split stays a backlog item.
