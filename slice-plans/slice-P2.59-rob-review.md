# Slice P2.59 — Review thread

## Round 1 — Rob → Marco
Verdict: BLOCKED · verify.sh: not re-run by me (BLOCKED round; CLAUDE.md §5). Your run: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`. My targeted runs: ArchitectureTests 27, the 3 mutations below.

The code is small and right: `CategoryEndpoints` is one plain route, `UseStrictJson` removes the copy, the arch rule holds through the migration, and all three mutations go red. One test pins the wrong contract and has to change now, because P2.59 is the only slice allowed to edit it.

### 1. T4 pins that a login 400 has no `Cache-Control` — BLOCKING   (`backend/tests/Ecommerce.Api.IntegrationTests/HttpContractTests.cs:62-68`)
**What I saw:** you assert `Assert.Null(bad.Response.Headers.CacheControl)` and `Assert.Empty(...Pragma)` on the 400 from `POST /auth/login`. That absence is an accident of MVC: `ModelStateInvalidFilter` short-circuits before `[ResponseCache]` runs. The test name also says "auth responses carry the exact no-store headers", which this half contradicts. (The plan asked for "exact values as emitted today". That was my mistake, and you followed it correctly.)
**Is the 400 a bug?** No. 09-api.md:47 requires `no-store` on token responses only. A 400 carries no token, and RFC 9111 does not cache a 400 heuristically. So it does not go to the backlog either.
**Why it matters:** in P2.71 the natural way to keep "no-store headers kept" is one filter or header on the auth group. That also covers the 400, so T4 goes red. The migration plan (line 48) forbids editing tests after P2.59. P2.71 would then have to strip a protective header from 400s just to satisfy a test, or break the rule.
**What I want:** pin the contract, not the accident. Drop the 400 request and its two assertions. Pin the token response instead: a successful `POST /auth/login` must have exactly `Cache-Control: no-store, no-cache` and `Pragma: no-cache`, plus the `/me` assertions you already have. Rename the test to `T4_token_and_me_responses_carry_the_exact_no_store_headers`. Side note: `PostAsync(url, "{}")` serialises the C# string, so the body was the JSON string `"{}"`, not an empty object. That goes away with the change.
**How I'll check:** read the diff; run `--filter FullyQualifiedName~HttpContractTests` green. Then a mutation: remove `[ResponseCache(...)]` from `AuthController` and confirm T4 goes red.
**Lesson:** a characterisation test pins the documented contract and today's values for it. It never pins the absence of a safety header.

### 2. Arch test re-implements "is under Api/" — NOTE   (`ModuleVisibilityTests.cs:79`)
`"Api" + Path.DirectorySeparatorChar` in the test and the `Replace(...)/"Api/"` check in `EndpointRules.Violations` express one rule in two ways. → backlog.

### 3. Rule 7 wording during the transition — NOTE   (`03-principles-hard-rules.md:40`)
"An HTTP endpoint handler (Minimal API)" reads as if controllers are no longer covered, but 22 slices still have controllers. → backlog: "(Minimal API handler, or a controller until P2.81)".

### 4. verify.sh hygiene awk "cannot open" on a deleted file — NOTE
This is a tooling nit (it still passes). → backlog.

### Checks done (no finding)
- **Changed test** `Controllers_live_only_…` → `Http_endpoints_are_declared_only_in_a_module_Api_namespace`: the ControllerBase loop is byte-identical. The vacuity guard `controllers + endpointFiles > 0` is weaker only if controller reflection silently returned 0 while endpoint files exist. That is the end state D10 requires at P2.81, and `endpointFiles` comes from the same scan that produces violations, so the scan is never vacuous. **Accepted.**
- **Inventory:** I counted every `[Http*]` and `[Route]` against the rows. That gives 55 controller rows, plus categories, health ×2, hub ×2 and fallback, for 61 lines. The policies match the attributes (the class `AdminOrSupport` on dashboard, customers, search and the `ReturnsController` admin-orders GETs; `Admin` elsewhere). Anonymous is exactly the auth ×6 (not `me`), products, images, categories, webhook, health and fallback. CORS matches the `Program.cs:113-124` prefixes. No trailing-slash RawText.
- Public surface: only `CatalogModule.MapCatalogEndpoints`. `CategoryEndpoints` is internal. The usings are all used. `WithName` and the group are gone. `GetCategoriesAsync` makes the same single call as before.
- `ConfigureHttpJsonOptions` also feeds the ProblemDetails writer: no enums and no deserialisation there, so the output is unchanged.
- Docs: 06 visibility paragraph, the 13.2 row (grep: no duplicate), CLAUDE.md §2/§5 lines, tracker G rows. The P2.58 archive matches HEAD (`cmp` ×3 identical). **Before commit:** set the tracker row P2.59 to `Done` (P2.58's commit did the same).

### Mutations (each restored from a scratch copy, `cmp` identical)
1. `.AllowAnonymous()` → `.RequireAuthorization("Admin")`: red T1 (`Actual: GET api/v1/categories | Admin | auth | -`) and II11 (`missing anonymous: api/v1/categories`). Failed: 2.
2. `// app.MapGet("x", () => 1);` appended to `Application/CatalogBrowseService.cs`: red `Http_endpoints_are_declared_only_in_a_module_Api_namespace` ("Application\CatalogBrowseService.cs:68 maps an HTTP endpoint outside the module's Api folder"). Failed: 1, Passed: 26.
3. `Disallow` line removed: red T5 and `IA1_duplicates_unknown_category_extra_members_and_bad_prices_are_rejected`. Failed: 2.

**What was good:** You noticed the 400 gap and raised it instead of hiding it. The inventory row format makes a failing diff readable in one line (mutation 1 shows exactly the column that moved).

## Round 2 — Rob → Marco
Verdict: PASS · verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)

### 1. T4 asserts exact headers — Confirmed
I read `HttpContractTests.cs:57-71`. T4 is now `T4_token_and_me_responses_carry_the_exact_no_store_headers`. It asserts `no-store, no-cache` and `Pragma: no-cache` on login and `/me`, and the 400 check is gone. That matches what I asked for.

Mutation re-run (1 of 2): I deleted `[ResponseCache]` at `AuthController.cs:13`. T4 went red with `Expected: "no-store, no-cache" Actual: null` (the other 3 contract tests passed). I restored the file and `cmp` shows it identical to the backup.

Full `verify.sh` passed, with 27 architecture tests among the test totals. Tracker row P2.59 is set to Done.

**Lesson:** a header pin has to compare the exact value. A weaker check lets a removed attribute go unnoticed.
