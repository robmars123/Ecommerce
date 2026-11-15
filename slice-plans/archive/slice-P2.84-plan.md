# Slice P2.84 — Admin logout in the sidebar footer

Tracker row: `P2.84 | Admin logout: "Log out" in the admin sidebar footer; revokes the refresh token via POST /api/v1/auth/logout, clears the session, goes to sign-in | admin-shell`.

**Read:** this plan · CLAUDE.md §2 code style, §3 · arch §10 refresh-token line (`docs/architecture/10-cross-cutting.md`, grep `Refresh token`) · then only these files:
`frontend/apps/store/features/admin-shell/components/AdminShell.tsx` (`SidebarFooter`, `Sidebar`), `features/admin-shell/components/AdminShell.test.tsx` (helpers at the top, `describe('AdminShell footer')`, S26), `features/auth/hooks/useLogoutMutation.ts`, `features/auth/components/AccountBar.tsx` + its test (the two "Log out" tests, as the pattern), `features/auth/components/AdminGate.tsx`, `features/auth/admin-access.ts`, `tests/admin-shell-data.test.tsx` (top 45 lines: the `renderRouter('./app')` set-up), `tests/fetch-mock.ts`, `tests/render-with-query.tsx`, `tests/session.ts`, `components/ActionButton.tsx`.

**Scope:** frontend only. No backend change, no new endpoint, no new package, no new hook, no change to `useLogoutMutation` or `session.ts`.

**Risk tier: High (reduced mutation set).** Justification: the slice adds no money, stock or server guard, but it is the admin's way to end an authenticated session on what may be a shared device. The auth rules it relies on (session ends locally even if the API call fails; every cached admin query is dropped; one request per press) are auth/session state transitions under CLAUDE.md §5, so Rob does a full review and runs the two mutations in §6. Nothing here is "read-only/layout", so Low does not fit.

## 1. What exists today (checked, not assumed)

- `useLogoutMutation` already does the right thing: `mutationFn` sends `logout(refreshTokenStorage.get())` and **swallows any error** (best effort); the hook-level `onSettled` always runs `endSession()` then `queryClient.clear()`. So a failed logout call still clears the session locally. Reuse as is.
- `endSession()` clears the access token and the stored refresh token (native), publishes `signed-out`, and calls the root `onSignedOut` (`app/_layout.tsx` also clears the query client).
- `AdminGate` (wraps every `/admin` page in `app/admin/_layout.tsx`) turns a `signed-out` session into `<Redirect href="/login?returnTo=<current path>">` via `adminRouteDecision(null, path)`.
- The storefront `TopNav`/`AccountBar` is not mounted on `/admin` (root Stack `admin` screen has `headerShown: false`), so there is no second "Log out" on admin pages.
- `SidebarFooter` is a single `accessible` View labelled `Signed in as <name>, <role>`; `accessible` collapses its children into one screen-reader element.
- `AdminShell.test.tsx` helpers `renderShell`, `renderShellWithRoutes`, `renderShellAt` render **without** a `QueryClientProvider` (S26 even says so). `useLogoutMutation` calls `useQueryClient()`, which throws without a provider.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Put the button in `SidebarFooter`, calling `useLogoutMutation()` there, using the existing `ActionButton` with `pending={logout.isPending}` | Row says sidebar footer; `SidebarFooter` only renders with a user, so the hook only mounts when there is a session; `ActionButton` already disables while pending (no double send) | A new `LogoutButton` component or a shared admin/storefront component: no second consumer in this slice |
| Restructure the footer: outer `View` (border-top, spacing) holds the existing `accessible` name/role group **and**, as a sibling, the `ActionButton` | A button inside an `accessible` View is swallowed into the group label on iOS/Android and cannot be focused or activated by a screen reader | Button inside the accessible group: not reachable with VoiceOver/TalkBack |
| Navigation after logout is done by `AdminGate`, not by the button: the session ends, the gate redirects to `/login?returnTo=<current admin path>` | Zero new navigation code; already tested gate behaviour. An explicit `router.replace` in a per-call `mutate(…, { onSettled })` is unreliable: the gate unmounts the shell as soon as `endSession()` publishes, and TanStack Query v5 drops per-call callbacks of an unmounted observer, so the result would depend on timing. `returnTo` is safe: `safeReturnTo` only keeps same-app paths and the gate re-checks the role after the next sign-in (Customer → `/`, Support on an Admin-only page → `/admin`) | `router.replace('/login')` before `mutate()`: the login screen would show for a moment while still signed in, and the hook's work runs after the page is gone — harder to reason about. Dropping `returnTo`: needs a new code path; see Nathaniel question in §7 |
| Narrow (phone) layout: no extra handling; the button is in the overlay sidebar, the gate redirect unmounts the shell | The overlay state dies with the shell | Closing the overlay before logout: no visible effect, extra code |
| Test helpers in `AdminShell.test.tsx` wrap their tree in `QueryClientProvider` (fresh `createTestQueryClient()` per render) | The footer now needs a query client, as it does in the real app (root layout provides one) | Making the hook optional / guarding `useQueryClient`: production code bent for tests |
| No decision-log row | UI-local choice inside an existing pattern (gate redirect); no architecture section governs admin navigation | — |

## 3. Files

