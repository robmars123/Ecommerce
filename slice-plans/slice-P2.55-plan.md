# Slice P2.55 — Top bar avatar initial and cart count badge

Tracker row: `docs/admin-dashboard-feature-tracker.md` P2.55 (`components`, Cart). Customer storefront top bar (`TopNav`). Design: `docs/design-refs/northline-order-detail.md` §Structure item 1 and the [feature] list. Mode: Autonomous. All files: **Marco**.

**Risk tier: Low** (CLAUDE.md §5). Read-only display: no cart write, no auth rule, no money computed, no backend change. The one touch on the payment screen is a cache invalidation after the server already says the order left `PendingPayment`; it changes no payment state. Rob reads the diff; **no mutations**. Marco's Jest tests + full `verify.sh`.

## 1. Read

Marco reads only this:
- This plan.
- `frontend/apps/store/components/TopNav.tsx`, `components/TopNav.test.tsx`
- `frontend/apps/store/features/auth/components/AccountBar.tsx`, `AccountBar.test.tsx`
- `frontend/apps/store/features/cart/hooks/useCartCount.ts`, `useCartQuery.ts`
- `frontend/apps/store/features/checkout/components/PaymentStep.tsx` (lines 1–90) and `CheckoutView.test.tsx` (setup lines 1–120 and the test "an already paid order shows \"Payment received\"")
- `frontend/apps/store/tests/session.ts`, `tests/cart-fixtures.ts`, `tests/render-with-query.tsx`
- Architecture §5.2, §5.5 (`docs/architecture/05-frontend.md`), §11.3 (`docs/architecture/11-testing.md`, `grep -n "^### 11.3"` + offset).

## 2. What already exists (do not rebuild)

- The cart badge is already built: `useCartCount()` (signed in: sum of server cart `quantity`, `null` until loaded; guest: sum of the device store) feeds `NavLink badgeCount` in `TopNav`; hidden at 0/null, `99+` above 99, label `"Cart, N item(s)"`. Guest cases are tested in `TopNav.test.tsx`.
- Gaps this slice closes: (a) no test for the signed-in (server) count, and `useCartCount` has no co-located test (§11.3); (b) the badge goes stale after payment: on `Paid` the server removes the cart lines (`IOrderPayments` contract comment), but the client cart query is never invalidated, so the always-mounted `TopNav` keeps the old count (staleTime 30 s, no remount, `refetchOnWindowFocus: false`); (c) no avatar initial.

## 3. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Count source stays the existing `useCartCount` (`GET /api/v1/cart` via `queryKeys.cart` when signed in; Zustand guest store otherwise). No backend change | One request already serves `/cart`, checkout and the badge; no new auth surface | A `GET /cart/count` endpoint (new surface, duplicate read) |
| Badge shows **total quantity** (sum of `quantity`), not line count | Already built and tested; matches "N items" label and shopper expectation (2 × tee = 2 items) | Line count (would change existing tests for no gain) |
| Signed in: hidden while the server cart is loading or errored (`null`). Guest: device lines. Sign-in switches to the server cart after merge; sign-out `queryClient.clear()` + guest store | Never show a guessed number; existing behaviour | Showing the guest count until the server answers (flashes a wrong number) |
| Freshness: add, edit, remove (`useCartMutations` `setQueryData`/invalidate), merge (`useGuestCartMerge` `setQueryData`), reorder (`useReorder` `setQueryData`) and guest edits (Zustand) already update the shared key/store. **New:** `PaymentStep` invalidates `queryKeys.cart` once when the polled order status is no longer `PendingPayment` | The server removes lines on Paid; the polled order is the server's answer (rule 5), so invalidating then is the earliest correct moment. Invalidate, never `setQueryData` to empty: the server cart wins (§5.2) | Optimistic clear on payment-form success (a hint only, rule 5); a cart refetch interval (load for no gain); invalidating in `useOrderLiveStatus` (realtime is a hint; the checkout poll already covers the flow) |
| Avatar: a small circle with the initial, rendered in `AccountBar` before the name, signed in only. Name source: `session.user.displayName.trim() \|\| session.user.email` (same rule as `AdminShell` `SidebarFooter`); initial = first code point (`Array.from(name)[0]`) upper-cased; `'?'` if both are empty | Session already holds the user (no profile request); `Array.from` keeps an astral character whole; same fallback rule as the admin shell | Fetching a profile; `name[0]` (splits surrogate pairs); a shared `Avatar` component in `components/` (one consumer) |
| Keep the visible display name next to the avatar | Smallest change; existing `AccountBar` test reading "Customer User" stays unchanged; the phone layout already wraps | Avatar only (changes an existing test, hides who is signed in on web) |
| Accessibility: avatar + name are one `accessible` View labelled `"Signed in as {name}"`; the initial `Text` adds no separate stop. Cart link label unchanged (`"Cart"` / `"Cart, N item(s)"`) | One screen-reader stop, same pattern as `SidebarFooter`; a lone "C" is noise | Labelling the initial ("Avatar C") |
| Signed-out: no avatar (Log in / Register as today). Restoring: nothing (as today) | Existing states; no flash | A placeholder avatar for guests |
| Styling: existing tokens only (`rounded-pill`, `bg-primary-subtle`, `text-primary-text`, `text-small font-bold`, arbitrary size `h-[28px] w-[28px]`); no hex | §5.5 one token source; works in both themes | New tokens |

Decision log: Rob appends one row "Top bar avatar and cart badge freshness (P2.55)".

## 4. Files

