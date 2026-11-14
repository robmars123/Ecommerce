# Slice P2.68 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 warnings, jest 272 suites / 2111 tests

You moved the admin category endpoints across cleanly. The new file does what the controller did: one validator call, then one service call, then a map of the outcome. It adds no query, no read before validation and no new behaviour. I found no blocking issues.

### What I checked

- **Routes and auth:** one group on `api/v1/admin` with the `Admin` policy, and the three routes `categories` / `categories/{slug}`. No `MapPost("")`, so no trailing-slash route. Inventory rows 18/44/71 are unchanged (T1 green).
- **Round trips:** the handlers call the same three service methods with the same commands as the controller. Validation runs before any DB access. The service is untouched, so the per-request counts in plan §1 still hold. The extra parent-slug SELECT on PUT already existed and is on the backlog. I found no N+1 and no over-fetching.
- **Outcome mapping:**
  - `Invalid` passes the service's own keys straight through, which is what `ModelState.AddModelError(field, …)` did.
  - `Conflict` keeps a single `errors` key, the 409 type and the title "Conflict".
  - `_` gives a 404 with the title "Category not found".
  - IC13, IC14 and IC15 pin all three, and they were green on MVC first.
- **`ToErrors`:** it uses the same `CamelCase` as `ToProblem` and groups messages per key in failure order, which matches what ModelState produced. `ToProblem` is left alone for `AdminProductsController`.
- **201:** the 201 sends no `Location`, as in MVC (IC11). The `(string?)null` cast only picks the string overload of `Created`.
- **Unreadable JSON:** the 400 is still `application/problem+json` with the same `type`, `traceId` and `correlationId`. The changes to `title` and `errors` are recorded in 13.2 (A6). IC12 also proves that nothing gets written.
- **Boundaries:** the class is `internal static` in `Ecommerce.Catalog.Api.Admin`. The only references are to `Identity.Contracts`, and the arch tests are green.
- **Tests:** no existing test was edited or deleted. The five new pins follow the existing `IdentityTestEnv` pattern.

### Is `ToResult<TOk>` too clever? No, keep it.
It is the controller's `switch` written as a switch expression. The generic parameter only carries each handler's success type (`Created<…>`, `Ok<…>`, `NoContent`), so the typed `Results<…>` union still compiles. Writing it three times would mean 3 × 4 arms of identical mapping, and the 404 title or the 409 shape could then drift between verbs. A reader sees four arms, one per outcome, and that is all it does. The one bit of ceremony is `where TOk : IResult`, and `Results<…>` requires it.

### Mutations (re-run by me, each restored from a scratch copy, `cmp` identical)

| # | Mutation | Red |
|---|---|---|
| M1 | remove `ConfigureHttpJsonOptions` (Program.cs:33) | `Failed: 3` — HttpContractTests.T5, AdminCategoryContractTests.IC12, AdminCategoryApiTests.IC4 |
| M2 | group policy → `AdminOrSupport` | `Failed: 2` — EndpointInventoryTests.T1, AdminCategoryApiTests.IC3 (Support → Forbidden) |
| M3 | `Update` skips the validator (`if (validation.Errors.Count < 0)`) | `Failed: 1` — AdminCategoryApiTests.IC4 |

These match your report exactly. (My first M3 attempt edited the wrong line and failed to compile. I restored the file from the copy, confirmed it with `cmp`, and re-ran.)

### 1. No POST as Customer/Support — NOTE
**What I saw:** IC3 sends PUT and DELETE as Customer and Support, but nothing sends POST as either role. The group policy covers POST too, and M2 shows that the inventory row catches a policy change. Even so, a role test is the more direct proof.
**What I want (next touch):** add a POST role row to IC3. This is now in the backlog.
**Lesson:** each verb on an auth-guarded write gets its own role test. A shared group is today's implementation, and the test should not depend on it.

**What was good:**
- Step 1, capturing the six MVC bodies before writing any code, turned A6 and A7 from guesses into recorded facts.
- IC12 asserts "nothing written" as well as the status. That is the check that matters for mass assignment.
