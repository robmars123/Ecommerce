# Slice P2.55 — Marco report

Concerns: None. Phone-width: the avatar group is inside the existing flex-wrap bar (not manually checked in a browser; no layout test added).
Bugs found: features/checkout/components/PaymentStep.tsx — T14 (cart never invalidated after Paid)
Bugs fixed: PaymentStep `useEffect([client, status])` invalidates `queryKeys.cart` once when status leaves PendingPayment — T14 passes
Bugs open: None

## Red output (test-first)
- T14 before the PaymentStep change (T15 passed, as it should):
  `expect(client.getQueryState(queryKeys.cart)?.isInvalidated).toBe(true)` Expected: true, Received: false (CheckoutView.test.tsx, both ios and web projects). `Tests: 2 failed, 2 passed`.
- Avatar: `avatar-initial.test.ts` "Cannot find module './avatar-initial'"; AccountBar T5 and T6 failed (no "Signed in as ..." label) before the AccountBar change; T7 passed.

## Tests changed
No existing test changed or weakened. Only additions, plus two non-behavioural edits for the new cases:
- `TopNav.test.tsx`: `routes()` now takes an optional client (`routes(client = createTestQueryClient())`) so T13 can reach it; added `act` import and imports for cart fixtures, fetch helper, queryKeys. All existing calls unchanged.
- `CheckoutView.test.tsx`: added `queryKeys` import and `createTestQueryClient` to the existing render-with-query import.

## Tests added
- `avatar-initial.test.ts` (new): T1–T4.
- `AccountBar.test.tsx`: T5–T7.
- `useCartCount.test.tsx` (new): T8–T11 (`renderHook` is async in this RNTL version, so it is awaited).
- `TopNav.test.tsx`: T12–T13 in `describe('cart badge')`.
- `CheckoutView.test.tsx`: T14–T15 (own `renderWith(client)` helper; existing `render` untouched).

## Changed
New: `features/auth/avatar-initial.ts` (`shownName`, `avatarInitial`; `shownName` is a second export used by AccountBar so name and initial share one rule), `avatar-initial.test.ts`, `features/cart/hooks/useCartCount.test.tsx`.
Edited: `AccountBar.tsx` (accessible group "Signed in as {name}", initial circle, name = displayName.trim() || email), `PaymentStep.tsx`, the three test files above, `docs/admin-dashboard-feature-tracker.md` (P2.55 Done), `AGENT_STATUS.md`.
No backend, package, or config change. No mutations (Low risk).

## verify.sh
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`
jest: Test Suites: 272 passed, Tests: 2069 passed. build 0 warnings. (An earlier run failed only on prettier format in two new test files; fixed with prettier --write.)

## Quality-bar §2
All boxes met for a Low-risk frontend slice: tests for new behaviour (T1–T15), verify.sh green, no secrets, no hex colours (tokens only), no git writes. Mutations N/A (Low). Backend/integration checks N/A (no backend change).
