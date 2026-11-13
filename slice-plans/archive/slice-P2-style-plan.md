# Slice P2-style — Admin shell and dashboard match the CSS values doc

Mode: Autonomous · Risk tier: **LOW** (layout only; no API, data, auth, money or behaviour change) · Rob · 2026-11-12

Goal: the admin shell and `/admin` dashboard that are already built take the values in `docs/admin-dashboard-css-values.md` (below: **CSS §N**) in light and dark mode, including the ≤980 px and ≤560 px breakpoints. Nothing new is built.

**Read:** this plan · CSS §1, §2, §3, §4 (rows `.page-head` to `.meta-line` only), §5, §6 (rows `.status-row`, `.badge*` only), §7 (rows `.stock-item`, `.qty` only), §9, §10, §11 — grep `-n "^## "` and use `offset`/`limit`; never read CSS §12 (copy-paste stylesheet) or §8 · architecture §5.5 (`docs/architecture/05-frontend.md`, `grep -n "^### 5.5"`) · decision log rows `grep -n "P2-style\|P2.46–P2.47" docs/architecture/13.2-decision-log.md` · the files in "Files" below.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | **One palette, app-wide.** Replace the values of the existing keys in `themes.dark`/`themes.light` (`lib/design-tokens.js`) with the CSS §1 tokens, using the mapping in §3.1 below. Storefront shifts with it (same green accent family, slate neutrals). | Arch §5.5 and the P2.46–P2.47 log row: one token source, the whole app follows one theme. All admin screens and JS-prop colours pick it up unchanged. | Admin-only palette via `vars()` on the shell root: a second theme path, and JS-prop colours (`LoadingState`, `TextField`, Stack options) would still use the storefront palette. **Flagged** (N2). |
| D2 | Add only the colour keys a built element consumes: `subtle` (CSS `--text-3`), `warning-subtle` (`--amber-bg`), `warning-border` (`--amber-border`), `danger-foreground` (`#ffffff` both modes, text on the red Orders badge, CSS §3 `.nav-item .dot`). | Every token needs a consumer in this slice (Rob rule). | Porting all 22 tokens: `--card-2`, `--border-strong`, `--accent-hover`, `--red-bg`, `--red-border`, `--blue`, `--blue-bg` are only used by unbuilt parts (stories, empty-icon, hover, chart, badges by status). |
| D3 | Letter-spacing and line-height become **px** in the token file (em × font size, ratio × font size, round to 2 decimals), carried by named type roles in `fontSize` as Tailwind tuples `[size, { lineHeight, letterSpacing }]`. Weight stays a separate class. | RN has no `em` letter-spacing or unitless line-height on native; arch §5.5 already says tokens that must be exact are px. One class per type role keeps markup short. | Arbitrary em/unitless classes: wrong on native. |
| D4 | Breakpoints from CSS §10 as tokens: `sidebarBreakpoint` 768 → **981** (sidebar shown at ≥ 981, so hidden at ≤ 980); new `narrowBreakpoint` **561** (≤ 560 layout below it). Width-driven layout stays in JS (`useWindowDimensions`) like the existing shell, through one pure helper with unit tests. | Same mechanism as the shell; testable in Jest. | NativeWind `screens` variants: untestable, a second mechanism. |
| D5 | CSS grid → `flex-row flex-wrap` with `grow` + percentage `basis` (KPI: 4 / 2 / 1 columns; Top products + Low stock: side by side `basis-0` with `grow-[115]`/`grow-[85]` at ≥ 981, stacked below). | RN has no CSS grid; grow + basis gives the ratios with the 14 px gap. | `onLayout` measuring: runtime state for a static ratio. |
| D6 | Shadows: tokens `boxShadow.card` (CSS §1 `--shadow`, **light value**) and `boxShadow.accent` (CSS §9 Refresh shadow), applied as `web:shadow-card` / `web:shadow-accent`. Native gets no shadow; dark mode reuses the light shadow. | NativeWind 4.2.7's native `box-shadow` mapping is unverified; the doc's two shadows differ by mode and Tailwind's shadow token is not a CSS variable here. | A per-mode `--shadow` variable from `ThemeRoot`: changes `themeVariables`/TH5 for a near-invisible difference. |
| D7 | Touch targets win over the doc: every pressable keeps `min-h-touch` (44). Segment buttons (CSS 36 px), Refresh and buttons (40 px) and nav items (9 px padding) render at 44 high. | Quality bar §6 / arch §5.5 accessibility invariant. | Doc heights: would fail the touch-target rule. |
| D8 | `ActionButton` gains one optional prop `pill?: boolean` → `rounded-pill`, `text-ui`, and the caller adds no other style. The Refresh button uses it; all other callers unchanged. | Refresh keeps the tested pending/busy guard. | A local Refresh `Pressable`: copies the double-tap guard. |
| D9 | The dashboard renders its own page head (`Stack.Screen` title + header `Text`) instead of `ScreenTitle`. | `ScreenTitle` is shared with the storefront. Role `header` and name "Admin dashboard" stay. | A size prop on `ScreenTitle`. |
| D10 | The dashboard-local `Section` is deleted; Orders by status and Needs attention use `AdminPanel`. In the sidebar, the Needs attention card uses the CSS §3 `.sidebar-footer` box instead of `AdminPanel`. | One panel component; header role and text unchanged. | Keeping `Section`. |
| D11 | No text node is split, merged or reworded (`"Blue Shirt · 9 sold"`, `"… · 2 left"`, `"N paid"`, `"N low"`, status rows). Only containers and classes change. | Tests and screen readers read them; no behaviour change. | The two-column `.qty` stock row: new content. |
| D12 | Arbitrary px classes (`p-[18px]`, `h-[60px]`) are allowed for one-off doc values; values used 2+ times become tokens (§3.2). Precedent: `TopNav` `min-w-[20px]`. | Exact on every platform (arch §5.5: px, not rem). | Tailwind's numeric scale: rem-based, 14 px rem on native. |

