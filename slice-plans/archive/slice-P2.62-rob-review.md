# Slice P2.62 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not run in full (not a PASS round); `verify.sh hygiene` → `PASS hygiene 1s no matches` / `VERIFY PASS (hygiene)`

The C# side is right and small: one route, one policy, two singletons, no I/O, and the CORS rule is one local function with two call sites. All four mutations go red when I run them. One thing blocks: the `cs_var_hits` line in `verify.sh` is not the line the plan specified, and it is hard to read.

### 1. `printf` format string split over two lines, plus stray spaces — BLOCKING   (`scripts/verify.sh:29-30`)
**What I saw:** Your edit wrote the plan's `printf '%s\n'` as `printf '%s` + a real newline + `' "$f"; fi; done | xargs awk '`. There are also five stray spaces before the `| while`. The whole pipeline is one ~190-character line that wraps in the middle of a quoted string.
**Why it matters:** This is the quality gate, and Nathaniel's bar is "very easy to read". A reader sees what looks like an unclosed quote. The obvious "fix" is to join the two lines, which gives `printf '%s' "$f"`. That glues every path into one word, so awk can't open anything, and since S7 now fails closed, hygiene fails on every run. The behaviour is correct today only because of a hidden newline.
**What I want:** Keep the same logic, but write it as plan S6 spelled it, one pipeline stage per line:
```
  g ls-files --cached --others --exclude-standard -- 'backend/*.cs' | grep -vE '/(bin|obj)/' |
    while IFS= read -r f; do if [ -f "$f" ]; then printf '%s\n' "$f"; fi; done |
    xargs awk '
```
Change nothing else in `verify.sh`.
**How I'll check:** `sed -n 28,31p scripts/verify.sh | cat -A` shows `'%s\n'` on one line and no doubled spaces. Proof P re-run: the appended `var probe = 1;` with the controller deleted → FAIL listing `StripePaymentGatewayTests.cs:<n>`, then restore → `cmp` identical → hygiene PASS.
**Lesson:** If a string literal only works because of an invisible character, it will break at the next edit.

### 2. Paths with spaces / empty list — NOTE (backlog)
I checked both cases the coordinator asked about. With a space in a path, `xargs` splits it, awk exits 123, and hygiene now FAILs. That is a false failure, never a false pass, and the plan scoped it out. With an empty file list, `grep -v` exits 1, which gives "scan failed". This can't happen in this repo. I sent both cases to the backlog with the `\0` / `xargs -0` fix.

### Checks that passed (no action)
- **CORS parity:** `EndpointInventoryTests` is unedited and green, and it compares the whole table. Catalog's module routes (`categories`, `images`, `products`) don't match any prefix, so they stay `-`. Branch order and `OrdinalIgnoreCase` are unchanged. Health, hub and fallback stay outside the `""` group. The Admin module has no `RequireCors`.
- **Mutations (mine):** M1 → 3 red (inventory T1, T1 Support, AdminInsights settings). M2 → 4 red (+ II11). M3 → 2 red (inventory, T2). M4 → 2 red (inventory, T2). Each was restored from a scratch copy and `cmp`-identical.
- **Handler:** synchronous. It reads `DashboardSettings` (a singleton instance) and `IInventorySettingsReader` (a singleton that returns a config value). No DbContext, no N+1, no extra round-trip. `[FromServices]` is on both parameters. Types are `internal`. The only new public member is `MapAdminEndpoints`.
- **S7:** `local hits` sits on its own line, so the `||` sees the scan's real exit status. The hits are printed before FAIL. No other step changed.
- **Tests:** none edited or deleted. The only test change is the new `AdminSettingsContractTests.cs`.
- **Line endings:** No normalisation is needed. `git ls-files --eol` shows `i/lf w/lf` for `Program.cs`, `AdminModule.cs` and `verify.sh`, and `file` shows no CRLF. The repo is `* text=auto`, so the index stays LF either way. The warning is `core.autocrlf=true` noise that git prints for every LF working file.

**What was good:** T2 also pins the 401 carrying ACAO + ACAC. That is the real requirement for the web app's refresh flow, and a preflight-only test would have missed it. Making S7 fail closed fixes a real hole: before, hits were printed and the step still passed.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)

### 1. verify.sh printf/newline — Confirmed
`sed -n 26,34p | cat -A` shows one stage per line, `printf '%s\n' "$f"` guarded by `[ -f "$f" ]`. Proof P re-run by me: appended a `var` line to `StripePaymentGatewayTests.cs`; hygiene FAILED naming file:123 with "C# var found", no "cannot open" (the deleted `AdminSettingsController.cs` was in the list, so the deleted-file case is covered). Restored, `cmp` identical, hygiene PASS.

Full verify: build 0 warnings, tests green, jest 2111 passed. No open bugs. Tracker P2.62 set to Done.
