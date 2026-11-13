# Slice P2.49 — Reorder: put the order's items back in the cart (customer order detail)

Mode: Autonomous · Risk tier: **High** (CLAUDE.md §5 — cart write, idempotency, relies on the order-ownership rule) · Mutations: **4** (§7).
Scope: tracker row P2.49 only (`docs/admin-dashboard-feature-tracker.md`). Storefront `/orders/[id]`, the "Reorder item" **[feature]** in `docs/design-refs/northline-order-detail.md` (header actions, line 16; `.head-actions` CSS lines 65 and 104). P2.50–P2.53 are out of scope. All files: **Marco**.

## 1. Read

- `docs/architecture/03-principles-hard-rules.md` §3.4 (server computes money; idempotency).
- `docs/architecture/08-workflows.md` §8.1 — the "Server cart writes" and "Merge on login" bullets only (lines 10–13).
- `docs/architecture/09-api.md` — grep `cart/merge` (route row and the contract line).
- `docs/architecture/05-frontend.md` §5.2, §5.5 (tests by role/label, tokens, touch targets).
- `docs/architecture/11-testing.md` §11.3 (frontend tests; jest projects ios/android/web).
- `docs/architecture/13.2-decision-log.md` — grep `Guest merge on the client`, `Cart edit quantity and merge idempotency`, `Reorder` (row added by this plan).
- Code: `features/orders/components/OrderDetailView.tsx` (`Header`), `features/cart/api.ts` (`mergeCart`), `features/cart/cart-messages.ts` (`mergeResultText`), `features/cart/hooks/useGuestCartMerge.ts` (the key/retry pattern to copy, not to call), `lib/idempotency-key.ts`, `lib/query-keys.ts`, `tests/render-with-query.tsx`, `tests/fetch-mock.ts`, `tests/order-fixtures.ts`, `features/orders/components/OrderDetailView.copy.test.tsx` (setup to copy).
- Backend (read only, to know what you rely on): `backend/tests/Ecommerce.Api.IntegrationTests/Cart/CartMergeTests.cs` test names; `Orders/OrdersReadApiTests.cs` test `T7_another_customers_order_is_403_and_never_appears_in_the_callers_list`.

