Concerns: AE6 differs after conversion. STOPPED per instruction; no workaround. Mutations M1-M6 and full verify.sh NOT run.
Bugs found: AuthEndpoints.cs Logout - AE6 (POST /api/v1/auth/logout with no content: MVC 204, minimal API 404).
Bugs fixed: None
Bugs open: the AE6 difference above (BLOCKED for Rob per plan section 8).
Tests changed: None edited. Added Identity/AuthEndpointContractTests.cs (AE1-AE6).

MVC capture (scratch test, deleted; output in scratchpad mvc-capture.txt):
- login {} 400 problem 15.5.1, errors {"Email":["The Email field is required."],"Password":[...]} (PascalCase), no no-store header.
- register {} 400, errors Email/Password/DisplayName PascalCase, no no-store.
- login bad email 400, errors.email ["Enter a valid email address."], no-store present.
- logout no body 204, logout `null` 204, both no-store.
- cookie refresh no body + cookie 200, Set-Cookie ecommerce_rt, refreshToken null in body.
- /me anonymous 401 problem+json 15.5.2, WWW-Authenticate Bearer, no no-store.
AE1-AE6 were all green on MVC first run (6 passed) before conversion; no pin value changed.

After conversion: 37 of 38 pass (Identity, HttpContract, EndpointInventory, AE1-AE5 green). AE6 line 193 fails:
  Expected: NoContent  Actual: NotFound   (logout, SendAsync(Post, Logout, null))
Not yet checked: whether the `null`-literal logout and the no-body cookie refresh pass (the test stops at the first assert).

Changed: AuthEndpoints.cs (new), AuthController.cs (deleted with rm), AuthValidators.cs (ToErrors), IdentityModule.cs (MapIdentityEndpoints), ModuleComposition.cs, AuthEndpointContractTests.cs (new).
verify.sh: not run.
Quality-bar section 2: not complete (blocked).

## Round 2
Concerns: None for P2.71. Earlier slices have the same no-Content-Type 404 (below), out of scope.
Bugs found/open: None. Bugs fixed: AE6 404 - `.Accepts<...>(isOptional: true, "application/json", "*/*")` + one-line comment on refresh and logout (AuthEndpoints.cs:21-23); AE6 now green.
Tests changed: None edited.
Step 3: 38/38 green (Identity|HttpContract|EndpointInventory), incl. AE6 null-literal and cookie refresh.
Mutations (each: scratch copy, break, red, restore, cmp ok):
- M1 me AllowAnonymous: red T1 inventory (me | anon), II11, AE3.
- M2 no NoStore filter: red II1, T4, AE1, AE3 (Expected "no-store, no-cache", Actual null).
- M3 Secure=false: red AE5.
- M4 drop ExpireCookie on refresh failure: red AE5.
- M5 drop Revoke(Reuse): red II4 (Expected Unauthorized, Actual OK), II5, AE5 (Expected Reuse, Actual null).
- M6 login detail changed: red AE2 (Actual "No account with that email.").
- M7 remove logout Accepts: red AE6 (Expected NoContent, Actual NotFound).
- M8 remove refresh Accepts: red AE6 (Expected OK, Actual NotFound).
Earlier slices: no-Content-Type. RAN against current Minimal API (temp probe test, deleted): admin POST categories, PUT categories/{slug}, POST products, PUT products/{id}, POST inventory/adjust, authenticated Admin, body with Content-Type removed -> 404 on all five; with no body at all -> also 404. MVC comparison NOT run, reasoned only: MVC had no accepts matcher, so the request reached the action; a non-empty body with no Content-Type gives 415 and an empty body gives 400 ([ApiController] required body). Difference: 404 now vs 415/400 before. Files untouched. Fix idea for Rob: a host-level change or per-route Accepts("*/*"); not applied.
verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export); jest 2111 passed.
Quality-bar §2: all met; no uncommitted secrets, no test weakened, no git writes.
