# Slice P2-style — Marco report

Concerns:
- Fonts: the plan's `app/+html.tsx` has no effect here. `app.json` has `web.output: "single"`, and in single mode Expo uses `public/index.html` as the page template and ignores `+html.tsx`. I built `+html.tsx` first, exported, and the Google Fonts link was missing from `dist/index.html`. `+html.tsx` would also have broken the existing `tests/routes.test.tsx`, because that test lists every file under `app/`. I removed it and used `public/index.html` (Expo's own template with the placeholders kept). No package, no `app.json` change. Moving to `output: "static"` would make `+html.tsx` work, but it changes the build, so I did not do it.
- The sans stack is written twice, in `lib/design-tokens.js` and in `public/index.html`, because HTML cannot import JS. A test (F4) checks that they match.
- Not done: step 7, the manual web check at 1280/900/375 px in light and dark. I cannot see a browser and :8081 is an owner port. Jest and `expo export` pass, but nobody has looked at the result.
- Dark mode: white on `#10b981` and on `#f87171` is about 2.5:1 contrast. It is the doc's palette (plan risk, N2).
- Not shown red: T4 and F1 token tests were written after the tokens. They only assert that keys and values exist. L1-L3 were red first: `Cannot find module './layout'`.
- Two verify runs failed before the passing one, neither because of this slice's final code. (1) First run: `+html.tsx` broke `routes.test.tsx` (fixed by removing it), and one integration test hit `Failed to connect to 127.0.0.1:55117, address already in use`. (2) Second run: `tests/routes.test.tsx` "/products" hit the 5 s timeout on its first test under load. It passed alone and in the third full run.

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed: None. No existing test was edited. New tests only:
- `features/admin-dashboard/layout.test.ts` L1-L3 (boundaries use the token constants)
- `lib/design-tokens-style.test.ts` T4 + F1 (new keys, `0 < narrowBreakpoint < sidebarBreakpoint`, font stacks)
- `lib/web-fonts.test.ts` F3-F5 (Google Fonts link and preconnect, stack matches the token, Expo placeholders kept)
- `components/ActionButton.test.tsx` AP3 (pill: pending still disables and sets busy, idle still fires)

Existing tests that must pass unchanged all pass: design-tokens T1-T3, theme, ThemeRoot, AdminPanel A1/A2, AdminShell, AdminSearch, DashboardView, admin-shell-data, P2.46 phone width + PG1-PG3, P2.47, `routes.test.tsx`.

## Font decision (N1)
Web only, no new package. `public/index.html` carries the preconnect to `fonts.googleapis.com` and `fonts.gstatic.com`, the stylesheet link (Inter 400/500/600/700, JetBrains Mono 500, your exact URL), and `#root, #root * { font-family: <sans stack> }`. react-native-web sets `font: 14px System` on every Text and TextInput, so inheriting from `body` would not work; the id selector wins. Token file exports `webFontFamily` (`sans`: `Inter, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`; `mono`: `"JetBrains Mono", monospace`). Android and iOS never read `public/index.html`, and no font class exists, so they keep the system font. No mono consumer is built, so there is no mono class yet. The `Platform.OS` gate was not needed: the web-only file is the gate.

## Other deviations from the plan
- None else for D1-D12, except: Needs attention in the sidebar keeps its header role through its own `View` (plan §3.4), and `ScreenTitle` replaced by `Stack.Screen` + own header Text on the dashboard (D9).
- Skipped as planned: icons, avatar, active nav highlight, stock bar, `.qty` label, charts, tabs, Export, Live pill.
- If `hover:`/`active:scale` classes had failed export I would have dropped them: they did not fail (`tsc` and `expo export` pass).

Changed (slice P2-style):
- `frontend/apps/store/lib/design-tokens.js`, `design-tokens.d.ts`, `tailwind.config.js` (palette, new keys, breakpoints 981/561, type roles, spacing, radii, shadows, fonts)
- `components/AdminPanel.tsx`, `components/ActionButton.tsx` (`pill`)
- `features/admin-shell/components/AdminShell.tsx`, `AdminSearch.tsx`
- `features/admin-dashboard/components/DashboardView.tsx`, new `layout.ts`
- `public/index.html` (new)
- New tests listed above

verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`. The `jest` step covers 228 suites in 3 projects; `export`: `Exported: dist`; `dist/index.html` contains the fonts link.

Quality-bar §2:
- [x] Plan built and verify green
- [x] No existing test edited or weakened
- [x] No new package, no secrets, git untouched
- [x] Touch targets kept at 44 (D7); P2.46 guard unchanged and green
- [x] Rules §3.4: no money, stock, auth or outbox code touched
- [ ] Manual web check at three widths and both themes: not done (see Concerns)
