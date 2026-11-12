# Slice P2.34-35-40-33 — Orders list, order detail, inventory restyle + low-stock threshold 3

Tracker rows P2.34, P2.35, P2.40, P2.33 (tier 1), one slice by owner decision; nothing outside these rows. Arch §12.3 "P2 Admin UI (Phase 2)"; app context §4.8, §4.10, §4.11, §4.14, §5; §13.2 rows "Admin order list URL contract (tracker P2.34)", "Stock indicator `Low` threshold", "Prettier for the frontend"; arch §7.4 `Inventory:LowStockThreshold`.

Mode: Autonomous. **Risk tier: Low.** Restyle, URL-driven list status/page, one config value, Prettier. No money, stock, endpoint, auth or state transition; the threshold drives display only (§7.4). Review: tests + `verify.sh` + Rob reads the diff, no mutations. Line-by-line check: `OrderActions.tsx`, `RefundForm.tsx`, `InventoryRow.tsx` change `className` only ("Action freeze", judged by the Prettier review method).

## Read:

- This plan.
- `frontend/apps/store/features/admin-orders/components/AdminOrderListView.tsx` + `.test.tsx`, `AdminOrderDetailView.tsx` + `.test.tsx`, and the root `View` of `OrderActions.tsx` and `RefundForm.tsx`.
- `frontend/apps/store/features/admin-inventory/components/AdminInventoryView.tsx`, `InventoryRow.tsx`, and `AdminInventoryView.test.tsx` (names only).
- `frontend/apps/store/app/admin/orders/index.tsx`, `orders/[id].tsx`, `inventory.tsx`, `app/admin/_layout.tsx`.
- `features/admin-catalog/components/AdminProductListView.tsx` lines 1–60 (URL-param pattern), `lib/design-tokens.js`, `components/ScreenTitle.tsx`.
- `backend/src/Ecommerce.Api/appsettings.json` (`Inventory`); `IntegrationTests/Inventory/InventoryHostTests.cs`, `StockStatusTests.cs`, `Projections/AdminDashboardApiTests.cs` (`Low_stock_lists_…`).
- `scripts/verify.sh`. `docs/quality-bar.md` §1, §3, §4. Nothing else.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **URL contract** — binding, exactly as §13.2 "Admin order list URL contract (tracker P2.34)" (8 case-sensitive `OrderStatus` names, anything else → All; `page` digits ≥ 1 else 1, written only when > 1) | The dashboard links exact enum names; the API takes one status | A comma list for P2.28; lower-case aliases |
| The screen reads status and page **only from the URL** (`useLocalSearchParams`, parsed by a pure function). Status chips write the URL at once: `router.setParams({ status: s === 'all' ? undefined : s, page: undefined })`. The pager writes `page`. Apply writes `page: undefined` and commits the local date, customer and order-number draft | Links and param changes refilter with no sync effect; page resets on status change. Same as inventory/products | Chips as an Apply draft (needs a sync effect); date/customer/number in the URL (not asked) |
| Chips filter at once; text and date fields still wait for **Apply filters**; a chip press doesn't validate or commit that draft | Chips are the URL filter (P2.34); field behaviour stays as tested | Apply committing chips too (two sources) |
| Pure module `features/admin-orders/order-list-params.ts`: `parseOrderListParams(raw: { status?: string \| string[]; page?: string \| string[] }): { status: OrderStatus \| 'all'; page: number }`. The view uses it, and nothing else parses | The contract is tested as a table, the same way as `breadcrumb.ts` and `search.ts` | Inline parsing in the component (untestable edge cases) |
| Route `orders/index.tsx` no longer passes `initialStatus`. The view reads the params itself. Remove the `initialStatus` prop | One source (the URL). Same shape as `AdminProductListView` and `AdminInventoryView` | Keeping the prop as well as the URL |
| **Hide the Stack header** on these three routes only. Each route file renders `<Stack.Screen options={{ headerShown: false }} />` next to its view. `ScreenTitle` stays as it is (its `title` option still sets the web document title, and the options merge) | P2.6 left "two titles" to the restyle rows; the breadcrumb replaces the back link. Per route leaves unrestyled screens alone | In `_layout.tsx` (four screens outside the slice); changing shared `ScreenTitle` |
| New `components/AdminPanel.tsx`: `{ title?: string; children: ReactNode }` → `View className="gap-md rounded-card border border-border bg-surface p-lg"`. When `title` is set, a `Text accessibilityRole="header" className="text-title font-semibold text-foreground"` comes first | 3 screens, 8 call sites; header markup equals today's `Section`, so role queries hold | Repeating the class string; placing it in `features/admin-shell` |
| Restyle rules (all three screens): RN primitives only, token utilities only (`lib/design-tokens.js` via Tailwind). No `style=`, no hex, no arbitrary `[...]` values, no new tokens. **No copy changes**: every visible string and accessible name stays byte-identical | Tier-1 "layout only" (tracker). The footer says existing tests stay unchanged, and they query by text and role | Adding labels or headings, or new tokens |
| P2.33: `appsettings.json` `Inventory:LowStockThreshold` 5 → 3; code default in `InventoryOptions` stays 5; nothing in Development settings or compose | App context §4.8; arch §7.4 | Changing the code default; a compose override |
| Tests that relied on the old **configured** value of 5 keep their intent (the boundary at the threshold and threshold+1) with their numbers moved to 3/4. They don't pin `"5"` in a settings override | They prove the shipped config | A `"5"` override per test |

