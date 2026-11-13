# Slice P2-style — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — jest 228 suites / 1764 tests, build 0 warnings, `Exported: dist`

Low tier: I read the diff, the four new test files and `public/index.html`; no mutations. The restyle sticks to class strings, tokens and one `pill` prop. No text node, role, label or behaviour changed, and no existing test was edited.

### 1. `public/index.html` instead of `app/+html.tsx` — Accepted
**What I saw:** you replaced the plan's `+html.tsx` with `public/index.html` because `web.output` is `"single"`. I diffed your file against Expo's own template (`node_modules/expo/node_modules/@expo/cli/static/template/index.html`). The only differences are the two preconnects, the stylesheet link and the `#web-fonts` style block. `%LANG_ISO_CODE%`, `%WEB_TITLE%`, charset, viewport, the `expo-reset` styles (html/body height, body overflow, `#root` flex), `<noscript>` and `<div id="root">` are all unchanged.
**Why it holds:** the theme (P2.47) lives in `ThemeRoot` (`vars()` on a `flex-1 bg-background` View), not in the HTML, so the template does not affect it. There are no secrets, env values or hosts besides Google Fonts. `nginx.conf` sends no CSP that would block the link. The Dockerfile `COPY . .` picks up `public/`. `dist/index.html` has the fonts link, the noscript, the root div and the three `_expo/static` scripts, and the export boots.
**Lesson:** check the build mode before you pick the hook. You proved it with an export instead of trusting the plan, and that was the right call.

### 2. `#root, #root *` font-family — no conflict today — NOTE
**What I saw:** no file in `app/`, `components/`, `features/` or `lib/` sets `fontFamily`, `font-mono` or a monospace stack, and there is no icon-font package (`@expo/vector-icons`, `expo-font`). So the id-specificity rule overrides nothing that is set on purpose. The Stripe Elements iframes are separate documents and are not affected. One gap: RN `Modal` (storefront `QuickAddPicker`) portals to `document.body`, outside `#root`, so on web it stays in the system font. That is cosmetic, so I logged it to the backlog.
**Lesson:** a broad `*` selector is fine while nothing sets a family. The first mono or icon font has to add a more specific rule.

### 3. Existing tests — Confirmed
`git log --all -- components/ActionButton.test.tsx` is empty and the file is untracked, so it is new. `git diff --stat` shows no test file modified. The new tests are `layout.test.ts` (L1–L3 at the token boundaries), `design-tokens-style.test.ts` (T4, F1), `web-fonts.test.ts` (F3–F5) and `ActionButton.test.tsx` (AP3: pill still disables and sets busy while pending). The default `ActionButton` class string is unchanged apart from moving `rounded-control` into `shape`.

### 4. Storefront palette — NOTE (owner decision N2)
**What I saw:** light mode is fine (white on `#0a7a5c` is about 5.4:1). In dark mode, white on `#10b981` is about 2.5:1 and white on `#f87171` is about 2.8:1. The old dark palette (`#f5fbf7` on `#3dae6f`, about 2.9:1) already failed AA, and neither the quality bar nor §5.5 sets a contrast rule, so this is not a regression of a stated invariant. P2.46 (phone width + PG1–PG3) and P2.47 (ThemeRoot TR2, theme TH5/TH6) pass unchanged in the green run. I logged it to the backlog, together with the manual browser check you could not do.

### 5. Scope — Confirmed
There is no chart, donut, tabs, Export, Live pill, icons, avatar, active-nav highlight or stock bar. `layout.ts` has one consumer (`DashboardView`). Every new colour key is used (`subtle`, `warning-subtle`/`-border`, `danger-foreground`). Class strings are literal, so Tailwind sees them. You deleted `Section` and the dashboard no longer imports `ScreenTitle`, as D9/D10 planned.

### 6. Stale comment and unused mono token — NOTE
`lib/design-tokens.js` (comment above `webFontFamily`) still says `app/+html.tsx` loads the fonts. `webFontFamily.mono` and the JetBrains Mono download have no consumer yet. Both are in the backlog. They are not bugs.

**Doc upkeep (Rob):** I updated the P2-style row in `13.2-decision-log.md` with N1/N2: fonts on web only through `public/index.html`, and `+html.tsx` rejected because of single output.

**What was good:** the font stack lives in two places, and F4 ties them together with a test so they cannot drift. The breakpoint boundaries in L1–L3 come from the token constants, so a token change cannot leave a stale literal behind.
