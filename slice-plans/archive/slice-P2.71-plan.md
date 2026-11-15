# Slice P2.71 — Auth endpoints to Minimal API (Rob, 2026-11-14)

Tracker row: `P2.71 | Auth endpoints to Minimal API; no-store headers kept | Identity`. Tier **High** (auth, tokens, cookies). Mode: Autonomous.

**Read:** `slice-plans/minimal-api-migration-plan.md` §2–§3 · this plan · `docs/architecture/09-api.md:43–50` · `03-principles-hard-rules.md:40` (rule 7) · `06-backend-modules.md:63–69` · `Identity/Ecommerce.Identity/Api/{AuthController,AuthValidators,RefreshTokenTransport}.cs` · `IdentityModule.cs` · `Inventory/Ecommerce.Inventory/Api/AdminInventoryEndpoints.cs` (pattern) · `Ecommerce.Api/Composition/ModuleComposition.cs:30–31` · tests: `Api.IntegrationTests/Identity/{AuthFlowTests,IdentityTestEnv}.cs`, `HttpContractTests.cs:56–72`, `EndpointInventoryTests.cs` (auth rows).

One controller → one endpoints file. No migrations, no `*.Contracts` change, no packages, no new service, no logging added.

---

## 1. What MVC does today (what must survive)

All routes are under `api/v1/auth`; every one is `[AllowAnonymous]` except `GET me` (bare `[Authorize]`, inventory row `(default) | auth | credentialed`).

| Route | Success | Handler failures |
|---|---|---|
| `POST register` | 201 `TokenResponse`, no `Location` | 400 FluentValidation · 409 "Account already exists" |
| `POST login` | 200 `TokenResponse` | 400 · 401 "Login failed" / "Email or password is incorrect, or the account is temporarily locked." (same for unknown email, wrong password, locked) |
| `POST refresh` (body `RefreshRequest?`, optional) | 200 `TokenResponse` | 400 (token > 100 chars) · 401 "Session expired" / "Please log in again."; in cookie mode the cookie is expired |
| `POST logout` (body `LogoutRequest?`, optional) | 204 always | 400 (token > 100 chars); cookie mode expires the cookie |
| `POST password-reset` | 202, no body | 400 |
| `POST password-reset/confirm` | 204 | 400 FluentValidation · 400 "Reset link not valid" |
| `GET me` | 200 `UserResponse` | 401 when `sub` is missing or the user row is gone |

**No-store (`[ResponseCache(NoStore = true, Location = None)]`).** That is an MVC *action* filter: it writes `Cache-Control: no-store,no-cache` and `Pragma: no-cache` before the action runs, so it is on **every response the action produces — 2xx and the handler's own 400/401/409**. It is *not* on responses that never reach the action: the auth-middleware 401 for anonymous `/me`, and `[ApiController]`'s automatic 400 for an unreadable body / missing non-nullable member (that filter runs at order −2000, before the cache filter).

**Already confirmed, unchanged by this slice:**
- Refresh rotation and reuse detection live in `RefreshService`/`RefreshRotation` (II4, II5, II6). Replay of a used token → 401 and family revoked with reason `Reuse` (`RefreshService.cs:47–50`), warning logged without the token.
- Cookie transport and flags live in `RefreshTokenTransport` (HttpOnly, `Secure` from config, SameSite=Strict, Path=`/api/v1/auth`); opt-in by `X-Auth-Transport: cookie`. The handler only decides *when* to write or expire the cookie.
- Constant-time path for a missing or locked user already exists (`LoginService.cs:31–42`, `VerifyAgainstDummy`). Nothing to add; the handler must not look a user up itself.
- **Rate limiting: absent** (no `AddRateLimiter` in `backend/src`). It is Slice 7 scope (`10-cross-cutting.md:13`, `12-process-slice-plan.md:35,46`). Not added here.
- **CORS:** `api/v1/auth*` → `credentialed` via `AddCorsByRoutePrefix` (`Program.cs:128–145`), which already wraps the module-endpoint group. II16 proves the preflight on `auth/login`; the inventory pins `credentialed` on every auth row.

