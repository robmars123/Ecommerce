# Slice P2.6 — Breadcrumb in the top bar (`admin-shell`)

Tracker row P2.6 (`docs/admin-dashboard-feature-tracker.md`). Arch §12.3 row "P2 Admin UI (Phase 2)". App context §4.14 (Shell: "The top bar has a breadcrumb…").
Mode: Autonomous. **Risk tier: Low (confirmed)** (CLAUDE.md §5) — display and in-app links only: no endpoint, no data fetch, no token/claims change, no auth rule added or changed. Crumb links only point at routes that `AdminGate` already governs, and test B6 proves a role never gets a crumb to a page it can't open. Review = Marco's tests + `verify.sh` + Rob reads the diff; no mutations.

## Read:

- This plan.
- `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` and `AdminShell.test.tsx` (S1–S18, helpers `renderShell`, `renderShellWithRoutes`).
- `frontend/apps/store/features/admin-shell/nav.ts` and `nav.test.ts` (style for the pure test file).
- `frontend/apps/store/features/auth/admin-access.ts` (`adminRouteDecision`), used only by test B6.
- `docs/quality-bar.md` §1, §3, §4.
- Nothing else. No backend files. Don't fold in the open P2.3/P2.4 backlog manual checks.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| A pure function `breadcrumbFor(pathname): Crumb[]` in `features/admin-shell/breadcrumb.ts`, plus an internal `Breadcrumb` component in `AdminShell.tsx` that reads `usePathname()` | The route-to-label mapping is the only logic, so it is tested as a plain table. The component follows the `Brand`/`SidebarFooter` convention (internal to the shell file) | Each screen sets its own crumbs through context or `Stack.Screen` options. That touches 11 screens and adds a provider with one job |
| Root crumb is **Dashboard** (`/admin`), not "Admin" | It matches the sidebar label. "Admin" would collide with the role text in the footer (S13/S17 `getByText('Admin')`) and says nothing extra | Root "Admin" or "Home" |
| Section labels come from `ADMIN_NAV` items with `ready: true` (match on `href`). Detail labels come from a small static table in `breadcrumb.ts` | Renaming a nav item renames its crumb, and Analytics/Settings get crumbs automatically once they are ready. Only 4 detail labels are new strings | A second, separate label table for sections, which would drift from the nav |
| Dynamic segments get a **fixed** label: `Order details`, `Product details`, `Customer details`. `products/new` → `New product`, matched **before** `:id` | The shell must not fetch data, because that would add a request and an auth path to every admin page. The detail screen already shows the number/name. Raw IDs (GUIDs) are noise | Render the raw ID segment; fetch the order number/product name in the shell |
| Path segments are **never** rendered. Labels only come from the nav or the static table | URL text is user-controlled. A label set cannot show anything unexpected | Title-casing unknown segments |
| Unknown or non-admin paths → `[]`, and `Breadcrumb` renders nothing. This covers `/`, `/admin/foo`, `/admin/orders/a/b`, `/admin/categories/x`, empty segments, and not-ready nav routes (`/admin/analytics`, `/admin/settings`) | It never shows a wrong "current page". Expo Router sends real unknown URLs to not-found anyway | Showing `Dashboard` as current on an unknown page (false), or a guessed label |
| Normalise: strip trailing `/` (except a bare `/`). Ignore the query string (`usePathname` already excludes it) | `/admin/orders/` and `/admin/orders?status=Paid` must show the same crumbs as `/admin/orders` | Strict matching, which would show nothing for a trailing slash |
| The last crumb is the current page: plain `Text`, not a link. Earlier crumbs are `Link asChild` + `Pressable accessibilityRole="link"` with `min-h-touch` | This is the usual breadcrumb pattern. The touch target meets the existing token | Making every crumb a link, so the current page links to itself |
| Separator `›` is a `Text` with `accessibilityElementsHidden` + `importantForAccessibility="no-hide-descendants"` | Screen readers read only the crumb names. The container has `accessibilityLabel="Breadcrumb"` and is the stable test query (no `testID`) | `/` as an announced character |
| **Wide:** add a top bar to the content column, above `children` (`border-b border-border bg-surface px-lg`), that holds the breadcrumb. It always renders, because P2.7 search and P2.9 pill go here | The wide layout has no top bar today, and §4.14 puts the breadcrumb in one | Putting it in the sidebar, or relying on the Stack header |
| **Phone:** the existing top bar becomes two rows. Row 1 is unchanged (`Menu` + `Store Admin`). Row 2 is the full breadcrumb, `flex-row flex-wrap`. It is visible without opening the menu | Ancestor links matter most on phones, where the sidebar is hidden. The longest trail ("Dashboard › Customers › Customer details", about 40 chars) fits one line at 390 px and wraps on 320 px instead of scrolling sideways. S2/S17 stay valid | A "compact" prop that shows only the current crumb (an extra variant, and you lose the parent link). Squeezing it into row 1 (overflows at 390 px) |
| No role filtering of crumbs | Every ancestor of a page a role can open is also openable: Support's pages are `/admin`, `/admin/orders/**`, `/admin/customers/**`, whose ancestors are `/admin`, `/admin/orders`, `/admin/customers`. B6 locks this so a future route can't break it silently | Calling `adminRouteDecision` per crumb at render, which is dead logic today |
| Theme tokens only (`text-body`, `text-caption`, `text-muted`, `text-foreground`, `font-bold`, `bg-surface`, `border-border`, `gap-xs`, `px-lg`, `py-xs`, `min-h-touch`). No `style` props, hex or rgb | P2.1 tokens give light and dark mode for free (arch §5.5) | Inline colours |
| No decision-log row | This is presentation of routes that already exist. The docs have no design gap here | — |

