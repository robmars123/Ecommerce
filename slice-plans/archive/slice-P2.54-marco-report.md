# Slice P2.54 — Marco report

Concerns: None
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: none changed. Appended `describe('totalsNote')` (U1, U2) to status-labels.test.ts (plus one import name); new OrderDetailView.totals-note.test.tsx (J1, J2 x7, J3 x2). Existing tests untouched.

Red (test-first): U1/U2 failed before implementation, "totalsNote" not a function (U1 x8 statuses, U2). J tests were written before the render change, in the same run as U; J1 not-found output was not separately quoted (head-truncated), note not shown until OrderDetailView was changed.

Changed:
- frontend/apps/store/features/orders/status-labels.ts (totalsNote, Cancelled-only)
- frontend/apps/store/features/orders/components/OrderDetailView.tsx (import, `note` in Content, Text after Total row)
- frontend/apps/store/features/orders/status-labels.test.ts, components/OrderDetailView.totals-note.test.tsx
- docs/admin-dashboard-feature-tracker.md (P2.54 Done)
Prettier --write applied to the touched files (first verify failed on format only).

verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 268 suites / 2039 tests; features/orders 246 green unchanged.

Quality-bar §2: tests for new behaviour ✔; no secrets ✔; no backend/money/stock change (N/A rules); tokens only, no console ✔; verify.sh green ✔.
