Concerns:
- Domain rule rejects "_" in a SKU (`Product.AddVariant`: A-Z, 0-9, hyphen). Plan I9 assumed SKU `A_B-1`. Safe minimum: I9 seeds `AAB-1` through the domain, then `UPDATE product_variants SET sku='A_B-1'` (column allows it). Escaping is still pinned.
- Time-of-day flake, not mine: `AdminDashboardApiTests.An_automatic_refund_leaves_revenue_unchanged...` fails when real UTC time is within 16 min of midnight (test moves the clock +16 min; IdentityTestEnv clock starts at real `UtcNow`). The first verify ran at 23:46 UTC and hit it (Expected 15.0000, Actual 0.00); passed after midnight. Suggest Rob backlog it.

Bugs found: None (the dashboard flake above is pre-existing and unrelated)
Bugs fixed: None
Bugs open: None
Tests changed: None (no existing test touched)

Changed (slice P2.8):
- New: Orders.Contracts/IOrderSearch.cs; Catalog.Contracts/ICatalogSearch.cs; Orders/Infrastructure/OrderSearch.cs; Catalog/Infrastructure/CatalogSearch.cs; Admin/Application/AdminSearchTerm.cs, AdminSearchService.cs; Admin/Api/AdminSearchController.cs
- Edited: OrdersModule.cs, CatalogModule.cs, AdminModule.cs (one DI line each)
- Tests: Admin.Tests/AdminSearchTermTests.cs (U1-U3), AdminSearchServiceTests.cs (U4-U6), IntegrationTests/Admin/AdminSearchApiTests.cs (I1-I10; I3 is a theory plus a 101/100 test)
- Tracker and docs untouched.

Red output (test-first; stubs first: TryParse always true, service empty, controller 200):
- Admin.Tests: `Failed! - Failed: 12, Passed: 0, Total: 12` (U1 x5, U2, U3 x3, U4, U5, U6; empty/permissive stubs).
- Integration: I3 x5 (`?q=`, `a`, `%20%20`, `%20a%20`, missing) "Assert.Equal() Failure: Values differ" (200 instead of 400); I3 101-chars; I6 (Support: order absent, "Collection was empty"); I4, I5, I7, I8, I9, I10 red (empty results). I1, I2 were green at the stub stage (the attributes were already in place).
- Then implemented: Admin.Tests `Passed: 23`; integration `AdminSearchApiTests` `Passed: 15` (first I9 run failed on the SKU domain rule, fixed as above).

Mutations (scratch copy, break, red, restore, `cmp` silent):
- M1 `User.IsInRole(IdentityRoles.Admin)` -> `true`: `I6_Support_never_gets_products_or_skus... [FAIL]`, Failed 1 / Passed 14. Restored, cmp OK.
- M2 `[Authorize(Policy = AdminOrSupport)]` -> `[Authorize]`: `I2_Customer_gets_403_and_staff_get_200 [FAIL]`, Failed 1 / Passed 14. Restored, cmp OK.
- M3 drop `.Replace("_", "\\_")` in CatalogSearch: `I9_LIKE_wildcards_and_backslashes_are_literal [FAIL]`, Failed 1 / Passed 14. Restored, cmp OK.

verify.sh: `VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)`; build 0 warnings, 0 errors; Admin.Tests 23; jest 1445 passed (189 suites); drift clean; tsc clean; export OK. (Run after 00:00 UTC; see the flake above.)

Quality-bar §2:
- [x] Tests for every new rule (role policy, Support filter, validation, escaping, limit/order): U1-U6, I1-I10.
- [x] Auth: 401, 403, 400 integration tests exist.
- [x] No `var`, named records, thin controller, no cross-module DbSet (Admin uses only the two Contracts; the Catalog join is inside Catalog).
- [x] Side-effect-free, no money moved; no `DateTime.UtcNow`; no secrets.
- [x] No migration, package or frontend change (drift clean).
- [x] Rate limit and index considered and recorded in the plan's Decisions (no new ones).
- N/A: tracker edit (the coordinator does it after Rob's PASS).

## Round 2 — fix for Rob's finding 1 (I9)
- Fixed: `AdminSearchApiTests.cs` I9 no longer uses raw SQL (`grep UPDATE` is empty; unused EF using removed). `_` is proven via product names "a_b Tee" and "aXb Tee": `q=a_b` returns only "a_b Tee", and `skus` is empty (AXB-1 seeded). OrderSearch: order `ORD-I9-000123`, `q=0%25` and `q=0_` both give empty `orders`. No production change.
- Mutation M3 (drop `_` escape in CatalogSearch): I9 FAIL, `Expected: "a_b Tee"  Actual: "aXb Tee"`, Failed 1 / Passed 14. Restored, `cmp` clean.
- Mutation M-new (drop `_` escape in OrderSearch): I9 FAIL, Failed 1 / Passed 14. Restored, `cmp` clean.
- Green: AdminSearchApiTests Passed 15/15.
- verify.sh changed: `VERIFY PASS (secrets hygiene build test)`.
