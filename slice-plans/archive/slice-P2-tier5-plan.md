# Slice P2-tier5 — Dashboard tabs, static stories and criteria, Live store pill (tracker tier 5)

Rows: P2.18 · P2.19 · P2.20 · P2.9. Autonomous. Builder: **Marco, all files**.
**Risk tier: Low.** Frontend only: no endpoint, no backend, no auth or hub change, no money or stock. Marco's Jest tests + full `verify.sh`; Rob reads the diff. **No mutations.**
P2.9 is **in**: the hub (`OrderStatusHub`, `[Authorize]`, `/hubs/orders`) and `@microsoft/signalr` 10.0.11 (`lib/realtime.ts`) already exist. **No new package or service.**

## 1. Read

- `CLAUDE.md` (loaded) · quality-bar §1, §3, §4
- app context §4.13 (SignalR line), §4.14 (Shell, Dashboard additions)
- `docs/admin-dashboard-feature-tracker.md` (all rows: story sources, footer rules)
- `docs/admin-dashboard-css-values.md` §4 (`.view-tabs`, `.pill`, `.live-dot`), §8 (User Stories), §9 ("Live store" pill, Definition of Done bar, `.p-high`/`.p-med`), §10 (≤ 560 px tab rules)
- §13.2, grep only: `Admin UI sidebar nav`, `Admin dashboard tier 5` (the row this plan adds)
- Code: the files in §3, plus `lib/realtime.test.ts`, `tests/jest-setup.js`, `features/admin-dashboard/period.ts` (pattern for the tab parser)

