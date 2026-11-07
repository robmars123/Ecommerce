# Token efficiency — Rob → Nathaniel

Process task. I measured with `wc -c` and two scratchpad scripts over the last 14 rob/marco transcripts (tool_use ids deduped). I edited only CLAUDE.md; diffs for the protected files are below.

## 1. Evidence

**Where tokens go:** every tool result is re-sent on each later turn (cache reads still count), so a byte loaded at turn 5 of 50 is paid ~45×. Peak context drives cost.

| Run | Calls | Output tok | Peak ctx |
|---|---|---|---|
| Marco builds Slice 1b | 143 | 193k | 349k |
| Marco resumes Slice 1b | 196 | 180k | 321k |
| Rob compacts 1b plan to 20 KB | 58 | 171k | 254k |
| Rob plans Slice 1b | 36 | 125k | 265k |
| Rob closes v1 doc gaps (10 resumes) | 123 | 119k | 233k |
| Rob plans nativewind-shop (the "107k" run) | 32 | 43k | 104k |
| Marco builds nativewind-shop (in progress) | 52 | 39k | 112k |

**Tool-result bytes by category, 14 runs:**

| Category | KB | Calls | Note |
|---|---|---|---|
| `cat`/`sed` of code via Bash | 803 | 154 | multi-file `cat` loops: 23.6 KB, 23.0 KB, 19.0 KB, 11.6 KB single results |
| Image reads | 649 | 2 | base64 screenshots; only ~1.5–3k tokens each |
| Read of slice-plans | 465 | 16 | old plans were 39–60 KB; `slice-1a-plan` read 3×, `slice-1b-plan` 6× across rounds and resumes |
| `cat` of docs via Bash | 295 | 64 | whole-file `cat 08-workflows.md 11-testing.md 12-…` = 19 KB in one call |
| Read of docs / CLAUDE.md | 169 | 19 | Rob Read CLAUDE.md (9.6 KB with line numbers) even though it is auto-loaded |
| test/build output | 125 | 105 | untailed dotnet/jest output |
| verify.sh | 77 | 71 | already compact (about 1 KB per run); 5 runs per agent on average |

**Most-loaded docs (14 runs):** index `ecommerce-architecture.md` 31×, `AGENT_STATUS.md` 28×, decision log 25× (mostly grep, which is fine), `08-workflows.md` 23×, `quality-bar.md` 12×.

**File sizes (bytes):** CLAUDE.md 7313 → **7456** · rob.md 6049 · marco.md 3877 · AGENT_STATUS 837 · index 4728 · quality-bar 10274 · app-context **10967** · decision log **23092** (50 rows, a single `###`) · 11-testing **10582** · 05-frontend **10273** · other arch files 0.4–9.8 KB · nativewind-shop-plan 20426.

**Instructions that force big reads**
1. rob.md says "read the changed files". In practice that means a whole-file `cat`, not the hunks.
2. marco.md step 2 reads the plan and then the index to map § to file. The index (4.7 KB) gets loaded every run because `Read:` lines cite § numbers, not files and lines.
3. Nothing bans multi-file `cat` or limits command output. That is the largest sink, at 803 KB.
4. The plan cap was applied after the fact. The "Compact 1b plan" run cost 171k output tokens just to rewrite the plan.
5. Fix rounds are resumed, and a resumed agent keeps its whole context: Marco 1b resume reached 321k, and Rob doc gaps did 10 resumes.
6. The Never lists in rob.md and marco.md repeat CLAUDE.md, which every subagent already loads.

## 2. What I changed in CLAUDE.md (7313 → 7456 B, +143)

The rules text lost about 190 B of wording, and the token-budget section gained new rules (+445 B). The net cost is about 35 tokens per agent. Every guardrail, hard rule, Never item, team-flow step and report format is unchanged in meaning.
- The 5-row task table is now one "Read by task" line, with the same entries.
- **Once:** each file or section is read once, the plan included. Never Read CLAUDE.md or your own agent file, since both are already loaded. Resumed rounds work from context plus the items handed over.
- **Ranges:** `grep -n` first, then `offset`/`limit` or `sed -n`. No `cat` of several files or of a file over ~150 lines, and no printing a file you just edited.
- **Cap output:** `verify.sh 2>&1 | tail -n 30` and quote the `VERIFY` line. Build, test, jest and tsc output goes through `| tail -n 20`. Grep uses `head_limit`.
- Decision log: "grep only, never whole" (this replaces the separate banned item).
- Plan cap: "written to size once" (no compaction pass).
- Wording trims in TDD, Never, Learning mode and Environment. The agent-file paths in "Team" were dropped because `/agents` resolves them.

## 3. Proposed diffs for protected files (please apply)

### `.claude/agents/rob.md`

L14 before:
`Follow the context budget in \`CLAUDE.md\`. Use the task table in \`docs/ecommerce-architecture.md\` (the index) to choose files under \`docs/architecture/\`, and read only those. Always read §2, §3.4 and the slice's §12.3 row, plus quality-bar §1, §3 and §4. Grep the decision log (§13.2); never read it whole. Never re-read a section already in your context.`
after:
`Follow the token budget in \`CLAUDE.md\`. Load the index tables with \`grep -n "^| " docs/ecommerce-architecture.md\`, then read only the chosen sections by line range. Always read §2, §3.4, the slice's §12.3 row, and quality-bar §1, §3, §4 (\`grep -n "^## " docs/quality-bar.md\`, then \`offset\`/\`limit\`).`
(The decision-log and re-read rules are now in CLAUDE.md.)

