Concerns:
- Plan D14/IU5 expected 415 for a JSON body. The host's anonymous terminal 404 fallback (Program.cs) swallows the Consumes mismatch, so the real answer is 404 (no body reaches the action, nothing stored). IU5 pins 404. Rob to decide if a 415 matters.
- D13 said the service logs product id/key/length. Architecture tests forbid Microsoft.Extensions.Logging in Catalog Application (LayerRuleTests), so the logger and log line were removed; ctor is (IAdminCatalogReadStore, IImageStorage, IClock). Add logging at the controller if wanted.
- IU8 under TestServer: 7 MB body -> 400 (not 413); pinned BadRequest. Kestrel answers 413.
- A stray idle `python` process (PID 793) was started by a mistyped command of mine; I was not allowed to kill it. It is harmless (waiting on stdin).
Bugs found: None
Bugs fixed: None
Bugs open: None
Tests changed: AdminFakes.cs - additive only (FakeAdminReadStore.ProductExistsAsync). No existing test edited; AuthorizationTests II-10/II-11 green unchanged.

Package: AWSSDK.S3 4.0.104.11 (pinned in Directory.Packages.props, referenced only from Ecommerce.Catalog.csproj).
`dotnet list ... package --vulnerable`: "The given project `Ecommerce.Catalog` has no vulnerable packages given the current sources."

Test-first red (before route/logic existed):
- ImageFormatTests: `Failed! - Failed: 1, Passed: 7 ... Each_signature_is_detected_with_its_content_type_and_extension` (Detect stub returned null).
- AdminProductImageApiTests: `Failed! - Failed: 14, Passed: 1, Total: 15`; IU1/2/3/4(all 7)/5/7/8/9 `Expected: OK/BadRequest/Unauthorized/... Actual: NotFound` (route missing). IU6 passes trivially at 404.
Green after implementation: Catalog.Tests 202 pass; IU + AuthorizationTests 20 pass.

Mutations (scratch copy in scratchpad -> break -> red -> restore -> cmp identical):
- M1 `[AllowAnonymous]` on Upload: IU3 `Expected: Unauthorized  Actual: OK`, Failed 1. Restored, cmp OK.
- M2 Detect returns Png for any >=1 byte: ImageFormatTests `Failed: 7 of 8`; IU4 wave/gif/svg/html FAIL (`Failed: 4`). Restored, cmp OK.
- M3 `Length > MaxBytes` check disabled: service unit `A_file_over_the_limit_...` FAIL; IU4 kind "toobig" FAIL. Restored, cmp OK.

Changed (slice 9.5 gap-fill):
- Directory.Packages.props; Ecommerce.Catalog.csproj; Ecommerce.Api/appsettings.json (Catalog:Images:Storage, five blank keys)
- Catalog: Domain/ImageFormat.cs (new), Domain/StorageKey.cs (ForProductImage), Application/Admin/{IImageStorage (new), AdminProductImageService (new), AdminPorts, AdminProductCommands, AdminProductResults (Unavailable, UploadedImageView)}, Infrastructure/{CatalogImageStorageOptions (new), SpacesImageStorage (new), AdminCatalogReadStore}, Api/Admin/{AdminProductsController, AdminResponses}, CatalogModule.cs
- Tests: Api.IntegrationTests/Catalog/Admin/AdminProductImageApiTests.cs (IU1-IU9); Catalog.Tests: Domain/{ImageFormatTests, StorageKeyTests}, Application/{AdminProductImageServiceTests (+FakeImageStorage), AdminFakes}, Infrastructure/{CatalogImageStorageOptionsTests, SpacesImageStorageTests}

verify.sh: VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export); build 0 warnings; jest 1103; integration 485 (before final logger removal run: full verify re-run after, all PASS).

Quality-bar §2: all boxes met - tests for each new rule, no secrets (blank placeholders only), no network in tests (IU7 proves blank config -> 503 before any client), module boundaries kept (architecture tests green), no var, IClock used, no docs edited. Frontend N/A (out of scope). Real Spaces untested (plan Risk 1; Slice 7 smoke).
