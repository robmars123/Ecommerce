# Slice P2.3 — Sidebar nav, hidden by role (plan)

Tracker row P2.3 (`docs/admin-dashboard-feature-tracker.md`), module `admin-shell`. Slice P2 (arch §12.3). Builds on P2.1–P2.2 (shell already in `app/admin/_layout.tsx` → `features/admin-shell/components/AdminShell.tsx`).

**Out of scope:** P2.4 footer (name/role), P2.6 breadcrumb, P2.10/P2.11 badges, active-item highlight, the User Stories tab (P2.18/P2.19), `/admin/analytics` (P2.42), `/admin/settings` (P2.43). No new route files, no API, no package.

## 1. Read

- `CLAUDE.md` (already loaded) · arch §5.1, §5.3, §5.5 (`docs/architecture/05-frontend.md`) · §11.3 (`11-testing.md`, F7 row only)
- App context §2 (roles table), §4.14 (Shell bullet), §5 (routes table) — `docs/ecommerce-app-context.md`
- Decision log rows "Admin theme tokens and shell (tracker P2.1–P2.2" and "Admin UI sidebar nav (tracker P2.3" — `grep -n "P2\.[123]" docs/architecture/13.2-decision-log.md`
- Code: `features/admin-shell/components/AdminShell.tsx` + `.test.tsx`, `features/auth/admin-access.ts` (read only), `features/auth/hooks/useSession.ts`, `tests/session.ts`, `lib/design-tokens.js` (token names only)

## 2. Risk tier

**Low** (CLAUDE.md §5): no endpoint, no data fetch, no money/stock/state transition. Visibility reuses the existing, already-tested `adminRouteDecision` (D2), so no new authorization rule is introduced; the API stays the only authority (arch §3.4 rule 11, §5.3) and `AdminGate` still redirects a direct URL. Review = Marco's tests + `verify.sh` + Rob reads the diff; no mutations.
**Tier escalates to High** if the diff touches `features/auth/admin-access.ts`, `AdminGate.tsx`, or any backend file — Marco must not do that in this slice; if it seems necessary, stop and report.

## 3. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | One nav table `ADMIN_NAV` in `features/admin-shell/nav.ts`: 3 groups (Overview, Manage, Insights) of items `{ label, href, ready }`, in the tracker's order | Single source for groups/order; pure, unit-testable | Hard-coding JSX per item (untestable order, repeats the role check 10 times) |
| D2 | An item is visible iff `adminRouteDecision(role, item.href) === null`; a group with no visible items is not rendered (Support sees no Insights heading) | Nav and the route gate cannot disagree: one role table (`admin-access.ts`) already encodes app context §5. Hidden = the gate would redirect anyway | A second role list per item (`roles: ['Admin','Support']`) — duplicates the gate and can drift |
| D3 | Resulting visibility — Admin: all 10. Support: Dashboard, User Stories, Orders, Customers. Customer/null: nothing | App context §2 + §5: Support reads orders/customers/dashboard; Products, Categories, Inventory, Returns, Analytics, Settings are Admin-only. User Stories is a dashboard tab (§4.14), so it follows `/admin` (Admin, Support) | Hiding User Stories from Support — the dashboard (its host) is Admin+Support and the content is static |
| D4 | Items whose screen does not exist yet — **User Stories** (P2.19), **Analytics** (P2.42), **Settings** (P2.43) — render with `ready: false`: shown, not a link, `accessibilityState={{ disabled: true }}`, muted text plus a "Soon" caption. Their row flips `ready` to `true` (User Stories then links to the dashboard tab, decided in P2.18) | Row P2.3 names all 10 items, so the sidebar matches the target; linking to a missing route lands on Expo Router's unmatched screen (a broken link); role hiding still applies (Support never sees Analytics/Settings) | (a) link now to 404s; (b) omit the three until their rows — sidebar would not match P2.3; (c) stub route files — new screens are out of scope (rows P2.42/43) |
| D5 | `href` for User Stories is `/admin` for now (visibility only; not navigable while `ready: false`) | Visibility needs a path the gate understands; the tab's query/URL is P2.18's decision | Inventing `?tab=user-stories` now — no consumer |
| D6 | `AdminShell` reads the role itself via `useSession()`; `_layout.tsx` unchanged | Same pattern as `OrderActions` (`features/admin-orders`); the shell always sits inside `AdminGate`, so the session is settled | Passing `role` as a prop from the layout — extra wiring for one consumer |
| D7 | Group list wrapped in a `ScrollView` inside the sidebar (brand row stays fixed) | 3 headings + 10 × 44 px touch rows exceed a short phone's height in the overlay; items must stay reachable | Shrinking touch targets below `min-h-touch` |
| D8 | Group headings are `Text` with `accessibilityRole="header"`; every ready item keeps the P2.2 `Link asChild` + `Pressable` (role `link`, `min-h-touch`) and calls `onNavigate` so the overlay closes | Screen readers get structure; reuses P2.2's tested close behaviour | A section list component — no consumer beyond this |
| D9 | No active-item highlight in this slice | Not in row P2.3; breadcrumb (P2.6) covers "where am I" | Adding `usePathname` styling now — unplanned feature |

Decision-log row appended by Rob (see §8).

## 4. Files and contracts

