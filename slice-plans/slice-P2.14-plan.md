# Slice P2.14 (+ P2.15, P2.16) — Period filter in the URL · Refresh keeps old figures on error · as-of line

Tracker rows (`docs/admin-dashboard-feature-tracker.md`, tier 2 "MVP: dashboard `/admin` essentials"): **P2.14** "Period filter: Today / 7 days / 30 days", **P2.15** "Refresh button; keeps old figures on error", **P2.16** "As-of line: time, period, currency, lag". All `admin-dashboard`. Builder: **Marco**. Mode: Autonomous. **Frontend only** (`frontend/apps/store`). No §12.3 row and no §11.2 numbered test (Phase 2 gap-fill). Conventions carried from `slice-plans/archive/slice-P2.21-plan.md` and `slice-P2.30-plan.md`: `AdminPanel`, theme-token classes only, flex-wrap, P2.46 phone-width guard, existing tests byte-identical.

**Read:** CLAUDE.md is loaded · app context §4.8 (`docs/ecommerce-app-context.md`, `grep -n "^### 4.8"`, ~14 lines) · arch §8.8.3 (`08.8-eventual-consistency.md`, lines 19–23) · arch §5.5 (`05-frontend.md`, `grep -n "^### 5.5"`) · arch §11.3 (`11-testing.md`, `grep -n "^### 11.3"`) · decision log: `grep -n "P2.14\|URL contract" docs/architecture/13.2-decision-log.md` (two rows; the P2.14 row is written for this slice) · quality-bar §2 and §4 · this plan · code: `features/admin-dashboard/{components/DashboardView.tsx, components/DashboardView.test.tsx, hooks/hooks.ts, types.ts}`, `tests/admin-dashboard-fixtures.ts`, `tests/render-with-query.tsx`, `tests/fetch-mock.ts` (`jsonResponse`, `problemResponse`, `calledUrls`), `features/admin-orders/order-list-params.ts` (the URL-parse pattern to copy), `features/admin-orders/components/AdminOrderListView.tsx` lines 24–70 (`useLocalSearchParams` + `router.setParams`), `features/admin-orders/components/AdminOrderListView.test.tsx` lines 140–185 (`toHaveSearchParams`, `act` + `router.setParams`), `components/ActionButton.tsx`, `tests/admin-phone-width-guard.test.ts` (read only).

## What already exists (checked in code, not assumed)

| Row | Already there | Gap this slice closes |
|---|---|---|
| P2.14 | Chips Today / 7 days / 30 days (`PERIODS`, `DashboardView.tsx:21–25, 170–181`); `useDashboardQuery(period)` with `keepPreviousData`; period sent as `?period=` (`api.ts`) | Period is `useState` only: lost on reload, back, or a shared link. Not in the URL like the orders list |
| P2.15 | `ActionButton label="Refresh"` → `query.refetch()`, `pending={query.isFetching}` (`:182`) | On a failed refresh TanStack keeps `data` and sets `isError`, but the view takes the `data ?` branch (`:185`), so the failure is **silent**. No test pins either half of the rule |
| P2.16 | `Figures as of <asOf> UTC` (`:187`, `formatAsOf` `:27–37`) | No period, currency or lag on the line |

**Backend (P2.14 check):** `GET /api/v1/admin/dashboard` already takes `period` — `AdminDashboardController.Get` → `DashboardPeriod.Parse` (`Ecommerce.Admin/Application/DashboardKpis.cs:8–14`: `null|""|today`, `7d`, `30d`, else `null`) → `ValidationProblem` 400 with `errors.period`. Already integration-tested in `AdminDashboardApiTests.cs`: 401/403 `Dashboard_without_a_token_is_401_and_as_a_Customer_is_403`, happy `Dashboard_for_an_Admin_has_the_servers_KPIs_…` and `Dashboard_accepts_the_7d_and_30d_periods`, 400 `Dashboard_with_an_unknown_period_is_400` (`yesterday`, `1d`, `7D`). Period window uses `IClock` (`DashboardQueryService.GetAsync`, `period.From(now)` = UTC days). **The API does not change.**

**Response fields for P2.16:** `asOf` = min projector `AsOf` or now (arch §8.8.3, `DashboardQueryService.cs:67`); `period` = `period.Name`; `currency` = `DashboardSettings.Currency` (`Store:Currency`); lag = `needsAttention.laggingProjectors[]` (`{ name, lagSeconds }`), **only projectors beyond the 60 s target** (`ProjectorHealth.Lagging`). The response has no lag for healthy projectors and no target value — D5 words around that rather than adding a field.

