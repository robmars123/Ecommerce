# nativewind-shop — Marco report

Concerns:
- C1 (D4 spike result) `react-native-css-interop` registers its components only when `NODE_ENV !== 'test'` (`runtime/wrap-jsx.js`), so `className` never resolves to `style` under Jest. Not BLOCKED: the plan's fallback applies. Only the asserted properties stay in `style` from tokens (StubLink, ErrorState, CategoryFilter, SortPicker: minHeight/flex; SearchBar: flex/minWidth; PriceText: line-through). All else is `className`. The 7 style tests are byte-identical.
- C2 expo-router `Link` and expo-image `Image` are not registered with css-interop, so `className` on them is ignored. Links are now `<Link asChild><Text|Pressable className>` (roles/names unchanged, all link tests pass). The expo-image `Image` fills its sized wrapper via `StyleSheet.absoluteFill`.
- C3 Detail price is `text-title` (same as card). A container class cannot enlarge sibling RN `Text`, and the plan allowed no new prop. Cosmetic.
- C4 Not done: manual visual check at 1280/390 px and the Expo Go phone check (no browser or device available here). Web export emits the CSS (10 KB, token utilities such as `min-h-touch`, `bg-primary-subtle`, `rounded-card` present). Native styling is unverified; Nathaniel/Rob should do §5.5 step 4.
- C5 Admin list rows kept their one-line text; admin screens now use dark tokens too.

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed: none edited. Appended only (all hunks additions, except import lines):
- `lib/query-client.test.ts` and `features/catalog/list-params.test.ts`: import lines widened for new symbols (no `it` body touched).
- `VariantSelector.test.tsx`: one import line widened.
- Appended: L4 (query-client), L5 (api-client), S1, S2, S3, D-1, D-2. New files: `lib/logger.test.ts` (L1-L3), `components/TopNav.test.tsx` (N1).

Packages (exact pins; `expo install --check` green): nativewind 4.2.7 · react-native-reanimated 4.7.1 · react-native-worklets 0.13.0 (Reanimated's required peer `0.13.x`, version from `expo install`) · tailwindcss 3.4.19 (dev). Nothing else.

Red output (test-first, `lib/logger.test.ts` before `lib/logger.ts` existed):
`FAIL lib/logger.test.ts  ● Test suite failed to run  Cannot find module './logger' from 'lib/logger.test.ts'`
Then green: 7 passed.

Mutation checks (broken -> red -> restored -> green):
- Remove the JWT rule from `redact` -> L1 red (1 failed, 6 passed) -> restored, 7 passed.
- Add `title: error.title` to the entry -> L2 red (1 failed) -> restored.
- Source = `JSON.stringify(query.queryKey)` -> L4 red (1 failed, 11 passed) -> restored, 12 passed.
- Drop `disabled` from Out chips -> D-1 red (1 failed) -> restored.
- AccountBar added back to HomeView as well as TopNav -> `tests/app/admin.test.tsx` red (4 failed: "account bar by role on the home screen") -> restored.

Changed (new): `babel.config.js`, `metro.config.js`, `tailwind.config.js`, `global.css`, `nativewind-env.d.ts` (also `declare module '*.css'` so tsc accepts the side-effect import), `lib/design-tokens.js` + `.d.ts`, `lib/logger.ts` + test, `components/TopNav.tsx` + test, `tests/style-stub.js`.
Changed (modified): `package.json` (+ lock), jest `moduleNameMapper` (one entry: `[.]css$` -> `tests/style-stub.js`; the only jest-config change), `app/_layout.tsx` (global.css, StatusBar light, TopNav header, token background), `lib/query-client.ts` (QueryCache/MutationCache onError -> logClientError), `lib/api-client.ts` (D16: sent id on network/timeout/fallback), `list-params.ts` (`showingRange`), `ProductListView` (D8-D10), `ProductCard`, `StockBadge` (dot), `VariantSelector` (D12, `variantGroupLabel`), `ProductDetailView` (back link), `ProductDetailContent` (two columns, SKU row), `HomeView` (AccountBar removed).
Migrated to className only: all of `components/*`, `features/auth/components/*` forms and AccountBar, `features/admin-catalog/components/*`, `Pagination`, `PriceText`, `ProductImageView`, `SearchBar`, `CategoryFilter`, `SortPicker`.
Hex/rgb grep over `app components features` (non-test): no matches. The only `StyleSheet.create` left are the four D4 fallbacks (CategoryFilter, SortPicker, SearchBar, PriceText).

Deviations from the plan:
- Added `touch` (numeric 44) and `minHeight.textarea` (96px) tokens: Jest needs a number, and the multiline field needs a non-arbitrary value.
- `tests/style-stub.js` is a new 1-line file (the plan said "an existing-style stub"; none existed).
- `d.ts` uses named `export const` declarations (not `export =`) so `import { touch }` type-checks.
- No `Stack.Screen` header title bar on store routes (D7) and no icon/font (D14), as planned. Gaps G1-G8 not built.

verify.sh (full, Release):
PASS secrets · hygiene · build (0 Warning(s), 0 Error(s)) · test (.NET, all pass) · drift · expo-check ("Dependencies are up to date") · tsc clean · jest "Test Suites: 47 passed, 47 total | Tests: 308 passed, 308 total" (was 295 before new tests; 13 added: logger 7... see files) · export "Exported: dist".
`VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`

Quality-bar §2: tests written and passing for all new logic; invariants have mutation checks (above); no secrets; no backend change; no git writes; test-first red quoted; manual native check NOT done (C4) — unchecked, reason: no device/browser in this session.