| File | Change |
|---|---|
| `frontend/apps/store/features/admin-shell/nav.ts` | **New.** Exports `NavItem = { label: string; href: string; ready: boolean }`, `NavGroup = { title: string; items: NavItem[] }`, `ADMIN_NAV: readonly NavGroup[]`, and `visibleNav(role: UserRole \| null): NavGroup[]` (pure; D2). Imports `adminRouteDecision` from `@/features/auth/admin-access` and `UserRole` type from `@/features/auth/types` |
| `frontend/apps/store/features/admin-shell/nav.test.ts` | **New.** Tests N1–N4 |
| `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` | `Sidebar` gets `groups: NavGroup[]` (computed once in `AdminShell` from `useSession()` → `visibleNav(role)`), renders headings + items per D4/D7/D8. Brand, close button, breakpoint logic, top bar unchanged. Tokens/classes only from §5.5 (`text-muted`, `text-caption`, `text-foreground`, `min-h-touch`, spacing tokens) — no raw hex/px |
| `frontend/apps/store/features/admin-shell/components/AdminShell.test.tsx` | Setup change for S1–S7 (see §6) + new S8–S12 |

`ADMIN_NAV` exact content (labels and hrefs are part of the contract; N1 pins it):

- Overview: Dashboard `/admin` ready · User Stories `/admin` **not ready**
- Manage: Orders `/admin/orders` · Products `/admin/products` · Categories `/admin/categories` · Inventory `/admin/inventory` · Returns `/admin/returns` · Customers `/admin/customers` — all ready (route files exist under `app/admin/`)
- Insights: Analytics `/admin/analytics` **not ready** · Settings `/admin/settings` **not ready**

`href` typing: expo-router typed routes may reject `/admin/analytics` (no file yet). Keep `href: string` in `NavItem` and cast only at the `<Link href>` site of ready items (the existing `AdminGate` uses `as never` the same way). Not-ready items never reach `<Link>`.

No migrations, no backend, no `package.json` change.

## 5. Build order

1. `nav.test.ts` N1–N4 first → run, quote red (module missing) in the report.
2. `nav.ts` → N1–N4 green.
3. Update `AdminShell.test.tsx` setup (§6, existing tests) → S1–S7 still green against the **old** shell (proves setup-only change), then add S8–S12 → quote red.
4. `AdminShell.tsx` → all green.
5. `bash scripts/verify.sh 2>&1 | tail -n 30` — full run, quote the `VERIFY` line.
6. Manual check (report one line each): web at 1024 px as Admin and Support, 390 px overlay scrolls to Settings; dark mode only (light is P2.47).

## 6. Required tests (Jest; no §11.2 numbered tests are named by row P2.3 — F7 coverage is extended by N2/S9)

Test-first: N1–N4 and S8–S10 (role visibility is the slice's guard).

**`nav.test.ts` (pure)**
- **N1** `ADMIN_NAV` deep-equals the table in §4 (titles, order, labels, hrefs, `ready`).
- **N2** `visibleNav('Support')` deep-equals Overview[Dashboard, User Stories], Manage[Orders, Customers]; no Insights group.
- **N3** `visibleNav('Admin')` returns all 3 groups with all 10 items.
- **N4** `visibleNav('Customer')` and `visibleNav(null)` return `[]`.

**`AdminShell.test.tsx`** (wide = 1024 unless stated; use `signInAs` from `@/tests/session`, `resetSession` in `afterEach`, `jest.mock('expo-secure-store')` as in `AdminGate.test.tsx`; register `admin/orders`, `admin/products`, `admin/categories`, `admin/inventory`, `admin/returns`, `admin/customers` as stub routes where navigation is asserted)
- **S8** Admin: headers Overview, Manage, Insights (role `header`, in that order); 7 enabled links; pressing each ready link → `toHavePathname(href)` (table-driven).
- **S9** Support: links Dashboard, Orders, Customers only; text User Stories present; no Products, Categories, Inventory, Returns, Analytics, Settings text; no Insights header.
- **S10** Admin: User Stories, Analytics, Settings are disabled (`toBeDisabled()`), not role `link`, and pressing one leaves the pathname unchanged.
- **S11** Narrow (390): open menu → press Orders → pathname `/admin/orders` and the overlay is closed.
- **S12** Customer session (defensive, shell outside gate): no nav items and no group headers render; brand still renders.

**Changed existing tests (justify in report):** S1–S7 gain only session setup (`beforeEach(() => signInAs('Admin'))`, `afterEach(resetSession)`, secure-store mock) because the shell now reads the role; every assertion line stays byte-identical. Rob will diff those lines — any assertion change is BLOCKING.

`features/auth/admin-access.test.ts` and `AdminGate.test.tsx` must be unchanged.

## 7. Risks

| Risk | Mitigation |
|---|---|
| Typed routes reject not-yet-existing hrefs → `tsc` fails | `href: string` + cast at the ready-item `Link` only (§4) |
| Session store bleeds between tests (module singleton) | `resetSession()` in `afterEach`; S12 signs in as Customer explicitly |
| Duplicate "Close menu"/link names break S3–S5 queries | Nav items must not use "Close menu" or "Open menu" labels; S5's `toHaveLength(2)` stays valid |
| Someone reads "hidden" as protected | Comment in `nav.ts` one line: UI only, API enforces (arch §3.4 rule 11) |
| Overlay content taller than the screen | D7 `ScrollView`; manual check at 390 px |

## 8. Doc upkeep (Rob, done with this plan)

Appended one row to `docs/architecture/13.2-decision-log.md`: "Admin UI sidebar nav (tracker P2.3, Rob)" — D2 (visibility from `adminRouteDecision`) and D4 (not-ready items shown disabled until P2.19/P2.42/P2.43). Marco marks tracker row P2.3 Done only after Rob's PASS.
