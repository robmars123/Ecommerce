Concerns: None (see Deviations; manual web check not done)
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None (no existing test touched)

Slice: P2.1-P2.2 (gap-fill, Low risk) - admin theme tokens + admin shell. Frontend only, no new package.

## Changed (frontend/apps/store/)
- lib/design-tokens.js - `dark` (existing colours + `scrim`), `light` (D2), `colors: dark`, `themes`, `sidebarBreakpoint: 768`, `width.sidebar: 240px`.
- lib/design-tokens.d.ts - `themes`, `sidebarBreakpoint`.
- lib/design-tokens.test.ts (new) - T1-T3.
- features/admin-shell/components/AdminShell.tsx (new) - wide: fixed sidebar; narrow: top bar + Menu toggle + overlay (scrim backdrop + panel), brand and one Dashboard link.
- features/admin-shell/components/AdminShell.test.tsx (new) - S1-S6.
- app/admin/_layout.tsx - Stack wrapped in `<AdminShell>` inside `<AdminGate>`; Stack, screenOptions and comment unchanged (only re-indented).
- tests/app/admin-shell.test.tsx (new) - R1-R2.
Not touched: tailwind.config.js, app/_layout.tsx, AdminGate, any existing test.

## Tests added
Each runs in two Jest projects (ios + web): T1-T3 = 6, S1-S6 = 12, R1-R2 = 4; 22 test runs total.
Red first: T1-T3 failed with `Cannot read properties of undefined (reading 'light')` (6 failed); S1-S6 suite failed to run (module missing); then green.
Jest default window width was not measured directly. All 179 existing suites pass unchanged with the shell in place, so no duplicate-text clash occurred.

## verify.sh
VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)
jest: Test Suites: 179 passed, 179 total | Tests: 1271 passed, 1271 total. build: 0 errors, 0 warnings. tsc clean.

## Deviations from the plan
1. Window-width mock: `jest.mock('react-native/Libraries/Utilities/useWindowDimensions')` worked as the plan said (default export replaced); no other module needed.
2. R1/R2 also mock the width to 1280 so "wide" is deterministic (plan says "wide").
3. S-tests use RNTL 14 async API (`await renderRouter/fireEvent`) and `toBeExpanded()/toBeCollapsed()` (jest-native matchers in repo) instead of `toHaveAccessibilityState`; same assertion.
4. Overlay layout: panel is on the left and the scrim is the remaining flex-1 Pressable beside it (not a full-screen scrim behind the panel). Both are labelled "Close menu" (two, as S5 requires).
5. Plan step 4 (manual browser check at 1280px/375px) was NOT done: no browser available to this agent. Rob/Nathaniel should check: sidebar wide, toggle + 16px gutter at 375, no horizontal scroll, header/back still readable on each admin screen.
6. NativeWind accepted the 8-digit hex `scrim` (build/export pass; Risk 4 not triggered).

## Round 1
Fixed: removed `flex-1` from the Sidebar root in `AdminShell.tsx:25` (width now holds beside the scrim).
Test: new S7 in `AdminShell.test.tsx` (overlay panel className has `w-sidebar`, no `flex-1`; nativewind emits no style in jest, so className is asserted). Red before: `Expected value: not "flex-1"; Received ["w-sidebar","max-w-full","flex-1",...]`. Green after: 4 suites / 18 tests pass.
Existing tests unchanged.
verify.sh changed: VERIFY PASS (secrets hygiene tsc jest export); jest 1273 passed.
