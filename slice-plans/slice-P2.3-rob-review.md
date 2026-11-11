# Slice P2.3 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run by me (BLOCKED round). Your Round 1 line: `VERIFY PASS`, jest 1291 passed.

The nav work is clean and matches the plan. `nav.ts` follows D1/D2 and does not touch `admin-access.ts`, `AdminGate` or the backend, so the tier stays Low and I ran no mutations. `AdminShell.test.tsx` is +92/−0 (numstat), so S1–S7 gained setup lines only and no assertion changed. The single blocker is in the environment fix: `expo install --fix` replaced exact pins with `~` ranges, and the project requires exact pins.

### 1. Exact pins became `~` ranges — BLOCKING   (`frontend/apps/store/package.json:23-30`)
**What I saw:** `expo`, `expo-constants`, `expo-crypto`, `expo-linking`, `expo-router`, `expo-secure-store` and `expo-status-bar` went from exact versions (`"58.0.23"`) to tilde ranges (`"~58.0.24"`). These seven are now the only ranges among the dependencies. Every other entry is exact.
**Why it matters:** arch §5.5 (`05-frontend.md`, adoption step 1) says to "pin exact versions as for every other package". Nathaniel approved a patch *bump*, not a change in how versions are pinned. With `~`, any later `npm install` (adding a package, a fresh clone without `npm ci`) can float to 58.0.25+ and rewrite the lock. That is an SDK change nobody planned or reviewed, and `package.json` would no longer name the version that was tested.
**What I want:** set the seven entries to the exact versions now in the lock, with no `~`: `58.0.24`, `58.0.16`, `58.0.8`, `58.0.17`, `58.0.31`, `58.0.3`, `58.0.5`. Then run `npm install` in `frontend/apps/store` so the lock's root `packages[""]` specs match. No resolved version may change. Leave `app.json` as it is.
**How I'll check:** a lock-vs-HEAD comparison shows the same version set as this round (one removal, `getenv`; the bumps listed below); `grep -n '"~\|"\^' package.json` matches nothing outside the jest `moduleNameMapper` keys; `verify.sh changed` plus the `expo-check` step pass (`npx expo install --check` accepted exact pins before this round).
**Lesson:** a tool's side effect does not change the project's conventions. Undo the part the approval didn't cover.

### Rulings on the three environment items you flagged

**(a) Transitive minors: acceptable.** I compared the lock against HEAD myself. There are more minors than your report lists: `@expo/env` 2.5.4→2.6.0, `expo-modules-macros` plus its 4 platform packages 0.29.0→0.33.0, `expo-modules-autolinking` 58.0.17→58.1.0, `expo-modules-core` 58.0.30→58.1.0, `expo-modules-cli` 0.4.0→0.5.0, `expo-server` 58.0.6→58.1.0. No `@parcel/watcher` package changed. Each one is required by the bumped Expo packages' own ranges (`expo-modules-core → expo-modules-macros 0.33.0` exact, `expo → expo-modules-cli ~0.5.0`, `expo-router → expo-server ^58.1.0`, `@expo/cli → @expo/env ~2.6.0`). All resolve from registry.npmjs.org. No majors, no new top-level packages, no added entries. These are SDK-managed effects of the approved patch.

**(b) Removals: acceptable.** The lock lost **one** entry, not seven: `getenv` 2.0.0. Nothing depends on it after `@expo/env` 2.6.0. The "removed 7 packages" in npm's output counts `node_modules` changes, not lock entries.

**(c) app.json: acceptable.** `expo-status-bar` 58.0.5 ships `app.plugin.js` (`withStatusBar`), and `expo install` registers config plugins for packages that have one. The stripe entry is only reformatted. It only affects native prebuild, so I've put an Expo Go/EAS check in the backlog.

### 2. Report accuracy — NOTE
Your Round 1 counts were wrong: 7 removals (actually 1), 4 minors (actually 9 across 5 families), "@parcel/watcher". In a dependency change, the report is what Nathaniel relies on. Next time, produce the counts with a lock diff, not from npm's console summary.

**What was good:** `visibleNav` derives visibility from `adminRouteDecision`, so the nav and the gate cannot disagree (D2). Running S1–S7 against the old shell before you changed it proved the setup really was setup-only.

## Round 1 re-review — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)

### 1. `~` ranges in package.json — Confirmed
I read the diff. All 7 Expo packages are now exact (expo 58.0.24, constants 58.0.16, crypto 58.0.8, linking 58.0.17, router 58.0.31, secure-store 58.0.3, status-bar 58.0.5). The lock root specs match. The resolved-version churn in the lock diff (e.g. @expo/config 58.0.12) is the patch bump the pins imply, so it is expected.
Full run: build 0 warnings, jest 1291 passed (181 suites), drift clean, expo-check up to date.

No open bugs.
