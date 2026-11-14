# Slice P2.70 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` · build 0 warnings · jest 272 suites / 2111 tests

This is a clean one-for-one conversion. The handlers stay thin, the service is untouched, and you were right to break from the plan on IV6. I found no blocking issues and no bugs.

### Checklist (plan §7), checked against the code, not the report
- **Thin handler:** `Adjust` validates, makes one `AdjustAsync` call, logs, and maps the result. `List` makes one `ListAsync` call. There is no `DbContext`, no `InTransactionAsync`, no `SaveChanges`, no Catalog call and no loop. `git diff --stat` shows nothing under `Application/`, `Infrastructure/` or `Domain/`, so the §3.4 stock rule (ensure → lock in variant-ID order → adjust → audit → one save, all in one tx) still lives only in the unchanged `InventoryAdminService`.
- **Queries:** the counts are unchanged (list = 3, adjust = 1 Catalog read + 1 tx). The handler adds no query, no N+1 and no extra projection; it maps the service's records with the existing `ToResponse()`.
- **409 bodies:** `Conflict(...)` returns a plain `ProblemDetails` with `onHand`/`reserved`/`available` and no `errors`. IV1 and IV2 pin this, and both went red under M1 and M2.
- **Boundaries:** `AdminInventoryEndpoints` and `QueryValues` are `internal`, and `MapInventoryEndpoints` is the only public addition, on `InventoryModule`. There is no new project reference. `QueryValues.cs` differs from Catalog's only in the namespace (`diff`: line 3 only).
- **Tests:** no existing test was edited or deleted. `backend/tests` has one new file only.
- **Dead code:** the controller is deleted. The Inventory assembly is still in `ModuleAssemblies` with no controllers left, but master plan D14 removes that list in P2.81, so nothing needs to change now.

### 1. Two-stage list validation — Confirmed (your deviation is right)
**What I saw:** my plan (P3/IV6) said MVC returns `page` and `pageSize` together for `?page=abc&pageSize=0`. Your capture shows that MVC returns only `page`. That fits how MVC works: under `[ApiController]`, the automatic 400 filter returns binding errors before the action body runs, so the range checks never ran. Your handler does the same thing in the same order. First it parses both values (so `?page=abc&pageSize=abc` still reports both keys, as MVC did). Then it checks the ranges together (pinned by `?page=0&pageSize=0` → both keys).
**Why it matters:** a single combined return would have added a `pageSize` key that MVC never sent. That is a contract change hidden inside a "no behaviour change" migration.
**Plan corrected:** P3, IV6 and the §8 risk line in `slice-P2.70-plan.md` now describe the two-stage order and the MVC capture.
**Lesson:** pin the captured behaviour, not the behaviour the plan predicted. Step 2 exists for exactly this.

### 2. Log category change — Confirmed harmless
**What I saw:** the category is now `Ecommerce.Inventory.Api.AdminInventoryEndpoints`. I grepped for `AdminInventoryController`, `Ecommerce.Inventory.Api` and the message text across appsettings, compose, docs and tests, excluding build output. The only hits are the new file and the slice files. No `LogLevel` filter, dashboard or test reads the old category. The message template and its fields (`VariantId`, `Outcome`, no reason text) are unchanged.

### 3. Mutations, re-run by me (scratch copy → `sed` → `~Inventory` filter → restore → `cmp OK` for each)
| # | Break | Red (Failed / 50) |
|---|---|---|
| M1 | Insufficient arm → `Ok(null!)` | 3: IV2, T10, concurrent adjust vs reserve |
| M2 | Stale arm → `Ok(null!)` | 2: IV1, `A_stale_expectedOnHand_is_409…` |
| M3 | validation return skipped | 8: IV4, `An_invalid_adjust…` ×6, `A_501_character_reason…` |
| M4 | `Admin` → `AdminOrSupport` | 2: `Both_endpoints_are_403_for_support…`, `T1_every_endpoint…` |
| M5 | `actorId` → `null` | 2: T10, `A_successful_adjust…` |

These match your report exactly. The file is restored and byte-identical (`cmp`).

### 4. Edge cases checked, no finding
- Blank `page=` / whitespace → `null` → defaults; IV7 pins this, same as MVC.
- A repeated key takes the first value (IV7). `?page=abc&page=2` → 400, consistent with P2.63.
- A `null` JSON body is rejected by minimal API binding before the handler runs, because the parameter is non-nullable. IV5 pins it and checks nothing was written.
- The `sub` claim is read the same way as in the controller. The Admin policy guarantees an authenticated user.

### 5. NOTE — default validator message on `{}` (backlog)
`{}` returns `variantId: "'Variant Id' must not be empty."`, which is FluentValidation's default. `WithMessage("Choose a variant.")` binds only to `NotEqual`, not to `NotNull`. This is the same on MVC, and IV4 correctly pins only the keys for `{}`. It goes to the backlog.

**What was good:** You captured MVC before writing any code and pinned the value you actually saw, even though it contradicted my plan. You also added the `page=0&pageSize=0` row, so the second stage is still proven to report both keys. The `switch` expression with a single `Conflict` helper is shorter than the controller and keeps the same arms.