## Files per row

**P2.34 — Orders list** (`admin-orders`)

| File | Change |
|---|---|
| `features/admin-orders/order-list-params.ts` | **New.** `parseOrderListParams` per the contract. A header comment cites §13.2 "Admin order list URL contract". It reuses the `STATUSES` list (move the constant here and export it, so the view and the parser share one list). |
| `features/admin-orders/order-list-params.test.ts` | **New.** P1–P7 below. |
| `features/admin-orders/components/AdminOrderListView.tsx` | URL-driven status and page (decisions above). The local draft holds `from/to/customer/orderNumber` only. Query: `useAdminOrdersQuery({ ...applied, status }, page)`. `validateFilters`, `AdminOrderFilters`, `hooks.ts` and `api.ts` are **unchanged**. Restyle: root `ScrollView contentContainerClassName="w-full max-w-content gap-lg self-center p-lg"`. Chip row `flex-row flex-wrap gap-sm`, chip `min-h-touch justify-center rounded-pill border px-md`. Selected: `border-primary bg-primary-subtle` + text `text-body font-semibold text-primary-text`. Unselected: `border-border bg-surface` + `text-body text-foreground`. Fields + Apply inside one `AdminPanel` (no title). Results (list / empty / error / loading) inside one `AdminPanel` (no title), each row `border-b border-border` with the same `Link`/`Text` and the same string. The pager stays under the list, with the same `ActionButton`s and labels. |
| `features/admin-orders/components/AdminOrderListView.test.tsx` | **Append** L1–L6. The existing 8 tests stay byte-identical. |
| `app/admin/orders/index.tsx` | `<><Stack.Screen options={{ headerShown: false }} /><AdminOrderListView /></>`. No `useLocalSearchParams`. |

**P2.35 — Order detail** (`admin-orders`)

