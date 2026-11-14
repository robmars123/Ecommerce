# Slice P2.64 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, integration 577/577, jest 2111/2111.

The conversion is a faithful transport-only move: I diffed `AdminCustomersEndpoints.cs` against the deleted controller at `HEAD`, and every rule, text, default, mapping line and the role flag carried over unchanged. The two new behaviours that Minimal API would have changed silently (repeated keys, and binding errors reported before range errors) are handled and pinned.

### What I checked
- **Routes / auth:** the group is `api/v1/admin` + `customers` / `customers/{id:guid}`, so there's no trailing slash, and `AdminOrSupport` is set once on the group. The inventory T1 and II11 tests stay green with no edits.
- **Queries:** each handler makes exactly one service call (`ListAsync` / `GetAsync`) with no loop around it. `git diff --stat` shows nothing under `Application/` or any other module, so the per-request query count is unchanged (list 2, detail 1/6/10). This slice adds no N+1, no extra round-trip and no new field. The over-fetch that already exists in the readers was backlogged at plan time (C9).
- **Limits before the call:** `page ≥ 1`, `pageSize` between 1 and 100, and the trimmed `search` at most 100 characters are all rejected before `ListAsync` runs.
- **Edge cases traced:** blank values fall back to the defaults (I10). For `?page=abc&page=2`, MVC also bound the first value, so it's a 400 in both. `?page= 5 ` parses, because `NumberStyles.Integer` allows surrounding whitespace, which matches MVC. A missing key binds `string[]` as empty, so `FirstOrDefault` gives null and the default applies. A malformed guid fails the route constraint → 404 problem (I13).
- **Changed tests:** none. I1–I8 are byte-identical, and I9–I13 are additions pinned on MVC first (21/21 before and after, per your report).
- **Mutations** (I re-ran each one, restored from a scratch copy, and `cmp` came back clean every time):

| # | Mutation | Red (mine) |
|---|---|---|
| M1 | policy → `Admin` | T1, I1, I2 (3 failed / 21) |
| M2 | drop `RequireAuthorization` | T1, II11, I1 (3) |
| M3 | role flag → `true` | I2 (1) |
| M4 | `pageSizeNumber is < 1` only | I6 `?pageSize=101`, I12 (2) |
| M5 | `string.Join(",", search ?? [])` | I11 (1) |

These match your report exactly.

### 1. `ReadInt` copied per file — NOTE   (`AdminCustomersEndpoints.cs:118`, `Catalog/Api/ProductEndpoints.cs:59`)
**What I saw:** the 14 lines are byte-identical in two modules. Each caller also writes `x?.FirstOrDefault()` before calling it.
**Why it's fine now:** Admin can't reference Catalog, and a shared helper would need a home that both modules can see. That home would be SharedKernel, and it would put HTTP-binding wording ("The value 'x' is not valid.", which mirrors MVC) into the domain kernel. With two copies in two modules, a copy is the plainest choice: anyone reading the file sees the whole rule without jumping elsewhere.
**When it changes:** if a third consumer appears **inside one module** (Admin is the likely one, since P2.65 and the admin orders lists take query paging), make it one `internal static class QueryValues` in that module. It should take `string[]?` directly (`ReadInt(page, "page", errors)`), so the `?.FirstOrDefault()` noise goes away at each call site. Don't create a cross-module helper for this.
**Lesson:** duplicate across a module boundary; share within one, but only once a second consumer exists.

### 2. I9 second call doesn't assert the status — NOTE   (`AdminCustomersApiTests.cs`, I9 `both`)
**What I saw:** `?page=abc&pageSize=500` checks the `errors` keys but not `400`. An `errors` object only exists on a validation problem, so this is safe today. An explicit `Assert.Equal(HttpStatusCode.BadRequest, …)` would make the test read as complete. Do it the next time you touch the file.

**What was good:** you put the two-stage validation (bad numbers alone, then the range checks together) behind a single comment line and an early return, which is the plainest way to say it. Pinning I9–I13 on MVC before the conversion meant the handler was checked against what MVC actually did, not against the plan's description of it.
