# Slice P2.85 — Account page (plan, Rob)

Tracker row (`docs/admin-dashboard-feature-tracker.md`, Nathaniel 2026-11-15): "P2.85 | Account page for every signed-in user (Customer, Support, Admin): name, email and role, with a log out button; reached from the top-bar avatar and the admin sidebar footer. Signed-out users go to sign-in | `account`, Identity | Todo".

Scope: **read-only**. No profile edit, no password change (separate row). **Frontend only, no backend change.**

**Read:** this plan · `docs/architecture/05-frontend.md` §5.2, §5.4, §5.5 · `docs/architecture/11-testing.md` §11.3 · `docs/ecommerce-app-context.md` §5 (the `/account` row) · decision-log row "Account page data source (P2.85, Rob)" (grep `P2.85` in `docs/architecture/13.2-decision-log.md`) · then only the files named in "Files" below and their existing tests. No §12.3 row (Phase 2 tracker row).

Risk tier (CLAUDE.md §5): **Low**. No endpoint, no server rule, no money/stock/payment, no new state transition. The gate is UI-only and the page shows only the caller's own in-memory session user. Logout reuses `useLogoutMutation` unchanged (already tested in P2.84 and `AccountBar.test.tsx`). Review: Marco's tests + full `verify.sh`, Rob reads the diff. **No mutations.**

---

## 1. What the client already knows

- `features/auth/types.ts`: `SessionUser = { id, email, displayName, role }`; `TokenResponse.user` arrives on login, register and every refresh (`session.ts` `startSession` publishes it). `useSession()` exposes it.
- Backend already has `GET /api/v1/auth/me` (`AuthEndpoints.cs`, `RequireAuthorization`), unused by the frontend (`queryKeys.auth.me` exists, no consumer).
- So name, email and role are already in the session. **No new endpoint, and the page does not call `/auth/me`.**

