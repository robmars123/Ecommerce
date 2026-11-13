# Slice P2.56 - Marco report (product image upload stored in Postgres)

Concerns:
- Frontend Jest tests were written after the editor, not red-first (the plan asks for the editor flow red first). The hard invariants (backend) were red first.
- Red-first backend: the integration tests IU3/IU4/IF1/IF2 failed before any implementation, 17 of 18 red (IU3: `42P01 relation "image_blobs" does not exist`; IF1/IF2/IU1: upload returned 503 from the unconfigured Spaces store). `ImageBlobTests` and `ImageFileServiceTests` were red by compile error (`CS0246 StoredImage` / `ImageBlob` not found).
- `expo-image-picker` (D12) installed with `npx expo install`: version `~58.0.10` (installed 58.0.10). `app.json` plugin added with a `photosPermission` string.
- Hygiene prints an `awk: cannot open file` line for the deleted files; step still PASS.
- Migration `AddImageBlobs` has only CreateTable/DropTable. Drift clean.
- M3: `ImageBlob.Create` (domain guard) also rejects MaxBytes+1, so IU4 "toobig" goes red as a 500 when the service check is removed; the service unit test is the precise red.
- iOS HEIC check needs an EAS build; not done.

Bugs found: None
Bugs fixed: None
Bugs open: None

Mutations (each: break, red, restore from scratch copy, `cmp`):
- M1 `[AllowAnonymous]` on `AdminProductsController.Upload`: `IU3_upload_needs_an_admin_token` RED (`Expected: Unauthorized / Actual: OK`; Failed 1). Restored, `cmp` identical.
- M2 `Detect(...) ?? ImageFormat.Png` in `AdminProductImageService`: integration `IU4` RED for svg, svgtype, html, wave, gif (Failed 5, Passed 3); unit `A_non_image_signature_is_invalid_on_file` RED x2. Restored, `cmp` identical.
- M3 size check replaced by `if (false)`: unit `A_file_over_the_limit_is_invalid_on_file_and_one_byte_at_the_limit_is_accepted` RED; integration `IU4 (kind: "toobig")` RED (Failed 1). Restored, `cmp` identical.

Tests changed:
- `AdminProductImageApiTests.cs`: rewritten per plan. Now uses the real Postgres storage (fake kept for IU9). IU1 asserts `{storageKey,url}` and GET bytes. IU2 asserts the stored type through GET. IU3 and IU4 assert zero `image_blobs` rows. IU4 gains `svgtype` (SVG sent as image/svg+xml). IU5, IU6, IU8 count rows instead of fake puts. The fake gains `GetAsync` (interface change).
- **Deleted** IU7 (blank config gives 503): D11 removed the configuration and the 503 path.
- `CatalogStockAndImageTests`: renamed to `Image_url_is_the_api_image_path_when_no_base_url_is_configured_and_primary_image_is_null_without_images`; blank BaseUrl now gives `/api/v1/images/dev/a.jpg` (D9). "No images" still null.
- `AdminProductApiTests.IA5` renamed `..._with_api_image_urls_...`: the preview image url is now the API path, not null (D9).
- `CatalogSchemaTests`: expected table list gains `image_blobs`.
- `AuthorizationTests` (II11): the expected-anonymous list gains `api/v1/images/{**key}` (the new public route, D4).
- `AdminProductImageServiceTests`: removed the Unavailable test; replaced with "storage failure propagates and the result carries the resolved url"; service gets the resolver. Fake storage gains `GetAsync`.
- `AdminFakes.NoImages` and `CatalogBrowseServiceTests.FakeImages`: `Resolve` returns `string` (interface change). `NoImages` now returns `"url:" + key` (it was null); no assertion relied on null.
- **Deleted** `SpacesImageStorageTests.cs` (D11, plan) and `CatalogImageStorageOptionsTests.cs` (not named in D11, but it tests the deleted `CatalogImageStorageOptions`; flag for Rob).
- Jest: `AdminProductCreateView.test.tsx` "can add and remove variant and image rows" lost its image part (storage-key field is gone, D15); replaced by a disabled "Add image" + hint test (D16).

New tests: `ImageBlobTests` (4), `ImageFileServiceTests`, `ConfiguredImageUrlResolverTests` (2), `ImageFileApiTests` IF1-IF3 (public, read-only: 401/403/400 do not apply; bad and unknown keys are the same 404), Jest: `resolveImageUrl` (3), api-client FormData and `timeoutMs` (2), `uploadProductImage`, picker web (2) and native (2), `AdminProductDetailView` image flow a-d (4), create view hint.

Changed (slice P2.56):
Backend Catalog: `Domain/ImageBlob.cs`, `Infrastructure/Persistence/ImageBlobConfiguration.cs`, `Infrastructure/PostgresImageStorage.cs`, `Application/ImageFileService.cs`, `Api/ImagesController.cs` (new); `Application/Admin/{IImageStorage,AdminProductImageService,AdminProductResults}.cs`, `Api/Admin/{AdminProductsController,AdminResponses}.cs`, `Application/IImageUrlResolver.cs`, `Infrastructure/{ConfiguredImageUrlResolver,CatalogImageOptions}.cs`, `CatalogModule.cs`, `Ecommerce.Catalog.csproj`; deleted `SpacesImageStorage.cs`, `CatalogImageStorageOptions.cs`. `Directory.Packages.props` (AWSSDK.S3 removed), `appsettings.json` (Storage block removed). Migration `20261113202138_AddImageBlobs` (+Designer, snapshot).
Frontend: `package.json`, `package-lock.json`, `app.json`; `lib/{platform.d.ts,platform.web.tsx,platform.native.tsx,api-client.ts,config.ts}`; `features/catalog/components/ProductImageView.tsx`; `features/admin-catalog/{api.ts,types.ts,form-model.ts}`, `hooks/useUploadProductImageMutation.ts` (new), `components/{ImageKeysEditor,ProductForm,AdminProductDetailView}.tsx`; matching tests.
Doc/backlog: done by Rob (section 7); untouched by me.

verify.sh:
`VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)`
build 0 Error(s) 0 Warning(s); test all projects green (integration 554); drift "No changes have been made to the model since the last migration."; jest 272 suites, 2097 tests; tsc clean.

Quality-bar §2: all boxes met: tests for each new behaviour; invariants red-first on the backend; no secrets; no `var`/anonymous types; module boundaries kept (Catalog references only SharedKernel, Persistence, Contracts); IClock used for `created_at`; no external call inside any transaction (single INSERT); one new npm package named in the plan; AWSSDK.S3 removed. Unchecked: Jest editor flow not red-first (see Concerns).
