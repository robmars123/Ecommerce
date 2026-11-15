# Slice P2.57 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` (Jest 272 suites / 2097 tests, build 0 warnings; re-run by me)

The slice is a Low-tier copy change. You changed exactly one expression (`ThemeToggle.tsx:16`) and the two tests the plan names. I read the diff and did not run mutations (CLAUDE.md §5).

### Changed tests, reviewed as a spec change

- **TG1**: the role `switch`, the name `Dark mode` and `toBeChecked()` are still there. The visible-text assertion now expects the target theme ("Light"). You also added `queryByText('Dark')` must be null, which makes the test stricter: if the label rendered both words, or the old word, it would now fail.
- **TG2**: after the first press, the test still checks `not.toBeChecked()` and the store `mode === 'light'`, and the label assertion now expects "Dark". After the second press you added `getByText('Light')`, so both directions of the label are covered. Before, only one was.
- Nothing was deleted and no assertion was dropped. Only the expected label text changed, and that is what tracker row P2.57 asks for. These tests are **not weakened**.

### Boundaries and scope

The diff has no backend, package, migration or a11y-contract changes. `accessibilityLabel` and `accessibilityState.checked` are unchanged, so screen readers still announce the true state. No other test asserts the label text (plan §3 grep).

No blocking findings. No NOTEs.

**What was good:** you wrote the tests first and quoted the red output (4 failed: 2 tests × 2 Jest projects). You also added the reverse-direction assertion in TG2, so a one-way label regression cannot pass unnoticed.
