# Ecommerce

A single-store ecommerce app for web, Android, and iOS. It's built to production standards to practice staff-level system design: checkout, inventory, payments, and notifications stay correct under concurrency, retries, and crashes.

> **Status:** Slice 1a (Catalog browse) in review. Slice 1b (Identity + admin product create) is next.

## Stack

| | |
|---|---|
| API | .NET 10, ASP.NET Core, EF Core — modular monolith |
| Data | PostgreSQL 17 (system of record), Redis 7 (cache only) |
| App | Expo + React Native + React Native Web, TypeScript — one codebase, EAS cloud builds. Styling: Tailwind via NativeWind (decided; not yet installed — architecture §5.5) |
| Payments | Stripe PaymentIntents + signed webhooks |
| Hosting | DigitalOcean App Platform, Docker, GitHub Actions |

## Docs

| File | Purpose |
|---|---|
| [docs/ecommerce-architecture.md](docs/ecommerce-architecture.md) | Index of the architecture, one file per section in [docs/architecture/](docs/architecture/): design, invariants, data model, API, tests, slice plan, decisions |
| [docs/ecommerce-app-context.md](docs/ecommerce-app-context.md) | Product scope, roles, workflows, screens |
| [docs/quality-bar.md](docs/quality-bar.md) | Merge and release gates |
| [CLAUDE.md](CLAUDE.md) | Instructions for Claude Code sessions |
| [AGENT_STATUS.md](AGENT_STATUS.md) | Current agent activity |

## Layout

```
backend/src/Ecommerce.Api/          Host: Program.cs, middleware, observability, health, module composition
backend/src/Ecommerce.SharedKernel/ Cross-cutting abstractions (IClock, DomainException)
backend/src/Ecommerce.Persistence/  The one EcommerceDbContext + all EF migrations
backend/src/Modules/<Module>/       Ecommerce.<Module> (internal implementation) + Ecommerce.<Module>.Contracts (public contract)
backend/tests/                      Per-module unit tests, architecture tests, API integration tests (Testcontainers)
frontend/apps/store/                Expo app
docs/                               Architecture, product, and quality docs
```

## Local development

**Prerequisites:** .NET 10 SDK, Node LTS, Docker Desktop (Linux containers), Expo Go on a phone. [Stripe CLI](https://docs.stripe.com/stripe-cli) is needed from Slice 4.

```bash
docker compose up -d postgres                          # Postgres 17 only (Redis arrives in Slice 7)
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --connection "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce"
dotnet run --project backend/src/Ecommerce.Api --launch-profile http   # API on http://localhost:5080
curl http://localhost:5080/health/ready                # 200 Healthy once migrated
curl "http://localhost:5080/api/v1/products?search=shirt&sort=price-asc"   # public catalog (dev seed data, see below)
cp frontend/apps/store/.env.example frontend/apps/store/.env.local        # tells the app where the API is
cd frontend/apps/store && npm install && npx expo start         # App (press w for web)

# From Slice 4 — secrets go in user-secrets or .env (gitignored), never in committed files
dotnet user-secrets --project backend/src/Ecommerce.Api set "Stripe:SecretKey" "<test key>"
stripe listen --forward-to localhost:5080/api/v1/webhooks/stripe
```

**Catalog dev data (Slice 1a).** In the Development environment `appsettings.Development.json` sets `Catalog:DevSeed:Enabled` to `true`, so the API inserts about 24 products, 6 categories and a few deliberately hidden items on start (idempotent: it only inserts what is missing and never updates or deletes). Run `database update` first; if migrations are pending the seeder logs a warning and skips. The flag is `false` in `appsettings.json`, and the API refuses to start if it is enabled in any other environment. Product image URLs are `null` until `Catalog:Images:BaseUrl` is set (empty on purpose; the storage/CDN domain is an open decision).

**Accounts and the admin (Slice 1b).** Every API endpoint requires a signed-in user unless it is explicitly public (catalog reads, health, and the auth endpoints).

- *Signing key.* Access tokens are signed with `Identity:Jwt:SigningKey` (base64 of at least 32 random bytes). In Development, leaving it empty gives one ephemeral key per API process (a warning is logged and tokens stop working on restart). Everywhere else an empty or short key stops the API at startup. To keep a stable key locally: `dotnet user-secrets set "Identity:Jwt:SigningKey" "<base64 of 32+ random bytes>" --project backend/src/Ecommerce.Api`.
- *First admin.* Registration only ever creates customers. Set `Identity:Bootstrap:AdminEmail` and `Identity:Bootstrap:AdminPassword` through user-secrets or environment variables (never in a committed file); on start the API creates that admin if the email is unused. It never promotes an existing account, and it skips with a warning while migrations are pending.
- *Password reset links.* No email provider is chosen yet. In Development, `appsettings.Development.json` sets `Identity:PasswordReset:DevPickupDirectory`, so each reset request writes a small file with the link under `artifacts/dev-mail/` (gitignored). Elsewhere nothing is sent and a warning is logged. Setting the directory outside Development stops the API at startup.
- *Web sign-in.* The web app asks for the refresh token as an httpOnly `SameSite=Strict` cookie scoped to `/api/v1/auth` (header `X-Auth-Transport: cookie`). `localhost:8081` and `localhost:5080` count as the same site, so this works in local development with Chrome or Edge. Safari refuses a `Secure` cookie on plain http; `Identity:RefreshTokens:Cookie:Secure` is `true` in the committed config and must stay so. A production web origin on a different site than the API needs a re-plan (never `SameSite=None`).
- *Other keys.* `Identity:Jwt:AccessTokenMinutes` (15), `Identity:RefreshTokens:SlidingLifetimeDays` / `AbsoluteLifetimeDays` (14 / 30), `Identity:Lockout:MaxFailedAttempts` / `DurationMinutes` (5 / 5), `Identity:PasswordReset:TokenLifetimeMinutes` (30) and `LinkBaseUrl`.

**App to API.** The app reads `EXPO_PUBLIC_API_BASE_URL` (see `frontend/apps/store/.env.example`; copy it to the gitignored `.env.local`). There is no fallback: if it is missing the app shows "The app isn't configured to reach the store." In Expo web dev (`http://localhost:8081`) the API allows that origin through `Cors:AllowedOrigins` (Development only). On a phone with Expo Go, `localhost` is the phone itself, so set the variable to your computer's LAN IP, for example `http://192.168.1.20:5080`. The API must then listen on that address (`dotnet run --project backend/src/Ecommerce.Api --urls http://0.0.0.0:5080`) and the Expo Go origin has no browser CORS check.

**Module boundaries.** A module references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts`; its implementation types are `internal`. `Ecommerce.ArchitectureTests` makes `dotnet test` fail on any violation (architecture §6.2).

**Tests:**

```bash
dotnet test                                    # backend unit + integration — Docker must be running
cd frontend/apps/store && npm test             # frontend (Jest + React Native Testing Library)
```

### Local Docker stack

Local only, not a deploy (nothing is pushed or published; production web is `expo export` to a CDN). Optional: `cp .env.example .env` and fill `JWT_SIGNING_KEY` / admin bootstrap values (`.env` is gitignored).

```bash
docker compose up -d postgres
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --connection "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce"
docker compose up -d --build        # web http://localhost:8082  / api http://localhost:5081
docker compose exec api ls /tmp/dev-mail   # dev password-reset mail
docker compose down                 # never -v (keeps the database volume)
```

Changing the API URL means rebuilding `web` with a new `EXPO_PUBLIC_API_BASE_URL` build arg. Use Chrome, Edge or Firefox (Safari may reject the Secure refresh cookie on http://localhost).
