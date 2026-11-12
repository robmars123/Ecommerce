# Slice P2.21 (+ P2.29, P2.31) — Dashboard KPI cards · recent orders and low stock restyle

Tracker rows (`docs/admin-dashboard-feature-tracker.md`, tier 2 "MVP: dashboard `/admin` essentials"): **P2.21** "KPI cards with secondary lines; never blank. Support sees no revenue", **P2.29** "Recent orders table restyle", **P2.31** "Low stock panel restyle". All `admin-dashboard`. One slice by owner request; nothing outside these rows. Builder: **Marco**. Mode: Autonomous. Frontend only (`frontend/apps/store`). No §12.3 row and no §11.2 numbered test (Phase 2 gap-fill). Conventions carried from `slice-plans/archive/slice-P2.36-plan.md`, `slice-P2.37-plan.md`, `slice-P2.47-plan.md` (token classes only, `AdminPanel`, flex-wrap, P2.46 guard, no copy changes on restyle rows).

**Read:** CLAUDE.md is loaded · app context §4.8 (`docs/ecommerce-app-context.md`, `grep -n "^### 4.8"`, ~14 lines) · arch §5.5 (`05-frontend.md`, `grep -n "^### 5.5"`) · arch §11.3 (`11-testing.md`, `grep -n "^### 11.3"`) · decision log: `grep -n "P2.21" docs/architecture/13.2-decision-log.md` (one row, written for this slice) · quality-bar §2 and §4 · this plan · code: `features/admin-dashboard/{types.ts, components/DashboardView.tsx, components/DashboardView.test.tsx}`, `tests/admin-dashboard-fixtures.ts`, `components/AdminPanel.tsx`, `lib/money.ts` (`formatMoney` only), `features/admin-orders/components/AdminOrderListView.tsx` lines 105–135 (row class to copy), `tests/admin-phone-width-guard.test.ts` (rules PG1–PG3, read only).

## Risk tier — Low

Read-only screen; no endpoint, auth rule, money computation, stock or state transition changes. **The API does not change.** I checked the auth rule the row names:

