# Slice P2.56: Product image upload stored in Postgres

**Plan status: READY.** Unblocked by Nathaniel's stack-lock edit (`docs/architecture/02-stack-lock.md` lines 17, 23, 29, owner decision 2026-11-13): Images = Postgres `bytea` (`image_blobs`) served by the API; Spaces dropped; optional CDN via `Catalog:Images:BaseUrl`. Doc upkeep (§7) is done.

Mode: Autonomous · Risk tier: **High** (admin write endpoint, migration, public endpoint serving stored bytes) · Owner of all files: **Marco**.

## 1. Read

- `docs/admin-dashboard-feature-tracker.md`: row P2.56
- Architecture: §2, §3.4 rules 6, 7, 9, 11, 16, 17 and 18, §6.2, §8.7, §9 (the admin products row and `GET /products/{slug}`), §10.5, §11.2
- `docs/architecture/13.2-decision-log.md`: grep "Product image upload", "Dev data and images" and "Catalog snapshot for Cart"
- Quality bar §1, §3 and §4
- Code and tests: the files in §3, plus `Infrastructure/ProductWriteStore.cs` (the DbContext pattern) and `tests/render-with-query.tsx`

## 2. Decisions

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | **Postgres storage** (owner decision, §2 Images row). Decision-log row "Product image storage (P2.56)" supersedes "Product image upload (app-context 9.5 gap-fill…)" on storage | Spaces is unconfigured, so uploads always fail with 503. Nathaniel chose Postgres | Re-opening Spaces (owner said no) |
| D2 | **New table `image_blobs`**, one additive migration `AddImageBlobs`. Columns: `storage_key varchar(512)` PK (`pk_image_blobs`), `content_type varchar(32) not null`, `size_bytes integer not null`, `sha256 bytea not null` (32 bytes), `content bytea not null`, `created_at timestamptz not null` (set from `IClock`). Public schema, no FK, no change to any existing table | `product_images` (the product-to-key link) already exists. `sha256` feeds the ETag. PK on the key means one lookup per read | `catalog.product_images` (name taken; a new schema breaks rule 18). A surrogate id (extra index, no consumer). A FK to `products` (D3 writes blobs before the product is saved) |
| D3 | **Storage keys stay the link.** `product_images.storage_key` points at `image_blobs.storage_key`. Upload still attaches nothing. The admin's version-checked `PUT` attaches the key | The product write path, validation and concurrency do not change | Attaching on upload (a second, unversioned write path, already rejected in 13.2) |
| D4 | **Public read: `GET /api/v1/images/{**key}`**, `[AllowAnonymous]`, in a new `Api/ImagesController`. Keys that fail `StorageKey.IsValid` or are unknown return 404 (the same response, so neither case can be told apart). A hit returns 200 with `File(bytes, content_type, lastModified: created_at, entityTag: "\"<sha256 hex>\"")`, `Cache-Control: public, max-age=31536000, immutable` and `X-Content-Type-Options: nosniff`. A matching `If-None-Match` returns 304, which the framework handles through `File(..., entityTag)` | Keys are server-made and never overwritten, so they are immutable. `nosniff` plus a stored type that is only ever JPEG/PNG/WebP means stored bytes cannot be served as HTML or script | Published-only images (admin form and preview need unpublished ones; keys have 74 random bits). Rate limit and CDN are backlog |
| D5 | **Thin controller via `ImageFileService.GetAsync(key)`** (Application). It returns null for an invalid key without touching storage, otherwise `IImageStorage.GetAsync`. `IImageStorage` gains `Task<StoredImage?> GetAsync(string key, CancellationToken ct)`, where `StoredImage(string ContentType, byte[] Content, byte[] Sha256, DateTimeOffset CreatedAt)` is a named record | Rule 7. The single port keeps the existing upload fake usable | A separate reader interface (a second abstraction for one table) |
| D6 | **`PostgresImageStorage : IImageStorage`** in `Infrastructure`, registered **scoped** (it uses the scoped DbContext). `PutAsync` copies the stream (it is ≤ 5 MB), builds `ImageBlob.Create(key, contentType, bytes, clock.UtcNow)`, adds it and saves. That is one INSERT with no external call | The service keeps its port. No transaction spans anything external (rule 6) | Writing through `IProductWriteStore` (wrong aggregate) |
| D7 | **Upload endpoint unchanged**: same route, Admin policy, multipart `file`, limits and validation order. The response becomes `{ storageKey, url }`, where `url` comes from `IImageUrlResolver` | The editor shows a thumbnail straight away. Only the storage changes | Returning only the key and building the URL on the client (duplicates the resolver) |
| D8 | **Sniffing stays signature-only** (`ImageFormat.Detect`). The client `Content-Type` and file name are ignored. The stored `content_type` is the detected one. `ImageBlob.Create` also rejects any type outside Jpeg/Png/WebP, 0 bytes, or more than `ImageFormat.MaxBytes` (a domain guard, because the bytes are now served by us) | Defence in depth at the serving boundary | A DB check constraint (the domain guard already covers it, and the constraint has no other consumer) |
| D9 | **URL resolver**: a blank `Catalog:Images:BaseUrl` now returns the API-relative path `/api/v1/images/{escaped segments}`. A set BaseUrl behaves as today, and it is how production points at the API host or a CDN without an invented domain. `IImageUrlResolver.Resolve` returns `string` (never null). Remove null branches that existed only for a "no base URL" null. A missing image stays null | One rule for storefront, cart, orders and admin, with no host config on any port | Request-host URLs (wrong behind proxies). A `localhost:5080` dev BaseUrl (breaks compose and smoke) |
| D10 | **Frontend resolves relative URLs once**: `resolveImageUrl(url)` in `lib/config.ts`. A leading `/` gets `getApiBaseUrl()` prefixed, or null if that is unset. Absolute URLs pass through. `ProductImageView` is the app's only `<Image source>` | A single choke point | Changing every consumer |
| D11 | **Remove Spaces completely**: `Infrastructure/SpacesImageStorage.cs`, `Infrastructure/CatalogImageStorageOptions.cs`, their registration in `CatalogModule`, the `AWSSDK.S3` `PackageReference` and its `PackageVersion` in `Directory.Packages.props`, the `Catalog:Images:Storage` block in `appsettings.json`, `ImageStorageNotConfiguredException`, `AdminResult<T>.Unavailable` and its 503 mapping (the image upload is its only producer), and `tests/Ecommerce.Catalog.Tests/Infrastructure/SpacesImageStorageTests.cs` | An unregistered adapter is dead code, which is blocking in review. Removing a package needs no approval | Leaving it unregistered "for later" |
| D12 | **Picker: `expo-image-picker` (NEW npm package, rule 16).** *What:* the Expo SDK 58 image-picker module, installed with `npx expo install expo-image-picker` so the SDK-matched version is pinned. Marco records the exact version in his report, and it is added to `app.json` plugins with a `photosPermission` string. *Why:* the admin runs in the one Expo codebase on web, Android and iOS. This one API opens the photo library on native and a file input on web. *Replaces:* the typed storage-key field as the way to add an image | No picker is installed (`package.json` checked) | A hand-written `<input type=file>` in `platform.web` (leaves native admins with no upload). `expo-document-picker` (any file type, no image filter, also a new package) |
| D13 | **Platform split in `lib/platform.*` (rule 17)**: `pickImageFile(): Promise<FormData \| null>` builds a form with field `file`. On web it uses `asset.file` (a `File`). On native it uses `{ uri, name, type }` from the asset. Options: `mediaTypes: ['images']`, `allowsEditing: false`, `quality: 0.9` (makes iOS export JPEG, see Risks). Cancel returns null. Declared in `platform.d.ts`, and `platform.conformance.ts` checks both files | The file part differs by platform | Picker calls in the feature |
| D14 | **Upload transport**: `apiSend` accepts a `FormData` body. It is sent as-is, with no JSON `Content-Type` (fetch sets the boundary). A new optional `timeoutMs` is used by the upload at 60 000 | A 5 MB upload on a slow uplink can pass the 15 s default. Auth, the 401 refresh and error parsing stay in one place | A second XHR client so a percentage can be shown (duplicates auth) |
| D15 | **Progress is indeterminate.** While uploading, "Add image" reads "Uploading…" and is disabled. On success a row is added with a thumbnail (`ProductImageView`), the description field and Remove. On failure no row is added and an error line shows `userMessage(error)`, or the `file` field error from a 400. Hint: "Save the product to keep new images." The typed storage-key field is removed. A server error on `images[i].storageKey` still shows under that row | fetch has no upload progress | Pending placeholder rows with retry |
| D16 | **Create view**: `ProductForm` gains `productId?: string`. With no id, "Add image" is disabled with the hint "Save the product first to add images." | The upload route needs an existing product id (D3) | Creating a draft product implicitly |
| D17 | **Orphans are out of scope.** Uploaded-but-unsaved blobs and blobs whose key is removed from a product stay in `image_blobs`. Add this to the backlog as "image_blobs garbage collection (unreferenced and older than N days)" | Already accepted in 13.2 for Spaces | A delete on Remove (the PUT may still be unsaved or rolled back) |
| D18 | **Dev seeder unchanged.** Its keys `dev-seed/{slug}/{n}.jpg` have no blob, so they resolve to the API path and get a 404. `ProductImageView`'s image wrapper gains `bg-surface-raised`, so a failed load looks like the empty placeholder. Backlog: seed real placeholder bytes | Smallest change; the seeder still works | Generating images in the seeder |

