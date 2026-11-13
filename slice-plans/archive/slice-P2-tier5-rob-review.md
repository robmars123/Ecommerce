# Slice P2-tier5 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 252 suites / 1935 tests

The code is clean and matches the plan: the tab lives in the URL, the static tabs mount no dashboard queries, `realtime.ts` keeps one socket with a correct stale-connection guard, and the changed tests do not loosen the role gate. One thing blocks: two acceptance criteria describe behaviour that did not ship. The plan made that the risk to check (§7, "Static copy drift"), and this copy is shown in the app as the definition of what the dashboard does.

### 1. Two criteria claim more than shipped — BLOCKING   (`features/admin-dashboard/stories.ts`, US-04 and US-05)
**What I saw:** US-04 says "Tapping a bucket opens the orders list filtered to it." In `OrdersByStatusPanel.tsx:84-106` only single-status buckets are links. The two mixed buckets ("In progress" = Paid/Processing/Shipped, "Cancelled or refunded") are plain text with one link per status under them. That is the tier-4 decision-log row: "single-status buckets link, multi-status ones list per-status links", and there is no multi-status filter. US-05 says "Export CSV downloads the figures of the chosen period." On Android and iOS the export opens the share sheet (`lib/platform`, RN `Share`, same decision row). It downloads only on web.
**Why it matters:** An Admin who reads US-04 taps "In progress" and nothing happens. A tester using the Acceptance Criteria tab as a checklist records a bug that is really wrong copy. S3 only checks *which rows* a story cites, not what the sentence claims, so no test can catch this. Reading against the code is the only check.
**What I want:** Reword both lines to match what shipped, e.g. US-04: "A single-status bucket opens the orders list for that status; a mixed bucket lists a link for each of its statuses." US-05: "Export CSV saves the chosen period's figures (a download on web, the share sheet on a phone)." Change nothing else. DT3 reads the criteria from the array, so it still passes without edits.
**How I'll check:** I'll read the two strings against `OrdersByStatusPanel.tsx` and the tier-4 decision row. Then `verify.sh changed` plus jest for `features/admin-dashboard`.
**Lesson:** Static copy that says "this is what we built" is a claim like any other. Check it against the code, not only against the tracker row's short title.

### Things I checked that are fine
- **Changed and deleted tests.** N1, B7, S8, S9 and S10 changed only because D8 removed `ready` and the "Soon" branch on purpose (decision-log P2.3 row). The role gate is not weaker. `visibleNav` still calls `adminRouteDecision` on the path part, and `/admin` is a Support route both before and after. S9 still asserts that Products, Categories, Inventory, Returns, Analytics, Settings and Insights are absent for Support. N2 is unchanged. Route guarding reads `usePathname()` (no query), so `?tab=` cannot get around it. Deleting S10 is justified because the branch it tested is gone. Keeping it would mean keeping dead code.
- **`realtime.test.ts` mock.** It only adds `onreconnecting`/`onclose` capture. The 6 existing `it` blocks are byte-identical.
- **`jest-setup.js`.** It adds `subscribeToConnectionState` to the global mock. Without it every shell render throws. It hides nothing, because the real function is tested in `realtime.test.ts`, where the global mock is replaced.
- **R5.** I read the test and the guard. `whenCurrent` compares `connection === built` around reconnected, reconnecting, close and both `start()` outcomes. R5 fires the first socket's `onclose` and `onreconnecting` after the second one is up and asserts the exact call list `[[false],[true]]`. That is the right shape, and Marco's quoted red (`Received 3: false`) proves it fails without the guard. `onreconnected` → `notify(null)` is now guarded as well. That changes nothing for order screens, because a stopped connection never reconnects.
- **S30/S31.** S30 covers 1024 and 390 and checks one subscription. S31 checks that a signed-out render shows no pill and makes no subscribe call. With `{user ? <LiveStorePill/> : null}` in both bars, the socket is released when the user logs out.
- **The other criteria.** US-01, 02, 03, 06, 07 and 08 each match their cited Done rows and app context §4.14. I checked the low stock "N left" text in `DashboardView.tsx:281`. The DoD bar paraphrases the tracker footer correctly.
- **Marco's concerns.** (1) Renaming the new tests S30/S31 to avoid the existing S13/S14: accepted. (2) The prettier fixes on new files: fine, and `format` passes. (3) A failed first `start()` leaves the pill offline: accepted risk, already in the backlog.

### 2. Narrow top bar width — NOTE
S30 at 390 checks only that the pill is present. "Menu", "Store Admin", "Live store · offline" and the theme toggle share one row. → backlog.

**What was good:** You made R5 fail first with a precise assertion (`toEqual([[false],[true]])`), not just "last call". The static tabs never mount `DashboardView`, so DT2 and DT6 can prove that no dashboard fetch happens. Only `DashboardView` changed (one prop), and its tests are untouched.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)

### 1. US-04 / US-05 strings — Confirmed
US-04 criterion 2 now says a single-status bucket links to the filtered list and the mixed buckets show one link per status. That matches `OrdersByStatusPanel.tsx:84-106`: `b.href` gives one bucket link, and otherwise each `b.statuses` entry gets its own `/admin/orders?status=` link. US-05 criterion 4 now says web downloads a CSV and Android/iOS open the share sheet, which is what shipped. The rest of `stories.ts` is unchanged apart from these two strings, and no test file was edited. The files are untracked, so I read them directly. Marco's report records no test edits, and the full suite is green.

No new blocking issues. No mutations (low tier).
