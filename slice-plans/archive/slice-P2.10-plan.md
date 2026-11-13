# Slice P2.10 — Orders badge, Orders card counts, "vs previous period" chip (Part A)

Mode: Autonomous · Planner: Rob · Tracker rows: **P2.10, P2.23, P2.22** (`docs/admin-dashboard-feature-tracker.md`). Slice row: arch §12.3 "P2 Admin UI".

**Risk tier: HIGH** (CLAUDE.md §5). P2.22 adds a server-computed money figure (previous revenue) and a money-derived percentage, and Support must never receive it (app context §2/§9 "Support sees no revenue", §3.4 rules 1 and 11). Full review + the three mutations in §5.

## Split (why this is Part A only)

The five requested rows are two independent pieces of work:

| Part | Rows | What | Why separate |
|---|---|---|---|
| **A (this plan)** | P2.10, P2.23, P2.22 | Extend the **existing** `GET /api/v1/admin/dashboard` (two fields) + shell badge + two KPI-card additions | No new endpoint, no new package, no new auth surface |
| B (next plan, slice "P2.25") | P2.25, P2.26 | New Admin-only `GET /api/v1/admin/dashboard/trend?period=` with hourly/daily buckets from `admin_dashboard_orders`/`admin_dashboard_refunds`; installs `react-native-svg` (approved, decision log "Chart package (tracker P2.5)") and builds the line chart with TOTAL, ORDERS, "View report →" | New endpoint (401/403/400, Support 403 per §12.3 row), first native-SVG dependency, bucket arithmetic over UTC hours/days. Together with A it would exceed one reviewable diff. Open question for Part B: "View report →" targets `/admin/analytics`, which is P2.42 and still a disabled "Soon" nav item — Part B must render it disabled or the owner reorders P2.42 first |

Backlog check: `slice-plans/backlog.md` has no item tied to P2.10/P2.22–P2.26.

## 1. Read:

Marco reads exactly this, nothing else in `docs/`:

- `docs/architecture/03-principles-hard-rules.md` §3.4 (rules 1, 7, 8, 9, 11, 12, 15)
- `docs/architecture/08.8-eventual-consistency.md` §8.8.6 (read models; KPIs aggregate at read time)
- `docs/architecture/09-api.md` — the `GET /admin/dashboard` row only (`grep -n "admin/dashboard"`)
- `docs/ecommerce-app-context.md` §4.14
- `docs/quality-bar.md` §2 (self-check) and §5
- `docs/architecture/13.2-decision-log.md` — **grep only** these rows: "Admin dashboard KPI cards", "Admin dashboard period, refresh", "Admin shell badge", "Admin order list URL contract", "Slice 6b projections", and the new row "Dashboard previous period and order outcomes" added by this plan
- This plan.

