# Slice P2.62 — AdminSettings to Minimal API + D7 CORS host wiring + verify.sh `var`-scan fix (Rob, plan)

Mode: Autonomous · Tier: **High** (auth policy, CORS credentials, quality gate) · Tracker: P2.62 (`docs/admin-dashboard-feature-tracker.md:81`)

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§3 (D4, D5, D7 especially) · this plan · `03-principles-hard-rules.md:40` · `06-backend-modules.md:63–69` · `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSettingsController.cs` · `backend/src/Modules/Catalog/Ecommerce.Catalog/Api/CategoryEndpoints.cs` (shape to copy) · `CatalogModule.cs:64–70` · `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs:30–31` · `backend/src/Ecommerce.Api/Program.cs:40–68, 107–149` · `EndpointInventoryTests.cs` · `Projections/AdminInsightsApiTests.cs:59–66, 161–171` · `Identity/IdentityHostTests.cs:172–198` (II16, preflight pattern) · `Identity/AuthorizationTests.cs:32–46, 72–78` (II10 `AssertProblem`) · `scripts/verify.sh:18–47` · `slice-plans/backlog.md` lines with "P2.56 R1" and "P2.59 R1".

No migrations, no `*.Contracts` change, no packages, no doc changes (D7 is already decided in the master plan and in decision log row "Minimal API migration").

## 1. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| S1 | One route, no group (D4 single-route clause): `app.MapGet("api/v1/admin/settings", Get).RequireAuthorization(AuthorizationPolicies.Admin)` | Exactly the controller's route and class policy (D5); nothing to share | `MapGroup("api/v1/admin")` for one route |
| S2 | Handler is a **synchronous** `private static Ok<SettingsResponse> Get([FromServices] DashboardSettings dashboard, [FromServices] IInventorySettingsReader inventory)` | Both are singletons holding config values; no I/O, so `async Task` would be noise. Deviation from D3's "async" wording only; named method + `TypedResults` kept | `async Task<Ok<…>>` with no await |
| S3 | `SettingsResponse` record moves unchanged into `AdminSettingsEndpoints.cs` (it lives in the controller file today) | Same JSON (`currency`, `lowStockThreshold`); one file per former controller | New `Contracts` type |
| S4 | D7 as written: in `Program.cs`, `RouteGroupBuilder moduleEndpoints = app.MapGroup(""); moduleEndpoints.MapModuleEndpoints();` then one static local function `AddCorsByRoutePrefix(IEndpointConventionBuilder endpoints)` applied to `moduleEndpoints` **and** `app.MapControllers()`. It reads `RawText.TrimStart('/')` (a `""` group yields `/api/v1/...`) and keeps today's two branches verbatim (auth/admin → `credentialed`, cart → `cart`, `OrdinalIgnoreCase`) | One rule for both stacks until P2.81 deletes the MVC call; modules never learn policy names | Per-endpoint `RequireCors(...)` in Admin (module would know host policy names) |
| S5 | `ModuleComposition.MapModuleEndpoints` becomes `app.MapCatalogEndpoints().MapAdminEndpoints();` `AdminModule.MapAdminEndpoints(this IEndpointRouteBuilder app)` calls `AdminSettingsEndpoints.Map(app)` and returns `app` | D1/D2 | Reflection |
| S6 | **verify.sh fix:** in `cs_var_hits`, between the `grep -vE '/(bin|obj)/'` and `xargs awk`, keep only paths that exist: `\| while IFS= read -r f; do if [ -f "$f" ]; then printf '%s\n' "$f"; fi; done`. Use `if … fi`, **not** `[ -f ] && printf` (the latter makes the loop exit 1 when the last path is missing, which trips `pipefail`) | Deleted-in-tree files stop reaching awk, so awk never aborts | gawk-only `BEGINFILE`/`ERRNO` (clever, not portable) |
| S7 | **verify.sh fail-closed:** in `step_hygiene` replace `if cs_var_hits \| grep .; then` with `local hits; hits=$(cs_var_hits) \|\| { echo "C# var scan failed"; return 1; }` and `if [ -n "$hits" ]; then echo "$hits"; echo "C# var found (explicit types only)"; return 1; fi` | Today, under `set -o pipefail`, `cs_var_hits \| grep .` takes xargs' exit 123 when awk aborts, so the `if` is false **even when hits were printed**: the step passes with a `var` on screen. A failed scan must fail the step | Leaving the scan's exit status ignored |

