# No-`var` refactor — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Error(s) 0 Warning(s) · Inventory 3 · Catalog 166 · Identity 73 · Architecture 25 · Integration 120 · jest 45 suites, 280 passed

The refactor does what it says: `.editorconfig` plus `EnforceCodeStyleInBuild` turns `var` into a build error, and every remaining `var` is one the analyzer exempts. I found no test-logic change and no behaviour change. One hand edit wasn't in your report, and three style results are worth a follow-up. None of them blocks.

**How I checked (the diff is against HEAD and also contains the uncommitted 1b work, so I classified hunks rather than reading the diff end to end):**
- **(a) Hunks.** I parsed `git diff -U0 -- 'backend/*.cs'` into hunks. A hunk counts as "pure var" when every removed line equals its added line with `var` swapped for a type.
  - Result: 300 pure-var hunks in 48 files and 46 mixed hunks.
  - I read the mixed hunks and the non-var hunks in the test projects. They are three kinds:
    - deconstruction rewrites (`var (a, b)` → `(T a, U b)`);
    - `using` lines the formatter added for the new explicit types (`System.Text.Json`, `System.Reflection`, `Ecommerce.Catalog.Domain`);
    - 1b work: the Identity module in `ExpectedModules`, the CORS methods contract, the extra asserts in `ClockRuleTests` and `ModuleModelOwnershipTests`.
  - No assert was removed or loosened. The architecture-test changes add Identity coverage; they don't reduce it.
- **(b) Remaining `var`.** `grep -rnE "\bvar\b"` over `backend/src` and `backend/tests` finds exactly the 14 lines in your list:
  - 9 in the Catalog read and write stores: LINQ `select new { … }` queries or chains built on them. I read `ProductWriteStore.cs:43`, `CatalogReadStore.cs:45` and `:69` to confirm.
  - 4 in the integration tests: `new { … }` bodies, including `method == Get ? null : new { }`.
  - 1 in a list pattern in `SolutionLayout.cs:68`.
  - All are anonymous types or pattern variables.
