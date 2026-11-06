# Frontend naming review — Rob

Mode: Review (scoped task, not a slice) · 2026-11-03
Scope: `frontend/apps/store/` against the new architecture §5.4 (naming conventions).
Verdict: **BLOCKED**. Naming only. No logic bugs. Marco applies the fix list below.

## Blocking failures (the fix list for Marco)

All 21 route files keep their file names and paths. Do not move or rename any file in `app/`. Only the default-export function names change.

### A. Shared components: switch to named exports (§5.4 "Components")

- `frontend/apps/store/components/ScreenStub.tsx:11`: `export default function ScreenStub` → `export function ScreenStub`
- `frontend/apps/store/components/StubLink.tsx:9`: `export default function StubLink` → `export function StubLink`

### B. Import alias `@/` (§5.4 "Imports")

- `frontend/apps/store/tsconfig.json:3-5`: add `"paths": { "@/*": ["./*"] }` under `compilerOptions`. Do not add `baseUrl` or a babel plugin, because Expo SDK 58 resolves tsconfig `paths` natively.
- In every route file, change `import ScreenStub from '../…/components/ScreenStub'` to `import { ScreenStub } from '@/components/ScreenStub'`. Make the same change for `StubLink` in `app/index.tsx:2` and `app/+not-found.tsx:2`. The lines are:
  - line 1 in `+not-found.tsx` (lines 1 and 2), `index.tsx` (lines 1 and 2), `cart.tsx`, `checkout.tsx`, `login.tsx`, `register.tsx`, `reset-password.tsx`, `orders/index.tsx`, `products/index.tsx`, `admin/index.tsx`, `admin/inventory.tsx`, `admin/returns.tsx`, `admin/customers/index.tsx`, `admin/orders/index.tsx`, `admin/products/index.tsx`
  - line 2 in `orders/[id].tsx`, `products/[slug].tsx`, `admin/customers/[id].tsx`, `admin/orders/[id].tsx`, `admin/products/[id].tsx`

### C. Route component names (§5.4 "Route component": `…ListScreen` / `…DetailScreen` / `…Screen`)

| File:line | Now | Rename to |
|---|---|---|
| `app/_layout.tsx:4` | `RootLayout` | unchanged (correct) |
| `app/+not-found.tsx:4` | `NotFound` | `NotFoundScreen` |
| `app/index.tsx:4` | `Home` | `HomeScreen` |
| `app/products/index.tsx:3` | `Products` | `ProductListScreen` |
| `app/products/[slug].tsx:4` | `Product` | `ProductDetailScreen` |
| `app/cart.tsx:3` | `Cart` | `CartScreen` |
| `app/checkout.tsx:3` | `Checkout` | `CheckoutScreen` |
| `app/orders/index.tsx:3` | `Orders` | `OrderListScreen` |
| `app/orders/[id].tsx:4` | `Order` | `OrderDetailScreen` |
| `app/login.tsx:3` | `Login` | `LoginScreen` |
| `app/register.tsx:3` | `Register` | `RegisterScreen` |
| `app/reset-password.tsx:3` | `ResetPassword` | `ResetPasswordScreen` |
| `app/admin/index.tsx:3` | `AdminDashboard` | `AdminDashboardScreen` |
| `app/admin/products/index.tsx:3` | `AdminProducts` | `AdminProductListScreen` |
| `app/admin/products/[id].tsx:4` | `AdminProduct` | `AdminProductDetailScreen` |
| `app/admin/inventory.tsx:3` | `AdminInventory` | `AdminInventoryScreen` |
| `app/admin/orders/index.tsx:3` | `AdminOrders` | `AdminOrderListScreen` |
| `app/admin/orders/[id].tsx:4` | `AdminOrder` | `AdminOrderDetailScreen` |
| `app/admin/returns.tsx:3` | `AdminReturns` | `AdminReturnListScreen` |
| `app/admin/customers/index.tsx:3` | `AdminCustomers` | `AdminCustomerListScreen` |
| `app/admin/customers/[id].tsx:4` | `AdminCustomer` | `AdminCustomerDetailScreen` |

(All paths are under `frontend/apps/store/`.) Keep `export default` on every route, because Expo Router requires it.

### Acceptance

From `frontend/apps/store`:

- `npx tsc --noEmit` exits 0.
- `npx expo export --platform web` exits 0 and still lists the same routes.
- `grep -rn "export default" components` returns nothing.
- `grep -rn "\.\./" app` returns nothing.

## Already compliant

- Route file names and folders are correct and router-mandated: `reset-password.tsx`, `[id].tsx`, `[slug].tsx`, `_layout.tsx`, `+not-found.tsx`, `index.tsx`.
- `app/` contains only routes and layouts.
- Component file names already match their component names (`ScreenStub.tsx`, `StubLink.tsx`).
- Props types (`ScreenStubProps`, `StubLinkProps`) are `type`s declared beside their components and not exported.
- `styles` is defined with `StyleSheet.create`, at the bottom of each file.
- `app.json`: `name` is a display name, and `slug` and `scheme` are kebab-case. No bundle identifiers are set, which is correct while the domain is still an open decision.
- `package.json` `name: "store"` matches the folder name.
- There is no `eas.json` yet. That is expected, because EAS config arrives with the first cloud build.

## Bugs found

None.

## Decisions made