## 3. Files

**Backend: Catalog (`backend/src/Modules/Catalog/Ecommerce.Catalog/`)**
- `Domain/ImageBlob.cs` (new, internal): `StorageKey`, `ContentType`, `SizeBytes`, `Sha256`, `Content`, `CreatedAt`. Uses `static Create(...)` with the D8 guards, which throw `ArgumentException`. SHA-256 through `System.Security.Cryptography.SHA256.HashData`.
- `Infrastructure/Persistence/ImageBlobConfiguration.cs` (new): table and columns as in D2, snake_case, `ValueGeneratedNever`.
- `Application/Admin/IImageStorage.cs`: add `GetAsync`, add the `StoredImage` record, delete the exception.
- `Application/ImageFileService.cs` (new, D5).
- `Infrastructure/PostgresImageStorage.cs` (new, D6). It reads with `AsNoTracking`, as a single row by PK.
- `Application/Admin/AdminProductImageService.cs`: drop the try/catch and Unavailable. Return the key plus the resolved URL. Fix the class comment, which currently says "Nothing is written to Postgres".
- `Application/Admin/AdminProductResults.cs`: remove `Unavailable`. `UploadedImageView` and `UploadedImageResponse` gain `Url`.
- `Api/Admin/AdminProductsController.cs`: remove the 503 arm. No other change.
- `Api/ImagesController.cs` (new, internal, `[ApiController] [AllowAnonymous] [Route("api/v1/images")]`): one `GET {**key}` action, D4.
- `Application/IImageUrlResolver.cs` and `Infrastructure/ConfiguredImageUrlResolver.cs`: D9. Callers are `CatalogBrowseService`, `ProductReader` and `AdminProductService`; drop only the dead null branches.
- `CatalogModule.cs`: remove the storage options. Register `IImageStorage → PostgresImageStorage` (scoped) and `ImageFileService` (scoped).
- Delete: `Infrastructure/SpacesImageStorage.cs`, `Infrastructure/CatalogImageStorageOptions.cs`.
- `Ecommerce.Catalog.csproj`: remove `AWSSDK.S3`. `Directory.Packages.props`: remove its `PackageVersion`.

