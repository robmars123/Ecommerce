# Slice P2.30 (+ P2.24, P2.32) — Top products restyle · inventory alert banner · quick actions by role

Tracker rows (`docs/admin-dashboard-feature-tracker.md`, tier 2 "MVP: dashboard `/admin` essentials"): **P2.30** "Top products panel restyle", **P2.24** "Inventory alert banner linking to `/admin/inventory`", **P2.32** "Quick actions, hidden by role". All `admin-dashboard`. One slice by owner request; nothing outside these rows. Builder: **Marco**. Mode: Autonomous. Frontend (`frontend/apps/store`) plus **one new backend integration test** (no production backend change). No §12.3 row and no §11.2 numbered test (Phase 2 gap-fill). Conventions carried from `slice-plans/archive/slice-P2.21-plan.md`: `AdminPanel`, token classes only, flex-wrap, P2.46 guard, no copy changes on restyle rows.

**Read:** CLAUDE.md is loaded · app context §4.8 (`docs/ecommerce-app-context.md`, `grep -n "^### 4.8"`, ~14 lines) · arch §5.5 (`05-frontend.md`, `grep -n "^### 5.5"`) · arch §11.3 (`11-testing.md`, `grep -n "^### 11.3"`) · decision log: `grep -n "P2.24" docs/architecture/13.2-decision-log.md` (one row, written for this slice) · quality-bar §2 and §4 · this plan · code: `features/admin-dashboard/components/{DashboardView.tsx, DashboardView.test.tsx}`, `tests/admin-dashboard-fixtures.ts`, `components/AdminPanel.tsx`, `features/auth/admin-access.ts` (`adminRouteDecision`), `features/auth/hooks/useSession.ts`, `features/admin-shell/nav.ts` (`visibleNav`, the pattern to copy), `tests/session.ts` (`signInAs`), `tests/admin-phone-width-guard.test.ts` (PG1–PG3, read only); backend: `backend/tests/Ecommerce.Api.IntegrationTests/Inventory/AdminInventoryAdjustTests.cs` lines 17–31 and 144–155, `backend/src/Modules/Inventory/Ecommerce.Inventory/Api/AdminInventoryController.cs` line 15 (M1 only).

## Risk tier — Low

No endpoint, response shape, money, stock, or state transition changes. **The API does not change.** Role hiding on the dashboard is **display only**; the server is the guard (§3.4 rule 11), and I checked every target:

| Quick action / link | Screen route (client gate) | Server endpoints it uses | Server policy | Existing test proving Support is refused |
|---|---|---|---|---|
| Add product | `/admin/products/new` (Admin) | `POST /api/v1/admin/products` | `AdminProductsController` class-level `AuthorizationPolicies.Admin` | `AdminProductImageApiTests.IU3_upload_needs_an_admin_token` (Support 403, same class attribute) |
| Categories | `/admin/categories` (Admin) | `POST/PUT/DELETE /api/v1/admin/categories` | `AdminCategoriesController` class-level `Admin` | `AdminCategoryApiTests.IC3_put_and_delete_need_an_admin` (`Support` → 403) |
| Inventory, banner | `/admin/inventory` (Admin) | `GET /api/v1/admin/inventory`, `POST …/adjust` | `AdminInventoryController` class-level `Admin` | **None** — only Customer 403 is tested (`Both_endpoints_are_401_for_anonymous_and_403_for_a_customer`). Added here as **S1** |
| Orders | `/admin/orders` (Admin, Support) | `GET /api/v1/admin/orders` | `AdminOrderReadsController` `AdminOrSupport` | `AdminOrderListApiTests.Anonymous_is_401_customer_is_403_without_data_and_support_and_admin_are_200` |
| Returns | `/admin/returns` (Admin) | `GET /api/v1/admin/returns`, decisions | `AdminReturnsController` class-level `Admin` | `ReturnTests` line 172 (Support 403) |

Policies: `IdentityModule.cs:135–136` (`Admin` = RequireRole Admin; `AdminOrSupport` = Admin or Support). **No auth gap** — every endpoint already refuses Support. The only gap is a missing *test* for Support on inventory; S1 closes it and **M1** (Marco, once) proves S1 bites. That is the slice's only mutation. Review = Low tier (CLAUDE.md §5): diff read + Marco's tests + full `verify.sh`, no Rob mutations.

## Role matrix (P2.32, P2.24)

| Element | Admin | Support |
|---|---|---|
| Add product · Categories · Inventory · Returns | shown | hidden |
| Orders | shown | shown |
| Inventory alert banner (when `lowStock` non-empty) | shown | hidden |

Customer and signed-out never reach `/admin` (`AdminGate`); not tested here.

## Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | **No API change.** Banner signal = the dashboard response's existing `lowStock` array (server already filters by `Inventory:LowStockThreshold`, set to 3 in P2.33) | The response already answers "is anything at or below the threshold" | A new count field or endpoint; reading the threshold on the client |
| D2 | **Banner shows iff** `data` is loaded **and** `data.lowStock.length > 0` **and** `adminRouteDecision(role, '/admin/inventory') === null`. Hidden when the list is empty, while loading/error, and for Support | Support cannot open `/admin/inventory` (the gate redirects), so a call to action they cannot take is noise | Showing it to Support with no link |
| D3 | **Banner copy, no count:** heading `Stock is running low`, action text `Review inventory`. The banner is one `Link href="/admin/inventory" asChild` around a `Pressable accessibilityRole="link" accessibilityLabel="Stock is running low. Review inventory"` | The server caps `lowStock` at 20 (`DashboardQueryService.LowStockMax`), so a count would be wrong at the cap. Accessible name is distinct from the `Inventory` quick action, so `getByRole('link', { name: 'Inventory' })` stays unique | `N items low` (lies at 20); `accessibilityRole="alert"` — it would make the frozen needs-attention test's `getAllByRole('alert')` length 3 |
| D4 | Banner placement: inside the `data` block, directly **after** the as-of line and **before** the KPI cards. Classes: `Pressable` `min-h-touch flex-row flex-wrap items-center justify-between gap-sm rounded-card border border-warning bg-surface p-md`; heading `Text` `text-body font-semibold text-warning`; action `Text` `text-small text-primary` | `warning` is an existing theme token (light and dark in `lib/design-tokens.js`); flex-wrap passes PG2 at 375 px | A new token; a hex colour |
| D5 | **Quick actions keep today's five** (`QUICK_ACTIONS`: Add product, Categories, Inventory, Orders, Returns), same labels and hrefs | Labels are existing copy and the frozen test `offers the quick actions` names them; decision log row "Admin categories screen" put Categories here | Renaming to app context §4.8 wording (copy change, breaks a frozen test); adding Customers (backlog 12.2 item — the sidebar from P2.3 now reaches it) |
| D6 | **Role filter = the route gate:** `QUICK_ACTIONS.filter((a) => adminRouteDecision(role, a.href) === null)` where `role = useSession().user?.role ?? null`, inline in `DashboardView` (`QuickActions` component). Same helper for the banner (D2) | One role map for the gate, the sidebar (`visibleNav`) and the dashboard; no second table to drift | A new `quick-actions.ts` module or a per-action `roles` field (one consumer; duplicates `admin-access.ts`) |
| D7 | **KPIs stay server-keyed.** The new `useSession` read feeds only D2 and D6; `data.kpis ? … : null` is unchanged | P2.21 D5: Support's no-revenue rule is the server's (`kpis: null`) | Gating KPIs on role too |
| D8 | **P2.30 restyle:** `<Section title="Top products">` → `<AdminPanel title="Top products">`; each row `Text` class → `border-b border-border py-md text-body text-foreground` (not a link, so no `min-h-touch`); **same string** `${t.name} · ${t.units} sold`; empty text `No sales in this period.` unchanged; same order (server order) | Matches the P2.31 low-stock row style; restyle = classes and wrapper only | Rank numbers, bars, or units in a column (copy/layout additions no row asks for) |
| D9 | **P2.32 panel:** `<Section title="Quick actions">` → `<AdminPanel title="Quick actions">` rendered **only when at least one action is visible** (always true for Admin/Support today; guards an empty panel). Inner `View className="flex-row flex-wrap gap-md"` unchanged; each link `Text` class → `min-h-touch rounded-control border border-border px-md py-md text-body text-foreground` | Reads as an action, tap target kept; same strings | Using `ActionButton` (it is a button role; the frozen test queries `link`) |
| D10 | `Section` stays for Orders by status and Needs attention; period chips, Refresh, as-of line, KPI cards, recent orders and low-stock panel unchanged | Other rows own them; `Section` keeps two consumers | Restyling the rest now |

## Files

| File | Row | Change |
|---|---|---|
| `frontend/apps/store/features/admin-dashboard/components/DashboardView.tsx` | P2.30, P2.24, P2.32 | D8 on the Top products block. New private `LowStockBanner({ show })` or inline JSX per D2–D4. Extract the quick-actions block into private `QuickActions({ role })` per D6/D9. In `DashboardView`: `const role = useSession().user?.role ?? null;`. Imports: `useSession` from `@/features/auth/hooks/useSession`, `adminRouteDecision` from `@/features/auth/admin-access`, `UserRole` type if needed. Nothing else. |
| `frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx` | P2.24, P2.32 | **Append only** the new tests below inside the `describe`. No existing block changes (render map already has `admin/orders/index`, `admin/inventory`). |
| `backend/tests/Ecommerce.Api.IntegrationTests/Inventory/AdminInventoryAdjustTests.cs` | P2.32 | **Append** one `[Fact]` S1. `Fixture` record and `StartAsync` unchanged; S1 adds its Support user inline (`f.Env.AddUserAsync("support@example.test", UserRole.Support)` then `f.Env.AccessTokenAsync(...)`). |