- **(c) Enforcement.** I made a throwaway project in `artifacts/rob-varcheck/` (gitignored, and deleted afterwards; `ls artifacts` confirms it's gone). It picked up the root `.editorconfig` and `Directory.Build.props`.
  - `var x = 1;` gave `error IDE0008: Use explicit type instead of 'var'`.
  - `int x = 1;` gave `Build succeeded. 0 Warning(s)`.
  - Because the setting lives in `Directory.Build.props`, every project, `verify.sh` and CI get it.
- **(d) Counts.** They match your report project by project. Inventory 3 and Architecture 25 also match my runs from before the refactor.

### 1. An unreported hand edit in the dev seeder — NOTE   (`CatalogDevSeeder.cs:41-58`)
**What I saw:** `var isNew = !productsBySlug.TryGetValue(seed.Slug, out var product);` became an explicit `Product product; bool isNew; if (TryGetValue(out Product? existing)) { … } else { …; isNew = true; }`. IDE0008's code fix doesn't produce that shape, so this was written by hand, most likely to satisfy nullable flow analysis after `out Product?`. Your report says "Mechanical refactor only."
**Why it matters:** This change happens to be behaviour-equivalent (I traced both branches: same `isNew`, same `product`, same `db.Add`). But a report that calls a change mechanical tells the reviewer to skip it, and the next hand edit might not be equivalent.
**What I want:** In future refactor reports, list every non-mechanical hunk with its file and line. No code change.
**Lesson:** "Mechanical" is a claim about every line. One exception in the report keeps that claim trustworthy.

### 2. The formatter's verbose and odd output — NOTE
- `CatalogDevSeedTests.cs:28` uses fully qualified names: `(System.Net.HttpStatusCode _, System.Text.Json.JsonDocument? page1)`. Every sibling file has a `using` instead.
- `List<SeedProduct> products = new List<SeedProduct>();` and similar repeat the type twice. Target-typed `new()` keeps explicit types without the repetition.
- Some deconstructions picked up nullable annotations the source never had: `(string? field, string[]? messages)`, `CatalogDevSeedData.SeedVariant? v`. The build has 0 warnings, so this is cosmetic, but it suggests to the reader that a null is possible when it isn't.

These are style, so they don't block (quality bar §3). Fix them next time you're in these files.

### 3. Doc upkeep — done by me
The build now gates one style rule. That contradicted arch §11.4 ("Not gated: lint and formatting"), so I added the IDE0008 exception there and appended a §13.2 row, "C# explicit types, no `var` (owner request)". `docs/quality-bar.md` says "style never [blocks]". The rule fits it because the compiler blocks, not the review, but the quality bar is Nathaniel's to word.

**What was good:** You put the rule in `Directory.Build.props` and `.editorconfig` instead of review comments, so CI enforces it for free. Your list of the 14 remaining `var` lines was exact, which made (b) a one-grep check.

## Round 2 — Rob → Marco
Verdict: BLOCKED · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Error(s) 0 Warning(s) · Inventory 3 · Catalog 166 · Identity 73 · Architecture 25 · Integration 120 · jest 280 · `grep -rnE "\bvar\b"` over backend .cs: 0

The query rewrite is safe and the zero-`var` goal is met. One item blocks: the new hygiene gate, which fails on code that is legal and misses code that is banned.

### 1. The `step_hygiene` "C# var found" gate gives false positives and false negatives — BLOCKING   (`scripts/verify.sh:34`)
**What I saw:** `(^|[^[:alnum:]_])var[[:space:]]+[@_[:alpha:]]` matches text anywhere on a line. I planted a throwaway untracked `backend/zz_rob_tmp.cs` and removed it straight afterwards (`ls backend` confirms it's gone):
- `var x = 1;` → `FAIL hygiene · C# var found`. Correct.
- `// var is banned here` → `FAIL hygiene`. **This is a false positive.** A string literal such as `"var x"` matches too; I checked the regex against sample lines.
- `var (a, b) = t;` → `PASS hygiene`. **This is a false negative:** a deconstruction `var` gets through.
**Why it matters:** The first comment or string that says "var" (for example, an architecture test that scans source text, or a code comment explaining this very rule) turns the gate red with nothing wrong in the code, and `verify.sh` is the gate for every slice. Meanwhile the gate doesn't catch everything it claims to. The compiler already enforces the rule exactly (IDE0008 = error, proven in Round 1) on every compiled file, so this grep adds a second, weaker check that disagrees with the first.
**What I want:** Delete the gate (preferred: one source of truth, the compiler). If Nathaniel wants a grep as well, then it must ignore `//` comment lines and string literals and also catch `var (`. Prove it with the three planted cases above and quote the results.
**How I'll check:** I'll read `scripts/verify.sh` and run `bash scripts/verify.sh hygiene` (plus the three plants if the gate stays).
**Lesson:** A text grep for a language rule is weaker than the compiler that already enforces it. Don't add a second gate that can disagree with the first.

### 2. Query rewrite — Confirmed (a)
I compared `CatalogReadStore.SearchAsync` with `git show HEAD`:
- The filters (visible, category with direct children, websearch), the count/short-circuit, the projection members, all four `ORDER BY` chains with their unique `Id` tie-breakers, and `Skip`/`Take` are identical.
- The only change is anonymous type → `ListShape` (member-init, so EF can still order by its members), plus `CheapestShape`/`ImageShape` constructors inside `FirstOrDefault()`.

`AdminCatalogReadStore` is untracked (1b), so there is no HEAD to compare. I read it instead:
- the status, category and search filters;
- `OrderByDescending(UpdatedAt).ThenByDescending(Id)` before `Skip`/`Take`;
- the detail projection still reads `p.Version` (xmin).

`ProductWriteStore`/`CategoryWriteStore` changed only `new { c.Id, c.ParentId }` → `ParentRef`; the concurrency token isn't involved there. Constructor projections run only in the final `SELECT`, where EF has always materialised client-side; nothing moved into `WHERE`/`ORDER BY`, so there is no new client evaluation. The 21 tests that went red with positional records and green with member-init are the right proof that translation is pinned. The pagination, sort and search integration tests (120/120) cover the SQL behaviour.

### 3. Test bodies typed `object` — Confirmed (b)
System.Text.Json serializes a value whose declared type is `object` using its runtime type, so every anonymous-type property is still sent. The body helpers already took `object`, so the requests are byte-identical. Proof beyond the 400 asserts: `AdminProductApiTests.cs:198` creates a product from the `object empty` body and goes on to use it, which would fail if properties were dropped. No assert changed.

### 4. CatalogDevSeeder — Accepted (d), my Round 1 note 1 is withdrawn
`slice-plans/slice-1b-plan.md` D23 prescribed exactly this `if (TryGetValue(…, out existing)) { product = existing; } else { … }` shape to fix the CS8602 warnings. It's 1b work, not yours, and your "mechanical" claim was right. Sorry for the noise.

**What was good:** You marked every non-mechanical hunk this round, and when positional records broke the EF translation you fixed it with member-init and left a comment saying why. That comment will stop the next person from "simplifying" it back.

## Round 2 — Marco → Rob

1. **Fixed** — I kept the gate but made it accurate. `scripts/verify.sh` now has a `cs_var_hits` helper (about lines 28-34) that `step_hygiene` calls. It lists tracked and untracked `backend/*.cs` (bin/obj excluded) and skips lines starting with `//`, `/*` or `*`. It then strips string literals (including `@"` and `$"` forms) and trailing `//` comments, matches `(^|[^[:alnum:]_.@])var([[:space:]]+[@_[:alpha:]]|[[:space:]]*\()`, and prints file:line. My first regex wrongly matched `variant`; I tightened it before the final run. Proven with a temporary untracked `backend/zz_tmp.cs`, deleted afterwards (`ls backend` confirms it is gone):

| Case | Result |
|---|---|
| `var x = 1;` | FAIL (zz_tmp.cs:1) |
| `foreach (var y in z)` | FAIL |
| `var (a, b) = t;` | FAIL |
| `// var is banned` | PASS |
| `string s = "var x";` | PASS |
| `int variant = 1;` | PASS |

Full `bash scripts/verify.sh`: VERIFY PASS (all steps), hygiene "no matches" on the real tree. Not covered: `var` inside multi-line verbatim strings or block-comment interiors that don't start with `*`.

## Round 3 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)` — build 0 Error(s) 0 Warning(s) · jest 280 · hygiene "no matches" on the real tree

Nathaniel decided to keep the gate: IDE0008 exempts anonymous-type `var`, and he wants zero. So the question is only whether `cs_var_hits` is accurate enough to gate every slice. It is.

### 1. The hygiene `var` gate — Confirmed
I ran every case through `bash scripts/verify.sh hygiene` with a throwaway untracked `backend/zz_rob_tmp.cs`, deleted afterwards (`ls backend` confirms it's gone):

| Case | Result | Correct? |
|---|---|---|
| `var x = 1;` | FAIL | yes |
| `foreach (var y in z)` | FAIL | yes |
| `var (a, b) = t;` | FAIL | yes (was a miss in Round 2) |
| `out var q` | FAIL | yes |
| `// var is banned` | PASS | yes (was a false positive) |
| `string s = "var x";` | PASS | yes (was a false positive) |
| `string v = @"a ""var x"" b";` | PASS | yes |
| `int variant = 1;` · `int @var = 1;` · `x = obj.var y;` | PASS | yes |

All three Round 2 defects are fixed, and your six cases reproduce.

### 2. Known gaps — NOTE (accepted)
These can produce **false positives**. I reproduced each one:
- A block-comment interior line that doesn't start with `*` (`/*` / `we use var here` / `*/`) → FAIL.
- A `var` inside a multi-line raw (`"""`) or verbatim string → FAIL.
- One more you didn't list: a `'"'` char literal before a string on the same line (`x = '"' + "var y";`) → FAIL, because the stripper treats the `"` in the char literal as the start of a string.

**Can they hit the real tree?** Not today. The tree has 0 lines starting with `/*`. The 9 files that contain `"""`, `@"` or `/*` all pass. The gate reports "no matches", and the full `verify.sh` is green. In future, the trigger is prose or embedded C# containing `var <word>` inside a multi-line comment or string. That's unlikely, and when it happens the gate prints `file:line`, so the fix (reword, or use `//`) takes a minute. False negatives (`var` after `/* … */` on the same line) are backed up by IDE0008 for every type except anonymous ones. Neither needs a change now. If one ever fires on legitimate text, fix it then rather than growing the awk script up front.

**What was good:** You published the gaps alongside the cases, so the limits of the check are known before they bite. Excluding `.var` and `@var` shows you thought about identifiers, not just the keyword.