**Backend: host and persistence**
- `backend/src/Ecommerce.Api/appsettings.json`: remove `Catalog:Images:Storage`. `BaseUrl` stays `""`.
- `backend/src/Ecommerce.Persistence/Migrations/<ts>_AddImageBlobs.cs`, plus Designer and snapshot. `Up` is only `CreateTable("image_blobs")` and `Down` only `DropTable`. No `AlterColumn` or `DropColumn` elsewhere.

**Frontend (`frontend/apps/store/`)**
- `package.json` and `app.json`: `expo-image-picker` (D12).
- `lib/platform.d.ts`, `lib/platform.web.tsx`, `lib/platform.native.tsx`: `pickImageFile` (D13).
- `lib/api-client.ts`: FormData body and `timeoutMs` (D14).
- `lib/config.ts`: `resolveImageUrl` (D10).
- `features/catalog/components/ProductImageView.tsx`: use `resolveImageUrl`, and add `bg-surface-raised` to the wrapper (D18).
- `features/admin-catalog/api.ts`: `uploadProductImage(id, form)` → `{ storageKey, url }`. `types.ts`: `UploadedImage`.
- `features/admin-catalog/hooks/useUploadProductImageMutation.ts` (new; no cache invalidation, since the product is unchanged).
- `features/admin-catalog/form-model.ts`: `ImageFormRow` gains `url: string | null`, filled from `AdminImage.url`. The request body is unchanged (no url in the request).
- `features/admin-catalog/components/ImageKeysEditor.tsx`: D15 and D16. It keeps its file name to limit churn and gets an updated doc comment.
- `features/admin-catalog/components/ProductForm.tsx`: passes `productId`. `AdminProductDetailView.tsx` passes `product.id`. The create view passes nothing.

