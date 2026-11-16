# Slice P2.84 — Marco report

Concerns:
- Plan §2 says the gate redirects to `/login?returnTo=<current admin path>`. Measured: the gate first redirects with `/admin/orders`, then re-runs at `/admin` while the page stack unmounts, so the login URL ends with `returnTo=/admin`. Safe (same-app path, role re-checked), but not the current page. O4 asserts `/admin`, with a comment. Logged in backlog; Rob to decide (plain `/login` would need explicit navigation).
- Plan §3 lists three test helpers; four more AdminShell renders (badges, Paid badge, attention x2) also mount the footer without a query client and failed, so I wrapped them the same way.
- O4 cache assertion: the root client is not reachable from the test, so it spies `QueryClient.prototype.clear` and expects 2 calls (root `onSignedOut` + the mutation's own). M2 turns it red.
- O6: second tap is made after `waitFor(button disabled)`. A same-tick second press (before React renders `pending`) does send a second call. A human double tap is a frame apart, so I left it; flagging for Rob.
- Backlog: web failed-logout cookie note added (coordinator default 1). AdminGate unchanged (default 2).

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed (review as possibly weakened):
- `AdminShell.test.tsx` helpers `renderShell`, `renderShellWithRoutes`, `renderShellAt`, plus `renderWithBadges`, `renderPaidBadgeAt`, `renderWithAttention` and the attention layout render (C4): wrapped in `QueryClientProvider` with `createTestQueryClient()`. No assertion changes.
- S26: renamed "...renders and sends no request until someone types"; added `expect(fetchSpy).not.toHaveBeenCalled()` (stronger).
- New: O1-O3 in `AdminShell.test.tsx`; O4-O7 in `tests/admin-logout.test.tsx`.

Red-first (O4-O7 before the footer change): `npx jest tests/admin-logout.test.tsx` -> `Tests: 10 failed, 10 total` (two projects), `findByRole('button', { name: 'Log out' })` not found.

Mutations (scratch copy of `useLogoutMutation.ts`, restored, `cmp` clean):
- M1 delete `await endSession();` -> `Tests: 10 failed, 10 total` (O4-O7 red, both projects).
- M2 delete `queryClient.clear();` -> `Tests: 2 failed, 8 passed` (O4 red, both projects).

Changed:
- frontend/apps/store/features/admin-shell/components/AdminShell.tsx (footer: outer View, accessible group, ActionButton + useLogoutMutation)
- frontend/apps/store/features/admin-shell/components/AdminShell.test.tsx
- frontend/apps/store/tests/admin-logout.test.tsx (new)
- slice-plans/backlog.md (two notes)
- AGENT_STATUS.md

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 274 suites, 2127 tests passed.

Quality-bar §2: all boxes met for a frontend-only slice; backend, migration and new-package items N/A (none touched). Not committed.