## Files

| File | Change |
|---|---|
| `frontend/apps/store/features/admin-shell/breadcrumb.ts` | **New.** `export type Crumb = { label: string; href: string }`; `export function breadcrumbFor(pathname: string): Crumb[]`. Imports `ADMIN_NAV` from `./nav`. |
| `frontend/apps/store/features/admin-shell/breadcrumb.test.ts` | **New.** B1–B7 below. |
| `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` | Add internal `Breadcrumb()` (uses `usePathname` from `expo-router` and `breadcrumbFor`; returns `null` for `[]`). Wide branch: wrap content column as `top bar + children`. Phone branch: second row in the top bar. Sidebar, footer, overlay unchanged. |
| `frontend/apps/store/features/admin-shell/components/AdminShell.test.tsx` | Add `describe('AdminShell breadcrumb')` with S19–S25 and one helper `renderShellAt(width, url)`. S1–S18 unchanged. |
| `docs/admin-dashboard-feature-tracker.md` | Row P2.6 → Done only after Rob's PASS. |

No new packages, tokens, hooks, providers, endpoints or screens. No backend change.

### `breadcrumbFor` contract (exact output)

`D` = `{ label: 'Dashboard', href: '/admin' }`. `:id` = any single non-empty segment other than `new` where noted.

| pathname | result |
|---|---|
| `/admin` | `[D]` |
| `/admin/orders` · `/admin/products` · `/admin/categories` · `/admin/inventory` · `/admin/returns` · `/admin/customers` | `[D, {nav label, href}]` |
| `/admin/orders/:id` | `[D, Orders, { label: 'Order details', href: pathname }]` |
| `/admin/products/new` | `[D, Products, { label: 'New product', href: '/admin/products/new' }]` |
| `/admin/products/:id` (`:id` ≠ `new`) | `[D, Products, { label: 'Product details', href: pathname }]` |
| `/admin/customers/:id` | `[D, Customers, { label: 'Customer details', href: pathname }]` |
| anything else | `[]` |

The section label is the `ADMIN_NAV` item whose `ready` is true and whose `href` equals `/admin/<section>`. `Dashboard` is the ready item with `href === '/admin'`. Keep it to one function plus one small detail table. No class, no route-tree abstraction.

### `Breadcrumb` render contract

- `<View accessibilityLabel="Breadcrumb" className="flex-row flex-wrap items-center gap-xs">`.
- Ancestors are links (`text-body text-muted`). The last crumb is `Text` (`text-body font-bold text-foreground`, `numberOfLines={1}`). Put `›` (`text-caption text-muted`, hidden from accessibility) between crumbs.
- Links use `Link href={crumb.href as never} asChild`, the same pattern as the sidebar. No `onPress` side effects.

## Build order

1. Write B1–B7 and S19–S25 first. Run `npx jest features/admin-shell 2>&1 | tail -n 20` and quote the red output in the report. It fails because the module doesn't exist and there's no `Breadcrumb` label.
2. Implement `breadcrumb.ts` (B green).
3. Implement `Breadcrumb` and the wide/phone top-bar wiring (S19–S25 green). Confirm S1–S18 pass untouched, especially S2, S7 and S17.
4. Check theme tokens: `git -c safe.directory='*' diff -- frontend/apps/store/features/admin-shell | grep -nE "#[0-9a-fA-F]{3,8}|rgb|style=" ` returns nothing. Quote it.
5. Run the full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line, and write `slice-P2.6-marco-report.md` (≤ 6 KB). Say whether you eyeballed :8081 at 1280 and 390 px (wide bar height, phone wrap).