## 2. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | **Previous window = same elapsed time, one period earlier**: `[from − Days, now − Days]` (both ends inclusive, as `ReadKpisAsync` already is). Today at 14:05 compares with yesterday 00:00–14:05; 7d with the 7 days before, cut at the same time of day | Like-for-like; a full previous day would make every morning look like a drop. The two windows never overlap (`now − Days < from` always) | Full previous period `[from − Days, from)` |
| D2 | Previous revenue = `store.ReadKpisAsync(prevFrom, prevTo).Revenue` — **reuse** the existing KPI read | One revenue definition (paid − actor refunds, Slice 6b D9) for both figures; no new store method to drift | A new `ReadRevenueAsync` (second copy of the revenue query) |
| D3 | Server sends `kpis.previousRevenue` (Money) and `kpis.revenueChangePercent` (number or `null`), both **inside `kpis`** | Support already gets `kpis: null` from the role check (6b D19), so the new money figure inherits the guard with no second check. Percent is computed on the server: the client never derives figures from money (decision row "Admin dashboard KPI cards") | Top-level fields (a second Support guard to forget); client-side percent from two money strings |
| D4 | `revenueChangePercent = round((current − previous) / |previous| × 100, 1, AwayFromZero)`; **`null` when previous = 0** | Revenue can be negative (refunds > paid, KpiTests already cover it): `|previous|` keeps the sign meaning "up/down". Division by zero has no honest percentage | Signed denominator (a rise from −100 to −50 would read −50 %); returning 0 or 100 % for previous 0 |
| D5 | Pure function `KpiCalculator.ChangePercent(decimal current, decimal previous) → decimal?` in `DashboardKpis.cs` | Unit-testable invariant next to the other KPI arithmetic | Inline arithmetic in the service or controller (rule 7) |
| D6 | New top-level `deliveredCount` beside `cancelledCount`: distinct orders whose status history entered `Delivered` within `[from, now]`, via new `IOrderReader.CountDeliveredAsync(from, to)` | Same source and shape as `cancelledCount` (decision row "Admin shell badge…"); history time is `IClock`-stamped. Counts are not money, so top-level like `cancelledCount` | Projection `admin_dashboard_orders` (has no status time; would need a migration); one generic `CountEnteredAsync(string status…)` contract (stringly-typed status in a contract for two callers) |
| D7 | In `OrderStores.cs`, one private helper `CountEnteredAsync(OrderStatus, from, to)` used by both `CountCancelledAsync` and `CountDeliveredAsync` | Removes the copy without widening the contract | Duplicating the query |
| D8 | P2.23 renders on the **existing Orders KPI card** as an extra line: `"{delivered} delivered · {cancelled} cancelled"`, zeros shown (never blank). Admin only by consequence (Support has no KPI cards, P2.21) | Smallest UI change; P2.21 already rejected a Support-only Orders card | A fifth card; a Support card (needs a `kpis` split no row asks for) |
| D9 | P2.22 chip on the **Revenue card**: a pill with the server percent formatted `+12.5%` / `-3.0%` / `0.0%` (tone success / danger / muted), followed by muted text `vs {formatMoney(previousRevenue)} previous period`. `revenueChangePercent: null` → no pill, text only | Uses both new fields (no dead field); tone is the sign of a server number, not client arithmetic. Text, not arrows, reads correctly to screen readers | Arrow glyphs; hiding the line when previous is 0 (the card must never be blank) |
| D10 | P2.10 Orders badge = count of `ordersByStatus` entry `Paid` (projection, all-time current status, not period-scoped), text `"{n} paid"`, hidden at 0. **The badge is its own link** to `/admin/orders?status=Paid` (URL contract: decision row "Admin order list URL contract"), rendered as a **sibling** of the nav link, not inside it | The row says the badge links to the filtered list while the Orders item must still open all orders. Nested links are invalid on web (`<a>` in `<a>`). Data already in the shell's dashboard query — no API change | Changing the Orders nav href to the filtered list; a new Paid-count field; nesting a `Link` inside the nav `Link` |
| D11 | `NavBadges` value becomes `{ text: string; href?: string }`. No `href` → rendered inline in the nav link exactly as today (Inventory); with `href` → sibling `Link`, `accessibilityRole="link"`, `accessibilityLabel="{n} paid orders"` | One badge model for both items; Inventory behaviour unchanged | A second map just for badge links; giving Inventory a redundant href |
| D12 | Badge count is eventually consistent (≤ 60 s, §8.8); display only, refetched with the shell's 60 s dashboard query | Read model allowed for admin display (rule 15 bars only checkout/payment/inventory) | A direct Orders read for one badge |

No migration. No new package. No new endpoint. Existing roles on `GET /admin/dashboard` unchanged (Admin, Support).

## 3. Files, contracts

### Backend

| File | Change |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders.Contracts/IOrderReader.cs` | Add `Task<int> CountDeliveredAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);` with the same doc shape as `CountCancelledAsync` |
| `backend/src/Modules/Orders/Ecommerce.Orders/Application/OrderPorts.cs`, `Application/OrderReader.cs` | Port + pass-through, as for cancelled |
| `backend/src/Modules/Orders/Ecommerce.Orders/Infrastructure/OrderStores.cs` | Private `CountEnteredAsync(OrderStatus, from, to)`; `CountCancelledAsync` and `CountDeliveredAsync` call it (D7). Query unchanged for cancelled |
| `backend/src/Modules/Admin/Ecommerce.Admin/Application/DashboardKpis.cs` | `DashboardPeriod.PreviousFrom(now)` = `From(now).AddDays(-Days)`, `PreviousTo(now)` = `now.AddDays(-Days)`; `KpiCalculator.ChangePercent` (D4/D5) |
| `backend/src/Modules/Admin/Ecommerce.Admin/Application/DashboardQueryService.cs` | New record `RevenueComparison(decimal PreviousRevenue, decimal? ChangePercent)`; `DashboardView` gains `RevenueComparison? Comparison` and `int DeliveredCount`. In `GetAsync`: when `includeKpis`, read current KPIs, then previous via `ReadKpisAsync(PreviousFrom, PreviousTo)` (D2), build `Comparison`; else both `null`. `DeliveredCount = await orders.CountDeliveredAsync(from, now, ct)` for every role |
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminDashboardController.cs` | `KpisResponse` gains `MoneyResponse PreviousRevenue, decimal? RevenueChangePercent` (mapped only inside the existing `view.Kpis is null ? null : …` branch; `Comparison` is non-null whenever `Kpis` is). `DashboardResponse` gains `int DeliveredCount` after `CancelledCount`. No logic beyond mapping (rule 7) |
| Test fakes implementing `IOrderReader`: `tests/Ecommerce.Admin.Tests/CustomerQueryServiceTests.cs`, `tests/Ecommerce.Notifications.Tests/Application/Fakes.cs`, `tests/Ecommerce.Orders.Tests/Application/ReturnsAndRefundsTests.cs` | Add `CountDeliveredAsync => throw new NotSupportedException();` — compile-only change, list it in the report |

