# Slice P2.47 + P2.46 — Marco report

Concerns: None blocking. (1) The theme is a CSS-variable switch: native proof of `var()` resolution is manual check 3, pending Nathaniel. (2) `InventoryRow.tsx:112` (single `Adjust <sku>` button in a plain `flex-row`) is left: one child, bounded by SKU length; only manual check 1 can confirm a very long SKU.
Bugs found: None | Bugs fixed: None | Bugs open: None
Tests changed (existing):
- `lib/design-tokens.test.ts` T2 rewritten as the plan specifies (`colors` export removed by D3; now asserts both themes have each LEGACY key, `colorVariables[key] === 'var(--color-<key>)'`, key sets equal). T1, T3 untouched. Red before rewrite: `● design tokens › T2: colors is the dark theme ...` (1 failed, 2 passed).
- `AdminShell.test.tsx`, `TopNav.test.tsx`: new `describe` appended (S-T1, N-T1) plus one import line each. Diff is additions only (+19/-0, +13/-0).
No other existing test touched; no existing test broke from the new toggle.

## Spike gate (build step 2) — passed
`npx expo export --platform web` exported; emitted CSS `dist/_expo/static/css/web-4f6b0257...css`; `grep -o "background-color:var(--color-background)"` -> `background-color:var(--color-background)`.

## Red output (test-first)
- TH1-TH6 `lib/theme.test.ts`: `Cannot find module './theme' from 'lib/theme.test.ts'` -> implemented -> 16 passed (ios+web).
- TG1/TG2: `Cannot find module './ThemeToggle'` (ios, android).
- TR1/TR2/TL1: failed before `ThemeRoot` StatusBar/`LoadingState` change (suite total "5 failed, 6 passed"); now green.
- TR2 lives in `ThemeRoot.test.web.tsx` and uses react-dom into jsdom (the repo pattern in `lib/platform.test.web.tsx`; RNTL cannot render RNW output). It reads `style.getPropertyValue('--color-background')`.
- PG1-PG3 guard sanity check: scratch `w-[400px]` in `components/LoadingState.tsx`: `PG2 ... "components/LoadingState.tsx:8: <View className=\"w-[400px] items-center p-xl\">"` -> 1 failed; restored, `cmp` identical.
- PG1-3 and PW1 were green on first run on current code (guard found no existing overflow constructs).

## P2.46 sweep (files edited)
Added `flex-wrap` to:
- `features/admin-dashboard/components/DashboardView.tsx:104` (pager)
- `features/admin-orders/components/AdminOrderListView.tsx:146` (pager)
- `features/admin-orders/components/AdminReturnsView.tsx:108` (pager), `:44` (Approve/Decline)
- `features/admin-shell/components/AdminShell.tsx:80` (sidebar header: Brand + Close in a 240 px sidebar, about 208 px inner)
Checked, left (already `flex-wrap`, or content bounded): all `flex-row flex-wrap` rows in AdminProductDetailView:56, AdminProductListView:65/80, CategoryRow:62/83/98, AdminCustomerDetailView:65/74, DashboardView:141/197, InventoryRow:87/104, AdminOrderDetailView:31/44, AdminOrderListView:77, AdminReturnsView:72, OrderActions:99, AdminShell Breadcrumb:47, TopNav:46/47/56. Non-wrapping but bounded: VariantRowsEditor:73 (label + Switch), InventoryRow:112 (see Concerns), AdminSearch:65 (`flex-1` input + Clear), AdminShell:17 Brand, :51 crumb, :116 disabled nav item (`Soon`), :141/:184 layout containers, :146 wide top bar (>= 768 only), :166 phone top bar (Store Admin is `flex-1`).

## Changed (frontend/apps/store)
New: `lib/theme.ts`, `components/ThemeRoot.tsx`, `components/ThemeToggle.tsx`; tests `lib/theme.test.ts`, `components/ThemeToggle.test.tsx`, `components/ThemeRoot.test.tsx`, `components/ThemeRoot.test.web.tsx`, `components/LoadingState.test.tsx`, `tests/admin-phone-width.test.tsx`, `tests/admin-phone-width-guard.test.ts`.
Edited: `lib/design-tokens.js` + `.d.ts` (`colorVariables`, `colors` removed), `tailwind.config.js`, `app/_layout.tsx` (ThemeRoot, `useThemeColors`, StatusBar moved into ThemeRoot; Prettier re-indented the wrapped block), `app/admin/_layout.tsx`, `components/{LoadingState,TextField,TopNav}.tsx`, `features/admin-shell/components/{AdminSearch,AdminShell}.tsx`, `features/catalog/components/{SearchBar,QuickAddPicker}.tsx`, plus the three sweep files above.
Untouched per freeze: backend, package files, babel/metro/global.css, routes.test.tsx, jest-setup, docs, tracker. `grep import { colors ...` returns nothing.

## Jest counts
Before: 193 suites / 1503 tests. After: 206 suites / 1564 tests (+13 / +61, the ios/android/web projects multiply the counts).

## verify.sh
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, tsc clean, jest 206 suites / 1564 passed, export OK. `npm run format` run first.

## Quality-bar §2
- [x] Tests for new behaviour (TH, T2, TG, TR, TL, S-T1, N-T1, PW1, PG1-3), red quoted.
- [x] No new packages; no backend; no secrets; no hex outside `design-tokens.js`.
- [x] Docs/tracker untouched (Rob/coordinator own them).
- N/A money/stock/auth mutations (Medium tier, no §3 mutations).
- Pending Nathaniel: manual checks 1-3 (375 px both themes with `scrollWidth <= innerWidth`; reload persistence + QuickAddPicker in light; Expo Go kill and reopen, status bar readable).

## Round 2 — Marco
1. Long labels: `components/ActionButton.tsx:20` Pressable gets `max-w-full`, so `Adjust <64-char SKU>` and `Edit/Delete <100-char name>` stay inside the row. `tests/admin-phone-width.test.tsx` now returns one category named with 100 chars (CategoryRow mounts with the long label on `/admin/categories`; the inventory mock already uses a 100-char SKU). Jest has no layout engine, so there is no red here; it stays a manual check. Manual check 1 now also needs a 64-char SKU on Inventory and a 100-char category on Categories, `scrollWidth <= innerWidth`, both themes.
2. PG1: regex is now `/(?:^|\s)horizontal(?=\s*(?:=|\/?>|$))/` per line, so a prop on its own line is flagged. Red: CategoryFilter lines 14-20 appended to `components/LoadingState.tsx` -> `PG1 ... "components/LoadingState.tsx:17: horizontal"` (android and ios failed). Restored from scratch copy, `cmp` identical. Old regex would have passed this.
- Jest (guard, width, ActionButton, admin-catalog, admin-inventory): 24 suites, 170 tests passed.
- `verify.sh changed`: `VERIFY PASS (secrets hygiene format tsc jest export)`.
