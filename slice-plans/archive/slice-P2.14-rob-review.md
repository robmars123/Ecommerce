# Slice P2.14 (+ P2.15, P2.16) — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 212 suites / 1648 passed

Low tier, so I read the whole diff and the new files, with no mutations. The change matches D1–D8: the period lives in the URL, a failed refresh is no longer silent, and the as-of line has four parts, all taken from the response. I found no blocking issues and no bugs.

### What I checked
- **Scope:** the diff touches only `features/admin-dashboard/` (two changed files and four new ones) plus my decision-log row. `backend/`, `lib/`, tokens, packages and `tests/**` are unchanged.
- **Frozen tests:** the `DashboardView.test.tsx` diff has 0 `-` lines (I counted with `grep -c '^-[^-]'`). The one hunk starts after the closing `});` of the existing `describe` at line 322. The five frozen tests are byte-identical and green.
- **D1/D2 (period in the URL):** the period is read only as `parseDashboardPeriod(params.period)`. The `find` does a strict `===` against `PERIODS`, so `7D`, `''`, `1y` and an array all fall to `'today'`. Only the parsed value reaches `useDashboardQuery`. Choosing Today writes `undefined`, so `/admin` has no `?period=` (U2 checks `toHaveSearchParams({})`). No `useState<DashboardPeriod>` is left; the `useState` import stays because `RecentOrders` (line 93) uses it. U3 shows that `period=1y` is never sent and the first call is `period=today`.
- **D4/D5 (refresh failure):** the notice sits inside the `data ?` branch and is gated on `query.isError`, so it appears only when `data && isError`. The `ErrorState` branch (line 237) is unchanged. R1 shows the old figures stay, the as-of time stays, and no `Try again` appears. R2 shows the notice clears on the next success. R3 shows that a failed switch to 30 days gives the error screen with `$1,234.50` gone, so the figures never disagree with the chip. Retries go through `shouldRetry` in `lib/query-client.ts`, so the notice is not delayed by blind retries on a 4xx.
- **D6/D7 (as-of line):** every part comes from `data.*`. The only `new Date` is inside `formatAsOf`, whose body is byte-identical to the one you removed. Lag is `Math.max` over the server's integer `lagSeconds`; that is not money math, and there is no `Date.now()`. The row has no `alert` role, so the frozen alert count holds. A2 pins max over first or sum, and your self-check (first entry → red A2, `75 s`) proves the test can fail.
- **Styling:** only token classes: `flex-row flex-wrap gap-sm`, `text-muted`, `text-warning` and `text-danger` (both defined in `lib/design-tokens.js` for light and dark). There is no `style=`, hex or `[...]`. The phone-width guards pass in the jest step.
- **No duplicates:** `PERIODS` and `formatAsOf` each exist once (in `period.ts` and `as-of.ts`).
- **Self-checks:** both are quoted as red, restored from a scratch copy, and reported `cmp`-identical. The current file contents match the plan (`query.isError`, max lag), which is consistent with that.

### 1. `AsOfLine` component in the view — NOTE (accepted)   (`components/DashboardView.tsx:34`)
**What I saw:** you put the four `Text`s in a small `AsOfLine({ data })` component instead of inlining them, as the plan described.
**Why it is fine:** it has one consumer, it narrows `data` to non-null for TypeScript, and it matches the file's existing `Section`, `KpiCards` and `QuickActions` helpers. It adds no behaviour and no export. Accepted.
**Lesson:** a deviation is fine when it has a reason, is reported, and changes no contract. You did all three.

### 2. Test-first order collapsed into one red run — NOTE   (`slice-P2.14-marco-report.md`)
**What I saw:** you wrote the tests for all three rows first and quoted one red run (`14 failed, 66 passed`, plus the two missing modules). The plan asked for one red-then-green cycle per row.
**Why it matters (lightly):** the red quote still covers every TF test (P1, P2, A1, A2, U1, U2, R1, V1), so the invariant evidence holds. What you lose is per-row isolation: a code change made for one row could have turned another row's test green unnoticed. Low tier, so not blocking. Logged to the backlog.

**What was good:** R3 pins the behaviour that has no code behind it (D5). It is the test that would catch someone later "improving" the error state by caching the last dashboard across periods. `asOfParts` takes the label from `PERIODS` and the period from `d.period`, so the line names the period the figures are for, and V3 proves it.
