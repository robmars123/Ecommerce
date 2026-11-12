# Slice P2.7 — Search box with grouped results (`admin-shell`)

Tracker row P2.7 (`docs/admin-dashboard-feature-tracker.md`). Arch §12.3 row "P2 Admin UI (Phase 2)". App context §4.14 (Shell: "The top bar has a breadcrumb, search…"; Search: `GET /api/v1/admin/search?q=` (Admin, Support), up to 5 each of orders, products and SKUs). App context §2 roles (Support: read orders/customers only, never revenue). §13.2 rows "Admin UI sidebar nav (P2.3)" and "Admin search UI and contract (P2.7/P2.8)" (added by this plan).
Mode: Autonomous. **Risk tier: Low** (CLAUDE.md §5): a read-only fetch and display in the shell. No endpoint, no token/claims change, no money, no server auth rule. The client role filter is UI hiding only (rule 11); the server half is P2.8's. Review = Marco's tests + `verify.sh` + Rob reads the diff; no mutations. Rob reads the Support-filter code and tests Q3/C8 line by line.

## Split decision: UI-first, no backend in P2.7

P2.7 builds the box against the contract below, with `fetch` mocked in Jest. P2.8 builds the endpoint and must match the contract (its integration tests pin the shape). Nothing in the docs requires the API in this row. The tracker lists P2.8 as its own Admin-module row, and folding it in would put two rows in one slice (quality bar §1).

**Until P2.8 ships:** the live API answers 404 for the route. 4xx is not retried (`shouldRetry`). The box shows its error state ("Search isn't available right now." + Try again). Nothing crashes and no other screen is affected.

**Tracker:** after Rob's PASS, set P2.7 status to `Done (UI; API in P2.8)`. P2.8's PASS turns it into plain `Done`. This follows the tracker rule "Done only when its API, screen and tests are in".

### The contract (binding on P2.8; recorded in §13.2 "Admin search UI and contract")

`GET /api/v1/admin/search?q=<text>`. Roles: Admin and Support (§4.14). Anonymous → 401, Customer → 403. `q` is trimmed and must be 2–100 chars, otherwise 400 Problem Details. 200 body:

```ts
type AdminSearchResult = {
  orders:   { id: string; orderNumber: string; status: OrderStatus }[];   // ≤ 5
  products: { id: string; name: string }[];                               // ≤ 5
  skus:     { sku: string; productId: string; productName: string }[];    // ≤ 5
};
```

- **Support** always gets `products: []` and `skus: []` from the server. Support can't read the catalog (app context §2 "Read orders, customers, and timelines"; §5 `/admin/products` is Admin only).
- **No money field anywhere.** Support must never see revenue (app context §2, §4.8), and search has no use for totals. So no role-dependent money field exists.
- Match rules and ordering inside each list belong to P2.8. The client renders the lists in the order the server sends them.

## Read:

- This plan.
- `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` and `AdminShell.test.tsx` (helpers `renderShell`, `renderShellAt`; S1–S25).
- `frontend/apps/store/features/admin-shell/breadcrumb.ts` (style for a pure module) and `features/auth/admin-access.ts` (`adminRouteDecision`).
- `frontend/apps/store/features/admin-customers/api.ts` and `api.test.ts` (client and test pattern). Also `features/catalog/hooks/useDebouncedValue.ts`, `features/catalog/components/SearchBar.tsx` and `SearchBar.test.tsx` (fake-timer debounce test pattern).
- `frontend/apps/store/lib/query-keys.ts` (+ test), `tests/render-with-query.tsx`, `tests/fetch-mock.ts`, `components/ErrorState.tsx`, `components/LoadingState.tsx`, `features/orders/status-labels.ts` (`statusLabel`).
- `docs/quality-bar.md` §1, §3, §4.
- Nothing else. No backend files.

## Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| UI-first. P2.7 is frontend only, and the contract above binds P2.8 | Two tracker rows, two slices. The docs fix roles, limits and groups, which is enough to pin the shape | Blocking P2.7 on P2.8. Folding the Admin-module endpoint into a UI row |
| Minimal result shape: no money, no emails, no `isActive`/`variantId` | Every field has a consumer in this slice (label or link). Leaving money out entirely removes the Support revenue risk instead of guarding it | Reusing the admin order list item (`grandTotal`, `customerEmail`). That means a role-dependent null and PII in a type-ahead |
| Groups are shown only when the role can open their link target: `adminRouteDecision(role, '/admin/orders')` → Orders, `'/admin/products'` → Products and SKUs. Empty groups are hidden | One role table for gate, nav and search, so they can't drift (§13.2 P2.3 row). A Support link to a product page would bounce to `/admin`. It also protects against a stale cache from another account or a server bug. The server is still the authority (P2.8) | Rendering whatever arrives (12.2 "client never reads the role" applies to a server-withheld *value*. Here the issue is a dead *link*). A separate per-group role list |
| Links: order → `/admin/orders/${encodeURIComponent(id)}`. Product → `/admin/products/${encodeURIComponent(id)}`. SKU → its product's page `/admin/products/${encodeURIComponent(productId)}` | These are existing detail routes (app context §5). There is no per-variant route, and the product page edits its variants | Linking SKU to `/admin/inventory`. That page is paged with no variant filter, so the SKU might not be on page 1 |
| Pure module `features/admin-shell/search.ts`: `searchTerm(raw)` and `searchGroups(role, result)`. The component only maps them | The role filter and hrefs are the only logic, so they're tested as tables (same as `breadcrumb.ts`) | Logic inside the component |
| `useQuery` lives in an **internal** `SearchResults` component that is mounted only while the panel is open with a valid term. No exported hook | The shell tests S1–S25 render without a `QueryClientProvider`. A hook in the always-mounted box would break all of them. With no exported hook there's no extra hook test to own (§11.3) | Hook in `AdminSearch` with `enabled`. That forces a provider into the existing shell helpers and edits 25 tests |
| `queryKeys.admin.search(q)` = `['admin','search',q]`, no `placeholderData` | Keyed by term, so a late answer for `ab` can't render under `abc`. Without kept data, a new term shows Loading, never stale groups (same as 12.2) | `keepPreviousData` (shows the wrong results for the typed text) |
| Debounce 300 ms with `useDebouncedValue` (catalog hook, already shared by admin-customers through `SearchBar`). Constant `ADMIN_SEARCH_DEBOUNCE_MS = 300` in `search.ts` | Same feel as the other searches. No new hook | Reusing `SearchBar` (its label "Search products", its fixed styles and its emit-on-change contract don't fit a type-ahead) |
| Term: trim, then `slice(0, 100)`. Length < 2 → `null`, so no request. The input has `maxLength={100}`. 1 char shows the hint "Type at least 2 characters." | Matches the §4.14/P2.8 `q` range, so the client never triggers a 400 | Sending and letting the server 400 |
| States inside the panel (`accessibilityLabel="Search results"`): hint · `LoadingState` · `ErrorState` (fixed copy "Search isn't available right now.", `onRetry` = refetch) · empty "No matches for “{term}”." · groups | Reuse of the shared state components. Fixed error copy because the pre-P2.8 404 would otherwise read like "not found" | `userMessage(error)` text |
| Close rules: **Escape** (`onKeyPress`, `nativeEvent.key === 'Escape'`) hides the panel and keeps the text, and typing reopens it. **Clear** button (shown when there is text) empties the text and closes. Pressing a result clears and closes. A **pathname change** (`usePathname` effect) clears and closes | Escape for web and hardware keyboards. Clear for touch, because phones have no Escape. Navigation from the sidebar or a result must not leave old results in the bar. `onKeyPress` exists on all platforms, so this is not platform code (rule 17) | Close on blur (on web, blur fires before the result press and swallows it). A modal or overlay (z-index/elevation issues on Android) |
| The panel is **inline** in the top bar and pushes the content down. No absolute overlay | Works the same on web, iOS and Android at any width. Nothing to stack | Absolute dropdown |
| **Wide:** the top bar row becomes `flex-row items-start justify-between gap-md`: the `Breadcrumb` wrapper is `flex-1`, and `AdminSearch` sits in a `w-sidebar max-w-full` column with the panel under the input. **Phone:** a third top-bar row holds `AdminSearch` at full width (rows: Menu+brand · breadcrumb · search). It's visible without opening the menu | It reuses the existing `width.sidebar` token, so there's no new token. Phone search must work without the overlay. Breadcrumb rows are unchanged (S19–S25) | A new width token. A search toggle button on phones (extra state for no gain) |
| Rendered only when there is a signed-in `user` (`<AdminSearch role={user.role} />`). Placeholder "Search", `accessibilityLabel="Search admin"`, `placeholderTextColor={colors.muted}` (§5.5 JS-prop exception, as in `SearchBar`) | `AdminGate` already guarantees an Admin or Support. A neutral placeholder doesn't promise products to Support | A role-specific placeholder function |
| Item labels: order `${orderNumber} · ${statusLabel(status)}`, product `name`, SKU `${sku} · ${productName}`. Group headers `Orders`, `Products`, `SKUs` (`accessibilityRole="header"`), in that order | Plain text from the response, with the status label reused | Raw status enum text |
| Theme tokens only (`bg-surface`, `border-border`, `text-body`, `text-caption`, `text-muted`, `text-foreground`, `rounded-input`, `px-lg`, `gap-xs`, `min-h-touch`, `w-sidebar`). No `style=`, hex or rgb | Light and dark mode for free (§5.5) | — |

## Files

| File | Change |
|---|---|
| `frontend/apps/store/features/admin-shell/types.ts` | **New.** `AdminSearchOrder`, `AdminSearchProduct`, `AdminSearchSku`, `AdminSearchResult` exactly as the contract (`OrderStatus` from `features/orders/types`). A header comment cites §13.2 "Admin search UI and contract". |
| `frontend/apps/store/features/admin-shell/api.ts` | **New.** `fetchAdminSearch(q: string, signal?: AbortSignal): Promise<AdminSearchResult>` → `apiGet('/api/v1/admin/search', { params: { q }, signal })`. |
| `frontend/apps/store/features/admin-shell/api.test.ts` | **New.** A1–A2. |
| `frontend/apps/store/features/admin-shell/search.ts` | **New.** `ADMIN_SEARCH_DEBOUNCE_MS = 300`. `searchTerm(raw: string): string \| null`. `type SearchGroup = { title: 'Orders' \| 'Products' \| 'SKUs'; items: { key: string; label: string; href: string }[] }`. `searchGroups(role: UserRole \| null, result: AdminSearchResult): SearchGroup[]` (role filter + hrefs + labels, empty groups dropped). Keys: order `id`, product `id`, SKU `sku`. |
| `frontend/apps/store/features/admin-shell/search.test.ts` | **New.** Q1–Q5. |
| `frontend/apps/store/features/admin-shell/components/AdminSearch.tsx` | **New.** `export function AdminSearch({ role }: { role: UserRole })` with input, Clear, Escape and pathname close. Internal `SearchResults({ term, role, onNavigate })` with `useQuery` and the states. Results use `Link href asChild` + `Pressable accessibilityRole="link" onPress={onNavigate}` (sidebar pattern). |
| `frontend/apps/store/features/admin-shell/components/AdminSearch.test.tsx` | **New.** C1–C11. |
| `frontend/apps/store/features/admin-shell/components/AdminShell.tsx` | Wide top bar: row with breadcrumb `flex-1` + `AdminSearch` column. Phone: third row. Nothing else changes. |
| `frontend/apps/store/features/admin-shell/components/AdminShell.test.tsx` | Add `describe('AdminShell search')` with S26–S29 and one new helper `renderShellWithQueryAt(width, url)` (`renderRouterWithQuery` or a `_layout` with `QueryClientProvider`, same routes as `renderShellAt` + `admin/orders/[id]` stub). S1–S25 and existing helpers unchanged. |
| `frontend/apps/store/lib/query-keys.ts` (+ `query-keys.test.ts`) | Add `admin.search: (q: string) => ['admin', 'search', q] as const` and one test K1. |
| `docs/admin-dashboard-feature-tracker.md` | P2.7 → `Done (UI; API in P2.8)` only after Rob's PASS. |
| `slice-plans/backlog.md` | One line: manual check of the P2.7 search at 1280 and 390 px, light and dark, on web :8081, for Nathaniel. |

No new packages, tokens, routes, providers or backend files. Rob has already added the §13.2 row. Marco doesn't edit docs other than the tracker.

## Build order

1. Write Q1–Q5, A1–A2, K1, C1–C11 and S26–S29 first. Run `npx jest features/admin-shell lib/query-keys 2>&1 | tail -n 20` and quote the red output (missing modules/label). **Q3 and C8 (Support filter) must be shown red first.**
2. `types.ts`, `api.ts`, the `query-keys` entry → A and K green.
3. `search.ts` → Q green.
4. `AdminSearch.tsx` → C green.
5. Wire into `AdminShell.tsx` → S26–S29 green. S1–S25 must pass **unedited**.
6. Token check: `git -c safe.directory='*' diff -- frontend/apps/store/features/admin-shell; git -c safe.directory='*' status --porcelain frontend/apps/store/features/admin-shell`, then grep the new files for `#[0-9a-fA-F]{3,8}|rgb|style=`. It must return nothing. Quote it.
7. Run the full `bash scripts/verify.sh 2>&1 | tail -n 30`, quote the `VERIFY` line, and write `slice-P2.7-marco-report.md` (≤ 6 KB). Say whether you eyeballed :8081 at 1280 and 390 px. Expect the error state there, because P2.8 isn't built.

## Required tests (Jest). All test-first.

Fixture: one `makeSearchResult(overrides?)` at the top of `search.test.ts`, with a copy in `AdminSearch.test.tsx`, or a new `tests/admin-search-fixtures.ts` if both use it (preferred). Full result: orders `[{ id: 'o1', orderNumber: '1001', status: 'Paid' }]`, products `[{ id: 'p1', name: 'Blue Mug' }]`, skus `[{ sku: 'MUG-BLU-L', productId: 'p1', productName: 'Blue Mug' }]`.

`search.test.ts` (pure):

| ID | Test |
|---|---|
| Q1 | `searchTerm` `it.each`: `''`, `' '`, `'a'`, `' a '` → `null`. `'ab'` → `'ab'`. `'  ab  '` → `'ab'`. 101 × `'x'` → 100 × `'x'`. `' ' + 'x'.repeat(100)` → 100 chars. |
| Q2 | Admin + full result → exactly `[Orders, Products, SKUs]` with exact `toEqual` items: labels `1001 · Paid` (via `statusLabel`), `Blue Mug`, `MUG-BLU-L · Blue Mug`. Hrefs `/admin/orders/o1`, `/admin/products/p1`, `/admin/products/p1`. An id `a/b` encodes to `a%2Fb`. |
| Q3 | **Support** + full result → exactly `[Orders]`. No href in the output starts with `/admin/products`. Support + only products/skus → `[]`. `Customer` and `null` → `[]`. |
| Q4 | Admin: empty groups dropped (only skus → `[SKUs]`; all empty → `[]`). The server's item order is kept (two orders in reverse id order stay in that order). |
| Q5 | Role invariant: for `Admin` and `Support` with the full result, every item `href` satisfies `adminRouteDecision(role, href) === null`. |

`api.test.ts`:

| ID | Test |
|---|---|
| A1 | `fetchAdminSearch('ab & c')` calls exactly one URL whose pathname is `/api/v1/admin/search` and whose `searchParams` are `{ q: 'ab & c' }` (encoded on the wire). It resolves to the body. The signal is passed (check `init.signal` on the spy call). |
| A2 | A 404 problem response (the state before P2.8) rejects with `ApiError`, `status` 404. |

`query-keys.test.ts`: **K1** `admin.search('ab')` equals `['admin','search','ab']` and differs from `admin.search('abc')`.

`AdminSearch.test.tsx`: `jest.useFakeTimers()`, `renderRouterWithQuery` with routes `index` → `<AdminSearch role={role} />`, plus `admin/orders/[id]` and `admin/products/[id]` stubs. Query results inside `within(screen.getByLabelText('Search results'))`. Advance with `await act(() => jest.advanceTimersByTimeAsync(n))`.

| ID | Test |
|---|---|
| C1 | Type `a`, advance 1000 → hint "Type at least 2 characters." and `fetch` never called. Type `''` → no `Search results` panel. |
| C2 | Debounce: type `ab`, advance 299 → 0 calls. Advance 1 → exactly 1 call with `q=ab`. Then type `abc`, `abcd` within 300 ms, advance 300 → one more call, `q=abcd` (no `abc` call). `'  ab '` sends `q=ab`. |
| C3 | Loading: a never-resolving fetch → `Loading` inside the panel, no groups. |
| C4 | Admin, full result → headers `Orders`, `Products`, `SKUs` (role header) and links `1001 · Paid`, `Blue Mug`, `MUG-BLU-L · Blue Mug`. Pressing the order link → `toHavePathname('/admin/orders/o1')`, the panel is gone, and the input value is `''`. |
| C5 | Pressing the SKU link → `toHavePathname('/admin/products/p1')`. |
| C6 | Empty: all lists empty → "No matches for “ab”.", and no header. |
| C7 | Error: a 404 problem → alert "Search isn't available right now." and exactly 1 call. Pressing `Try again` → 2 calls. A later 200 shows the groups. |
| C8 | **Support**, full result → `Orders` header and link present. No `Products`/`SKUs` header, and no link whose name contains `Blue Mug`. Support with only products/skus → the "No matches" copy. |
| C9 | Escape: with results visible, `fireEvent(input, 'keyPress', { nativeEvent: { key: 'Escape' } })` → panel gone, input value kept, no new fetch. Typing another char reopens the panel. |
| C10 | Clear: with text present, the `Clear search` button empties the input and removes the panel. No new fetch. The button is absent when the input is empty. |
| C11 | Stale answer: the `q=ab` fetch is held on a deferred promise. Type `abc`, and its fetch resolves with a different order (`1002`). Then resolve `ab` (`1001`). Only `1002 · Paid` is shown. Also assert the input `maxLength` prop is 100. |

`AdminShell.test.tsx` additions (S1–S25 untouched):

| ID | Test |
|---|---|
| S26 | Wide, existing `renderShell(1024)` (no QueryClient): the `Search admin` input is present, the brand and `Body` render. This proves the always-mounted box needs no provider. |
| S27 | Narrow 390 via `renderShellAt`, `/admin/orders`: the `Search admin` input is visible without opening the menu. `Open menu` is collapsed and the Breadcrumb is still present. |
| S28 | Narrow 390, `renderShellWithQueryAt`, Admin: type `ab` and advance 300 ms → results appear. Pressing `1001 · Paid` → `/admin/orders/o1`. `Admin navigation` stays absent (overlay closed). |
| S29 | Wide, `renderShellWithQueryAt` at `/admin`: with results open, pressing the sidebar `Orders` link → `/admin/orders` and `Search results` is gone (pathname close). |

## Risks

- **Contract drift with P2.8:** the client trusts the shape. P2.8's plan must cite §13.2 "Admin search UI and contract" and include integration tests for the 200 shape, ≤ 5 per list, Support `products`/`skus` empty, 400 for `q` length 1 and 101 (after trim), and 401/403. Rob checks this when planning P2.8.
- **Fake timers + router + TanStack:** if `renderRouterWithQuery` hangs under fake timers, use `jest.useFakeTimers({ doNotFake: ['nextTick', 'setImmediate'] })`. Don't switch to real timers with `waitFor` sleeps, and don't mock `useDebouncedValue`.
- **`getByText` collisions:** `Orders`/`Products` appear in the sidebar, the breadcrumb and the group headers. Scope with `within(...)`, and use `getByRole('header', …)` for headers.
- **Escape on native:** there is no Escape key on touch devices, so Clear and navigation cover close there. Jest only simulates the key event.
- **Wide top-bar height** grows while results show (inline panel). That's accepted. P2.9 (pill) and P2.10/11 (badges) will share this bar, so keep the search column self-contained.
- **Scope creep:** no backend, no pill, no badges, no arrow-key result navigation, no recent-searches, no submit-to-search page.