No backend code, no endpoint, no migration, no new package. §12.3 has no row for these tracker rows; the tracker row is the acceptance criterion.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Reuse `POST /api/v1/cart/merge`** (one request, all lines, `Idempotency-Key`) — no new endpoint | It already does exactly the reorder rule (§8.1, app context §4.4): sums with the existing server line, caps at min(available, `Cart:MaxQuantityPerLine`), removes inactive/unknown/other-currency variants, prices every line from Catalog, answers one result per line, and replays a repeated key without re-adding. All of it is covered by `CartMergeTests` (T22, 12 tests incl. 401, 400, parallel same key, extra `price` field → 400). Zero new backend surface | A new `POST /orders/{id}/reorder` (or `/cart/reorder`) in Cart calling a new `IOrderReader` method: duplicates the merge rule, adds a contract method, a cross-module call, an ownership check and an idempotency scope — all for the same outcome. Per-item `POST /cart/items`: N requests, partial failure half-fills the cart, a lost response plus retry doubles quantities, and 409/400 per line instead of a capped result |
| **Ownership** is the existing rule on `GET /orders/{id}` (`OrderQueryService.GetAsync`: other customer → 403, unknown → 404; test T7). The client reorders only lines it was allowed to read | The merge body is just `{variantId, quantity}` — the same thing any signed-in user may already send to `POST /cart/items`. Reorder grants no new capability, so there is no new ownership guard to add | A server-side ownership check on a reorder endpoint (needs the endpoint rejected above) |
| **Prices:** the request carries **only** `variantId` and `quantity`, built explicitly (never spread the `OrderItem`). The order's `unitPrice`/`lineTotal` are never sent; the cart shows current Catalog prices | §3.4 rule 1; strict binding already 400s an extra `price` field (T22 bad-bodies test), so a leaked field would also break the feature | Sending the old price "for display" |
| **Unavailable / out of stock / over stock** are the merge outcomes: `Removed` (Unavailable / OutOfStock), `Capped` (OutOfStock / LimitReached), `Kept`. The client never caps or filters — it sends the order quantities and shows the server's per-line result | Server is the only truth for stock and cap (§8.1 "never trusted from the device"); one rule, already tested | Client-side filtering by `stockStatus` (stale, duplicates the rule) |
| **Idempotency / double tap:** one key from `newIdempotencyKey()` per reorder **press**, held in a `useRef`. An in-flight `useRef` guard ignores a second press before re-render; the button is also `disabled` while pending. A failed request (network, 5xx, timeout) keeps the key, and "Try again" resends the **same** key and body. A `conflict` (409 key reuse) drops the key so the next try gets a new one. After success the button stays disabled ("Added to cart") for this screen visit | Same pattern as the guest merge (decision-log row "Guest merge on the client"): a lost response + retry replays the stored reply, never doubles. A deliberate new reorder later (new visit) is a new intent with a new key | A key derived from the order id (`reorder-{orderId}`): a second, intentional reorder within 24 h would silently replay and add nothing; and the key would collide across users only via the 409 path |
| **Duplicate variants in one order:** aggregate the order items by `variantId` (sum quantities, first-occurrence order) before sending | The merge validator requires distinct variants (400 otherwise). Orders come from carts (one line per variant), so this is defensive and costs one `reduce` | Sending raw items and risking a 400 |
| **Which orders show Reorder:** every status **except `PendingPayment`** | A pending order's cart lines are still in the cart until Paid (§8.2); reordering would double them. Cancelled, Refunded, Returned and Delivered are the classic "buy again" cases | Hiding it for Cancelled — a cancelled order is the commonest "buy again" case |
| **Result UI on the order detail page**, not the global `MergeResultNotice`: a local panel (`accessibilityRole="summary"`, heading "Added to your cart") listing one line per result as `` `${name}: ${mergeResultText(r)}` `` (name from the order item), plus a "View cart" link (`/cart`). Failure: `accessibilityRole="alert"` with `userMessage(error)` and a "Try again" button | The global notice's copy is about the device cart ("The cart on this device was added…") — wrong here; its store has retry wiring for the guest flow. Reusing `mergeResultText` keeps the per-line wording identical | Publishing into `useMergeResultStore` (wrong heading, would also collide with a guest-merge notice) |
| On success: `queryClient.setQueryData(queryKeys.cart, response.cart)` | The header cart count and `/cart` show the new cart without a refetch (same as the guest merge) | `invalidateQueries` — an extra request |
| Code lives in `features/orders` (`hooks/useReorder.ts`, component `ReorderAction` in `OrderDetailView.tsx`), importing `mergeCart` and `mergeResultText` from `features/cart` | The order page is the only consumer; cart already exports both. No new shared component | A generic `components/ReorderButton` — no second consumer |

Record: Rob appends one decision-log row "Reorder via cart merge (P2.49, Rob)" in this planning step — done.

## 3. Files (all Marco)

| File | Change |
|---|---|
| `frontend/apps/store/features/orders/hooks/useReorder.ts` | **new** — the hook (contract below) |
| `frontend/apps/store/features/orders/hooks/useReorder.test.tsx` | **new** — hook-level tests §5 (H1–H6) |
| `frontend/apps/store/features/orders/components/OrderDetailView.tsx` | `ReorderAction({ order })` in `Header`'s actions column next to "← All orders"; result/failure panel under the header row inside the same `Panel` |
| `frontend/apps/store/features/orders/components/OrderDetailView.reorder.test.tsx` | **new** — screen tests §5 (S1–S7) |
| `docs/admin-dashboard-feature-tracker.md` | P2.49 `Todo` → `Done` after verify passes |

**Hook contract** (`useReorder.ts`):

```ts
export type ReorderLine = MergeLineResult & { name: string };
export type ReorderState =
  | { status: 'idle' } | { status: 'pending' }
  | { status: 'done'; lines: ReorderLine[] }
  | { status: 'failed'; message: string };
export function reorderLines(items: OrderItem[]): MergeLineInput[]; // exported pure helper: aggregate by variantId, only {variantId, quantity}
export function useReorder(items: OrderItem[]): { state: ReorderState; reorder: () => void };
```