Wire contract (camelCase JSON):

```
kpis: { revenue, orderCount, averageOrderValue, customers,
        previousRevenue: { amount, currency }, revenueChangePercent: number | null }   // kpis stays null for Support
deliveredCount: number                                                               // top level, both roles
```

### Frontend (`frontend/apps/store`)

| File | Change |
|---|---|
| `features/admin-dashboard/types.ts` | `DashboardKpis` + `previousRevenue: Money; revenueChangePercent: number \| null`; `Dashboard` + `deliveredCount: number` (doc comment like `cancelledCount`) |
| `tests/admin-dashboard-fixtures.ts` | Defaults: `previousRevenue: usd('1000.00')`, `revenueChangePercent: 23.5`, `deliveredCount: 0` |
| `features/admin-dashboard/kpi-cards.ts` | `kpiCards(kpis, period, outcomes: { delivered: number; cancelled: number })`. `KpiCard` gains optional `detail?: string` (Orders, D8) and `change?: { pill: string \| null; tone: 'success' \| 'danger' \| 'muted'; text: string }` (Revenue, D9). Formatting: `percent.toFixed(1)` with `+` for > 0; tone by sign |
| `features/admin-dashboard/components/DashboardView.tsx` | `KpiCards` passes `{ delivered: dashboard.deliveredCount, cancelled: dashboard.cancelledCount }`; renders `change` as a wrapping row (pill with `border-success/danger/border` + `text-success/danger/muted`, then text) and `detail` as a muted line. Tokens only (§5.5) |
| `features/admin-shell/badges.ts` | `NavBadges = Readonly<Partial<Record<string, NavBadge>>>`, `NavBadge = { text: string; href?: string }`; add `/admin/orders` from the `Paid` entry of `ordersByStatus` (D10) |
| `features/admin-shell/components/AdminShell.tsx` | Sidebar item row: nav `Link` (flex-1) + optional sibling badge `Link` when `badge.href` (D11); inline badge unchanged when no `href`. Pressing the badge link calls `onNavigate` like the nav link (closes the phone overlay) |

## 4. Build order (invariant tests first)

1. **Red first** — `KpiTests`: `ChangePercent` cases (§5 U1–U4). Quote the red (method missing / wrong value).
2. **Red first** — integration: Support never gets previous revenue (I2) and the previous-window boundary test (I3). Quote red.
3. `DashboardPeriod.PreviousFrom/To`, `ChangePercent`, service + controller mapping → green.
4. Orders: contract, port, store helper, fakes → I5/I6 green.
5. Frontend types + fixture, `kpi-cards.ts` + tests, `DashboardView` + tests.
6. `badges.ts` + tests, `AdminShell` sibling link + tests.
7. Mutations M1–M3 (§5), then full `bash scripts/verify.sh 2>&1 | tail -n 30`.

## 5. Required tests

No §11.2 numbered or F test is named by the §12.3 P2 row for these rows; its acceptance ("Support … no revenue rendered") is covered by I2 and J6. `GET /admin/dashboard` is a **changed** endpoint: its existing 401/403 (`Dashboard_without_a_token_is_401_and_as_a_Customer_is_403`) and 400 (`Dashboard_with_an_unknown_period_is_400`) tests must stay unchanged and green; list them in the report as the auth/validation coverage.

**Unit — `backend/tests/Ecommerce.Admin.Tests/KpiTests.cs`** (TF = test-first)
- U1 TF: 120 vs 100 → `20.0`; 100 vs 120 → `-16.7` (AwayFromZero at 1 dp).
- U2 TF: previous `0` → `null` (current 0 and current 50).
- U3 TF: negative previous: −50 vs −100 → `50.0` (|previous|); 50 vs −100 → `150.0`.
- U4 TF: `DashboardPeriod.PreviousFrom/To` for today/7d/30d at a fixed `now` (e.g. 2026-11-12T14:05Z): today → 11-11T00:00Z..11-11T14:05Z; 7d → 10-30T00:00Z..11-05T14:05Z; 30d → 09-14T00:00Z..10-13T14:05Z. Assert `PreviousTo < From(now)`.

