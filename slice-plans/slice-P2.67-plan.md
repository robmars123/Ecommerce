# Slice P2.67 — AdminProjections replay endpoint to Minimal API (Rob)

Tier **High** (Admin-only write: replay resets a read model). Mode Autonomous. Tracker row P2.67 (`docs/admin-dashboard-feature-tracker.md:86`), master plan row P2.67 (`slice-plans/minimal-api-migration-plan.md:84`).

**Read:** master plan §2.1 D3/D4/D5/D8 and §4 standard steps · this plan · `Admin/Ecommerce.Admin/Api/AdminDashboardController.cs` (23 lines, the whole controller) · `Api/AdminSettingsEndpoints.cs` (single-route pattern, no group) · `AdminModule.cs:29–35` · `tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs:163–204` (replay tests) and `:449–465` (P2.65 pin header + `AssertValidationProblem` style) · `EndpointInventoryTests.cs:56`. Nothing else. Do not open `ProjectionServices.cs`: the service is unchanged and §6 below states what it does.

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| J1 | New file `Api/AdminProjectionsEndpoints.cs`, `internal static class AdminProjectionsEndpoints`, `Map` registered in `AdminModule.MapAdminEndpoints` after `AdminInsightsEndpoints.Map(app)`. Delete `Api/AdminDashboardController.cs` in the same change (it holds only this class now) | Master D1/§4 step 2; both mapped → `AmbiguousMatchException` | Keeping the old file name |
| J2 | One route, no group: `app.MapPost("api/v1/admin/projections/{name}/replay", Replay).RequireAuthorization(AuthorizationPolicies.Admin)` | Master D4 single-route rule (same as `AdminSettingsEndpoints`); Admin only, so Support never triggers a write (6b D5) | A group on `api/v1/admin` (nothing else to share it) |
| J3 | Route param stays `{name}`, unconstrained `string`. Handler params: `string name, [FromServices] IProjectionReplayer replayer, CancellationToken ct`. **No body parameter**: tests POST `{}` and it must stay ignored | Same RawText as inventory row 56; same binding | A `:regex` constraint (changes RawText and 404 path) |
| J4 | Return type `Results<Accepted, ProblemHttpResult>`. One call `await replayer.ReplayAsync(name, ct)`, then a switch with the controller's three arms in the same order, the `_` arm still 404: `Done` → `TypedResults.Accepted((string?)null)` (no Location, as MVC `Accepted()`); `Busy` → `TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Projection busy", detail: "projection-busy: another worker is running this projector.")`; `_` → `TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Projection not found")`. Strings byte-identical. If the target-typed switch does not compile, use three plain `if`/`return`s — no casts beyond the `(string?)` | Master D3/D8; zero behaviour diff; the cast picks the `string?` overload over `Uri` | `TypedResults.StatusCode(202)` (untyped); `Results.Accepted()` |
| J5 | Keep the controller's `<summary>` (Admin only, 6b D5, 409 while another worker holds it) on the new class | Same intent, one line | Dropping the 6b reference |
| J6 | No role flag from `ClaimsPrincipal`: replay has no role-dependent output; the policy alone gates it | Nothing consumes a flag here | Adding an unused `ClaimsPrincipal` |
| J7 | No docs change. `13.2-decision-log.md:95` mentions `AdminDashboardController` as history; P2.81 rewrites controller wording | Historical row, not a live reference | Editing a history row |

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminProjectionsEndpoints.cs` | **new** (J1–J5), ~25 lines |
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminDashboardController.cs` | **delete** |
| `backend/src/Modules/Admin/Ecommerce.Admin/AdminModule.cs` | one line: `AdminProjectionsEndpoints.Map(app);` |
| `backend/tests/Ecommerce.Api.IntegrationTests/Projections/AdminDashboardApiTests.cs` | **append** P1–P2 under `// ---- P2.67 pins: replay result shapes, written on MVC before the Minimal API conversion ----`; existing tests unedited |
| `docs/admin-dashboard-feature-tracker.md:86` | P2.67 → Done (after PASS) |

No migrations, no `*.Contracts`, no SharedKernel/Persistence change, no packages, no `EndpointInventoryTests` edit.

## 3. Build order

1. **Baseline on MVC.** Run `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~AdminDashboardApiTests|FullyQualifiedName~CatalogSalesTests|FullyQualifiedName~EndpointInventoryTests|FullyQualifiedName~AuthorizationTests"`; quote counts.
2. **Pins on MVC, test-first.** Add P1–P2 (§4); run on MVC and quote green. A pin red on MVC means MVC is the spec: fix the pin's expectation to what MVC does and report it; never the code.
3. **Convert** (J1–J4) in one change: new file + `AdminModule` line + delete the controller file.
4. Step-1 filter green, pins and inventory unedited and green; then full `bash scripts/verify.sh 2>&1 | tail -n 30`.
5. Mutations (§5), red quoted for each.

## 4. Required tests