## 2. Decisions

| Decision | Reason | Alternative rejected |
|---|---|---|
| **D-A** One file `Api/AuthEndpoints.cs`, `internal static class AuthEndpoints`; one group `app.MapGroup("api/v1/auth").AddEndpointFilter(NoStore)`; each route maps its leaf (`"register"`, `"login"`, `"refresh"`, `"logout"`, `"password-reset"`, `"password-reset/confirm"`, `"me"`). Six routes get `.AllowAnonymous()`, `me` gets `.RequireAuthorization()` | Master plan D1/D4/D5; per-route anonymity is exactly the controller's; no group-level policy so nothing stacks | `AllowAnonymous()` on the group with `me` overriding (harder to read: two opposite settings on one route) |
| **D-B** No-store = one endpoint filter on the group that sets `Response.Headers.CacheControl = "no-store,no-cache"` and `Pragma = "no-cache"` **before** `await next(context)` | Same scope as MVC's action filter: every handler response incl. 400/401/409 gets it; middleware 401 and binding 400 don't (they never reach the filter, as before). Master plan D12 | Header code in each handler; a header *after* `next` (same effect today, but less obvious that it can't be skipped by an early return) |
| **D-C** No-store on handler 4xx **kept**, no 13.2 row | It is today's behaviour, not a change. `09-api.md:47` is a floor ("token responses"); covering 4xx too is stricter and harmless (a 401 must not be cached either). D12 in the migration row already covers the mechanism | Narrowing to 2xx only (a behaviour change for no gain) |
| **D-D** `ValidationProblems.ToProblem(ControllerBase …)` is replaced in `AuthValidators.cs` by `internal static Dictionary<string, string[]> ToErrors(this ValidationResult)` that keeps today's dotted camelCase (`CamelCase` helper stays) and groups by key; the `Microsoft.AspNetCore.Mvc` using goes | Master plan D8: MVC helper deleted with its last caller; same `errors` keys as today | Copying Inventory's single-segment `ToErrors` (drops the dotted-path handling this module already has) |
| **D-E** Body params: `[FromBody] RegisterRequest request` (required) on register/login/password-reset/confirm; `[FromBody] RefreshRequest? request` and `[FromBody] LogoutRequest? request` (optional) on refresh/logout, then `request ??= new …(null)` as today. Services, validators and `RefreshTokenTransport` as `[FromServices]`; `HttpContext http` on refresh/logout/register/login (cookie read/write); `ClaimsPrincipal user` on `me` | D3; optional body keeps "logout is 204 every time" for a client that sends no body | `HttpRequest` + `HttpResponse` as two params (both needed together; one `HttpContext` reads plainer) |
| **D-F** Results: register `Results<Created<TokenResponse>, ValidationProblem, ProblemHttpResult>` via `TypedResults.Created((string?)null, …)` (no `Location`, as today); login/refresh `Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult>`; logout `Results<NoContent, ValidationProblem>`; password-reset `Results<Accepted, ValidationProblem>` via `TypedResults.Accepted((string?)null)`; confirm `Results<NoContent, ValidationProblem, ProblemHttpResult>`; me `Results<Ok<UserResponse>, UnauthorizedHttpResult>` | Same status, body and headers as MVC; problems go through `IProblemDetailsService` (type, traceId, correlationId kept) | `TypedResults.Json(…, statusCode: 201)` (less clear than `Created`) |
| **D-G** One private helper `static TokenResponse Issue(HttpContext http, RefreshTokenTransport transport, SessionIssued session)` — writes the cookie in cookie mode and returns `TokenResponse.From(session, cookie)`; callers wrap it in `Created`/`Ok` | The only logic shared by register, login and refresh; replaces the controller's `Issued` | Returning `IResult` from the helper (would lose the typed union) |
| **D-H** `IdentityModule.MapIdentityEndpoints(this IEndpointRouteBuilder app)` (public, the only public addition) calls `AuthEndpoints.Map(app)` and returns `app`; `ModuleComposition.MapModuleEndpoints` chain gains `.MapIdentityEndpoints()`. `ModuleAssemblies` keeps Identity until P2.81 (D14) | Master plan D1/D2; Inventory precedent | Removing Identity from `ModuleAssemblies` now (P2.81's job) |
| **D-I** Accepted difference (13.2 migration row): an unreadable body (malformed, unknown member, wrong type, empty on a required body) is a 400 problem with title "Bad Request" and no `errors`. A body that *parses* but omits `email`/`password` etc. now reaches FluentValidation (null-safe: `EmailAddress.Normalize(null)` → `""`), so `{}` is a 400 with FluentValidation messages under camelCase keys instead of MVC's implicit-`[Required]` messages | Already accepted for every slice; the validators already reject null; no service ever sees a null | Adding `[Required]`-style rules just to copy MVC's wording |

Doc upkeep done with this plan: `09-api.md:44` now says `.AllowAnonymous()` (master plan §1 assigns it to P2.71). No 13.2 row (D-C).

## 3. Files

| File | Change |
|---|---|
| `backend/src/Modules/Identity/Ecommerce.Identity/Api/AuthController.cs` | **Delete** |
| `…/Identity/Ecommerce.Identity/Api/AuthEndpoints.cs` | **Add** (D-A, D-B, D-E, D-F, D-G). Handler bodies are the controller's, line for line: validate → one service call → map. `me` keeps the comment that the id comes only from the token |
| `…/Identity/Ecommerce.Identity/Api/AuthValidators.cs` | `ValidationProblems` → `ToErrors` (D-D); drop Mvc using |
| `…/Identity/Ecommerce.Identity/IdentityModule.cs` | `MapIdentityEndpoints` (D-H) + `Microsoft.AspNetCore.Routing` using |
| `backend/src/Ecommerce.Api/Composition/ModuleComposition.cs` | `.MapIdentityEndpoints()` in the chain |
| `backend/tests/Ecommerce.Api.IntegrationTests/Identity/AuthEndpointContractTests.cs` | **Add** — pins AE1–AE6 (§5) |

`RefreshTokenTransport.cs`, `AuthRequests.cs`, `AuthResponses.cs`, all Application/Domain files: **unchanged**. Existing tests: **unchanged** (II1–II17, T4, EndpointInventory, II16).

## 4. Build order

1. **Pins first, on MVC.** Write `AuthEndpointContractTests` (§5) against the current controller and run it: every pin must be green on MVC. Record in the report any value you had to change to make it green (that is MVC's real behaviour — the pin follows MVC, not this plan). AE4 `{}` and AE6 are where MVC's answer is least certain; capture them first.
2. Add `AuthEndpoints.cs`, `ToErrors`, `MapIdentityEndpoints`, composition line; delete `AuthController.cs`.
3. Run `dotnet test backend/tests/Ecommerce.Api.IntegrationTests -c Release --filter "FullyQualifiedName~Identity|FullyQualifiedName~HttpContract|FullyQualifiedName~EndpointInventory"` → all green, unedited.
4. Mutations M1–M6 (§6), each: scratch copy → break → quote red → restore → `cmp`.
5. Full `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line.

## 5. Required tests

Existing (must pass unedited through the new endpoints): II1–II9 (`AuthFlowTests`, `PasswordResetTests`), II10–II12 (`AuthorizationTests`), II13 (no password/token in logs), II16 (credentialed CORS on `auth/login`), T4 (exact no-store on login and `/me`), `EndpointInventoryTests` (7 auth rows: method, route, policy, anon, `credentialed`).

New, `AuthEndpointContractTests` (all **test-first on MVC**, step 1). Use `IdentityTestEnv`; `SendAsync(method, url, body: null, …)` sends no content at all.

- **AE1 no-store on every handler response.** Exact `Cache-Control` `"no-store, no-cache"` and `Pragma` `"no-cache"` (T4's assertions, via one local `AssertNoStore`) on: register 201 (and no `Location`), register 400 (short password), register 409, login 401, refresh 200, refresh 401 (garbage token), logout 204, password-reset 202 (no `Location`, empty body), confirm 400 "Reset link not valid".
- **AE2 bad credentials, one answer.** Wrong password, unknown email, and a locked account (5 wrong guesses, then the *correct* password) each give 401, `application/problem+json`, type `…rfc9110#section-15.5.2`, title `"Login failed"`, detail `"Email or password is incorrect, or the account is temporarily locked."`, `correlationId` echoed, and **no `Set-Cookie`**. Exact strings, not just "equal to each other" (II2 only compares).
- **AE3 `/me` policy.** Anonymous → 401 problem with `WWW-Authenticate: Bearer` (only the JWT challenge sets it, so a lost `RequireAuthorization` is visible as behaviour, not just metadata). A valid token whose user row does not exist → 401 problem, type `…15.5.2`, plus `AssertNoStore` (handler path). Mint the token with the host's `IAccessTokenIssuer` for an unsaved `User`, or reuse II12's approach — your choice, no new helper outside the test file.
- **AE4 validation shapes.** register `{"email":"ana@example.test","password":"short","displayName":"Ana"}` → 400, `application/problem+json`, type `…15.5.1`, title `"One or more validation errors occurred."`, `errors.password` exactly `["Password must be at least 8 characters."]` (use `PasswordPolicy.MinLength` if visible), `traceId`, `correlationId`. login `{"email":"not-an-email","password":"x"}` → `errors.email` contains `"Enter a valid email address."`. login `{}` and register `{}` → 400 problem with a non-empty `errors` object and **no user row / no hashing** (key casing differs MVC vs minimal — D-I — so assert only on what both give).
- **AE5 cookie transport.** Env started with `Identity:RefreshTokens:Cookie:Secure = "true"`: cookie-mode login `Set-Cookie` contains `secure`, `httponly`, `samesite=strict`, `path=/api/v1/auth` and an `expires` attribute. Cookie-mode refresh with a garbage cookie → 401 and a `Set-Cookie` for `ecommerce_rt` with a 1970 expiry. **Cookie-mode reuse:** rotate once, replay the first cookie → 401, 1970 `Set-Cookie`, family `RevokedReason == Reuse`. Body-mode failed refresh and body-mode logout → **no** `Set-Cookie`.
- **AE6 optional bodies.** `POST logout` with no content → 204; with JSON literal `null` → 204. Cookie-mode `POST refresh` with no content and a valid cookie → 200. (If MVC answers differently in step 1, pin MVC's answer and say so in the report.)

Mutations below must each turn at least one of these red.

## 6. Mutations (Marco runs, quotes red; Rob re-runs ≤ 2)

| # | Guard | Break | Must go red |
|---|---|---|---|
| M1 | Policy | `me`: `.RequireAuthorization()` → `.AllowAnonymous()` | II11 (unexpected anonymous), `EndpointInventoryTests`, AE3 (no `WWW-Authenticate`) |
| M2 | No-store | remove `.AddEndpointFilter(NoStore)` from the group | T4, AE1, AE3 |
| M3 | Cookie flags | `RefreshTokenTransport.Options`: `Secure = settings.Secure` → `Secure = false` | AE5 (no existing test pins `Secure`) |
| M4 | Cookie cleared on failed refresh | delete the `ExpireCookie` call on the refresh failure path in `AuthEndpoints` | AE5 (garbage and reuse cases) |
| M5 | Refresh reuse | delete `family.Revoke(RefreshTokenRevocationReason.Reuse, now);` (`RefreshService.cs:49`) | II4, II5, AE5 reuse case — proves the new endpoint still drives the reuse path |
| M6 | Bad-credential message | change the login 401 `detail` to `"No account with that email."` | AE2 (II2 stays green — that is why AE2 exists) |

## 7. What the reviewer checks (Rob)

- **Queries per request:** each handler calls exactly one service method once (`RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync`, `RequestAsync`, `ConfirmAsync`, `GetAsync`); no repository, `DbContext` or user lookup in the endpoints file. `me` = one read. Validation runs before any service call.
- **No token or password in logs:** the endpoints file has no logger and no string interpolation of a request value; II13 green.
- **Constant-time missing-user path:** unchanged in `LoginService`; the login handler has no branch on user existence and maps every non-success outcome to the same 401.
- **Policies:** six `.AllowAnonymous()`, one `.RequireAuthorization()`, no policy on the group; routes built from `app` only; no `MapGet("")`.
- **No-store:** filter on the group, headers set before `next`, exact strings `"no-store,no-cache"` / `"no-cache"`.
- **Cookie:** written only in cookie mode, expired on failed refresh and on logout in cookie mode only; token never in the body in cookie mode.
- No `var`, named records only, no dead code (`ValidationProblems.ToProblem` gone, no MVC using left in Identity `Api/`), no new public type besides `MapIdentityEndpoints`.

## 8. Risks

- **Optional body under minimal APIs (AE6).** Minimal APIs answer 415 to a body with a non-JSON `Content-Type`, and treat a zero-length body as "no body". First-party clients always send `{}` (`features/auth/api.ts:26–35`), so the real risk is only third-party/no-body calls; AE6 makes the answer explicit. If minimal differs from MVC on the no-content case, that is BLOCKED for Rob (logout must stay 204 every time), not an accepted difference.
- **`me` 401 shape.** MVC's `Unauthorized()` went through `[ApiController]` client-error mapping; `TypedResults.Unauthorized()` goes through status-code pages. Both should give type `…15.5.2`; AE3 pins it.
- **Binding-400 headers.** Neither stack sets no-store on an unreadable-body 400 (the filter is not reached). Not pinned; harmless either way (no token in that response).
- **Rate limiting** is still missing (Slice 7). Open, out of scope.

## Amendment 1 — AE6 404 on a no-body logout/refresh (2026-11-14)

**Cause (reproduced in a scratch net10 app, not guessed).** Neither the handler nor body binding causes it. Minimal APIs add `IAcceptsMetadata` (`application/json`, optional) for every `[FromBody]` parameter. Routing's `AcceptsMatcherPolicy` then drops `logout`/`refresh` for a request that has **no `Content-Type`**, and the host's anonymous `MapFallback` (`Program.cs:119`, which has no accepts metadata) is the only candidate left, so the answer is 404. Without the fallback, the same request gets 200. MVC never hit this because the controller had no `[Consumes]`. Probe results with the fallback present: nullable `[FromBody] T?` → 404; `[FromBody(EmptyBodyBehavior = Allow)] T?` → **still 404**, so that is not the fix. With `Content-Type: application/json` and an empty body → 200; with a `null` literal → 200.

**Fix (D-E amended).** Keep `[FromBody] RefreshRequest? request` / `[FromBody] LogoutRequest? request` and `request ??= new …(null)` as they are. Add this to the two routes only:
```
group.MapPost("refresh", Refresh).AllowAnonymous().Accepts<RefreshRequest>(isOptional: true, "application/json", "*/*");
group.MapPost("logout", Logout).AllowAnonymous().Accepts<LogoutRequest>(isOptional: true, "application/json", "*/*");
```
Add a one-line comment above them: `// "*/*": a request with no body and no Content-Type must reach the handler, not the 404 fallback (AE6).` Probe results with the fix: no content → 200 (binds null); empty JSON → 200; `null` → 200; `{"t":"x"}` binds; malformed JSON → 400; `text/plain` body → 415 (binding still requires JSON when a body is sent). Do not change `MapFallback`, the other routes or the tests. AE6 stays as written.

**Mutations added (Marco runs, quotes red, restores, `cmp`).**
| # | Guard | Break | Must go red |
|---|---|---|---|
| M7 | No-body logout reaches the handler | remove `.Accepts<LogoutRequest>(…)` from `logout` | AE6 line 193: expected 204, actual 404 |
| M8 | No-body cookie refresh reaches the handler | remove `.Accepts<RefreshRequest>(…)` from `refresh` | AE6 cookie refresh: expected 200, actual 404 (the logout asserts pass first) |

After the fix: re-run step 3's filter and confirm all 38 tests are green, including AE6's `null`-literal and cookie-refresh asserts, which have not run yet. Then continue at step 4.
