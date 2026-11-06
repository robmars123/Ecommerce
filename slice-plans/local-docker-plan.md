# Local Docker stack (api + web) — plan

Owner request, in writing: "build and deploy the frontend to docker". **Local only**: everything binds to 127.0.0.1, and nothing is pushed, published or deployed. Server and cloud deploy stay in Slice 7. Production web delivery is unchanged (`expo export` → CDN, arch §2). §13.2 row: "Local Docker stack (owner request, dev only)".

**Read:** arch §2, §3.4, §10.1, §10.5 · that §13.2 row (`grep -n "Local Docker" docs/architecture/13.2-decision-log.md`) · README "Local development" · `compose.yaml`, root `Dockerfile` + `.dockerignore`, `backend/src/Ecommerce.Api/appsettings*.json`, `frontend/apps/store/app.json` (`web.output: single`), `frontend/apps/store/lib/config.ts`, `scripts/verify.sh` (`step_docker`).

## Decisions

| # | Decision | Reason | Rejected |
|---|---|---|---|
| D1 | `frontend/apps/store/Dockerfile`, context `frontend/apps/store`, with its own `.dockerignore` | The app owns its `package-lock.json`, and the root `.dockerignore` already excludes `frontend/` | A root web Dockerfile with the whole repo as context |
| D2 | `node:24.8.0-alpine` (`npm ci`, `npx expo export --platform web`) → `nginx:1.27-alpine` serving `dist/` | Node 24 = CI and the local toolchain. Base images, not packages | `npx serve` (a runtime npm package); serving the SPA from the API |
| D3 | `nginx.conf`: `listen 80`, `try_files $uri $uri/ /index.html`, `no-cache` on `index.html`, long cache on `/_expo/static/` | `output: single`, so every expo-router route falls back to `index.html` | Switching to `output: static` |
| D4 | `EXPO_PUBLIC_API_BASE_URL` is a **build arg** (default `http://localhost:5081`), set as `ENV` before the export | Expo inlines it at build time, and `config.ts` reads only that | Runtime `/env.js` injection (a code change in `config.ts`) |
| D5 | Loopback ports: **web `127.0.0.1:8082:80`**, **api `127.0.0.1:5081:8080`** | No clash with :5080 (debug API), :8081 (Expo), :5434 (Postgres); same binding as postgres | Binding 0.0.0.0, which exposes a trust-auth stack on the LAN |
| D6 | `api`: `build: .` (root Dockerfile, unchanged), `ASPNETCORE_ENVIRONMENT: Development`, `depends_on: postgres: condition: service_healthy` | Only Development allows the dev seeder (`Program.cs` guard) and an empty JWT key | A `Docker` environment, which needs a code change |
| D7 | Connection string to the service name; trust auth, so no password | Inside the container, `localhost:5434` from `appsettings.Development.json` is the container itself | Mounting an appsettings file (config drift) |
| D8 | `Identity__Jwt__SigningKey: ${JWT_SIGNING_KEY:-}` from an **untracked root `.env`**; empty = the existing Development ephemeral key | No committed secret and no setup required; with a key set, sessions survive an api restart | A committed dev key (rule 10, secret scan) |
| D9 | Bootstrap email and password from `${ADMIN_BOOTSTRAP_EMAIL:-}` / `${ADMIN_BOOTSTRAP_PASSWORD:-}` in that `.env`. A committed root `.env.example` has the three names with **empty** values | `.gitignore` ignores `.env` and keeps `!.env.example`; compose reads `.env` by default; empty = bootstrap skips (existing behaviour) | Values in `compose.yaml` |
| D10 | `Cors__AllowedOrigins__0: http://localhost:8082` | It replaces index 0 of the Development array for this container only; the host API keeps :8081 | Listing both origins |
| D11 | **The refresh cookie works with no change.** :8082 → :5081 on `http://localhost` is same-site (ports don't count), so `SameSite=Strict` is sent. Chromium and Firefox treat `http://localhost` as a secure context, so `Secure` holds. This is the same arrangement as today's :8081 → :5080 setup, which passed in 1b | No fix needed; `Cookie:Secure` stays `true` | `Cookie__Secure=false` |
| D12 | **No startup migration** (§10.5). The container shares the `postgres` service and volume with host development, so the existing host `dotnet tool run dotnet-ef database update --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api` runs before `up`. `/health/ready` reports pending migrations | Never drops data; no new code | `Database.Migrate()` at startup; a migrator container |
| D13 | Reset dev mail: `DevPickupDirectory: /tmp/dev-mail`, `LinkBaseUrl: http://localhost:8082/reset-password`; read with `docker compose exec api ls /tmp/dev-mail` | The Development path resolves to `/artifacts`, which the non-root `$APP_UID` can't create | A Windows bind mount |
| D14 | `verify.sh` `step_docker` also runs `docker build -t ecommerce-web:local frontend/apps/store`. `ci.yml` is not touched | Proves both images build; CI edits need Nathaniel in writing | Editing `ci.yml` |
| D15 | No packages are added (rule 16) | — | — |

**API `environment` (exact keys):** `ASPNETCORE_ENVIRONMENT: Development` · `ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=ecommerce;Username=ecommerce` · `Cors__AllowedOrigins__0: http://localhost:8082` · `Identity__Jwt__SigningKey: ${JWT_SIGNING_KEY:-}` · `Identity__Bootstrap__AdminEmail: ${ADMIN_BOOTSTRAP_EMAIL:-}` · `Identity__Bootstrap__AdminPassword: ${ADMIN_BOOTSTRAP_PASSWORD:-}` · `Identity__PasswordReset__LinkBaseUrl: http://localhost:8082/reset-password` · `Identity__PasswordReset__DevPickupDirectory: /tmp/dev-mail`.

## Files

| File | Change |
|---|---|
| `compose.yaml` | Add `api` and `web`. **The `postgres` service and the `ecommerce-pg17-data` volume are untouched.** `web.build: { context: frontend/apps/store, args: { EXPO_PUBLIC_API_BASE_URL: http://localhost:5081 } }`. No `api` healthcheck (the aspnet image has no curl) |
| `frontend/apps/store/Dockerfile` | New (D2, D4): lockfile copy → `npm ci` → source → `ARG`/`ENV` → export → nginx stage copies `dist` and `nginx.conf` |
| `frontend/apps/store/.dockerignore` | `node_modules`, `dist`, `.expo`, `.env*` (so `.env.local` can't leak into the build), `coverage`, `*.log` |
| `frontend/apps/store/nginx.conf` | New (D3) |
| `.env.example` (root) | New: `JWT_SIGNING_KEY=`, `ADMIN_BOOTSTRAP_EMAIL=`, `ADMIN_BOOTSTRAP_PASSWORD=` — empty values only, with comments |
| `scripts/verify.sh` | `step_docker` (D14) |
| `README.md` | Short "Local Docker stack" section: optional `.env`, `database update`, `docker compose up -d --build`, the :8082/:5081 URLs, dev mail, "local only — not a deploy" |

## Build order

1. `nginx.conf`, `Dockerfile`, `.dockerignore` → `docker build -t ecommerce-web:local frontend/apps/store`; the image contains `index.html` and `_expo/static`.
2. `compose.yaml` and `.env.example` → `docker compose config -q`.
3. The `verify.sh` change, then the README section.
4. Run the checks, then the full `bash scripts/verify.sh` and `bash scripts/verify.sh docker`.

## Checks (quote the real output)

- `docker compose up -d --build`; `docker compose ps` shows 3 services running.
- `curl -s -o /dev/null -w "%{http_code}" http://localhost:8082/` → 200; `/products` and `/admin/products` return the same `index.html`.
- `curl -s http://localhost:5081/health/ready` → 200; `curl -s "http://localhost:5081/api/v1/products?pageSize=1"` → 200 with seeded items.
- A CORS preflight to `/api/v1/auth/login` with `Origin: http://localhost:8082` returns `Access-Control-Allow-Origin: http://localhost:8082` and `Allow-Credentials: true`. With `Origin: http://evil.test` there is no ACAO header.
- Login at :8082: log in, reload, still signed in (`ecommerce_rt` was set and sent). If Marco has no browser, this is a manual check for Nathaniel.
- `git status --short` shows no `.env`; `verify.sh secrets` passes.
- Never stop the owner's :5080 or :8081 processes. Teardown is `docker compose down` only (never `-v`).

## Risks

- **Safari** may reject `Secure` cookies on `http://localhost`; local support is Chrome, Edge and Firefox (accepted, dev only).
- **Shared dev database** with the :5080 API: the seeder and the bootstrap are idempotent; tests use Testcontainers.
- **No `JWT_SIGNING_KEY`:** every api restart signs everyone out (expected).
- **Changing the API URL** means rebuilding `web` with a new build arg.
- **Image tags** must exist. If one doesn't, use the nearest pinned tag (never `latest`) and report it.
- **CI doesn't build the web image** until Nathaniel approves a `ci.yml` change.