Behaviour: `reorder()` returns immediately if `inFlight.current` or `state.status === 'done'`. Sets `inFlight.current = true`, `keyRef.current ??= newIdempotencyKey()`, state `pending`; calls `mergeCart(reorderLines(items), keyRef.current)`. Success → `setQueryData(queryKeys.cart, response.cart)`, state `done` with names mapped by `variantId` from `items` (fallback `'Item'`). Error → if `ApiError` with `kind === 'conflict'` set `keyRef.current = null`; state `failed` with `userMessage(error)`. `finally` clears `inFlight`. No `console.*`; no logging of order data.

**UI contract** (in `Header`, actions column, `flex-row flex-wrap gap-sm`; on `compact` each button `flex-1` like `.head-actions .btn{flex:1}`):
- Hidden when `order.status === 'PendingPayment'`.
- `Pressable` `accessibilityRole="button"`, `accessibilityLabel`: idle `Reorder all items`, pending `Adding items to cart`, done `Items added to cart`; `accessibilityState={{ disabled, busy }}`; `disabled` when pending or done; text `Reorder` / `Adding…` / `Added to cart`. Primary-style tokens already used by other primary buttons in the app (grep an existing primary `Pressable`, e.g. checkout); `min-h-touch`. No new tokens.
- Done panel: `accessibilityRole="summary"`, title "Added to your cart", one `Text` per line, a `Link href="/cart" asChild` with `accessibilityRole="link"` "View cart".
- Failed panel: `accessibilityRole="alert"`, message text in `text-danger`, button "Try again" (calls `reorder()`; the hook reuses the key).

No `app/` change. No backend change. No package change.

## 4. Build order

1. Write `useReorder.test.tsx` H1–H3 (the guards) first; run, quote red (module not found / assertion).
2. Implement `useReorder.ts`; H1–H6 green.
3. Write `OrderDetailView.reorder.test.tsx` S1–S3 first; quote red (button not found).
4. Implement `ReorderAction` + panels in `OrderDetailView.tsx`; S1–S7 green.
5. Run the existing `OrderDetailView.test.tsx`, `.copy.test.tsx`, `.refunds.test.tsx` **unchanged**; they must pass. If one fails because it counts buttons or fetch calls, stop and report — do not edit it without listing the reason.
6. Run the backend tests you rely on, unchanged: `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~CartMergeTests|FullyQualifiedName~T7_another_customers_order" 2>&1 | tail -n 20` and quote the summary.
7. Run the 4 mutations in §7 (scratch copy → break → red → restore → `cmp`), quote each red.
8. Full `bash scripts/verify.sh 2>&1 | tail -n 30`. Mark the tracker row Done. Report to `slice-plans/slice-P2.49-marco-report.md` (≤ 6 KB).

## 5. Required tests

**Hook — `useReorder.test.tsx`** (`renderHook` with `queryWrapper(createTestQueryClient())`, session signed in as Customer, fetch via `mockFetchRoutes`/`jest.spyOn(globalThis,'fetch')`):

- **H1 (test-first, price guard)** Body is exactly `{ lines: [{ variantId: 'v-1', quantity: 2 }, …] }` — `toEqual` on the parsed body, and assert no line has `unitPrice`, `lineTotal`, `price`, `sku` or `name`. URL ends `/api/v1/cart/merge`, method `POST`, header `Idempotency-Key` present and non-empty.
- **H2 (test-first, double tap)** Calling `reorder()` twice synchronously (inside one `act`) sends **one** request.
- **H3 (test-first, retry key)** First call → network failure (fetch rejects) → state `failed`; `reorder()` again → second request carries the **same** `Idempotency-Key` and same body. Then a 409 problem response → next `reorder()` uses a **different** key.
- H4 `reorderLines` aggregates duplicate variants (`v-1`×2 + `v-1`×1 → one line ×3) and keeps first-occurrence order.
- H5 Success writes `response.cart` to `queryKeys.cart` (read back with `client.getQueryData`) and maps names by `variantId`; an unknown id gets `'Item'`.
- H6 After `done`, `reorder()` sends nothing more.

