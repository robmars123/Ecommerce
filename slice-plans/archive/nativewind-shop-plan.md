# nativewind-shop — Plan (standalone step, not a numbered slice)

Owner request (Nathaniel): adopt NativeWind (arch §5.5 steps 1–3), restyle Shop (`/products`), Product Detail (`/products/[slug]`) and a top nav to the owner's two mockups (dark "Northline."), add one client logger. This satisfies the §13.2 "Frontend styling" condition "adoption only when Nathaniel asks". Backend: **no change**. Stack lock (§2) and hard rules (§3.4): intact — no BLOCKED item.

**Read:** arch §2 (Frontend styling + Frontend tests rows), §3.4 rules 1, 10, 16, 17 · §5 whole file (`docs/architecture/05-frontend.md`, §5.5 is the adoption recipe) · §10.3 · §11.3 · app context §4.1–§4.3, §5 · decision log rows: `grep -n -i "styling\|client error logging" docs/architecture/13.2-decision-log.md` · quality bar §1, §2, §4 · this plan. Mockups are described in §3 below; you do not need the images.

Search only in: `frontend/apps/store/{app,components,features,lib,tests}` and the new root config files. `features/auth` and `features/admin-catalog` are in scope **only** for the mechanical StyleSheet → className migration.

---

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | Packages (exact pins, no `^`/`~`): `nativewind` 4.2.x (dep) · `react-native-reanimated` at the SDK 58 version via `npx expo install` (dep) · `tailwindcss` 3.4.x (devDep). `react-native-worklets` **only if** `npx expo install react-native-reanimated` lists it as Reanimated's required peer at SDK 58, version from `expo install`. Nothing else. | §2 styling row / §5.5 step 1. nativewind: compiles `className` for web+native, **replaces** `StyleSheet.create` as default. reanimated (+worklets): NativeWind's peer, replaces nothing. tailwindcss: the compiler, v3 because NativeWind 4's css-interop needs `~3`, replaces nothing. | `clsx`/`tailwind-merge` (§5.4: conditional classes as plain strings); `@expo/vector-icons` / `expo-font` (not requested; see D12); Tailwind v4 / NativeWind v5 (§13.2) |
| D2 | Tokens in `lib/design-tokens.js` (CommonJS) + `.d.ts`; `tailwind.config.js` `theme.extend` reads them. Spacing, font sizes, radius, touch, max widths written in **px** (§5.5: native rem = 14px). Components use token utilities (`p-lg`, `text-body`, `rounded-card`), not Tailwind's default rem scale (`p-4`, `text-base`) or arbitrary values (`p-[13px]`). | One source; same size on every platform | Overriding `theme` wholesale (breaks NativeWind preset defaults); rem defaults (renders smaller on native) |
| D3 | Dark theme only, tokens from the mockup (values in §3). Root `StatusBar style="light"`; Stack `contentStyle.backgroundColor` from the token (JS-prop exception, §5.5). | Mockup is dark; no theme toggle was asked for | Light+dark with `dark:` variants (unplanned feature) |
| D4 | NativeWind-in-Jest spike **first**. If `className` resolves to style in Jest, every asserted style moves to `className`. If not, only the **asserted property** stays in `style` from tokens (§5.5 Tests bullet, extended this step to PriceText/CategoryFilter/SortPicker/SearchBar). Either way, the 7 style-asserting tests stay byte-identical. | §5.5; tests are the accessibility/layout invariants | Editing tests to match the migration (blocking, quality bar §1) |
| D5 | Top nav = `components/TopNav.tsx`, mounted as the root Stack's `screenOptions.header` (`app/_layout.tsx`). Admin keeps `headerShown: false` + its own Stack, so admin routes don't get it. Content: brand **"Northline."** link → `/` · **Shop** → `/products` (selected on `/products*`) · **Orders** → `/orders` (stub) · `AccountBar` (moved here from `HomeView`, unchanged) · **Cart** → `/cart` (stub). Safe-area top inset via `useSafeAreaInsets` (runtime-value exception). | `tests/app/admin.test.tsx` renders `/` under the real layout and requires exactly one `Log in`, `Register`, `Log out` and staff-only `Admin` link → AccountBar must exist exactly once, so it moves rather than duplicates. Header slot keeps admin tests untouched. | Separate "Sign in" / "Admin · Payments" items (duplicate auth UI; breaks single-match queries); TopNav above the Stack (would also render on admin routes) |
| D6 | "Admin · Payments" = AccountBar's existing staff-only **Admin** link → `/admin`; "Sign in" stays **Log in** / **Register**. | No payments admin route exists (app context §5); AccountBar labels are pinned by existing tests; admin links hidden for non-staff (§5.3, F7) | New `/admin/payments` stub (later slice's route) |
| D7 | Stack per-screen headers are replaced by TopNav on store routes. Back navigation = TopNav links, detail "← Back to shop", OS back/swipe. In-screen `accessibilityRole="header"` titles stay (routes.test, "keeps the Products header"). `Stack.Screen options.title` stays (web document title). | Mockup has no title bar; tests need in-screen headers | Keeping the default Stack header under the nav (double bar) |
| D8 | Shop count line: two sibling `Text`s — `Showing {first}–{last} of` + the **existing** `"{n} products"` text (unchanged string), in one live-region row. `first/last` from new pure `showingRange(page)` in `list-params.ts`. | Matches mockup; existing `getByText('2 products')` stays exact | Rewriting the string (changes an existing test) |
| D9 | Sort stays the existing segmented `SortPicker` (Relevance only with search, Newest, Price ↑/↓), restyled; label prefix "Sort" as plain text before it. No "Featured". | API sorts are relevance/price/newest (app context §4.1.2); `SortPicker.test` presses options directly, so a closed dropdown breaks it; a native select needs a picker package | Dropdown/Modal menu (test change) or `@react-native-picker/picker` (unplanned package) — **gap G4** |
| D10 | Product card: image, category eyebrow (`uppercase` **class**, never `toUpperCase()` — tests match `'Tops'`), name, stock dot + label, price + struck compare-at. **No rating, no "Add to cart".** Grid keeps `columnsFor` (4/3/2), content centred at `max-w-content`. | Only API fields (ProductListItem has no rating); add-to-cart is Slice 3 and on **detail only** (§12.3 row 3) | Fake ratings; disabled stub buttons — **gaps G1, G3** |
| D11 | Detail: "← Back to shop" link (`/products`) in `ProductDetailView` above content (all states; not in the admin preview, which reuses `ProductDetailContent`). Two columns at `lg:` (gallery left, info right), stacked below. Info: eyebrow, name header, price + struck compare-at, description, variant group label, chips, stock, then divider + **SKU** row (selected variant's `sku`). **No Save badge, no stepper, no Add-to-cart button, no Shipping/Returns/Cart rows.** | `ProductDetailView.test` "has no add-to-cart control in this slice" must stay unchanged; Save $X = client money arithmetic (rule 1, quality bar §3 Frontend); shipping methods are Slice 4 (`GET /shipping-methods`), returns copy isn't in the docs, guest cart is Slice 3 | Stub button / stepper (breaks that test; cap `Cart:MaxQuantityPerLine` belongs to Slice 3, §13.2); client `compareAt − price` — **gaps G2, G5, G6** |
| D12 | Variant chips: `stockStatus === 'Out'` → `disabled`, `accessibilityState {disabled, selected}`, `accessibilityHint="Sold out"`, label text struck through. Accessible **name unchanged** (`variantLabel`). Default selection unchanged (first variant, even if Out). Group label: `Size` when every variant has a `size`, else `Option`, followed by the selected label (`Size: S`) via exported pure `variantGroupLabel(variants)`. | Owner ask; name kept so existing tests' `getByRole('button', {name})` still match; "defaults to the first variant" test pins default | Renaming to "XL, sold out" (breaks tests); skipping Out by default (behaviour change, breaks a test) |
| D13 | Stock line: `StockBadge` adds a colour dot (`success`/`warning`/`danger`) beside the **unchanged** text; `Unknown` still renders nothing. Until Slice 2 replaces the stub reader, live data is `Unknown`, so no stock line shows — correct. | Never colour-only; never claim stock without data | — |
| D14 | No icons, system font. Search box: placeholder text only; Cart: text label. | Icons/fonts need new packages not requested | `@expo/vector-icons` as direct dep |
| D15 | Logger per the new §13.2 row "Frontend client error logging": `lib/logger.ts`, called only from `QueryCache`/`MutationCache` `onError` in `createQueryClient`. `api-client.ts` stays console-free (existing "never calls console" tests). | F2 + rule 10; one call per final failure | Logging in `api-client` (breaks two existing tests, sees raw requests); Sentry (new service, §2) |
| D16 | `api-client` attaches the **sent** `X-Correlation-ID` to `network`/`timeout` errors and as fallback when the server sent none: `body.correlationId ?? header ?? sentId`. | A timed-out request may have reached the server; the ID still links logs | Logging without an ID for network failures |

---

## 2. Files

**New (store root):** `babel.config.js`, `metro.config.js`, `tailwind.config.js`, `global.css`, `nativewind-env.d.ts` — exactly per §5.5 step 2 (`content`: `./app/**/*.{ts,tsx}`, `./components/**/*.{ts,tsx}`, `./features/**/*.{ts,tsx}`). `global.css` imported once in `app/_layout.tsx` as `@/global.css`. If Jest can't import `.css`, add one `moduleNameMapper` entry for `\.css$` to an existing-style stub — the only jest-config change allowed; report it.

**New (lib/components):** `lib/design-tokens.js`, `lib/design-tokens.d.ts`, `lib/logger.ts`, `lib/logger.test.ts`, `components/TopNav.tsx`, `components/TopNav.test.tsx`.

**Modified — behaviour:** `app/_layout.tsx` (D3, D5) · `lib/query-client.ts` (+test) · `lib/api-client.ts` (D16, +test) · `features/catalog/list-params.ts` (+test) · `features/catalog/components/ProductListView.tsx` (D8–D10) · `ProductCard.tsx`, `StockBadge.tsx`, `VariantSelector.tsx`, `ProductDetailView.tsx`, `ProductDetailContent.tsx` (D10–D13) · `HomeView.tsx` (AccountBar removed, D5).

**Modified — className migration only (no behaviour change):** every other file with `StyleSheet`/`style=` in `app/`, `components/`, `features/auth/components/`, `features/admin-catalog/components/`, `features/catalog/components/` (34 files today). After this step: no `StyleSheet.create` except the D4 fallback and §5.5 exceptions; `grep -rnE "#[0-9a-fA-F]{3,8}\b|rgba?\(" app components features` returns nothing.

**Not touched:** backend, `docs/quality-bar.md`, `.github/`, `.claude/`, `app.json` (display name stays; brand text is UI only), every existing `it(...)` body.

### Module contracts

```ts
// lib/logger.ts
export type ClientLogEntry = {
  source: string;            // static, e.g. 'products.list' — never params
  kind: ApiErrorKind | 'unexpected';
  status?: number;
  correlationId?: string;
  message?: string;          // only for non-ApiError errors; redacted, ≤ 200 chars
};
export function redact(text: string): string;          // Bearer x → 'Bearer [redacted]'; JWT eyJ…·…·… → '[token]'; emails → '[email]'; 13–19 digit runs → '[number]'
export function logClientError(error: unknown, source: string): void;
```
- `ApiError` → `{source, kind, status, correlationId}`; **never** `title`, `fieldErrors`, `message`, URL, query string or body. Level: `server`/`network`/`timeout`/`unexpected` → `console.error`; other kinds → `console.warn`. One console call per failure: `console[level]('[client-error]', entry)`.
- Source = leading **string** segments of the query/mutation key, max 2, joined by `.` (`['products','detail',slug]` → `products.detail`); no key → `'mutation'`/`'query'`; result passed through `redact`.
- `correlationId`: redacted and capped to 64 chars.

```ts
// list-params.ts
export function showingRange(page: ProductListPage): { first: number; last: number };  // (page-1)*pageSize+1 … +items.length-1
// VariantSelector.tsx
export function variantGroupLabel(variants: ProductVariant[]): 'Size' | 'Option';
```

### Tokens (derived from the mockup; hex only in `lib/design-tokens.js`)

colors: `background #0f1210` · `surface #171b18` (nav, cards, inputs) · `surface-raised #1e231f` (chips) · `border #2a302b` · `foreground #eef1ee` · `muted #9ba39d` · `primary #3dae6f` · `primary-foreground #f5fbf7` · `primary-subtle #1c3326` (active nav bg) · `primary-text #4cc283` (active nav text) · `selected #e8ece9` / `selected-foreground #111412` (active chip) · `success #3dae6f` · `warning #e0a43a` · `danger #e35d4f`.
spacing: `xs 4px · sm 8px · md 12px · lg 16px · xl 24px · 2xl 32px · 3xl 48px`. fontSize: `caption 12px · small 14px · body 16px · title 18px · heading 24px · display 32px`. borderRadius: `control 8px · input 10px · card 12px · pill 9999px`. minHeight/minWidth: `touch 44px`. maxWidth: `content 1200px`. width/height: `thumb 72px`.

---

## 3. Screens (mockup → what to build)

**TopNav** (`surface`, bottom `border`): brand left; Shop/Orders links (Shop selected = `primary-subtle` bg + `primary-text`, `accessibilityState.selected`); right: AccountBar, Cart (outlined `rounded-control`). Wraps on phone. Every pressable `min-h-touch min-w-touch`.

**Shop `/products`:** centred `max-w-content`; in-screen "Products" header (small); full-width SearchBar (`rounded-input`, `surface`, placeholder colour from `muted` token); Sort row; CategoryFilter pills (`rounded-pill`, active `selected`); count row (D8); grid of cards (`surface`, `border`, `rounded-card`, image on top, body padding `lg`); Pagination below. Empty/error/loading states unchanged.

**Card:** eyebrow `text-caption uppercase tracking-wide text-muted` → name `text-title text-foreground` → stock line → price row (price `text-title` bold, compare-at struck `text-muted`).

**Detail `/products/[slug]`:** back link; `lg:flex-row` with gallery (main image + thumbnails, selected thumb `border-primary`) and info column (D11, D12). Price: existing `PriceText`, larger size via a container class, not a new prop.

---

## 4. Build order

1. **Spike (≤ 30 min):** install D1 packages, add the five config files, migrate `StubLink` only; run `npx jest components/StubLink.test.tsx` and `npx expo export --platform web`. Record whether `className` resolves in Jest → choose the D4 branch; quote the output in the report. If Jest can't run under the NativeWind Babel transform at all, **stop and report BLOCKED** (do not alter the Jest preset beyond the `.css` mapper).
2. `npx expo install --check` green; pins exact.
3. **Test-first — logger** (`lib/logger.test.ts`, red first, quote it), then `lib/logger.ts`.
4. Query-client wiring + tests; `api-client` D16 + test. Re-run `lib/api-client*.test.ts` unchanged → still green ("never calls console").
5. Tokens + `tailwind.config.js` `theme.extend`.
6. Migrate shared `components/`, then `features/auth`, `features/admin-catalog`, then catalog — run `bash scripts/verify.sh changed` after each group; existing tests must pass with no edits.
7. TopNav + layout (D3, D5, D7) + TopNav tests; run `tests/routes.test.tsx` and `tests/app/admin.test.tsx` unchanged.
8. Shop restyle (D8–D10, D13) + new tests. 9. Detail restyle (D11, D12) + new tests.
10. Mutation checks (§6). 11. Full `bash scripts/verify.sh`; manual: web export visual vs. mockup at 1280 px and 390 px, and one Expo Go phone check that styles render natively (§5.5 step 4) — say so honestly if not done.

---

## 5. Required tests (§11.3; RNTL by role/label, never class strings)

**Existing tests: zero edits** to any existing `it`/`describe` body, fixture or helper. New `it` blocks may be **appended** to existing test files. Report `git diff --stat -- '*.test.ts*' tests/` and confirm every hunk is an addition.

| Test | File | Must prove |
|---|---|---|
| L1 **test-first** | `lib/logger.test.ts` | `redact` strips Bearer, JWT, email, 16-digit run; leaves a 32-hex correlation ID intact |
| L2 **test-first** | same | ApiError(500, title "user@x.com failed", correlationId) → exactly one `console.error` with `{source, kind:'server', status:500, correlationId}`; serialized call contains no `title`, no email |
| L3 | same | 404/400/401 → `console.warn`; non-ApiError → `kind:'unexpected'`, redacted, ≤ 200-char message |
| L4 | `lib/query-client.test.ts` | failed products query with `search: 'secret term'` logs once (after retries), `source 'products.list'`, carries the correlation ID; the search term appears nowhere in the call; a failed mutation logs once |
| L5 | `lib/api-client.test.ts` | network and timeout `ApiError.correlationId` equals the sent header; server-echoed ID still wins |
| N1 | `components/TopNav.test.tsx` | brand, Shop, Orders, Cart are links that navigate to `/`, `/products`, `/orders`, `/cart`; Shop `selected` on `/products`, not on `/cart`; signed out shows Log in once (no new style assertions — §5.5 allows only the existing touch-target checks) |
| S1 | `list-params.test.ts` | `showingRange`: page 1 of 12 (size 8) → 1–8; page 2 → 9–12; single item → 1–1 |
| S2 | `ProductListView.test.tsx` (append) | "Showing 1–2 of" and "2 products" both shown; still exactly one Products header |
| S3 | `ProductCard.test.tsx` (append) | category text is `Tops` (not upper-cased in the string); no "Add to cart", no rating/review text |
| D-1 | `VariantSelector.test.tsx` (append) | Out variant: `toBeDisabled()`, hint "Sold out", press does **not** call `onSelect`; name stays `variantLabel`; `variantGroupLabel` → `Size` / `Option` |
| D-2 | `ProductDetailView.test.tsx` (append) | "Back to shop" link navigates to `/products` (also present on the 404 state); `Size: Small`-style label updates on selection; SKU row shows `BS-S` then the Medium SKU after selecting Medium; no Save/rating text |

Stepper-bounds tests requested by the owner are **deferred** with the stepper (G2).

## 6. Mutation checks (report each: broken → red → restored)

- Remove the JWT rule from `redact` → L1 red. Pass `error.title` into the entry → L2 red.
- Use the full query key as source → L4 red. Drop `disabled` on Out chips → D-1 red.
- Render AccountBar in both HomeView and TopNav → `tests/app/admin.test.tsx` red (proves D5's reasoning).

## 7. Risks

- **R1 NativeWind under `jest-expo`** — mitigated by the spike and D4; worst case BLOCKED with output quoted, never a test edit.
- **R2 Reanimated 4 / worklets at SDK 58** — `expo install --check` is the gate; Babel plugin comes from `babel-preset-expo`, do not add it twice.
- **R3 Web CSS vs native** — §5.5 step 4 manual Expo Go check; NativeWind `px` tokens only (D2).
- **R4 Console noise** — route tests that hit 500s now print `[client-error]`; acceptable, not silenced by editing existing tests.
- **R5 Header removal on native** — no back arrow on store screens (D7); verify on the phone check.

## 8. Gaps for the owner (not built; each needs a docs/contract decision first)

- **G1 Ratings + review counts** — not in the Catalog contract and no reviews feature in v1 scope. Omitted on card and detail.
- **G2 Detail Add to cart, quantity stepper (−/+), "Add to cart · $total", Cart/guest-merge note** — Slice 3 (cart, `Cart:MaxQuantityPerLine`, §13.2); an existing test pins "no add-to-cart control" until then. The "· $total" must then be a labelled estimate or the server's line value (§5.2, rule 1).
- **G3 Card "Add to cart"** — §12.3 Slice 3 puts add-to-cart on detail only; card quick-add needs an owner decision on which variant it adds.
- **G4 Sort as a dropdown select / "Featured"** — no `featured` sort in the API; a dropdown needs an owner-approved test change or a picker package.
- **G5 "Save $X" badge** — client subtraction is money logic; needs a server-provided saving amount in the contract.
- **G6 Shipping and Returns rows** — shipping comes from `GET /shipping-methods` (Slice 4); returns policy copy isn't in the docs.
- **G7 Brand** — "Northline." is UI text only; `app.json` `name` unchanged until the owner says otherwise.
- **G8 Logging coverage** — render crashes (Expo Router `ErrorBoundary`) and session restore/refresh calls aren't logged (§13.2 row).

## 9. Docs updated with this plan

§5.1 (`logger.ts`), §5.5 Tests bullet (D4 fallback), §13.2 row "Frontend client error logging". At PASS, Rob sets the §5.4/§5.5 "until adopted" sentences to past tense. Building anything in §8 is blocking.
