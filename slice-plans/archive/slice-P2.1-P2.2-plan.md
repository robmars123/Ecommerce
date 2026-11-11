**Read:** `docs/admin-dashboard-feature-tracker.md` rows P2.1–P2.4 (lines 10–13) · arch §5.4 and §5.5 (`docs/architecture/05-frontend.md` lines 31–75) · arch §11.3 (`docs/architecture/11-testing.md` lines 46–57) · quality-bar §1, §4 · decision-log row "Admin theme tokens and shell" (grep `docs/architecture/13.2-decision-log.md`, don't read the log) · target screenshot `C:\Users\Tenten\AppData\Local\Temp\claude\9acc43989f554e88\44380f48-dc68-48cc-88cc-047645b8bae1\images\1.png` (left sidebar only).
Code to read before editing: `lib/design-tokens.js`, `lib/design-tokens.d.ts`, `tailwind.config.js` (read only — no edit), `app/admin/_layout.tsx`, `app/_layout.tsx` (read only), `features/auth/components/AdminGate.tsx` (read only), `components/TopNav.tsx` (className + `min-h-touch` pressable pattern), `features/catalog/components/ProductListView.tsx` lines 37–45 (`useWindowDimensions` pattern), `tests/app/admin.test.tsx` (renderRouter + `signInAs` + fetch-mock pattern), `tests/routes.test.tsx` lines 1–60 (admin rows), `tests/admin-dashboard-fixtures.ts`, `tests/fetch-mock.ts`, `tests/session.ts`.

All paths below are under `frontend/apps/store/` unless they start with `docs/`.

# Slice P2.1–P2.2 — Admin theme tokens and admin shell (frontend)

Deliverables: tracker **P2.1** (tokens, dark + light, one file) and **P2.2** (shell in `app/admin/_layout.tsx`: sidebar, collapses at phone width). Gap-fill, no §12.3 row (like 12.2). Builder: **Marco**. Mode: Autonomous.

Out of scope (later rows — do not start them): grouped nav items and role hiding (P2.3), sidebar footer name/role (P2.4), breadcrumb/search/top-bar pills/badges (P2.6–P2.11), Needs-attention card (P2.12), wiring light mode onto screens or a mode switch (P2.47), restyling any existing admin screen (P2.29+). No backend, no API, no new package, no change to `tailwind.config.js`, `app/_layout.tsx`, `AdminGate`, or any existing test.

## Risk tier — Low

Layout and tokens only: no money, stock, payment, auth rule or state transition is added or changed. The admin guard stays exactly where it is (`AdminGate` wraps everything in `app/admin/_layout.tsx`; the shell goes **inside** it, so a Customer is redirected before the shell renders — existing `tests/app/admin.test.tsx` F7b keeps proving it, unchanged). CLAUDE.md §5 Low: Marco's tests + full `verify.sh`, Rob reads the diff, **no mutations**.

## Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | Light palette lives in the existing `lib/design-tokens.js` as `themes: { dark, light }`; `colors` stays exported and **is** `themes.dark` (same object). Tailwind keeps consuming `tokens.colors`. | §5.5: one token source; the whole app already reads `colors` (Tailwind + JS-prop cases like the admin Stack `screenOptions`). Zero churn for existing screens. | A new `components/admin-theme.ts` (tracker "module `components`") — a second token source breaks §5.5. A CSS-variable / NativeWind `vars()` rewrite of every colour — that is P2.47's job, and it touches every screen. |
| D2 | Light values (Marco copies exactly): `background #f6f8f6`, `surface #ffffff`, `surface-raised #eef2ef`, `border #d6ddd8`, `foreground #111412`, `muted #5a645d`, `primary #23804c`, `primary-foreground #ffffff`, `primary-subtle #e2f2e8`, `primary-text #1d6e41`, `selected #111412`, `selected-foreground #f5fbf7`, `success #23804c`, `warning #9a6410`, `danger #c0392b`, `scrim #11141266`. | Same green/amber/red roles as the dark set and the screenshot; primary darkened so white text on it and green text on white stay ≥ 4.5:1. | Reusing dark hues on white (green `#3dae6f` on white is ~2.9:1). |
| D3 | One new colour key in **both** modes: `scrim` (dark `#000000b3`, light as D2) — the backdrop behind the phone-width sidebar. | The overlay needs a translucent colour and §5.5 forbids literals in components; it has a consumer in this slice. | No backdrop (no tap-outside-to-close on phones). |
| D4 | New size tokens: `width.sidebar: '240px'` and a numeric top-level `sidebarBreakpoint: 768` (typed in `.d.ts` like `touch`). Wide = `width >= 768`. | §5.5: no one-off pixel literals. 240 < 320 so the open phone overlay never causes horizontal scroll. 768 = tablet portrait gets the fixed sidebar, phones the toggle. | Tailwind `md:` responsive classes — NativeWind breakpoints don't drive the conditional render the tests need, and we must not render the sidebar twice. |
| D5 | Shell is `features/admin-shell/components/AdminShell.tsx` (new feature folder `admin-shell`, tracker module). `app/admin/_layout.tsx` becomes `<AdminGate><AdminShell><Stack screenOptions=…/></AdminShell></AdminGate>`; the Stack, its `screenOptions` and **its comment stay verbatim**. | §5.4: `app/` holds routes/layouts only. The nested Stack still needs its own `screenOptions` (the comment's reason is unchanged); the shell only frames it. | Building the shell inside `_layout.tsx` (untestable in isolation, components in `app/`). Replacing the Stack with `Slot` — loses the per-screen headers/back button that the admin screens rely on today. |
| D6 | Minimal nav: the sidebar renders brand ("Store Admin" / "Commerce Suite", with an "S" mark) and **one** link, "Dashboard" → `/admin`. Nothing else. | The shell needs a focusable item to prove links close the phone overlay; Dashboard is visible to both Admin and Support (no role logic needed). Every other item, group heading and role hiding is P2.3. | Full grouped nav now — that is P2.3 (role hiding is its point; half of it here would be untested scope). |
| D7 | Phone layout: a top bar (`bg-surface`, `border-b border-border`, `px-lg` = 16px gutter) with an "Open menu" button and the brand name; the sidebar is not rendered until opened; opened = absolute overlay (`bg-scrim` backdrop Pressable "Close menu" + `w-sidebar max-w-full` panel with its own "Close menu" button). Pressing the Dashboard link or either close control closes it. On wide, `open` state is ignored (sidebar always shown, no top bar, no toggle). | Built from `View`/`Pressable`/`Text` only — no drawer lib, no svg, no Reanimated (a fade is not required). Not mounting the sidebar while closed keeps it out of the screen-reader/tab order. | Animated slide-in (Reanimated adds complexity and test flakiness for no row requirement). Keeping the sidebar mounted off-screen (still focusable on web). |
| D8 | Glyphs are plain text: toggle shows "Menu", close shows "Close" (or "✕"); no icon font, no svg. | No new package; one codebase. | `@expo/vector-icons` — not approved in a plan; would need naming here. |
| D9 | Accessibility contract (what tests query): sidebar container `accessibilityLabel="Admin navigation"`; toggle `accessibilityRole="button"`, label "Open menu", `accessibilityState={{ expanded: isOpen }}`; close controls label "Close menu"; nav link `accessibilityRole="link"` with name "Dashboard". All pressables `min-h-touch min-w-touch`. | §11.3/§5.5: tests assert role and label, never classes. | `testID`s. |

Decision-log row "Admin theme tokens and shell (tracker P2.1–P2.2, Rob)" is appended by Rob with this plan.

## Files

| File | Change |
|---|---|
| `lib/design-tokens.js` | Add `const dark = { …existing colours…, scrim }`, `const light = {…D2}`; export `colors: dark`, `themes: { dark, light }`, `sidebarBreakpoint`, `width.sidebar`. Existing values unchanged. |
| `lib/design-tokens.d.ts` | `export const themes: { dark: TokenMap; light: TokenMap };` `export const sidebarBreakpoint: number;` |
| `lib/design-tokens.test.ts` | **New** — T1–T3. |
| `features/admin-shell/components/AdminShell.tsx` | **New** — `export function AdminShell({ children }: AdminShellProps)`. Reads `useWindowDimensions().width`; `isWide = width >= sidebarBreakpoint`; `const [isOpen, setOpen] = useState(false)`. A private `Sidebar` function in the same file (brand + Dashboard `Link href="/admin" asChild`, `onPress` → close). Wide: `flex-1 flex-row bg-background` → Sidebar (`w-sidebar border-r border-border bg-surface`) + `View className="flex-1"` children. Narrow: `flex-1 bg-background` → top bar + `View className="flex-1"` children + overlay when open. |
| `features/admin-shell/components/AdminShell.test.tsx` | **New** — S1–S6. |
| `app/admin/_layout.tsx` | Wrap the existing Stack in `<AdminShell>` inside `<AdminGate>`. Nothing else. |
| `tests/app/admin-shell.test.tsx` | **New** — R1–R2 (route-level, real `./app`). |

No other file. `tailwind.config.js` picks up `scrim` and `w-sidebar` automatically via `tokens.colors` / `tokens.width`.

## Build order

1. T1–T3 first (red: `themes` undefined), then the token edits → green.
2. S1–S6 red (module missing), then `AdminShell.tsx` → green.
3. Edit `_layout.tsx`; R1–R2; then run the full existing Jest suite **before** `verify.sh` — every existing test must pass unchanged (see Risk 1).
4. Manual web check: `npx expo start` web, sign in as Admin, open `/admin`, `/admin/orders`, `/admin/products`, `/admin/categories`, `/admin/inventory`, `/admin/returns`, `/admin/customers` at 1280px and at 375px (devtools). Report: sidebar visible wide; toggle + 16px gutter on 375; no horizontal scrollbar; overlay opens/closes; each screen's content and its Stack header/back still work. Quote what you saw in the report (no screenshots required).
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30` → `slice-P2.1-P2.2-marco-report.md` (≤ 6 KB).

## Required tests

All Jest. Test-first ones marked ●. Query by role/label (§11.3); no class-string assertions.

**`lib/design-tokens.test.ts`**
- ● T1 `themes.dark` and `themes.light` have exactly the same key set (sorted key arrays equal) and every value is a non-empty string matching `/^#[0-9a-f]{6}([0-9a-f]{2})?$/i`.
- ● T2 `colors` is `themes.dark` (`toBe`) — one source, no parallel palette; and `themes.dark` still contains every key the app used before (`background … danger`, list them) so no existing className breaks.
- T3 `sidebarBreakpoint` is a positive number and `width.sidebar` is a px string whose number is `< 320` (the open overlay fits the narrowest phone).

**`features/admin-shell/components/AdminShell.test.tsx`** — mock width with `jest.mock('react-native/Libraries/Utilities/useWindowDimensions', …)` returning a `jest.fn()` you set per test (if jest-expo resolves it elsewhere, mock whatever module `react-native`'s `useWindowDimensions` re-exports; state which in the report). Render inside `renderRouter({ index: () => <AdminShell><Text>Body</Text></AdminShell> }, …)` so `Link` has a router.
- ● S1 Wide (1024): "Admin navigation" present, "Store Admin" and "Commerce Suite" shown, link "Dashboard" present, "Body" rendered, no "Open menu" button.
- ● S2 Narrow (390): "Body" rendered, "Open menu" button present with `expanded: false`, "Admin navigation" **absent** (`queryByLabelText` null).
- ● S3 Narrow: press "Open menu" → "Admin navigation" present, toggle `expanded: true`; press the panel's "Close menu" → absent again.
- S4 Narrow: open, press link "Dashboard" → overlay closes.
- S5 Narrow: open, press the backdrop ("Close menu", the scrim) → closes. (Use `getAllByLabelText('Close menu')` and assert there are two; press each in turn in separate opens.)
- S6 Boundary: width `sidebarBreakpoint - 1` → toggle shown; width `sidebarBreakpoint` → sidebar shown, no toggle.

**`tests/app/admin-shell.test.tsx`** — real `./app`, `signInAs`, fetch mocked like `tests/app/admin.test.tsx` and `tests/routes.test.tsx` admin rows.
- R1 Admin at `/admin/orders`, wide: "Admin navigation" present **and** the orders screen title renders (the existing screen works inside the shell).
- R2 Customer at `/admin/orders`, wide: redirected to `/products`, "Admin navigation" never rendered, zero `/api/v1/admin` calls (guard still wraps the shell).

Existing tests: **none changed or deleted.** `tests/routes.test.tsx` and `tests/app/admin.test.tsx` must pass as they are.

Phone width (16px gutter, no horizontal scroll) is proven by T3 + D7's `px-lg`/`max-w-full` (Rob checks by reading) + the step-4 manual check; Jest cannot measure layout.

## Risks

1. **Duplicate text in existing tests.** jest-expo's default window width decides whether the sidebar renders in every existing admin test. If it is ≥ 768, "Dashboard"/"Store Admin" appear on every admin screen and an existing `getByText` may become ambiguous. Fix by keeping shell labels distinct (e.g. the brand is "Store Admin", never "Admin dashboard") — **never by editing an existing test**. Report the default width you observed.
2. **Nested Stack theme.** Moving the Stack inside a `View` must not drop `contentStyle`/`headerStyle`; the comment in `_layout.tsx` is why they exist. Keep them verbatim; the step-4 check confirms readable headers.
3. **Flex overflow on web.** The content `View` must be `flex-1` (RN Web Views default to `min-width: 0`); don't add fixed widths to the content side, or wide tables in orders/products will push the page sideways.
4. **8-digit hex on native.** `#RRGGBBAA` is supported by RN and CSS; if NativeWind's compiler rejects it, report it — don't swap to `rgba()` literals in components (the value stays in the token file).
5. **Light palette has no consumer yet** beyond T1. Accepted: P2.1 is literally "define both modes"; switching (CSS variables / `dark:` variant / `useColorScheme`) is P2.47 and is the decision flagged below.

## Open for Nathaniel (does not block this slice)

- **How light mode will be switched (P2.47):** follow the OS (`useColorScheme`), an in-app toggle, or both — and whether the storefront follows too. The mechanism (NativeWind `vars()` CSS variables vs. `dark:` variants) will be planned with that row; D1 keeps either option open.
- The D2 light values are a design choice; change them in the token file only.
