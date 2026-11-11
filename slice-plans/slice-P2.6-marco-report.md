Concerns: Two titles on wide screens (Stack header under new top bar), accepted per plan. One transient Jest 5 s timeout in tests/app/admin.test.tsx (F7b "signed out") during the first full verify under load; passes alone and in the second full run. If it recurs, it is a cold-start timeout, not a logic failure.
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: None. S1-S18 untouched. Added B1-B7 (breadcrumb.test.ts) and S19-S25 (AdminShell.test.tsx, helper renderShellAt).
Changed (slice P2.6, frontend/apps/store/features/admin-shell):
- breadcrumb.ts (new): breadcrumbFor + Crumb
- breadcrumb.test.ts (new)
- components/AdminShell.tsx: internal Breadcrumb; wide top bar above children; phone top bar is two rows
- components/AdminShell.test.tsx: S19-S25
- slice-plans/backlog.md: P2.6 manual 1280/390 px check line
Red first: `npx jest features/admin-shell` before implementing: Test Suites: 4 failed, 2 passed; Tests: 12 failed, 48 passed (breadcrumb module missing; getByLabelText('Breadcrumb') not found). After: 6 suites, 124 tests passed.
Theme check: git diff grep for hex/rgb/style= returned nothing.
Not eyeballed on :8081 (no browser in this run); geometry check deferred to backlog.
verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); jest 183 suites / 1383 tests passed.
Quality-bar §2: tests for new behaviour [x]; verify.sh green [x]; no secrets [x]; no backend/auth/money change [N/A, Low tier, no mutations]; tracker row not marked Done (coordinator, after Rob).
