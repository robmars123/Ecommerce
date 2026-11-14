Concerns: None
Bugs found/fixed/open: None
Tests changed: none edited. Added 2 in ImageFileApiTests (the earlier red theory split per Amendment 1): Unknown_image_key_is_a_404_problem [Fact], Empty_image_key_is_a_400_validation_problem [Theory, 2 rows]. IF1-IF3, inventory, II11 untouched.

MVC dump (both /api/v1/images and /api/v1/images/): 400, application/problem+json; charset=utf-8, no Cache-Control,
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"key":["The key field is required."]},"traceId":"00-...","correlationId":"image-test-1"}
Matches the plan's expected list exactly. Pin green on MVC (6/6 in ImageFileApiTests) before conversion. Temporary dump line removed.

Step 4 red (IsNullOrEmpty branch removed): both rows fail "Expected: BadRequest / Actual: NotFound" (service IsValid(null)). Restored from scratch copy, cmp OK.

Changed: Catalog/Api/ImageEndpoints.cs (new), Catalog/Api/ImagesController.cs (deleted), Catalog/CatalogModule.cs (+1 line), tests/.../Catalog/ImageFileApiTests.cs (+2 tests). Tracker not marked, not committed.
Post-convert filter (ImageFileApiTests|EndpointInventory|Authorization): 12 passed.
verify.sh: VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)
Quality-bar §2: all met; Low risk, no mutations.
