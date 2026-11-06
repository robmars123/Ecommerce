# Slice 1b — Identity + Admin Product Create: Plan

Mode: Plan · Author: Rob · Date: 2026-11-03 · Fix-round cap: 3 (CLAUDE.md)

**Verdict: READY**

---

## Summary

Slice 1b adds authentication, roles, and the admin side of the catalog. It finishes Slice 1.

- **New Identity module**, `Ecommerce.Identity` plus `Ecommerce.Identity.Contracts`, in the Module + Contracts layout (§6.2). It covers:
  - register, login, refresh rotation with family reuse detection, logout, password reset and confirm, and `me`
  - lockout
  - roles (Customer, Support, Admin)
  - JWT bearer authentication, and an authorization fallback policy (every endpoint is authenticated unless it opts out).
- **Catalog admin:** create, edit (with optimistic concurrency), publish, unpublish, preview, list, and create category. All of it is Admin-only.
- **Frontend:**
  - real Log in, Register, and Reset password screens
  - a session store
  - the bearer token and single-flight refresh in `lib/api-client.ts` (F2, bearer part)
  - the refresh token in SecureStore on native and in an httpOnly cookie on web
  - an admin route guard (F7)
  - the admin product list, create, edit, and preview screens.
- **Carry-ins:**
  - domain length checks for variant name, size, and colour
  - the 2 CS8602 warnings in `CatalogDevSeeder.cs`
  - two `api-client.ts` notes from the 1a review
  - architecture-test coverage for the new module.

**Done when:** an Admin logs in and creates a product, which is inactive and has no stock. The product stays invisible to the public until it is published. F2 (bearer part) and F7 pass.

Slice 1b has no §11.2 test (tests 1–19 belong to Slices 2–6b). Its invariants are security invariants. They are **test-first** here (§6 marks them **TF**):
- refresh rotation and reuse detection
- concurrent refresh
- lockout under parallel attempts
- the role matrix on admin endpoints
- "every endpoint declares its auth"
- single-use reset tokens
- mass assignment
- the stale admin edit.

**Slice order check:** 1a passed (`slice-1a-rob-review.md`, re-review PASS). The Module + Contracts refactor passed (`module-projects-rob-review.md`, re-review PASS). 1b is next, and Slice 2 can't start until 1b passes (§12.3).

---

## 1. Scope

**In:**
- Identity module: domain, application, infrastructure, api.
- One migration.
- JWT authentication, authorization policies, the fallback policy.
- Auth endpoints (§3.6), `GET /auth/me`, admin bootstrap.
- Catalog admin endpoints (§4.4), domain write methods, and the carry-in checks.
- SharedKernel `IUnitOfWork` plus persistence exception translation (D11).
- Strict JSON request binding (D13).
- CORS for credentials, POST and PUT (D6).
- Request-log `UserId` enrichment (D24).
- Frontend: `lib/platform.ts`, `features/auth/**`, `features/admin-catalog/**`, the api-client changes, the admin layout guard, real auth screens, the admin product screens, and an AccountBar on Home.
- Doc and README updates (§9).

**Out.** Any of these in the diff blocks the review (quality bar §1):
- Inventory, stock, audit_log, the "Out of Stock" admin filter (Slice 2).
- Cart, merge (Slice 3).
- Outbox, real email (Slices 4 and 6).
- Rate limiting, Redis, caching (Slice 7).
- An image upload endpoint or object-storage client (open decision).
- Support/role management UI or endpoints.
- Customer profile editing, change password.
- Email verification.
- Social login.
- `IUserReader` / `ICurrentUser` contracts (D18).
- NativeWind or any `className`.
- The 1a web layout bugs.
- The `ProductListView` double-tap note.
- Any package not listed under "Packages" below.

---

## 2. Decisions (Rob, within the docs)

