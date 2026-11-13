## 2. Stack lock

Only Nathaniel changes the stack. Agents never do — a slice that would need a change ends `BLOCKED`.

| Layer | Choice | Notes |
|---|---|---|
| Backend | C# / ASP.NET Core, .NET 10 | Modular monolith: one API, one deployment |
| ORM | EF Core, code-first migrations | Npgsql provider |
| Database | PostgreSQL 17 | **System of record for all money, inventory, and order state** |
| Cache | Redis 7 | Cache, rate-limit counters, optional SignalR backplane. Never truth |
| Frontend | Expo + React Native + React Native Web, TypeScript, Expo Router, Metro | **One codebase** for web, Android, iOS. Store-ready, not submitted |
| Frontend state | TanStack Query (server state), Zustand (cart UI state only) | Cart is re-validated server-side at checkout |
| Frontend styling | Tailwind CSS via **NativeWind** — `nativewind` 4.2.x + `tailwindcss` 3.4.x (v3 LTS); `react-native-reanimated` (NativeWind peer) at the Expo SDK version via `npx expo install` | Owner decision (Nathaniel, 2026-11-03). `className` on web, iOS and Android from one codebase. Exact versions pinned at adoption (§5.5). Replaces plain `StyleSheet.create` as the default. Not Tailwind v4 / NativeWind v5 until v5 is stable (§13.2) |
| Auth | ASP.NET Core Identity + short-lived JWT + rotating refresh token | Roles: Customer, Support, Admin |
| Payments | Stripe PaymentIntents + signed webhooks | Test mode first |
| Email | `IEmailSender` (SMTP / Resend / SendGrid) | Provider chosen at deploy time |
| Images | PostgreSQL `bytea` (`image_blobs` table), served by the API (`GET /api/v1/images/{key}`); CDN optional in front | Owner decision (Nathaniel, 2026-11-13). Replaces DO Spaces for product images. Products store keys only; bytes live in `image_blobs` |
| Real-time | SignalR | Order status to the owning customer. Not required for correctness |
| Background work | `IHostedService` workers in the API process | Expiry, outbox consumers, projectors. No separate worker or broker in v1 |
| Observability | Serilog + OpenTelemetry | Correlation ID on every request, order ID on every order log |
| Tests | xUnit + Testcontainers (Postgres) + WebApplicationFactory | Integration tests on real Postgres, never in-memory EF |
| Frontend tests | Jest via `jest-expo` preset + React Native Testing Library (`@testing-library/react-native` + `test-renderer`) + `expo-router/testing-library` | Owner-approved amendment (Nathaniel, 2026-11-03). Expo's official runner; versions pinned to the SDK via `npx expo install`. Replaces nothing — the frontend had no runner. No Detox/Playwright/MSW in v1 (§11.3) |
| Hosting | DigitalOcean App Platform + Managed Postgres + Managed Redis | Docker image from CI. Spaces dropped for images (Nathaniel, 2026-11-13) |
| CI | GitHub Actions | Build, test, migration check, secret scan, Docker build. No deploy on red |

**Frontend delivery:** web via `expo export --platform web` → CDN. Android and iOS via **cloud EAS Build** → signed AAB / IPA. No Android Studio, local Android SDK, Xcode, or Mac. No committed `android/` or `ios/` folders. No Kotlin, Java, Swift, Capacitor, or separate React DOM app. Local testing: Expo Go on a physical phone + web export.

**Open decisions — left blank on purpose. Never invent these; use config placeholders.**
Stripe keys and webhook secret · production domain · email provider and sender · optional image CDN in front of the API (config `Catalog:Images:BaseUrl`, blank = API-relative URLs).