- Server: `AdminDashboardController.Get` (`backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminDashboardController.cs:66`) passes `User.IsInRole(IdentityRoles.Admin)` as `includeKpis`; Support gets `kpis: null` (6b D19, arch §9 dashboard row).
- Integration test already exists: `AdminDashboardApiTests.Dashboard_for_an_Admin_has_the_servers_KPIs_and_for_Support_has_kpis_null` (asserts `kpis` null and that the revenue amount is absent from Support's whole body); 401/403/400 tests also exist (`Dashboard_without_a_token_is_401_and_as_a_Customer_is_403`, `Dashboard_with_an_unknown_period_is_400`).
- Client: renders KPIs only when the server sent them (`data.kpis ? … : null`); it never reads the role. This slice keeps that.

So the rule was added and tested in Slice 6b; nothing new to test on the server. Because P2.21's acceptance names the rule, Marco runs **one evidence mutation (M1)** on the existing guard and quotes red; it is the only mutation and is not repeated by Rob. Review = Low tier (CLAUDE.md §5): diff read + Marco's tests + full `verify.sh`, no Rob mutations.

## Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | **No API change.** Secondary lines come only from fields already in `kpis` (`revenue`, `orderCount`, `averageOrderValue`, `customers.{total,new,returning}`) and the response's `period` | Smallest change; every line is answerable from today's response. Decision log row "Admin dashboard KPI cards" | New fields (previous-period revenue is P2.22; cancelled/completed counts are P2.23) |
| D2 | Four cards, in this order, with these exact strings (`<p>` = period phrase, D3): **Revenue** · `formatMoney(revenue)` · `Paid minus refunds <p>`. **Orders** · `String(orderCount)` · `Paid <p>`, or `No paid orders <p>` when 0. **Average order value** · `formatMoney(averageOrderValue)` · `Across 1 order` / `Across N orders`, or `No paid orders <p>` when `orderCount` is 0. **Customers** · `String(customers.total)` · `N new · M returning`, or `No customers <p>` when `total` is 0 | Revenue definition is app context §4.8; each secondary line explains or splits the figure above it | Client-computed AOV or totals (hard rule 1; the client never adds money) |
| D3 | `<p>` from **`data.period` (the response)**, not the selected chip: `today` → `today`, `7d` → `in the last 7 days`, `30d` → `in the last 30 days` | With `keepPreviousData`, the old figures stay on screen while the new period loads; the phrase must describe the figures shown, not the chip | Reading the `period` state |
| D4 | **Never blank** = every rendered card always has a label, a value and a secondary line. Zero figures render as the server sent them (`$0.00`, `0`) with the zero-state secondary from D2. No clamping, no `—` placeholder, negative revenue (refunds > paid) shown as formatted (`-$5.00`) | The server always sends all four figures for Admin; `formatMoney` already never returns an empty string for a non-empty amount | A "No data" card state (no server case produces it) |
| D5 | **Support:** `kpis: null` → no cards and no placeholder text; the rest of the dashboard renders as today. The client never checks the role for KPIs | Server is the guard (§3.4 rule 11); same pattern as customers `totalSpent` (decision log 12.2) | A client role check; a Support-only Orders/Customers card (needs the server to split `kpis`; no row asks) |
| D6 | Pure mapping `kpiCards(kpis, period): KpiCard[]` in new `features/admin-dashboard/kpi-cards.ts`, `export type KpiCard = { label: string; value: string; secondary: string }`. Rendering stays in `DashboardView.tsx` as `KpiCards` (replaces the `Kpis` function) | Strings and fallbacks unit-tested without rendering; one consumer each | A separate component file (one 15-line consumer) |
| D7 | Card markup: wrapper `View className="flex-row flex-wrap gap-md"` (no "Sales" heading). Each card `View accessibilityLabel={card.label} className="grow basis-40 gap-xs rounded-card border border-border bg-surface p-lg"` with three `Text`: label `text-small text-muted`, value `text-heading font-semibold text-foreground`, secondary `text-small text-muted`. `key={card.label}` | `basis-40` (160 px, Tailwind default scale, not arbitrary) gives 2 per row at 375 px (343 content: 160+12+160) and 4 per row at `max-w-content`; passes PG2. `accessibilityLabel` lets tests scope value and secondary to their card with `within(getByLabelText(...))` | `flex-1` without basis (four squeezed cards at 375 px); an arbitrary `basis-[…]` (restyle rule) |
| D8 | **P2.29:** Recent orders `Section` → `<AdminPanel title="Recent orders">`, same branch order and conditions. Rows: list `View` `gap-xs` → no class (`<View>`), each row `Text` class `min-h-touch border-b border-border py-md text-body text-foreground`, **same string** (`orderNumber · email · date · total · status`, raw `o.status`). Pager row unchanged | Matches the restyled admin order list (P2.34); tests query the whole row string; a column table would overflow 375 px and split the string | Columns per field (breaks `findByText`, P2.36 precedent); switching to `statusLabel` (copy change) |
| D9 | **P2.31:** Low stock `Section` → `<AdminPanel title="Low stock">`; row `Text` gets `border-b border-border` added, same string `${productName} (${sku}) · ${available} left`, same `Link` to `/admin/inventory`; empty text unchanged | Same row style as D8 | Colour-coding by count (no row asks) |
| D10 | `Section` stays for the sections not in these rows (Orders by status, Top products, Needs attention, Quick actions); page container, period chips, Refresh, as-of line unchanged | Later rows (P2.30, P2.32, P2.14–P2.16, P2.27) own them; `Section` keeps consumers | Restyling the whole screen now |

## Files

| File | Row | Change |
|---|---|---|
| `features/admin-dashboard/kpi-cards.ts` | P2.21 | **New.** `KpiCard` type, `kpiCards(kpis: DashboardKpis, period: DashboardPeriod): KpiCard[]` per D2–D4. Private `PERIOD_PHRASE: Record<DashboardPeriod, string>`. Uses `formatMoney` from `@/lib/money`. No other export. |
| `features/admin-dashboard/kpi-cards.test.ts` | P2.21 | **New.** K1–K5. |
| `features/admin-dashboard/components/DashboardView.tsx` | P2.21, P2.29, P2.31 | Replace `Kpis` with `KpiCards({ kpis, period })` per D7, called as `{data.kpis ? <KpiCards kpis={data.kpis} period={data.period} /> : null}`. D8 in `RecentOrders`; D9 in the low-stock block. Import `AdminPanel`. Remove imports left unused. Nothing else. |
| `features/admin-dashboard/components/DashboardView.test.tsx` | P2.21 | Only the edits listed under "Changed existing tests", plus new tests D1–D3 appended inside the `describe`. Import `within` from `expo-router/testing-library`. |

**Changed existing tests** (the only edits to existing blocks; each justified as P2.21 new behaviour, none weakened):
1. `shows the figures exactly as the server returned them…`: the four lines `Revenue: $1,234.50`, `Orders: 7`, `Average order value: $176.36`, `Customers: 5 (3 new, 2 returning)` become per-card assertions: `within(screen.getByLabelText('Revenue'))` has `$1,234.50` and `Paid minus refunds today`; `Orders` has `7` and `Paid today`; `Average order value` has `$176.36` and `Across 7 orders`; `Customers` has `5` and `3 new · 2 returning`. First line keeps `await screen.findByLabelText('Revenue')`. Stronger: each value is now tied to its label, and the secondary line is checked. The as-of, top-product and low-stock lines stay byte-identical.
2. `asks the server again when the period changes…`: line `await screen.findByText('Revenue: $1,234.50')` → `await screen.findByText('$1,234.50')` (load anchor only).
3. `shows the error with a retry…`: final line same replacement (`'$1,234.50'`).

Every other existing block (Support test, status link, low-stock link, needs-attention, recent-orders paging, quick actions) stays **byte-identical**.

## Freeze list

No edits: `backend/**` (except M1's temporary mutation, restored and `cmp`-checked); `features/admin-dashboard/{api.ts, api.test.ts, types.ts, hooks/**}`; `tests/admin-dashboard-fixtures.ts` (build new fixtures inline with `makeDashboard({ kpis: {...} })`); `components/**` including `AdminPanel.tsx`; `lib/**`; `tests/admin-phone-width*.ts(x)`, `tests/routes.test.tsx`, `tests/jest-setup.js`; `app/**` (the `/admin` Stack header is not in these rows); `package.json`, lock files, `tailwind.config.js`, `lib/design-tokens.js` (no new tokens, no package); `docs/**` including the tracker (Rob/coordinator mark rows Done); `.github/`, `.claude/`. In `DashboardView.tsx`: no visible string or accessible name changes except the KPI cards (D2, D7) and the removed `Sales` heading. Restyle rules: RN primitives and token utilities only; no `style=`, no hex, no arbitrary `[...]` values. If a frozen test fails, fix the code, not the test; if you think the test is wrong, stop and report it.

## Build order

1. `AGENT_STATUS.md` → Marco BUSY. Read the `Read:` list. Baseline: `cd frontend/apps/store && npx jest features/admin-dashboard tests/admin-phone-width 2>&1 | tail -n 20`; quote the pass count.
2. **M1 evidence (auth rule, test-first proof).** Copy `AdminDashboardController.cs` to the scratchpad. Change line 66's `User.IsInRole(IdentityRoles.Admin)` to `true`. Run `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~Dashboard_for_an_Admin_has_the_servers_KPIs_and_for_Support_has_kpis_null" 2>&1 | tail -n 20` (Postgres up); quote red. Restore from the copy; `cmp` → no output; quote. Re-run the filter, quote green.
3. **Test-first P2.21 (pure):** write `kpi-cards.test.ts` K1–K5; run → red (module not found); quote. Write `kpi-cards.ts` → green; quote.
4. **Test-first P2.21 (screen):** make the three listed existing-test edits and add D1–D3; run `npx jest features/admin-dashboard 2>&1 | tail -n 20` → red; quote. Implement `KpiCards` in `DashboardView.tsx` → green.
5. **P2.29** (D8), then **P2.31** (D9). After each, `npx jest features/admin-dashboard tests/admin-phone-width 2>&1 | tail -n 20` green; existing recent-orders and low-stock tests untouched.
6. `git -c safe.directory='*' diff --stat` and `git -c safe.directory='*' diff -- frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx`: confirm only the three listed edits plus additions. `npm run format` in `frontend/apps/store`.
7. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line. Fix and re-run until green.
8. Write `slice-plans/slice-P2.21-marco-report.md` (≤ 6 KB): VERIFY line; baseline and final jest counts; red quotes for M1, step 3 and step 4; `cmp` output; files changed; the three changed existing-test edits with one-line justifications; manual check list (below) marked "pending Nathaniel". Marco AVAILABLE.

## Required tests

`features/admin-dashboard/kpi-cards.test.ts` (pure, test-first)
- **K1** Admin fixture (`makeDashboard().kpis`, `today`) → exactly `[{Revenue, $1,234.50, Paid minus refunds today}, {Orders, 7, Paid today}, {Average order value, $176.36, Across 7 orders}, {Customers, 5, 3 new · 2 returning}]` (`toEqual`, order matters).
- **K2** Never blank, zero period: amounts `0.00`, `orderCount` 0, customers 0/0/0, `7d` → values `$0.00`, `0`, `$0.00`, `0`; secondaries `Paid minus refunds in the last 7 days`, `No paid orders in the last 7 days` (Orders and AOV), `No customers in the last 7 days`. Every `value` and `secondary` non-empty.
- **K3** Period phrase: `it.each` over `today` / `7d` / `30d` → Revenue secondary `Paid minus refunds today` / `… in the last 7 days` / `… in the last 30 days`.
- **K4** Singular: `orderCount` 1 → AOV secondary `Across 1 order`, Orders secondary `Paid today`.
- **K5** Server money passes through: revenue `-5.00` → `-$5.00` (no clamping); customers `{ total: 2, new: 0, returning: 2 }` → `0 new · 2 returning`.

`features/admin-dashboard/components/DashboardView.test.tsx` (new blocks; the three edits are listed above)
- **D1** No revenue without server KPIs, whatever the role: `it.each(['Admin', 'Support'])` with `makeDashboard({ kpis: null })` → after `findByText('Orders by status')`, `queryByLabelText` is null for `Revenue`, `Orders`, `Average order value`, `Customers`, and `queryByText('Paid minus refunds today')` is null. Proves the client keys on server data, not the role (D5).
- **D2** Never blank on screen: Admin, zero `kpis` as K2 with `period: 'today'` → `within(getByLabelText('Revenue'))` shows `$0.00` and `Paid minus refunds today`; `Orders` shows `0` and `No paid orders today`; `Customers` shows `0` and `No customers today`.
- **D3** Phrase follows the response: Admin, `makeDashboard({ period: '30d' })` while the chip state is still `Today` → `Paid minus refunds in the last 30 days` and `Paid in the last 30 days` visible.

Existing tests that cover P2.29 and P2.31 unchanged and green: `lists recent orders and pages through them`, `links a low-stock row to inventory`, the low-stock line in the first test, `tests/admin-phone-width.test.tsx` PW1 `/admin` (pager buttons), `tests/admin-phone-width-guard.test.ts` PG1–PG3 (covers the new classes). Server: `AdminDashboardApiTests` unchanged (happy, 401/403, 400, Support `kpis: null`), exercised by M1.

**Mutation (one):** M1 above — the only money/security guard this row names. No others (CLAUDE.md §3 "no extras").

## Review method (Rob)

Low tier, one review. `git diff --stat` + untracked; read `kpi-cards.ts`, its test, the `DashboardView.tsx` diff and the test diff. Checks:
- Backend diff empty (`git diff --stat -- backend` prints nothing); M1 red and `cmp` quoted in Marco's report.
- Existing test edits are exactly the three listed; every other existing block byte-identical (read the test-file diff hunk by hunk).
- Strings match D2/D3 exactly; period phrase reads `data.period`; no role read for KPIs (`grep -n "role\|useSession" features/admin-dashboard/components/DashboardView.tsx` adds nothing).
- No `style=`, hex or arbitrary `[...]` classes in the diff; restyle rows changed classes and wrappers only, every row string byte-identical.
- No dead code: `Kpis` and any now-unused import removed; `Section` still used.
No Rob mutations (Low tier). Full `verify.sh` in the PASS round only; quote the `VERIFY` line.

## Manual checks (Nathaniel; Marco lists them as pending)

1. `/admin` at 375 px in light and dark: cards two per row, no horizontal scroll; recent-orders rows wrap.
2. Signed in as Support: no KPI cards; recent orders and low stock render.
3. Switch Today → 30 days: during the load the card phrases still match the figures shown.

## Risks

- **`basis-40` on native:** NativeWind maps it to `flexBasis: 160`; if a card renders full width on device, that is layout only (manual check 1), not a test failure. Do not swap in an arbitrary value.
- **Ambiguous text queries:** `$1,234.50` and `7` may appear elsewhere if fixtures change; tests scope with `within(getByLabelText(...))` except the two load anchors, which use the unique revenue string.
- **`/admin` still shows the Stack header** (other admin routes hide it): out of scope; goes to `slice-plans/backlog.md` as a NOTE, not this slice.
- **Copy drift:** the KPI strings are new copy owned by this plan; any wording change during build is a plan change, not Marco's call.