## 2. Mapping: direct · closest equivalent · skipped

**Direct (NativeWind/RN supports it):** colours, borders (1 px), radii, padding, gaps, font sizes and weights, `uppercase`, `underline`, `overflow-hidden`, `flex-wrap`, `items-*`/`justify-*`, `margin-left: auto` (`ml-auto`), light/dark via the app toggle (CSS `prefers-color-scheme` dark values = `themes.dark`; app toggle decides, per the P2.46–P2.47 row; `color-scheme` meta not needed).

**Closest equivalent:**
- Grid columns → flex-wrap + basis (D5). `position: sticky` sidebar/top bar → already fixed: the shell's sidebar and top bar sit outside the screen's `ScrollView`.
- Top bar `color-mix(… 88%) + backdrop-filter: blur(8px)` → solid `bg-background` (no blur or `color-mix` in RN; no opacity modifiers on tokens, arch §5.5).
- `em` letter-spacing and unitless line-height → px (D3).
- Shadows → web only, light value in both modes (D6).
- `:hover` (nav item) → NativeWind `hover:bg-background` on the `Pressable`; `:active` scale on Refresh → `active:scale-[0.98]` via the `pill` variant. If either class fails `tsc`/`expo export`, drop it and note it in the report (no other workaround).
- Dashed status-row border → `border-b border-border web:border-dashed` (iOS has no single-side dashed border).
- Heights 36/40 px → 44 (D7). Search input 38 px → 44 (`min-h-touch` already on it).
- `text-underline-offset` → plain `underline`.

**Skipped (with reason):**
- Fonts Inter / JetBrains Mono — not shipped (no `assets/fonts`, no `@expo-google-fonts/*`); system font stays. Decision N1.
- Icons and glyphs: nav icons, KPI icons, alert ⚠ icon, low-stock thumb, empty-state icon box, search SVG icon (input left padding stays 14, not 36) — not built; no icon package; adding glyphs is new content.
- Avatar circle with initials (CSS §3 `.avatar`) — new content (initials logic); the footer keeps name + role in the `.avatar-row` style.
- Active nav item highlight (`.nav-item.active`) — needs route matching; not built (N3).
- Low-stock progress bar (`.stock-bar`, fixed 30 % inline) — the mock-up's width is not a real figure; showing it would invent data.
- `.qty` "LOW" label, Manage inventory, Back to Overview, Export, Live pill/dot, tabs, chart, donut, stories, DoD bar, footer note — not built.
- `white-space: nowrap` (`.link`, alert badge) — banned by the P2.46 guard (PG2).
- ≤ 980 `.search { display: none }` — the phone top bar keeps search: hiding it removes a working feature (behaviour). ≤ 560 `.view-tabs`, `.donut-flex`, `.ac` rules — elements not built.
- `max-width: content` (1200) on the dashboard is **removed** to match `.main` (flex 1, no max) — listed here so it is not read as a regression.

## 3. Files, contracts

