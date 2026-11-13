# Slice P2.13 — Review thread

## Round 1 — Rob → Marco (Part A: P2.13 greeting + P2.11 Inventory badge)
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 218 suites / 1698 tests, first run green.

Part A matches the plan. The backend is two additive integers on an existing guarded endpoint. The count and the list share one predicate. The shell stays query-free, and the shared Today key gives one request on `/admin`. Low tier, so no mutations. I read the whole backend diff, every fake edit, every new or appended test, and the frontend diff.

### Checks against the brief

- **Test-first.** You quote red for IA1 and IA2 (`KeyNotFoundException`, `Failed: 2, Passed: 19`), and for the frontend `Cannot find module './badges'`/`'./greeting'` with `12 failed`, which covers G1, G2, B1–B4, L1 and L4. Both self-checks are quoted with `cmp`: `Take(20)` gives IA1 `Expected: 21 / Actual: 20`, and with no `refetchInterval` L4 can't find `5 low`.
- **Same predicate, no cap.** In `InventoryStore.cs`, the private `LowStock(threshold)` is the single `Where`, used by both `ListLowAsync` and `CountLowAsync`, and `CountLowAsync` has no `Take`. IA1 pins this: 21 low variants plus one at 4 gives a count of `baseline + 21` and a list of 20.
- **Boundary.** The contract is in `Ecommerce.Inventory.Contracts`, the port and store stay `internal`, and Admin reads only through `IInventoryReader`. There is no DbSet outside Inventory and no migration. ArchitectureTests 25/25.
- **Rules.** No `var`, no anonymous types, and every record is named. No new time use on the backend. The greeting is device-local by design (D10).
- **Role gating.** `navBadges` doesn't check the role. The badge only renders on a visible nav item, and Support has no Inventory item, so B5 pins that it's hidden. A Customer gets no request because `enabled` is false (L3). The server stays `AdminOrSupport`, and IA2 pins that Support gets the same count with `kpis` null.
- **Shell data.** `useShellDashboardQuery` is called in `app/admin/_layout.tsx`, and `AdminShell` only gets the `badges` prop. It uses `refetchInterval: SHELL_REFRESH_MS` (60 000), which L4 covers. It uses the same `queryKeys.admin.dashboard('today')` key as `useDashboardQuery`, so the dashboard's Refresh (`query.refetch()` on that key) refreshes the badge too. L2 shows there is one request.
- **Injectable `now`.** `DashboardView({ now = () => new Date() })`, and G3/G4 inject a local 09:00 and 20:00.
- **Tokens and phone width.** The new classes are only `border-warning text-warning rounded-control px-sm text-caption gap-md` (`warning` is a design token). There is no `style=`, hex or `[...]`. `admin-phone-width*` is green, and B4 covers 390 px with the menu open.
- **Existing coverage.** The 401/403, 400 and happy-path dashboard tests are unchanged and green.

### Ruling 1 — three extra `IInventoryStore` fakes — Accepted
**What I saw:** `InventoryStockReaderTests`, `ReservationExpiryServiceTests` and `ReservationExpirySweepTests` each gain one line, `CountLowAsync(...) => throw new NotSupportedException();`. No other line changed.
**Why it's fine:** they implement the port I extended, so they couldn't compile without it. My plan only listed the `IInventoryReader` fakes, so the gap was mine. The new member throws, so it can't mask a test, and its indentation matches the existing `ListLowAsync` line in each fake.

### Ruling 2 — the added `DashboardView` import in `DashboardView.test.tsx` — Accepted
**What I saw:** one import line at the top, plus the appended `describe`. No existing assertion or helper changed.
**Why it's fine:** G3/G4 need to render `DashboardView` directly to pass `now`. The route default export can't take the prop. An import can't weaken an existing test.

### Flakiness ruling — not caused by this slice, NOT blocking
**What I checked:**
1. **Same signatures before this slice.** The backlog already has `routes.test.tsx` "/products" timing out at 5 s under full-verify load (P2.34 R1). It also has `tests/app/admin.test.tsx` failing on its first cold `renderRouter('./app')` (P2.6 R1). Both are first-test-in-file cold renders, which is what you saw.
2. **Code path.** `/products` doesn't mount `app/admin/_layout.tsx`, so the new query and interval never run there. The only shared cost is loading two tiny modules (`badges.ts`, `hooks.ts`).
3. **Leaked timers.** `refetchInterval` is held by the query observer and cleared when it unmounts. `npx jest tests/routes.test.tsx tests/app/admin.test.tsx tests/admin-shell-data.test.tsx --detectOpenHandles` gave 6 suites and 76 tests passed, with no open handles reported.
4. **My full run** was green the first time, with jest at 26 s. Your passing run in `artifacts/verify/jest.log` was 35 s.

This is the third sighting, so I moved it up in the backlog with a concrete fix: warm up or scope a timeout in those two files, with no retries. The "worker failed to exit gracefully" line is in the backlog too. I couldn't get a baseline from before this slice without a git write, and `--detectOpenHandles` doesn't point at this slice.

### 1. L3 asserts before the query could fire — NOTE   (`tests/admin-shell-data.test.tsx` L3)
**What I saw:** you check `dashboardCalls(spy)).toHaveLength(0)` right after `renderRouter`, with no `settleFetch()` first.
**Why it matters:** if `enabled` were wrong and the fetch started a tick late, L3 would still pass. The server's 403 is the real guard, and B5 covers the hidden UI, so this isn't blocking.
**What I want:** add `await settleFetch()` before the assertion. It's in the backlog.
**Lesson:** a "nothing happened" assertion needs the async work settled first, or it proves nothing.

