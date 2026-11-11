# Slice P2.4 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 warnings, jest 181 suites / 1305 tests.

Small, on-plan diff: one internal `SidebarFooter`, `Sidebar` takes `user`, both branches pass it, and S13–S18 were written test-first. You didn't touch auth, session, `admin-access.ts`, `AdminGate` or the backend, so the slice stays Low tier and I ran no mutations.

### Checks I made myself
- **S1–S12 untouched:** the test diff changes only the import line (adding `within` and `makeTokens`) and appends `describe('AdminShell footer')` after the old last line. No existing `it` changed. Confirmed.
- **S16 after your matcher fix:** `it.each([['blank', '   '], ['empty', '']])` still covers both cases. Each case checks the label `Signed in as <email>, Admin` and the email `Text`, and checks that `queryAllByText(/^\s*$/)` returns nothing. Without the `trim() || email` fallback, both label queries fail, so the test still guards the fallback. Your fix was to the test only, and the production expression matches the plan contract. Confirmed.
- **S7 / width:** the root gets `h-full`, not `flex-1`. S15 also checks that `flex-1` is not among the panel's classes. Good.
- **Minimal code:** no new files, hooks, packages or label maps, and no unplanned UI.

### 1. Footer pinning not checked in a browser — NOTE
**What I saw:** you only ran the export step, so nobody has seen whether `h-full` on the Sidebar root pins the footer to the bottom of the wide column and the overlay.
**Why it's acceptable here:** this is a Low-tier, display-only slice. Jest can't measure layout, and if the pin fails the result is only cosmetic: the footer sits directly under the nav, as plan risk 1 already allows. It can't hide the role or break S7.
**What I want:** Nathaniel checks it in a browser. I've added this to `backlog.md` (P2.4 R1).
**Lesson:** when a layout claim can't be tested in Jest, write it down as an open manual check. Don't let it pass silently.

**What was good:** you reported the S16 failure as your own test bug and fixed the test without loosening the production code. S18 went green before the implementation, and your report says so rather than hiding it.