**Integration — `backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs`** (use `ProjectionEnv`, `e.Env.Clock`, re-sign-in after clock moves as the cancelled tests do; pin the clock to a mid-day UTC time first if `ProjectionEnv` doesn't, so `AddDays` never crosses a different midnight)
- I1 happy: Admin, one paid order today and none before → `kpis.previousRevenue.amount = "0.00"`, `currency = "USD"`, `revenueChangePercent` is JSON `null`; `deliveredCount` present (0).
- I2 TF (security): order paid yesterday, query today as Support → `kpis` null **and** `support.Text` contains neither `previousRevenue` nor the previous amount string; the same request as Admin has them.
- I3 TF (money window): order A paid at T, order B paid at T+2 min, clock → T+1 day+1 min, query `today` as Admin → `previousRevenue` = total(A) only (B is after `now − 1 day`), current revenue 0; then `7d` → previous 0 and current includes both.
- I4: previous revenue nets an actor refund that succeeded in the previous window (reuse the refund helper used by the existing revenue tests) — proves the shared definition (D2).
- I5: an order driven Paid → Processing → Shipped → Delivered through the existing admin endpoints (helpers in `Orders/OrdersFulfillmentApiTests.cs`) → `deliveredCount` 1 for Admin **and** Support; a Paid-only order never counts.
- I6: after clock + 2 days, `today` → `deliveredCount` 0, `7d` → 1 (mirrors the cancelled period test).

**Jest — `frontend/apps/store`**
- J1 `features/admin-dashboard/kpi-cards.test.ts`: Orders card `detail` = `"3 delivered · 1 cancelled"`, and `"0 delivered · 0 cancelled"` at zero.
- J2 same file: Revenue `change` for `23.5` → pill `+23.5%` success; `-4` → `-4.0%` danger; `0` → `0.0%` muted; `null` → pill `null`, text `vs $0.00 previous period`; text always uses the server `previousRevenue` via `formatMoney`.
- J3 `DashboardView.test.tsx`: Admin dashboard renders the pill and `vs $1,000.00 previous period` in the Revenue card and the outcomes line in the Orders card (query within the card by its `accessibilityLabel`).
- J4 `features/admin-shell/badges.test.ts`: Paid 4 → `{ '/admin/orders': { text: '4 paid', href: '/admin/orders?status=Paid' } }`; no Paid entry or count 0 → no orders key; inventory value becomes `{ text: '3 low' }`.
- J5 `AdminShell.test.tsx`: the badge link is a separate link (`getByRole('link', { name: '4 paid orders' })`), pressing it goes to pathname `/admin/orders` with `status=Paid`; the Orders nav link still goes to `/admin/orders` without params; on phone width pressing the badge closes the overlay.
- J6 `DashboardView.test.tsx`: Support (`kpis: null`) renders no `previous period` text and no `%` pill.

**Changed existing tests (must be listed and justified in the report):** `badges.test.ts` B1–B3 and `AdminShell.test.tsx` B4–B6 change only the badge **value shape** (string → `{ text }`) and, where the fixture's default `Paid: 4` now adds an orders badge, set `ordersByStatus: []` or extend the expected map — no assertion is removed or loosened. `kpi-cards.test.ts` call sites gain the `outcomes` argument. The three `IOrderReader` fakes gain one throwing member.

**Mutations (HIGH tier, one per money/security guard).** Copy the file to the scratchpad, break, run the named test, quote red, restore from the copy, `cmp`.
- M1 window (`DashboardKpis.cs`): `PreviousTo` → `From(now)` (full previous period) → U4 and I3 red.
- M2 percent (`DashboardKpis.cs`): `Math.Abs(previous)` → `previous` → U3 red; and separately the `previous == 0` guard removed → U2 red (DivideByZeroException). Count as one mutation of the percent guard; report both reds.
- M3 Support guard (`AdminDashboardController.cs`): `User.IsInRole(IdentityRoles.Admin)` → `true` → I2 red.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Clock-sensitive integration tests flake near UTC midnight | Pin the test clock mid-day before placing orders (I3, I6) |
| Projection lag makes the Paid badge and previous revenue up to 60 s stale | Accepted (D12, §8.8 target); as-of line already shows lag |
| Shared integration DB polluting exact asserts | Assert exact values only where the existing KPI test does (fresh `ProjectionEnv`); otherwise baseline deltas as the cancelled tests do |
| Changing `NavBadges` shape silently drops the Inventory badge | J4 keeps the inventory expectation; B4 unchanged in meaning |
| `deliveredCount` counts an order delivered then refunded/returned | By design: "completed in the period" = entered Delivered (D6); documented in the decision row |
| Phone width overflow from the chip row | `flex-row flex-wrap`; covered by the existing phone-width checks (P2.46) |
