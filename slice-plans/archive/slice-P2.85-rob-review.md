# Slice P2.85 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run this round (only run on a PASS round; your report quotes `VERIFY PASS`, jest 276 suites / 2149 tests)

The slice is small and follows the plan: `/account` reads only `useSession().user`, `RequireSignIn` moved cleanly with its 3 importers, both entries are `Link asChild` + `Pressable` with the old labels, and no `/auth/me` call exists anywhere in the new code. One test assertion is vacuous and leaves the Name row's main branch untested; that is the only blocker.

### 1. A2 does not prove the page shows the name — BLOCKING   (`frontend/apps/store/tests/account.test.tsx`, A2, the `${role} User` line)
**What I saw:** you assert `getAllByText(`${role} User`).length >= 1`. The storefront TopNav `AccountBar` already prints `{role} User` in the header on `/account`, so that count is at least 1 even if `AccountView` renders no Name row at all.
**Why it matters:** `AccountView` has one branch on the name: `user.displayName.trim() || 'Not set'`. A3 covers the `Not set` side. Nothing covers the named side: if the row were changed to always print `Not set`, or dropped, or printed the email, A2 and A3 would both stay green. That is the main thing the page is for.
**What I want:** make the name assertion count what only the page adds. Exact count: `expect(screen.getAllByText(`${role} User`)).toHaveLength(2); // top bar + the page's Name row`. Do the same check on the role line while you are there: for `Admin` the `>= 1` is also satisfied by the AccountBar's "Admin" link, so use the exact per-role count (Customer 1, Support 1, Admin 2 on `/account`; confirm by running it) or scope both lookups to the page with `within(...)`. Email `toHaveLength(1)` is already right.
**How I'll check:** I'll change `AccountView`'s Name value to `'Not set'` and expect A2 red, then restore and `cmp`.
**Lesson:** when a page repeats data already shown in the chrome, `>= 1` proves the chrome, not the page — count exactly or scope the query.

### 2. AdminShell S8/S9/S12 count changes — Accepted, not weakened
**What I saw:** 10→11, 4→5, 0→1, each with a `(P2.85)` comment; S8 title extended.
**Ruling:** justified. The footer group is now a real `link` role, rendered for any signed-in user, so each count goes up by exactly one; the per-link navigation checks in S8 and the name checks and hidden-section checks in S9 are untouched, and A6 proves the extra link is the account link and that it navigates (Admin and Support). S12's Customer harness case is not reachable in the app (AdminGate redirects), and `0→1` still proves no nav section leaks.

### 3. A2 email "exactly once" vs A3 "at least twice" — Consistent
**Ruling:** both are right about the data. In A2 the top bar shows the display name, so the email appears only in the page (exactly 1 — this assertion is the non-vacuous one). In A3 the name is blank, so the top bar falls back to the email; page + top bar = 2. A3's `>= 2` is not vacuous (the top bar alone gives 1). Tightening it to exactly 2 is a NOTE (backlog).

### 4. A4 presses the last "Log out" — NOTE
It is the page's button because the native stack renders the custom header before the screen content (`expo-router/build/react-navigation/native-stack/views/NativeStackView.native.js`, header `View` then `render()`). Correct, but implicit; a one-line comment is in the backlog.

**Other checks (no finding):** no `/auth/me` or other request from `AccountView` (A2 asserts 0 calls); `shownName` in `SidebarFooter` is the same rule as the removed inline code; the Log out buttons sit outside both link `Pressable`s, so no nested press; no new packages, no backend, no edits to `useLogoutMutation`, `session.ts`, `admin-access.ts`, `nav.ts`; my doc rows (app-context §5 `/account`, decision log "Account page data source") match the build. Code is plain and short: `AccountView` is one 3-row panel and a button, nothing extra.

**What was good:** A5 was green before the route existed and you noticed, then made it meaningful with the hung SecureStore read (same as `tests/app/admin.test.tsx`). And you flagged the count changes yourself instead of quietly editing them.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export; jest 2149 passed)

### 1. Vacuous A2 name/role assertions — Confirmed
I checked `tests/account.test.tsx` A2: name is now `toHaveLength(2)`, role `toHaveLength(role === 'Admin' ? 2 : 1)`, email stays exact. I re-ran your mutation on a scratch copy: `AccountView` Name value forced to 'Not set'. A2 went red for all three roles ("Expected length: 2"), 6 failed / 12 passed in the account file. I restored the file and `cmp` was identical.

No other test changed in this round. No open bugs.

**What was good:** exact counts tie the assertion to the two places the name renders, so a regression in either place fails.
