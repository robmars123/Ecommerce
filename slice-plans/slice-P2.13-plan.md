# Slice P2.13 (+ P2.11, P2.12) — Greeting · Inventory badge · Needs attention card

Tracker rows **P2.13** (`admin-dashboard`), **P2.11**, **P2.12** (`admin-shell`). **P2.10 is not built.** Builder: Marco. Autonomous. No §12.3 numbered test. Conventions as `slice-plans/archive/slice-P2.14-plan.md`: token classes only (no `style=`, hex, `[...]`), `AdminPanel`, flex-wrap, P2.46 guard, existing tests byte-identical unless named below, test-first.

## Split — two Marco rounds

Each row needs backend + frontend plus shared shell plumbing: too much for one round. **Part A = P2.13 + P2.11** (builds the plumbing). **Part B = P2.12** (reuses it), only after Rob's PASS on A. A → `slice-P2.13-marco-report.md` / `slice-P2.13-rob-review.md`; B → `slice-P2.12-marco-report.md` / `slice-P2.12-rob-review.md`.

**Read:** this plan · decision log `grep -n "P2.11/P2.12\|P2.24/P2.32" docs/architecture/13.2-decision-log.md` · app context §4.8, §4.14 (grep `^### 4.8\|^### 4.14`) · `09-api.md` line 20 · arch §11.3 · quality-bar §2, §4 · backend (under `backend/src/Modules/`): `Admin/Ecommerce.Admin/{Api/AdminDashboardController.cs, Application/DashboardQueryService.cs, Application/DashboardKpis.cs}`; Part A `Inventory/{…Contracts/IInventoryReader.cs, …/Application/InventoryPorts.cs, …/Infrastructure/InventoryStockReader.cs, InventoryStore.cs:39–44}`; Part B `Orders/{…Contracts/IOrderReader.cs, …/Application/{OrderPorts,OrderReader}.cs, …/Infrastructure/OrderStores.cs:119–120, Persistence/OrderConfiguration.cs:96–109}`; tests `Projections/{AdminDashboardApiTests,ProjectionEnv}.cs`, `Checkout/RetryAndExpiryTests.cs` T4 (Part B) · frontend (`frontend/apps/store/`): `features/admin-shell/{components/AdminShell.tsx, components/AdminShell.test.tsx, nav.ts}`, `features/admin-dashboard/{components/DashboardView.tsx, DashboardView.test.tsx top 60 lines + last describe, types.ts, api.ts}`, `app/admin/_layout.tsx`, `features/auth/admin-access.ts`, `tests/{admin-dashboard-fixtures,fetch-mock,settle,session}.ts`, `tests/routes.test.tsx:40–85`, `lib/query-client.ts`, `components/AdminPanel.tsx`.

## Where each figure comes from (checked in code)

| Figure | Today in `GET /admin/dashboard` | After this slice |
|---|---|---|
| Low stock | `lowStock[]`, **capped at 20** (`LowStockMax`; P2.24 refused a count for that reason) | + top-level `lowStockCount` ← `IInventoryReader.CountLowStockAsync` (D5) |
| Cancelled | none (`ordersByStatus` = current status, all time; read model has no status time) | + top-level `cancelledCount` ← `IOrderReader.CountCancelledAsync` (D7) |
| Pending returns · dead letters · lag | `needsAttention.{pendingReturns, deadLetters[], laggingProjectors[]}` (lag only beyond 60 s) | unchanged; card shows count · sum · max |
| User name | `useSession().user.displayName` | same, trimmed, omitted if blank |

## Role matrix

Server: `GET /admin/dashboard` stays `AdminOrSupport`; Support still gets `kpis: null` and **every count above** (it already receives lowStock, pendingReturns, deadLetters, lag since 6b; arch §9 row). **No new auth rule.** UI hiding (display only, `adminRouteDecision`, same as P2.24/P2.32):

| Item | Admin | Support | Customer |
|---|---|---|---|
| Greeting | yes | yes | route gated (no change) |
| Inventory badge "N low" | yes | no (Inventory nav item already hidden) | no query (`enabled` false) |
| Card: low stock (link `/admin/inventory`) | yes | hidden | — |
| Card: cancelled today (text) | yes | yes | — |
| Card: pending returns (link `/admin/returns`) | yes | hidden | — |
| Card: failed events (dead letters) | yes | yes | — |
| Card: lag | yes | yes | — |

Dead letters to Support: no doc restricts them; Admin-only later = server null + test.

## Risk tier — Low (both parts)

