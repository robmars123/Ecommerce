# Slice P2.10 Part A — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 1734 passed (220 suites), drift clean.

The diff matches the plan row for row: no new endpoint, package or migration; the previous revenue and percent are computed on the server and travel inside `kpis`, so the existing Support guard covers them; the delivered count reuses the cancelled query through one private helper. I found no bug and no blocking issue.

### Checked
- **Boundaries:** Admin reaches Orders only through `IOrderReader` (Contracts); `CountEnteredAsync` is private in `OrderReadStore`; no new public type; ArchitectureTests green.
- **Money (rule 1):** `KpiCalculator.ChangePercent` (`DashboardKpis.cs:36`) rounds 1 dp away from zero, divides by `|previous|`, returns `null` at 0. The client only formats `toFixed(1)` and picks the tone from the server number's sign (`kpi-cards.ts:10`). Previous revenue reuses `ReadKpisAsync` (D2), and I4 proves it nets an actor refund.
- **Support guard (rule 11):** `DashboardQueryService.cs:71` reads both windows only when `includeKpis`; the controller maps `Comparison` only inside the `Kpis is null ? null` branch. I2 checks the raw body for the field names and the amount.
- **Window (D1):** `PreviousTo = now − Days`; U4 asserts all three periods and `PreviousTo < From`. I3 pins the clock to mid-day and puts B two minutes after A, so a full-day previous window would include B.
- **Thin controller, `IClock`:** mapping only; the tests move `e.Env.Clock` and sign in again after each move.
- **Nested link (D10/D11):** the badge is a sibling `Link` in a row `View`, not inside the nav `Link`; the Inventory inline path and its `"Inventory, 3 low"` label are unchanged.

### Changed existing tests — none weakened
- `kpi-cards.test.ts` K1–K5: K1's expected objects gain `change`/`detail`, which is stricter; K2's literal gains the two required fields; K3–K5 only add the `outcomes` argument. No assertion removed.
- `badges.test.ts` B1–B3: value `'3 low'` → `{ text: '3 low' }`, plus `ordersByStatus: []` so the fixture's default `Paid: 4` doesn't add a second key. `toEqual` on the whole map is still exact.
- `AdminShell.test.tsx` B4–B6: only the badge fixture's shape and the helper's parameter type change; the assertions are the same.
- `DashboardView.test.tsx`: the first test and the Support test gain assertions; the zero-period literal gains the two fields.
- Three `IOrderReader`/port fakes gain one throwing member; this only keeps them compiling.
- `Dashboard_without_a_token_is_401_and_as_a_Customer_is_403` and `Dashboard_with_an_unknown_period_is_400` are untouched and green.

### Mutations (my runs; each file copied to the scratchpad, restored, `cmp` OK)
- **M1** `PreviousTo` → `From(now)`: U4 red ×3 (`Expected: 2026-11-11T14:05:00Z  Actual: 2026-11-12T00:00:00Z`, and the same for 7d and 30d).
- **M2** `/ Math.Abs(previous)` → `/ previous`: U3 red (`Expected: 50.0  Actual: -50.0`). I didn't re-run the zero-guard half; Marco quotes `DivideByZeroException` for U2.
- **M3** `User.IsInRole(IdentityRoles.Admin)` → `true`: I2 red (`Expected: Null  Actual: Object`).

### 1. Badge accessibility label hard-codes "orders" — NOTE   (`AdminShell.tsx`, badge `Link`)
**What I saw:** the label for any badge with an `href` is built as `` `${badge.text} orders` ``. Today only Orders has one, so it reads "4 paid orders".
**Why it matters:** if a future row gives Inventory or Returns a linked badge, a screen reader will announce "3 low orders".
**What I want:** when a second linked badge appears, add a `label` to `NavBadge`, or build the label from `item.label`. Not now: there is no second consumer yet. Logged in the backlog.
**Lesson:** generic renderers shouldn't carry one caller's words.

### 2. `view.Comparison!` relies on a service invariant — NOTE   (`AdminDashboardController.cs:79`)
**What I saw:** the controller uses `!` because the service sets `Comparison` whenever it sets `Kpis`.
**Why it matters:** a later edit that sets `Kpis` without `Comparison` would compile and then throw a NullReferenceException on every Admin dashboard request. Today I1–I4 would catch that.
**What I want:** nothing this slice. If the view grows again, nest the comparison inside one `AdminKpis` record so the two can't diverge. Logged in the backlog.
**Lesson:** make illegal states unrepresentable instead of asserting them with `!`.

**What was good:** `PinMidDay` plus a two-minute gap in I3 makes the window boundary test deterministic and sensitive to the exact mutation the plan named. The sibling-link layout keeps the Inventory path byte-identical in behaviour, so B4–B6 needed only a type change.