### 3.1 `frontend/apps/store/lib/design-tokens.js` (+ `design-tokens.d.ts`)
- `themes.light` / `themes.dark` values (CSS §1, light column / dark column): `background`←`--bg` · `surface`←`--card` · `surface-raised`←`--bg-2` · `border`←`--border` · `foreground`←`--text` · `muted`←`--text-2` · `primary`, `success`, `primary-text`←`--accent` · `primary-foreground`←`#ffffff` (both; CSS §4 `.btn-refresh` colour) · `primary-subtle`←`--accent-bg` · `selected`/`selected-foreground`← CSS §9 row 1 (light `#0f172a`/`#ffffff`, dark inverted) · `warning`←`--amber` · `danger`←`--red` · `scrim` unchanged. New keys per D2. All 6-digit hex (T1).
- `width.sidebar` 240px → 232px (CSS §3 `.sidebar`). `sidebarBreakpoint` 981, new `narrowBreakpoint` 561 (D4; add to `.d.ts`).
- `spacing` add: `gutter` 14px (grid gaps, section margins), `card` 18px (KPI padding, panel horizontal padding), `page` 28px (main horizontal padding wide). CSS §11.
- `borderRadius` add: `panel` 14px (`--radius`), `nav` 9px (nav item, logo). Existing `card` 12px serves alert and sidebar footer; `control` 8px; `input` 10px; `pill`.
- `fontSize` add type roles (D3; CSS §2 type scale + §3/§4 rows): `page-title` 26 (lh 1.05, ls −.03em), `page-title-sm` 23 (same ratios), `kpi` 28 (lh 1, ls −.04em), `panel-title` 14.5 (ls −.01em), `ui` 13.5 (lh 1.5), `ui-sm` 13 (lh 1.5), `meta` 12.5 (lh 1.5), `label` 12 (ls .06em), `nav-label` 11 (ls .08em), `eyebrow` 11 (ls .02em), `badge` 11 (ls .04em). `.d.ts`: `fontSize` becomes `Record<string, string | [string, Record<string, string>]>`.
- `boxShadow`: `card`, `accent` (D6); add to `.d.ts`.
### 3.2 `frontend/apps/store/tailwind.config.js`
- `extend.boxShadow: tokens.boxShadow`. Nothing else (the other maps are already wired).
### 3.3 `features/admin-dashboard/layout.ts` (new) + `layout.test.ts`
- `export type DashboardLayout = { kpiBasis: 'basis-full' | 'basis-[45%]' | 'basis-[22%]'; panelsSideBySide: boolean; narrow: boolean; wide: boolean }`
- `export function dashboardLayout(width: number): DashboardLayout` — `wide` = width ≥ `sidebarBreakpoint`; `narrow` = width < `narrowBreakpoint`; KPI 4 columns when wide, 1 when narrow, else 2; `panelsSideBySide` = wide. Class strings are literal in this file so Tailwind sees them.
### 3.4 `features/admin-shell/components/AdminShell.tsx`
- Sidebar (CSS §3 `.sidebar`): keep `w-sidebar` and no `flex-1` (asserted by AdminShell tests), `bg-surface`, `px-[14px] py-[20px] gap-[6px]`; border-r stays on the wrapper.
- Brand (`.brand`, `.logo`, `.brand b`, `.brand span`): logo box 34×34 `rounded-nav bg-selected`, "S" 15 px bold `text-selected-foreground`; "Store Admin" `text-small font-bold`; "Commerce Suite" `text-eyebrow font-medium uppercase text-subtle`.
- Group titles (`.nav-label`): `text-nav-label font-semibold uppercase text-subtle`, padding 14/10/6.
- Nav items (`.nav-item`): `rounded-nav border border-transparent px-[10px] gap-[10px] hover:bg-background`, label `text-ui font-medium text-muted`; keep `min-h-touch min-w-touch`. Disabled items: same layout, `text-subtle`.
- Badges: Inventory "N low" → CSS §9 Inventory nav pill (`rounded-pill border border-warning-border bg-warning-subtle px-[7px] py-[2px] text-badge font-bold text-warning`). Orders "N paid" → `.nav-item .dot` (`rounded-pill bg-danger min-h-[20px] min-w-[20px] px-[6px] items-center justify-center`, text `text-badge font-bold text-danger-foreground`). The badge's own link `Pressable` keeps `min-h-touch min-w-touch` and its labels.
- Needs attention (sidebar) → `.sidebar-footer` box: `mt-auto` not used (it sits in the ScrollView), `rounded-card border border-border bg-background p-md`, header `text-ui-sm font-semibold text-foreground`, rows `text-meta text-muted` (tone rows keep `text-warning`/`text-danger`).
- User footer → `.avatar-row` without the avatar: `border-t border-border pt-[14px] mt-[14px]`, name `text-ui-sm font-bold`, role `text-caption text-subtle`. Accessibility label unchanged.
- Wide top bar (`.topbar`): `min-h-[60px] items-center border-b border-border bg-background px-page`; search wrapper `ml-[20px] max-w-[340px] flex-1` (replaces `w-sidebar max-w-full`). Breadcrumb container keeps `flex-wrap` (asserted); current crumb `text-ui font-semibold`, links `text-ui text-muted`.
- Phone top bar: `bg-background px-lg`, title `text-small font-bold`. Structure, Menu button, overlay, search unchanged.
### 3.5 `features/admin-shell/components/AdminSearch.tsx`
- Input: `rounded-pill border-border bg-surface px-[14px] text-ui`; `placeholderTextColor={colors.subtle}` (CSS §9 search stroke `#94a3b8` = light `--text-3`). Results panel: `rounded-card`, rows `text-ui`, group titles `text-nav-label uppercase text-subtle`.
### 3.6 `components/AdminPanel.tsx`
- `.panel` (CSS §5): outer `overflow-hidden rounded-panel border border-border bg-surface web:shadow-card`. With a title: head `border-b border-border px-card pt-lg pb-md`, title `text-panel-title font-bold text-foreground` (role header kept). Body `gap-md px-card pt-lg pb-card`. No title: body only. Applies to every admin screen that uses it (11) — intended.
### 3.7 `components/ActionButton.tsx`
- `pill?: boolean` (D8): `rounded-pill px-lg web:shadow-accent active:scale-[0.98]`, label `text-ui`. Default rendering byte-identical for other callers.
### 3.8 `features/admin-dashboard/components/DashboardView.tsx`
- Root `ScrollView` `contentContainerClassName`: `w-full` plus `px-page pb-[36px]` when `wide`, else `px-lg pb-[28px]` (CSS §3 `.main`, §10). Remove `max-w-content self-center` (§2). Gap between blocks `gap-gutter`.
- Page head (`.page-head`, D9): row `flex-row flex-wrap items-start justify-between gap-lg pt-[22px] pb-lg`; left: `Stack.Screen` title + header Text `text-page-title` (`text-page-title-sm` when `narrow`) `font-bold`, greeting `text-ui text-muted mt-[6px] max-w-[620px]`; right: `.controls` `flex-row flex-wrap items-center gap-sm`.
- Segmented control (`.seg`): container `flex-row rounded-pill border border-border bg-surface p-[3px] web:shadow-card`; buttons `min-h-touch rounded-pill px-[14px] justify-center`, selected `bg-selected` + `text-selected-foreground`, else transparent + `text-muted`; label `text-ui-sm font-semibold`. Keep `accessibilityState.selected`, role, label.
- Refresh: `ActionButton … pill`.
- Error line unchanged except `text-meta`. Meta line (`.meta-line`): `flex-row flex-wrap items-center gap-x-[14px] gap-y-sm`, texts `text-meta text-subtle`, lag keeps `text-warning` when lagging.
- Alert banner (`.alert`): `rounded-card border border-warning-border bg-warning-subtle px-[14px] py-[13px] gap-md`, title `text-ui font-semibold text-foreground`, link text `text-ui-sm font-semibold underline text-foreground`. Label and role unchanged.
- KPI cards (`.kpi`): `grow ${kpiBasis} rounded-panel border border-border bg-surface p-card web:shadow-card`; label `text-label font-semibold uppercase text-subtle mb-md`; value `text-kpi font-bold text-foreground`; secondary/detail `text-meta text-muted mt-sm`; change pill (`.trend`) `rounded-pill border px-[6px] py-[3px] text-badge font-bold bg-background` + tone border/text (muted tone: `border-border text-muted`); grid `flex-row flex-wrap gap-gutter`.
- Orders by status: `AdminPanel` (D10); rows `.status-row`: `min-h-touch flex-row items-center justify-between py-sm border-b border-border web:border-dashed text-ui-sm` — the single Text node keeps `"Status: N"`.
- Top products + Low stock: wrapper `flex-row flex-wrap gap-gutter` when `panelsSideBySide` (children `basis-0 grow-[115]` / `grow-[85]` `min-w-0`), else column. Rows `.stock-item`: `py-[11px] border-b border-border text-ui text-foreground` (last row keeps its border: no `:last-child` in RN). Empty text `text-ui-sm text-muted`.
- Needs attention, Recent orders: `AdminPanel`; row texts `text-ui`/`text-ui-sm`. Quick actions links → CSS §9 Top Products secondary button style: `min-h-touch rounded-pill border border-border bg-surface px-[14px] text-ui-sm font-semibold text-foreground`.

