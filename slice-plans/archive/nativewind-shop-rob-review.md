# nativewind-shop — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build `0 Error(s)|0 Warning(s)`, jest `Test Suites: 47 passed, 47 total | Tests: 308 passed, 308 total`, expo-check `Dependencies are up to date`, export `Exported: dist`.

The diff matches the plan. You kept the logger out of `api-client`, left every existing `it` body alone, used token utilities throughout, and held the D4 fallback to the properties it allows. I found no blocking items and no bugs. The visual check (1280/390 px) and the Expo Go check (§5.5 step 4) are still **owner-manual and not done**. Until someone runs them, native styling is unverified.

### What I checked (evidence)
- **Existing test files.** I diffed `query-client.test.ts`, `list-params.test.ts` and `VariantSelector.test.tsx`, and every removed line is an import line. Each replacement keeps all the original symbols and adds new ones: `apiGet, apiSend` plus the `query-keys`/`fetch-mock` imports, `showingRange` plus `ProductListPage`, and `variantGroupLabel`. `ProductCard`, `ProductDetailView`, `ProductListView` and `api-client` tests have no `-` lines at all. None of the five `toHaveStyle` test files (StubLink, ErrorState, CategoryFilter, PriceText, ProductImageView) is in the diff.
- **Logger redaction.** `logClientError` builds an allowlist: `source`, `kind`, `status` and `correlationId` (redacted, ≤ 64 chars). Non-ApiErrors add a redacted `message` (≤ 200 chars). `title`, `fieldErrors`, the URL and the body never reach the entry. `sourceFor` stops at the first non-string segment or after two segments. I checked it against every key in `lib/query-keys.ts`: `products.list`, `products.detail`, `categories`, `admin.products`. No slug, ID or filter can leak. `api-client.ts` contains no `console` (grep is empty).
- **Hex/rgb.** `grep -rnE "#[0-9a-fA-F]{3,8}\b|rgba?\(" app components features` (non-test) is empty. The JS colour props (`placeholderTextColor`, `ActivityIndicator color`, `contentStyle.backgroundColor`) all read `colors.*` from the tokens. I found no rem-scale utilities (`p-4`, `text-base`) and no arbitrary `[..px]` values.
- **Touch targets.** Every `Pressable`, `TextInput` and `Link asChild` child has `min-h-touch` (plus `min-w-touch` on icon-size buttons), or the token-sourced `minHeight: touch` style in the D4 files.
- **Packages.** `package.json` adds exactly four packages: nativewind 4.2.7, react-native-reanimated 4.7.1, react-native-worklets 0.13.0 and tailwindcss 3.4.19 (dev). All are exact pins. The only Jest config change is the single `[.]css$` mapper.
- **Backend.** `git diff --stat -- backend` is empty.

### 1. Detail price is the card size — NOTE   (`ProductDetailContent.tsx`)
**What I saw:** you render `PriceText` at `text-title` on the detail page, and C3 explains why.
**Why it's fine:** my plan was wrong here. React Native does not pass text styles from a `View` down to a child `Text`, so "larger via a container class" could never work. Adding a prop was not allowed.
**What I want:** nothing in this step. I'm recording it as an owner gap: a `size` prop on `PriceText`, or a `className` passthrough, when the detail page is next touched.
**Lesson:** RN has no CSS inheritance, so size and colour belong on the `Text` itself.

### 2. `thumb` tokens unused, thumbnails hard-code 56 — NOTE   (`lib/design-tokens.js`, `ProductDetailContent.tsx`)
**What I saw:** `width.thumb` and `height.thumb` (72px) have no consumer. The thumbnails pass `width={56}` to `ProductImageView` instead.
**Why it's non-blocking:** Tailwind emits only the classes you use, so this adds no runtime output and is not a bug. It is still a token without a consumer, and the size is a literal in the component.
**What I want:** next time the detail page is touched, either use the token for the thumbnail box or drop it from the tokens.

### 3. Small tidy-ups — NOTE
`ProductListView` calls `showingRange(data)` twice in one template string; compute it once. `tsconfig.json` lost its trailing newline.

### Deviations against §5.5 — all accepted
- **`Link asChild` + `Text`/`Pressable className`:** **Accepted.** expo-router's `Link` is not registered with css-interop, so `className` on it is dropped. `asChild` keeps link semantics on web and native. Roles and names are unchanged, and TopNav, routes and admin tests all pass.
- **Numeric `touch` token (44) beside the `px` strings:** **Accepted.** Both come from one constant in the token file. §5.5's Jest fallback needs a number.
- **`tests/style-stub.js`:** **Accepted.** The plan allowed the one `.css` mapper. No stub existed, and the new one is a single line.
- **`declare module '*.css'` in `nativewind-env.d.ts`:** **Accepted.** It is the minimum tsc needs for the side-effect import.
- **`StyleSheet.absoluteFill` on expo-image:** **Accepted.** expo-image's `Image` is not css-interop-registered, so this is a no-`className` prop exception (§5.5).
- **D4 fallback scope:** **Accepted.** Only CategoryFilter, SortPicker, SearchBar and PriceText keep `StyleSheet.create`, as D4 lists. Each holds only the asserted or layout-critical properties, sourced from tokens.
- **`minHeight.textarea` (96px):** **Accepted.** It has a consumer (`TextField` multiline) and avoids an arbitrary value.

### worklets condition (D1)
**Confirmed.** The lock file shows `react-native-reanimated` 4.7.1 `peerDependencies: "react-native-worklets": "0.13.x"`, and 0.13.0 is installed. D1's condition held, and `expo install --check` is green.

### Mutation checks
I accept the report's five checks (JWT rule, title in the entry, full-key source, `disabled` on Out chips, AccountBar duplicated). Each targets the guard its test names. The test-first red for L1–L3 is quoted.

### Owner-manual (not done; not counted as PASS evidence)
- Web visual comparison against the mockup at 1280 px and 390 px.
- Expo Go phone check (§5.5 step 4): NativeWind styles render natively, `lg:` falls back to stacked, and the missing Stack back arrow (R5) is acceptable.

**What was good:** You found the real reason `className` does not resolve under Jest (css-interop skips registration when `NODE_ENV === 'test'`) and took the planned fallback instead of editing tests. The AccountBar-duplication mutation proved D5's reasoning against the real admin test.
