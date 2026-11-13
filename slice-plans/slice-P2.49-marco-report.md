# Slice P2.49 — Marco report (Reorder via cart merge)

Concerns: None blocking. (1) `summary` role is on an `accessible` inner View holding the title and lines; "View cart" sits beside it, not inside, so a screen reader does not swallow the link. (2) The failed panel puts `accessibilityRole="alert"` on the message Text (RNTL needs an accessible element). (3) Plan's `Header` hook placement: `useReorder` is called once in `Header` (always mounted), shared by the button and the result panel; the button is simply not rendered for PendingPayment.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None. No existing test edited; existing OrderDetailView.test/.copy/.refunds pass unchanged (features/orders: 26 suites, 180 tests).

Changed (slice P2.49):
- frontend/apps/store/features/orders/hooks/useReorder.ts (new)
- frontend/apps/store/features/orders/hooks/useReorder.test.tsx (new, H1-H6)
- frontend/apps/store/features/orders/components/OrderDetailView.tsx (ReorderAction, ReorderResult, Header)
- frontend/apps/store/features/orders/components/OrderDetailView.reorder.test.tsx (new, S1-S7)
- docs/admin-dashboard-feature-tracker.md (P2.49 Done)
- AGENT_STATUS.md. No backend, package or app/ change.

Test-first red:
- H1-H6: "Cannot find module './useReorder'" (2 suites failed, 0 tests) before the hook existed; 12/12 green after.
- S1-S7: "Unable to find an element with role: button, name: Reorder all items" (12 failed, 2 passed) before the UI; green after (a role/matcher tweak was test-side only: `summary` needs `accessible`; `toHaveTextContent` with `exact:false`).

Backend relied on, unchanged: CartMergeTests + T7 -> "Passed! Failed: 0, Passed: 14, Total: 14".

Mutations (scratch copy, break, red, restore, cmp = identical each time; counts are over 2 jest projects):
- M1 delete in-flight guard: H2 red ("Expected number of calls: 1, Received: 2"), H6 also red; 4 failed.
- M2 `keyRef.current = newIdempotencyKey()`: H3 red (retry key Expected X, Received Y); 2 failed.
- M3 `lines.push({ ...item })`: H1 red (extra fields in body), H4 red; 4 failed.
- M4 OrderQueryService.cs:52 `order.UserId != userId` -> `false`: T7 red ("Expected: Forbidden, Actual: OK"), Failed 1 of 2. Restored, cmp identical.

verify.sh: first run FAIL format (prettier, 2 new files); ran `prettier --write` on them, then full re-run:
VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export) - build 0 warnings, jest 258 suites / 1973 tests.

Quality-bar §2: tests for each rule (H1-H6, S1-S7, existing server T22/T7) [x]; money server-side (no price sent) [x]; idempotency key reuse/drop [x]; no secrets/console logging [x]; no new package/endpoint [x]; module boundaries (features/orders imports cart's api/types/messages only; no cross-module DbSet) [x]; accessibility labels/roles/touch targets (min-h-touch) [x]; mutations M1-M4 quoted [x]; verify.sh full pass [x]. Not run: Android/iOS device, only Jest ios/android/web projects.
