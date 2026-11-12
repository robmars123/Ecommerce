# Slice P2.47 (+ P2.46) — Light and dark mode app-wide · phone width without horizontal scroll

Tracker rows: **P2.47** "Light and dark mode on every admin screen" and **P2.46** "Phone width: no horizontal scroll" (`docs/admin-dashboard-feature-tracker.md`, tier 1, "close tier 1 as a sweep"). Gap-fill, no §12.3 row and no §11.2 numbered test. Builder: **Marco**. Mode: Autonomous. Frontend only (`frontend/apps/store`).

**Read:** CLAUDE.md is loaded · arch §5.5 (`docs/architecture/05-frontend.md`, `grep -n "^### 5.5"`, includes the new "Light and dark" bullet) · arch §11.3 (`11-testing.md`) · decision log: `grep -n "P2.46–P2.47\|P2.1–P2.2" docs/architecture/13.2-decision-log.md` · quality-bar §2 and §4 · this plan · code: `lib/design-tokens.js` + `.d.ts` + `.test.ts`, `tailwind.config.js`, `app/_layout.tsx`, `app/admin/_layout.tsx`, `features/cart/guest-cart-store.ts` (the persist pattern to copy), `features/admin-shell/components/AdminShell.tsx` + test (harness at lines 15–30), `components/TopNav.tsx`, `tests/routes.test.tsx` (fetch map to copy), then each file in "Files" as you touch it.