| File | Change |
|---|---|
| `frontend/apps/store/features/auth/avatar-initial.ts` (new) | `export function avatarInitial(user: Pick<SessionUser, 'displayName' \| 'email'>): string` — name rule and initial rule above. Pure |
| `frontend/apps/store/features/auth/avatar-initial.test.ts` (new) | Unit tests (§5 T1–T4) |
| `frontend/apps/store/features/auth/components/AccountBar.tsx` | Signed-in branch: wrap the existing name `Text` and a new initial circle in one `View accessible accessibilityLabel={\`Signed in as ${name}\`}` with `flex-row items-center gap-sm`. Name shown = `displayName.trim() \|\| email` (same rule). Admin link and Log out unchanged, outside the group |
| `frontend/apps/store/features/auth/components/AccountBar.test.tsx` | Add tests T5–T7. Existing tests unchanged |
| `frontend/apps/store/features/cart/hooks/useCartCount.test.tsx` (new) | Hook tests T8–T11 (`renderHook` with `queryWrapper(client)` from `tests/render-with-query.tsx`, fetch mocked with `jest.spyOn(globalThis, 'fetch')`) |
| `frontend/apps/store/components/TopNav.test.tsx` | Add T12–T13 inside `describe('cart badge')`. Existing tests unchanged |
| `frontend/apps/store/features/checkout/components/PaymentStep.tsx` | `useQueryClient()`; `useEffect(() => { if (status !== undefined && status !== 'PendingPayment') void client.invalidateQueries({ queryKey: queryKeys.cart }); }, [client, status])`. Effect keyed on `status`, so it fires once per transition, not per poll |
| `frontend/apps/store/features/checkout/components/CheckoutView.test.tsx` | Add T14–T15 |
| `docs/admin-dashboard-feature-tracker.md` | P2.55 → Done (after PASS) |

No backend, no migration, no new package, no new route, no `lib/` change.

## 5. Build order and required tests

Test-first (quote the red) for **T14** (the only behaviour change outside display). The rest may be written alongside.

1. `avatar-initial.ts` + tests:
   - T1 `{ displayName: 'nathaniel', email }` → `'N'`.
   - T2 blank/whitespace `displayName` → initial of the email (`'c'` from `customer@…` → `'C'`).
   - T3 astral first character (e.g. `'𝒜lice'`) → whole code point, not half a surrogate (`length` 2).
   - T4 both empty → `'?'`.
2. `AccountBar`:
   - T5 signed-in customer: `getByLabelText('Signed in as Customer User')` exists and contains text `'C'`; `'Customer User'` still visible.
   - T6 blank display name (`signInAs('Customer', { user: { …, displayName: '  ' } })`): label `"Signed in as customer@example.test"`, initial `'C'`.
   - T7 signed out: no `/Signed in as/` label.
3. `useCartCount` (new hook test):
   - T8 signed in: `null` before the cart response, then the **sum of quantities** (lines 2 + 1 → 3, not 2 lines).
   - T9 signed in, cart request fails (500): stays `null`.
   - T10 guest: sum of guest store lines; no `GET /api/v1/cart` call made.
   - T11 signed in: `client.setQueryData(queryKeys.cart, makeCart([]))` → `0` (the shared key drives the badge; proves add/merge/reorder freshness, which all write this key).
4. `TopNav` (existing route map in the file; it already mounts `TopNav` as the Stack header under a `QueryClientProvider`):
   - T12 signed-in customer, `GET /api/v1/cart` → two lines qty 2 and 1 → link `"Cart, 3 items"` and text `'3'`.
   - T13 signed-in, after the badge shows `"Cart, 1 item"`, `invalidateQueries(queryKeys.cart)` with the next cart response empty → the badge disappears and the link name is `"Cart"`. (Expose the client by building it in the test and passing it to a `routes(client)` variant; do not change existing tests' behaviour.)
5. `PaymentStep` (via `CheckoutView.test.tsx`, existing `renderRouterWithQuery` route map; pass `client`):
   - **T14 (test-first)** seed `client.setQueryData(queryKeys.cart, makeCart())`; order `GET /api/v1/orders/o-1` returns `Paid`; after "Payment received", `client.getQueryState(queryKeys.cart)?.isInvalidated` is `true`. Red before the `PaymentStep` change.
   - T15 order stays `PendingPayment` (the "Continue to payment" resume case): the seeded cart query is **not** invalidated.
6. `bash scripts/verify.sh 2>&1 | tail -n 30` (full). Quote the `VERIFY` line and jest count in `slice-P2.55-marco-report.md`.

No §11.2 numbered or F tests are named by this row (P2 row in §12.3 requires "every P2 row Done; phone width and both themes pass"): the avatar uses theme tokens only; the existing phone-width tests must stay green.

## 6. Risks

- **Effect loop / over-fetch:** invalidating on every poll render would refetch the cart every 2 s. Mitigation: the effect depends on `status` only (T15 guards the PendingPayment side; Rob checks the deps array).
- **Payment-screen regression:** the change is additive; all existing F4/F5/F11 tests in `CheckoutView.test.tsx` must pass unchanged. Any changed existing test is blocking unless justified.
- **Webhook lag:** if the poll sees Paid, the lines are already gone (same transaction as the status change, per the Orders contract), so the refetch is correct; no race to handle.
- **Phone width:** the extra 28 px circle in the wrapping top bar; `flex-wrap` already handles it. Manual check on a narrow web window in the report (one line).
- **Scope creep:** no profile menu, no dropdown, no avatar image, no admin shell change.