Existing, unedited, must stay green:
- `Replay_is_401_anonymous_and_403_for_a_Customer_and_for_Support` (policy; the Support-403 is the write-role guard).
- `Replay_of_an_unknown_projector_is_404_and_of_a_known_one_is_202`.
- `Replay_while_the_projector_is_held_by_another_worker_is_409_and_changes_nothing` (no reset under contention).
- `CatalogSalesTests:141` (replay of the catalog projector → 202, then rebuild).
- `EndpointInventoryTests` row 56 (`POST api/v1/admin/projections/{name}/replay | Admin | auth | credentialed`) and II10/II11.

New pins (step 2, on MVC first; use the `Api` record's `Response`/`Text`; the env sends `X-Correlation-ID: id-test-1`):
- **P1** `Replay_404_is_a_problem_and_202_has_no_body_or_location`: `nope` as Admin → 404, `application/problem+json`, `status` 404, `title` "Projection not found", `type` `https://tools.ietf.org/html/rfc9110#section-15.5.5`, **no `detail` property**, non-empty `traceId`, `correlationId` "id-test-1". `admin.dashboard` as Admin → 202, `Text` empty, no `Location` header, no `Content-Type` on the content.
- **P2** `Replay_409_is_a_problem_with_the_busy_title_and_detail`: hold `pg_advisory_lock(hashtext('projection:admin.dashboard'))` exactly as the existing 409 test does → 409, `application/problem+json`, `status` 409, `title` "Projection busy", `detail` "projection-busy: another worker is running this projector.", `type` `https://tools.ietf.org/html/rfc9110#section-15.5.10`, non-empty `traceId`, `correlationId` "id-test-1".

## 5. Mutations (High) — break → red → restore from scratch copy → `cmp`

| # | Guard | Break | Expected red |
|---|---|---|---|
| M1 | Admin-only write (policy) | `.RequireAuthorization(AuthorizationPolicies.AdminOrSupport)` | `EndpointInventoryTests` (row 56) and `Replay_is_401_…_and_for_Support` (Support gets 202) |
| M2 | Busy → 409 (no second replay concurrently reported as success) | map `ReplayOutcome.Busy` to `TypedResults.Accepted((string?)null)` | `Replay_while_…_is_409_…` and P2 |

Two only (one per guard, CLAUDE.md §3). Quote each red line and the `cmp` result.

## 6. What Rob checks in review (name, then evidence)

- **Queries per request (handler):** exactly one service call, no loop. Replay itself, unchanged (`Ecommerce.Persistence/Projections/ProjectionServices.cs:23–55`): unknown name → 0 queries (in-memory lookup over `IEnumerable<IProjector>`); busy → 1 (`pg_try_advisory_lock` on its own connection); done → lock + `BEGIN` + projector `ResetAsync` (dashboard: 2 `ExecuteDelete`; catalog sales: 1) + 2 `DELETE` + 1 upsert + `COMMIT` + unlock = constant, no projector loop, no per-event work (the worker rebuilds later).
- **Rebuild/replay write path:** idempotent (reset to checkpoint 0 + empty read model; a second replay yields the same state); one transaction for reset, dedupe marks, versions and checkpoint; no external call inside it (only DB statements; the advisory lock is a separate DB session, not an external service). Unchanged by this slice; the diff must not touch it.
- **Projection:** no read-model query in this endpoint; nothing projected or serialised — 202 has no body, 404/409 are problem details. No response record to move.
- **Cross-module contract calls:** none. The handler depends only on SharedKernel `IProjectionReplayer` (Persistence implementation); projectors from Admin and Catalog are reached through DI, not module contracts. Unchanged.
- **Policy:** single `RequireAuthorization(Admin)`, not stacked, no `AllowAnonymous`; inventory row 56 unedited.
- **Shapes:** P1/P2 green on MVC (quoted) and after conversion; strings byte-identical to the deleted controller.
- **Minimal:** no group, no body param, no `ClaimsPrincipal`, no helper; controller file gone; `using`s trimmed (no `Microsoft.AspNetCore.Mvc` beyond `[FromServices]`).

## 7. Backlog NOTEs (appended to `slice-plans/backlog.md` with this plan)

- N1: `ProjectionReplayer.ReplayAsync` maps every non-`Done`/`Busy` outcome to 404 via `_` in the endpoint; a future `ReplayOutcome` member would surface as "Projection not found". Make the switch exhaustive when the enum grows.
- N2: Replay resolves every registered `IProjector` (and its scoped deps) from DI to find one by name; cheap today (2 projectors), worth a name → type map if projectors gain heavy dependencies.

## 8. Risks

- **`Accepted` overload ambiguity** (`string?` vs `Uri`): J4's cast; P1 proves no `Location`.
- **Problem `type`/`detail` drift between MVC `ProblemDetailsFactory` and `IProblemDetailsService`:** P1/P2 pinned on MVC first.
- **Empty-body POST:** tests send `{}`; with no body parameter the minimal handler ignores it, as MVC did. If any pin shows 415/400, stop and report; no improvised body binding.
- **Policy downgrade to AdminOrSupport** would let Support wipe a read model: M1.