**Docs**: done by Rob (§7).

## 4. Build order

1. **Red first (security and invariant tests):** IU3 (auth, no row written), IU4 including the disguised file, `ImageBlob` guard unit tests, IF1 (404s) and IF2 (304). Quote the red output from before the implementation exists.
2. Domain `ImageBlob`, its configuration and the migration. Drift must be clean.
3. `PostgresImageStorage`, `ImageFileService`, `ImagesController`, the service and controller edits, and the Spaces removal. Then green.
4. Resolver (D9) and the changed catalog tests.
5. Frontend: `config`, `api-client`, `platform`, the editor and views. Jest red first for the editor flow.
6. Run the full `bash scripts/verify.sh`. Write the report to `slice-plans/slice-P2.56-marco-report.md`.

## 5. Required tests

The P2.56 tracker row names no §11.2 numbered or F tests. Every test below is required. **TF** marks the tests written first.

**Integration: `AdminProductImageApiTests.cs`.** It now uses the real `PostgresImageStorage`. The `RecordingImageStorage` fake stays only for IU9.
- IU1 (changed): a PNG upload returns 200 `{storageKey, url}`. `url` is `/api/v1/images/products/{id}/{uuid}.png`. GET on that `url` returns bytes identical to the upload. `product_images` is unchanged until the `PUT`.
- IU2 (kept): JPEG and WebP sent with a wrong client type or name are stored as the detected type. Assert this with GET `Content-Type`.
- **TF** IU3 (kept and strengthened): no token returns 401. Customer and Support return 403. **Zero `image_blobs` rows** after the calls.
- **TF** IU4 (kept and extended): missing, empty, oversize (`MaxBytes + 1`) or non-image files return 400 on `file` with zero rows. Add **disguised** cases: HTML bytes sent as `image/png` with `a.png`, and SVG bytes sent as `image/svg+xml`. Both return 400 with zero rows.
- IU5, IU6 (404 for an unknown product, zero rows) and IU8: kept.
- IU7 (blank config returns 503): **deleted**. Justification: D11 removes the configuration and the 503 path, and the behavior no longer exists. List it in the report.
- IU9 (kept, with the fake): a storage exception returns 500 and does not leak the message.

**Integration: `Catalog/ImageFileApiTests.cs` (new).**
- **TF** IF1: anonymous GET of an uploaded key returns 200 with the correct `Content-Type`, `Cache-Control` containing `immutable` and `max-age=31536000`, `ETag`, and `X-Content-Type-Options: nosniff`. An unknown valid key returns 404. Invalid keys (`products/../x.png`, `products//x.png`, a key with `%5C`) return 404.
- **TF** IF2: `If-None-Match` set to the returned ETag gives 304 with an empty body.
- IF3: upload, then PUT the product with the key, then publish. The storefront `GET /products/{slug}` image url equals the upload `url` with BaseUrl blank, and GET on it returns 200.
- 401/403/400 do not apply (public, read-only); say so in the report.