| File | Change |
|---|---|
| `features/admin-orders/components/AdminOrderDetailView.tsx` | Root becomes a `ScrollView` (the current `View` doesn't scroll, so long orders are cut off inside the shell) with the same content-container classes as the list. The summary block (number, date, status, customer) goes into an `AdminPanel` without a title. Replace the local `Section` with `AdminPanel title=…` for Items, Refunds and Return requests (same titles, same conditional). `OrderActions` and `RefundForm` are rendered exactly as now: same props, same order, no wrapper. |
| `features/admin-orders/components/OrderActions.tsx`, `RefundForm.tsx` | **className only**, on the root `View`: `gap-md` → `gap-md rounded-card border border-border bg-surface p-lg`. No other line changes. |
| `app/admin/orders/[id].tsx` | Add `<Stack.Screen options={{ headerShown: false }} />` before the view. |

**P2.40 — Inventory** (`admin-inventory`)

| File | Change |
|---|---|
| `features/admin-inventory/components/AdminInventoryView.tsx` | Content container `w-full max-w-content gap-lg self-center p-lg`. The rows (and the empty/error/loading state) go into one `AdminPanel` with no title. The title `Admin · Inventory` is unchanged (test asserts it). The pager stays outside the panel. |
| `features/admin-inventory/components/InventoryRow.tsx` | **className only.** For example, the stock-status text becomes a pill `rounded-pill px-sm` keeping its existing colour classes, and the adjust form gets `rounded-control bg-surface-raised p-md`. No change to the `STATUS` texts, handlers, labels, `adjust-model.ts` or hooks. |
| `app/admin/inventory.tsx` | Add `<Stack.Screen options={{ headerShown: false }} />`. |

**Shared**

| File | Change |
|---|---|
| `components/AdminPanel.tsx` + `components/AdminPanel.test.tsx` | **New.** A1–A2 below. |

**P2.33 — threshold** (Inventory, config)

| File | Change |
|---|---|
| `backend/src/Ecommerce.Api/appsettings.json` | `"LowStockThreshold": 5` → `3`. Nothing else. |
| `backend/tests/Ecommerce.Api.IntegrationTests/Inventory/InventoryHostTests.cs` | Rename `The_default_settings_are_a_threshold_of_5_and_a_30_second_sweep` → `The_configured_settings_are_a_threshold_of_3_and_a_30_second_sweep`, and assert `(3, 30)`. **Test-first** (H1). |
| `…/Inventory/StockStatusTests.cs` | `T20_the_reader_classifies…`: `v[0]` stock 6 → **4** (InStock), `v[1]` 5 → **3** (Low). Everything else is unchanged. `T20_a_reservation_moves_the_indicator`: stock 6 → **4** (InStock), and after reserving 1 → 3 (Low). The assertions are unchanged. |
| `…/Projections/AdminDashboardApiTests.cs` `Low_stock_lists_a_variant_at_the_threshold_and_not_one_above_it` | SKU `LOW-5`/stock 5 → `LOW-3`/stock **3**, and `LOW-6`/6 → `LOW-4`/**4**. Update the asserted SKU strings and `(atThreshold, 3)` to match. The test name and intent stay the same. |

No other backend test is edited; if another fails, stop and report it with numbers.

**Docs (Rob):** §13.2 URL-contract row, threshold row and arch §7.4 updated. Tracker rows set Done by the coordinator after PASS.

## Action freeze (P2.35/P2.40, checked by Rob in review)

No action is added or removed. `OrderActions` (role gate, `actionsFor`, mutation), `RefundForm` (submit, validation, idempotency) and `InventoryRow` change root/element `className` only. `adjust-model.ts`, `useAdjustStockMutation.ts`, `order-actions.ts`, `hooks.ts`, both `api.ts` and the existing `OrderActions`/`RefundForm`/`AdminOrderDetailView`/`AdminInventoryView`/`adjust-model` tests are untouched (= `prettier(HEAD)`) and green. Known, out of scope (backlog): `RefundForm` shows for Support. **Do not fix it here.**

## Required tests

Frontend (arch §11.3). No §11.2-numbered or F test is named for these rows. F7/F12/F13 already exist and stay unchanged.

| # | File | Must prove |
|---|---|---|
| P1 | `order-list-params.test.ts` | `{}` → `{ status: 'all', page: 1 }` |
| P2 | ″ | each of the 8 statuses round-trips (`it.each`) |
| P3 | ″ | `'paid'`, `'Bogus'`, `''`, `['Paid','Shipped']` → `'all'` |
| P4 | ″ | page `'2'` → 2; `'0'`, `'-1'`, `'abc'`, `'1.5'`, `''`, `['2','3']` → 1 |
| P5 | ″ | status and page are independent (`{ status: 'Shipped', page: '3' }` → both) |
| L1 ✱ | `AdminOrderListView.test.tsx` | Pressing chip `Shipped` → the last fetch has `status=Shipped&page=1`. `toHaveSearchParams({ status: 'Shipped' })`. The chip has `accessibilityState.selected` true and `All` has false |
| L2 ✱ | ″ | Start at `?status=Paid`, press `All` → `toHaveSearchParams({})`, and the last fetch has no `status` |
| L3 ✱ | ″ | Start at `?status=Paid`, then `act(() => router.setParams({ status: 'Shipped' }))` (`router` from `expo-router`) → the last fetch has `status=Shipped`, and the `Shipped` chip is selected. This is the P2.28/dashboard-link case |
| L4 ✱ | ″ | Start at `?status=Paid&page=2` → the first fetch has `page=2`. Press `Next page` from `/admin/orders` → `toHaveSearchParams({ page: '2' })`. On page 2, pressing a chip removes `page` and fetches `page=1` |
| L5 | ″ | Start at `?status=Paid&page=2`, type a customer, press Apply → the last fetch has `{ status: 'Paid', customer, page: '1' }`, and the URL is `{ status: 'Paid' }` |
| L6 | ″ | Type an invalid To date and press chip `Shipped` → the fetch has `status=Shipped` and no `to`, and no field error is shown (the chip doesn't commit the draft) |
| A1 | `components/AdminPanel.test.tsx` | With `title` → `getByRole('header', { name })` plus children rendered |
| A2 | ″ | Without `title` → no header, children rendered |

✱ = test-first. Write L1–L4 against today's view, run them, and **quote the red** (chips don't write the URL, a param change doesn't refilter, page isn't read from the URL). Then implement.

Backend (xUnit, Testcontainers):

| # | Test | Must prove |
|---|---|---|
| H1 ✱ | `InventoryHostTests.The_configured_settings_are_a_threshold_of_3_and_a_30_second_sweep` | The host's bound `InventoryOptions` is `(3, 30)`. Red first against `appsettings.json` = 5 (quote `Expected: (3, 30) / Actual: (5, 30)`). Then change the config |
| H2 | `StockStatusTests` T20 ×2, `AdminDashboardApiTests.Low_stock_…` (edited per the table) | At threshold (3) → Low / listed; at 4 → InStock / not listed. Run them before your edit and quote the failures that the config change causes, so the edit is shown to be needed |

**Changed tests (reviewed as possibly weakened invariants):** the four backend tests only; each keeps "threshold → Low, threshold+1 → not Low". No frontend test is edited or deleted.

## Build order

1. Set yourself BUSY. **P2.33 first:** edit H1 → run it → quote the red. Change `appsettings.json` → H1 is green. Run the Inventory and Projections integration tests → quote the T20/dashboard failures → apply the H2 edits → green.
2. `order-list-params.ts` + P1–P5 (red → green).
3. L1–L4 appended → quote the red. Implement the URL-driven list → L1–L6 and the existing 8 tests green.
4. `AdminPanel` + A1–A2.
5. Restyle the three views + route files, then the className-only edits. **Now follow "Prettier adoption" build order** (it fixes where steps 5–7 sit).
6. Grep your diff for `style=`, `#[0-9a-f]`, `\[`-arbitrary classes, `var ` (C#): none.
7. Full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote `VERIFY`. Report `slice-plans/slice-P2.34-marco-report.md` (≤ 6 KB): reds, changed tests with before/after numbers, files.
8. Manual check (you can't automate it, so list it as open for Nathaniel): at 1280 px and 375 px, `/admin/orders`, `/admin/orders/<id>` and `/admin/inventory` show one title (no Stack header), no horizontal scroll, and the detail scrolls to the refund form.

## Risks

- **Stray fetch on Apply** (`setApplied` + `setParams` in one handler may render once with the old page): acceptable; tests assert the last call. No sync effect.
- **`setParams` and `undefined`:** keep both keys in every call; L2/L4 prove removal.
- **Header hidden on native:** the breadcrumb and OS back replace the back button. Report a dead end on iOS; don't touch `_layout.tsx`.
- **`getByText` collisions:** scope new queries by role.
- **Scope creep:** no new copy, P2.28 buckets, URL for date/customer, Support refund fix, `_layout.tsx`/`ScreenTitle`/other screens.
- One `verify.sh`/integration run at a time.

## Prettier adoption (amendment, owner request)

Nathaniel asked to adopt Prettier. This replaces Marco's ad hoc, config-less run. Risk tier stays **Low** (tooling + formatting; no runtime change).

| Decision | Reason | Alternative rejected |
|---|---|---|
| **Package (rule 16, §13.2 "Prettier for the frontend"):** `prettier` **3.10.0**, exact pin, `devDependencies` of `frontend/apps/store` only (`npm install --save-dev --save-exact prettier@3.10.0`) | Owner request. It replaces manual formatting; `.editorconfig` covers C# only | ESLint/`eslint-plugin-prettier` (not asked); a root `package.json` (no workspace exists) |
| **`frontend/apps/store/.prettierrc`** (JSON): `{ "printWidth": 140, "singleQuote": true, "semi": true, "trailingComma": "all", "tabWidth": 2, "bracketSameLine": false, "arrowParens": "always", "endOfLine": "auto" }` | Measured on `HEAD` (293 TS/TSX files), files changed: width 100 → 184, 120 → 136, **140 → 86**, 160 → 83. Other values match the code as it is. `endOfLine: auto`: this checkout has `core.autocrlf=true` + `* text=auto`, so working files are a mix of CRLF and LF while the repo stores LF. `auto` keeps each file's ending and git normalises on commit | `lf` (fails `--check` on any CRLF checkout); width 160 (3 fewer files, much longer lines) |
| **`frontend/apps/store/.prettierignore`:** `node_modules/` `dist/` `web-build/` `.expo/` `coverage/` `assets/` `*.json` `expo-env.d.ts` `nativewind-env.d.ts` | `*.json` covers `package(-lock).json`, `app.json`, `tsconfig.json`, which npm/Expo rewrite in their own layout . The `.d.ts` files are generated | Formatting JSON too (churn that the next `expo install` undoes) |
| **Scripts:** `"format": "prettier --write ."`, `"format:check": "prettier --check ."` | Run from the app dir | — |
| **`scripts/verify.sh`:** `step_format() { cd "$app" && npm run format:check; }`; `summary_of` gets `format) echo 'Prettier code style\|Code style issues' ;;`; `format` goes in the default list after `hygiene` and in the usage comment; `changed_steps` adds it with the frontend steps (`s+=(format tsc jest export)`). `.github/workflows/` is **not** touched | A failure is fixed by `npm run format`, never argued in review; Rob still never raises formatting (quality bar §3) | A CI gate (needs Nathaniel's written request for workflows) |

Docs (done by Rob): §13.2 new row "Prettier for the frontend"; §13.2 row "Lint / format in CI" and arch §11.4 "Not gated" now name this exception. **For the coordinator:** CLAUDE.md §7's `verify.sh` line doesn't list `format ✔`; Nathaniel decides whether to add it.

**Build order:**
0. **Undo the ad hoc run first.** Prettier keeps an object multi-line if it was written that way (`objectWrap: preserve`), so the width-80 run's expansions never collapse back. For every frontend file the plan says stays untouched (`app/admin/_layout.tsx`, `admin-orders/api.ts`, `api.test.ts`, `hooks/hooks.ts`, `order-actions.ts`, `types.ts`, `AdminReturnsView*`, `AdminOrderDetailView.test.tsx`, `RefundForm.test.tsx`, any other), overwrite it with `git show HEAD:<path> > <path>` (a file write, not a git write). `OrderActions.tsx`, `RefundForm.tsx`, `InventoryRow.tsx`: same, then re-apply the className edit. `AdminOrderListView.test.tsx`: same, then re-append L1–L6 (copy them to your scratch dir first).
1. Install (exact pin), `.prettierrc`, `.prettierignore`, scripts, `verify.sh` step. `bash scripts/verify.sh format` fails (expected).
2. `npm run format` on the whole app, nothing else in that step.
3. Finish the remaining work; `npm run format` again.
4. Self-check with the review method: the three className-only files show only `className` words; every untouched file shows an **empty** diff. "Byte-identical" in this plan now means "identical to `prettier(HEAD)`".
5. Full `verify.sh`. Report the format-only files as a count, plus the files with real changes.

**Review method (Rob and Marco).** From `frontend/apps/store`, for each modified file `f`:

```
git -c safe.directory='*' show HEAD:frontend/apps/store/$f | npx prettier --stdin-filepath "$f" > "$SCRATCH/head.fmt"
git -c safe.directory='*' diff --no-index -w --ignore-cr-at-eol --word-diff=plain "$SCRATCH/head.fmt" "$f"
```

The same `.prettierrc`/ignore apply, so only semantic changes are left; a className edit that re-wraps a tag still shows only the changed words. New files are reviewed as they are.

**Risks:** about 86 format-only files hide real changes, so the review method is mandatory. `package-lock.json` changes only for `prettier`; `expo-check` stays green. No other package.
