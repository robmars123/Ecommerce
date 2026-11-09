**Read:** app-context row 9.5 (line 104) · arch §2 (02-stack-lock.md: Images row + "Open decisions") · §3.4 (rules: thin controllers, no external call inside a tx, `IClock`) · §6.2 (module layering, 06-backend-modules.md line 63) · §8.7 first bullet (08.5-refund-fulfillment-admin.md line 22) · §9 row `GET/POST /admin/products …` (09-api.md line 22) · §13.2 row "Product image upload" (grep, don't read the log) · quality-bar §1, §3, §4.
Code to read before editing: `Catalog/Api/Admin/AdminProductsController.cs`, `AdminResponses.cs`, `Catalog/Application/Admin/{AdminPorts,AdminProductCommands,AdminProductResults}.cs`, `AdminCategoryService.cs` (constructor + `IClock` use only), `Catalog/Domain/StorageKey.cs`, `Catalog/Infrastructure/{CatalogImageOptions,AdminCatalogReadStore}.cs`, `CatalogModule.cs`, `Ecommerce.Catalog.csproj`, root `Directory.Packages.props`, `Ecommerce.Api/appsettings.json`; tests `Ecommerce.Catalog.Tests/Application/AdminFakes.cs`, `Api.IntegrationTests/{ApiFactory.cs, Identity/IdentityTestEnv.cs (StartAsync, SendAsync), Catalog/Admin/AdminSamples.cs, Checkout/CheckoutEnv.cs (the `RemoveAll<IPaymentGateway>` swap only)}`.

# Gap-fill 9.5 — Product image upload (backend API)

Deliverable: app-context **9.5** — `POST /api/v1/admin/products/{id}/images` stores one image in S3-compatible storage and returns its key. Builder: **Marco**. Mode: Autonomous. Owner authorization: Nathaniel lifted the §9 hold ("complete it and any dependency"). Bucket, endpoint and CDN domain stay **open decisions**: config placeholders only, blank in committed config, no secrets, no network call in any test.

Out of scope: any frontend change. **Follow-up (not planned here):** the 9.6 admin product edit screen could add a picker that calls this endpoint and puts the returned key into the `images` list; today it still takes typed keys and keeps working unchanged. Also out: orphan-object cleanup, image resizing, deleting objects from storage.

## Risk tier — High

This adds an **auth surface** (a new Admin-only write endpoint) and takes **untrusted bytes that get served publicly from the CDN**. Full review + the §3 mutations in §4 (one per security guard: auth, file signature, size). No money, stock, payment or state transition.

## 1. Decisions (§13.2 row "Product image upload", §9 row 22 and §2 open-decisions note updated — by Rob, done)

| # | Decision | Reason | Alternative rejected |
|---|---|---|---|
| D1 | **Server-side upload.** `POST /admin/products/{id}/images`, `multipart/form-data`, one part named `file` → **200** `{ "storageKey": "…" }` | The server can check the bytes really are an image before anything is public; the key only exists once the object does; no bucket CORS (an open decision); matches §4 diagram (API → Spaces) | Presigned PUT URL (can't inspect bytes, key handed out before the object exists, needs CORS on an undecided bucket) |
| D2 | Upload **attaches nothing**: no `product_images` row, no version bump. The admin sends the key in the next `PUT /admin/products/{id}` (version-checked, `StorageKey.IsValid`) | One write path for a product's image list, already guarded by optimistic concurrency (1b D12); row 9.5 says "returns a key only" | Appending to `product_images` here (second, unversioned writer; alt text and sort order would need their own rules) |
| D3 | 404 when the product id does not exist, checked **before** storage is touched | No objects under a key prefix for a product that doesn't exist | Uploading regardless |
| D4 | Allowed formats decided **only by the file signature**: JPEG `FF D8 FF`; PNG `89 50 4E 47 0D 0A 1A 0A`; WebP `"RIFF"` at 0 and `"WEBP"` at 8. The client's `Content-Type` and file name are ignored. Stored `Content-Type` and extension come from the detected format (`image/jpeg`/`jpg`, `image/png`/`png`, `image/webp`/`webp`). SVG, GIF, HEIC and everything else → 400 field `file`, "Only JPEG, PNG or WebP images are allowed." | The object is served publicly; a client-chosen type could publish HTML or script-capable SVG from our CDN | Trusting `Content-Type`/extension; allowing SVG |
| D5 | Size: **1 byte – 5 MB** (`5 * 1024 * 1024`). Empty → 400 `file` "Choose an image file."; over → 400 `file` "Images must be 5 MB or smaller." The service check (on `IFormFile.Length`, counted by the server) is the guard. Framework backstops on the action: `[RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + 1)]` and `[RequestSizeLimit(MaxBytes + 64 * 1024)]`, so a just-over file reaches the service (clean 400) and a far-over body is cut off by the framework | Product photos fit easily; bounds memory/disk per request | No limit; relying only on the framework limit (status code differs by host, message is not ours) |
| D6 | Key = `products/{productId:D}/{imageId:D}.{ext}`, all lower case, `imageId = Guid.CreateVersion7(clock.UtcNow)`. Built by a new `StorageKey.ForProductImage(Guid, Guid, string)`; must satisfy `StorageKey.IsValid` | Server-made: no client path, no traversal, no PII from file names; unique, so objects are never overwritten; same id style as the rest of Catalog (`IClock`, rule) | Client file name in the key; content hash (re-upload of the same file would be fine either way; not needed) |
| D7 | Port `IImageStorage` (Catalog Application): `Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)`. Adapter `SpacesImageStorage` (Infrastructure, singleton) on **`AWSSDK.S3`**. Tests swap the port for a fake | The only network edge; one consumer (D1) and one fake | Calling the SDK from the service (untestable without network) |
| D8 | **New package `AWSSDK.S3`** (pulls `AWSSDK.Core`). Why: S3-compatible client for DO Spaces, the client DigitalOcean documents for .NET, maintained by AWS, handles SigV4 signing. Replaces: nothing (no storage client exists). Pin the latest stable **4.x** at adoption in `Directory.Packages.props`; reference it from `Ecommerce.Catalog.csproj` only. Run `dotnet list backend/src/Modules/Catalog/Ecommerce.Catalog package --vulnerable` and quote it | Stack lock §2 names S3-compatible storage; a storage client is required to meet 9.5 | Hand-written SigV4 over `HttpClient` (security code we'd own); `Minio` client (less common, same role) |
| D9 | Config `Catalog:Images:Storage:{ServiceUrl, Region, Bucket, AccessKeyId, SecretAccessKey}`, all `""` in `appsettings.json`. `CatalogImageStorageOptions.IsValid`: all blank **or** all set with `ServiceUrl` an absolute **https** URL; `ValidateOnStart` (partial config fails startup, like `Catalog:Images:BaseUrl`). Secrets come only from environment at deploy (Slice 7) | Open decisions stay blank (§2); a half-configured store must not start silently | Failing startup when blank (would break dev, CI and tests) |
| D10 | **Unconfigured → 503.** `SpacesImageStorage.PutAsync` with all-blank options throws `ImageStorageNotConfiguredException` (Application, next to the port) **before creating any client**. The service catches it → new `AdminResult<T>.Unavailable(string Message)` → controller → 503 Problem Details, title "Image storage is not configured" | Honest status; proves no network call happens in dev/CI | 500; registering no implementation (DI failure at request time) |
| D11 | Adapter request: `PutObjectRequest { BucketName, Key, InputStream, ContentType, CannedACL = PublicRead, UseChunkEncoding = false }` + header `Cache-Control: public, max-age=31536000, immutable`. Client: `AmazonS3Config { ServiceURL, AuthenticationRegion = Region, RequestChecksumCalculation = WHEN_REQUIRED, ResponseChecksumValidation = WHEN_REQUIRED }`, `BasicAWSCredentials`. Build the request in an `internal static BuildRequest(...)` so it is unit-tested without network. Client created lazily once (`Lazy<IAmazonS3>`), disposed with the singleton | Spaces objects are private by default and the CDN serves public ones; keys never change so the cache can be immutable; SDK 4 default checksum/chunked signing is not supported by every S3-compatible provider | Bucket policy (open decision); default checksum settings |
| D12 | Storage failures (network, auth) are **not caught**: they surface as the global 500 Problem Details. The admin retries; each retry gets a new key. No transaction is open during the call (rule: no external call inside a tx — nothing is written to Postgres at all) | A failed upload has nothing to roll back; orphans are accepted (§13.2) | Retry loop in the adapter |
| D13 | New `AdminProductImageService` (not `AdminProductService`, already ~300 lines). Product existence via a new `IAdminCatalogReadStore.ProductExistsAsync(Guid, ct)` (`AsNoTracking().AnyAsync`) | Small, one-purpose service; cheap existence check | Loading the tracked aggregate with variants and images |
| D14 | Binding: `Upload(Guid id, IFormFile file, CancellationToken ct)` with `[Consumes("multipart/form-data")]`. `file` is non-nullable, so a missing part is the framework's automatic 400 keyed `file` (no code). A JSON body → 415. Other form fields are ignored (no JSON strict binding on multipart; nothing reads them) | Zero hand-written binding checks | A FluentValidation validator over `IFormFile` (needs to read the stream anyway; the service does it once) |

No migration, no new project reference, no frontend, no change to create/update.

## 2. Files and contracts

### Package
- `Directory.Packages.props` — `<PackageVersion Include="AWSSDK.S3" Version="4.x.y" />` (latest stable at adoption; report the exact version).
- `backend/src/Modules/Catalog/Ecommerce.Catalog/Ecommerce.Catalog.csproj` — `<PackageReference Include="AWSSDK.S3" />`.

### Catalog domain
- **New** `Domain/ImageFormat.cs` — `internal sealed record ImageFormat(string ContentType, string Extension)` with three static instances, `public const int HeaderLength = 12;`, `public const long MaxBytes = 5 * 1024 * 1024;`, `static ImageFormat? Detect(ReadOnlySpan<byte> header)` (D4; fewer than the needed bytes → null).
- `Domain/StorageKey.cs` — add `ForProductImage(Guid productId, Guid imageId, string extension)` (D6).

### Catalog application
- **New** `Application/Admin/IImageStorage.cs` — the port (D7) + `internal sealed class ImageStorageNotConfiguredException : Exception`.
- `AdminProductCommands.cs` — `internal sealed record UploadImageCommand(Stream Content, long Length);`
- `AdminProductResults.cs` — `internal sealed record UploadedImageView(string StorageKey);` and `AdminResult<T>.Unavailable(string Message)`.
- `AdminPorts.cs` — `IAdminCatalogReadStore.ProductExistsAsync(Guid id, CancellationToken ct)`.
- **New** `Application/Admin/AdminProductImageService.cs` — ctor `(IAdminCatalogReadStore, IImageStorage, IClock, ILogger<AdminProductImageService>)`. `UploadAsync(Guid productId, UploadImageCommand cmd, ct)`, in this order:
  1. `!await ProductExistsAsync` → `NotFound`.
  2. `Length == 0` → `Invalid("file", empty msg)`; `Length > ImageFormat.MaxBytes` → `Invalid("file", size msg)`.
  3. Read up to `HeaderLength` bytes (`ReadAtLeastAsync(..., throwOnEndOfStream: false)`); `Detect` null → `Invalid("file", format msg)`.
  4. Rewind (`Content.Position = 0`; the form-file stream is seekable — if `!CanSeek`, throw `InvalidOperationException`, it's a programming error).
  5. Key per D6; `PutAsync(key, content, format.ContentType, ct)`; catch `ImageStorageNotConfiguredException` → `Unavailable(…)`.
  6. Log Information `product id, key, length` (never config values); `Ok(new UploadedImageView(key))`.

### Catalog infrastructure
- **New** `Infrastructure/CatalogImageStorageOptions.cs` — five string props, `IsConfigured`, static `IsValid` (D9).
- **New** `Infrastructure/SpacesImageStorage.cs` — `internal sealed class SpacesImageStorage(IOptions<CatalogImageStorageOptions>) : IImageStorage, IDisposable` (D10, D11).
- `Infrastructure/AdminCatalogReadStore.cs` — implement `ProductExistsAsync`.

### Catalog API
- `Api/Admin/AdminResponses.cs` — `internal sealed record UploadedImageResponse(string StorageKey);` + mapping.
- `Api/Admin/AdminProductsController.cs` — inject `AdminProductImageService`; action `[HttpPost("{id:guid}/images")]` with the D5 and D14 attributes, body = `Map(await images.UploadAsync(id, new UploadImageCommand(file.OpenReadStream(), file.Length), ct), v => Ok(v.ToResponse()))` (dispose the stream with `await using`). `Map` gains `Unavailable u => Problem(statusCode: 503, title: "Image storage is not configured", detail: u.Message)`. Stays in this controller so it inherits the class-level `[Authorize(Policy = AuthorizationPolicies.Admin)]` — **do not add any attribute that changes auth on the action**.
- `CatalogModule.cs` — `AddOptions<CatalogImageStorageOptions>().Configure<IConfiguration>(…"Catalog:Images:Storage:*"…).Validate(IsValid, "Catalog:Images:Storage must be all blank or fully set with an https ServiceUrl.").ValidateOnStart()`; `AddSingleton<IImageStorage, SpacesImageStorage>()`; `AddScoped<AdminProductImageService>()`.

### Host config
- `backend/src/Ecommerce.Api/appsettings.json` — `Catalog:Images:Storage` with the five keys `""`. Nothing in `appsettings.Development.json`, `compose.yaml` or `.env*`.

### Docs (Rob, done — Marco does not edit docs)
- `09-api.md` row 22, `02-stack-lock.md` open decisions, `13.2-decision-log.md` row "Product image upload". App-context 9.5 cell: coordinator after PASS.

## 3. Build order

1. **Test-first (quote red):** integration IU3 (auth) and IU4 (400 cases) against the not-yet-existing route → red (404/405). Then add domain `ImageFormat` + `StorageKey.ForProductImage` with their unit tests red → green.
2. Port, exception, `AdminProductImageService` + unit tests (fake storage, fake read store) red → green.
3. Controller action + `Unavailable` arm + module wiring; IU1–IU8 green.
4. Package + options + `SpacesImageStorage` + its unit tests; IU7 (real adapter, blank config → 503) green.
5. Mutations M1–M3 (§4), each: scratch copy → break → quote red → restore from the copy → `cmp`.
6. `bash scripts/verify.sh 2>&1 | tail -n 30`; quote the `VERIFY` line and the `--vulnerable` output in `slice-9.5-gapfill-marco-report.md`.

## 4. Required tests

No §11.2 numbered or F test is named for 9.5 by §12.3. All below are required for PASS.

### Integration — **new** `backend/tests/Ecommerce.Api.IntegrationTests/Catalog/Admin/AdminProductImageApiTests.cs`
Start with `IdentityTestEnv.StartAsync(postgres, configure: s => { s.RemoveAll<IImageStorage>(); s.AddSingleton<IImageStorage>(fake); })` except IU7. `RecordingImageStorage` (in the test file) records `(key, contentType, bytes)` and can be told to throw. Send multipart with `MultipartFormDataContent` + `HttpRequestMessage` on `env.Client` (add one private helper in the file; `IdentityTestEnv.SendAsync` is JSON-only). Test bytes: tiny arrays starting with each signature, padded — no real image files committed.
- **IU1 happy PNG** — create product; upload → 200, `storageKey` matches `^products/{id}/[0-9a-f-]{36}\.png$`; fake got exactly one put with that key, `image/png`, and **byte-identical** content (proves the rewind). `GET /admin/products/{id}` still shows no images (D2). Then `PUT` the product with that key in `images` → 200 and the image is listed.
- **IU2 JPEG and WebP** — `.jpg`/`image/jpeg`, `.webp`/`image/webp`; client sends a wrong `Content-Type` (`text/plain`) and file name `x.exe` — ignored.
- **IU3 ★ auth** — no token → 401; Customer → 403; Support → 403. Fake received nothing in each.
- **IU4 ★ 400** (each: key `file`, fake received nothing): no `file` part; empty file; `MaxBytes + 1` bytes with a PNG header; SVG text (`<svg …>`) sent as `image/png` named `a.png`; HTML; `RIFF….WAVE` (RIFF but not WebP); GIF `GIF89a`.
- **IU5 415** — JSON body → 415; fake nothing.
- **IU6 404** — random product id with a valid PNG → 404; fake nothing.
- **IU7 unconfigured** — no fake (real `SpacesImageStorage`, blank config from `appsettings.json`) → 503 Problem Details within a short timeout; proves no network path.
- **IU8 far over limit** — 7 MB body → the framework backstop rejects it; fake nothing. Observe the status once (400 or 413 under `TestServer`), then assert that exact code and quote it in the report.
- **IU9 storage failure** — fake throws `IOException` → 500 Problem Details; the body does not contain the exception message.

### Unit — `backend/tests/Ecommerce.Catalog.Tests`
- `Domain/ImageFormatTests.cs` — each signature detected with the right content type/extension; null for: empty, 2 bytes, `RIFF` + `WAVE`, GIF, `<svg`, `<?xml`, `%PDF`, PNG signature with one byte changed.
- `Domain/StorageKeyTests.cs` (or extend the existing file) — `ForProductImage` format, lower case, and `StorageKey.IsValid` is true for it.
- `Application/AdminProductImageServiceTests.cs` (extend `AdminFakes.cs`: `FakeAdminReadStore.ProductExistsAsync` + a `FakeImageStorage`) — unknown product → `NotFound`, no put; empty/oversize/bad signature → `Invalid` keyed `file`, no put; ok → one put, key uses `Guid.CreateVersion7(clock.UtcNow)` time prefix (assert the key's id parses to a v7 Guid and the product id prefix), content byte-identical; `ImageStorageNotConfiguredException` → `Unavailable`; other exception propagates.
- `Infrastructure/CatalogImageStorageOptionsTests.cs` — all blank valid; any one set alone invalid; all set with `http://` invalid; all set with `https://` valid.
- `Infrastructure/SpacesImageStorageTests.cs` — blank options → `PutAsync` throws `ImageStorageNotConfiguredException`; `BuildRequest` sets bucket, key, content type, `PublicRead`, `UseChunkEncoding = false`, the cache header.

### Mutations (High tier — one per security guard; Marco runs and quotes red; Rob re-runs)
- **M1 auth** — add `[AllowAnonymous]` to the `Upload` action → IU3 must go red (401 expectation fails).
- **M2 signature** — make `Detect` return PNG for any input with ≥ 1 byte → IU4 (SVG/HTML/RIFF-WAVE/GIF cases) and `ImageFormatTests` must go red.
- **M3 size** — delete the `Length > MaxBytes` check in the service → IU4 `MaxBytes + 1` case and the service unit test must go red.

Existing tests: none may change except the additive members in `AdminFakes.cs`. `AuthorizationTests` II-10 is **not** edited; II-11 must stay green unchanged (the new action declares auth through its class).

## 5. Risks

- **Real Spaces is never exercised in CI.** D11's settings (checksums, chunk encoding, ACL) are untestable without a bucket; the first deploy smoke (Slice 7) must upload one image and fetch it via the CDN URL. Listed for the Slice 7 release gate, not this slice.
- **Polyglot files.** A signature check proves the prefix, not that the whole file is a clean image; we don't decode or re-encode (no image library in the stack). Mitigation: only raster types, content type set by us, never SVG. Accepted for v1.
- **Orphans.** Uploaded-but-never-saved objects stay in the bucket (§13.2). Backlog item for a sweep.
- **SDK version drift.** `AWSSDK.S3` ships often; pin exactly and keep the checksum settings explicit so a default change can't silently break Spaces.
- **Framework limit behaviour differs by host** (Kestrel 413 vs `TestServer`); IU8 pins what the test host does, and the service check (IU4, M3) is the real guard.
- **Shared `AdminResult<T>`** gains `Unavailable`; the products `Map` ends in `_ => throw` (gets the new arm); `AdminCategoriesController.ToActionResult` ends in `default: 404`. Leave it unchanged — `AdminCategoryService` never returns `Unavailable` (no storage use), so the default can't be reached by it.