L16 before: `…then read the changed files and the tests that cover them, and nothing else…`
after: `…then read the changed hunks (\`git diff -- <file>\`) and the tests that cover them; open a whole file only when a finding needs it…`

L20 before: `Write \`slice-plans/slice-N-plan.md\`, **≤ 20 KB**, so Marco can build without asking:`
after: `Write \`slice-plans/slice-N-plan.md\`, **≤ 20 KB** (check \`wc -c\` before handing back; write it once), so Marco can build without asking:`

L22 before: ``1. `Read:` — the exact sections Marco needs (his whole reading list).``
after: ``1. `Read:` — the exact sections Marco needs as `file:start-end` line ranges (his whole reading list; he won't open the index).``

L39 before: `Run \`bash scripts/verify.sh\` yourself and quote its summary lines.`
after: `Run \`bash scripts/verify.sh 2>&1 | tail -n 30\` yourself and quote the \`VERIFY\` line and step summaries.`

L45 before: `- **Re-review after a fix round:** check only the blocking items and the new diff; re-run \`verify.sh\`.`
after: `- **Re-review after a fix round:** check only the blocking items and the new diff (\`git diff -- <files named in the reply>\`); don't re-read the plan or docs; re-run \`verify.sh\`.`

L79 (Never) before: `Write implementation code in \`backend/\` or \`frontend/\` · any git write command · change the stack lock or waive a hard rule · invent keys, domains, or providers · deploy or touch secrets · kill processes you didn't start · pass a slice to be helpful.`
after: `Everything in CLAUDE.md's Never list, plus: write implementation code in \`backend/\` or \`frontend/\` · change the stack lock or waive a hard rule · pass a slice to be helpful.`

### `.claude/agents/marco.md`

L15 before: `2. Read the plan, then **only** the sections on its \`Read:\` line. The files are under \`docs/architecture/\`; the index \`docs/ecommerce-architecture.md\` maps § to file. Also read quality-bar §1, §2 and §4. No whole-doc reads, no folder globs.`
after: `2. Read the plan **once** (never again this session, fix rounds included), then only the \`file:lines\` ranges on its \`Read:\` line, plus quality-bar §1, §2, §4 (\`grep -n "^## " docs/quality-bar.md\`, then \`offset\`/\`limit\`). Open the index only if a citation has no lines. No whole-doc reads, no folder globs.`

L32 before: `- **Inner loop (cheap):** search only inside the module or feature you're changing. Run the one test project…`
after: `- **Inner loop (cheap):** search only inside the module or feature you're changing; read code with \`grep -n\` + ranges, never a multi-file \`cat\`. Run the one test project (\`| tail -n 20\`)…`

L33 before: `- **Finish:** run the full \`bash scripts/verify.sh\` once (Release by default; never kill a process to free a lock). Quote its summary lines, then complete quality-bar §2.`
after: `- **Finish:** run the full \`bash scripts/verify.sh 2>&1 | tail -n 30\` once (Release by default; never kill a process to free a lock). Quote the \`VERIFY\` line and step summaries, then complete quality-bar §2.`

L37 (Never) before: `Any git write command (read-only status/diff/log is fine) · deploy · put secrets … · kill processes you didn't start · report a bug as fixed without a passing test.`
after: `Everything in CLAUDE.md's Never list, plus: add a package outside the plan · report a bug as fixed without a passing test.`

### `docs/quality-bar.md`
No change proposed. Reading it by `## ` offsets (diffs above) is enough.

## 4. Proposals not applied (your call)

1. **Split the decision log** (23 KB, one heading, 50 rows). Option A: add a `### 13.2.x` per area (catalog, auth, frontend, process) so `grep -n "^### "` plus `offset` works. Option B: move rows for closed slices 0–1b to `13.2-decision-log-archive.md`.
2. **Bring files back under the ~10 KB cap:** app-context 11.0 KB, 11-testing 10.6 KB, 05-frontend 10.3 KB (split at a `###`).
3. **Index:** add a line-range column to the Files table so plans can cite `file:lines` without opening each file.
4. **Fix rounds (team-flow change, so I did not put it in CLAUDE.md):** when a resumed agent's peak context passes ~150k, start a fresh agent with only the blocking items and `git diff --stat` instead of resuming.
5. **Output tokens:** Rob planning used 43k–171k output tokens, which is mostly reasoning. Writing the plan once at size removes the compaction rerun (171k in 1b).

## 5. Expected savings (estimates)

- No multi-file `cat` of code or docs: about 15–25k tokens off the peak context of a build run (the 4 largest results alone were about 77 KB), which compounds over roughly 25 later turns.
- Plan read once, no re-reads on resume: 5k tokens saved per avoided re-read (20 KB plan), 1–5 per slice.
- `file:lines` instead of index plus whole sections: about 2–4k tokens per agent run.
- No compaction pass: up to about 170k output tokens per slice in the worst case seen.
- Together I expect peak context per run to drop by roughly 20–30% (a Marco build at about 110k → about 80k) and total billed tokens per slice by a similar or larger share. Fresh agents for later fix rounds (proposal 4) would cut the 300k+ resumed runs the most.
