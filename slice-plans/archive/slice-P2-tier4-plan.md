# Slice P2-tier4 — Charts, insights, settings, export (tracker tier 4)

Rows: P2.25 · P2.26 · P2.27 · P2.28 · P2.42 · P2.44 · P2.43 · P2.17 · P2.45. Autonomous. Builder: **Marco, all files**.
**Risk tier: High** (new Admin-only auth surface; revenue exposure to Support; money aggregation). Full review + §3 mutations.

## 1. Read

- `CLAUDE.md` (already loaded) · quality-bar §1, §2, §5, §6
- arch §3.4 · §9 admin dashboard rows · §5.5 · §11.3
- §13.2, grep only: `Chart package`, `Admin dashboard KPI cards`, `Admin dashboard period, refresh`, `Dashboard previous period`, `Admin order list URL contract`, `Admin dashboard tier 4` (the row this plan adds)
- app context §2 (Support row), §4.8, §4.14, §5 (`/admin/analytics`, `/admin/settings`)
- `docs/admin-dashboard-css-values.md` §6 and the hardcoded-values table (chart colours, Export button)
- Code: the files named in §3 and `Projections/{AdminDashboardApiTests.cs, ProjectionEnv.cs}`.

## 2. Decisions made (open points resolved with the recommended option)

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | Trend = `GET /api/v1/admin/dashboard/trend?period=today\|7d\|30d`, **Admin only**. `period` parsed by the existing `DashboardPeriod.Parse` (same 400). Today → **hourly** UTC buckets from 00:00Z up to and including the current hour (`now.Hour + 1` buckets); 7d/30d → **daily** UTC buckets from `period.From(now)`, 7 or 30 buckets, last one partial. No future buckets | UTC matches the dashboard periods (6b D10); a future zero bucket reads as "no sales" | Device-local buckets; 24 fixed hourly buckets |
| D2 | Bucket revenue = paid in `[start, end)` − actor refunds succeeded in `[start, end)`, from `admin_dashboard_orders.paid_at` / `admin_dashboard_refunds.succeeded_at` (projection, existing indexes). Window `[from, now]` exactly as `ReadKpisAsync`, so **Σ buckets = `kpis.revenue`** and Σ orders = `kpis.orderCount` for the same clock. Server returns `total` and `orderCount`; a bucket may be negative | Same cash-basis revenue as the KPI (6b D9); server owns money (rule 1) | Gross-only trend; client-side total |
| D3 | Bucketing in a pure `TrendCalculator` over two narrow row lists read in one store call; no SQL `date_trunc` | Unit-testable boundaries; a single store's 30 days of paid orders is small | SQL `GROUP BY` (harder to test) |
| D4 | Response `{ period, granularity: "hour"\|"day", asOf, currency, total: Money, orderCount, buckets: [{ start, revenue: Money, orders }] }`; `asOf` by the dashboard's rule via one shared helper | Projected data shows as-of (app ctx 4.8) | No as-of |
| D5 | Export = `GET /api/v1/admin/dashboard/export?period=`, **Admin only**, same period rule/400. Body = the trend series as CSV: header `bucket_start_utc,revenue,orders,currency`, one row per bucket, CRLF line ends, amounts invariant `0.00##` (as `MoneyResponse`), start `yyyy-MM-ddTHH:mm:ssZ`. `Content-Type: text/csv; charset=utf-8`, `Content-Disposition: attachment; filename="dashboard-<period>-<yyyy-MM-dd>.csv"` (date = `now` from `IClock`, UTC) | Same numbers as the chart; no free text → no CSV injection | KPIs + top products (mixed shape, names need escaping); a totals row |
| D6 | Trend and export live in one new `AdminInsightsController` (`[Route("api/v1/admin/dashboard")]`, class-level `[Authorize(Policy = AuthorizationPolicies.Admin)]`), **not** in `AdminDashboardController` (AdminOrSupport) | One explicit Admin guard per controller; no method-level policy stacking to forget | Method-level `[Authorize]` inside the AdminOrSupport controller |
| D7 | Settings = `GET /api/v1/admin/settings`, **Admin only** (`AdminSettingsController`, class-level Admin policy) → `{ currency, lowStockThreshold }` — exactly two fields. Currency from the existing `DashboardSettings`; threshold from a new Inventory contract `IInventorySettingsReader { int LowStockThreshold { get; } }` (internal impl over `StockSettings`). Controller maps the two values directly (no service: there is no logic) | Inventory owns its validated config (§13.2 threshold row); small interface (§3.2) | Admin reading Inventory's config key (duplicates default and validation); a member on `IInventoryReader` (edits 3 fakes) |
| D8 | Settings has no input, so **no 400 test** (N/A, stated in the test file header). 401/403 (Customer, Support) and happy path are required | Nothing to validate | A fake 400 |
| D9 | Frontend Admin gate for trend, chart, "View report →" and Export = `adminRouteDecision(role, '/admin/analytics') === null`; the trend query is `enabled` only then | Quick-actions pattern (P2.32); Support never requests revenue; the API 403 is the real guard | Gating on `kpis !== null`; calling and hiding on 403 |
| D10 | Chart geometry parses `Money.amount` to a number **for y-positions only**; every money **label** is the server string (`formatMoney(total)`); y-range = `[min(0, min), max(0, max)]`, all-zero → flat baseline (target screenshot), 1 bucket → one dot | Geometry, not money arithmetic (rule 1) | Server-sent pixel numbers |
| D11 | Status buckets (display only; API keeps 8): **Awaiting payment** = PendingPayment (`warning`) · **In progress** = Paid, Processing, Shipped (`muted`) · **Completed** = Delivered (`success`) · **Cancelled or refunded** = Cancelled, Refunded, Returned (`danger`). Colours from `useThemeColors()` (existing keys, no new tokens). Percent = `Math.round(count / total × 100)` of the bucket; display only, may not sum to 100 | Groups by next action; §7.3 terminals together | New colour tokens |
| D12 | Tapping: a **single-status** bucket header is the link (`/admin/orders?status=<Status>`); a **multi-status** bucket shows its member statuses with count > 0 as sub-links with today's exact text `` `${status}: ${count}` `` → `/admin/orders?status=<Status>`. No API or URL-contract change | The order filter takes one status (§13.2 order-list rows); `Paid: 4` test unchanged | Multi-status filter (changes two recorded contracts); bucket → one member status (list shows fewer than the count) |
| D13 | Empty donut: total 0 → no SVG, text `No orders yet.` (today's copy). Donut centre = total count + label `orders` | Never a 0/0 arc | An empty ring |
| D14 | Dashboard Refresh refetches the dashboard **and** the trend (Admin); each keeps its last data on error. Supersedes the P2.14 "dashboard query only" clause for the trend | One button refreshes every figure | A second refresh control |
| D15 | Analytics `/admin/analytics` (Admin, feature `admin-analytics`): period from the URL with the dashboard's rules (`parseDashboardPeriod`), the same period switch, full-width trend panel, full-width top products from the **existing dashboard query** (`queryKeys.admin.dashboard(period)`, cache shared). No new endpoint | Top products already served per period; smallest change | A top-products endpoint |
| D16 | Export button on the dashboard header, Admin only (D9): `apiGetText` → `saveTextFile('dashboard-<period>.csv', 'text/csv', text)` in `lib/platform` — web: `Blob` + object URL + `<a download>` then revoke; native: React Native core `Share.share({ title, message })`. Pending state while running; failure → inline alert `Export failed: <userMessage>` | App ctx 4.14 puts Export on the dashboard; platform code only in `lib/platform` (rule 17); no new package | Endpoint without a consumer; a new package |
| D17 | `lib/api-client.ts` gains `apiGetText(path, options)`: same `execute` path (auth, single 401 refresh, timeout, correlation id, error kinds), `Accept: text/csv`, returns the raw body | Reuses the one HTTP path | Raw `fetch` in the feature |
| D18 | `react-native-svg` installed now (first consumer; approved P2.5): `npx expo install react-native-svg`, then pin **exact** in `package.json`. Line chart + donut are our own components (`Svg`, `Path`, `Circle`, `Line`, `LinearGradient`, `Defs`, `Stop`, `Text as SvgText`). No other package | §13.2 "Chart package" | Any chart library |
| D19 | Nav: Analytics and Settings `ready: true` (shell links + breadcrumb follow from `nav.ts`). Route gate unchanged: `adminRouteDecision` already sends Support from both to `/admin` | Screens now exist | — |

Rob added the §13.2 row and the §9 rows with this plan.

## 3. Files and contracts

### Backend (Admin module unless noted)
- `Application/RevenueTrend.cs` (new): `internal sealed record TrendBucket(DateTimeOffset Start, decimal Revenue, int Orders)`; `RevenueTrendView(string Period, string Granularity, DateTimeOffset AsOf, DateTimeOffset To, string Currency, decimal Total, int OrderCount, IReadOnlyList<TrendBucket> Buckets)`; `PaidAtRow(DateTimeOffset PaidAt, decimal GrandTotal)`; `RefundAtRow(DateTimeOffset SucceededAt, decimal Amount)`; `RevenueEvents(IReadOnlyList<PaidAtRow> Paid, IReadOnlyList<RefundAtRow> Refunds)`; `static class TrendCalculator { Compute(DashboardPeriod period, DateTimeOffset now, RevenueEvents events) }` (D1–D2).
- `Application/DashboardKpis.cs`: `DashboardPeriod` gains `Granularity` (`Days == 1 ? "hour" : "day"`). No other change.
- `Application/ProjectionAsOf.cs` (new, or a static on an existing file): `static DateTimeOffset From(IReadOnlyList<ProjectorHealth> projectors, DateTimeOffset now)`; `DashboardQueryService` switches to it (behaviour identical, existing tests unchanged).
- `Application/RevenueTrendService.cs` (new): deps `IDashboardStore`, `IClock`, `DashboardSettings`, `IConsumerHealthReader`; `GetAsync(DashboardPeriod, CancellationToken) → RevenueTrendView`.
- `Application/IDashboardStore.cs`: `Task<RevenueEvents> ReadRevenueEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)` — same predicates as `ReadKpisAsync`, `AsNoTracking`, two narrow selects. `Infrastructure/DashboardStore.cs` implements it.
- `Api/AdminInsightsController.cs` (new): D6; actions `GET trend` → `RevenueTrendResponse(string Period, string Granularity, DateTimeOffset AsOf, string Currency, MoneyResponse Total, int OrderCount, IReadOnlyList<TrendBucketResponse> Buckets)`, `TrendBucketResponse(DateTimeOffset Start, MoneyResponse Revenue, int Orders)`; `GET export` → `File(bytes, "text/csv; charset=utf-8", fileName)`. Invalid period → `ValidationProblem` with key `period` (copy the dashboard message).
- `Api/TrendCsv.cs` (new): `internal static string Write(RevenueTrendView view)` per D5 — pure, InvariantCulture.
- `Api/AdminSettingsController.cs` (new): D7; `SettingsResponse(string Currency, int LowStockThreshold)`.
- `AdminModule.cs`: register `RevenueTrendService` (scoped).
- **Inventory:** `Ecommerce.Inventory.Contracts/IInventorySettingsReader.cs` (new, public); `Ecommerce.Inventory/Infrastructure/InventorySettingsReader.cs` (new, `internal sealed`, ctor `StockSettings`); register in `InventoryModule.cs` (lifetime matching `StockSettings`).
- No migration, no new table, no new index (both columns already indexed).

### Frontend (`frontend/apps/store`)
- `package.json`: `react-native-svg` exact pin (D18).
- `lib/api-client.ts`: `apiGetText` (D17). `lib/query-keys.ts`: `admin.trend(period)` = `['admin','dashboard','trend',period]`, `admin.settings` = `['admin','settings']`.
- `lib/platform.d.ts`, `platform.web.tsx`, `platform.native.tsx`, `platform.conformance.ts`: `saveTextFile(fileName: string, mimeType: string, text: string): Promise<void>` (D16).
- `features/admin-dashboard/types.ts`: `RevenueTrend`, `TrendBucket`. `api.ts`: `fetchRevenueTrend(period, signal)`, `fetchDashboardExport(period)`. `hooks/hooks.ts`: `useRevenueTrendQuery(period, enabled)` (`keepPreviousData` like the dashboard).
- `features/admin-dashboard/trend.ts` (new, pure): `trendGeometry(trend, width, height)` → `{ points, linePath, areaPath, gridYs }` and `bucketLabel(start, granularity)` (UTC `HH:00` / `MMM d`).
- `features/admin-dashboard/status-buckets.ts` (new, pure): `STATUS_BUCKETS` (D11, typed with `OrderStatus` from `features/orders/types` so every status is placed exactly once) and `statusBuckets(ordersByStatus)` → `{ total, buckets: [{ key, label, tone, count, percent, statuses: {status,count}[], href? }] }`; unknown status names ignored.
- Components (new, `features/admin-dashboard/components/`): `RevenueTrendPanel.tsx` (props: `period`, optional `reportLink`; header TOTAL + ORDERS from the server; SVG per CSS doc §6, `width="100%"`, `viewBox 0 0 560 170`, colours `success`/`border`/`subtle` from `useThemeColors()`; own loading text and an error line + `ActionButton` labelled **`Retry trend`** — never `ErrorState`'s default label, so existing dashboard tests keep one retry button); `OrdersByStatusPanel.tsx` (title stays **`Orders by status`**, donut + legend per D11–D13, column layout below 560 px); `TopProductsPanel.tsx` (extracted verbatim from `DashboardView`, same text); `PeriodSwitch.tsx` (extracted segmented control, same roles/labels/selected state).
- `DashboardView.tsx`: use the extracted components; add Export button (`label="Export CSV"`, pill) and the trend panel with `reportLink` → `{ pathname: '/admin/analytics', params: period === 'today' ? {} : { period } }`, text `View report →`, both only under the D9 gate; trend panel rendered only inside the `data` branch; Refresh per D14.
- `features/admin-analytics/components/AnalyticsView.tsx` + `app/admin/analytics.tsx` (D15; route file like `app/admin/inventory.tsx`).
- `features/admin-settings/{types.ts, api.ts, hooks.ts, components/AdminSettingsView.tsx}` + `app/admin/settings.tsx`: two read-only rows "Store currency" / "Low-stock threshold" (`<n> units available or fewer`), note "Read-only. Set in server configuration.", loading, `ErrorState` with retry.
- `features/admin-shell/nav.ts`: D19.
- `tests/admin-dashboard-fixtures.ts`: `makeRevenueTrend()`; new `tests/admin-settings-fixtures.ts` only if the settings test needs more than one inline object.
- Tracker: mark the nine rows Done when their tests pass.

## 4. Build order (security and invariants first)

1. **Red first:** `AdminInsightsApiTests` auth tests (trend + export: 401, Customer 403, Support 403) and `AdminSettingsApiTests` auth tests → quote the red (404 before the routes exist), then controllers with the Admin policy → green.
2. **Red first:** `TrendCalculatorTests` (boundaries, refund subtraction, Σ = KPI) → `TrendCalculator` → green.
3. Store read, `RevenueTrendService`, `ProjectionAsOf` extraction, trend happy-path and 400 tests; the Σ buckets = `kpis.revenue` integration test (with an actor refund).
4. `TrendCsv` unit tests → export happy-path test (headers, rows match the trend).
5. Inventory contract + settings happy path.
6. Frontend: install svg, `apiGetText` + platform tests, pure `trend.ts` / `status-buckets.ts` tests first, then components, then the **Support gate test first** (D9) before wiring the dashboard, analytics, settings, nav.
7. `bash scripts/verify.sh 2>&1 | tail -n 30` once at the end; run the 4 mutations (§6).

## 5. Required tests (T = test-first)

**Backend unit — `backend/tests/Ecommerce.Admin.Tests/TrendTests.cs`**
- T · U1 today at 14:20Z → 15 hourly buckets, first `00:00Z`, last `14:00Z`; 7d → 7 daily, 30d → 30 daily, first = `period.From(now)`.
- T · U2 paid at exactly a bucket boundary lands in the later bucket; paid at `now` lands in the last bucket; rows before `from` or after `now` ignored.
- T · U3 an actor refund succeeded in a bucket lowers that bucket only; a bucket can be negative; `Total` = paid − refunds over the window; `OrderCount` = paid rows.
- U4 no rows → every bucket 0/0, total 0. U5 `DashboardPeriod.Granularity`.
- U6 `TrendCsv.Write`: header, CRLF, invariant amounts under `de-DE`, `Z` starts, one row per bucket.

**Integration — `Ecommerce.Api.IntegrationTests/Projections/AdminInsightsApiTests.cs` (helpers may be added to `ProjectionEnv`, nothing changed there)**
- T · I1 trend: anonymous 401, Customer 403, **Support 403** (body has no amount).
- T · I2 export: anonymous 401, Customer 403, **Support 403**.
- T · I3 settings: anonymous 401, Customer 403, **Support 403**.
- I4 trend 400 for `yesterday`, `1d`, `7D` with error key `period`; I5 export 400 same.
- I6 trend Admin today: `granularity` `hour`, bucket count = clock hour + 1, the paid order in its hour; `total`/`orderCount` equal `/admin/dashboard?period=today` `kpis.revenue`/`orderCount` at the same clock.
- T · I7 7d with one paid order and one **actor** refund (as in the existing refund tests): Σ = `total` = dashboard `kpis.revenue`; 30d → 30 buckets.
- I8 export Admin 7d: 200, `text/csv`, `attachment; filename="dashboard-7d-<clock date>.csv"`, header + 7 rows, revenue values equal the trend's.
- I9 settings Admin: 200, exactly `currency` and `lowStockThreshold` properties, values from config (`USD`, the configured threshold).

**Frontend Jest**
- `lib/api-client.test.ts` (new cases only): `apiGetText` returns text, sends bearer + `Accept: text/csv`, 403 → `forbidden`, 401 → one refresh then retry.
- `lib/platform.test.{web,native}.tsx` (new cases): web object URL + download anchor + revoke; native `Share.share` with the text.
- `trend.test.ts`: points = buckets, x 0→width, max at top, negative below baseline, all-zero flat, one bucket one point; UTC labels.
- `status-buckets.test.ts`: each of the 8 statuses in exactly one bucket; counts, %; total 0 → empty; unknown ignored; `href` only on single-status buckets.
- **New file** `features/admin-dashboard/components/DashboardInsights.test.tsx` (its own fetch mock with trend + export): Admin sees TOTAL `$…` and ORDERS from the server, SVG present, `View report →` opens `/admin/analytics?period=7d`; trend error shows `Retry trend` and the rest of the dashboard; Refresh refetches trend (D14); Export success calls `saveTextFile` with the CSV text; Export failure shows `Export failed:`; donut legend shows 4 buckets with counts and %, single-status bucket opens `/admin/orders?status=PendingPayment`, `Paid: n` sub-link opens `?status=Paid`; empty status list → `No orders yet.`, no donut.
- T · **S1 Support:** no request to `/trend` or `/export`, no chart, no `Export CSV`, no `View report`, no `$` text on the page.
- `features/admin-analytics/components/AnalyticsView.test.tsx`: Admin sees trend + top products for the URL period; period switch writes the URL and refetches; Support at `/admin/analytics` lands on `/admin` with **no** trend request.
- `features/admin-settings/components/AdminSettingsView.test.tsx`: Admin sees `USD` and the threshold; error → retry loads; Support at `/admin/settings` lands on `/admin`, no settings request.
- **Existing tests unchanged** (`DashboardView.test.tsx` untouched). Only justified edits: `AdminShell.test.tsx` S8 link count 7 → 9 and new route registrations; nav/breadcrumb tests asserting those items not ready. List each with the reason.

## 6. Mutations (Rob runs; Marco quotes red for each)

| # | Guard | Break | Must turn red |
|---|---|---|---|
| M1 | `AdminInsightsController` class policy (trend + export) | `AuthorizationPolicies.Admin` → `AdminOrSupport` | I1 and I2 Support-403 assertions |
| M2 | `AdminSettingsController` class policy | `Admin` → `AdminOrSupport` | I3 Support-403 |
| M3 | Frontend Admin gate (D9) in `DashboardView` | gate forced `true` | Jest S1 (Support requests `/trend`, sees `$`) |
| M4 | Trend money (D2) | `TrendCalculator` stops subtracting refunds | U3 and I7 |


## 7. Risks

- **Revenue leak to Support** → D6, D9, M1–M3.
- **Trend ≠ KPI** if the bucket window or predicates drift from `ReadKpisAsync` (`<=` at `now`) → I6/I7 compare against the live dashboard response.
- **Existing dashboard tests**: their `mockApi` returns 599 for `/trend`; hence the `Retry trend` label and the `data`-branch rule. Do not edit `DashboardView.test.tsx`.
- **Jest time**: the previous slice hit a jest timeout — keep new suites lean (one render per case).
- **react-native-svg in Jest**: if `jest-expo` does not transform it, add it to `transformIgnorePatterns` (config only).
- **Phone width (P2.46)**: SVG `width="100%"` + `viewBox`; donut column under 560 px.
- **In-memory bucketing** (D3): revisit past ~100k paid orders / 30 d.
- `docs/architecture/09-api.md` is ~11.5 KB with no `###` to split at — noted in backlog, not split here.
