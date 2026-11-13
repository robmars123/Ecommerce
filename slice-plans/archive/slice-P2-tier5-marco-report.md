# Slice P2-tier5 — Marco report

Concerns: None blocking. Notes: (1) the plan's S13/S14 names collide with existing footer tests S13/S14, so the new pill tests are named S30 (plan S13, widths 1024 and 390) and S31 (plan S14, signed out). (2) `format` is now a verify step; prettier flagged 3 new files, fixed with `prettier --write`. (3) Initial-start failure leaves the pill offline until remount (plan risk, accepted).
Bugs found: None
Bugs fixed: None
Bugs open: None

## R5 test-first (stale connection)
Wrote R1-R6 first against a version of `subscribeToConnectionState` with no current-connection guard. Red, only R5 (ios and android):
`R5 ignores events from a connection that is no longer the current one — expect(again).toHaveBeenLastCalledWith(true); Received 3: false` (first socket's `onclose` flipped the pill to offline after the second socket was up).
Then added `whenCurrent` (`connection === built`) around onreconnected, onreconnecting, onclose and both `start()` outcomes: `lib/realtime.test.ts` 24/24 green (6 existing its unchanged).

## Tests changed (one-line reason each)
- `features/admin-shell/nav.test.ts` N1 — expected table drops `ready` and uses the stories href (D8 removed the flag).
- `features/admin-shell/breadcrumb.test.ts` B7 — iterates items whose href has no `?` instead of `ready`; B8 added.
- `features/admin-shell/components/AdminShell.test.tsx` S8 — 10 links; path part compared, stories link also checks `tab=stories`.
- same, S9 — Support now sees 4 links incl. User Stories as a link (role gating unchanged: still no admin-only item).
- same, S10 — deleted: it tested the disabled "Soon" branch D8 removed.
- `lib/realtime.test.ts` — mock `build()` gains `onreconnecting`/`onclose` capture (mock only); existing `it` blocks untouched.
- `tests/jest-setup.js` — global `@/lib/realtime` mock adds `subscribeToConnectionState`.
Not touched: `DashboardView.test.tsx`, `DashboardInsights.test.tsx` (git diff empty), `useOrderLiveStatus.test.tsx`.

## Changed
New: `features/admin-dashboard/{tabs.ts, tabs.test.ts, stories.ts, stories.test.ts}`, `components/{ViewTabs, UserStoriesTab, AcceptanceCriteriaTab, AdminDashboardScreen}.tsx`, `components/DashboardTabs.test.tsx`, `features/admin-shell/components/{LiveStorePill.tsx, LiveStorePill.test.tsx}`.
Edited: `app/admin/index.tsx`, `DashboardView.tsx` (one optional `tabs` prop), `admin-shell/{nav.ts, breadcrumb.ts}`, `AdminShell.tsx` (Soon branch removed, pill when signed in), `lib/realtime.ts`, tests above, `docs/admin-dashboard-feature-tracker.md` (P2.18, P2.19, P2.20, P2.9 Done), `AGENT_STATUS.md`.
`DashboardTabs.test.tsx` mounts `{ 'admin/index': AdminIndexScreen }` via `renderRouterWithQuery` (no `renderRouter('./app')`).

## Tests added
realtime R1-R6; LiveStorePill P1, P2; AdminShell S30, S31; breadcrumb B8; tabs T1, T2; stories S1-S5; DashboardTabs DT1-DT7 (DT7 checks the card wrapper is `basis-full` at 390 and `basis-[45%]` at 1024).

verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings; jest Test Suites: 252 passed, Tests: 1935 passed; tsc clean; export ok. (First run failed only `format`; fixed.)

## Quality-bar §2
- [x] Tests for every screen and the connection logic (reasons above).
- [x] No mutations (Low tier, plan §6).
- [x] No new package, no backend, hub or auth change.
- [x] Theme tokens only; no new hex.
- [x] Story copy cites only Done rows (S3 enforces); Rob to check each criterion against its rows.
- [x] No secrets, no git writes.
- N/A: backend endpoint tests (no endpoint added).

## Round 1 fix
Reworded two strings in `frontend/apps/store/features/admin-dashboard/stories.ts` (no tests edited):
- US-04: "A single-status bucket links to the filtered orders list; the mixed buckets (In progress, Cancelled or refunded) show one link per status."
- US-05: "Export CSV downloads a CSV file on web and opens the share sheet on Android and iOS."
jest features/admin-dashboard: 26 suites, 236 tests passed.
VERIFY PASS (secrets hygiene format tsc jest export)