| # | Decision | Reason |
|---|---|---|
| D1 | **Identity storage is owned by the module.** The module has its own `users` table and an internal domain `User` entity. Hashing uses ASP.NET Core Identity's `PasswordHasher<User>` (Identity v3 format: PBKDF2-HMAC-SHA512, salted, versioned, rehash on upgrade) from the shared framework (`Microsoft.Extensions.Identity.Core`, already in `Microsoft.AspNetCore.App`, so **no package**). It sits behind an Application port, `IPasswordHashing`. We do **not** use `UserManager`, `SignInManager`, `IdentityUser`, `IdentityDbContext`, or `Microsoft.AspNetCore.Identity.EntityFrameworkCore`. Lockout, roles, and reset tokens are module domain logic, using the Identity defaults where §10.1 names them (hashing). | (1) One `EcommerceDbContext` with module-internal `.Domain` entities (§6.2). `IdentityUser`/`IdentityUserRole<>` are framework types, so `ModuleModelOwnershipTests` would need an exception, and that would weaken an architecture test. (2) Lockout and token expiry must run on `IClock` (rule 9, fake-clock tests §11.1) and inside our row lock (D7). `UserManager`'s lockout runs on its own time source and its own read-modify-write. (3) Identity's reset tokens need a shared, persisted Data Protection key ring (another package plus a table, and broken across replicas without it). Hashed, single-use DB tokens are simpler and match the refresh-token design. The tested crypto stays Identity's (§13.1 "tested hashing"). Recorded in §13.2. |
| D2 | **Roles:** one `role` column per user, `Customer \| Support \| Admin` (CHECK constraint). Registration always creates `Customer`. The JWT carries a `role` claim. Policies live in `Ecommerce.Identity.Contracts` as string constants: `AuthorizationPolicies.Admin` (role Admin) and `AuthorizationPolicies.AdminOrSupport`. Role names live there as `IdentityRoles`. The **fallback policy requires an authenticated user**, so every endpoint without metadata needs a token. Public endpoints opt out with `[AllowAnonymous]` / `.AllowAnonymous()`. A role change takes effect at the next refresh (≤ 15 min), because refresh re-reads the role from the database. | App context §2 roles form a hierarchy in v1, so one column with a CHECK is enough; a join table is an expand-only migration later. With the fallback policy, a forgotten attribute means 401, never a public admin endpoint (§3.3 "secure by default"). Contracts stay free of ASP.NET (constants only), so Catalog can write `[Authorize(Policy = AuthorizationPolicies.Admin)]` while referencing only `Identity.Contracts`. |
| D3 | **Admin bootstrap is create-only.** It uses `Identity:Bootstrap:AdminEmail` and `Identity:Bootstrap:AdminPassword`, both empty in committed config and set via user-secrets or env vars. On startup, if both are set and migrations are applied: if no user has that email, it creates the user with role Admin. If an Admin with that email exists, it does nothing. If a **non-Admin** with that email exists, it logs an Error (user id only) and **does not promote**. It never demotes. | Registration is open and emails are unverified. "Promote this email on startup" would let anyone who registers the configured email first become Admin. Create-only can't be hijacked; the worst case is a visible bootstrap failure. There is no SQL and no endpoint. Slice 2 adds the `audit_log` write when that table exists (carry-in). |
| D4 | **Access token: JWT HS256, 15 minutes** (§10.1).<br>• **Key:** `Identity:Jwt:SigningKey`, base64, **at least 32 bytes after decoding**. Empty in all committed config. Set with `dotnet user-secrets` or an env var.<br>• **Missing key:** in **Development**, an empty key means one ephemeral random key per process, generated once in a singleton `JwtSigningKeyProvider`, with a Warning log. **Any other environment fails at startup** (ValidateOnStart), and the message names the setting.<br>• **Claims:** `sub` (user id), `role`, `jti` (`Guid.CreateVersion7(clock.UtcNow)`), `iat`/`nbf`/`exp` from `IClock`, plus `iss` and `aud`. `iss = "ecommerce-api"` and `aud = "ecommerce-store"` are config names, not domains.<br>• **Validation:** `ValidAlgorithms = [HS256]`, `RequireSignedTokens`, `RequireExpirationTime`, issuer, audience, and lifetime checks, `ClockSkew = 30s`, `MapInboundClaims = false`, `NameClaimType = "sub"`, `RoleClaimType = "role"`. Never enable `IdentityModelEventSource.ShowPII`. | One process both issues and validates, so a symmetric key is enough. Pinning the algorithm closes `alg: none` and algorithm confusion. The ephemeral dev key means no secret is committed and Nathaniel needs no setup step. A dev restart only forces a silent refresh, because refresh tokens are DB-backed. |
| D5 | **Refresh tokens:**<br>• **Format:** an opaque 256-bit random value (base64url), stored only as its **SHA-256 hash** (`bytea`, unique).<br>• **Families:** login and register each start a **family** (`refresh_token_families` row with `absolute_expires_at = now + 30d`). Every refresh **rotates**: it consumes the presented token and issues a child with `expires_at = min(now + 14d, family.absolute_expires_at)`.<br>• **Reuse:** presenting an already-consumed token is **reuse**. It revokes the whole family (reason `Reuse`), returns 401, and emits a security warning (user id and family id, never the token).<br>• **No grace window:** a second refresh with the same token, even concurrent, is treated as reuse.<br>• **Rejected without revoking:** a revoked family, an expired token, or a family past its absolute expiry gets 401 and no revocation.<br>• **Locks:** each refresh locks the family row (`FOR UPDATE`).<br>• **Revocation:** logout revokes the family. A password reset revokes all of the user's families. | Rotation plus reuse detection turns a stolen refresh token into a detectable event: the attacker and the victim can't both keep using one family. Hashing means a DB leak yields no usable tokens. A fast hash is enough because the input has 256 bits of entropy. The absolute cap bounds how long a theft that is never detected can last. Strict reuse trades a rare forced logout (lost response, then a client retry) for no replay window. The client prevents its own double refresh with single-flight (D20). Revisit with a short reuse interval only if forced logouts are measured. |
| D6 | **Refresh-token transport, chosen per client:**<br>• **Native:** the token travels in the JSON body and is stored in **SecureStore** (`WHEN_UNLOCKED_THIS_DEVICE_ONLY`).<br>• **Web:** the token travels **only** as an httpOnly cookie: `ecommerce_rt`, `HttpOnly`, `Secure` (config `Identity:RefreshTokens:Cookie:Secure`, default true), `SameSite=Strict`, `Path=/api/v1/auth`, `Expires` equal to the token expiry. A request opts in with the header **`X-Auth-Transport: cookie`**. With that header, the server never puts the refresh token in the body and reads it only from the cookie. Without it, the server reads only the body and ignores the cookie.<br>• **Access token:** in memory only on both platforms.<br>• **Cache headers:** every token response has `Cache-Control: no-store`.<br>• **CORS** (configured origins only): `AllowCredentials`, methods `GET, POST, PUT`, request headers `X-Correlation-ID, Content-Type, Authorization, X-Auth-Transport`, exposed `X-Correlation-ID`. | §5.2 asks for "secure storage on web". Script can't read an httpOnly cookie, so XSS can't steal the long-lived credential. Requiring the custom header makes a cross-origin cookie request need a CORS preflight, which unlisted origins fail, so the cookie can't be used for CSRF. SameSite=Strict blocks cross-site sends. Dev works because `localhost:8081` and `localhost:5080` are the same site. **Constraint for the open domain decision:** the production web origin and API must be the same site (e.g. sibling subdomains), or this design is revisited. Recorded in §13.2. |
| D7 | **Lockout: 5 failed attempts lock the account for 5 minutes** (config `Identity:Lockout`).<br>• **The lock comes first.** Login opens a transaction and locks the user row (`SELECT … FOR UPDATE`) **before** verifying the password. It checks the lockout, verifies, then records the failure or success and commits.<br>• **Constant work.** An unknown email and a locked account both run one verification against a fixed dummy hash, then return the same generic 401.<br>• **On success:** the failure count resets and the hash is upgraded if Identity reports `SuccessRehashNeeded`.<br>• **Refresh is not blocked by lockout.** Lockout protects the password, and a refresh token is a separate credential.<br>• **A successful reset clears the lockout.** | Verifying before taking the lock would let N parallel guesses all be checked before the counter reaches 5 (a lockout bypass). Under the lock, at most 5 guesses are ever verified. Constant work keeps "no account" and "locked" off a timing channel. Hashing happens inside the transaction, but it is CPU work, not an external call (rule 6). It holds one row lock and one connection for about the hash time, which only serializes logins to the same account. |
| D8 | **Password policy:** 8–128 characters, no composition rules. It is checked by FluentValidation on every DTO that carries a new password, using constants from `Application/PasswordPolicy.cs`. Hashing uses Identity defaults (D1). | NIST SP 800-63B: length helps, composition rules don't. The 128 maximum bounds PBKDF2 input. §10.1 said "Identity defaults". Hashing keeps them, but the composition defaults are dropped, and §10.1 is updated. |
| D9 | **Enumeration:**<br>• **Login and reset never reveal whether an account exists.** Login returns the same generic 401 problem ("Email or password is incorrect, or the account is temporarily locked."). Reset returns `202` with an empty body for any well-formed email.<br>• **Register does reveal it:** `409` "An account with this email already exists."<br>• **Accepted residual:** a reset for an existing user does a little extra DB work, which is a small timing gap. The Slice 6 outbox removes it. | Enumeration-proof registration needs verify-first registration, which needs email delivery (open decision) and an email-verification feature that isn't in v1 scope. Accepted, mitigated later by Slice 7 rate limits on register, login, and reset. **Revisit when** an email provider exists and abuse is measured. Recorded in §13.2. |
| D10 | **Password reset:**<br>• **Tokens:** an opaque 256-bit token, SHA-256 hashed, **single use**, 30 minutes (config). Only the newest token is valid: issuing one invalidates the user's outstanding tokens.<br>• **Request:** a transaction locks the user, invalidates old tokens, and inserts the new hash. After **commit**, outside any transaction, it calls `IPasswordResetLinkSender.SendAsync(userId, email, link)`.<br>• **Confirm:** a transaction locks the user and then the token. It checks the token is valid, sets the new hash, marks the token used, revokes every family, clears the lockout, and commits.<br>• **Senders in 1b:**<br>&nbsp;&nbsp;– `DevPickupDirectoryLinkSender`: Development only. It writes a text file (link, user id, time) into `Identity:PasswordReset:DevPickupDirectory` (Development: `../../../artifacts/dev-mail`, which is gitignored).<br>&nbsp;&nbsp;– `NoProviderLinkSender`: everywhere else. It logs a Warning with the user id only.<br>&nbsp;&nbsp;– Startup **fails** if `DevPickupDirectory` is set outside Development.<br>• **Link:** `Identity:PasswordReset:LinkBaseUrl` + `?token=…`. It is empty (no link) in committed config, and Development uses `http://localhost:8081/reset-password`.<br>• **No token in any log, in any environment.** | No email provider is chosen (§2), so none is invented. A pickup directory is the SMTP-style dev pattern: Nathaniel can test the whole flow, and the "never log the token" assertion holds globally. **Rule 6:** 1b makes **no external call**. The DB state change commits first, and the only side effect is a local file in Development. **Wiring a real provider directly into `IPasswordResetLinkSender` is forbidden.** Slice 6 replaces this seam with an outbox event `PasswordResetRequested { userId }`, where the Notifications consumer mints the token (Identity contract) and sends the email, with `(consumer, event_id)` dedupe. That keeps the plaintext token out of the DB and out of the outbox. Recorded in §12.3 and §13.2 as a Slice 6 carry-in. |
| D11 | **SharedKernel `IUnitOfWork` is pulled forward and kept minimal.** `Ecommerce.SharedKernel/Persistence/IUnitOfWork` has `SaveChangesAsync(ct)` and `InTransactionAsync<T>(Func<CancellationToken, Task<T>>, ct)`. A nested call throws. Persistence also adds `ConcurrencyConflictException` and `UniqueConstraintViolationException(string ConstraintName)`. `Ecommerce.Persistence/EfUnitOfWork` is registered Scoped by `AddEcommercePersistence`. It uses READ COMMITTED, and it translates `DbUpdateConcurrencyException` and Postgres `23505` (with the constraint name). | Identity needs real transactions with row locks now, and Application can't see EF (layer rules). §6.2 planned this abstraction for Slice 2/4. Introducing the minimal shape now means Slices 2 and 4 extend it instead of each module re-inventing it. One transaction per request scope still holds, because every module shares the scoped context. |
| D12 | **Locking strategy depends on the case.** Auth flows use **pessimistic** row locks (short server-side critical sections) with one global lock order: **`users` → `refresh_token_families` → `refresh_tokens`**, and **`users` → `password_reset_tokens`**. Admin product edits use **optimistic** concurrency: `products.xmin` is mapped as a `uint Version` row version, the client sends `version` with every PUT, and a stale version gets **409**. On every admin save, the write store forces `UpdatedAt` to be modified, so the version check runs even when the clock didn't move. **In a locking flow, the first load of an entity is the `FOR UPDATE` query.** Any earlier lookup is an `AsNoTracking` projection of immutable columns (for example `family_id` or `user_id` by token hash), because otherwise EF identity resolution hands back a stale tracked instance. Repositories throw if a `ForUpdate` method is called outside a transaction. | A human editing a product has think time, so a lock can't be held across a read and a later write. A lost update is detected instead. Auth races happen within milliseconds on the server, so serializing them is simple to prove. A fixed lock order prevents deadlocks (Postgres would abort one with 40P01 and return a 500). The stale-tracked-entity and lock-outside-transaction traps are classic EF bugs. |
| D13 | **Validation:** FluentValidation (core package only), with validators in each module's `Api` folder, called explicitly by controllers. The controller validates, calls the service, and maps (rule 7). A local `ValidationResult → ValidationProblem` helper exists per module, so neither SharedKernel nor Contracts needs ASP.NET. **Strict JSON:** `JsonSerializerOptions.UnmappedMemberHandling = Disallow` globally, so an unknown request field (`role`, `isActive`, `price` on a wrong DTO) is a 400 binding problem. | §10.1 names FluentValidation for every write DTO. The auto-validation package `FluentValidation.AspNetCore` is deprecated and not used. Rejecting unknown members makes mass assignment a loud 400 instead of a silently ignored field. |
| D14 | **API surface added** (base `/api/v1`, details in §3.6 and §4.4):<br>• **Auth:** `auth/register`, `login`, `refresh`, `logout`, `password-reset`, `password-reset/confirm` (all anonymous); `GET auth/me` (any authenticated user).<br>• **Admin products:** `GET admin/products`, `GET admin/products/{id}`, `GET admin/products/{id}/preview`, `POST admin/products`, `PUT admin/products/{id}`, `POST admin/products/{id}/publish`, `POST admin/products/{id}/unpublish`.<br>• **Admin categories:** `POST admin/categories`.<br>• **Roles:** every admin endpoint uses the Admin policy (Support gets 403, as in app context §2).<br>• **Status codes:** 400 for validation, 401 with no or an invalid token, 403 for the wrong role, 404 for an unknown id, 409 for conflicts (duplicate slug or SKU, stale version, publishing with no active variant). | `me` gives the client a bearer-protected read, and gives tests a non-admin bearer target. Preview and get-by-id are the "admin preview" of §12.3. Category create is the "enforced in the 1b admin service" rule from the 1a plan (a parent can't be a child), and production needs some way to add categories without SQL. Recorded in §9. |
| D15 | **Admin update semantics:** `PUT /admin/products/{id}` carries the whole editable document: `version`, `slug`, `name`, `description`, `categorySlug`, `variants[]`, `images[]`.<br>• **Variants:** every existing variant must appear by `id`, or the request gets 400 ("set isActive false instead of removing"). Entries without an `id` are added. **SKU and currency are immutable** for existing variants. A new variant must use the product's currency.<br>• **Last active variant:** deactivating the last active variant of a published product gets 409 ("unpublish first").<br>• **Slug:** editable only while the product is unpublished.<br>• **Images:** replaced as a list, and the array order becomes `sort_order`.<br>• **Limits:** at most 50 variants and 20 images per product (domain).<br>• **Inactive by default:** create always produces an inactive product (§8.7). | No silent deactivation by omission. Inventory (Slice 2) and order snapshots (Slice 4) key on variant id and SKU, so those stay stable. A published slug is a public URL. The limits bound request size and DoS. |
| D16 | **Images are keys only.** There is no upload endpoint: `POST /admin/products/{id}/images` is deferred until the bucket is chosen (§2 open decision). A storage key is 1–512 characters, has no control characters, no `\`, no `:` (so no URL schemes), no leading `/`, and no empty, `.`, or `..` path segment. Spaces are allowed: the resolver escapes them, and the 1a test I-11 uses `dev/a b.jpg`. URLs come from the existing resolver, so they are `null` while `Catalog:Images:BaseUrl` is empty. | §8.7 says keys only. Rejecting `:` stops an admin from pointing images at arbitrary external URLs. It is still a key, not a URL. |
| D17 | **Admin list filters:** `search` (the same full-text index as the public list), `category` (slug, including its children), `status = all \| active \| inactive` (default `all`), and `page`/`pageSize` with the same bounds as the public list (400 when out of range). The sort is `updated_at desc, id desc`. "Out of Stock" arrives in **Slice 2**. | Stock doesn't exist yet. There is no new index: the admin list isn't a hot path (§10.4 launch scale). |
| D18 | **`IUserReader` and `ICurrentUser` are not added** to `Identity.Contracts` in 1b. They arrive with their first consumer (Cart, Slice 3). 1b's Contracts project holds `IdentityRoles` and `AuthorizationPolicies` (consumed by Catalog). | Same rule as 1a D27: no consumer means dead code. |
| D19 | **`lib/platform.ts` is one file** using `Platform.OS`, not a `.native.ts`/`.web.ts` Metro split. It exports `refreshTokenTransport(): 'cookie' \| 'body'` and `refreshTokenStorage` (native: `expo-secure-store`; web: no-ops that return `null`). Both branches are tested by mocking `Platform.OS`. | Rule 17 keeps platform code in `lib/platform.ts`, and a single file satisfies it. A Metro split would trigger §11.3's per-platform Jest projects. That is a bigger test-infra change with no extra safety for about 20 lines. **Switch to the split** when platform code grows: payments SDK (Slice 4), push. |
| D20 | **The session store is `features/auth/session.ts`,** a small external store read through `useSyncExternalStore`. It is not Zustand (Zustand is cart only, §2) and not TanStack Query. The access token lives in a module variable and is **never part of the React snapshot**. `lib/api-client.ts` receives `setAuthHandlers({ getAccessToken, refreshAccessToken })`, so `lib` never imports `features`. Refresh is **single-flight**: concurrent 401s share one refresh promise. Sign-out (logout, or a refresh that returns 401) clears the query cache (`queryClient.clear()`). | One refresh at a time is what strict reuse detection (D5) requires of the client. Keeping the token out of React state keeps it out of devtools and render logs. Clearing the cache stops the next user on the device from seeing the previous user's admin data. |
| D21 | **F7 guard:** `app/admin/_layout.tsx` renders `AdminGate`, which uses the pure `adminRouteDecision(role, pathname)`.<br>• **Admin:** every admin route.<br>• **Support:** `/admin`, `/admin/orders*`, `/admin/customers*`. Other admin routes redirect to `/admin`.<br>• **Customer:** redirected to `/`.<br>• **Signed out:** redirected to `/login?returnTo=<path>`.<br>• **While the session is restoring:** a loading state, with no redirect and no fetch.<br>• **Visibility:** admin links appear only for Admin/Support (AccountBar).<br>• **`returnTo`:** sanitized by `safeReturnTo`, which accepts only a single leading `/`, no `//`, no `\`, and no scheme. Anything else becomes `/`. | App context §5: a customer is redirected and gets no data. The API is the real control (rule 11, tested in II-10). The guard is UX. Sanitizing `returnTo` closes an open redirect. |
| D22 | **New route `/admin/products/new`** (`app/admin/products/new.tsx`) for create. App context §5 is updated. Edit and Preview are a toggle inside `/admin/products/[id]`, with no new route. | A static `new` route is clearer than special-casing `[id] = "new"`. Expo Router ranks static segments above dynamic ones. |
| D23 | **Carry-ins:**<br>• Variant name is required, 1–200 characters, and can't be only whitespace. Size and colour are `null` or 1–50 characters. Empty becomes `null` in Application. Enforced in `AddVariant` and the new `UpdateVariant`.<br>• Fix CS8602 at `CatalogDevSeeder.cs:58,66` by restructuring the `TryGetValue` branch (`if (productsBySlug.TryGetValue(…, out var existing)) { product = existing; } else { … }`), with **no `!`** and no behavior change. The existing seeder tests are the regression proof, and `dotnet build -c Release --no-incremental` must show **0 warnings**.<br>• `api-client.ts`: guard a pre-aborted signal (no fetch call), and report a timeout that fires while the body is read as `timeout`, not `server`.<br>• **Not included:** the 1a `ProductListView` double-tap note and the web layout bugs (chip ScrollViews, search overflow). They are catalog UI, unrelated to 1b. They go to the NativeWind adoption step (§5.5 step 3) or the next catalog UI change. | These come from the 1a and refactor reviews and were requested by the owner. The api-client fixes sit in a file 1b rewrites anyway. The UI bugs would be an unrelated refactor (quality bar §1). |
| D24 | **Request logs carry `UserId`** (the `sub` claim) through Serilog `EnrichDiagnosticContext`. Never a token, header, or body. | §3.3: "which user" must be answerable from the logs. |
| D25 | **Styling stays `StyleSheet.create`** (§5.5: NativeWind isn't adopted). New pressables keep the 44 minimum. | Owner constraint. |
| D26 | **Security events go through a port:** `ISecurityEventSink` (Application) with `RefreshTokenReuseDetected(userId, familyId)` and `AccountLockedOut(userId)`. Infrastructure implements it with structured Warning logs. | Application can't use `Microsoft.Extensions.Logging` (source-scan rule). This is also the future audit and alert hook. |
| D27 | **No rate limiting in 1b.** It is Slice 7 (§12.3), which **adds register and password-reset** to the login, checkout, and webhook limits in §10.1. Also no token-table cleanup: retention for consumed and expired refresh and reset tokens is a Slice 7 item. | Slice order. The table growth is bounded by login frequency, and it is harmless at launch scale. |
| D28 | **Publishing without stock is allowed in 1b,** because no stock exists, and the UI shows no badge for `Unknown`. The Slice 2 plan decides whether publishing requires a stock row (§8.7 "set initial stock, then publish"). | §8.7's order needs Inventory. Blocking publish now would make "done when" impossible. |

**Packages (complete list):**
- NuGet `Microsoft.AspNetCore.Authentication.JwtBearer` **10.0.14**: referenced by `Ecommerce.Identity` only. It brings `Microsoft.IdentityModel.JsonWebTokens` transitively for token creation. §2 Auth (JWT). It replaces nothing.
- NuGet `FluentValidation` **12.1.1**: referenced by `Ecommerce.Identity` and `Ecommerce.Catalog`. Named in §10.1. It replaces the hand-written validation for **write** DTOs only; 1a's `ProductSearchCriteria` stays.
- npm `expo-secure-store`: install with `npx expo install expo-secure-store` and pin the exact SDK 58 version. It is named by §5.2 ("SecureStore on native") and is an Expo SDK module.
- **Not added:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `FluentValidation.AspNetCore`, `FluentValidation.DependencyInjectionExtensions` (register validators by hand), `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `System.IdentityModel.Tokens.Jwt` (direct), `jwt-decode`, `zustand`, any form library.

---

## 3. Backend — Identity module

### 3.1 Projects

| Path | Contents |
|---|---|
| `backend/src/Modules/Identity/Ecommerce.Identity.Contracts/Ecommerce.Identity.Contracts.csproj` | References only SharedKernel. `IdentityRoles.cs` holds `public static class IdentityRoles { public const string Customer = "Customer", Support = "Support", Admin = "Admin"; }`. `AuthorizationPolicies.cs` holds `public static class AuthorizationPolicies { public const string Admin = "Admin"; public const string AdminOrSupport = "AdminOrSupport"; }` |
| `backend/src/Modules/Identity/Ecommerce.Identity/Ecommerce.Identity.csproj` | `FrameworkReference Microsoft.AspNetCore.App`. Project references: SharedKernel, Persistence, Identity.Contracts. Packages: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `FluentValidation`. `InternalsVisibleTo` only `Ecommerce.Identity.Tests` and `Ecommerce.Api.IntegrationTests` |
| `backend/tests/Ecommerce.Identity.Tests/` | Same shape as `Ecommerce.Catalog.Tests` (`OutputType Exe`, xunit.v3) |
| `Ecommerce.slnx`, `Dockerfile` | Add both src projects and the test project to the slnx, under `/src/Modules/Identity/` and `/tests/`. Add both src csproj `COPY` lines to the Dockerfile restore layer |
| `Directory.Packages.props` | Add the two `PackageVersion` entries above |

`Ecommerce.Catalog.csproj` adds a `ProjectReference` to `Ecommerce.Identity.Contracts`. The host references `Ecommerce.Identity`. `ModuleComposition.ModuleAssemblies` and `AddEcommerceModules` add `IdentityModule`.

### 3.2 Domain — `Ecommerce.Identity.Domain` (internal; depends only on SharedKernel)

| File | Contract |
|---|---|
| `UserRole.cs` | `enum UserRole { Customer, Support, Admin }`, stored as a string |
| `EmailAddress.cs` | `static string Normalize(string? raw)`: trims. `static bool IsValid(string)`: 3–254 characters, exactly one `@`, non-empty local and domain parts, no whitespace or control characters. Any regex ends in `\z` (the 1a B2 lesson) |
| `LockoutPolicy.cs` | `sealed record LockoutPolicy(int MaxFailedAttempts, TimeSpan Duration)`, with validation (≥ 1, > 0) |
| `User.cs` | Fields: `Id`, `Email`, `DisplayName` (1–100), `PasswordHash`, `SecurityStamp` (new 32-hex value on every credential change, generated from a value passed in, never from a system clock or `Random`), `Role`, `AccessFailedCount`, `LockoutEnd?`, `CreatedAt`, `UpdatedAt`.<br>Methods:<br>• `static Register(Guid id, string email, string displayName, string passwordHash, string securityStamp, DateTimeOffset now)` (role Customer)<br>• `static CreateAdmin(...)` (bootstrap only)<br>• `bool IsLockedOut(DateTimeOffset now)`: `LockoutEnd > now`<br>• `bool RecordFailedLogin(DateTimeOffset now, LockoutPolicy p)`: returns true when this attempt locked the account. It increments the count; at `MaxFailedAttempts` it sets `LockoutEnd = now + Duration` and resets the count to 0<br>• `RecordSuccessfulLogin(now)`: resets the count and clears `LockoutEnd`<br>• `ReplacePasswordHash(string hash, string newStamp, DateTimeOffset now)`: also clears the lockout<br>• `UpgradePasswordHash(string hash, now)`: the rehash case, no stamp change |
| `RefreshTokenFamily.cs` | `Id`, `UserId`, `CreatedAt`, `AbsoluteExpiresAt`, `RevokedAt?`, `RevokedReason?` (`enum RefreshTokenRevocationReason { Logout, Reuse, PasswordReset }`). `Revoke(reason, now)` is idempotent: the first reason and time are kept |
| `RefreshToken.cs` | `Id`, `FamilyId`, `TokenHash` (`byte[]`, 32), `CreatedAt`, `ExpiresAt`, `ConsumedAt?`, `ReplacedById?` |
| `RefreshRotation.cs` | A pure decision: `static RotationOutcome Decide(RefreshTokenFamily family, RefreshToken token, DateTimeOffset now)` returns `FamilyRevoked \| Reuse \| Expired \| Rotate`. **Order:** revoked family → consumed token (reuse) → expired token or expired family → rotate. `static RefreshToken Rotate(RefreshTokenFamily f, RefreshToken presented, Guid newId, byte[] newHash, DateTimeOffset now, TimeSpan sliding)` consumes the presented token, sets `ReplacedById`, and returns a child with `ExpiresAt = min(now + sliding, f.AbsoluteExpiresAt)`. Applying `Reuse` calls `family.Revoke(Reuse, now)` |
| `PasswordResetToken.cs` | `Id`, `UserId`, `TokenHash`, `CreatedAt`, `ExpiresAt`, `UsedAt?`, `InvalidatedAt?`. `bool CanRedeem(now)`. `Redeem(now)` throws `DomainException` if it can't be redeemed. `Invalidate(now)` |

### 3.3 Application — `Ecommerce.Identity.Application` (no `Microsoft.*`, no EF; uses SharedKernel and Contracts)

| File | Contract |
|---|---|
| `IdentitySettings.cs` | Plain records built once in `IdentityModule` from options: `TokenLifetimes(AccessToken 15m, RefreshSliding 14d, RefreshAbsolute 30d, PasswordReset 30m)`, `LockoutPolicy`, `RefreshCookieSettings(string Name, bool Secure)`, `PasswordResetLinkSettings(string? LinkBaseUrl)` |
| `PasswordPolicy.cs` | `MinLength = 8`, `MaxLength = 128` |
| Ports | `IUserRepository`: `Add`, `FindByEmailForUpdateAsync`, `FindByIdForUpdateAsync`, `FindByIdAsync` (no tracking).<br>`IRefreshTokenRepository`: `AddFamily`, `AddToken`, `FindFamilyIdByTokenHashAsync` (AsNoTracking projection), `GetFamilyForUpdateAsync`, `FindTokenByHashAsync` (only after the family lock), `RevokeAllFamiliesForUserAsync(userId, reason, now)` (`ExecuteUpdate` inside the transaction).<br>`IPasswordResetTokenRepository`: `Add`, `FindUserIdByTokenHashAsync`, `GetForUpdateByHashAsync`, `InvalidateOutstandingAsync(userId, now)`.<br>`IPasswordHashing`: `Hash(string)`, `Verify(string hash, string password) → Failed \| Success \| SuccessRehashNeeded`, `VerifyAgainstDummy(string password)`.<br>`IAccessTokenIssuer`: `Issue(User, DateTimeOffset now) → (string Token, DateTimeOffset ExpiresAt)`.<br>`ISecureTokenGenerator`: `NewToken() → string` (256-bit base64url), `NewSecurityStamp()`.<br>`IPasswordResetLinkSender`: `SendAsync(Guid userId, string email, string? link, ct)`.<br>`ISecurityEventSink` (D26) |
| `TokenHash.cs` | `static byte[] Of(string token)`: SHA-256 of the UTF-8 bytes (System.Security.Cryptography, a framework assembly) |
| `AuthResults.cs` | `UserView(Guid Id, string Email, string DisplayName, UserRole Role)`, `SessionIssued(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, UserView User)`. Outcome unions per service (`RegisterOutcome.Created \| DuplicateEmail`, `LoginOutcome.Success \| InvalidCredentials`, `RefreshOutcome.Success \| Rejected`, `ResetConfirmOutcome.Success \| InvalidToken`) |
| `RegistrationService.cs` | Hashes **outside** the transaction. Inserts the user (role Customer) and a family with its first token in one `InTransactionAsync`. A `UniqueConstraintViolationException("ux_users_email")` becomes `DuplicateEmail` |
| `LoginService.cs` | D7, exactly in this order: transaction → `FindByEmailForUpdateAsync` → null: `VerifyAgainstDummy` and `InvalidCredentials` → locked: `VerifyAgainstDummy` and `InvalidCredentials` (no count change) → `Verify` → failed: `RecordFailedLogin` (if it locked, `AccountLockedOut`) and `InvalidCredentials` → success: `RecordSuccessfulLogin`, upgrade the hash if needed, new family and token, then commit. The access token is issued after commit |
| `RefreshService.cs` | D5 and D12: `FindFamilyIdByTokenHashAsync` (none → Rejected) → transaction → `GetFamilyForUpdateAsync` → `FindTokenByHashAsync` → `RefreshRotation.Decide`. On Reuse: revoke, commit, `RefreshTokenReuseDetected`, then Rejected. On Rotate: re-read the user (role, for the new access token), save the child, commit. `LogoutAsync(token)`: the same lookup, then lock the family and revoke it (`Logout`). It always succeeds |
| `PasswordResetService.cs` | `RequestAsync(email)`: normalize → transaction { `FindByEmailForUpdateAsync`; if null, return; `InvalidateOutstandingAsync`; add the token hash } → **after commit**, if a user existed, `SendAsync`. The result is the same either way. `ConfirmAsync(token, newPassword)`: hash the password outside the transaction → `FindUserIdByTokenHashAsync` (none → InvalidToken) → transaction { lock the user, then the token (lock order D12) → `CanRedeem`, `Redeem`, `ReplacePasswordHash`, `RevokeAllFamiliesForUserAsync(PasswordReset)` } |
| `CurrentUserService.cs` | `GetAsync(Guid id) → UserView?` |

### 3.4 Infrastructure — `Ecommerce.Identity.Infrastructure`

| File | Contract |
|---|---|
| `Persistence/IdentityDbModel.cs` + configurations | `modelBuilder.HasPostgresExtension("citext")`. Tables, snake_case and mapped explicitly:<br>• **`users`:** `id uuid pk`, `email citext` with `ux_users_email`, `display_name varchar(100)`, `password_hash text`, `security_stamp varchar(64)`, `role varchar(20)` with `ck_users_role CHECK (role IN ('Customer','Support','Admin'))`, `access_failed_count int` with `ck_users_access_failed_count (>= 0)`, `lockout_end timestamptz null`, `created_at`, `updated_at`.<br>• **`refresh_token_families`:** `id`, `user_id` (fk → users, restrict) with `ix_refresh_token_families_user_id`, `created_at`, `absolute_expires_at`, `revoked_at null`, `revoked_reason varchar(20) null`.<br>• **`refresh_tokens`:** `id`, `family_id` (fk restrict) with `ix_refresh_tokens_family_id`, `token_hash bytea` with `ux_refresh_tokens_token_hash`, `created_at`, `expires_at`, `consumed_at null`, `replaced_by_id uuid null`.<br>• **`password_reset_tokens`:** `id`, `user_id` (fk restrict) with `ix_password_reset_tokens_user_id`, `token_hash bytea` with `ux_password_reset_tokens_token_hash`, `created_at`, `expires_at`, `used_at null`, `invalidated_at null` |
| `UserRepository.cs`, `RefreshTokenRepository.cs`, `PasswordResetTokenRepository.cs` | `ForUpdate` methods use `FromSql($"… FOR UPDATE")`, parameterized. The citext comparison casts explicitly: `WHERE email = CAST({email} AS citext)`. Each method throws `InvalidOperationException` when `Database.CurrentTransaction` is null |
| `IdentityPasswordHashing.cs` | Wraps `PasswordHasher<User>`. The dummy hash is computed once (lazy) from a random value at startup, never a committed literal |
| `RandomTokenGenerator.cs` | `RandomNumberGenerator` |
| `Jwt/JwtOptions.cs`, `Jwt/JwtSigningKeyProvider.cs`, `Jwt/JwtAccessTokenIssuer.cs`, `Jwt/ConfigureJwtBearerOptions.cs` | D4. `JsonWebTokenHandler` with a `SecurityTokenDescriptor` (`IssuedAt`/`NotBefore`/`Expires` set explicitly from `IClock`). `IConfigureNamedOptions<JwtBearerOptions>` reads the key from the provider |
| `PasswordReset/PasswordResetOptions.cs`, `DevPickupDirectoryLinkSender.cs`, `NoProviderLinkSender.cs` | D10. The registration is chosen by an environment check in a factory: Development **and** a configured directory → pickup; otherwise → no provider |
| `LoggingSecurityEventSink.cs` | Structured Warning logs: `"Refresh token reuse detected; family {FamilyId} of user {UserId} revoked"`, `"User {UserId} locked out"` |
| `Bootstrap/AdminBootstrapOptions.cs`, `Bootstrap/AdminBootstrapHostedService.cs` | D3. If migrations are pending, it logs a Warning and skips |

### 3.5 Module root — `IdentityModule.cs` (the only public type)

- `AddIdentityModule(IServiceCollection, IConfiguration)`:
  - registers `IdentityDbModel` (TryAddEnumerable), options with ValidateOnStart, the settings records, repositories (Scoped), services (Scoped), `IPasswordHashing`, `IAccessTokenIssuer`, `ISecureTokenGenerator`, `JwtSigningKeyProvider` (Singleton), the sender factory, the sink, the bootstrap hosted service, validators (Scoped, by hand)
  - `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer()`
  - `AddAuthorizationBuilder()` with the two policies and `SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`.
- The environment-dependent checks (signing key; `DevPickupDirectory` outside Development) are options validators that take `IHostEnvironment` (`.Validate<IHostEnvironment>(…)`). Registration itself must not need `IHostEnvironment`, because `ModuleModelOwnershipTests` builds `AddEcommerceModules` with configuration only.
- Config keys (`appsettings.json`, with these values and comments in the README):

```json
"Identity": {
  "Jwt": { "Issuer": "ecommerce-api", "Audience": "ecommerce-store", "SigningKey": "", "AccessTokenMinutes": 15 },
  "RefreshTokens": { "SlidingLifetimeDays": 14, "AbsoluteLifetimeDays": 30, "Cookie": { "Name": "ecommerce_rt", "Secure": true } },
  "Lockout": { "MaxFailedAttempts": 5, "DurationMinutes": 5 },
  "PasswordReset": { "TokenLifetimeMinutes": 30, "LinkBaseUrl": "", "DevPickupDirectory": "" },
  "Bootstrap": { "AdminEmail": "", "AdminPassword": "" }
}
```

`appsettings.Development.json` sets only `PasswordReset.LinkBaseUrl = "http://localhost:8081/reset-password"` and `PasswordReset.DevPickupDirectory = "../../../artifacts/dev-mail"`. **No signing key and no bootstrap password in any committed file.** The CI secret scan flags the word "password" directly followed by an equals sign and a value, so no test or config literal may contain that shape.

### 3.6 Api — `Ecommerce.Identity.Api` (thin; no Infrastructure namespace, per the layer rule)

`AuthController` `[ApiController] [Route("api/v1/auth")]`:

| Endpoint | Auth | Request → Response |
|---|---|---|
| `POST register` | `[AllowAnonymous]` | `{ email, password, displayName }` → **201** `TokenResponse` · 400 · 409 (duplicate email) |
| `POST login` | `[AllowAnonymous]` | `{ email, password }` → **200** `TokenResponse` · 400 · **401** generic (D9) |
| `POST refresh` | `[AllowAnonymous]` | body `{ refreshToken }`, or the cookie with `X-Auth-Transport: cookie` → **200** `TokenResponse` · **401** |
| `POST logout` | `[AllowAnonymous]` | body `{ refreshToken }`, or the cookie with the header → **204** always. With the cookie transport it also expires the cookie |
| `POST password-reset` | `[AllowAnonymous]` | `{ email }` → **202** always (400 only for a malformed email) |
| `POST password-reset/confirm` | `[AllowAnonymous]` | `{ token, newPassword }` → **204** · 400 (`"This reset link is invalid or has expired."`, or password policy errors) |
| `GET me` | authenticated | → 200 `{ id, email, displayName, role }` |

```jsonc
// TokenResponse — Cache-Control: no-store on every auth response
{ "accessToken": "…", "accessTokenExpiresAt": "2026-11-03T12:15:00Z",
  "refreshToken": "…",            // null when X-Auth-Transport: cookie (it is in Set-Cookie instead)
  "refreshTokenExpiresAt": "2026-11-17T12:00:00Z",
  "user": { "id": "…", "email": "a@example.test", "displayName": "Ana", "role": "Customer" } }
```

- A `RefreshTokenTransport` helper in Api reads the header and the cookie and writes or expires the cookie, using `RefreshCookieSettings` from Application.
- Request DTOs are `sealed record`s with exactly these fields.
- FluentValidation validators enforce: email ≤ 254 and valid format; password 8–128 (`PasswordPolicy`); displayName 1–100 after trimming; refreshToken and token ≤ 100 characters.

### 3.7 Host — `Ecommerce.Api`

- **`Program.cs`:**
  - `UseAuthentication()` then `UseAuthorization()`, after `UseCors()` and before `MapControllers()`
  - `AddJsonOptions`: add `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`
  - CORS policy per D6
  - Serilog `EnrichDiagnosticContext`: `UserId` from `sub` (D24).
- **`Health/HealthEndpoints.cs`:** `.AllowAnonymous()` on both endpoints. Without it the fallback policy makes health return 401. II-11 and the existing `HealthTests` catch that.
- **`Composition/ModuleComposition.cs`:** add `IdentityModule`.
- **401/403 bodies:** these must be `application/problem+json` with `traceId` and `correlationId`. `UseStatusCodePages` already writes ProblemDetails for an empty body, and II-10 proves it. If the JwtBearer challenge bypasses it, set `JwtBearerEvents.OnChallenge`/`OnForbidden` to write ProblemDetails through `IProblemDetailsService`. Don't assume either way.

### 3.8 SharedKernel and Persistence (D11)

| File | Contract |
|---|---|
| `Ecommerce.SharedKernel/Persistence/IUnitOfWork.cs` | `Task SaveChangesAsync(CancellationToken ct)`, `Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)` |
| `Ecommerce.SharedKernel/Persistence/ConcurrencyConflictException.cs`, `UniqueConstraintViolationException.cs` | Plain exceptions. The unique one carries `ConstraintName` |
| `Ecommerce.Persistence/EfUnitOfWork.cs` | Begins READ COMMITTED, runs `work`, saves, commits. On any exception it rolls back and rethrows the translated exception. A nested call throws `InvalidOperationException`. Registered Scoped in `AddEcommercePersistence` |

---

## 4. Backend — Catalog admin

### 4.1 Domain changes — `Ecommerce.Catalog.Domain`

| Item | Contract |
|---|---|
| `Product.Version` | `public uint Version { get; private set; }`. A primitive, mapped to `xmin` in Infrastructure |
| `Product` constants | `MaxVariants = 50`, `MaxImages = 20`, `MaxVariantNameLength = 200`, `MaxVariantOptionLength = 50` |
| `AddVariant` (changed) | Adds the D23 name, size, and colour checks and the `MaxVariants` check. Everything else is unchanged |
| `UpdateDetails(string slug, string name, string description, Guid categoryId, DateTimeOffset now)` | The same validation as `Create`, shared through private helpers. A slug change while `IsActive` throws `DomainException` |
| `UpdateVariant(Guid variantId, string name, string? size, string? colour, decimal price, decimal? compareAtPrice, int weightGrams, bool isActive, DateTimeOffset now)` | An unknown id throws. Price, compare-at, weight, name, size, and colour rules are as in `AddVariant`. Deactivating the last active variant of an active product throws. There is no SKU or currency parameter, so they are immutable |
| `ReplaceImages(IReadOnlyList<(Guid Id, string StorageKey, string AltText)> images, DateTimeOffset now)` | At most 20. `sort_order` = index. Keys must pass `StorageKey.IsValid` (D16). Alt text ≤ 300 |
| `AddImage` (changed) | Uses `StorageKey.IsValid` |
| `Publish` / `Unpublish` | **Idempotent.** No change (and no `UpdatedAt` bump) when already in that state. Publish still requires an active variant |
| `ProductVariant` | `internal void Update(…)` and `internal void SetActive(bool)`, called only from `Product` (the aggregate root sets `UpdatedAt`). Keep the existing `Deactivate()`, because the seeder and tests use it |
| `StorageKey.cs` | `static bool IsValid(string?)` (D16) |
| `Category` | No change. The "parent must be top-level" check needs a lookup, so it lives in `AdminCategoryService` |

### 4.2 Application — `Ecommerce.Catalog.Application.Admin`

| File | Contract |
|---|---|
| `IProductWriteStore.cs` | `Task<Product?> GetForUpdateAsync(Guid id, ct)` (tracked; with variants and images), `void Add(Product)`, `void SetExpectedVersion(Product, uint version)`, `Task<Guid?> FindCategoryIdBySlugAsync(string slug, ct)` |
| `ICategoryWriteStore.cs` | `Task<(Guid Id, Guid? ParentId)?> FindBySlugAsync(string slug, ct)`, `void Add(Category)` |
| `IAdminCatalogReadStore.cs` | `ListAsync(AdminProductListCriteria, ct)` → page rows. `GetAsync(Guid id, ct)` → the full admin row: all variants, image keys, version, category. `GetPreviewAsync(Guid id, ct)` → the public `ProductDetailRow` shape, active variants only, **ignoring `is_active`**, plus `IsActive` |
| `AdminProductListCriteria.cs` | Parses and validates like `ProductSearchCriteria` (D17). Returns criteria or errors |
| `AdminProductCommands.cs` | `CreateProductCommand`, `UpdateProductCommand(uint Version, …)`, `VariantInput(Guid? Id, string Sku, string Name, string? Size, string? Colour, decimal Price, string Currency, decimal? CompareAtPrice, int WeightGrams, bool IsActive)`, `ImageInput(string StorageKey, string AltText)`, `CreateCategoryCommand(string Slug, string Name, string? ParentSlug)` |
| `AdminProductResults.cs` | `AdminProductView` (with `Version`, images with resolved `Url` and `StorageKey`). Outcome union: `Ok(T) \| NotFound \| Invalid(errors: field → messages) \| Conflict(field?, message)` |
| `AdminProductService.cs` | `CreateAsync`: unknown category → `Invalid(categorySlug)`. New ids from `Guid.CreateVersion7(clock.UtcNow)`. Saves through `IUnitOfWork`. `ux_products_slug` → `Conflict(slug)`; `ux_product_variants_sku` → `Conflict(variants)`.<br>`UpdateAsync`: not found → `NotFound`; `SetExpectedVersion`; check the variant list is complete (D15); apply domain methods; save. `ConcurrencyConflictException` → `Conflict(version, "This product was changed by someone else. Reload and try again.")`.<br>`PublishAsync`/`UnpublishAsync`: a `DomainException` on publish → `Conflict`.<br>`GetAsync`, `ListAsync`, `PreviewAsync`: the preview runs **one** `IInventoryReader` call, same as the public detail.<br>Any other `DomainException` → `Invalid`. Application trims strings and turns empty size or colour into `null` |
| `AdminCategoryService.cs` | Unknown parent → `Invalid(parentSlug)`. A parent that itself has a parent → `Invalid(parentSlug, "Categories can be nested one level only.")`. `ux_categories_slug` → `Conflict(slug)` |

### 4.3 Infrastructure

- **`ProductConfiguration`:** `builder.Property(p => p.Version).IsRowVersion()` (Npgsql maps `uint` to the `xmin` system column). Marco confirms in the idempotent script that **no DDL** touches `xmin`.
- **`ProductWriteStore`:** `SetExpectedVersion` sets the `Version` original value and marks `UpdatedAt` modified (D12).
- **`CategoryWriteStore`, `AdminCatalogReadStore`:** AsNoTracking. The list uses at most 2 round-trips, as the public list does.

### 4.4 Api — `Ecommerce.Catalog.Api.Admin`

`AdminProductsController` `[Route("api/v1/admin/products")] [Authorize(Policy = AuthorizationPolicies.Admin)]` and `AdminCategoriesController` `[Route("api/v1/admin/categories")]` (same policy). The existing public controllers get `[AllowAnonymous]`.

| Endpoint | Response |
|---|---|
| `GET ?search=&category=&status=&page=&pageSize=` | 200 `{ items: [{ id, slug, name, category, isActive, variantCount, activeVariantCount, price: Money \| null, updatedAt }], page, pageSize, totalCount, totalPages }` · 400 |
| `GET {id}` | 200 `AdminProduct` · 404 |
| `GET {id}/preview` | 200 `{ product: <public ProductDetail shape>, isActive, isVisibleToCustomers }` · 404 |
| `POST` | 201 + `Location: /api/v1/admin/products/{id}` + `AdminProduct` · 400 · 409 |
| `PUT {id}` | 200 `AdminProduct` · 400 · 404 · 409 |
| `POST {id}/publish`, `POST {id}/unpublish` | 200 `AdminProduct` · 404 · 409 (publish with no active variant) |
| `POST /api/v1/admin/categories` | 201 `{ slug, name, parentSlug }` · 400 · 409 |

```jsonc
// AdminProduct
{ "id": "…", "slug": "linen-shirt", "name": "Linen Shirt", "description": "…",
  "category": { "slug": "tops", "name": "Tops" }, "isActive": false, "version": 812345,
  "createdAt": "…", "updatedAt": "…",
  "variants": [ { "id": "…", "sku": "LS-M", "name": "Medium", "size": "M", "colour": null,
      "price": { "amount": "49.00", "currency": "USD" }, "compareAtPrice": null, "weightGrams": 250, "isActive": true } ],
  "images": [ { "storageKey": "products/linen-shirt/1.jpg", "altText": "Front", "sortOrder": 0, "url": null } ] }

// POST body (PUT adds "version" and optional variant "id"; variants carry "isActive" on PUT)
{ "slug": "linen-shirt", "name": "Linen Shirt", "description": "…", "categorySlug": "tops",
  "variants": [ { "sku": "LS-M", "name": "Medium", "size": "M", "colour": null,
                  "price": { "amount": "49.00", "currency": "USD" }, "compareAtPrice": null, "weightGrams": 250 } ],
  "images": [ { "storageKey": "products/linen-shirt/1.jpg", "altText": "Front" } ] }
```

**Money in:**
- `amount` must match `^\d{1,15}(\.\d{1,4})?\z`, parsed with `decimal.Parse(…, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)` (validator plus parse helper).
- `currency` must match `^[A-Z]{3}\z`.
- A `compareAtPrice` currency must equal the price currency.

**Other validator rules** match the domain limits: slug per `Slug.IsValid`, name 1–200, description ≤ 4000, variants 0–50, images 0–20, `weightGrams` 0–1,000,000.

No price is ever computed. The server stores exactly the decimal the admin typed.

---

## 5. Frontend (`frontend/apps/store/`) — StyleSheet only, naming per §5.4

| File | Contract |
|---|---|
| `lib/platform.ts` | D19. `refreshTokenTransport()`, `refreshTokenStorage.{get,set,clear}` (native: `SecureStore` key `ecommerce.refreshToken`, `keychainAccessible: WHEN_UNLOCKED_THIS_DEVICE_ONLY`) |
| `lib/api-client.ts` | Keeps `apiGet`. Adds `apiSend<T>(method: 'POST' \| 'PUT', path, { body?, signal?, auth?: 'bearer' \| 'none', refreshTransport?: boolean })`.<br>`ApiErrorKind` adds `'unauthorized' \| 'forbidden' \| 'conflict'` (401, 403, 409).<br>`setAuthHandlers({ getAccessToken, refreshAccessToken })`.<br>• **Bearer requests** (the default for `apiSend` and `apiGet`) add `Authorization: Bearer` when a token exists.<br>• **A 401 on a request that carried a token** calls `refreshAccessToken()` once (single-flight lives in the session), retries once with the new token, and on failure throws `unauthorized`.<br>• **`refreshTransport: true`** (auth endpoints on web) adds `X-Auth-Transport: cookie` and `credentials: 'include'`. Every other request uses the default credentials.<br>• **JSON bodies** get `Content-Type: application/json`.<br>• **D23:** a pre-aborted signal makes no fetch call (it throws the abort), and a timeout during the body read becomes `timeout`.<br>• **No `console.*`.** `ApiError` never stores headers or the token.<br>• `userMessage` gets copy for the new kinds ("Please log in to continue.", "You don't have access to that.", "That changed while you were editing. Reload and try again.") |
| `lib/query-client.ts` | `shouldRetry` stays false for the new kinds |
| `lib/query-keys.ts` | Adds `admin.products.all` / `.list(params)` / `.detail(id)` / `.preview(id)`, and `auth.me` |
| `features/auth/types.ts` | `UserRole = 'Customer' \| 'Support' \| 'Admin'`, `AuthUser`, `TokenResponse`, `LoginRequest`, `RegisterRequest`, `PasswordResetRequest`, `PasswordResetConfirmRequest` |
| `features/auth/api.ts` | `login`, `register`, `refresh`, `logout`, `requestPasswordReset`, `confirmPasswordReset`. Auth calls use `auth: 'none'` and `refreshTransport: refreshTokenTransport() === 'cookie'`. On native the refresh token goes in the body |
| `features/auth/session.ts` | D20. State `{ status: 'restoring' \| 'signed-out' \| 'signed-in'; user?: AuthUser }`. Functions: `subscribe`, `getSnapshot`, `startSession(TokenResponse)`, `endSession()`, `refreshSession(): Promise<boolean>` (single-flight; a 401 ends the session; a network error returns false and keeps the session), `restoreSession()` (native with no stored token → signed-out, no fetch; web → one refresh attempt through the cookie), `getAccessToken()`, `onSignedOut(listener)`. Registers `setAuthHandlers` at module load |
| `features/auth/hooks/useSession.ts`, `useLoginMutation.ts`, `useRegisterMutation.ts`, `useLogoutMutation.ts`, `usePasswordResetRequestMutation.ts`, `usePasswordResetConfirmMutation.ts` | TanStack mutations. Success calls `startSession`. Logout runs the API call best-effort, then `endSession` and `queryClient.clear()` |
| `features/auth/return-to.ts` | `safeReturnTo(raw: string \| string[] \| undefined): string` (D21) |
| `features/auth/admin-access.ts` | `adminRouteDecision(role: UserRole \| null, pathname: string): { allow: true } \| { redirect: string }` (D21) |
| `features/auth/components/AdminGate.tsx`, `LoginForm.tsx`, `RegisterForm.tsx`, `PasswordResetRequestForm.tsx`, `PasswordResetConfirmForm.tsx`, `AccountBar.tsx` | Forms: labelled inputs; `secureTextEntry` for passwords; submit disabled while pending; field errors from `fieldErrors`; plain-language error copy (D9 wording); links between Log in, Register, and Reset. Login navigates to `safeReturnTo(returnTo)`. Confirm success navigates to `/login` with "Your password was changed. Please log in." AccountBar: signed-out → Log in / Register; signed-in → name + Log out; Admin or Support → an "Admin" link |
| `features/admin-catalog/types.ts`, `api.ts`, `hooks/useAdminProductsQuery.ts`, `useAdminProductQuery.ts`, `useAdminProductPreviewQuery.ts`, `useCreateProductMutation.ts`, `useUpdateProductMutation.ts`, `usePublishProductMutation.ts`, `useCreateCategoryMutation.ts` | Mutations invalidate `admin.products.all` and `products.all`. **No optimistic updates** |
| `features/admin-catalog/components/AdminProductListView.tsx`, `ProductForm.tsx`, `VariantRowsEditor.tsx`, `ImageKeysEditor.tsx`, `AdminProductDetailView.tsx`, `CategoryCreateForm.tsx` | **List:** search, status chips, category filter, pagination (URL params as in 1a D23), "Add product" link, rows link to `/admin/products/{id}`. **ProductForm:** create and edit; prices are plain strings typed by the admin (the client does no money math); maps 400 `fieldErrors` to fields; 409 shows the conflict message with a "Reload" action. **Detail:** an Edit/Preview toggle, Publish/Unpublish, and in Preview a banner "Not visible to customers" when inactive. **CategoryCreateForm:** an inline form on the list screen |
| `features/catalog/components/ProductDetailContent.tsx` | **Extracted, with no behavior change**, from `ProductDetailView` (presentational: takes a `ProductDetail`). `ProductDetailView` keeps its props, behavior, and existing tests, which must stay unchanged and green. Preview reuses `ProductDetailContent` |
| `features/catalog/components/HomeView.tsx` | Renders `AccountBar` at the top. The existing tests stay green |
| `app/_layout.tsx` | Calls `restoreSession()` once on mount. `onSignedOut` calls `queryClient.clear()` |
| `app/admin/_layout.tsx` | New. `<AdminGate><Stack /></AdminGate>`. If headers double up, hide the parent header for the `admin` segment. **The route-test header assertions must keep passing** |
| `app/login.tsx`, `app/register.tsx`, `app/reset-password.tsx` | Thin. They keep the screen names and the headers "Log in", "Register", "Reset password". `reset-password` shows the confirm form when `token` is present, otherwise the request form |
| `app/admin/products/index.tsx`, `[id].tsx`, `new.tsx` | Thin. Headers "Admin · Products", "Admin · Product", "Admin · New product" |
| `package.json` | `expo-secure-store` pinned exact. `npx expo install --check` must stay green |

---

## 6. Tests

**TF** = test-first: write the test, run it, paste it failing for the right reason, then implement. A 404 from a missing route doesn't count as red. For concurrency TFs, first build the flow **without** the lock or version check, show the assertion failing, then add the lock.

### 6.1 `Ecommerce.Identity.Tests` (domain + application, fakes, fake clock)

| ID | Test | TF |
|---|---|---|
| ID-1 | Lockout math: 4 failures don't lock; the 5th returns true, sets `LockoutEnd = now+5m`, and resets the count. `IsLockedOut` is true at +4:59 and false at +5:00. A success resets the count. `ReplacePasswordHash` clears the lockout and changes the stamp | **TF** |
| ID-2 | `RefreshRotation.Decide`/`Rotate` table: revoked family → FamilyRevoked (even with a fresh token); consumed → Reuse and revokes the family with reason Reuse; a second revoke keeps the first reason; expired token → Expired; absolute expiry passed → Expired; valid → Rotate (presented consumed at now, `ReplacedById` set, child expiry is `min(sliding, absolute)` at both edges) | **TF** |
| ID-3 | `EmailAddress`: trims; rejects no `@`, two `@`, spaces, a trailing `\n`, 255 characters; accepts `a@b.c` | |
| ID-4 | `PasswordResetToken`: can't redeem when used, invalidated, or at or after expiry; a second `Redeem` throws | **TF** |
| ID-5 | `LoginService` (fake UoW, repos, hasher, sink): unknown email → exactly one `VerifyAgainstDummy` and `InvalidCredentials`; locked → dummy verify, count unchanged; wrong → count+1, and the sink is called on the locking attempt; success → family and token added, and the repository stored `TokenHash.Of(token)`, **never the plaintext**; `SuccessRehashNeeded` → hash upgraded; the access token is issued after the transaction completes | **TF** |
| ID-6 | `RegistrationService`: role is always Customer; a unique violation on `ux_users_email` → `DuplicateEmail`; hashing happens before `InTransactionAsync` (the fake records the order) | |
| ID-7 | `PasswordResetService`: unknown email → no sender call and the same outcome; known → `InvalidateOutstandingAsync` and an add inside the transaction, with the **sender called after the transaction returned** (the fake UoW records the order; rule 6); confirm revokes all families with `PasswordReset` and clears the lockout; an invalid token → `InvalidToken` | **TF** |
| ID-8 | `RefreshService` with fakes: Reuse → revoke, commit, and the sink called with user and family ids; FamilyRevoked and Expired → no revoke | |
| ID-9 | `JwtAccessTokenIssuer` + `JwtSigningKeyProvider` (Infrastructure, unit): claims `sub`, `role`, `jti`, `iss`, `aud`; `exp = now + 15m` from the fake clock; header `alg = HS256`; the token validates with the same key and fails with another; a key under 32 bytes is invalid; an empty key in Development gives one stable ephemeral key across calls | |

### 6.2 `Ecommerce.Catalog.Tests` additions

| ID | Test | TF |
|---|---|---|
| CD-1 | D23 lengths in `AddVariant` and `UpdateVariant`: name `""`, `"  "`, 201 characters → throw; 200 → ok; size or colour of 51 → throw, 50 → ok, null → ok; the 51st variant → throw | **TF** |
| CD-2 | `UpdateDetails`: a slug change while active throws; while inactive it works; invalid name or description throws; sets `UpdatedAt` | **TF** |
| CD-3 | `UpdateVariant`: price and compare-at rules; deactivating the last active variant of an active product throws; deactivating one of two works; deactivating the last one of an **inactive** product works; an unknown id throws | **TF** |
| CD-4 | `StorageKey`/`ReplaceImages`: rejects `../x`, `a/../b`, `/x`, `a//b`, `https://x/y.jpg`, `a\b`, a control character, 513 characters, the 21st image; accepts `dev/a b.jpg` and `products/x/1.jpg`; `sort_order` = index | |
| CD-5 | `Publish` and `Unpublish` are idempotent: a second call doesn't change `UpdatedAt` | |
| CA-1 | `AdminProductService` with fakes: unknown category → `Invalid(categorySlug)`; unique `ux_products_slug` → `Conflict(slug)`; `ux_product_variants_sku` → `Conflict(variants)`; `ConcurrencyConflictException` → `Conflict(version)`; an update missing an existing variant → `Invalid`; a variant id from another product → `Invalid`; a `DomainException` → `Invalid`; preview makes exactly one inventory call | **TF** |
| CA-2 | `AdminProductListCriteria`: defaults (status all, page 1, size 20); errors for a bad status, page 0, size 101, and search over 100 characters | |
| CA-3 | `AdminCategoryService`: parent with a parent → `Invalid`; unknown parent → `Invalid`; duplicate → `Conflict` | |

### 6.3 Integration — `Ecommerce.Api.IntegrationTests/Identity/` and `/Catalog/Admin/` (Testcontainers PG17, real HTTP)

**Setup.**
- Add `Identity/IdentityTestEnv.cs`. It creates users with known roles and passwords through the domain, `IPasswordHashing`, and `EcommerceDbContext` (never SQL), and has a `LoginAsync(role)` helper that uses the real endpoint.
- Inject a `FakeClock` where time matters.
- `ApiFactory` gets a default **random** signing key per factory (`RandomNumberGenerator`, base64) unless the test's settings override it. Every existing test keeps passing unchanged.

| ID | Test | TF |
|---|---|---|
| II-1 | Register: 201 with both tokens and `Cache-Control: no-store`; the DB role is Customer; `password_hash` ≠ the password and starts with the Identity v3 marker; a body with an extra `"role":"Admin"` → **400 and no user row**; registering `Ana@Example.TEST` after `ana@example.test` → 409 | **TF** |
| II-2 | Login: correct → 200; wrong password → 401 problem+json; unknown email → 401 with **identical** `title`/`detail`/`status` (ignoring `traceId` and `correlationId`); mixed-case email login works (proves the citext comparison on the `FOR UPDATE` path) | **TF** |
| II-3 | **Lockout under concurrency:** a counting `IPasswordHashing` decorator (via `ConfigureTestServices`) and 10 **parallel** wrong-password logins with `MaxFailedAttempts = 5` → **exactly 5** verifications against the real hash; the correct password then → 401; with `FakeClock` at +5m → 200 and the count reset | **TF** |
| II-4 | **Rotation and reuse:** login → refresh (new pair) → replay the original token → 401, family `revoked_reason = Reuse`, and the **child token from the first rotation → 401**. The log has the reuse Warning with the family id and no token text | **TF** |
| II-5 | **Concurrent refresh:** 10 parallel refreshes with the same token → **exactly one 200**; the family is revoked as Reuse; the family has ≤ 2 tokens; the token returned by the one 200 is unusable | **TF** |
| II-6 | Expiry: with `FakeClock`, a token past 14 days → 401 (no revoke); a family refreshed every 13 days → 401 once past 30 days | |
| II-7 | Logout: revokes the family, and refresh then → 401; logout twice → 204; a garbage token → 204 | |
| II-8 | **Web cookie transport:** login with `X-Auth-Transport: cookie` → `refreshToken: null` in the body, and `Set-Cookie: ecommerce_rt=…; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`; refresh with cookie and header → 200 and a rotated cookie; **the cookie without the header → 401** (the cookie is ignored, CSRF guard); logout with the header → 204 and an expired cookie | **TF** |
| II-9 | **Reset:** requests for a known and an unknown email → both 202 with identical (empty) bodies; the capturing fake sender gets exactly one call (the known user) **after commit**; the DB stores a 32-byte hash that isn't the token; confirm → 204; the old password fails and the new one works; every pre-reset family → 401 on refresh; the same token again → 400; with `FakeClock` past +30m → 400; a newer request invalidates the older token; **2 parallel confirms with one token → exactly one 204**; a reset clears an active lockout | **TF** |
| II-10 | **Role matrix (rule 11):** for every admin catalog endpoint (list, get, preview, create, update, publish, unpublish, create category): anonymous → **401**, Customer → **403**, Support → **403**, Admin → 2xx. Every 401/403 is problem+json with `traceId` and `correlationId`. `GET /auth/me` → 401 anonymous, 200 with **only the caller's** data | **TF** |
| II-11 | **Every endpoint declares auth:** from `EndpointDataSource`, every endpoint has `IAllowAnonymous` or `IAuthorizeData` metadata, and the **set of anonymous endpoints equals an explicit expected list** (public catalog GETs, health, the six anonymous auth endpoints). A new anonymous endpoint fails until it is listed. The evaluator is a pure function in the test project, with a self-test on synthetic endpoints that proves it fails (§6.2 style) | **TF** |
| II-12 | JWT hardening against `/auth/me`: a token signed with another key → 401; expired (minted with past times) → 401; `alg: none` (hand-built) → 401; wrong `iss` or `aud` → 401 | |
| II-13 | **No secrets in logs:** with `CollectingSink`, run register, login, refresh, cookie refresh, reset request and confirm, and a bad bearer request. No event's rendered message or property values contain the password, access token, refresh token, or reset token | **TF** |
| II-14 | Startup config: Testing with an empty signing key → startup fails and the message names `Identity:Jwt:SigningKey`; a 16-byte key → fails; `DevPickupDirectory` set in Testing → fails | |
| II-15 | Bootstrap: with email and password set → an Admin is created, and a second start doesn't duplicate it; an existing **Customer** with that email → still a Customer, with an Error logged (user id, no email or password) | |
| II-16 | CORS: a preflight from the allowed origin for `POST /api/v1/auth/login` (`Content-Type`, `X-Auth-Transport`) and `PUT /api/v1/admin/products/{id}` (`Authorization`) → allowed, `Access-Control-Allow-Credentials: true`, and the method list is exactly `GET, POST, PUT`; another origin → no ACAO. **`CatalogCorsTests.cs:66`** currently asserts `Allow-Methods == "GET"` for a POST preflight. It is a planned contract change (1a D21). Replace it with the exact `GET,POST,PUT` set, and also assert that DELETE is not allowed. Rob reviews it as a changed test | |
| II-17 | Schema and migration: the citext extension exists; `users.email` is `citext`; the four tables and their indexes, FKs, and CHECKs exist; an EF insert of role `Root` fails on `ck_users_role`; `MigrationTests` asserts `_AddIdentity` is applied; the snake_case model test covers the new tables; the idempotent script has no `xmin` DDL | |
| IA-1 | **Admin create:** 201 + `Location`, `isActive: false`, money as strings; the product is **not** in `GET /products` and its slug → 404 publicly ("done when"); duplicate slug → 409 `errors.slug`; a duplicate SKU across products → 409; unknown category → 400; an extra `"isActive": true` → 400; price `"-1"`, `"1.23456"`, `"abc"`, `1.5` (number) → 400; a 201-character variant name → 400 | **TF** |
| IA-2 | **Optimistic concurrency:** GET the version → PUT → 200 with a new version; PUT with the old version → 409 `errors.version`; **two parallel PUTs with the same version and a frozen `FakeClock` → exactly one 200, one 409** | **TF** |
| IA-3 | Update rules: omitting an existing variant → 400; a slug change while active → 400; deactivating the last active variant while active → 409; images are replaced in array order; a new variant with another currency → 400 | |
| IA-4 | Publish and unpublish: publish with no active variant → 409; publish → visible in the public list and detail; unpublish → public 404 immediately; both are idempotent (200, version unchanged on repeat) | |
| IA-5 | Preview of an inactive product → 200 with the public shape (active variants only, `url: null`), `isActive: false`, `isVisibleToCustomers: false`, while the public detail stays 404 | |
| IA-6 | Admin list: includes inactive products; `status=active\|inactive`; search; category including children; out-of-range page and size → 400; order is `updated_at desc, id desc` and stable across pages | |
| IA-7 | Category create: 201, and it appears in `GET /categories`; duplicate → 409; parent is a child → 400; unknown parent → 400 | |

**Existing tests:**
- They all keep passing, including `CatalogDevSeedTests` as the CS8602 regression and `HealthTests`, which must stay 200 anonymously.
- No assertion is weakened. The only planned test edit is `CatalogCorsTests.cs:66` (II-16).

### 6.4 `Ecommerce.ArchitectureTests` (coverage for the new module; same strength)

- `SolutionLayout.ExpectedModules` adds `"Identity"`.
- `LayerRuleTests`: the vacuity guard (`if (module == "Catalog")`) also covers **Identity**, for both the type check and the source scan (Domain, Application, and Api must be non-empty).
- `ClockRuleTests`: assert files from Identity `Domain` and `Application` are among the scanned files.
- `ModuleModelOwnershipTests`: assert Identity contributes an `IModuleDbModel` and all its entities are in `Ecommerce.Identity.Domain`.
- `ModuleVisibilityTests`, `ProjectReferenceRuleTests`, `AssemblyReferenceRuleTests`: no code change. They iterate modules, so Identity is covered automatically, and the review confirms it by the new test counts.
- **No rule evaluator changes.** If Marco finds he must change one, he stops and reports it (Rob reviews a changed architecture test like a weakened invariant test).

### 6.5 Frontend (Jest; `fetch` mocked with `jest.spyOn` only; `expo-secure-store` mocked with `jest.mock`)

| ID | File | Must prove | TF |
|---|---|---|---|
| F2b | `lib/api-client.test.ts` (add to it) | A bearer header is sent when a token exists and omitted for `auth: 'none'`. A 401 with a token → `refreshAccessToken` called once → the request retried with the new token → success. **Three parallel 401s → one refresh** (with the session's single-flight, tested in S-1; here a counting handler proves the client retries each once). A failed refresh → `unauthorized`, with no second retry. 403 → `forbidden`; 409 → `conflict` with `fieldErrors`. POST and PUT send a JSON body and `Content-Type`. `refreshTransport` adds `X-Auth-Transport: cookie` and `credentials: 'include'`, and plain calls don't. **Never logs the token:** every `console` method spied and never called; the token string is absent from `error.message`, `String(error)`, and `JSON.stringify(error)` after 401, 403, 500, network, and timeout failures. D23: a pre-aborted signal → no fetch; a timeout during the body read → `timeout` | **TF** |
| Q-1 | `lib/query-client.test.ts` (add to it) | `shouldRetry` is false for `unauthorized`, `forbidden`, `conflict` | |
| P-2 | `lib/platform.test.ts` | Web: transport `cookie`, and the storage functions never touch SecureStore. Native: transport `body`, and SecureStore is called with the key and `keychainAccessible` | |
| S-1 | `features/auth/session.test.ts` | Native `startSession` stores the refresh token and web doesn't. `refreshSession` is single-flight (3 calls → 1 fetch). A refresh 401 → signed-out, storage cleared, the `onSignedOut` listener fires. A network error → returns false and stays signed-in. Native restore with no stored token → signed-out with no fetch. Web restore → one cookie-transport refresh. `getSnapshot()` never contains the access token | **TF** |
| R-1 | `features/auth/return-to.test.ts` | `/checkout` → `/checkout`; `//evil.test`, `https://x`, `/\evil`, `javascript:x`, `''`, `undefined` → `/`; an array → its first value, sanitized | **TF** |
| F7a | `features/auth/admin-access.test.ts` | The full decision table: {null, Customer, Support, Admin} × {`/admin`, `/admin/products`, `/admin/products/7`, `/admin/inventory`, `/admin/orders/9`, `/admin/returns`, `/admin/customers/3`} → allow or the exact redirect | **TF** |
| F7b | `tests/app/admin.test.tsx` (`renderRouter`) | Signed out at `/admin/products` → pathname `/login`, and `returnTo` is `/admin/products`. Customer → `/`, no "Admin · Products" header, and **no `/api/v1/admin` fetch made**. Support at `/admin/products` → `/admin`; Support at `/admin/orders` → renders. Admin → the admin product list renders. Restoring → the loading indicator, with no redirect yet. Home's AccountBar shows "Admin" only for Admin and Support | |
| V-5 | `features/auth/components/*.test.tsx` | LoginForm: disabled while pending; 401 → the generic copy; success → navigates to the sanitized `returnTo`; field errors appear by field. RegisterForm: 409 copy. PasswordResetRequestForm: the **same** confirmation copy for any 202. PasswordResetConfirmForm: 400 → the invalid-link copy plus a link to request again; success → `/login` with the notice. AccountBar per role; Log out calls the API, ends the session, and clears the query cache | |
| V-7 | `features/admin-catalog/**/*.test.ts(x)` | `api.ts` paths, methods, and bodies: prices are sent **exactly as typed** (strings), with no computed field and no `isActive` on create. Hooks invalidate `admin.products.all` and `products.all` on success. ProductForm: 400 field errors are mapped, a 409 shows the conflict copy with Reload, and submit is disabled while pending (no optimistic update). AdminProductListView: the status, search, and category values reach the fetch URL, and rows link to the detail. AdminProductDetailView: the Edit/Preview toggle; the banner when inactive; Publish → POST, then refetch; a publish 409 → the message | |
| F1 | `tests/routes.test.tsx` (**update; don't weaken**) | Add a session column. Admin rows render with an Admin session set by a test helper (`tests/session.ts`, which calls `startSession` with a fake `TokenResponse`, no network) and with the admin API mocked. `login`, `register`, `reset-password`, `admin/products/index`, `[id]`, and the new `new.tsx` become `built: true`. The file-table check ignores `_layout.tsx` **at any depth** (`f.endsWith('_layout.tsx')`) and still requires every other file under `app/` to be in the table. The stub rows keep asserting "Not built yet." Not-found tests unchanged | |
| — | `ProductDetailView.test.tsx`, `HomeView.test.tsx`, existing `api-client.test.ts` cases | Unchanged and green after the `ProductDetailContent` extraction and the AccountBar addition. Each `HomeView` test renders under a signed-out session | |

---

## 7. Migration

- **One migration, `AddIdentity`.** It creates the citext extension and the four tables, with their FKs, unique indexes, indexes, and CHECKs. That is additive only, so it is backward-compatible (§10.5): the 1a image never reads these tables.
- **No DDL on `products`.** The `Version` row-version maps to the existing `xmin` system column. If the generated migration has an `xmin` `AddColumn`, the idempotent script must show that the Npgsql generator emits no SQL for it. Paste the relevant lines.
- **`Down`** drops only the four new tables. Leave the extension.
- **After generating:** run `has-pending-model-changes` (it must report no changes) and paste the `CREATE EXTENSION` and `CREATE TABLE users` statements from `migrations script --idempotent`.

---

## 8. Build order for Marco

1. Set `AGENT_STATUS.md` to BUSY. Builds: if Debug `Ecommerce.Api` is locked (Nathaniel may be running it under the VS debugger on :5080), use **`-c Release`** for every build and test command. **Never kill or stop any process.**
2. Packages: add the three packages. Pin them, then run `npx expo install --check`.
3. Projects: create the Identity and Contracts projects and the test project. Update the slnx, Dockerfile, composition, and the architecture-test expectations (§6.4), and confirm the build is green with no feature code.
4. SharedKernel and Persistence: `IUnitOfWork` and the exceptions, then `EfUnitOfWork`.
5. Domain TF: ID-1, ID-2, ID-4, CD-1, CD-2, CD-3 → red → implement → green. Then ID-3, CD-4, CD-5.
6. Application TF: ID-5, ID-7, CA-1 → red → implement → green. Then the rest of §6.1–6.2.
7. Infrastructure, the configurations, the migration (§7), and the drift check.
8. Api, host wiring (§3.7), `[AllowAnonymous]` on the public catalog and health endpoints.
9. Integration TF: II-1–5, II-8–11, II-13, IA-1, IA-2. For II-3, II-5, II-9 (parallel confirm), and IA-2, show the assertion failing with the lock or version check absent, then add it. Then the remaining II and IA tests.
10. Carry-ins: the CS8602 restructure → `dotnet build -c Release --no-incremental` shows 0 warnings → the seeder tests are green.
11. Frontend TF: F2b, S-1, R-1, F7a → red → implement. Then P-2, the components, screens, F7b, and the F1 update.
12. README (§9) and every §10 command, with real output pasted.

If an implementation detail forces a deviation from this plan, **stop and report it**. Don't silently diverge.

---

## 9. Docs and README

**Done by Rob in this plan:** the architecture doc and app context updates listed in the Plan report ("Files changed").

**Marco — `README.md` "Local development":**
- The JWT key: Development generates an ephemeral one, and you can set your own with `dotnet user-secrets set "Identity:Jwt:SigningKey" "<base64 of 32+ random bytes>" --project backend/src/Ecommerce.Api`. Say how to generate one, with no example value.
- Bootstrap an admin with user-secrets (`Identity:Bootstrap:AdminEmail`, `Identity:Bootstrap:AdminPassword`), with no example password.
- Reset links appear as files in `artifacts/dev-mail/`.
- Web login needs the API and the Expo web origin on `localhost` (the same site, for the cookie).

---

## 10. Commands (Marco pastes real output; Rob re-runs them in review)

```
dotnet build Ecommerce.slnx -c Release --no-incremental        # 0 warnings expected (CS8602 fixed)
dotnet test Ecommerce.slnx -c Release --no-build               # Docker running
dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --configuration Release --no-build
dotnet tool run dotnet-ef migrations script --idempotent --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --configuration Release --no-build --output artifacts/migrations/migrate.sql
docker build -t ecommerce-api:1b .
cd frontend/apps/store && npx expo install --check
cd frontend/apps/store && npx tsc --noEmit
cd frontend/apps/store && npm test -- --ci
cd frontend/apps/store && npx expo export --platform web
CI secret scan pattern (git grep --untracked, excluding node_modules) and the CI hygiene greps
```

**Manual smoke test** (paste the output). Use a **separate** API instance on another port (for example `--urls http://localhost:5090`) if :5080 is in use. Never stop the owner's process.
1. Run `dotnet tool run dotnet-ef database update …`, then start the API with the bootstrap admin set through env vars for that process only.
2. `curl` login as the admin → `POST /api/v1/admin/products` (no stock) → 201 inactive. Then `GET /api/v1/products/<slug>` → 404, and publish → 200.
3. `curl` register a customer → `GET /api/v1/admin/products` with the customer's token → 403.
4. Redact tokens in the pasted output: the first 8 characters, then `…`.

---

## 11. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | A citext comparison against a `text` parameter in raw `FromSql` falls back to case-sensitive `text = text` | Use an explicit `CAST(… AS citext)`. II-1 and II-2 test case-insensitivity on the LINQ path and on the lock path |
| R2 | EF identity resolution returns a stale tracked entity after a lock | The D12 rule (the first load is the `FOR UPDATE` query). II-5 and II-9 parallel tests catch it |
| R3 | Without the lock, the TF concurrency tests may pass by luck, so the red run isn't reproducible | Use 10 parallel requests and a `Task.WhenAll` start barrier. If the race won't reproduce, add a test-only delay hook in the fake hasher (II-3) and paste three runs. Never add retries |
| R4 | The JwtBearer challenge might not go through `UseStatusCodePages`, so a 401/403 ships with an empty body | II-10 asserts problem+json. Fix it with `JwtBearerEvents` and `IProblemDetailsService` (§3.7) |
| R5 | The fallback policy breaks health, CORS preflight, or the existing anonymous catalog endpoints | `[AllowAnonymous]` and `.AllowAnonymous()`. `UseCors` comes before auth. The existing tests plus II-11 and II-16 cover it |
| R6 | `UnmappedMemberHandling.Disallow` breaks an existing request path | 1a has no request bodies. Only the new DTOs are affected, and II-1 and IA-1 cover them |
| R7 | Safari doesn't accept `Secure` cookies on `http://localhost` | Dev uses Chrome or Edge. Config `Identity:RefreshTokens:Cookie:Secure` may be set false **only** in a local, non-committed override. Noted in the README |
| R8 | A cross-site production origin (an open decision) breaks `SameSite=Strict` | Recorded as a constraint on the domain decision (§13.2). The fallback is a re-plan, not `SameSite=None` by default |
| R9 | `xmin` row version emits DDL, or a frozen clock skips the version check | §7 script check. IA-2 with a frozen `FakeClock` |
| R10 | `expo-secure-store` behaves differently in Jest | Use `jest.mock('expo-secure-store')` with `jest.fn` and no new package. The P-2 native branch asserts the calls |
| R11 | The nested admin `Stack` doubles headers or changes the route-test header names | §5 `app/admin/_layout.tsx` note. F1 must stay unweakened |
| R12 | The slice is large, and the fix-round cap is 3 | The build order puts the security TFs first. The UI polish beyond the §5 contracts is non-blocking |
| R13 | Hashing inside the login transaction holds a connection for about the hash time during a login storm | Accepted at launch scale (D7). Slice 7 rate limits sit in front. Measure p95 login in Slice 7 |

---

## 12. Review gates Rob will check

- The Identity csproj references only SharedKernel, Persistence, and Identity.Contracts. Contracts holds constants only. The only public type in `Ecommerce.Identity` is `IdentityModule`. `InternalsVisibleTo` is as listed.
- No `UserManager`, `IdentityUser`, or Identity EF stores. Only the three named packages.
- Identity Application has no `Microsoft.*`, no EF, no logging, and no `DateTime.UtcNow`. Every lock flow uses `IUnitOfWork.InTransactionAsync`, follows the D12 lock order, and makes the `FOR UPDATE` query the first load.
- No plaintext refresh or reset token is persisted. No token, password, or `Authorization` header appears in any log (II-13).
- Every endpoint declares its auth (II-11). Admin endpoints have the full role matrix (II-10).
- The reset sender runs after commit, and there is no external call anywhere in 1b.
- Controllers only validate, call a service, and map. Validators exist for every write DTO. Unknown JSON fields → 400.
- The admin PUT has the version check, forced even with a frozen clock. Products are created inactive. No client money math. Prices are stored exactly as sent.
- The migration is additive only, with no `xmin` DDL and a clean drift check.
- The TF tests were shown red first, and the concurrency ones failed without their lock.
- No test was weakened. The `CatalogCorsTests:66` change is exact. The F1 filter change still covers every route file. The architecture tests have non-vacuous Identity guards.
- `dotnet build -c Release --no-incremental` shows 0 warnings. `expo install --check` is green.
- Nothing from Slice 2+ appears: no stock, audit table, outbox, rate limits, or upload.

---

## 13. Learning notes (for Nathaniel)

### 13.1 Design choices in interview form

> *"I chose X because of these constraints, gave up Y, and would switch to Z when this metric changes."* (§13.0)

**1. Short JWT and opaque, rotating refresh token, not server sessions.**
- **Why:** the API checks the JWT with no DB call, which matters most for catalog-heavy reads. The DB-backed refresh token restores what pure JWTs lose: revocation, logout, and theft detection.
- **Gave up:** instant revocation of access tokens. A stolen access token works for up to 15 minutes, and logout doesn't kill it.
- **Rejected:**
  - *Server sessions with a cookie on every request* (simplest revocation, but a DB or Redis lookup per request, and awkward on native).
  - *Long-lived JWTs* (no revocation at all).
  - *A JWT refresh token* (you still need a DB row to rotate and revoke it, so the JWT format adds nothing but size).
- **Switch when:** the 15-minute window isn't acceptable (for example admin actions after a firing). Then add a per-request security-stamp or `jti` denylist check for admin routes only, or shorten admin access tokens.

**2. Token families and strict reuse detection, with no grace window.**
- **Why:** if an attacker and the real user both hold the same refresh token, the second use of a consumed token proves that two parties have it. Revoking the family logs both out, which the attacker can't undo.
- **Gave up:** tolerance for a lost response. If the response to a refresh is lost and the client retries, the user is logged out.
- **Rejected:**
  - *Rotation without reuse detection* (a stolen token silently forks the session).
  - *A reuse interval of N seconds* (convenient, but an attacker who replays within N seconds wins).
- **Switch when:** forced-logout reports from flaky mobile networks are measured. Then add a short interval that returns the **same** child, not a new branch.

**3. Web refresh token in an httpOnly, SameSite=Strict cookie with a custom-header CSRF guard; native in SecureStore.**
- **Why:** the most damaging XSS outcome is stealing a 30-day credential, and script can't read an httpOnly cookie. The custom header forces a CORS preflight, so other origins can't ride the cookie.
- **Gave up:** one uniform transport, so the API now has two code paths. Production also needs the web app and API on the same site.
- **Rejected:**
  - *localStorage* (any XSS exfiltrates it).
  - *A cookie for native too* (native cookie jars are unreliable, and SecureStore is better there).
  - *`SameSite=None`* (reopens CSRF and needs more defenses).
- **Switch when:** the chosen domains are cross-site. Then use a backend-for-frontend on the web origin, or `SameSite=None` plus explicit CSRF tokens.

**4. Pessimistic locks for auth, optimistic versions for admin edits.**
- **Why:** a login is a millisecond critical section, where "lock, check, write" is easy to prove. An admin edit spans minutes of human think time, so you can't hold a lock across it. You **detect** the lost update with `xmin` and ask the second editor to reload.
- **Gave up:** auth throughput on a single hot account, and a smoother "merge" UX for editors.
- **Rejected:**
  - *Optimistic concurrency for lockout* (a conflict either loses a count or needs a retry loop, and verifying before the write lets parallel guesses through).
  - *Last-write-wins for admin edits* (silent data loss).
- **Switch when:** login p95 under load is dominated by lock waits. Then move the counter to an atomic `UPDATE … SET count = count + 1 RETURNING` and keep the verify-after-reserve order.

**5. Own user store plus Identity's password hasher, not `UserManager`.**
- **Why:** the hard parts that must be standard (PBKDF2 parameters, salt, format versioning, rehash on upgrade) stay Microsoft's. The parts that must fit **our** invariants (one DbContext, module-internal entities, `IClock`, row locks) are ours.
- **Gave up:** Identity's built-in features (2FA, external logins, email confirmation tokens) for free, plus some extra code (about 30 lines of lockout logic).
- **Rejected:**
  - *Full Identity EF stores* (they need framework entity types in our model and a Data Protection key ring).
  - *A hosted identity provider* (§13.1, less learning and another vendor).
- **Switch when:** SSO, social login, or 2FA lands. Re-evaluate Identity's full stack or an OIDC provider then.

**6. Fallback authorization policy, "authenticated unless `[AllowAnonymous]`".**
- **Why:** the failure mode of a forgotten attribute becomes 401, not a public admin endpoint. The endpoint-inventory test makes every new anonymous endpoint a reviewed decision.
- **Gave up:** a little ceremony on public endpoints.
- **Rejected:** opt-in `[Authorize]` (fails open).
- **Switch:** never. It is a pure safety default.

**7. Strict JSON binding (unknown fields → 400) plus explicit DTOs.**
- **Why:** mass assignment (`"role":"Admin"`, `"isActive":true`) fails loudly, and a contract drift between client and server shows up in tests.
- **Gave up:** lenient forward compatibility. Older clients that send extra fields break.
- **Switch when:** a public API with third-party clients appears. Then accept and ignore unknown fields, but only on versioned DTOs.

**8. Password reset through a sender seam called after commit, then the outbox in Slice 6.**
- **Why:** there is no provider, so nothing is invented, and rule 6 holds (the state commits first, with no external call).
- **Gave up:** guaranteed delivery. If the process crashes between commit and send, the email is lost; the user just asks again.
- **Rejected:** storing the plaintext token for a later sender (a DB read becomes account takeover).
- **Switch when:** Slice 6 lands. Then the consumer mints the token at send time.

### 13.2 Failure modes this slice defends against

| Failure | Attack or bug | Defense here | Proven by |
|---|---|---|---|
| Access-token theft | An XSS, log, or proxy leak replays a JWT | 15 min lifetime; memory-only storage; never logged; alg pinned, key ≥ 256 bits | II-12, II-13, F2b |
| Refresh-token theft | A stolen refresh token used in parallel with the victim's | Rotation + family revocation on reuse; hashed at rest; absolute 30-day cap; httpOnly cookie on web, SecureStore on native | II-4, II-6, II-8, ID-2 |
| Refresh race (benign) | Two tabs or a retry refresh at once and look like theft | Client single-flight; server family lock makes the outcome deterministic (one winner, then the family is revoked) | S-1, II-5 |
| Lockout bypass | N parallel guesses verified before the counter updates | Row lock **before** verify, so at most 5 verifications | II-3, ID-1 |
| Lockout as an oracle | "Locked" or "no account" returns faster, or with different text | Dummy verify on both paths; identical 401 body | II-2, ID-5 |
| Enumeration via reset | "No such email" response | Always 202, same body; sender only for real users | II-9, V-5 |
| Enumeration via register | 409 on an existing email | **Accepted** (D9); Slice 7 rate limits | II-1 (documents it) |
| Privilege escalation | `"role":"Admin"` on register; bootstrap email squatting | Strict JSON plus explicit DTOs; bootstrap is create-only | II-1, II-15 |
| Missing per-endpoint authorization | A new admin endpoint without `[Authorize]` | Fallback policy plus the anonymous-endpoint inventory test | II-10, II-11 |
| Per-resource authorization | Reading another user's data | `me` reads only the caller's `sub`; orders arrive in Slice 5 (test 7) | II-10 |
| CSRF on the cookie | A cross-site POST to refresh or logout | SameSite=Strict, plus the cookie only honoured with `X-Auth-Transport` (forces preflight) | II-8, II-16 |
| Reset-token replay | Reusing or racing a reset link | Single use under a lock; newest-only; 30 min; all sessions revoked | II-9, ID-4 |
| Open redirect | `/login?returnTo=//evil` | `safeReturnTo` | R-1 |
| Lost admin edit | Two admins save over each other | `xmin` version → 409 | IA-2 |
| Secrets in git | A committed signing key or admin password | Empty placeholders; Dev ephemeral key; startup fails without a key outside Dev; CI scan | II-14, secret scan |

### 13.3 What to study, and why

1. **OAuth 2.0 Security BCP (RFC 9700), the refresh-token rotation section, and the OAuth 2.0 for Browser-Based Apps draft.** This is where "rotation plus reuse detection" and "keep refresh tokens out of JS" come from. Be able to explain why the family is revoked rather than just the token.
2. **RFC 8725 (JWT Best Current Practices).** Algorithm pinning, `alg: none`, key length, and `aud`/`iss` validation. Be able to explain algorithm confusion (HS256 with an RSA public key used as the secret).
3. **OWASP cheat sheets: Authentication, Session Management, Forgot Password, CSRF.** These cover the enumeration, timing, and reset-token rules used here.
4. **NIST SP 800-63B §5.1.1.** Why length beats composition rules, and why throttling matters more than complexity.
5. **PostgreSQL docs: "Explicit Locking" (row-level locks, deadlocks) and "Concurrency Control" (READ COMMITTED).** Be able to explain why `FOR UPDATE` serializes the lockout check, why lock order prevents deadlocks, and what `xmin` is.
6. **EF Core docs: "Handling Concurrency Conflicts" and the change tracker's identity resolution.** The stale-tracked-entity trap in D12 is a real production bug class.
7. **ASP.NET Core docs: authorization policies and the fallback policy, plus JwtBearer `TokenValidationParameters`.**
8. **MDN: `Set-Cookie` (SameSite, `__Host-` prefix, Secure), CORS with credentials and preflight.** Be able to explain why a custom header defeats form-based CSRF.

### 13.4 Concepts you should be able to explain cold

- Authentication versus authorization, and endpoint-level versus resource-level authorization.
- Why access tokens are short and stateless while refresh tokens are long and stateful, and the revocation trade-off between them.
- Rotation, families, and reuse detection, including why a benign double refresh looks identical to theft.
- Why a password hash must be slow (PBKDF2, bcrypt, Argon2) while a 256-bit random token can use SHA-256.
- Timing side channels in login, and how a dummy hash equalizes them.
- Pessimistic versus optimistic concurrency, and when each is wrong.
- Lock ordering and deadlock avoidance.
- Why the side effect (reset email) happens after commit, and what the outbox adds (Slice 6).
- CSRF versus XSS: which storage each attack defeats, and why SameSite plus a custom header is enough here.
- Mass assignment, and why explicit DTOs plus strict binding beat an "ignore" list.
- Open redirects, and how to validate a return URL.