## Risk tier — Low

Read-only endpoint, no API change, no money/stock/payment/auth/domain state transition. P2.15 is a **client display-state rule** (keep figures, show the failure); it gets component tests but is not a guard. No security or validation guard is added (the 400 guard exists and is tested), so **no §3 mutations**. Review = Low tier (CLAUDE.md §5): Marco's tests + full `verify.sh`, Rob reads the diff. Marco runs two code-level self-checks (build order 3 and 4), quoted but not counted as mutations.

## Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | **Period lives in the URL:** `/admin?period=7d` or `30d`; `today` = no param. Read with `useLocalSearchParams<{ period?: string \| string[] }>()` and `parseDashboardPeriod`; write with `useRouter().setParams({ period: next === 'today' ? undefined : next })` | Same pattern as the orders list URL contract (decision log "Admin order list URL contract"): reload, back and a shared link keep the view | Keeping `useState` (row asks for a filter that survives navigation, like orders); a `today` value in the URL (two URLs for one view) |
| D2 | **`parseDashboardPeriod(raw)`**: exact, case-sensitive `today`/`7d`/`30d` when `raw` is a string; anything else (missing, `''`, `7D`, `1y`, an array) → `'today'`. The client never sends an unknown value; the server's 400 stays the guard for other callers | Mirrors the server's `Parse` and `parseOrderListParams`; a bad link degrades to the default instead of an error screen | Showing a 400 error for a hand-edited URL; case-insensitive matching (server is case-sensitive, `7D` → 400) |
| D3 | New file `features/admin-dashboard/period.ts` exports `PERIODS` (moved verbatim from `DashboardView.tsx`) and `parseDashboardPeriod`. `DashboardView` imports both | One list for chips, the parser and the as-of label (D5); pure and unit-testable like `order-list-params.ts` | Parser inline in the view (not unit-testable); in `types.ts` (types only) |
| D4 | **Refresh failure keeps the figures and says so.** When `query.data && query.isError`, render above the figures (first child of the `data` block, before the as-of row) one `Text accessibilityRole="alert" className="text-small text-danger"` with `` `Refresh failed: ${userMessage(query.error)} Showing the last figures.` `` The as-of row stays, so its time shows how old the kept figures are. The notice disappears on the next success (`isError` false). Refresh stays dashboard-only and keeps `pending={query.isFetching}` | TanStack v5 keeps `data` on a refetch error (verified: `queryObserver.js` uses placeholder only while `status === 'pending'`); only the notice is missing | `ErrorState` inside the block (second `Try again` button; Refresh already is the retry); a toast; Refresh also refetching recent orders (it has its own pager and loading/error state; no row asks) |
| D5 | **A failed period switch shows the existing `ErrorState`, not the other period's figures.** No code needed: the new key has no data and placeholder data is dropped on error, so `data` is undefined. Pinned by test R3 | Old figures under a new chip would be wrong figures, not old ones | Caching the last successful dashboard in component state to keep it on any error |
| D6 | **As-of line = four parts, all from the response:** pure `asOfParts(d: Dashboard)` in new `features/admin-dashboard/as-of.ts` returns `{ time, period, currency, lag, lagging }`: `time` = `` `Figures as of ${formatAsOf(d.asOf)} UTC` `` (`formatAsOf` **moved verbatim** from the view; output byte-identical); `period` = `` `Period: ${label}` `` with `label` from `PERIODS` for `d.period` (`Today`, `7 days`, `30 days`); `currency` = `` `Currency: ${d.currency}` `` (code as sent, e.g. `USD`); `lag` = `Lag: within target` when `laggingProjectors` is empty, else `` `Lag: ${max} s behind` `` with `max` = largest `lagSeconds`; `lagging` = boolean | Every field is the server's; the client only formats (time) and picks the largest of server integers (not money). Period follows `data.period`, so while `keepPreviousData` shows the old period during a switch, the line names the period the figures are for (same rule as P2.21 D3) | Client lag from `Date.now() - asOf` (device clock skew); a new `lagSeconds`/target field in the API (not needed for "within target"/"N s behind"); device-local time (server periods are UTC days; frozen test pins UTC) |
| D7 | **Render:** replace the single as-of `Text` (`DashboardView.tsx:187`) with `View className="flex-row flex-wrap gap-sm"` holding four `Text`s in order time · period · currency · lag. Classes `text-small text-muted`; the lag `Text` uses `text-small text-warning` when `lagging`. **No `accessibilityRole="alert"`** on the line (Needs attention already raises each lagging projector as an alert; the frozen test counts alerts) | Separate `Text`s keep the frozen string `Figures as of Nov 8, 2026, 14:30 UTC` matchable by `getByText` and wrap at 375 px (P2.46). Labels avoid clashing with chip text `Today` | One concatenated string (breaks the frozen first test); `·` separators as extra `Text`s (noise for screen readers) |
| D8 | Chip row, Refresh placement, KPI cards, banner, panels, quick actions unchanged. Query key stays `queryKeys.admin.dashboard(period)` | Other rows own them | — |