## 2. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | Data from `useSession().user` only; no request on the page | Same fields as `/auth/me`; zero round-trips; role/name changes reach the client on the next refresh (no profile edit exists to make them stale sooner) | Query `GET /auth/me` on mount (extra round-trip, loading/error states, same data); a new endpoint |
| D2 | Route `app/account.tsx` in the **root** Stack (storefront `TopNav` header), one page for all three roles | One page, one test path; Support's `/admin` allow-list (`admin-access.ts`) stays unchanged; staff go back with the existing "Admin" link in `AccountBar` | `app/admin/account.tsx` inside the admin shell (customers can't use it, needs `adminRouteDecision` widened for Support, two pages) |
| D3 | Gate with existing `RequireSignIn`; **move** it from `features/orders/components/` to `features/auth/components/` and update its 3 importers (`app/checkout.tsx`, `app/orders/index.tsx`, `app/orders/[id].tsx`) | It is auth gating with now 4 consumers across 3 features; `account` must not import from `orders` | Import from `features/orders` (wrong owner); a copy (duplication) |
| D4 | Feature folder `features/account/components/AccountView.tsx` (tracker module `account`) | Matches tracker; §5.1 feature layout | Putting the view in `features/auth` (auth = session mechanics, not a screen) |
| D5 | Top-bar entry: in `AccountBar`, the existing "Signed in as {name}" group (avatar + name) becomes a `Link href="/account" asChild` `Pressable` with `accessibilityRole="link"`, **same `accessibilityLabel`**, `min-h-touch`. Log out button stays in the bar | The avatar is the entry the row names; keeping the label leaves T5–T7 untouched | A separate "Account" text link (extra chrome); renaming the label (changes tests for no behaviour) |
| D6 | Admin entry: in `AdminShell` `SidebarFooter`, the name/role group becomes a `Link href="/account" asChild` `Pressable` (`accessibilityRole="link"`, **same label** `Signed in as {name}, {role}`, `min-h-touch`). Replace the inline `user.displayName.trim() \|\| user.email` with `shownName(user)` from `features/auth/avatar-initial` (same rule, one place). Log out button stays | Row names the sidebar footer; S13–S18 keep passing unchanged | Adding an "Account" item to `nav.ts` (nav is admin sections; `/account` is not one) |
| D7 | Page content: `ScreenTitle` "Account"; one `Panel` with three label/value rows — Name (`displayName.trim()`, or `Not set` when blank), Email, Role (wire value `Customer`/`Support`/`Admin`, as the sidebar footer shows); then `ActionButton` "Log out" using `useLogoutMutation()` with `pending={logout.isPending}` | Plain, reuses existing components; blank names exist (P2.55 avatar rule handles them) | Showing `shownName` in the Name row (would print the email twice); role display labels map (one consumer, wire values already readable) |
| D8 | After Log out on `/account`, `RequireSignIn` redirects to `/login?returnTo=/account` (no extra navigation code) | Row: "signed-out users go to sign-in"; same mechanism as `/orders`; `safeReturnTo` accepts `/account` | An explicit `router.replace('/login')` in the button handler (extra code, races the gate's redirect) |
| D9 | `AccountView` guards `if (!user) return null;` with a one-line comment (RequireSignIn guarantees a user; the null render covers the instant between `endSession` and the redirect) | Type-safe without a non-null assertion | `user!` (hides the logout frame) |

No new packages. No backend, migration or contract change.

## 3. Files

New
- `frontend/apps/store/app/account.tsx` — `<RequireSignIn returnTo="/account"><AccountView /></RequireSignIn>` (same shape as `app/orders/index.tsx`).
- `frontend/apps/store/features/account/components/AccountView.tsx` — D7, D9. Uses `useSession`, `useLogoutMutation`, `ScreenTitle`, `Panel`, `ActionButton`. Page padding/width as `OrderListView` does (copy its outer container classes, no new tokens).
- `frontend/apps/store/features/auth/components/RequireSignIn.tsx` — moved verbatim (D3); update its doc comment to say "Pages for a signed-in user" instead of "Orders".
- `frontend/apps/store/tests/account.test.tsx` — tests A1–A6 below (full router, `renderRouter('./app', …)`, pattern of `tests/admin-logout.test.tsx`).

Changed
- `features/orders/components/RequireSignIn.tsx` — deleted (moved).
- `app/checkout.tsx`, `app/orders/index.tsx`, `app/orders/[id].tsx` — import path only.
- `features/auth/components/AccountBar.tsx` — D5.
- `features/admin-shell/components/AdminShell.tsx` — D6 (`SidebarFooter` only).
- `features/auth/components/AccountBar.test.tsx` — add A7 (additions only).
- `tests/routes.test.tsx` — add row `['account.tsx', '/account', 'Account', true, 'Customer']` (the route-table test enumerates `app/`; addition only).
- `docs/ecommerce-app-context.md` §5 and decision log — **already done by Rob**, don't edit.

Do not touch: `useLogoutMutation`, `session.ts`, `admin-access.ts`, `nav.ts`, any backend file.

## 4. Build order

1. Move `RequireSignIn` (D3), fix the 3 imports; run `npx jest tests/routes.test.tsx features/orders` — green, nothing else changed.
2. Write `tests/account.test.tsx` A1–A6 and the A7 test, plus the routes row; run them **red** (route missing / not a link) and quote the red lines in the report.
3. `app/account.tsx` + `AccountView` → A1–A5 green.
4. `AccountBar` link (D5) → A7 green, T5–T7 still green unchanged.
5. `SidebarFooter` link + `shownName` (D6) → A6 green, `AdminShell.test.tsx` S13–S18 green unchanged.
6. Full `bash scripts/verify.sh 2>&1 | tail -n 30`.

## 5. Required tests (Jest; test-first = **TF**)

All in `tests/account.test.tsx` unless stated. Mock fetch with `mockFetchRoutes` (`tests/fetch-mock.ts`); stub the cart route for Customer (the TopNav badge fetches it) and the admin dashboard routes where the shell renders.

| ID | Test | TF |
|---|---|---|
| A1 | Signed out (`endSession()` before render, so status is not `restoring`): `/account` → pathname `/login` with `returnTo=/account`; no "Account" header, no email rendered | **TF** |
| A2 | `it.each(['Customer','Support','Admin'])`: signed in as role → header "Account", the name `{Role} User`, email `{role}@example.test` and role text are shown; pathname stays `/account` (Support/Admin are **not** redirected); **no fetch to `/api/v1/auth/me`** (D1) | **TF** |
| A3 | Blank `displayName` (`signInAs('Customer', { user: { …, displayName: '  ' } })`): Name row shows `Not set`, email shown | |
| A4 | Log out from the page: one `POST /api/v1/auth/logout`, `getSessionSnapshot().status === 'signed-out'`, pathname `/login` | **TF** |
| A5 | While restoring (`resetSession()` leaves status `restoring`): `/account` shows the loading state, does not redirect | |
| A6 | Admin sidebar footer (width 1024, mock `useWindowDimensions` as `admin-logout.test.tsx` does): at `/admin/orders`, for **Admin and Support**, the link named `Signed in as {Role} User, {Role}` exists with role `link`; pressing it lands on `/account` showing the "Account" header | **TF** |
| A7 | In `AccountBar.test.tsx`: signed-in Customer, the `Signed in as Customer User` element has role `link`; pressing it navigates to `/account` (add an `account` route stub to the `renderRouterWithQuery` map) | **TF** |
| R1 | `routes.test.tsx` row for `account.tsx` (renders under the real root layout as Customer) | |

Existing tests that must stay **unchanged and green**: `AccountBar.test.tsx` T5–T7 and logout tests, `AdminShell.test.tsx` S13–S18, `tests/admin-logout.test.tsx` O1–O4, `tests/routes.test.tsx` existing rows. Any edit to an existing assertion must be listed in the report with the reason (reviewed as a possibly weakened test).

Backend: none (no endpoint added or changed, so no integration tests).

## 6. Risks

- **Nested Pressables:** the `AccountBar`/footer link wraps only the avatar + name group, never the Log out button. Pressing Log out must not navigate to `/account` (A4 and existing logout tests catch it).
- **Phone admin menu:** the footer link lives in the overlay; navigating to `/account` leaves the admin layout, so the overlay unmounts — no `onNavigate` plumbing needed. Don't add any.
- **Logout redirect ordering:** `endSession` publishes signed-out before `queryClient.clear()`; the page may render once with `user === null` (D9 guard). Don't add effects or timers.
- **Test flake:** full-router tests load the whole `app/` tree; if A2/A6 exceed 5 s under parallel load use `jest.setTimeout(15_000)` with the same comment as `routes.test.tsx`, nothing else.
- **Backlog interplay:** P2.84 note about `returnTo=/admin` after admin logout is unchanged by this slice; `/account` logout yields `returnTo=/account` (D8), consistent with `/orders`.

## 7. Done when

A1–A7 + R1 green, all listed existing tests unchanged, full `verify.sh` passes (quote the `VERIFY` line), report ≤ 6 KB at `slice-plans/slice-P2.85-marco-report.md` listing every changed file and any changed test.
