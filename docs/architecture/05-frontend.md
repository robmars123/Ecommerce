## 5. Frontend architecture

### 5.1 Structure

```
frontend/apps/store/
  app/            # Expo Router screens (app context §5)
  features/       # catalog, cart, checkout, orders, admin — hooks + components
  components/     # shared RN components (rendered to DOM on web via RN Web)
  lib/
    api-client.ts # typed fetch client: auth, correlation ID — all platforms
    query-keys.ts # TanStack Query keys — single source for invalidation
    logger.ts     # client error log: allowlisted fields + correlation ID, redacted, console only (§13.2)
    platform.ts   # ONLY per-platform code: payments SDK, push, secure storage, deep links
```

### 5.2 Rules

- **Estimate, never authority.** The UI shows an estimated total; the API returns the authoritative one.
- **Checkout idempotency.** One UUID per checkout attempt, sent as `Idempotency-Key`. Retries (double-click, network) reuse it.
- **Optimistic UI only where safe.** Cart add: yes. Checkout submit, payment confirm, return request: no — wait for the server.
- **Server result wins.** If price or stock moved, show what changed; never silently keep the old value.
- **Tokens.** The access token is kept in memory only. The refresh token is in SecureStore on native and in an httpOnly `SameSite=Strict` cookie on web, which script can't read (Slice 1b D6). Both sit behind `platform.ts`, and the client refreshes single-flight.
- **SEO tradeoff accepted.** RN Web is client-rendered. If SEO becomes the measured bottleneck, add a Next.js storefront on the same API.
- **v1 budgets (targets):** no full reload on filter change, lazy images with explicit dimensions, at most one large hero image per page.

### 5.3 Routes

Defined in app context §5. Admin routes are hidden in the UI **and** blocked by role in the API.

### 5.4 Naming conventions

One rule underneath all of these: **a file is named after its single primary export when it has one (component → `PascalCase.tsx`, hook → `useThing.ts`); every other file and every folder is `kebab-case`.**

| Thing | Convention | Example |
|---|---|---|
| Route files in `app/` | Router-mandated. Lowercase `kebab-case` segments, `index.tsx`, `[param].tsx`, `_layout.tsx`, `+not-found.tsx`, `(group)/`. Never rename against the router: the file name **is** the URL | `app/reset-password.tsx`, `app/orders/[id].tsx` |
| Route component | `export default function` (required by Expo Router), named `<Area?><Entity>ListScreen` for `index.tsx` lists, `<Area?><Entity>DetailScreen` for `[param].tsx`, otherwise `<Area?><Name>Screen`. Layouts are `<Segment>Layout` | `OrderDetailScreen`, `AdminProductListScreen`, `CheckoutScreen`, `RootLayout` |
| `app/` contents | Routes and layouts only. No components, hooks, helpers, or tests in `app/` — the router turns every file there into a route | — |
| Feature folders | `features/<feature>/`, `kebab-case`, matching §5.1 and the backend module names. Inside: `components/`, `hooks/`, `api.ts`, `types.ts`, `store.ts` (Zustand, cart only) | `features/checkout/hooks/useCheckout.ts` |
| Components | `PascalCase.tsx`, file name = component name, **named export** | `components/PriceText.tsx` → `export function PriceText` |
| Props type | `<Component>Props`, a `type` declared beside the component; exported only if another file needs it | `type PriceTextProps = { … }` |
| Hooks | `useThing.ts` (camelCase, file = hook name), named export. TanStack Query hooks say what they do: `use<Entity>Query`, `use<Action>Mutation` | `useOrderQuery`, `usePlaceOrderMutation` |
| Lib / util modules | `kebab-case.ts`, named exports | `lib/api-client.ts`, `lib/query-keys.ts` |
| Platform splits | Metro extensions on the same base name, only under `lib/` (§5.2) | `lib/platform.native.ts`, `lib/platform.web.ts` |
| Types | `PascalCase`, no `I` prefix, no `T` prefix. Prefer `type`. API payloads are named after the API resource, with `Request` for bodies sent | `OrderSummary`, `CheckoutRequest` |
| Enumerations | No TS `enum`. A string-literal union whose values equal the API wire values exactly | `type OrderStatus = 'PendingPayment' \| 'Paid' \| …` |
| Constants | Module-level primitive constants `SCREAMING_SNAKE_CASE`; objects and functions `camelCase` | `MAX_CART_LINES`, `queryKeys`, `styles` |
| Variables / functions | `camelCase`. Booleans `is/has/can…`; handler props `on…`, handler functions `handle…` | `isPaid`, `onSubmit`, `handleSubmit` |
| Imports | Absolute alias `@/` (tsconfig `paths`, natively supported by Expo/Metro) instead of `../../..`. No barrel `index.ts` files — import the file directly | `import { ScreenStub } from '@/components/ScreenStub'` |
| Tests | Co-located `<file>.test.ts(x)`, same base name as the file under test — never under `app/`. Tests whose subject is a route live in top-level `tests/`: the route-table smoke test is `tests/routes.test.tsx`; a route-specific test mirrors the route path under `tests/app/` (§11.3) | `lib/query-keys.test.ts`, `tests/app/checkout.test.tsx` |
| Styles | Tailwind classes in `className` (NativeWind) by default — static classes in the JSX, conditional classes as plain string expressions. `StyleSheet.create` only for the exceptions in §5.5; then `const styles = StyleSheet.create({…})` at the bottom of the file, keys `camelCase`. **Until NativeWind is adopted (§5.5), `StyleSheet.create` stays the rule** | `className="flex-1 bg-surface px-4"` |
| Design tokens | Colours, spacing, radius, font sizes and the touch target are named tokens in `tailwind.config.js` (`theme.extend`, sourced from `lib/design-tokens.js`, §5.5), never hex/rgb or one-off pixel literals in components, features, or `app/`. Token names say what they are for (`primary`, `surface`, `muted`, `danger`), not the colour | `bg-primary`, `text-muted`, `rounded-card`, `min-h-touch` |
| `app.json` | `name` = display name; `slug` and `scheme` lowercase `kebab-case`. `ios.bundleIdentifier` and `android.package` stay **unset** until the production domain is chosen (§2 open decisions), then reverse-DNS of that domain (`android.package` lowercase, no hyphens). Never invented | `"slug": "ecommerce-store"` |