## Freeze list

No edits: `backend/src/**` (except M1's temporary one-line mutation, restored and `cmp`-checked); every other backend test file and every existing test in `AdminInventoryAdjustTests.cs`; `features/admin-dashboard/{api.ts, api.test.ts, types.ts, hooks/**, kpi-cards.ts, kpi-cards.test.ts}`; `features/auth/**` (including `admin-access.ts` and its test — the role map is reused, not changed); `features/admin-shell/**`; `tests/**` including `admin-dashboard-fixtures.ts` (build cases inline with `makeDashboard({...})`), `admin-phone-width*.ts(x)`, `routes.test.tsx`, `session.ts`; `components/**` including `AdminPanel.tsx`; `lib/**`, `tailwind.config.js`, `lib/design-tokens.js` (no new token); `app/**`; `package.json`, lock files (no package); `docs/**` (Rob owns the decision-log row; the tracker is marked Done by Rob/coordinator); `.github/`, `.claude/`. In `DashboardView.tsx`: no visible string or accessible name changes except the new banner (D3). Restyle rules: RN primitives and token utilities only; no `style=`, no hex, no arbitrary `[...]` values. **Every existing test in `DashboardView.test.tsx` stays byte-identical** — in particular `offers the quick actions` (Admin, five labels), the needs-attention test (`getAllByRole('alert')` length 2), and the first test's `Blue Shirt · 9 sold` / `Blue Shirt (BS-M) · 2 left` lines. If a frozen test fails, fix the code, not the test; if you think the test is wrong, stop and report it.

## Required tests

All new; test-first ones marked **(TF)** — write, run, quote red, then implement.

`DashboardView.test.tsx` (append)
- **Q1 (TF)** Support sees only Orders: `signInAs('Support')`, `mockApi(makeDashboard({ kpis: null }))` → `await screen.findByRole('link', { name: 'Orders' })`; `queryByRole('link', { name })` is null for each of `Add product`, `Categories`, `Inventory`, `Returns`. Red today (all five render for every role).
- **Q2** Admin vs Support matrix in one `it.each` over `[['Admin', ['Add product','Categories','Inventory','Orders','Returns']], ['Support', ['Orders']]]`: after `findByText('Quick actions')`, the set of visible quick-action link names (query each of the five; collect those present) `toEqual` the expected list, in that order. Admin side also passes today; Support side is red until D6 — that satisfies "hiding tested per role".
- **Q3** Support's Orders action opens the orders list: press `getByRole('link', { name: 'Orders' })` → `findByText('Orders list screen')`, `toHavePathname('/admin/orders')`.
- **B1 (TF)** Admin, default fixture (one low-stock row) → `findByRole('link', { name: 'Stock is running low. Review inventory' })` visible, `getByText('Stock is running low')` and `getByText('Review inventory')` visible; press it → `findByText('Inventory screen')`, `toHavePathname('/admin/inventory')`. Red today.
- **B2** Admin, `makeDashboard({ lowStock: [] })` → after `findByText('Nothing is running low.')`, `queryByRole('link', { name: 'Stock is running low. Review inventory' })` is null and `queryByText('Stock is running low')` is null.
- **B3** Support, `makeDashboard({ kpis: null })` (default fixture still has a low-stock row) → after `findByText('Orders by status')`, `queryByText('Stock is running low')` is null. Proves D2's role clause; red if the role check is dropped.
- **B4** No banner before data: Admin, dashboard fetch fails (`problemResponse(500, …)` as in the existing error test, recent orders `makeRecentOrdersPage([])`) → after `findAllByRole('button', { name: 'Try again' })`, `queryByText('Stock is running low')` is null.

P2.30: no new test (restyle). Existing coverage stays green unchanged: the first test's `Blue Shirt · 9 sold` line; `tests/admin-phone-width-guard.test.ts` PG1–PG3 (scans the new classes); `tests/admin-phone-width.test.tsx` PW1 `/admin`.

`AdminInventoryAdjustTests.cs` (append)
- **S1** `Both_endpoints_are_403_for_support_and_write_nothing`: start fixture, add a variant (`InventoryEnv.AddVariantAsync(f.Env, "AUTH-2")`), add a Support user and token; `GET /api/v1/admin/inventory` as Support → 403; `f.AdjustAsync(variant, 1, 0, "x", support)` → 403; `InventoryEnv.CountAsync(f.Env, "SELECT count(*) FROM inventory_stock")` → 0 (same assertion as the existing Customer test, line 154). Not red-first (the guard exists); **M1** is its red proof.

**Mutation (one, Marco):** **M1** — `AdminInventoryController.cs:15` `AuthorizationPolicies.Admin` → `AuthorizationPolicies.AdminOrSupport`; run S1 → red; restore from the scratch copy; `cmp` → no output; S1 green. No other mutations (CLAUDE.md §3 "no extras").

## Build order

1. `AGENT_STATUS.md` → Marco BUSY. Read the `Read:` list. Baseline: `cd frontend/apps/store && npx jest features/admin-dashboard tests/admin-phone-width 2>&1 | tail -n 20`; quote the pass count.
2. **S1 + M1 (security evidence first).** Postgres up (`docker compose up -d postgres`). Append S1; run `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~Both_endpoints_are_403_for_support" 2>&1 | tail -n 20` → green; quote. Copy `AdminInventoryController.cs` to the scratchpad; apply M1; re-run the filter → red; quote. Restore from the copy; `cmp` both files → no output; quote. Re-run → green; quote.
3. **P2.32 test-first:** append Q1–Q3; `npx jest features/admin-dashboard 2>&1 | tail -n 20` → Q1 and Q2[Support] red; quote. Implement D6 + D9 → green.
4. **P2.24 test-first:** append B1–B4; run → B1 red (B2–B4 may pass vacuously before the banner exists; that is expected — they are guards for D2); quote. Implement D2–D4 → green. Then temporarily delete the role clause from the banner condition, confirm B3 goes red, put it back (code-level self-check, not a reported mutation; quote the red line).
5. **P2.30:** D8. `npx jest features/admin-dashboard tests/admin-phone-width 2>&1 | tail -n 20` green; first test untouched.
6. `git -c safe.directory='*' diff --stat` and `git -c safe.directory='*' diff -- frontend/apps/store/features/admin-dashboard/components/DashboardView.test.tsx backend/tests` : confirm additions only (no `-` lines in either test file). `git -c safe.directory='*' diff --stat -- backend/src` prints nothing. `npm run format` in `frontend/apps/store`.
7. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line. Fix and re-run until green.
8. Write `slice-plans/slice-P2.30-marco-report.md` (≤ 6 KB): VERIFY line; baseline and final jest counts; S1 green, M1 red, `cmp` output; red quotes for steps 3 and 4 and the B3 self-check; files changed; confirmation that no existing test changed; manual checks below marked "pending Nathaniel". Marco AVAILABLE.

## Review method (Rob)

Low tier, one review. `git diff --stat` + untracked; read the `DashboardView.tsx` diff, the two test-file diffs. Checks:
- `backend/src` diff empty; S1 is appended only; M1 red and `cmp` quoted in Marco's report. No Rob mutations.
- Both test-file diffs are additions only (no removed or edited lines); every frozen block byte-identical.
- Role read used only by the banner condition and `QuickActions`; `data.kpis ? …` unchanged (D7). Filtering goes through `adminRouteDecision`; no hard-coded role names or second role table in the dashboard (`grep -n "'Admin'\|'Support'" DashboardView.tsx` → nothing).
- Banner condition matches D2 exactly (non-empty `lowStock`, data loaded, route allowed); copy and accessible name match D3; no `accessibilityRole="alert"`.
- Restyle (D8, D9): strings byte-identical, classes from tokens only; no `style=`, hex, or `[...]`.
- No dead code; `Section` still used; no new module, package, or token.
Full `verify.sh` in the PASS round only; quote the `VERIFY` line.

## Manual checks (Nathaniel; Marco lists them as pending)

1. `/admin` at 375 px, light and dark: banner wraps, warning border readable in both themes; quick-action links wrap; top-products rows match low-stock rows. No horizontal scroll.
2. Signed in as Support: no banner, only the Orders quick action; Orders opens the list.
3. Admin with no low-stock variants: no banner.

## Risks

- **Duplicate accessible names:** the KPI card `Orders` is a `View` with `accessibilityLabel`, not a link, so `getByRole('link', { name: 'Orders' })` stays unique; the banner's label is deliberately not `Inventory`. If a query finds two, fix the markup, not the test.
- **Support low-stock rows still link to `/admin/inventory`** (P2.31 behaviour; the gate bounces Support back to `/admin`). Out of these rows — Rob adds a NOTE to `slice-plans/backlog.md` at review; do not change it here.
- **`Link asChild` + `Pressable`:** if expo-router's `Link` does not forward the accessible name onto the `Pressable`, keep `accessibilityLabel` on the `Pressable` (it is the child that renders) and query by that name; do not add `role="alert"`.
- **Copy drift:** the banner strings are new copy owned by this plan; any wording change is a plan change, not Marco's call.