## 2. Files

| File | Change |
|---|---|
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSettingsController.cs` | **delete** |
| `backend/src/Modules/Admin/Ecommerce.Admin/Api/AdminSettingsEndpoints.cs` | **new**: `internal static class AdminSettingsEndpoints` with `public static void Map(IEndpointRouteBuilder app)` (S1) + `Get` (S2) + `SettingsResponse` (S3). Keep the controller's one-line summary comment |
| `backend/src/Modules/Admin/Ecommerce.Admin/AdminModule.cs` | add `MapAdminEndpoints` (S5) and its `using`s |
| `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` | chain `.MapAdminEndpoints()` (S5) |
| `backend/src/Ecommerce.Api/Program.cs` | S4: replace `app.MapModuleEndpoints();` and the inline `MapControllers().Add(...)` block with the group + local function; order unchanged (module endpoints, then controllers, then fallback) |
| `backend/tests/Ecommerce.Api.IntegrationTests/Admin/AdminSettingsContractTests.cs` | **new**, step 1 (T1, T2) |
| `scripts/verify.sh` | S6, S7 (nothing else) |
| `slice-plans/backlog.md` | mark the P2.56 R1 and P2.59 R1 `var`-scan notes resolved (append ` — **Resolved P2.62**` to each) |

Module contract: the only new public member is `AdminModule.MapAdminEndpoints`. `Ecommerce.Admin` already references `Ecommerce.Identity.Contracts` (`AuthorizationPolicies`) and `Ecommerce.Inventory.Contracts`.

## 3. Build order

1. **Pins on MVC first (test-first capture).** Write `AdminSettingsContractTests.cs` while the controller still exists; both must be **green on MVC**; quote the passing run. Do not change them afterwards.
   - **T1** `Settings_401_and_Support_403_are_problem_json` (PostgresFixture, `IdentityTestEnv` as in II10): anonymous → 401, Support token → 403; each `application/problem+json`, non-empty `traceId`, `correlationId == "id-test-1"` (copy II10's `AssertProblem` into the class; tests are not shared helpers).
   - **T2** `Settings_cors_is_credentialed_for_the_configured_origin_only` (no DB: `new ApiFactory(ApiFactory.Unreachable, settings: { ["Cors:AllowedOrigins:0"] = Allowed })`): `OPTIONS /api/v1/admin/settings` with `Origin: Allowed`, `Access-Control-Request-Method: GET`, `Access-Control-Request-Headers: authorization` → 204, single ACAO = Allowed, `Access-Control-Allow-Credentials: true`; same preflight from `https://evil.test.invalid` → no ACAO; an anonymous `GET` with `Origin: Allowed` → 401 that still carries ACAO = Allowed and ACAC `true` (the web app must be able to read the 401 to refresh).