### 3.9 Not touched
`app/admin/_layout.tsx` (Stack colours come from `useThemeColors()`), `ThemeRoot`, `lib/theme.ts`, `ScreenTitle`, storefront components, any API code, `badges.ts`, `attention.ts`, `nav.ts`, `kpi-cards.ts`, `as-of.ts`.

## 4. Build order
1. `layout.test.ts` first (red: module missing) → `layout.ts` green. Quote the red line.
2. Token test additions (§5 T4) red → tokens, `.d.ts`, `tailwind.config.js` green.
3. `AdminPanel`, `ActionButton` → run their tests and every admin screen test.
4. `AdminShell`, `AdminSearch` → AdminShell/AdminSearch/admin-shell-data tests.
5. `DashboardView` → its tests, `tests/admin-phone-width*.test.*`.
6. `npx tsc --noEmit`, then full `bash scripts/verify.sh` (must include `expo export`, which compiles the NativeWind CSS).
7. Manual check on web (:8081, owner port — start your own instance only if it is free, else report not done): `/admin` at 1280, 900 and 375 px, light and dark. Report what you saw; no screenshots needed.

## 5. Required tests
No §11.2 numbered or F test is named for this slice. Tier LOW: no mutations.
- **L1–L4 (test-first)** `layout.test.ts`: 1280 and 981 → `basis-[22%]`, side by side, not narrow; 980 and 561 → `basis-[45%]`, stacked; 560 and 375 → `basis-full`, `narrow` true. Boundaries use the token constants, not literals.
- **T4 (test-first)** `design-tokens.test.ts`: `subtle`, `warning-subtle`, `warning-border`, `danger-foreground` exist in both modes; `0 < narrowBreakpoint < sidebarBreakpoint`.
- **AP3** `ActionButton` test (new or in an existing file that covers it): with `pill`, pending still disables and sets busy (behaviour guard for D8).
- Must pass **unchanged**: design-tokens T1–T3, theme TH5/TH6, `ThemeRoot` tests, `AdminPanel` A1/A2, `AdminShell.test.tsx` (style asserts `w-sidebar`/no `flex-1` ~137 and ~234, `flex-wrap` ~345; breakpoint tests ~123/128 use the token), `AdminSearch.test.tsx`, `DashboardView.test.tsx`, `tests/admin-phone-width.test.tsx` (P2.46), `tests/admin-phone-width-guard.test.ts` PG1–PG3 (P2.46), touch-target `minHeight: 44` tests, all storefront tests.
- No existing test may be edited. If one fails, the code is wrong — unless the failure is a class-string assert this plan deliberately changes; there is none in this plan. Any edited test is listed in the report with the reason and is BLOCKING until Rob accepts it.