1. **One underlying rule.** A file is named after its single primary export: components are `PascalCase.tsx` and hooks are `useThing.ts`. Every other file and every folder is kebab-case. This matches the existing `api-client.ts` and `query-keys.ts` in §5.1 and the router's own lowercase segments.
2. **Route exports end in `…Screen`, using a List/Detail pattern.** Without the suffix, route components collide with feature components (`Cart` the screen vs `Cart` the component), and the stack traces and the React DevTools tree become ambiguous.
3. **Named exports everywhere except `app/`.** One name per symbol across the codebase makes grep, rename, and auto-import reliable. Default exports are kept only where Expo Router requires them. Recorded in §13.2.
4. **`@/` alias through tsconfig `paths`, and no barrel files.** Relative `../../../` paths break when files move between features. Barrels cause Metro import cycles and slow down bundling. The alias uses built-in Expo support, so no package is added.
5. **No TS `enum`. Use string unions equal to the API wire values.** Unions are erased at compile time and can't drift from the backend's serialized status names (§7.3).
6. **Tests are co-located `*.test.ts(x)` files, never in `app/`.** The router would turn a test file into a route. Choosing a frontend test runner is out of scope here: the stack lock lists none, and adding one needs a stack decision.
7. **Bundle ids stay unset.** Setting them now would mean inventing a domain (§2 open decisions).

## Doc changes

- `docs/ecommerce-architecture.md`: new §5.4 "Naming conventions", and a new row in §13.2 ("Frontend exports and imports").
- `docs/quality-bar.md` §6: added a one-line pointer to §5.4. A route renamed against the router is classed as a bug.
- I checked for stale paths in `CLAUDE.md`, `README.md`, `.gitignore`, `.dockerignore`, `.github/workflows/ci.yml`, and `docs/*`. All of them already use `frontend/apps/store` and `backend/...`, so nothing needed fixing. The `slice-plans/slice-0-*.md` files still say `apps/store`. They are historical records of Slice 0, so I left them as written.

## Non-blocking notes

- `ScreenStub` and `StubLink` are Slice 0 scaffolding. Each slice should delete them as it replaces the stubs, so they aren't left behind as dead code (quality-bar §6).

## Commands (baseline, before fixes)

- `npx tsc --noEmit` exited 0.
- `npx expo export --platform web` (written to the scratchpad output dir) exited 0 and ended with `Exported: …/dist`.

## Marco fix

Applied items A, B, C. No files moved or renamed; no packages added.

- A: `components/ScreenStub.tsx`, `components/StubLink.tsx` now use named exports.
- B: `tsconfig.json` has `"paths": { "@/*": ["./*"] }` (no baseUrl, no babel plugin). All imports in `app/` are `import { ScreenStub } from '@/components/ScreenStub'` / `import { StubLink } from '@/components/StubLink'`. Expo resolved the alias natively.
- C: all 20 route default-export functions renamed per the table (`_layout` `RootLayout` unchanged); `export default` kept.

Commands (from `frontend/apps/store`):

- `npx tsc --noEmit` -> exit 0
- `npx expo export --platform web` -> exit 0, `Exported: dist`, 24 web bundles (same route set: _layout, +not-found, 4x `[id]`, `[slug]`, cart, checkout, 7x index, inventory, login, register, reset-password, returns, entry)
- `grep -rn "export default" components` -> empty
- `grep -rn "\.\./" app` -> empty

## Re-review

Mode: Review (scoped, not a slice) · 2026-11-03 · Rob
Verdict: **PASS**. A, B, and C are applied as specified. No bugs.

I read every file under `frontend/apps/store/components/` and `app/` and re-ran the commands myself.

- **A:** `components/ScreenStub.tsx:11` and `components/StubLink.tsx:9` are now `export function …`. Props types are unchanged and not exported.
- **B:** `tsconfig.json` contains only the new `"paths": { "@/*": ["./*"] }`. There is no `baseUrl` and no babel plugin. All 20 non-layout route files import through `@/components/...` with named imports. `_layout.tsx` imports nothing local and was not touched (its mtime is still Slice 0's).
- **C:** all 20 default-export names match the table exactly, `RootLayout` is unchanged, and `export default` is kept on every route.
- No route file was renamed or moved. The same 21 files are at the same paths.
- `package.json`, `package-lock.json`, and `app.json` have their Slice 0 mtimes, so no package was added. Route bodies (titles, `useLocalSearchParams`, links) are unchanged.

Commands (from `frontend/apps/store`):

- `npx tsc --noEmit` exited 0.
- `npx expo export --platform web --output-dir <scratchpad>/rereview-dist` exited 0 and produced 24 web bundles: 21 routes (`_layout`, `+not-found`, 4× `[id]`, `[slug]`, `cart`, `checkout`, 7× `index`, `inventory`, `login`, `register`, `reset-password`, `returns`) plus `__common`, `__expo-metro-runtime`, and `entry`. That is the same route set as the baseline, and it means Metro resolved the `@/` alias.
- `grep -rn "export default" components` returned no output (exit 1).
- `grep -rn "\.\./" app` returned no output (exit 1).

Non-blocking: Marco's export wrote `frontend/apps/store/dist/`. It is gitignored (`frontend/apps/store/.gitignore:8`), so it has no effect on the working tree, but future runs should use `--output-dir` in the scratchpad.