**Integration: changed.** In `CatalogStockAndImageTests.Image_url_is_null_when_no_base_url_is_configured…`: with blank BaseUrl, the url is now the API path. "No images" still gives a null `primaryImage`. Rename the test and justify it with D9. List any other test changed by D9 in the report with the reason.

**Unit: `Ecommerce.Catalog.Tests`.**
- **TF** `ImageBlobTests`: rejects 0 bytes, `MaxBytes + 1` and `text/html`. Accepts exactly `MaxBytes`. Hashes with SHA-256.
- `AdminProductImageServiceTests`: remove the Unavailable test; keep sniffing and size (the M3 red).
- `ConfiguredImageUrlResolverTests`: blank base gives `/api/v1/images/products/a%20b/x.png`. A configured base is unchanged.
- `ImageFileServiceTests`: an invalid key returns null and storage is never called.
- Delete `SpacesImageStorageTests.cs` (D11).

**Jest**
- `lib/config.test.ts`: `resolveImageUrl` covers relative with a base, relative without a base (null), absolute, and null.
- `lib/api-client.test.ts`: a FormData body is passed through with no JSON `Content-Type`. `timeoutMs` overrides the default.
- `lib/platform.test.web.tsx` and `platform.test.native.tsx`: mock `expo-image-picker`. Pick returns a FormData with `file`. Cancel returns null.
- `features/admin-catalog/api.test.ts`: `uploadProductImage` POSTs multipart to `/api/v1/admin/products/{id}/images`.
- `AdminProductDetailView.test.tsx`, using `renderRouterWithQuery` with a small route map:
  - (a) Add image → picker (mocked through `@/lib/platform`) → "Uploading…" disabled → a row with a thumbnail. Save sends the new `storageKey` in the PUT.
  - (b) A 400 shows "Only JPEG, PNG or WebP images are allowed." and adds no row.
  - (c) A cancelled pick sends no request.
  - (d) Remove drops the row from the PUT.
- `AdminProductCreateView.test.tsx`: "Add image" is disabled with the hint.

**Mutations (Rob runs them, one per guard)**
- **M1 admin-only:** add `[AllowAnonymous]` to `AdminProductsController.Upload` → IU3 red.
- **M2 sniffing:** in `AdminProductImageService`, fall back to `ImageFormat.Png` when `Detect` returns null → IU4 disguised red.
- **M3 size:** delete the `command.Length > ImageFormat.MaxBytes` check → the service oversize unit test red (IU4 oversize too, if the framework limit lets `MaxBytes + 1` through; Marco reports which).

## 6. Risks

- **DB load on a public endpoint:** each cache miss reads up to 5 MB from Postgres. Immutable caching limits repeats. Backlog: rate-limit the image route, and put a CDN in front (Nathaniel's call under §2).
- **Database size and backups** grow with images. Nathaniel should know.
- **iOS HEIC:** if the picker returns HEIC, the server rejects it with 400 and the message is shown. `quality: 0.9` should make iOS export JPEG. Check it on an EAS build.
- **Shared test DB:** scope "zero rows" assertions to the test product's key prefix.

## 7. Doc upkeep (done by Rob, 2026-11-13)

- `13.2-decision-log.md`: append the row "Product image storage (P2.56, owner Nathaniel)", covering D1–D4, D9, D11 and D12. Mark the existing "Product image upload (app-context 9.5 gap-fill…)" row "Superseded by P2.56" on its storage and AWSSDK parts.
- `07-data-model.md`: add an `image_blobs` row. `09-api.md`: add public `GET /images/{key}` and the upload response `url`. `08.5-refund-fulfillment-admin.md` §8.7 bullet 1: "Images go to Postgres (`image_blobs`); products store keys". `04-system-diagram.md`, `10-cross-cutting.md` and the `13-tradeoffs.md` Spaces row: align them with Nathaniel's §2 wording.
- `slice-plans/backlog.md`: orphan GC (D17), dev-seed placeholder bytes (D18), image route rate limit or CDN.
