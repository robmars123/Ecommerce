# Slice P2.82 — Body endpoints answer a missing Content-Type like MVC did (Rob, 2026-11-14)

Tracker row: `P2.82 | Body endpoints answer a missing Content-Type like MVC did (415/400, not 404) | Catalog, Inventory, Identity, API`. Tier **High**: the bug lets a request skip the route's authorization check and land on the anonymous fallback. Mode: Autonomous. The slice runs after P2.71 is committed.

**Read:** this plan · `slice-plans/minimal-api-migration-plan.md` §2.1 (D2, D7) · `backend/src/Ecommerce.Api/Program.cs:105–145` (module group, `AddCorsByRoutePrefix`, `MapFallback`) · `Identity/Ecommerce.Identity/Api/AuthEndpoints.cs:16–27` · `Api.IntegrationTests/EndpointInventoryTests.cs` (how it enumerates endpoints) · `Identity/IdentityTestEnv.cs` (`SendAsync`, `Client`) · `Inventory/AdminInventoryContractTests.cs` (admin env and token helper).

No migrations, no packages, no `*.Contracts` change, no module code change except removing two lines from `AuthEndpoints.cs`.

---

## 1. The bug (confirmed by Rob in a scratch net10 probe, 2026-11-14)

Probe: one MVC `[ApiController]` action, one minimal `[FromBody] Req` route, an anonymous `MapFallback` (the host's shape), and curl.

| Request | MVC | Minimal today | Minimal + fix (§2) |
|---|---|---|---|
| no body, no `Content-Type` | **415** | 404 | 400 |
| JSON body, no `Content-Type` | 415 | 404 | 415 |
| JSON body, `text/plain` | 415 | 404 | 415 |
| JSON body, `application/json` | 200 | 200 | 200 |
| `application/json`, empty body | 400 | 400 | 400 |
| optional body (`T?`): no body, no CT | handler runs | 404 | handler runs |

**Cause.** For every `[FromBody]` parameter, Minimal APIs add `IAcceptsMetadata(application/json)`. `AcceptsMatcherPolicy` treats an endpoint without that metadata as `*/*`, and the host fallback (`Program.cs:119`) has none. So when a request's content type is missing or not JSON, the fallback is the only candidate left. Because the fallback is `.AllowAnonymous()`, the answer is 404 without reaching the route's auth check, so an anonymous admin POST without a Content-Type should get 404 where MVC gave 401 (reasoned; Marco measured only authenticated calls; CT2's red run in step 1 confirms it). MVC actions have no accepts metadata, so they were never filtered.

Marco's report reasoned that MVC gave 400 for no body. The probe shows **415**: `BodyModelBinder` finds no formatter, so it answers 415 when the body is required.

**Affected today:** admin `POST categories`, `PUT categories/{slug}`, `POST products`, `PUT products/{id}`, `POST inventory/adjust` (P2.68–P2.70), and auth `register`, `login`, `password-reset`, `password-reset/confirm` (P2.71). Auth `refresh` and `logout` are already fixed per route (P2.71 Amendment 1). Once the Cart, Orders, Returns, Refund and Checkout body routes (P2.72–P2.79) convert, they would have the same bug.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **D-A** One host convention. In `Program.cs`, next to `AddCorsByRoutePrefix`, add a static local function `AcceptAnyContentType(IEndpointConventionBuilder endpoints)`. It calls `endpoints.Finally(b => …)`: when the endpoint's last `IAcceptsMetadata` exists, it adds `new AcceptsMetadata([.. accepts.ContentTypes, "*/*"], accepts.RequestType, accepts.IsOptional)`. Call it once, on `moduleEndpoints`, with a one-line comment: `// Routing must not reject on Content-Type: body binding answers 415/400 as MVC did (P2.82).` | One place, and every route mapped through `MapModuleEndpoints` inherits it, including P2.72–P2.79 with no per-route code. `Finally` runs after the metadata that Minimal APIs infer, which the probe confirmed. Binding still demands JSON when a body is present, so a non-JSON body is still 415 | Per-route `.Accepts(…, "*/*")` (about 20 routes, and one forgotten route brings the bug back silently). Changing the fallback: giving it accepts metadata makes unknown routes with a non-JSON body hit the synthesized 415 endpoint, which has no `AllowAnonymous`, so they get 401. Removing the fallback brings back 401 for every unknown route |
| **D-B** Call `Finally` through `IEndpointConventionBuilder`. `RouteGroupBuilder` exposes it only as a default interface method, so `moduleEndpoints.Finally(…)` does not compile. Passing the group to a function typed `IEndpointConventionBuilder` handles this, the same way `AddCorsByRoutePrefix` already does | Plain; no cast at the call site | `((IEndpointConventionBuilder)moduleEndpoints).Finally` inline |
| **D-C** Remove `.Accepts<…>(…)` and their comment from `refresh` and `logout` (`AuthEndpoints.cs:21–23`); the routes become `.AllowAnonymous()` like the others | D-A makes them redundant. Dead code is blocking | Keeping both (two mechanisms for one rule) |
| **D-D** Accepted difference: **no body and no Content-Type on a required-body route → 400** (MVC: 415). Rob updates the 13.2 "Minimal API migration" row (line 113) when P2.82 passes | Both are client errors before any service call. Matching 415 needs custom code for one malformed call shape that no client sends | A custom filter that rewrites 400→415 |
| **D-E** `AcceptsMetadata` is public in `Microsoft.AspNetCore.Http.Metadata` (net10); the probe compiled it in a `Microsoft.NET.Sdk.Web` project. No package | — | — |
| **D-F** Webhook (P2.80) is unaffected: it has no body parameter (D13), so it has no accepts metadata. MVC controllers still mapped by `MapControllers` are not in `moduleEndpoints` and are untouched | — | — |

## 3. Files

| File | Change |
|---|---|
| `backend/src/Ecommerce.Api/Program.cs` | `AcceptAnyContentType(moduleEndpoints);` after `AddCorsByRoutePrefix(moduleEndpoints);` plus the local function (D-A, D-B). About 10 lines |
| `…/Identity/Ecommerce.Identity/Api/AuthEndpoints.cs` | D-C: lines 21–23 become two plain `.AllowAnonymous()` routes |
| `backend/tests/Ecommerce.Api.IntegrationTests/ContentTypeContractTests.cs` | **Add**: CT1–CT4 (§5) |
| `slice-plans/minimal-api-migration-plan.md` | Rob adds D15 at review: "body routes need no `.Accepts`; the host convention covers them (P2.82)" |

Existing tests are **unchanged**, AE6 included: after D-C, AE6 is what guards the optional-body case through the convention.

## 4. Build order

1. **Write CT1–CT4 first** against today's code, run them, and quote the red (404 everywhere, CT4 listing the routes without `*/*`).
2. **MVC-first capture (Identity).** The fix must match MVC, so capture MVC's answers with the auth controller put back for one run:
   - Copy to scratch: `AuthEndpoints.cs`, `AuthValidators.cs`, `IdentityModule.cs`, `Composition/ModuleComposition.cs`.
   - Write each file's pre-P2.71 version with read-only `git -c safe.directory='*' show eeb657b:<path> > <path>`, for `Api/AuthController.cs`, `Api/AuthValidators.cs`, `IdentityModule.cs` and `ModuleComposition.cs`. Then delete `AuthEndpoints.cs`. Do not use `checkout` or `restore`.
   - Run `--filter "FullyQualifiedName~ContentTypeContractTests.CT1|FullyQualifiedName~ContentTypeContractTests.CT3"`. Expected on MVC: everything green **except** the no-body case, which is red with `Actual: UnsupportedMediaType` (D-D). Quote it, and record the 415 `type` and media type MVC returned.
   - Restore the four files from scratch, delete `AuthController.cs`, and `cmp` each one.
   - Admin routes run on the same MVC framework, so the Identity capture plus the probe stand for them. Do not restore the admin controllers; their helpers were deleted in P2.68–P2.70.
3. Add D-A/D-B and apply D-C.
4. `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~ContentType|FullyQualifiedName~Identity|FullyQualifiedName~Admin|FullyQualifiedName~Inventory|FullyQualifiedName~Catalog|FullyQualifiedName~EndpointInventory|FullyQualifiedName~HttpContract"`: all green, existing tests unedited.
5. Mutations P1–P2 (§6).
6. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line.

## 5. Required tests: `ContentTypeContractTests` (all test-first)

Build "no Content-Type" requests on the env's `HttpClient` with `new HttpRequestMessage { Content = new StringContent(json) }`, then `request.Content.Headers.ContentType = null`. Send "no body" with `Content = null`. No new helper outside the file.

- **CT1 Auth required-body routes** (`register`, `login`, `password-reset`, `password-reset/confirm`, as a `[Theory]`): a valid JSON body with no CT → **415**, `application/problem+json`; the same body as `text/plain` → **415**; no body and no CT → **400** problem (D-D). In each case, assert no user row was created and the hashing counter did not move.
- **CT2 Admin required-body routes** (`POST admin/categories`, `PUT admin/categories/{slug}`, `POST admin/products`, `PUT admin/products/{id}`, `POST admin/inventory/adjust`), signed in as Admin: the same three cases (415 / 415 / 400) with nothing written. **Anonymous**, JSON body, no CT → **401** (today 404, so the auth check is skipped: this is the security half of the row).
- **CT3 Optional bodies:** `logout` with a JSON body and no CT → **415** (MVC 415). The no-body cases stay in AE6.
- **CT4 Structural, for future slices:** from `EndpointDataSource`, every `RouteEndpoint` whose last `IAcceptsMetadata` has content types must include `"*/*"`, and that set must have **at least 11** endpoints (non-vacuous: 9 required + refresh + logout). When P2.72–P2.79 convert, their body routes fall under this test automatically.

If MVC's 415 `type` (step 2) equals the minimal one, pin it in CT1. If they differ, pin the status and media type only, and list the difference in the report.

## 6. Mutations (Marco runs, quotes red, restores from scratch, `cmp`; Rob re-runs both)

| # | Guard | Break | Must go red |
|---|---|---|---|
| P1 | The convention exists | delete the `AcceptAnyContentType(moduleEndpoints);` call | CT1, CT2 (anonymous case gives 404, not 401), CT3, CT4, AE6 (`Actual: NotFound`) |
| P2 | Required bodies are covered, not just optional ones | inside the convention, skip non-optional: `if (… is { IsOptional: true } accepts)` | CT1, CT2, CT4; AE6 stays green, which proves CT1/CT2 cover what AE6 does not |

## 7. What the reviewer checks

- One convention, at the host, applied once, to `moduleEndpoints` only; no `.Accepts` left anywhere in `backend/src/Modules`. Check with `grep -rn "\.Accepts" backend/src/Modules` → none.
- `MapFallback` is unchanged.
- 415/400 are answered **after** authorization (CT2 anonymous → 401).
- No `var`, no new public type, no dead code.

## 8. Risks

- **Convention order.** If a later .NET changes when inferred metadata is added, `Finally` is still last by contract. CT4 fails loudly if it ever stops applying.
- **Swagger/OpenAPI:** none in the host (no `AddOpenApi`), so the added `*/*` changes no published contract.
- **P2.72–P2.79 plans** must not add per-route `.Accepts`. Rob's D15 in the master plan makes that explicit.