### 5.5 Styling (NativeWind + Tailwind)

Owner decision (Nathaniel, 2026-11-03; §13.2). Tailwind does not run on React Native directly; NativeWind compiles Tailwind classes to RN styles on iOS and Android and to CSS on web, from the same `className`.

- **Default:** `className` with Tailwind utilities and the project tokens. No inline `style={{…}}` objects for static values.
- **`StyleSheet` / `style` is still allowed** only for: values computed at runtime (image aspect ratio from API dimensions, safe-area insets, measured layout); Reanimated animated styles; and props that take a style object or colour value with no `className` mapping in NativeWind (for example `ActivityIndicator color`, `placeholderTextColor`). In those cases the colour or size still comes from the token source below — never a literal.
- **Tokens:** one source, `lib/design-tokens.js` (CommonJS, typed by `lib/design-tokens.d.ts`), consumed by `tailwind.config.js` `theme.extend` and by the rare JS-prop cases above. Colours, spacing, radius, and font sizes live there; no hex/rgb in `components/`, `features/`, or `app/`.
- **Touch targets:** the 44 minimum (44px on web, 44pt/dp on native; quality bar §6 "usable touch targets") is the token `touch: '44px'` (NativeWind maps `px` to RN density-independent units) in `minHeight`/`minWidth`, used as `min-h-touch min-w-touch` on every pressable. Do **not** use `min-h-11`: NativeWind's native `rem` is 14px, so `rem`-based spacing renders smaller on iOS/Android than on web. Tokens that must be exact on every platform are written in `px`.
- **Platform differences** in styling use NativeWind's `ios:` / `android:` / `web:` variants, not `Platform.OS` in components (rule 17 keeps platform code in `lib/platform.ts`).
- **Tests** assert behavior by role and label (§11.3), never class strings. The one allowed style assertion is the existing touch-target check (`toHaveStyle({ minHeight: 44 })` in `StubLink`, `CategoryFilter`, `ErrorState` tests): it is an accessibility invariant and must not be weakened or deleted by the migration. If NativeWind's Jest transform does not resolve `className` to a style object, those pressables take `minHeight`/`minWidth` from the token via `style` (an allowed exception above) so the tests stay as they are. The same fallback covers the other existing style-asserting tests (compare-at `line-through` in `PriceText`, scroller `flexGrow`/`flexShrink` in `CategoryFilter`/`SortPicker`, search input `flex`/`minWidth` in `SearchBar`): only the asserted property stays in `style`, everything else moves to `className`.

**Adoption — a separate, small step, run only when Nathaniel asks (not scheduled into any slice).** Marco, with a Rob plan and review:
1. `npx expo install nativewind react-native-reanimated` and `npm i -D tailwindcss@^3.4` (v3 LTS — NativeWind 4's `react-native-css-interop` requires `tailwindcss ~3`), then pin exact versions as for every other package; `npx expo install --check` must stay green.
2. Config: `tailwind.config.js` (`presets: [require('nativewind/preset')]`, `content` globs for `app/`, `components/`, `features/`); `global.css` with the three `@tailwind` directives, imported once in `app/_layout.tsx` (the web CSS entry); `babel.config.js` with `jsxImportSource: 'nativewind'` on `babel-preset-expo` plus `nativewind/babel`; `metro.config.js` wrapped in `withNativeWind(config, { input: './global.css' })`; `nativewind-env.d.ts` for `className` types.
3. Migrate the existing Slice 1a components and screens from `StyleSheet` to `className` + tokens, with no behavior change. The known 1a layout bugs (chip ScrollViews on web, search box overflow) are fixed in their own step, not mixed in.
4. Gates (quality bar §4, unchanged): `npx tsc --noEmit`, `npm test -- --ci` (Jest runs through the same Babel config, so the NativeWind transform must work under `jest-expo`), `npx expo export --platform web` (the CSS is emitted), and a manual Expo Go check on one phone that styles render natively.

Until that step lands, slices keep using `StyleSheet.create` — no half-configured `className` in the codebase.


