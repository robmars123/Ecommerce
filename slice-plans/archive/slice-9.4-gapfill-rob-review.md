# Slice 9.4 gap-fill — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not re-run by me (I run the full gate only in a PASS round, CLAUDE.md §5). Your report: `VERIFY PASS`, 0 warnings, jest 1103.

The diff matches the plan closely: the `EfUnitOfWork` change is exactly the two FK arms and nothing else, the delete guard is the FK with no pre-check query, the controller is thin, and IC8/IC9 were red-first. One race that delete makes reachable still returns 500. My plan's D7 missed it, so it's my gap, but it's a bug and it blocks.

### 1. PUT or DELETE on a row deleted mid-request answers 500. BLOCKING   (`Application/Admin/AdminCategoryService.cs` UpdateAsync / DeleteAsync save blocks)
**What I saw:** `UpdateAsync` and `DeleteAsync` load the tracked row with `GetForUpdateAsync`, then save. If another request deletes that row between the read and the save, EF's `UPDATE … WHERE id = @id` or `DELETE … WHERE id = @id` affects 0 rows. EF throws `DbUpdateConcurrencyException` (it checks the affected-row count even without a concurrency token), and `EfUnitOfWork.Translate` turns it into `ConcurrencyConflictException`. Neither method catches it.
**Why it matters:** an admin double-clicks Delete, or two admins delete the same category. One request gets 204 and the other gets 500 instead of the 404 that IC7 promises for a second delete. The same happens to a rename racing a delete. The 9.7 screen will show a server error for a normal action. D7 exists to close exactly this class of "newly reachable race returns 500".
**What I want:** in both methods add `catch (ConcurrencyConflictException) { return new AdminResult<CategoryView>.NotFound(); }` next to the existing catches. Nothing else changes. I've added this to the §13.2 "Category edit and delete" row: the row is gone, so the answer is 404, the same one a later request gets.
**How I'll check:** two new unit tests in `AdminCategoryAndCriteriaTests.cs`, red first (quote the red): `Update_of_a_category_deleted_mid_request_is_NotFound` and `Delete_of_a_category_deleted_mid_request_is_NotFound`. Each uses a stored category plus `uow.FailOnSave = new ConcurrencyConflictException("x")`. Then `bash scripts/verify.sh changed` + `dotnet test backend/tests/Ecommerce.Catalog.Tests -c Release`.
**Lesson:** when you add a delete, check every read-then-write path on that row for "the row vanished between my read and my save", not only the FK paths.

### Your deviations
1. **IC10 not re-added: Accepted.** `AdminProductApiTests.IA7` (line 258) covers POST happy and duplicate 409 (line 266), and it still passes after the `ToActionResult` refactor.
2. **IC3 role as a string: Accepted.** `Enum.Parse<UserRole>(role)` keeps the three cases (401, Customer 403, Support 403) on both verbs, and the row-unchanged check is there.
3. **Generic `CategoryFieldRules.Add<T>`: Accepted.** The rules and messages live in one place for both validators, as D10 asked. Your validator unit test pins the `Slug`/`Name` property names.
4. **Unit tests written after the code: Accepted for this Low-tier slice.** The invariant tests (IC8, IC9) were red-first, and your "break the D6 catch → 500 → restore → `cmp`" run proves the translation is what produces the 409. For finding 1, write the tests red-first.

### 2. Delete happy path has no unit test. NOTE (backlog)
`FakeCategoryStore.Removed` is never asserted. IC7 covers the path over HTTP.

### 3. IC5 checks only the status. NOTE (backlog)
The plan named a ProblemDetails body. Assert it when you're next in that file.

**What was good:** `EfUnitOfWork` gained exactly the two arms, both after the unique arms and after `DbUpdateConcurrencyException`, so existing translations keep their precedence. IC8 uses an **inactive** product, which is the case a "count active products" pre-check would have got wrong.

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); jest 159 suites / 1103 tests passed.

### 1. Update/Delete 500 on ConcurrencyConflictException — Confirmed fixed
`AdminCategoryService.UpdateAsync` and `DeleteAsync` now catch `ConcurrencyConflictException` around `SaveChangesAsync` and return `NotFound`, so a category deleted mid-request gives 404 and not 500. Two new tests (`Update_of_a_category_deleted_mid_request_is_NotFound`, `Delete_of_a_category_deleted_mid_request_is_NotFound`) set `FailOnSave` to the exception and assert `NotFound`. Marco's red (2 failed / 179 passed) matches those two tests. The delete test for an unrelated FK still checks that it is not swallowed. No test was weakened.

**What was good:** the catch is scoped to the save call only, and the specific-constraint filters on the FK catches are kept.
