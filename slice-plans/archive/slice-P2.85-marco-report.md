Concerns:
- Plan said S8-S18 and T5-T7 stay unchanged. The footer link (D6, role `link`) is a new link, so three AdminShell count assertions had to change (see Tests changed). Plan gap, not a design flaw.
- The Account page also renders under the storefront TopNav, whose AccountBar shows the same name/email, so A2/A3 use `getAllByText` for role/name/email (Admin link text also matches "Admin"). Email-count assertion in A2 is exactly 1 (only the page prints it for a named user).

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed (possibly weakened, please review):
- `features/admin-shell/components/AdminShell.test.tsx` S8: link count 10 -> 11; S9: 4 -> 5; S12: 0 -> 1. Reason: the new footer account link (D6) is a link role element rendered whenever a user is signed in (S12 Customer session still has a footer). Each has a comment "(P2.85)". S8 title extended with "plus the account link". Nothing else in those tests touched; per-link navigation checks and S9 name checks unchanged.
- `features/auth/components/AccountBar.test.tsx`: additions only (A7, `account` stub route in `render`, `Text` import). T5-T7 untouched and green.
- `tests/routes.test.tsx`: added row for `account.tsx` only.

Test-first red (before app/account.tsx, AccountBar and AdminShell edits): 22 failed / 74 passed over account, AccountBar, routes tests. Key lines:
- A7: "Unable to find an element with role: link, name: Signed in as Customer User"
- A1: "Expected: /login, Received: /account" (route absent, no gate)
- A2/A3/A4/A6, route table and R1: failed (account.tsx missing; footer not a link)
- A5 was green before the page existed (no route), then made meaningful by mirroring tests/app/admin.test.tsx (hung SecureStore read keeps status `restoring`; asserts the Loading label and pathname `/account`).
After implementation: account.test.tsx, AccountBar, AdminShell, routes, admin-logout, orders: all green.

Changed (slice P2.85, frontend only):
New: `app/account.tsx`, `features/account/components/AccountView.tsx`, `tests/account.test.tsx`
Moved: `features/orders/components/RequireSignIn.tsx` -> `features/auth/components/RequireSignIn.tsx` (comment reworded only)
Edited: `app/checkout.tsx`, `app/orders/index.tsx`, `app/orders/[id].tsx` (import path); `features/auth/components/AccountBar.tsx` (D5 Link+Pressable, same label); `features/admin-shell/components/AdminShell.tsx` (D6 Link+Pressable, `shownName`); tests above.
Untouched by me: docs (Rob's edits), backend, useLogoutMutation, session.ts, admin-access.ts, nav.ts.

verify.sh:
VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)
jest: Test Suites: 276 passed, 276 total | Tests: 2149 passed, 2149 total. build 0 warnings; backend tests all green.

Quality-bar §2:
- Tests for new behaviour: [x] A1-A7, R1
- Verify gate passes: [x]
- No secrets / no new packages: [x]
- Backend, endpoint integration tests, mutations: N/A (frontend only, Low tier per plan)
- Rules §3.4: [x] no money/stock/payment touched, no cross-module import (account imports auth; orders no longer owns RequireSignIn)
- No `var`/C# changes: N/A

## Fix round 1
Blocking: A2 name/role assertions were vacuous (`>= 1`).
Fixed in `frontend/apps/store/tests/account.test.tsx` A2: name `toHaveLength(2)` (top bar + page); role `toHaveLength(role === 'Admin' ? 2 : 1)` (Admin has an extra bar link). Counts verified by running.
Proof: AccountView Name value set to literal 'Not set' (scratch copy) -> A2 red for Customer, Support, Admin ("Expected length: 2 / Received length: 1"). Restored, `cmp` identical, A2 green.
Jest account file: 18 passed. `verify.sh changed`: VERIFY PASS (secrets hygiene format tsc jest export; jest 2149 passed).
