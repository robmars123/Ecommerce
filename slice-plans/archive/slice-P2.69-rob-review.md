# Slice P2.69 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, integration 604 passed, jest 272 suites / 2111 tests.

The conversion is a faithful 1:1 move: eight routes, one service call per handler, the same command-building lines, the same 404/409/400 shapes, and the upload conventions carry today's numbers. I re-ran M1–M5 myself; all red, all restored with `cmp` OK. No blocking findings.

### Mutations (re-run by Rob, scratch copy → break → red → restore → `cmp`)
| # | Break | Red |
|---|---|---|
| M1 | group policy → `AdminOrSupport` | `EndpointInventoryTests.T1`, `IU3` (2 failed / 16) |
| M2 | drop `RequestSizeLimitAttribute` metadata | `IP6` (1 / 7) |
| M3 | `Conflict` arm → `ValidationProblem` (400) | `IP1`, `IA2` ×3, `IA1`, `IA3`, `IA4` (7 / 18) |
| M4 | delete `Create`'s `if (!validation.IsValid)` return | `IA1`, `IP3`, `IP4` (3 / 18) |
| M5 | drop `.DisableAntiforgery()` | `IP6`, `IU1`, `IU2`, `IU4` ×8, `IU6`, `IU8` (13 / 22) |

M6 not re-run: `.Accepts` is deleted, so there is nothing to mutate; IP6 still asserts the inferred metadata is `["multipart/form-data"]` with `IsOptional == false`, and it is green on the current code.

### Coordinator's checks

**1. Non-multipart / no-Content-Type upload still 404 (IP7).** Two tests pin it: `IU5` (JSON body → 404, 0 blobs) and the new `IP7` (no Content-Type, empty body → 404, 0 blobs). Both use a product that exists, so the handler's only 404 ("Product not found") can't produce it, and if the handler were reached `file` would be null and it would answer 400 `errors.file`. A 404 therefore proves the request is rejected by the accepts-metadata/fallback before the handler. IP6 pins the cause (`IsOptional == false`), so the deletion of `.Accepts` is guarded both at the metadata level and over HTTP.

**2. P9 `.Cascade(Stop)` on shared validators.** `CreateProductRequestValidator` / `UpdateProductRequestValidator` have one consumer each (DI in `CatalogModule.cs:50-51`, used only by these handlers); the shared parts (`ProductFieldRules`, the child variant/image validators) are untouched. `Cascade(Stop)` only changes the path where `NotNull` fails: before it fell through to `.Must(v => v.Count …)` and threw (500, IP4 red quoted). For any non-null list the error list is identical. The null-list *message* now comes from FluentValidation instead of MVC's `[Required]`; no frontend code reads `errors.variants`/`errors.images` (grep of `frontend/apps/store`, no hits), the admin form always sends arrays, and no existing test asserts that text. `CatalogValidationTests` stayed green in verify.

**3. `ProductEndpoints` `string[]?`.** All five params bind `string[]?` and read `?.FirstOrDefault()` (page/pageSize via `QueryValues.ReadInt`, which does the same), so `?page=2&page=abc` → page 2, as MVC did. B6 was red first (`Expected: OK / Actual: BadRequest`, Marco's quote) and is green. `QueryValues.cs` is body-identical to Admin's (I diffed it). Backlog line 110 was **not** marked resolved; I marked it `Resolved P2.69` myself (backlog is mine to maintain; not a code finding).

**4. Full verify.sh** re-run by me after the `.Accepts` deletion: PASS (line above).

### Checklist (plan §6)
- **Queries:** one service call per handler; `List` builds criteria in memory then calls `ListAsync` once; upload does the null check before `UploadAsync`, so a missing file never hits the DB. No N+1, no extra round-trip added. Detail/preview over-fetch is already in the backlog (P2.69 plan note).
- **Writes:** handlers never touch the `DbContext`; one `SaveChanges` stays inside the services.
- **Shapes:** 409 keeps `errors.version` (IP1, M3); `Created` has the Location header (IA1); 404 title "Product not found" on all five id routes (IP2).
- **Diff scope:** `ProductEndpoints` limited to P6; `AdminValidators` limited to P10 + P9 (quoted red); `ToProblem` and its `using Microsoft.AspNetCore.Mvc` gone, no dead code left. No other production file touched.
- **Changed tests:** none edited; only B6 added and the new `AdminProductContractTests.cs`.
- **Boundaries:** `AdminProductsEndpoints` and `QueryValues` are `internal static`; architecture tests green.

### 1. Long upload return line — NOTE   (`Catalog/Api/Admin/AdminProductsEndpoints.cs:144`)
**What I saw:** the `UploadAsync` call, the command and the mapper sit on one ~150-character line.
**Why it matters:** it's the one line in the file a reader has to scroll to follow; everything else reads at a glance.
**What I want:** split it next touch. → backlog.

### 2. B6 pins `page` only — NOTE   (`ProductQueryBindingTests.cs:85`)
**What I saw:** `search`/`category`/`sort` share the `FirstOrDefault` line but no test sends them repeated.
**Why it matters:** a later edit back to `string?` on one of them would pass every test.
**What I want:** one repeated-key row for `sort` next touch. → backlog.

**What was good:** You stopped at the plan's M6 rule instead of keeping `.Accepts` "to be safe", and IP7 asserts the blob count as well as the status, so it proves nothing was stored, not just what the response was. `ToResult` keeps the controller's `throw` on an unknown result instead of copying categories' silent 404.