## 6. Risks
- **Storefront look changes** with D1 (neutrals and green shift; `primary-foreground` becomes pure white). Contrast in dark: white on `#10b981` and on `#f87171` is low (~2.5:1) — it is the doc's choice; noted to Nathaniel (N2), not blocking.
- **NativeWind on native:** `hover:`, `active:scale`, `web:` and fontSize tuples are only proven on web by `expo export`; no native visual check (no EAS).
- **Breakpoint 768 → 981** moves 768–980 px tablets to the phone layout (Menu overlay). Intended by CSS §10; AdminShell tests use 390/1024 and the token, so they stay valid.
- **PG2 guard:** no `w-[Npx]`/`basis-[Npx]` > 343 and no `whitespace-nowrap`; `max-w-[340px]` / `max-w-[620px]` are allowed (not matched and not a width floor).
- Arbitrary class strings must be literal (no template-built `basis-[${n}%]`) or Tailwind will not generate them.

## 7. Decisions for Nathaniel (not planned)
- **N1 Fonts:** Inter + JetBrains Mono need `expo-font` + `@expo-google-fonts/inter` / `@expo-google-fonts/jetbrains-mono` (new packages) or a web-only Google Fonts `<link>` in a new `app/+html.tsx` (no package, but a third-party request at runtime, web only). System font until you choose.
- **N2 Palette scope:** D1 restyles the storefront too. Say if you want admin-only colours instead (adds a scoped-palette mechanism).
- **N3 Not-built mock-up elements** worth a tracker row: active nav highlight, nav/KPI/alert icons, avatar initials, search hidden ≤ 980 px.