## Required tests (Jest). All test-first.

`breadcrumb.test.ts` (pure):

| ID | Test |
|---|---|
| B1 | `it.each` over every row of the contract table (all 6 sections + `/admin`): exact `toEqual` on labels **and** hrefs. |
| B2 | Dynamic: `/admin/orders/3f2b…-guid` → last label `Order details`, href = the pathname. Same for products and customers. `/admin/products/new` → `New product` (not `Product details`). |
| B3 | Unknown → `[]` (`it.each`): `/`, `''`, `/products`, `/admin/foo`, `/admin/orders/a/b`, `/admin/categories/x`, `/admin/inventory/x`, `/admin/returns/x`, `/admin//`, `/admin/analytics`, `/admin/settings`, `/administrator`. |
| B4 | Trailing slash: `/admin/`, `/admin/orders/`, `/admin/orders/abc/` equal their slash-free results. |
| B5 | No path segment ever appears as a label: for the dynamic inputs in B2, no crumb label contains the ID string. |
| B6 | Role invariant: for `Admin` and `Support`, and for every non-empty result path in B1/B2, if `adminRouteDecision(role, path) === null` then every crumb `href` also gets `null`. |
| B7 | Single source: for every `ADMIN_NAV` item with `ready: true`, `breadcrumbFor(item.href)` ends with `{ label: item.label, href: item.href }`. |

`AdminShell.test.tsx` adds the helper `renderShellAt(width, url)`. It mocks the width like the existing helpers and calls `renderRouter` with the shell mounted on `admin/index`, `admin/orders/index`, `admin/orders/[id]`, `admin/products/new`, `admin/customers/[id]`, each `<AdminShell><Text>Body</Text></AdminShell>`, using `initialUrl: url`. Query inside `within(screen.getByLabelText('Breadcrumb'))`, because the sidebar also has a `Dashboard` link.

| ID | Test |
|---|---|
| S19 | Wide, Admin, `/admin/orders/abc-123`: Breadcrumb has links `Dashboard` and `Orders`, and `Order details` is shown but is **not** a link. `abc-123` is rendered nowhere. Sidebar and `Body` still render. |
| S20 | Wide: pressing the `Orders` crumb → `toHavePathname('/admin/orders')`. The breadcrumb now has link `Dashboard`, and `Orders` is text, not a link. |
| S21 | Wide, `/admin`: Breadcrumb has exactly one crumb, `Dashboard`, with no links inside the Breadcrumb. |
| S22 | Narrow (390), `/admin/customers/xyz`: Breadcrumb is visible **without** opening the menu. `Open menu` is collapsed, `Store Admin` is rendered, and `Admin navigation` is absent. `Customer details` has `numberOfLines === 1`. The Breadcrumb container class contains `flex-wrap`. |
| S23 | Narrow, `/admin/products/new`: pressing `Products` navigates to `/admin/products` (route added to the helper) and the overlay stays closed. |
| S24 | Support, wide, `/admin/orders/abc`: links `Dashboard` and `Orders` present, current `Order details`. |
| S25 | Non-admin path (existing `renderShell(1024)` at `/`): `queryByLabelText('Breadcrumb')` is null, and the brand and `Body` still render. |

Existing S1–S18 must pass unchanged. Any edit to them is a reviewed change and must be justified in the report.

## Risks

- **Two titles on wide screens:** the Stack header still shows the screen title under the new top bar. That's accepted for this row. The screen-restyle rows (P2.31+) decide header visibility, and you don't hide the Stack header here.
- **`getByText` collisions:** `Dashboard`, `Orders` and `Products` now appear in both the sidebar and the breadcrumb on admin paths. New tests must scope with `within(...)`. Existing tests render at `/` (no breadcrumb), so they are unaffected. If any existing test fails for this reason, stop and report it. Don't edit it silently.
- **`renderRouter` and `usePathname` for dynamic routes:** if `admin/orders/[id]` doesn't resolve in the test router, add `admin/_layout`-free flat keys as in `renderShellWithRoutes`. Don't mock `usePathname`, because the real hook is what's under test.
- **Web/native geometry:** Jest can't check the bar height or wrap. Marco checks the web export, and the manual 1280/390 px check goes to `backlog.md` as a P2.6 line for Nathaniel.
- **Scope creep:** no search box, no pill, no badges, no Stack-header changes, and no fetched order numbers in this slice.
