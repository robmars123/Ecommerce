# Slice 1b - Identity + admin product create: Marco report

Fix round 1: see Round 1 - Marco to Rob in slice-1b-rob-review.md. Mutations M1-M10 run and reverted; IA-2 variant-only test added (needed whole-second test clock); bootstrap password policy enforced (II-15 case); :5090 smoke passed. Integration tests now 120; verify.sh PASS.

Concerns:
1. Plan gap: Section 5 says `usePasswordResetConfirmMutation` success -> `startSession`, but `password-reset/confirm` is 204 with no tokens and revokes every session. Safe minimum: no sign-in; success goes to `/login?notice=password-changed` ("Your password was changed. Please log in."). Rob to confirm.
2. Unplanned host change, `Program.cs`: `app.MapFallback(404).AllowAnonymous()`. The fallback policy turned every unknown route into 401 (ProblemDetailsTests, CorrelationIdTests went red). Side effect: a wrong-method request to a real route is now 404, not 405. II-11's anonymous list includes `{*path:nonfile}`. Rob to decide if acceptable.
3. TF evidence: now supplied as mutation runs (see thread). Unit tests from the stopped build are still not seen red.
4. Not run by me: `docker build` (Rob ran it). The :5090 smoke was run in round 1. The idempotent script was generated: `CREATE EXTENSION IF NOT EXISTS citext`, `CREATE TABLE users`, no products or xmin DDL in AddIdentity.
5. `resetSession()` is exported from `features/auth/session.ts` for tests only. Hooks live one per file; `useSession` is in `hooks/useSession.ts`.

Bugs found: None beyond the two below (fixed).
Bugs fixed:
- Unknown route 401 instead of 404 -> `Program.cs` fallback; ProblemDetailsTests + CorrelationIdTests pass.
- Test DB connection exhaustion ("too many clients", then port exhaustion) once more test classes ran in parallel -> `PostgresFixture` small pools with 2 s idle lifetime. 3 consecutive `verify.sh test` runs green.
Bugs open: None

Tests changed:
- `Catalog/CatalogCorsTests.cs:66` (planned): GET-only -> exact set GET, POST, PUT, plus DELETE not advertised.
- `Catalog/CatalogSchemaTests.cs` snake_case test (unplanned): expected table list now also contains the 4 identity tables. Still an exact list.
- `tests/routes.test.tsx` (planned): session column, new rows built, `_layout.tsx` ignored at any depth, `jest.mock('expo-secure-store')`, admin API mocked, `resetSession` afterEach. Every app/ file is still required.
- `PostgresFixture.cs`: pool settings only (test infrastructure).

Changed (slice 1b):
- Backend: new Identity module (Contracts + implementation, Domain/Application/Infrastructure/Api, JWT, refresh families, lockout, password reset, admin bootstrap), `IUnitOfWork`/`EfUnitOfWork`, AddIdentity migration, Catalog admin (domain rules, services, stores, `Api/Admin` controllers), host (CORS credentials, auth, fallback), arch tests extended.
- Backend tests added: Identity.Tests (domain, services, `JwtTests` ID-9), Catalog.Tests (admin domain/services), integration `Identity/` (AuthFlow II-1..8, PasswordReset II-9, Authorization II-10..12, IdentityHost II-13..17), `Catalog/Admin/` (IA-1..7).
- Frontend: `lib/platform.ts`, `api-client` (apiSend, bearer, 401 refresh-retry, new kinds, D23), `query-keys`, `features/auth/*`, `features/admin-catalog/*`, `ProductDetailContent` extracted, shared `TextField/ActionButton/ScreenTitle/FormScreen`, screens login/register/reset-password/admin products (+ `new.tsx`), root and admin layouts, HomeView AccountBar, `expo-secure-store` 58.0.2 pinned.
- README: Slice 1b section. Nothing in docs/ or quality-bar edited.

verify.sh:
- secrets PASS no matches; hygiene PASS no matches
- build PASS 0 Error(s) | 0 Warning(s)
- test PASS: Catalog 166, Inventory 3, Architecture 25, Identity 73, Integration 118, 0 failed
- drift PASS no changes since last migration; expo-check PASS up to date; tsc PASS clean
- jest PASS 45 suites, 277 tests; export PASS
- VERIFY PASS (secrets hygiene build test drift expo-check tsc jest export)

Quality-bar §2:
- [x] Module boundaries: Identity refs only SharedKernel/Persistence/Identity.Contracts; Catalog refs Identity.Contracts; types internal; ArchitectureTests green (25); controllers thin.
- [x] Money/stock tests: no stock movement. Admin prices parsed as exact decimal strings, tested (IA-1 bad prices, form-model, "1.50" kept).
- [x] Frontend co-located tests for new lib/features exports and components, plus F2b, S-1, R-1, F7a/b, P-2, V-5, V-7, F1. No `.only`/`.skip`, no tests under app/. Gaps: query hooks are covered via view tests, not their own files.
- [x] IClock everywhere (JWT lifetime, tokens, lockout via IClock; tests use FakeClock).
- [x] Prices server-side only; client sends typed strings, no math.
- [x] Idempotency/outbox: N/A for external state changes. The reset email is behind `IPasswordResetLinkSender`, called after commit; no real provider (D10).
- [x] Commands run, output quoted above.
- [x] Open decisions as placeholders: signing key and bootstrap credentials empty in config; no email provider; image BaseUrl still empty.
- [x] Docs: README updated; no rule or decision changed.
- [x] Plain-language explanation above.
