# Slice P2.67 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` — build 0 Error(s) 0 Warning(s); integration 591 passed / 0 failed; jest 272 suites, 2111 tests passed.

You moved the one replay route from MVC to a single Minimal API endpoint with the same policy, the same three outcomes and byte-identical problem strings, and pinned the shapes on MVC first. The diff is four files and does exactly what the plan asked; I found no bugs and nothing blocking.

### What I checked (evidence, not your report)

- **Git state:** `git status --short` shows ` D .../AdminDashboardController.cs` (working-tree deletion, space in the index column) and `git diff --cached --stat` is empty. Nothing is staged; the denied `git rm --cached` left no trace.
- **Boundaries:** the class is `internal static`; it depends only on `Ecommerce.Identity.Contracts` (policy names) and SharedKernel `IProjectionReplayer`. No csproj or architecture-test change. No remaining reference to `AdminProjectionsController`/`AdminDashboardController` in `backend/` (grep).
- **Policy:** one `RequireAuthorization(AuthorizationPolicies.Admin)`, not stacked, no `AllowAnonymous`; inventory row 56 unedited and green.
- **Queries / round-trips:** one service call per request, no loop, no read-model query, nothing serialised. Replay write path (`ProjectionServices.cs`) untouched by the diff.
- **Shapes:** `TypedResults.Accepted((string?)null)` gives 202 with no body, no `Location`, no content type — P1 proves all three. 404 has no `detail`, 409 has the exact detail; `type`, `traceId`, `correlationId` pinned in P1/P2.
- **Minimal code:** expression-bodied `Map` and handler, one switch, no group, no body param, no `ClaimsPrincipal`, no helper. `using`s are all used (`Mvc` only for `[FromServices]`). Easy to read in one screen.
- **Tests:** none edited or deleted; P1–P2 appended under the pin header. The existing 401/403/Support, 404/202, 409-no-change and `CatalogSalesTests` replay tests stay green.

### Mutations (re-run by me, each restored from a scratch copy, `cmp` OK)

| # | Break | Red |
|---|---|---|
| M1 | `.RequireAuthorization(AuthorizationPolicies.AdminOrSupport)` | `EndpointInventoryTests.T1_every_endpoint_has_the_expected_methods_auth_and_cors`, `Replay_is_401_anonymous_and_403_for_a_Customer_and_for_Support` — `Failed: 2, Passed: 4` |
| M2 | `ReplayOutcome.Busy => TypedResults.Accepted((string?)null)` | `Replay_409_is_a_problem_with_the_busy_title_and_detail`, `Replay_while_the_projector_is_held_by_another_worker_is_409_and_changes_nothing` — `Failed: 2, Passed: 3` |

Both guards are killed by two independent tests each, matching your report.

### NOTEs

None new. The plan's two NOTEs (non-exhaustive `_` → 404 arm; replayer resolving every `IProjector` to find one by name) are already in `slice-plans/backlog.md`.

**What was good:** you kept the `_` arm and the `(string?)` cast exactly as J4 said instead of "improving" the switch, so behaviour is provably unchanged. P1 asserts the *absence* of `detail`, `Location` and content type — the cheap pins that catch serializer and overload drift.
