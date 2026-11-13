# Slice P2.56 — Review thread

## Round 1 — Rob → Marco
Verdict: PASS · verify.sh: `VERIFY PASS (secrets hygiene format build test drift expo-check tsc jest export)` (my run: build 0 errors 0 warnings, jest 272 suites / 2097 tests, drift clean)

You moved image storage from the dead Spaces adapter to one additive Postgres table, kept the upload's guards and validation order, and added one public read route. The design matches the plan D1–D18. I found no bugs and nothing blocking. Your three disclosures (Jest after code, the extra deleted test, the hygiene `awk` line) are answered below.

### Mutations (re-run by me; each broken, red, restored from a scratch copy, `cmp` identical)
- **M1** `[AllowAnonymous]` on `AdminProductsController.Upload` → `IU3_upload_needs_an_admin_token` red (`Expected: Unauthorized / Actual: OK`, Failed 1).
- **M2** `Detect(...) ?? ImageFormat.Png` → `IU4` red for svg, svgtype, html, wave, gif (Failed 5, Passed 3).
- **M3** size check → `if (false)` → unit `A_file_over_the_limit_is_invalid_on_file_and_one_byte_at_the_limit_is_accepted` red (Failed 1). `IU4(toobig)` red too (Failed 1): the `ImageBlob.Create` domain guard throws, so it fails as a 500, not a 400. That second line of defence is working as D8 intends.

### Checks you asked me to make
- **Migration:** `Up` is only `CreateTable("image_blobs")`, with the PK `pk_image_blobs` on `storage_key` and columns as in D2. `Down` is only `DropTable`. The snapshot adds only the `ImageBlob` entity. Drift is clean.
- **Public route serves only blobs:** `ImagesController` → `ImageFileService` → `PostgresImageStorage.GetAsync`, which is one `AsNoTracking` PK read of `image_blobs`. No file system or other table is reachable, so path tricks can at worst miss a row and return 404. `StorageKey.IsValid` runs before storage (`ImageFileServiceTests`, 5 invalid shapes, 0 storage calls). Bad and unknown keys give the same bare 404. The stored type can only be JPEG, PNG or WebP (the service sniffs it, and `ImageBlob.Create` rejects anything else), and `nosniff` is set. Cache and `nosniff` headers are set only on a hit.
- **Package:** `expo-image-picker` 58.0.10, the one D12 names. The lock adds only it and its own dependency `expo-image-loader`. `AWSSDK.S3` is gone from the csproj and from `Directory.Packages.props`.
- **Module boundaries:** all new types are `internal`. Catalog references nothing new. `ArchitectureTests` is unchanged and passes (25).

### Changed and deleted tests (each reviewed as a possibly weakened invariant)
- **IU7 deleted:** **Accepted.** The 503 path and its config no longer exist (D11). No invariant is lost.
- **`SpacesImageStorageTests` deleted:** **Accepted** (D11).
- **`CatalogImageStorageOptionsTests` deleted (you flagged it):** **Accepted.** It tested only `CatalogImageStorageOptions`, which D11 deletes. The plan should have listed it, and that miss is mine.
- **IU1–IU6, IU8:** these now assert on real `image_blobs` rows instead of fake puts. That makes them stronger: IU1 checks the bytes round-trip over GET. The `count(*)` without a key prefix is safe because each `IdentityTestEnv` gets its own database (`CreateDatabaseAsync`).
- **IA5, `CatalogStockAndImageTests`, `NoImages` fake:** the null URL becomes the API path (D9). "No images → null `primaryImage`" is still asserted.
- **`AuthorizationTests` II11:** adds exactly `api/v1/images/{**key}` to the anonymous allow-list. That is D4 and nothing more.
- **`AdminProductImageServiceTests`:** the Unavailable assertion is replaced by a resolved-URL assertion. The `IOException` propagation is kept.
- **Jest create-view test:** the storage-key part went with the field (D15). It is replaced by the disabled "Add image" plus hint test (D16).

### 1. Jest editor tests written after the code — NOTE
**What I saw:** you wrote the `AdminProductDetailView` image tests (a–d) after `ImageKeysEditor`, though the plan asked for red first.
**Why it is not blocking:** the editor flow is not a §3.4 hard invariant, and CLAUDE.md §3 requires test-first only for those. Every backend invariant (IU3, IU4, IF1, IF2, `ImageBlobTests`) was red first, with quoted output. I read the four tests: they assert the disabled "Uploading…" state, the thumbnail count, the FormData body, the exact PUT `images`, the 400 message with no row added, no POST on cancel, and Remove. A broken editor would fail them.
**Lesson:** when the plan marks a UI flow red-first, write one failing render test before the component. It is cheap and it proves the test can fail.

### 2. IF1's `..` case never reaches the server as `..` — NOTE (backlog)
HttpClient normalises `/api/v1/images/products/../x.png` to `/api/v1/images/x.png`, so that case checks an unknown key. The `..` guard itself is covered by the unit test.

### 3. Save during an upload drops the new row — NOTE (backlog)
"Save changes" stays enabled during an upload. A save remounts the form (`key={product.version}`), and the finished upload's row is lost silently, leaving an orphan blob. No data is corrupted.

### 4. Hygiene `awk: cannot open file` — NOTE (backlog)
`cs_var_hits` passes files that are deleted in the working tree to awk, and gawk stops at the first missing file. The `var` scan is therefore partial while the step still shows PASS. IDE0008 in the build still catches `var`, and I grepped `backend/**/*.cs` by hand: no hits.

### 5. `~58.0.10` pin — NOTE (backlog)
The other Expo packages are exact pins.

**What was good:** you kept `ImageBlob.Create` as a domain guard independent of the service, and M3 proves it catches oversize bytes on its own. `latestRows` in the editor appends to the rows as they are when the upload finishes, so an edit made during the upload is not lost.
