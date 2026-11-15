# Slice P2.82 — Marco report

Concerns:
1. Plan gap: the convention as planned (every `IAcceptsMetadata`) also hit the multipart image upload route, failing existing IP6, IP7, IU5 (3 red). Those tests deliberately pin "multipart only, wrong Content-Type is 404". Safe minimum: the convention applies only to routes that accept `application/json` (one extra condition plus a comment in `Program.cs`). CT4 is scoped the same way (JSON routes). Rob to decide whether the upload route should also get 415 (that would mean editing IP6/7, IU5, which I did not do).
2. P1 and CT2's anonymous 401 case: CT2 fails on its admin case first, so the anonymous case is not separately visible as red. Anonymous admin POST with no Content-Type measured separately with a throwaway probe test (deleted): **404 today** (as Rob reasoned). Green after the fix means 401 (CT2 asserts it).
3. D-D observed: no body and no Content-Type gives 400 on minimal, 415 on MVC.

Bugs found: Program.cs host fallback skips auth on JSON body routes (404 instead of 401/415).
Bugs fixed: `AcceptAnyContentType` in Program.cs; CT1-CT4 now pass.
Bugs open: None

MVC capture (pre-P2.71 AuthController put back via `git show eeb657b:<path>`, then restored): CT1 and CT3 green for the no-CT and text/plain cases, so MVC returns 415, `application/problem+json`, type `https://tools.ietf.org/html/rfc9110#section-15.5.16`, identical to minimal, pinned in CT1. Red only on the no-body case: `Expected: BadRequest / Actual: UnsupportedMediaType` (4 of 5; CT3 green). Restored: `git status` shows only the new test file, so the four files equal HEAD (also copied from scratch).

Red on today's code (CT1-CT4): 10 failed, 1 passed (CT3 already green: logout has per-route `*/*`). Example: `Expected: UnsupportedMediaType / Actual: NotFound`; CT4 lists the routes without `*/*`.
Green after fix: 348 passed (filter from plan step 4).

Mutations (restored from scratch copy, `cmp` OK):
- P1 delete `AcceptAnyContentType(moduleEndpoints);`: 12 failed (CT1 x4, CT2 x5, CT3, CT4, AE6 `Actual: NotFound`).
- P2 skip non-optional (`accepts is { IsOptional: true }`): 10 failed (CT1 x4, CT2 x5, CT4); AE6 and CT3 stay green.

Tests changed: none existing edited. Added `backend/tests/Ecommerce.Api.IntegrationTests/ContentTypeContractTests.cs` (CT1-CT4).
Changed: `backend/src/Ecommerce.Api/Program.cs` (using, call, local function), `Identity/Ecommerce.Identity/Api/AuthEndpoints.cs` (`.Accepts` removed from refresh/logout), the new test. `grep "\.Accepts" backend/src/Modules`: none.
verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; build 0 warnings; jest 2111 passed.

Quality-bar §2: all boxes met; no migration, package or Contracts change; no var; no new public type; no tracker update, no commit (as instructed).