## 2. Decisions made (open points resolved with the recommended option)

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | The tab lives in the URL: `/admin?tab=stories` / `/admin?tab=criteria`; missing or unknown → Overview. Pure `parseDashboardTab` like `parseDashboardPeriod`; switching uses `router.setParams({ tab: next === 'overview' ? undefined : next })`, so `period` is kept | Deep-linkable, back button works, same pattern as P2.14 period and P2.34 order filters; the nav item needs a URL | Local `useState` (nav cannot link to a tab); separate routes `/admin/stories` (needs a Support gate change in `admin-access.ts`) |
| D2 | `app/admin/index.tsx` renders a new `AdminDashboardScreen` component that reads `tab`: Overview → `<DashboardView tabs={<ViewTabs …/>} />`; Stories/Criteria → a static view (same "Admin dashboard" header + the same `ViewTabs` + the tab body). `DashboardView` gains **one optional prop `tabs?: ReactNode`**, rendered right after the page-head row (css §4 order). No other `DashboardView` change | Static tabs mount no dashboard queries; `DashboardView.test.tsx` and `DashboardInsights.test.tsx` stay **unchanged** (they render `/admin`, i.e. Overview; no text "Overview"/"User Stories"/"Acceptance Criteria" collides, they count only `alert` roles) | Tabs inside `DashboardView` with conditional bodies (hooks still fetch; bigger diff); tabs above the page head (wrong place per css §4) |
| D3 | Tab bar: `accessibilityRole="tablist"`, three `Pressable`s with `accessibilityRole="tab"` and `accessibilityState={{ selected }}`; labels `Overview`, `User Stories`, `Acceptance Criteria`, plain text (no count pill) | Smallest; screen-reader state for the active tab | Count pills on tabs or nav (no row asks; backlog) |
| D4 | One static data file `features/admin-dashboard/stories.ts`: `USER_STORIES: readonly UserStory[]` (`{ id, title, quote, priority: 'High' \| 'Medium', rows: string[], criteria: string[] }`) and `DEFINITION_OF_DONE: readonly string[]`. Both tabs read it, so they cannot drift | One source; testable invariants (ids, counts) | Copy in JSX; fetching from an API (row says static) |
| D5 | Story content is **derived only from app context §4.14 and the Done tracker rows** (no new claims); each story lists its source row IDs in `rows`, shown in the card foot. The eight stories are in §3.3 | "Code only from docs you've read"; the docs define no US-01–US-08 text | Inventing product stories; storefront stories |
| D6 | User Stories tab = hero (title, one line, 2 stats: stories count, criteria count, both computed from the array) + 2-column grid (1 column when `layout.narrow` from `dashboardLayout`) of story cards: id, priority chip, title, quote, foot (`rows` joined by " · " and "N criteria"). **No filter row** | Css §8 hero + card; filter row is not in P2.19 | Priority filter chips (backlog) |
| D7 | Acceptance Criteria tab = per story, a heading `US-0n · title` and its `criteria` as a list with the ✓ bullet (css §8 `.ac`), then the Definition of Done bar with `DEFINITION_OF_DONE` (tracker footer rules) | Css §8 `.ac` + §9 DoD bar; the criteria live here, not repeated on the story cards | AC repeated inside each story card |
| D8 | Nav: `User Stories` → `href: '/admin?tab=stories'`. The `ready` flag and the disabled "Soon" branch are **removed** (no item is unready any more; a dead branch is blocking). `visibleNav` gates on the path part only (`href.split('?')[0]`), so Support still sees it (gate unchanged) | Decision-log P2.3 row: "their row enables them"; minimal code | Keeping `ready` with all `true` (dead code); changing `adminRouteDecision` |
| D9 | Breadcrumb: tabs are views of one route, so every tab shows "Dashboard" (`usePathname()` has no query). `navCrumb` matches path-only hrefs; the stories item (has `?`) never becomes a crumb | No URL-derived labels (breadcrumb rule) | A "User Stories" crumb read from the query |
| D10 | P2.9 = `lib/realtime.ts` adds `subscribeToConnectionState(listener: (connected: boolean) => void): () => void`. It joins the **same shared connection** (opened by the first subscriber of either kind, stopped when both sets are empty). The listener is called at once with the current value, then `true` when `start()` resolves or `onreconnected`, `false` on `onreconnecting`, `onclose`, or a failed `start()`. Every handler ignores events from a connection that is no longer the current one | One socket per app (existing rule in `realtime.ts`); "up" means the socket is connected, nothing more | A second connection; polling a health endpoint (not what the row asks); a new server "ping" method |
| D11 | Pill = `features/admin-shell/components/LiveStorePill.tsx`, rendered in the top bar **only when `user` is set** (subscribes on mount, unsubscribes on unmount): wide → between search and `ThemeToggle`; narrow → in the first row before `ThemeToggle`. Text `Live store` with a dot; connected → `primary-subtle`/`primary-text` + dot `primary`; not connected → neutral pill, muted dot, text `Live store · offline`. `accessibilityLabel` "Live store connected" / "Live store offline". Initial state `false` | Not colour-only; signed-out shell opens no socket; theme tokens only (`primary` = #10b981 dark, matches `.live-dot`) | Three states with "Connecting…"; a hardcoded `#10b981` |
| D12 | P2.9 does **not** change the hub, JWT `access_token` handling, CORS or roles. An admin connection joins its own `user:{id}` group (existing hub rule) and receives nothing extra. Hence Low, not High | The auth surface is untouched; no state transition | Admin-only hub (new server code for a display pill) |

## 3. Files and contracts (all Marco; `frontend/apps/store`)

### 3.1 New

| File | Contract |
|---|---|
| `features/admin-dashboard/tabs.ts` | `type DashboardTab = 'overview' \| 'stories' \| 'criteria'`; `DASHBOARD_TABS: readonly { tab: DashboardTab; label: string }[]` (order Overview, User Stories, Acceptance Criteria); `parseDashboardTab(raw: string \| string[] \| undefined): DashboardTab` (array → first; unknown → `'overview'`) |
| `features/admin-dashboard/tabs.test.ts` | §5 |
| `features/admin-dashboard/stories.ts` | D4 types and data (§3.3); `storyStats(stories)` → `{ stories: number; criteria: number }` |
| `features/admin-dashboard/stories.test.ts` | §5 |
| `features/admin-dashboard/components/ViewTabs.tsx` | `ViewTabs({ tab, onChange })`, D3; css §4/§10 values via existing tokens |
| `features/admin-dashboard/components/UserStoriesTab.tsx` | D6 |
| `features/admin-dashboard/components/AcceptanceCriteriaTab.tsx` | D7 |
| `features/admin-dashboard/components/AdminDashboardScreen.tsx` | D2; reads `tab` with `useLocalSearchParams`, owns `setParams` |
| `features/admin-dashboard/components/DashboardTabs.test.tsx` | §5 |
| `features/admin-shell/components/LiveStorePill.tsx` | D11; `useEffect(() => subscribeToConnectionState(setConnected), [])` |
| `features/admin-shell/components/LiveStorePill.test.tsx` | §5 |

### 3.2 Changed

| File | Change |
|---|---|
| `app/admin/index.tsx` | Render `AdminDashboardScreen` instead of `DashboardView` (route stays thin) |
| `features/admin-dashboard/components/DashboardView.tsx` | Optional `tabs?: ReactNode` prop, rendered after the page-head row. Nothing else |
| `features/admin-shell/nav.ts` | D8: drop `ready` from `NavItem` and items; stories href; `visibleNav` gates on the path part |
| `features/admin-shell/breadcrumb.ts` | `navCrumb` drops the `i.ready &&` test (D9) |
| `features/admin-shell/components/AdminShell.tsx` | Drop the disabled "Soon" branch (every item is a link); render `LiveStorePill` (D11) |
| `lib/realtime.ts` | D10. Keep `subscribeToOrderStatus` behaviour and signature identical |
| `tests/jest-setup.js` | Global `@/lib/realtime` mock adds `subscribeToConnectionState: jest.fn(() => () => undefined)` (else every shell test throws) |
| `lib/realtime.test.ts` | Mock `build()` gains `onreconnecting` and `onclose` capturing handlers (mock only); new `describe('subscribeToConnectionState')`. Existing `it` blocks unchanged |
| `features/admin-shell/nav.test.ts` | N1 expected table: no `ready`, stories href. N2 unchanged |
| `features/admin-shell/breadcrumb.test.ts` | B7: iterate items whose href has no `?` (was: `ready`); add B8 (§5) |
| `features/admin-shell/components/AdminShell.test.tsx` | `readyItems` → all items; S8: 10 links, pathname compared to the path part and, for the stories link, `toHaveSearchParams({ tab: 'stories' })`; S9: 4 links incl. User Stories; **S10 deleted** (the disabled branch it tested is removed by D8); add S-pill tests (§5) |
| `docs/admin-dashboard-feature-tracker.md` | P2.18, P2.19, P2.20, P2.9 → Done when tests pass |

Changed-test justification (for review): N1, B7, S8, S9, S10 assert the `ready`/"Soon" behaviour that D8 removes on purpose (the P2.3 decision row says the row enables the item). No assertion about role gating is loosened: S9 still proves Support sees no admin-only item.

### 3.3 Story content (D5) — Marco writes the quote and 2–4 criteria per story from the cited rows only

| id | title | priority | rows |
|---|---|---|---|
| US-01 | Admin shell and role-based navigation | High | P2.2, P2.3, P2.4, P2.6, P2.46, P2.47 |
| US-02 | Find an order, product or SKU from anywhere | Medium | P2.7, P2.8 |
| US-03 | See today's or the period's figures at a glance | High | P2.13, P2.14, P2.15, P2.16, P2.21, P2.22, P2.23 |
| US-04 | See orders by status and open the filtered list | High | P2.27, P2.28, P2.34, P2.10 |
| US-05 | Revenue trend, analytics and CSV export (Admin) | Medium | P2.25, P2.26, P2.42, P2.17 |
| US-06 | Act on low stock and items that need attention | High | P2.24, P2.31, P2.11, P2.12 |
| US-07 | Read-only store settings (Admin) | Medium | P2.43, P2.44 |
| US-08 | Support sees only what Support may | High | P2.45, P2.32 |

Criteria are observable behaviour already shipped (e.g. "Support gets 403 on export, trend and settings"); no secrets, keys or domains. `DEFINITION_OF_DONE` = the tracker footer rules (endpoint: happy, 401/403, 400 · every screen: Jest · restyle rows: existing tests unchanged · theme tokens only).

### 3.4 Styling

Theme tokens and existing classes only; no new hex or token. `.p-high` → `danger-subtle`/`danger-border`/`danger`; `.p-med` → `warning-subtle`/`warning-border`/`warning` (dark text tones differ slightly from css §9; accepted, backlog). ≤ 560 px tab sizes per css §10; no horizontal scroll at 390 px (P2.46 rule).

## 4. Build order

1. `realtime.ts` connection state: write the §5 R-tests first (R5 stale-connection test **test-first**, quote red), then code; run `lib/realtime.test.ts` — existing 6 tests still green.
2. `jest-setup.js` mock line.
3. `LiveStorePill` + AdminShell wiring + its tests.
4. Nav/breadcrumb `ready` removal and test updates (N1, B7, B8, S8, S9, S10).
5. `tabs.ts`, `stories.ts` + unit tests.
6. `ViewTabs`, `UserStoriesTab`, `AcceptanceCriteriaTab`, `AdminDashboardScreen`, `DashboardView` prop, `index.tsx` + `DashboardTabs.test.tsx`.
7. Confirm `git diff --stat` shows **no** change to `DashboardView.test.tsx` / `DashboardInsights.test.tsx`; full `bash scripts/verify.sh 2>&1 | tail -n 30`; tracker rows → Done.

## 5. Required tests (T = test-first). No backend tests: no endpoint added

`lib/realtime.test.ts` — `describe('subscribeToConnectionState')`
- R1 listener gets `false` at once, then `true` after `start()` resolves.
- R2 `onreconnecting` → `false`; `onreconnected` → `true`, and an order-status subscriber still gets `null` (existing contract).
- R3 `onclose` → `false`; a failed `start()` → `false` (no throw).
- R4 one order subscriber + one state subscriber → **one** connection; removing the order subscriber does not stop it; removing the state subscriber then calls `stop()` once.
- R5 **T** stale events: subscribe, unsubscribe, subscribe again (second connection started and `true`); firing the **first** connection's `onclose` leaves the listener at `true`.
- R6 no API base URL → listener gets `false`, no connection built.

`LiveStorePill.test.tsx`
- P1 renders "Live store connected" when the captured listener gets `true`, "Live store offline" for `false` (text not colour-only).
- P2 unmount calls the unsubscribe function.

`AdminShell.test.tsx` (added)
- S13 signed-in Admin at 1024 and at 390: the pill is in the top bar.
- S14 signed out: no pill and `subscribeToConnectionState` not called.

`breadcrumb.test.ts`: B8 `/admin` (any tab) → `[Dashboard]`.

`tabs.test.ts`: T1 `undefined`/`''`/`'x'` → overview; `'stories'`, `'criteria'`, `['criteria','x']` parsed; T2 `DASHBOARD_TABS` labels and order.

`stories.test.ts`: S1 ids are exactly `US-01`…`US-08` in order; S2 every story has a title, quote, ≥ 1 row id matching `/^P2\.\d+$/` and ≥ 2 criteria; S3 no story cites the same row twice and no tier-5 or tier-C row (P2.9, P2.18–P2.20, P2.48–P2.55) is cited (only shipped work); S4 `storyStats` totals; S5 `DEFINITION_OF_DONE` non-empty.

`DashboardTabs.test.tsx` (renders `app/admin/index` with the router, fetch mocked like `DashboardView.test.tsx`)
- DT1 `/admin` → Overview tab `selected`, dashboard figures render.
- DT2 pressing "User Stories" → URL `tab=stories`, 8 story cards (US-01…US-08), hero stats "8" and the criteria count; **no** `/api/v1/admin/dashboard` fetch after the switch from a fresh `/admin?tab=stories` render.
- DT3 `/admin?tab=criteria` → every story heading and its criteria; the DoD bar text.
- DT4 `/admin?period=7d` → press User Stories → Overview: `period=7d` is kept.
- DT5 `/admin?tab=bogus` → Overview.
- DT6 Support on `/admin?tab=stories` sees the stories (static, no role data).
- DT7 at 390 px the stories render in one column (no horizontal scroll), using `dashboardLayout`.

Unchanged and green: `DashboardView.test.tsx`, `DashboardInsights.test.tsx`, existing `realtime.test.ts` its, `useOrderLiveStatus.test.tsx`, N2.

## 6. Mutations

None (Low tier, CLAUDE.md §5). Rob reads the diff and checks R5 and S14 by reading them.

## 7. Risks

- **Stale-connection race (D10):** `stop()` of the old socket fires `onclose` after a new one is up; without the guard the pill shows offline while connected. R5 covers it.
- **Shared socket regression:** the refactor of start/stop must not change when customer order screens open or close the socket. Existing realtime tests + `useOrderLiveStatus` tests must pass unchanged.
- **Initial start failure:** `withAutomaticReconnect` does not retry a failed first `start()`, so the pill stays offline until the shell remounts. Accepted (the pill is a hint; backlog).
- **Logout:** the pill unmounts with the signed-in top bar, which releases the socket; if the shell keeps the pill mounted across logout, S14 catches it.
- **Token in URL:** unchanged path; logging stays `LogLevel.None` (arch 10.1).
- **Static copy drift:** stories cite row ids; S3 blocks citing unshipped rows. Copy must not claim behaviour not shipped; Rob checks each criterion against its rows in review.