## Files

| File | Row | Change |
|---|---|---|
| `frontend/apps/store/features/admin-dashboard/period.ts` | P2.14 | **New.** `PERIODS` (moved), `parseDashboardPeriod(raw: string \| string[] \| undefined): DashboardPeriod` per D2. One-line doc comment citing the decision-log row |
| `frontend/apps/store/features/admin-dashboard/period.test.ts` | P2.14 | **New.** P1–P2 |
| `frontend/apps/store/features/admin-dashboard/as-of.ts` | P2.16 | **New.** `formatAsOf` (moved verbatim, now not exported unless the test needs it — it doesn't), `asOfParts` per D6 |
| `frontend/apps/store/features/admin-dashboard/as-of.test.ts` | P2.16 | **New.** A1–A4, cases built with `makeDashboard({...})` |
| `frontend/apps/store/features/admin-dashboard/components/DashboardView.tsx` | all three | Remove `PERIODS` and `formatAsOf` (moved). `useState<DashboardPeriod>` → D1 (`useLocalSearchParams`, `useRouter` from `expo-router`; keep `useState` import — `RecentOrders` uses it). Chip `onPress` → `choosePeriod(p.value)` doing D1's `setParams`. D4 notice. D7 as-of row. Nothing else |
| `frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx` | all three | **Append only:** one new `describe('DashboardView period, refresh and as-of (P2.14–P2.16)', …)` at the end of the file with its own `renderAt(url)` helper (same route map as `render`) and tests U1–U3, R1–R3, V1–V3. No existing line changes |

`docs/architecture/13.2-decision-log.md`: one row appended by Rob ("Admin dashboard period, refresh, as-of line"). Tracker rows are marked Done by Rob/coordinator after PASS.

## Freeze list

No edits: `backend/**` (no API change; the period endpoint and its tests are reused as they are); `features/admin-dashboard/{api.ts, api.test.ts, types.ts, hooks/**, kpi-cards.ts, kpi-cards.test.ts}`; `lib/**` including `query-keys.ts`, `api-client.ts`, `query-client.ts`, `design-tokens.js`; `tailwind.config.js`; `components/**` (`AdminPanel`, `ActionButton`, `ErrorState` unchanged); `tests/**` including `admin-dashboard-fixtures.ts` (build cases inline with `makeDashboard({...})`), `render-with-query.tsx`, `fetch-mock.ts`, `admin-phone-width*.ts(x)`, `routes.test.tsx`; `app/**` (`app/admin/index.tsx` already renders `DashboardView`); `features/admin-orders/**`, `features/auth/**`, `features/admin-shell/**`; `package.json` and lock files (no package); `docs/**` except Rob's row; `.github/`, `.claude/`.

**Every existing test stays byte-identical**, in particular in `DashboardView.test.tsx`: the first test (`Figures as of Nov 8, 2026, 14:30 UTC`), `asks the server again when the period changes and when Refresh is pressed` (now goes through the URL; must still pass unchanged), `shows the error with a retry that loads the dashboard`, `raises dead letters and a lagging projector as alerts…` (alert count), `D3 words the period from the response, not the selected chip` (`Today` chip selected with no URL param). If a frozen test fails, fix the code; if you believe the test is wrong, stop and report it. Styling: RN primitives and token classes only; no `style=`, hex or `[...]` values. No visible string changes except D4's notice and D6's three new parts.

## Required tests

All new. **(TF)** = write first, run, quote the red line, then implement.

`period.test.ts`
- **P1 (TF)** `parseDashboardPeriod('today' | '7d' | '30d')` returns the same value (`it.each`). Red: module missing.
- **P2 (TF)** `undefined`, `''`, `'7D'`, `'1y'`, `'yesterday'`, `['7d', '30d']` → `'today'` (`it.each`).

`as-of.test.ts`
- **A1 (TF)** default `makeDashboard()` → `{ time: 'Figures as of Nov 8, 2026, 14:30 UTC', period: 'Period: Today', currency: 'Currency: USD', lag: 'Lag: within target', lagging: false }` (`toEqual`).
- **A2 (TF)** `laggingProjectors: [{ name: 'orders-dashboard', lagSeconds: 75 }, { name: 'sales', lagSeconds: 130 }]` → `lag: 'Lag: 130 s behind'`, `lagging: true` (max, not first, not sum).
- **A3** `period: '7d'` → `'Period: 7 days'`; `'30d'` → `'Period: 30 days'` (`it.each`).
- **A4** `currency: 'EUR'` → `'Currency: EUR'` (passed through, not looked up).

`DashboardView.test.tsx` (appended `describe`; use `mockApi` pattern inline or the file's `mockApi` — it is in module scope, so reuse it)
- **U1 (TF)** `renderAt('/admin?period=7d')`, Admin → first `/admin/dashboard?` call contains `period=7d`; `getByRole('button', { name: '7 days' })` `toBeSelected()`. Red today (state starts at `today`).
- **U2 (TF)** `renderAt('/admin')` → press `30 days` → `expect(screen).toHaveSearchParams({ period: '30d' })` and a call with `period=30d`; press `Today` → `toHaveSearchParams({})`. Red today.
- **U3** `renderAt('/admin?period=1y')` → after `findByText('$1,234.50')`, no called URL contains `period=1y`, the first contains `period=today`, `Today` is selected. (Proves D2 at the screen; may pass once U1 is implemented — it guards the fallback.)
- **R1 (TF)** keeps old figures on a failed refresh: `let fail = false`; fetch mock returns `makeDashboard()` or `problemResponse(500, { title: 'Server error' })` for the dashboard, `makeRecentOrdersPage([])` for recent orders. After `findByText('$1,234.50')`, set `fail = true`, wait until Refresh is enabled, press it → `findByRole('alert')` whose text starts with `Refresh failed:` (use `findByText(/^Refresh failed: .* Showing the last figures\.$/)`); `$1,234.50` and `Figures as of Nov 8, 2026, 14:30 UTC` still on screen; `queryByRole('button', { name: 'Try again' })` is null. Red today (figures stay but no notice).
- **R2** continue the R1 shape: after the notice, set `fail = false` and return `makeDashboard({ kpis: { ...makeDashboard().kpis!, revenue: { amount: '1300.00', currency: 'USD' } } })`; press Refresh → `findByText('$1,300.00')`; `queryByText(/^Refresh failed:/)` is null.
- **R3** a failed period switch never shows the other period's figures: dashboard mock returns 200 for `period=today` and 500 for `period=30d`. After `findByText('$1,234.50')`, press `30 days` → `findAllByRole('button', { name: 'Try again' })`; `queryByText('$1,234.50')` is null; `queryByText(/^Refresh failed:/)` is null.
- **V1 (TF)** default fixture → `findByText('Period: Today')`, `getByText('Currency: USD')`, `getByText('Lag: within target')`, and `getByText('Figures as of Nov 8, 2026, 14:30 UTC')`. Red today.
- **V2** `makeDashboard({ needsAttention: { pendingReturns: 0, deadLetters: [], laggingProjectors: [{ name: 'orders-dashboard', lagSeconds: 130 }] } })` → `findByText('Lag: 130 s behind')`.
- **V3** `makeDashboard({ period: '30d' })` with `renderAt('/admin')` → `findByText('Period: 30 days')` while `Today` chip `toBeSelected()` (the line follows the response, not the chip).

Must stay green unchanged: the five frozen tests; `tests/admin-phone-width-guard.test.ts` PG1–PG3; `tests/admin-phone-width.test.tsx` PW1; backend `AdminDashboardApiTests` (via `verify.sh`).

**Mutations:** none (no guard added; CLAUDE.md §3 "no extras").

## Build order

1. `AGENT_STATUS.md` → Marco BUSY. Read the `Read:` list. Baseline: `cd frontend/apps/store && npx jest features/admin-dashboard tests/admin-phone-width 2>&1 | tail -n 20`; quote the pass count.
2. **P2.14 test-first:** write `period.test.ts` (P1–P2) and append U1–U3 (with `renderAt`). Run `npx jest features/admin-dashboard 2>&1 | tail -n 20` → P1, P2, U1, U2 red; quote. Create `period.ts` (D2, D3), wire D1 in the view → green. Frozen `asks the server again…` and `D3 words the period…` still green.
3. **P2.15 test-first:** append R1–R3; run → R1 red; quote. Implement D4 → green. Self-check: temporarily change the notice condition to `false`, confirm R1 red, restore; quote the red line (not a mutation).
4. **P2.16 test-first:** write `as-of.test.ts` (A1–A4) and append V1–V3; run → A1, A2, V1 red; quote. Create `as-of.ts` (D6, `formatAsOf` moved verbatim), render D7 → green. Self-check: temporarily make `lag` use the first entry instead of the max, confirm A2 red, restore; quote.
5. `npx jest features/admin-dashboard tests/admin-phone-width 2>&1 | tail -n 20` green; `npx tsc --noEmit 2>&1 | tail -n 20` clean; `npm run format` in `frontend/apps/store`.
6. `git -c safe.directory='*' diff -- frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx` → additions only (no `-` lines). `git -c safe.directory='*' diff --stat -- backend` → nothing. `grep -n "useState<DashboardPeriod>\|formatAsOf\|const PERIODS" frontend/apps/store/features/admin-dashboard/components/DashboardView.tsx` → nothing (moved, not duplicated).
7. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line. Fix and re-run until green.
8. Write `slice-plans/slice-P2.14-marco-report.md` (≤ 6 KB): VERIFY line; baseline and final jest counts; red quotes for steps 2–4 and both self-checks; files changed; confirmation that no existing test changed and `backend/` is untouched; manual checks below marked "pending Nathaniel". Marco AVAILABLE.

## Review method (Rob)

Low tier, one review. `git diff --stat` + untracked; read `DashboardView.tsx`, `period.ts`, `as-of.ts` and their tests, and the `DashboardView.test.tsx` diff. Checks:
- `backend/` diff empty; no package, token or `lib/` change.
- `DashboardView.test.tsx` diff is additions only; the five frozen tests byte-identical.
- D1/D2: period read only via `parseDashboardPeriod`; `today` writes `undefined`; no `useState` for period remains; nothing but the parsed value reaches `useDashboardQuery`.
- D4: notice only when `data && isError`; `ErrorState` branch unchanged; R3 proves no cross-period figures.
- D6/D7: every part from `data.*`; no `Date.now()`/`new Date()` other than inside the moved `formatAsOf`; no money arithmetic; no `alert` role on the as-of row; `formatAsOf` body byte-identical to the removed one.
- Classes from tokens only (`flex-wrap` on the new row); no `style=`, hex, `[...]`. No dead code (`PERIODS` and `formatAsOf` exist once).
Full `verify.sh` in the PASS round only; quote the `VERIFY` line. No Rob mutations (Low tier).

## Manual checks (Nathaniel; Marco lists them as pending)

1. `/admin` at 375 px, light and dark: the as-of row wraps; lag shows in warning colour when behind; no horizontal scroll.
2. Pick 7 days, reload the browser tab → still 7 days; browser Back after switching restores the previous period.
3. Stop the API, press Refresh → figures stay, "Refresh failed: …" shows; start the API, Refresh → notice clears.

## Risks

- **History entries:** `router.setParams` on web replaces vs pushes per expo-router version; tests assert search params only, not history depth. Manual check 2 covers Back; if Back does not restore, report it — do not switch to `router.push` without a plan change.
- **`findByRole('alert')` ambiguity:** with `laggingProjectors` or dead letters in a fixture there are several alerts; R1 uses the default fixture (none) and matches the notice by text.
- **Refresh enabled timing:** `pending={query.isFetching}` disables Refresh during the period fetch; R1/R2 must `waitFor(... toBeEnabled())` before pressing, as the frozen period test does.
- **Decision log is ~58 KB** (over the ~10 KB file guide); splitting it is out of scope here and logged to the backlog.