2. **verify.sh proof, red half (before S6/S7).** Delete the controller first (the slice's own deletion), then run proof P below and quote the PASS-with-"cannot open" output: that is the bug.
3. Add `AdminSettingsEndpoints.cs`, `MapAdminEndpoints`, the composition chain (S1–S3, S5).
4. Program.cs D7 (S4). Run `EndpointInventoryTests`, II10, II11, II16, `CatalogCorsTests`, `CartCorsTests`, `AdminInsightsApiTests`, T1, T2. **If any RawText changes beyond a leading `/`** (inventory diff shows it), stop and report — no improvised fix (master plan §5).
5. verify.sh S6 + S7, then proof P green half.
6. Mutations M1–M4 (§5), each restored from a scratch copy and `cmp`-checked.
7. Backlog lines; full `bash scripts/verify.sh 2>&1 | tail -n 30`.

**Proof P (verify.sh).** With `AdminSettingsController.cs` deleted in the working tree: copy `backend/tests/Ecommerce.Payments.Tests/Infrastructure/StripePaymentGatewayTests.cs` (the last `backend/*.cs` path in `git ls-files` order, so after the deleted file) to the scratchpad; append the line `var probe = 1;`; run `bash scripts/verify.sh hygiene 2>&1 | tail -n 8`. Before S6/S7: the step passes and prints "cannot open" (quote it). After: it **fails**, printing `StripePaymentGatewayTests.cs:<n>: var probe = 1;` and no "cannot open". Restore from the scratch copy, `cmp` → identical, re-run hygiene → pass. Quote both runs and the `cmp`.

## 4. Behaviour that must not change, and its proof

| Behaviour | Proof |
|---|---|
| Route `GET api/v1/admin/settings`, policy `Admin`, auth, CORS `credentialed` | `EndpointInventoryTests` line `GET api/v1/admin/settings \| Admin \| auth \| credentialed` (unedited) |
| Every other route's CORS column (MVC admin/auth/cart still credentialed/cart; catalog `-`) | same test, whole table |
| 401 anon / 403 Customer / 403 Support / 200 Admin, body exactly `currency` + `lowStockThreshold` | `AdminInsightsApiTests` settings tests (unedited) + T1 |
| 401/403 problem shape | T1 |
| Credentialed preflight, unlisted origin refused | T2 + II16 (still on MVC admin/products) |
| Validation 400 | none: the endpoint takes no input (`AdminInsightsApiTests.cs:10`). No version/concurrency on this route |
| Existing tests | all pass **unedited**; the only test change is the new file. Any edited/deleted test is BLOCKING |

## 5. Required tests and mutations (CLAUDE.md §3)

Required: T1, T2 (new, step 1); inventory, II10, II11, II16, CatalogCors, CartCors, AdminInsights settings tests (existing). Proof P.

| # | Mutation | Must go red |
|---|---|---|
| M1 | policy `Admin` → `AdminOrSupport` | inventory T1; `Settings_without_a_token_is_401_as_a_Customer_is_403_and_as_Support_is_403`; T1 (Support 403) |
| M2 | `RequireAuthorization` removed (fallback policy applies) | inventory (policy `-`); Customer-403 assertion in AdminInsights settings test |
| M3 | `AddCorsByRoutePrefix(moduleEndpoints)` call removed | inventory (`credentialed` → `-`); T2 (no ACAC) |
| M4 | `.TrimStart('/')` removed from the local function | inventory; T2 |

Quote each red line; restore from scratch copy; `cmp`.

## 6. Reviewer checklist (Rob)

- **N+1 / round-trips / over-fetching:** the handler must not touch a DbContext, a store, or any scoped/IO service — two singletons only, one object built. Any added query is BLOCKING.
- **Policy:** exactly one `IAuthorizeData` (`Admin`) on the route; no group, so nothing stacks; no `AllowAnonymous`.
- **CORS:** one local function, two call sites; branch order and `OrdinalIgnoreCase` unchanged; no `RequireCors` in the Admin module; health/hub/fallback stay outside the `""` group.
- **Bugs:** `[FromServices]` on both parameters (else inference); `SettingsResponse` member names unchanged; no `var`; implementation types `internal`.
- **verify.sh:** only S6/S7 changed; the loop uses `if … fi`; proof P quoted red and green; `cmp` identical.

## 7. Risks

- **`MapGroup("")` RawText** gains a leading `/` on every module route: harmless for inventory/II11 (both trim) and handled by S4's `TrimStart`. Anything else → stop (step 4).
- **Minimal-endpoint preflight:** relies on ASP.NET marking `HttpMethodMetadata` as accepting preflight when CORS metadata is present; T2 proves it at runtime, not just the metadata.
- **verify.sh fail-closed (S7)** can surface a scan error that was silent before: that is intended; the build's IDE0008 still backs it up.
- Paths with spaces would still split in `xargs` (unchanged, none exist today) — out of scope.