Existing read-only endpoint, policy unchanged, two additive integers; no money, stock movement, payment, idempotency, auth rule or state transition (CLAUDE.md §5). Cross-module reads via `*.Contracts`, window via `IClock`. **No §3 mutations**; Marco's self-checks are evidence only.

## Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | Shell data = the existing dashboard endpoint with `period=today`, one `useQuery` on `queryKeys.admin.dashboard('today')` + `fetchDashboard('today', signal)` in new `features/admin-shell/hooks.ts` `useShellDashboardQuery(role)`; `enabled: adminRouteDecision(role, '/admin') === null`; `refetchInterval: 60_000` (`SHELL_REFRESH_MS`, = lag target arch §8.8) | Same key as the Today dashboard → one request on `/admin`; its Refresh refreshes the shell; full-app tests already mock the URL; no new auth surface | New `/admin/attention` endpoint (new guard, unmocked in full-app tests); mount-only refresh (layout never remounts) |
| D2 | The hook is called in `app/admin/_layout.tsx`; `AdminShell` gets data as **props only** and never calls `useQuery` | Frozen S1–S26 render `AdminShell` without a `QueryClientProvider` (S26 pins it) | Query inside `AdminShell`; `QueryClientContext` sniffing |
| D3 | Badges keyed by nav href: `navBadges(d: Dashboard): Readonly<Partial<Record<string, string>>>` in new `features/admin-shell/badges.ts` → `{ '/admin/inventory': `${d.lowStockCount} low` }` iff `lowStockCount > 0`, else `{}`. `AdminShell` prop `badges?`; Sidebar renders the badge on the item whose `href` matches | P2.10 later adds `'/admin/orders'` to the map; nav.ts untouched | `badge` on `NavItem`; `lowStock.length` (capped at 20) |
| D4 | Badge render: ready item `Pressable` gains `flex-row items-center justify-between gap-md`; badge = `Text className="rounded-control border border-warning px-sm text-caption text-warning"`; `accessibilityLabel` on the Pressable = `` `${label}, ${badge}` `` only when a badge exists (else unchanged, no label prop) | Name "Inventory, 3 low"; existing tokens; frozen queries unaffected | Filled `bg-warning` pill (no foreground token) |
| D5 | `lowStockCount`: `IInventoryReader.CountLowStockAsync(CancellationToken)` → `InventoryStore.CountLowAsync(threshold, ct)`; **one private predicate** shared with `ListLowAsync` (`QuantityOnHand - QuantityReserved <= threshold`) | Count and list can't drift | Duplicated `Where`; raising `LowStockMax` |
| D6 | Response shape: top-level `lowStockCount` (A) and `cancelledCount` (B) on `DashboardResponse`/`DashboardView`, after `LowStock`; TS `Dashboard` gets `lowStockCount: number`, `cancelledCount: number` | Nested in `needsAttention`, frozen test overrides would fail `tsc`; period-scoped `cancelledCount` is reusable by P2.23 | Nesting; optional TS fields |
| D7 | `cancelledCount` = distinct `order_id` in `order_status_history` with `ToStatus == Cancelled` and `from <= CreatedAt <= to`; `IOrderReader.CountCancelledAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken)`; window from `period.From(now)`, `now = clock.UtcNow` | Orders owns the time; direct read like `CountPendingReturnsAsync`; no migration | Read model (needs migration + replay); generic status count (no second consumer yet) |
| D8 | Card = `AdminPanel title="Needs attention"` inside the Sidebar `ScrollView`, after the nav groups (so it scrolls, never squeezes the footer). Items from pure `attentionItems(d, role)` (new `features/admin-shell/attention.ts`), non-zero only, in order: `N low-stock variant(s)` (link `/admin/inventory`, warning) · `N cancelled today` (text) · `N return request(s) waiting` (link `/admin/returns`) · `N failed event(s)` (sum of `deadLetters[].count`, danger) · `Data N s behind` (max `lagSeconds`, warning). Links = `Link asChild` + `Pressable accessibilityRole="link"` `min-h-touch`. None → `All clear.` (muted) | Links only where the role can act; no `alert` role (would re-announce on every screen) | Card in the main column; linking cancelled to `?status=Cancelled` (all-time list ≠ today's count) |
| D9 | Card states via prop `attention?: { kind: 'ready'; items: AttentionItem[] } \| { kind: 'unavailable' }`. Layout: `data` → ready; `!data && isError` → unavailable (`Alerts unavailable right now.`, muted); loading → omitted. A failed poll keeps last data | Alerts never vanish silently | Sidebar retry button (poll retries) |
| D10 | Greeting: pure `greeting(now: Date, displayName?: string): string` in new `features/admin-dashboard/greeting.ts`. Local hour via `now.getHours()`: 5–11 `Good morning`, 12–17 `Good afternoon`, 18–23 and 0–4 `Good evening`; name = `displayName.trim()`, appended as `, Name` if non-empty. `DashboardView({ now = () => new Date() }: { now?: () => Date } = {})`; render `Text className="text-body text-muted"` directly after `ScreenTitle` | App context §4.14 says device time; `new Date(2026, 10, 12, 9, 0)` is local, so tests are TZ-independent | `IClock` (backend rule); email fallback; re-greet timer |

## Files

**Part A (P2.13 + P2.11)**

| File | Change |
|---|---|
| `backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/IInventoryReader.cs` | + `Task<int> CountLowStockAsync(CancellationToken ct);` with a one-line doc comment |
| `.../Ecommerce.Inventory/Application/InventoryPorts.cs` | + `Task<int> CountLowAsync(int threshold, CancellationToken ct);` |
| `.../Ecommerce.Inventory/Infrastructure/InventoryStore.cs` | D5: shared predicate, `CountLowAsync` |
| `.../Ecommerce.Inventory/Infrastructure/InventoryStockReader.cs` | `CountLowStockAsync` → `store.CountLowAsync(settings.LowStockThreshold, ct)` |
| `backend/src/Modules/Admin/Ecommerce.Admin/Application/DashboardQueryService.cs` | `DashboardView` + `int LowStockCount`; `GetAsync` calls `inventory.CountLowStockAsync` |
| `.../Ecommerce.Admin/Api/AdminDashboardController.cs` | `DashboardResponse` + `int LowStockCount`; map it |
| Fakes (**additive only**, one member, file's own style): `tests/Ecommerce.Api.IntegrationTests/Catalog/CatalogStockAndImageTests.cs`, `tests/Ecommerce.Cart.Tests/Application/CartMergeServiceTests.cs`, `tests/Ecommerce.Catalog.Tests/Application/AdminFakes.cs`, `tests/Ecommerce.Catalog.Tests/Application/CatalogBrowseServiceTests.cs` | + `CountLowStockAsync`. No other line changes |
| `…/Projections/AdminDashboardApiTests.cs` | **Append** IA1, IA2 |
| `frontend/apps/store/features/admin-dashboard/types.ts` | `Dashboard` + `lowStockCount: number` (doc comment: uncapped) |
| `frontend/apps/store/tests/admin-dashboard-fixtures.ts` | **Named edit:** default `lowStockCount: 1` (matches the one default `lowStock` row) |
| `features/admin-dashboard/greeting.ts` + `greeting.test.ts` | **New** (D10) |
| `features/admin-dashboard/components/DashboardView.tsx` | D10 only: `now` prop, greeting `Text`; read `user` from `useSession()` (keep `role`) |
| `features/admin-dashboard/components/DashboardView.test.tsx` | **Append** one `describe` (G3, G4) |
| `features/admin-shell/badges.ts` + `badges.test.ts` | **New** (D3) |
| `features/admin-shell/hooks.ts` | **New** (D1) |
| `features/admin-shell/components/AdminShell.tsx` | prop `badges?`, passed to `Sidebar` (both layouts); D4 render. Nothing else |
| `features/admin-shell/components/AdminShell.test.tsx` | **Append** one `describe` (B4–B6) |
| `frontend/apps/store/app/admin/_layout.tsx` | call `useShellDashboardQuery(useSession().user?.role ?? null)`; `badges={query.data ? navBadges(query.data) : undefined}` |
| `frontend/apps/store/tests/admin-shell-data.test.tsx` | **New** full-app L1–L4 (`routes.test.tsx` render/mock pattern) |

**Part B (P2.12)**

| File | Change |
|---|---|
| `backend/src/Modules/Orders/Ecommerce.Orders.Contracts/IOrderReader.cs` | + `Task<int> CountCancelledAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);` |
| `.../Ecommerce.Orders/Application/OrderPorts.cs`, `Application/OrderReader.cs`, `Infrastructure/OrderStores.cs` | D7 pass-through + query (`OrderStatusChange` set, `AsNoTracking`, `Select(h => h.OrderId).Distinct().CountAsync`) |
| `DashboardQueryService.cs`, `AdminDashboardController.cs` | + `CancelledCount` (D6/D7) |
| Fakes (additive only): `tests/Ecommerce.Admin.Tests/CustomerQueryServiceTests.cs` (`FakeOrders`), `tests/Ecommerce.Notifications.Tests/Application/Fakes.cs` (`FakeOrders`) | + `CountCancelledAsync` |
| `AdminDashboardApiTests.cs` | **Append** IB1, IB2 |
| `types.ts`, `tests/admin-dashboard-fixtures.ts` | + `cancelledCount: number`; fixture default `cancelledCount: 0` |
| `features/admin-shell/attention.ts` + `attention.test.ts` | **New** (D8) |
| `AdminShell.tsx` | prop `attention?` (D9), card in `Sidebar` |
| `AdminShell.test.tsx` | **Append** one `describe` (C1–C3) |
| `app/admin/_layout.tsx` | compute `attention` per D9 |
| `tests/admin-shell-data.test.tsx` | **Append** L5 |

Docs: decision log row already appended by Rob. Tracker rows marked Done after PASS. No other doc edits.

## Freeze list

No edits outside the tables above, in particular: controller route/policy attributes, `DashboardPeriod`, migrations (**none**), `Ecommerce.ArchitectureTests`; `features/admin-shell/{nav,breadcrumb,search,api,types}.ts`, `AdminSearch.tsx`; `features/admin-dashboard/{api.ts, hooks/**, period.ts, as-of.ts, kpi-cards.ts}`; `features/auth/**`; `lib/**`; `components/**`; `tailwind.config.js`; `tests/**` except the fixture and the new file; `package.json`, lock files; `.github/`, `.claude/`, `docs/quality-bar.md`.

**Existing tests stay byte-identical** except the six fakes (one added member each) and the fixture default; appends go after the last line. Must stay green unchanged: AdminShell S1–S29, S-T1; DashboardView frozen tests; `routes.test.tsx`; `admin-phone-width*`; existing `AdminDashboardApiTests` (the changed endpoint's 401/403/400/happy coverage). Frozen test fails → fix the code, or stop and report.

## Required tests

**(TF)** = write first, run, quote the red line, then implement.

Part A — backend (`AdminDashboardApiTests.cs`, appended)
- **IA1 (TF)** `Low_stock_count_counts_every_low_variant_while_the_list_stops_at_20`: baseline `lowStockCount`; add 21 variants stock ≤ 3 (one exactly 3) and one at 4 → `baseline + 21`, `lowStock` length 20.
- **IA2** Support gets `200` with the same `lowStockCount` as Admin and `kpis` null (pins the role matrix row).
- Existing 401/403, 400 and happy tests cover the changed endpoint unchanged — cite them in the report.

Part A — frontend
- **G1 (TF)** `greeting.test.ts` `it.each` hours (local `new Date(2026, 10, 12, h, m)`): 0:00, 4:59 → evening; 5:00, 11:59 → morning; 12:00, 17:59 → afternoon; 18:00, 23:59 → evening.
- **G2 (TF)** names: `'Ada'` → `Good morning, Ada`; `'  Ada  '` → `Good morning, Ada`; `'   '` and `undefined` → `Good morning`.
- **G3** `DashboardView` Admin, `now` = 09:00 local → `findByText('Good morning, Admin User')`.
- **G4** Support, `now` = 20:00 → `findByText('Good evening, Support User')`.
- **B1 (TF)** `navBadges(makeDashboard({ lowStockCount: 3 }))` → `{ '/admin/inventory': '3 low' }`.
- **B2 (TF)** `lowStockCount: 0` → `{}`.
- **B3** `lowStockCount: 35` with the 1-row default list → `'35 low'` (count, not list length).
- **B4 (TF)** Admin, `badges={{ '/admin/inventory': '3 low' }}` → link `Inventory, 3 low`, link `Orders` unchanged; at 1024 and at 390 with the menu open.
- **B5** Support with the same `badges` → `queryByText('3 low')` null.
- **B6** `badges={{}}` → `getByRole('link', { name: 'Inventory' })`, no `low` text.
- **L1 (TF)** full app, Admin, wide, `/admin/orders`, dashboard mock `makeDashboard({ lowStockCount: 4 })` → `findByText('4 low')`; a called URL contains `/admin/dashboard?period=today`.
- **L2** full app, Admin, `/admin` → after the dashboard renders, exactly **one** call to `/api/v1/admin/dashboard?` (shared key; recent-orders excluded by pathname).
- **L3** full app, Customer at `/admin` → no call to `/api/v1/admin/dashboard` (query disabled).
- **L4** fake timers (S28 pattern): first response `lowStockCount: 4`, then `5`; advance 60 000 ms → `5 low`, two dashboard calls.

Part B — backend (appended)
- **IB1 (TF)** `Cancelled_count_counts_orders_cancelled_in_the_period`: baseline; place an order, clock +16 min, `SweepAsync` → Cancelled; today → `baseline + 1` for Admin and Support.
- **IB2 (TF)** clock +2 days: `today` → `0`, `7d` → `1`. A Paid order never counts.

Part B — frontend
- **A1 (TF)** `attention.test.ts` Admin, all five non-zero (`lowStockCount 2`, `cancelledCount 1`, `pendingReturns 3`, dead letters `[{a,2},{b,3}]`, lag `[75,130]`) → five items in D8 order with texts `2 low-stock variants`, `1 cancelled today`, `3 return requests waiting`, `5 failed events`, `Data 130 s behind`; hrefs on the first and third only.
- **A2 (TF)** Support, same data → three items: cancelled, failed events, lag.
- **A3** all zero → `[]`; singulars `1 low-stock variant`, `1 return request waiting`, `1 failed event`.
- **C1 (TF)** wide, ready items → header `Needs attention`; pressing `2 low-stock variants` goes to `/admin/inventory` (`renderShellWithRoutes` pattern).
- **C2** ready `[]` → `All clear.`. **C3** unavailable → `Alerts unavailable right now.`; no prop → no `Needs attention` text.
- **L5** full app, Support, wide, `/admin/orders`, mock with `lowStockCount 2`, dead letters `[{x,1}]` → `findByText('1 failed event')`, `queryByText(/low-stock/)` null.

## Build order — Part A

1. Marco BUSY. Write IA1, B1, B2, G1, G2, B4, L1 first; quote red.
2. Backend: contract → port → store (shared predicate) → reader → service → controller → four fakes. `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~AdminDashboardApiTests" | tail -n 20`; then build the three fake-owning test projects.
3. **Self-check (not a mutation):** in a scratch copy, make `CountLowAsync` reuse the list with `Take(20)` → IA1 red; restore, `cmp`, quote.
4. Frontend: `types.ts` + fixture, `greeting.ts`, `badges.ts`, `hooks.ts`, `AdminShell.tsx`, `_layout.tsx`, `DashboardView.tsx`. Then G3, G4, B3, B5, B6, IA2, L2–L4.
5. **Self-check:** set `refetchInterval` off → L4 red; restore, `cmp`, quote.
6. `tsc`, Jest for touched files, then full `bash scripts/verify.sh 2>&1 | tail -n 30` (one run). Quote `VERIFY`.
7. `slice-plans/slice-P2.13-marco-report.md` (≤ 6 KB): red lines, self-checks, changed test files with reasons, `VERIFY` line. Marco AVAILABLE.

## Build order — Part B (after Part A PASS)

1. Marco BUSY. Write IB1, IB2, A1, A2, C1 first; quote red.
2. Backend as in A; run `AdminDashboardApiTests`, Admin.Tests, Notifications.Tests.
3. **Self-check:** drop the `>= from` bound → IB2 red; restore, `cmp`, quote.
4. Frontend `types.ts` + fixture, `attention.ts`, `AdminShell.tsx` card, `_layout.tsx`; then A3, C2, C3, L5.
5. Full `verify.sh` → `slice-plans/slice-P2.12-marco-report.md`. Marco AVAILABLE.

## Review method

Low tier per part (CLAUDE.md §5): Rob reads `git diff --stat`, the **whole backend diff** (contract placement, `internal`, shared predicate, window + `IClock`, no `var`/anonymous types/cross-module DbSet), the fake edits (additive only), every new/appended test, then the frontend diff (props-only shell, tokens, role hiding). Full `verify.sh` once in the PASS round. No §3 mutations.

## Risks

- Shell polls the dashboard every 60 s on every admin screen (KPI read included for Admin): fine at single-store scale. The shared key also auto-refreshes the dashboard's Today view; a failed poll shows P2.15's notice, by design.
- `order_status_history` has no `(to_status, created_at)` index; backlog if it grows.
- "Today" is the UTC day; the greeting is device-local. They can disagree near midnight.
- `lowStockCount` counts Inventory rows; the list drops variants missing from Catalog, so count ≥ list in that rare case.
- Phone width: badge and card show when the menu is open. Wide `/admin` shows two "Needs attention" blocks (backlog: retire the dashboard one).
- A full-app test outside this plan breaking because the shell now fetches: report it, don't edit it.
