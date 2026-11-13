# Slice P2.48 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 254 suites / 1947 tests, expo-check "Dependencies are up to date".

Low tier, no mutations. The diff matches the plan: one local `CopyOrderNumber` in `OrderDetailView.tsx`, one new package, one global mock, one new test file, no existing test touched. Out-of-slice files (`CLAUDE.md`, `.claude/`, the P2-tier5 archive moves) not reviewed.

Checks I made:
- **Package:** `expo-clipboard` is the only new entry in `package.json` (`"58.0.4"`, exact, no `~`/`^`); the lock adds only `node_modules/expo-clipboard` 58.0.4 with peer deps on expo/react/react-native — no transitive packages. expo-check passed in my run. Decision-log row present.
- **Global mock** (`tests/jest-setup.js:21`): replaces only the native module with `setStringAsync: jest.fn(async () => true)`. The component's logic (state machine, labels, timer, try/catch) runs for real; the copy test resets the mock in `beforeEach` and drives true / false / reject per case, and asserts the call count and the exact argument `'ORD-2026-000001'`. Nothing under test is hidden. The default `true` only keeps other screens that render `OrderDetailView` loadable.
- **States:** resolves true → "Copied" / `Order number copied`; resolves false and rejects → "Copy failed" / `Couldn't copy order number`; each has its own test. The rejection is caught, so no unhandled promise.
- **Timer:** `useEffect` cleanup clears `timer.current` on unmount, and a new press clears the previous timer before scheduling. Reset after 2000 ms is tested with fake timers enabled after load and restored in `afterEach`.
- **Accessibility:** the button is a sibling of the `accessible` header View, not inside it, so the header label `Order ORD-…` is unchanged and the button is reachable by role; `min-h-touch` keeps the 44 pt target; tokens only.
- **Red quoted:** 10 failed / 2 passed (test 6 passes before the feature, as expected) — right reason.

### 1. Reset timer can be scheduled after unmount — NOTE   (`OrderDetailView.tsx:72-80`)
**What I saw:** you schedule the reset after `await setStringAsync(...)`. If the user leaves the screen while the copy is pending, the unmount cleanup runs first (no timer yet), then the timer is set and fires a `setState` on an unmounted component 2 s later.
**Why it is not blocking:** React 18+ treats that as a no-op, no warning, no leak beyond one 2 s timer; not observable. Logged to `backlog.md`.

### 2. No colour on Copied / Copy failed — NOTE
Plan allowed (did not require) success/danger tones. Logged to `backlog.md`.

**What was good:** the `COPY_LABELS: Record<CopyState, …>` table keeps visible text and screen-reader label in lockstep and makes a missing state a type error; putting the tests in a separate file left the existing 199-line detail test byte-for-byte unchanged.
