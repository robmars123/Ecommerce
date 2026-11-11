# Slice P2.7 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run by me (full run is for the PASS round only, CLAUDE.md §5); your report quotes `VERIFY PASS`, jest 189 suites / 1445 tests.

Low tier, frontend only. The diff is small and on-plan: pure `search.ts`, an internal `SearchResults` with the query, the contract types, the shell wiring, K1. One planned rule has no test that proves it, so this round is BLOCKED.

### 1. Route-change close is untested — S29 passes for the wrong reason — BLOCKING   (`features/admin-shell/components/AdminShell.test.tsx` S29, `renderShellWithQueryAt`; `AdminSearch.tsx:58`)
**What I saw:** `renderShellWithQueryAt` gives every route its own `<AdminShell>`. Going from `/admin` to `/admin/orders` mounts a new shell with an empty search box, so `Search results` is gone whether or not `AdminSearch` reacts to the pathname. I checked it. I replaced `useEffect(reset, [pathname])` with a no-op in a scratch edit, ran `npx jest features/admin-shell/components/AdminShell.test.tsx -t "S29"`, and got `Tests: 58 skipped, 2 passed`. Then I restored the file from my backup copy and `cmp` matched. C4 doesn't cover it either: there, `onNavigate={reset}` clears the box directly.
**Why it matters:** in the app, `AdminShell` sits in the admin `_layout` and stays mounted across navigation (§13.2 P2.1–P2.2 row). If someone deletes or breaks that effect, a sidebar click to Orders leaves the old results open over the new page. The decision row "Admin search UI and contract" promises "closes on … route change", and no test would fail.
**What I want:** make S29 match the real tree. Mount `AdminShell` once in a `_layout` route around a `Slot` (or `Stack`). The admin pages are plain `Body` stubs under it. Keep the S29 assertions: results open, then the sidebar `Orders` press gives `/admin/orders` and no `Search results`. Also assert the input value is `''`. Show the red: neutralise line 58 in a scratch copy, quote the S29 failure, restore and `cmp`. If `S28` uses the same helper, it must still pass.
**How I'll check:** I'll re-run that same neutralisation against S29 and expect red.
**Lesson:** a test proves a rule only when the tree under test matches production. If each route remounts the component, a "state resets on navigation" test passes no matter what the code does.

### 2. 1 char after a valid term shows the old results for up to 300 ms — NOTE (accepted)
**What I saw:** `term` comes from the debounced text. Going from `ab` to `a` keeps `ab`'s groups visible until the debounce fires, and then the hint shows. This is the same lag as `ab` → `abd`. It's inherent to debouncing, and keys are still per term, so an answer never shows under a different term than the one it was fetched for. The plan's "1 char shows the hint" holds once the debounce settles. I accept it. A cheap tightening for later: show the hint while `searchTerm(text) === null`, whatever the debounced value is. Sent to backlog.

### 3. `tests/settle.ts` real-clock wait — NOTE (accepted)
**What I saw:** `settleFetch` waits 3 × 10 ms on the real clock (captured before the fake timers go in), then calls `advanceTimersByTimeAsync(0)`. It doesn't mock the debounce, and it doesn't swap to real-timer `waitFor`. It doesn't weaken any assertion. Every test that uses it makes a **positive** assertion after settling (C4–C8 headers/links, C7 the alert, C11 `1002`), so a slow settle fails red, not a false green. The risk is load flakiness, not hidden bugs. The one negative that depends on settling is C11's "no `1001`", but it is also guaranteed by the per-term key. The plan's preferred route was `useFakeTimers({ doNotFake: ['nextTick','setImmediate'] })`. If either suite times out under full-verify load, switch to that instead of raising the 10 ms. Sent to backlog.

### 4. Q3 / C8 red — NOTE
Your red for both was "Cannot find module". That is a real red, but it doesn't show that the assertions tell the roles apart. I read them line by line, and they do. Q3 pins `['Orders']` exactly, plus no `/admin/products` href, Support with only catalog → `[]`, and Customer/null → `[]`. C8 first asserts the Orders header and link (proving the data landed), then no Products/SKUs header and no `Blue Mug` link, then the no-match copy for catalog-only data. If `canProducts` were dropped from `searchGroups`, both would fail. No action needed.

### Contract check
`types.ts` and `api.ts` match §13.2 "Admin search UI and contract": `orders {id, orderNumber, status: OrderStatus}`, `products {id, name}`, `skus {sku, productId, productName}`. There is no money or email field. `GET /api/v1/admin/search` takes `q` as a query param, and the signal is passed (A1). Confirmed.

**What was good:** the role filter reuses `adminRouteDecision` on the real link targets, and Q5 pins that invariant directly. Putting `useQuery` in an internal component mounted only with a valid term kept S1–S25 provider-free and unedited (S26 proves it).

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); jest 189 suites / 1445 tests passed.

**1. S29 "closes on route change" — Confirmed.** The fix mounts one `<AdminShell><Slot/></AdminShell>` in a `_layout`, so a navigation changes the pathname inside a single mounted shell, and S29 now asserts the input value is `''`. I re-ran your break myself. I copied AdminSearch.tsx to scratch and no-op'd line 58 (`setText('')`). S29 went red (`Tests: 2 failed, 58 skipped`, one failure in each of the 2 jest projects). I restored from the scratch copy and `cmp` returned identical.

**What was good:** the test now exercises the real route-change path in a single mounted shell, so it can no longer pass by remounting.