Out of scope: any backend; the storefront scrims `bg-black/60` (`app/login.tsx`) and `bg-black/50` (QuickAddPicker) colour choice (backlog note); restyling screens beyond what 375 px needs; tracker status edits (the coordinator flips both rows to Done after Rob's PASS).

## Risk tier — Medium

No money, stock, payment, idempotency, auth or state transition, so not High and no §3 mutations. Not Low either: it changes the root layout every route renders under, rewires how **every** colour class compiles (Tailwind → CSS variables), and adds persisted client state. A mistake shows as an unreadable app, not a wrong charge. Review = full diff read + the export CSS check + full `verify.sh` (see "Review method").

## Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | Owner (Nathaniel, 2026-11-12): **in-app toggle**, not OS; persists across reloads/restarts; **whole app** follows it. Logged in §13.2 (row "Light and dark mode, app-wide"), which revisits the P2.1–P2.2 row. | Owner decision. | Following `useColorScheme` (owner). |
| D2 | Mechanism: **NativeWind `vars()` CSS variables.** Tailwind colour `bg-background` compiles to `var(--color-background)`; one root `View` sets `--color-*` from `themes[mode]`. Screens keep their classes unchanged. | One token source (§5.5); zero class churn across ~60 screen/component files; NativeWind 4 resolves `var()` at runtime on native and as real CSS variables on web (`react-native-css-interop` `vars`, checked in `node_modules`). | `dark:` variants: every coloured class doubled in every file, two palettes in markup, NativeWind class-mode setup. Variables in `global.css` `:root`/`.dark`: hex values duplicated outside the token file. |
| D3 | `lib/design-tokens.js` exports `colorVariables` = `{ <key>: 'var(--color-<key>)' }` built from `Object.keys(dark)`; `tailwind.config.js` uses it for `colors`. **`colors` export removed** (no consumer left after D4). `themes` unchanged. | Tailwind and the runtime share one key list; dead export would be blocking. | Keep `colors` for T2 only (dead code). |
| D4 | JS-prop colours read `useThemeColors()` (returns `themes[mode]`): root Stack `contentStyle`, admin Stack `contentStyle`/`headerStyle`/`headerTintColor`, `LoadingState` `ActivityIndicator color`, `placeholderTextColor` in `TextField`, `AdminSearch`, `SearchBar`. | These props take a colour value, not a class (§5.5 exception); a CSS variable string does not work there on native. | Passing `'var(--…)'` strings to JS props (breaks on native). |
| D5 | Store `lib/theme.ts`: `createThemeStore()` + `useThemeStore` singleton, zustand `persist` + `createJSONStorage(() => AsyncStorage)`, name `theme`, version 1, state `{ mode: 'dark' \| 'light', toggle() }`, `partialize` to `mode`. Custom `merge`: a stored `mode` that is not exactly `'dark'`/`'light'` is ignored (stays `'dark'`). Default `'dark'`. Not cleared on sign-out. | Both packages are already dependencies and this is the guest-cart pattern (`features/cart/guest-cart-store.ts`); AsyncStorage is localStorage on web, device storage on native. A theme is a device preference, not user data. Bad stored data must never index `themes` with an unknown key (white-screen crash). | Any new package (none added). `expo-secure-store` (for secrets, not prefs). |
| D6 | `lib/theme.ts` also exports `themeVariables(mode)` → `{ '--color-<key>': themes[mode][key] }` for every key, `useThemeColors()`, and `useThemeStyle()` = `vars(themeVariables(mode))`. | One place builds the variable names; T2 pins that it agrees with `colorVariables`. | — |
| D7 | `components/ThemeRoot.tsx`: `<View style={useThemeStyle()} className="flex-1 bg-background">` + `<StatusBar style={mode === 'dark' ? 'light' : 'dark'} />`. Wraps everything inside `QueryClientProvider` in `app/_layout.tsx`, replacing the current `<StatusBar style="light" />`. | Every route and both Stacks are descendants. `vars()` output is a runtime value (§5.5 allowed `style`). StatusBar text must stay readable on the light background. | Variables per screen. |
| D8 | `QuickAddPicker`'s outer `View` inside `<Modal>` also gets `style={useThemeStyle()}`. | On web RN `Modal` portals to `document.body`, outside the root `View`, so it would not inherit the variables (its card would lose all token colours). The only `Modal` in the app (grep `<Modal`). | Setting variables on `document.documentElement` (platform code outside `lib/platform`). |
| D9 | **Toggle placement (D1 "storefront too"):** one `components/ThemeToggle.tsx`, placed in the admin shell top bar (wide: after the search; narrow: right end of the Menu / "Store Admin" row) and in the storefront `TopNav` (right group, before Cart). One preference drives both areas. Admin never renders `TopNav`, so no screen shows two toggles. | Minimal that satisfies both owner decisions: the shell has the toggle, and a customer (who never sees the shell) can still switch. One component, one store, two one-line placements. | Admin-only toggle (customers stuck on dark); a storefront settings screen (new route, out of scope). |
| D10 | Toggle UI: `Pressable`, `accessibilityRole="switch"`, `accessibilityLabel="Dark mode"`, `accessibilityState={{ checked: mode === 'dark' }}`, visible text `Dark` / `Light` (current mode), `min-h-touch min-w-touch` + token classes only. | Testable by role/name/state (§11.3); fixed name, state carries the value. | RN `Switch` (track colours are more JS props). |
| D11 | P2.46 test method: (a) a **static guard** over admin source, (b) a **phone-width render** of every admin route at 375, (c) a **manual** browser check. No new class-string assertions. | Jest has no layout engine, so overflow itself cannot be measured; §5.5 forbids class-string assertions. The guard catches the regressions that cause horizontal scroll by construction; the render proves each route mounts in the phone shell. | Playwright/visual tests (new package, §11.3 "not in v1"). |
| D12 | Cold start paints dark until the stored value loads (async). Accepted. | No new code or package; on web it resolves within a tick. | Blocking render until hydration (blank screen on slow storage). |

## Files

### P2.47 — theme

| File | Change |
|---|---|
| `lib/design-tokens.js` | Add `colorVariables` (D3); remove `colors: dark`; update the two comments that mention `colors`/"Light has no consumer". |
| `lib/design-tokens.d.ts` | Replace `colors` with `colorVariables: TokenMap`. |
| `tailwind.config.js` | `colors: tokens.colorVariables`. Nothing else. |
| `lib/theme.ts` (new) | D5, D6. `ThemeMode` type exported. |
| `components/ThemeRoot.tsx` (new) | D7. |
| `components/ThemeToggle.tsx` (new) | D10. |
| `app/_layout.tsx` | Wrap in `ThemeRoot`; drop `StatusBar` import/usage; `contentStyle` from `useThemeColors()`. |
| `app/admin/_layout.tsx` | `useThemeColors()` for the three props; keep the comment's intent, drop "dark tokens' light text". |
| `components/LoadingState.tsx`, `components/TextField.tsx`, `features/admin-shell/components/AdminSearch.tsx`, `features/catalog/components/SearchBar.tsx` | `colors.x` → `useThemeColors().x`. |
| `features/admin-shell/components/AdminShell.tsx` | Place `ThemeToggle` (D9) in both branches. |
| `components/TopNav.tsx` | Place `ThemeToggle` (D9). |
| `features/catalog/components/QuickAddPicker.tsx` | D8, one `style` prop. |

After this, `grep -rn "colors\b.*design-tokens\|import { colors" app components features lib` must return nothing.

### P2.46 — 375 px sweep

Admin routes (11): `/admin`, `/admin/orders`, `/admin/orders/9`, `/admin/returns`, `/admin/products`, `/admin/products/new`, `/admin/products/7`, `/admin/categories`, `/admin/inventory`, `/admin/customers`, `/admin/customers/3`.

Known overflow candidates (fix with `flex-wrap` on the row, or `shrink`/`flex-1` on the long `Text`):
- Pager rows — 2 `ActionButton`s + "Page n of m" in a non-wrapping row: `features/admin-orders/components/AdminOrderListView.tsx:146`, `AdminReturnsView.tsx:108`, `features/admin-dashboard/components/DashboardView.tsx:104`.
- Approve/Decline row with order-number labels: `AdminReturnsView.tsx:44`.
- Walk every other `flex-row` in `app/admin/**`, `features/admin-*/**` and the shared `components/` they render (33 hits in admin): any row whose children's content width is not bounded at 343 px (375 − 2×16 `px-lg`) gets the same fix. List each file:line you changed, and each you checked and left, in the report.

Do not change visuals at ≥ 768 beyond what wrapping does naturally.

### Tests

| File | Status |
|---|---|
| `lib/theme.test.ts` | new |
| `components/ThemeToggle.test.tsx` | new |
| `components/ThemeRoot.test.tsx` | new (ios/android) |
| `components/ThemeRoot.test.web.tsx` | new (web project) |
| `components/LoadingState.test.tsx` | new |
| `tests/admin-phone-width.test.tsx` | new |
| `tests/admin-phone-width-guard.test.ts` | new |
| `lib/design-tokens.test.ts` | **T2 rewritten** (only named change, below) |
| `features/admin-shell/components/AdminShell.test.tsx`, `components/TopNav.test.tsx` | **append** new `it` blocks only; existing blocks byte-identical |

## Freeze list

No edits: `backend/**`; `package.json`, `package-lock.json` (no new package); `babel.config.js`, `metro.config.js`, `global.css`, `nativewind-env.d.ts`; every existing test file and test block except T2 in `lib/design-tokens.test.ts` (T1, T3 untouched); `tests/routes.test.tsx`, `tests/jest-setup.js`, fixture files (new fixtures go in the new test file or a new `tests/*-fixtures.ts`); `docs/**` (Rob owns the doc rows already written); `docs/admin-dashboard-feature-tracker.md`; `.github/`, `.claude/`. If an existing test fails, fix the code, not the test; if you believe the test is wrong, stop and report it.

**T2 change and reason:** today T2 asserts `colors` is `themes.dark`. D3 removes `colors` (it would be dead code). New T2 keeps the same invariant — every legacy key still resolves — in its new form: for each `LEGACY_KEYS` entry, `themes.dark` and `themes.light` have it and `colorVariables[key] === 'var(--color-' + key + ')'`; and `Object.keys(colorVariables).sort()` equals `Object.keys(themes.dark).sort()`. `LEGACY_KEYS` list unchanged.

## Build order

1. `AGENT_STATUS.md` → Marco BUSY. Read the `Read:` list.
2. **Spike gate (D2):** do D3 + `tailwind.config.js` + a bare `ThemeRoot` (variables only) in `app/_layout.tsx`. Run `npx expo export --platform web` and `grep -o "background-color:var(--color-background)" dist/**/*.css | head -n 3` (or the equivalent for the emitted CSS path). If export fails or no `var(--color-` appears in the CSS, **stop**: report BLOCKED with the output — do not fall back to `dark:` variants yourself.
3. Test-first `lib/theme.test.ts` (TH1–TH6): quote red (module missing), then `lib/theme.ts`, green.
4. Rewrite T2 (red against old tokens if not yet changed — otherwise quote green), keep T1/T3.
5. `ThemeToggle.test.tsx` red → `ThemeToggle.tsx` green.
6. `ThemeRoot.test.tsx` + `.test.web.tsx` red → finish `ThemeRoot.tsx` (StatusBar) and wire `app/_layout.tsx`.
7. JS-prop consumers (D4) + `LoadingState.test.tsx`; then QuickAddPicker (D8).
8. Toggle placements (D9) + appended AdminShell/TopNav tests.
9. P2.46: guard test (PG1–PG3) green on current code — if it is red, it found real overflow; fix the code. Then `admin-phone-width.test.tsx` (PW1), then the flex-row sweep.
10. `npx tsc --noEmit | tail -n 20`, `npm test -- --ci | tail -n 20`, then full `bash scripts/verify.sh 2>&1 | tail -n 30` (one run, nothing else using Postgres/Release).
11. Write `slice-plans/slice-P2.47-marco-report.md` (≤ 6 KB): VERIFY line, red quotes, files, the sweep list from P2.46, the export-CSS grep output, and the manual checklist below marked "pending Nathaniel". Marco AVAILABLE.

## Required tests

● = test-first (quote red).

`lib/theme.test.ts` (AsyncStorage is mocked globally; clear it in `beforeEach`)
- ● TH1 empty storage → `mode` is `'dark'`.
- ● TH2 `toggle()` goes dark → light → dark.
- ● TH3 restart: set light, await persistence, `createThemeStore()` again + `await store.persist.rehydrate()` → `'light'`.
- ● TH4 stored `{"state":{"mode":"blue"},"version":1}` and a non-JSON string → rehydrated `mode` is `'dark'`, no throw.
- ● TH5 `themeVariables('light')` has exactly one `--color-<key>` per `themes.light` key with the light value; same for dark.
- ● TH6 `useThemeColors()` (renderHook) returns `themes.dark`, and `themes.light` after `toggle()` inside `act`.

`lib/design-tokens.test.ts` — T2 as specified under Freeze list.

`components/ThemeToggle.test.tsx`
- ● TG1 renders `getByRole('switch', { name: 'Dark mode' })` checked, visible text `Dark`.
- ● TG2 press → unchecked, text `Light`, store `mode === 'light'`; press again → checked.

`components/ThemeRoot.test.tsx` (mock `expo-status-bar` to capture `style`)
- ● TR1 children render; StatusBar `style` is `'light'` in dark, `'dark'` after switching to light.

`components/ThemeRoot.test.web.tsx`
- ● TR2 the root view's style carries `--color-background` = `themes.dark.background`; after switching, `themes.light.background` (assert the custom-property value, not a class).

`components/LoadingState.test.tsx`
- TL1 `ActivityIndicator` `color` prop is `themes.dark.primary`, then `themes.light.primary` after switching (proves the D4 JS-prop path).

`AdminShell.test.tsx` (appended)
- S-T1 wide and narrow (reuse `renderShell(1024)` / `renderShell(375)`) both show the `Dark mode` switch; pressing it flips `checked`, menu overlay stays closed.

`TopNav.test.tsx` (appended)
- N-T1 the storefront bar shows the `Dark mode` switch; pressing it sets the store to light.

`tests/admin-phone-width.test.tsx`
- PW1 `it.each` over the 11 admin routes: `useWindowDimensions` mocked to `{ width: 375 }` (AdminShell.test pattern), Admin session, fetch map copied from `routes.test.tsx` but list endpoints return one item with a 100-char name/SKU and `totalPages: 2` where the fixture allows. Assert: the route's title text, `Open menu` button present, `Admin navigation` absent, `Dark mode` switch present, and on paged lists both pager buttons present.

`tests/admin-phone-width-guard.test.ts` (reads files with `node:fs`; scope `app/admin/**`, `features/admin-*/**`, and `components/*.tsx` excluding tests)
- PG1 no `horizontal` prop on `ScrollView`/`FlatList`.
- PG2 no arbitrary width class `w-[Npx]`, `min-w-[Npx]`, `basis-[Npx]` with N > 343, and no `whitespace-nowrap`.
- PG3 no numeric `width:` / `minWidth:` literal > 343 in a `style` object.
Each failure message names file:line. Show it can fail: add a throwaway `w-[400px]` to a scratch copy, quote red, restore, `cmp` (mention in report; this is the guard's sanity check, not a §3 mutation).

## Manual checks (Nathaniel; Marco lists them as pending)

1. Web (`npx expo start --web` on a free port you start yourself, not :8081 if it is running): devtools device width 375, signed in as Admin, open each of the 11 routes in **both** themes; in the console `document.documentElement.scrollWidth <= window.innerWidth` must be `true` and nothing is clipped.
2. Toggle on admin, reload → same theme; storefront pages follow; QuickAddPicker sheet readable in light.
3. Expo Go (one phone): toggle, kill the app, reopen → theme kept; status bar readable in both.

## Review method (Rob)

Medium tier, one review: `git diff --stat` + untracked; read every changed file and the new tests. Checks: no `colors` import left (grep above); no hex/rgb added outside `lib/design-tokens.js`; T2 is the only edited existing block (diff of each existing test file shows additions only); `package.json` untouched; guard regexes actually match what they claim (read them); D5 merge guard present. Run `npx expo export --platform web` and the CSS grep. No §3 mutations (no money/security guard). Full `verify.sh` in the PASS round only; quote the VERIFY line.

## Split, if one round is not enough

Part A = P2.47 (build steps 2–8, tests TH/T2/TG/TR/TL/S-T1/N-T1); Part B = P2.46 (step 9, PW1/PG1–3, sweep). A must land first (PW1 asserts the toggle). Each part ends with a full `verify.sh` and its own report section. Expected: one round fits (~8 small source edits, 3 new source files, 7 new test files).

## Risks

1. **`var()` colours on native** compile and resolve, but only the spike (step 2) and manual check 3 prove it on this toolchain. Spike failure stops the slice.
2. **Opacity modifiers** (`bg-primary/50`) on a `var()` colour silently produce nothing. None exist today (grep `-[a-z]+/[0-9]+` hits only `bg-black/NN`). Logged in §5.5.
3. **Web portals:** any future `Modal` must apply `useThemeStyle()` (§5.5 bullet). Only QuickAddPicker today.
4. **Cold-start flash** for light users (D12).
5. **Existing tests querying all switches/buttons** could break when the toggle is added to `TopNav`/`AdminShell`. If one does, report it — do not edit it.
6. Overflow that Jest cannot see (text measured wider than 343) is only caught by manual check 1.
7. `docs/architecture/13.2-decision-log.md` is ~56 KB, over the ~10 KB file guideline: not split here (unrelated refactor), backlog.
