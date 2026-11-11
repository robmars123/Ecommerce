# Slice P2.4 — Sidebar footer: name and role (`admin-shell`)

Tracker row P2.4 (`docs/admin-dashboard-feature-tracker.md`). Arch §12.3 row "P2 Admin UI (Phase 2)". App context §4.14 (Shell).
Mode: Autonomous. **Risk tier: Low** (CLAUDE.md §5) — display-only, no endpoint, no token/claims change, no auth rule added or changed. Review = Marco's tests + `verify.sh` + Rob reads the diff; no mutations.

## Read:

- This plan.
- `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` and `AdminShell.test.tsx` (S1–S12).
- `frontend/apps/store/features/auth/types.ts` (`SessionUser`), `frontend/apps/store/tests/session.ts` (`signInAs`, `makeTokens`).
- `docs/quality-bar.md` §1, §3, §4.
- Nothing else. No backend files.

## Where the name comes from (finding)

The session already carries it. `SessionUser = { id, email, displayName, role }` arrives in every `TokenResponse` (login, register, refresh, restore) and is published in the session snapshot; `AdminShell` already reads `useSession().user` for the role (P2.3). On the server, `User.Create` trims the display name and rejects 0 or >100 characters (`User.MaxDisplayNameLength = 100`), for both customers and the bootstrap admin. So: **no new endpoint, no token change, no auth work.**

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| Name = `user.displayName` from `useSession()`; role = `user.role` | Already in the session snapshot; zero new data paths | `GET /me` endpoint or a name claim in the JWT — new auth surface for data we already have |
| Blank-name fallback: if `displayName.trim()` is empty, show `user.email` | The server forbids it today, but the client must never render an empty identity line; email is already in the snapshot | A placeholder string such as "Unknown" (hides who is signed in); hiding the footer (loses the role) |
| Role shown as the raw value (`Admin`, `Support`) | Values are already human-readable; no mapping table needed | A label map — an abstraction with one identity mapping |
| No footer when `user` is null (restoring / signed out) | Nothing true to show; the admin layout guard handles access | A skeleton or "Signed out" text — unplanned UI |
| Long names: single line, ellipsis (`numberOfLines={1}`) on both lines; sidebar width stays `w-sidebar` | Names may be 100 chars; the sidebar is fixed at 240 px and must not grow or wrap into the nav | Truncating the string in JS (breaks screen-reader text and the email fallback) |
| Footer lives inside `Sidebar`, below the `ScrollView`, so it appears in the wide sidebar and in the phone overlay, and **not** in the phone top bar | One component, one placement; collapsing behaviour comes for free from P2.2 | A second copy in the phone top bar (crowds the bar; P2.6/P2.9 will add breadcrumb and pill there) |
| Footer is one accessible group: `accessible` + `accessibilityLabel={`Signed in as ${name}, ${role}`}` | Screen readers read it as one item; gives tests a stable query without a `testID` | Two separate readable texts; a `testID` |
| Pin footer to the bottom: `ScrollView` gets `className="flex-1"`, Sidebar root gets `h-full` (not `flex-1`) | `flex-1` on the root would break the overlay width (guarded by S7); `h-full` fills height in both the wide column and the overlay row | `flex-1` on the root (fails S7); leaving the footer directly under the nav (not a footer) |
| No decision-log row | Pure presentation of existing session data; no design gap in the docs | — |

## Files

| File | Change |
|---|---|
| `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` | Add internal `SidebarFooter({ user }: { user: SessionUser })`; `Sidebar` takes `user: SessionUser \| null` and renders the footer when non-null; pass `user` from `AdminShell` in both branches; root `h-full`, `ScrollView` `flex-1`. Import `SessionUser` type from `@/features/auth/types`. |
| `frontend/apps/store/features/admin-shell/components/AdminShell.test.tsx` | Add S13–S18 below. S1–S12 unchanged. |
| `docs/admin-dashboard-feature-tracker.md` | Row P2.4 → Done only after Rob's PASS. |

No new files, packages, tokens, hooks, or endpoints. No backend change.

### `SidebarFooter` contract

- Computes `name = user.displayName.trim() || user.email` (one expression, no helper module).
- Renders a `View` with `border-t border-border pt-md` holding two `Text`s: name (`text-body font-bold text-foreground`, `numberOfLines={1}`) and role (`text-caption text-muted`, `numberOfLines={1}`).
- Uses only existing theme classes, so light and dark both work (P2.1 tokens).
- No press behaviour (sign-out redesign is out of scope).

## Build order

1. Write S13–S18 first; run `npx jest features/admin-shell` and quote the red output in the report (they fail: no footer exists).
2. Implement `SidebarFooter` and the `Sidebar`/`AdminShell` wiring.
3. Green: `npx jest features/admin-shell | tail -n 20`; confirm S1–S12 still pass untouched (S7 in particular).
4. Full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line → `slice-P2.4-marco-report.md` (≤ 6 KB).

## Required tests (Jest, `AdminShell.test.tsx`) — all test-first

Use `signInAs(role, { user: { ... } })` from `tests/session.ts` for custom names.

| ID | Test |
|---|---|
| S13 | Wide, Admin: footer shows `Admin User` and `Admin`; `getByLabelText('Signed in as Admin User, Admin')` exists inside `Admin navigation`. |
| S14 | Wide, Support: footer shows the Support display name and role `Support`; no `Admin` role text. |
| S15 | Long name (100 chars): the full string is rendered in a `Text` with `numberOfLines === 1`; the role `Text` also has `numberOfLines === 1`; the `Admin navigation` panel still has class `w-sidebar` and not `flex-1`. |
| S16 | Blank name (`'   '`) and empty name (`''`) (two cases, `it.each`): the footer shows the email; the accessible label is `Signed in as <email>, Admin`; no empty or whitespace-only name `Text` is rendered. |
| S17 | Narrow (phone width): collapsed → display name not rendered anywhere (top bar shows only Menu and `Store Admin`); press `Open menu` → footer name and role visible; press `Close menu` → gone again. |
| S18 | No user (`resetSession()` without `signInAs`, wide): no `Signed in as` label, no footer texts; brand and `Body` still render. |

Existing S1–S12 must pass unchanged; any edit to them is a reviewed change and must be justified in the report.

## Risks

- **Layout pin on web vs native:** `h-full` on the Sidebar root relies on the parent having a height (wide wrapper stretches in the `flex-row` root; overlay is `absolute inset-0 flex-row`). Jest cannot check geometry; Marco checks the web export (`verify.sh` export step) and states in the report whether he eyeballed it on :8081. If it does not pin, leave the footer under the nav rather than adding `flex-1` to the root (S7).
- **Accessible group hides inner texts on native screen readers** — intended; the group label carries name and role.
- **Session updates:** a refresh that returns a changed name re-renders via `useSession`; no caching added, so nothing stale.
- **Scope creep:** no sign-out button, avatar/initials, badge, or breadcrumb in this slice.