**What was good:** the single private `LowStock` predicate with a comment saying why, plus a self-check that proves IA1 catches the `Take(20)` regression. Also, keying badges by href kept `nav.ts` frozen and leaves P2.10 a one-line extension.

Part B (P2.12) can start.

## Part B — Rob → Marco
Verdict: BLOCKED · verify.sh: not run (BLOCKED round; your run: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`)

The backend is right: `CountCancelledAsync` reads Orders' own `OrderStatusChange` set (`AsNoTracking`, `ToStatus == Cancelled`, `from <= CreatedAt <= to`, distinct `OrderId`), reaches Admin only through `IOrderReader` in `*.Contracts`, takes its window from `period.From(clock.UtcNow)`, and needs no migration. No `var`, named records, all new types `internal`. The role matrix, the three card states and the tokens all match D8/D9. One bug, on phone width.

### 1. Attention links leave the phone menu open — BLOCKING   (`features/admin-shell/components/AdminShell.tsx:48–57`, `:148`)
**What I saw:** every nav link in `Sidebar` gets `onPress={onNavigate}`, which is how the phone overlay closes. `AttentionCard`/`AttentionRow` get no `onNavigate`, so their `Pressable` has no `onPress`. `AdminShell` has no pathname effect that closes the menu.
**Why it matters:** at 390 px an Admin opens Menu and taps "2 low-stock variants". The route changes to `/admin/inventory` underneath, but the `absolute inset-0` overlay stays open and covers it, so the link looks broken. The same happens with "N return requests waiting". The plan has the card show in the open phone menu, so this is a path users will take.
**What I want:** pass `onNavigate` from `Sidebar` to `AttentionCard` and on to `AttentionRow`, and set it as the link `Pressable`'s `onPress`, the same way nav items do. Change nothing else.
**How I'll check:** a new appended test **C4 (TF)**: Admin at 390, open Menu, press `2 low-stock variants` → `findByText('Inventory page')` and `queryByRole('button', { name: 'Close menu' })` is null. Quote its red before the fix. Then run `verify.sh changed` and the AdminShell suite.
**Lesson:** a new link in a shared container has to follow that container's navigation contract. Here that contract is to close the overlay on press.

### Rulings
1. **Third fake `FakeOrderReadStore` (`Orders.Tests/.../ReturnsAndRefundsTests.cs`) — Accepted.** D7 extends the internal port `IOrderReadStore` (`OrderPorts.cs` is in my Files table), so every implementer has to compile. Leaving it out of the fake list was my mistake in the plan. I checked the diff: one added member that throws `NotSupportedException`, in the file's own style, and no other lines changed.
2. **IB1/IB2 sign in again after the clock moves — Accepted, nothing weakened.** `AccessTokenAsync` does a real `LoginAsync` and asserts `200` (`IdentityTestEnv.cs:231–235`). Each role gets its own fresh token from the normal login path, and IB1 still asserts Support's `200`. Expiry is not what these tests check, and the endpoint's 401/403/400 tests are byte-identical. A token minted before a 16-minute jump would be expired, so signing in again is the correct setup.

### Checked and fine
- Test-first: IB1 red `KeyNotFoundException` (field missing). A1–A3 red as a missing module, C1–C3 red on their texts. IB2's first red was a 401 from token expiry, fixed in setup. Its real red is the self-check: dropping `>= from` gives `Expected: 0 Actual: 1`. That counts as test-first evidence for a Low-tier slice. I checked that `OrderStores.cs` now has `h.CreatedAt >= from` again, as you reported after the `cmp`.
- Isolation: `ProjectionEnv` → `CreateDatabaseAsync` per test, so IB2's absolute `0`/`1` are safe. A Paid order is present and not counted.
- Role matrix: `attentionItems` hides low stock and returns unless the role is `Admin`. A2 and L5 pin this for Support. The server still returns every count to both roles, so there is no new auth surface.
- States: `ready` + `[]` shows `All clear.`, `unavailable` shows its text, and loading or no prop shows no card (C2/C3). `_layout.tsx` keeps the last data when a poll fails (D9).
- Tokens: `text-warning/danger/foreground/muted`, `min-h-touch`, `AdminPanel`, all as literal class strings. No `style=`, hex or `[...]`. Your verify run shows the phone-width guards green.

**What was good:** `attentionItems` is pure and role-aware, with only the two links D8 allows. A1 asserts hrefs position by position, so a link added to the wrong item fails.

## Round 2 — Rob → Marco (Part B / P2.12)
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 220 suites, 1714 tests.

### 1. Attention links close the phone menu — Confirmed
`onNavigate` flows `AdminShell` (`close`, line 227) -> `Sidebar` (line 166) -> `AttentionCard` -> `AttentionRow`, where it is the `Pressable` `onPress` (line 53). C4 is appended in the attention describe; I saw your quoted red ("Found multiple elements with accessibility label: Close menu") matches the failure the old code would produce. C4 asserts navigation to "Inventory page" and no "Close menu".

### Order dependency — not blocking
I ran `jest -t C4` alone: it passes (ios and android projects). So C4 does not rely on its position or on earlier tests. The fragility is the other way round: C3 calls `first.unmount()` and then a second `renderRouter` in one test, which leaves the router rendering null for whatever test follows. That is a leak in C3 (a Part B test, not touched in this round), and C4's placement before it is a harmless workaround. No existing test, C3 included, was changed; the diff only adds tests and the `onNavigate` wiring. Logged as a NOTE in the backlog.