| File | Change |
|---|---|
| `features/admin-shell/components/AdminShell.tsx` | `SidebarFooter`: import `ActionButton` and `useLogoutMutation`; outer View + accessible group (unchanged label and texts) + `<ActionButton label="Log out" onPress={() => logout.mutate()} pending={logout.isPending} />`. Nothing else in the file changes. |
| `features/admin-shell/components/AdminShell.test.tsx` | Helpers wrap in `QueryClientProvider`; S26 renamed (see §5 changed tests); new `describe('AdminShell logout')` O1–O3. |
| `tests/admin-logout.test.tsx` (new) | Whole-app flow O4–O7 with `renderRouter('./app', …)`, set up like `tests/admin-shell-data.test.tsx`. |

Module contract: none new. Uses `POST /api/v1/auth/logout` as it is (arch §9 auth row). No migration.

Code-style reminders: plain JSX, no wrapper helper for "logout and navigate", no new prop on `AdminShell`/`Sidebar`, keep the existing `name` fallback logic untouched. Keep the button full width only if `ActionButton` already allows it via the parent; do not add a new `ActionButton` variant.

## 4. Build order

1. Write `tests/admin-logout.test.tsx` O4–O6 first; run `npx jest tests/admin-logout.test.tsx | tail -n 20` and quote the red (no "Log out" button found).
2. Wrap the three helpers in `AdminShell.test.tsx` in `QueryClientProvider`; run the file — must stay green **before** touching production code (proves the wrapper changes nothing).
3. Change `SidebarFooter`. Add O1–O3 and O7. Re-run both files green.
4. `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line. Report the S26 rename under "changed tests".

## 5. Required tests (Jest; arch §11 — no numbered §11.2/F test is named for this row)

Test-first (red before code): **O4, O5, O6.**

`tests/admin-logout.test.tsx` — whole app, `signInAs('Admin')`, width 1024, `initialUrl: '/admin/orders'`, `mockFetchRoutes` for the dashboard, `dashboard/recent-orders`, `admin/orders` and `auth/logout` (204), `EXPO_PUBLIC_API_BASE_URL` set as in `admin-shell-data.test.tsx`; `afterEach` resets session and restores mocks.

- **O4 (test-first)** Press "Log out": exactly one call to `/api/v1/auth/logout`, method POST; `getSessionSnapshot().status === 'signed-out'`; `screen` has pathname `/login` (assert `returnTo` search param is `/admin/orders` via `toHaveSearchParams` or the URL the gate builds); "Admin navigation" is gone; a query seeded under an admin key before the press (e.g. the dashboard data) is `undefined` in the client afterwards. If seeding the root client is awkward, assert instead that the shell's dashboard data is not re-requested after logout (call count frozen) — pick one, state which.
- **O5 (test-first)** `it.each` over a rejected fetch (`TypeError('Network request failed')`) and a `500` problem response from `/auth/logout`: still `signed-out`, still lands on `/login`. This is the "logout call fails" rule: the session is cleared locally regardless.
- **O6 (test-first)** Double press while the logout request is held open (route returns a promise resolved later): one logout call only; after resolving, signed-out. Proves `pending` disables the button.
- **O7** Support at `/admin/orders`: the button is there and works the same (one call, signed-out, `/login`).

`AdminShell.test.tsx`, new `describe('AdminShell logout')`:

- **O1** Wide, Admin: a `Log out` button is inside "Admin navigation", and is **not** inside the `Signed in as Admin User, Admin` group (`within(group).queryByRole('button', …)` is null) — guards the accessibility split.
- **O2** Narrow: no `Log out` until the menu opens; present in the overlay after "Open menu".
- **O3** Signed out (no `signInAs`): no `Log out` button.

Changed tests (must be listed in Marco's report and reviewed):
- `renderShell`, `renderShellWithRoutes`, `renderShellAt`: wrapped in `QueryClientProvider`. No assertion changes.
- **S26** title "renders without a QueryClient" is no longer true. Rename to "S26: wide, the search input renders and sends no request until someone types" and add `expect(fetchSpy).not.toHaveBeenCalled()` with a `jest.spyOn(globalThis, 'fetch')` spy. That keeps its intent (search costs nothing on mount) and strengthens it.

No backend tests: the endpoint, its 401/400 paths and family revocation are covered since slice 1b and are not touched.

## 6. Mutations (Rob, review round; Marco quotes red for each in his report)

Scratch-copy, break, run `npx jest tests/admin-logout.test.tsx`, expect red, restore, `cmp`.
- **M1** In `useLogoutMutation.ts` `onSettled`, delete `await endSession();` → O4 and O5 must go red.
- **M2** In `useLogoutMutation.ts`, delete `queryClient.clear();` → O4 (cache assertion) must go red. If Marco chose the "call count frozen" variant in O4 and M2 stays green, O4 is too weak: switch to the seeded-cache assertion.

## 7. Risks and open points

- **Web: failed logout leaves the httpOnly refresh cookie.** If `POST /auth/logout` fails on web, the local session ends but the cookie and the server-side family stay valid; a page reload runs `restoreSession()` and signs the admin back in. Same behaviour as the storefront since slice 1b; not changed here. → `slice-plans/backlog.md` NOTE (candidate: show "Couldn't sign out on the server, try again" or retry). **Needs Nathaniel** only if he wants it fixed in this slice.
- **`returnTo` after a deliberate logout** (decision §2): the next person to sign in on that device lands on the previous admin page, still role-checked. **Nathaniel may prefer a plain `/login`**; that would need a small explicit-navigation path and is not planned.
- `ActionButton` is the filled primary style; in the sidebar it may look loud. Acceptable for this row; restyling is a separate tracker row, not a new variant here.
- Helper wrapping touches many existing AdminShell tests at once; run the file green before the production change (build order step 2) so any failure is attributable.