**Screen — `OrderDetailView.reorder.test.tsx`**: mount `renderRouterWithQuery({ 'orders/[id]': OrderDetailScreen, cart: CartScreen }, { initialUrl: '/orders/o-1' })` (`CartScreen` = default export of `@/app/cart`), `signInAs('Customer')`, `mockFetchRoutes` for `/api/v1/orders/o-1`, `/api/v1/cart/merge` and `/api/v1/cart`. Never `renderRouter('./app')`. Same `beforeEach`/`afterEach` as `OrderDetailView.copy.test.tsx`.

- **S1 (test-first)** `getByRole('button', { name: 'Reorder all items' })` is shown for a `Shipped` order and the header `findByRole('header', { name: 'Order ORD-2026-000001' })` still resolves.
- **S2 (test-first)** Press → merge called once; `findByRole('summary')` contains "Added to your cart" and `Black tee, M: 2 in your cart`; button becomes `Items added to cart` and is disabled (pressing again sends nothing).
- **S3 (test-first)** Mixed result: a `Capped` (`OutOfStock`, quantity 1) and a `Removed` (`Unavailable`) line render `reduced to 1 (not enough in stock)` and `not added (no longer available)` — every line shown, none dropped.
- S4 Merge fails (500 problem) → `findByRole('alert')` with the shared `userMessage` text; "Try again" resends (second merge call, same `Idempotency-Key`).
- S5 `status: 'PendingPayment'` → `queryByRole('button', { name: 'Reorder all items' })` is null.
- S6 "View cart" link navigates: press → `expect(screen).toHavePathname('/cart')`.
- S7 Pending: with the merge response held (a never-resolved promise released at the end), the button is `Adding items to cart` and disabled (`accessibilityState.disabled`).

**Backend (existing, must stay green, unchanged):** `CartMergeTests` (all T22 — happy, 401, 400, replay, parallel same key, extra `price` → 400, inactive → Removed, over stock → Capped) and `OrdersReadApiTests.T7_another_customers_order_is_403_…`. These are the server-side proofs this slice depends on; no new endpoint, so no new integration tests are required (CLAUDE.md §3: the endpoint's happy/401/400 tests exist).

No §11.2 numbered tests belong to this row. Existing tests: zero edits expected; any change is listed with its reason.

## 6. Risks

- **Doubling via key reuse mistakes:** a fresh key per attempt turns a lost-response retry into a double add. H3 + M2 guard it.
- **Double tap before re-render:** state alone can let two presses through; the `useRef` guard (H2 + M1) is required, `disabled` is only the visible half.
- **Price leak:** spreading `OrderItem` into the body sends `unitPrice` → server 400, feature broken (and a §3.4 smell). H1 + M3.
- **PendingPayment double:** hidden by S5; if the status list ever grows, the rule is "not PendingPayment", not an allow-list.
- **> 100 distinct variants** in one order → merge 400 → failed panel with the generic message. Not reachable from today's checkout in practice; noted, not handled.
- **Existing order-detail tests** that count buttons or exact fetch calls may change; step 5 stops on that rather than editing them.
- **Live status polling** (`useOrderLiveStatus`) also calls the orders URL — use `mockFetchRoutes` (URL-keyed), not `mockImplementationOnce` sequences, and count merge calls by URL (`calledUrls`).
- **Fake timers** not needed; S7 uses a deferred promise.

## 7. Mutations (Marco quotes red for each; Rob re-runs ≤ 2 on re-review)

| # | Guard | Break (in a scratch copy, then restore + `cmp`) | Must go red |
|---|---|---|---|
| M1 | Double-tap / in-flight guard | delete the `if (inFlight.current …) return` line in `useReorder.ts` | H2 |
| M2 | Retry reuses the key | replace `keyRef.current ??= newIdempotencyKey()` with `keyRef.current = newIdempotencyKey()` | H3 |
| M3 | Client sends no price (server prices) | in `reorderLines`, build lines as `{ ...item }` (or add `unitPrice: item.unitPrice`) | H1 |
| M4 | Order ownership (the existing guard reorder relies on) | `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderQueryService.cs` line ~52: `order.UserId != userId` → `false` | `OrdersReadApiTests.T7_another_customers_order_is_403_and_never_appears_in_the_callers_list` |

## 8. Review

High tier: Rob reviews the diff + untracked files (quality-bar §3), confirms no backend/package change, no existing test edited, the body is built explicitly, re-runs M1 and M3, then runs full `verify.sh` for PASS.
