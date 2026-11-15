Concerns: None blocking.
- T4 pins that a 400 from `POST /auth/login` has NO Cache-Control/Pragma today (MVC validation short-circuit skips the ResponseCache filter); the 200 `GET /auth/me` has `no-store` + Pragma `no-cache`. Pinned as emitted; Rob may judge whether the 400 gap is a finding for the Auth slice.
- `verify.sh` hygiene prints an awk "cannot open" for the deleted `CatalogEndpoints.cs` but still PASSes (tooling nit).
- Plan §3 said `CatalogModule.MapCatalogEndpoints` stays; it now calls `CategoryEndpoints.Map(app); return app;` (4 lines, braces).

Bugs found: None
Bugs fixed: None
Bugs open: None

Tests changed:
- `ModuleVisibilityTests.Controllers_live_only_in_a_module_Api_namespace` renamed `Http_endpoints_are_declared_only_in_a_module_Api_namespace` (plan D10). The ControllerBase check is unchanged; added a text scan of every ModuleImpl source file via `EndpointRules.Violations`, and the vacuity guard is now `controllers + endpointFiles > 0` (strictly not weaker). No other existing test edited.

New tests: `HttpContractTests` (T2-T5), `EndpointInventoryTests` (T1), 2 self-tests in `RuleEvaluatorSelfTests`.

Red first:
- T5: `Assert.Equal() Failure` (Disallow expected, default actual) before `ConfigureHttpJsonOptions`; green after.
- D10 self-test (a) vs stub returning `[]`: `Failed ...Endpoint_rule_rejects_a_mapping_outside_the_Api_folder` ("Failed: 1, Passed: 1"); green after `EndpointRules`.

Baseline (HEAD): ArchitectureTests 25 passed. Now 27.

Characterization (T1-T4 green on today's code):
- T1 draft came from a run, then each of the 55 rows was hand-checked against controller attributes: Admin vs AdminOrSupport (customers, dashboard, dashboard/recent-orders, search, admin orders GET = AdminOrSupport; trend, export, settings, projections, inventory, products, categories, order actions, refund, returns = Admin), the anonymous set (products, categories, images, auth x6 except `me`, webhook, health, fallback), and CORS from `Program.cs:112-124` (auth and admin = credentialed, cart = cart, rest none). Categories row reads `GET api/v1/categories | - | anon | -`. Hub rows were stable between runs, so no filtering.
- T2: `type` `https://tools.ietf.org/html/rfc9110#section-15.5.1`, title `One or more validation errors occurred.`, `errors.page`, traceId, correlationId.
- T3: `...#section-15.5.5`, title `Product not found`.

Implement: `UseStrictJson` local function in `Program.cs` (used by `AddJsonOptions` and `ConfigureHttpJsonOptions`); `EndpointRules` in `Rules.cs`; `CatalogEndpoints.cs` replaced by `CategoryEndpoints.cs` (single route, no group, no WithName).

Mutations (each restored from a scratch copy, `cmp` identical):
1. `.AllowAnonymous()` -> `.RequireAuthorization("Admin")`: red T1, II11, `CatalogDetailTests.Categories_are_listed_with_parent_slug_ordered_by_name` (Failed: 3).
2. `// app.MapGet("x", () => 1);` appended to `Application/CatalogBrowseService.cs`: red `Http_endpoints_are_declared_only_in_a_module_Api_namespace`: "Application\CatalogBrowseService.cs:68 maps an HTTP endpoint outside the module's Api folder".
3. Removed the `Disallow` line: red T5, IA1 `AdminProductApiTests`, and CheckoutTests T5 (Failed: 3).

Changed:
- backend/src/Ecommerce.Api/Program.cs
- backend/src/Modules/Catalog/Ecommerce.Catalog/CatalogModule.cs
- backend/src/Modules/Catalog/Ecommerce.Catalog/Api/CatalogEndpoints.cs (deleted), Api/CategoryEndpoints.cs (new)
- backend/tests/Ecommerce.ArchitectureTests/{Rules,ModuleVisibilityTests,RuleEvaluatorSelfTests}.cs
- backend/tests/Ecommerce.Api.IntegrationTests/{EndpointInventoryTests,HttpContractTests}.cs
Slice P2.59. Not committed.

verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`; build 0 errors; Arch 27, Inventory 28 ..., jest 2111 passed. After that run I removed a duplicate `using` (CS0105 warning in the new inventory test) and re-ran the touched Api integration tests (12 passed) and ArchitectureTests (27 passed).

Quality-bar §2: tests for new behaviour (checked) · no secrets (checked) · module boundaries and `internal` types (checked; exports-only-module-class test green unedited) · no migrations/packages (N/A) · no frontend (N/A) · no existing test weakened (checked, see Tests changed) · verify.sh green (checked).

## Round 2
- T4 renamed `T4_token_and_me_responses_carry_the_exact_no_store_headers`; login-400 request and its two checks dropped; now asserts a successful login has exactly `Cache-Control: no-store, no-cache` and `Pragma: no-cache`, plus the unchanged `/me` checks (HttpContractTests.cs:56-72).
- Mutation: removed `[ResponseCache]` from AuthController.cs:13. T4 red: `Assert.Equal() Failure: Expected "no-store, no-cache", Actual null` (Failed 1, Passed 3). Restored from scratch copy; `cmp` identical.
- HttpContract filter: Passed 4/4. `verify.sh changed`: VERIFY PASS (secrets hygiene build test).
