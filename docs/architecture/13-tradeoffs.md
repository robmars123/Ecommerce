## 13. Tradeoffs

### 13.0 Constraints that drive every choice

1. **Staff path is .NET.** Depth in one stack beats breadth in three.
2. **Solo, Windows, no Mac.** Anything that needs three frontends, a Mac, or a team to operate fails on day one.
3. **Correctness under failure over feature count.** Each choice is judged first on whether it makes the invariants easier to prove.

If a constraint changes, re-argue the stack. The interview form: *"I chose X because of these constraints, gave up Y, and would switch to Z when this metric changes."*

### 13.1 Stack

| Choice | Why | Gave up | Rejected | Revisit when |
|---|---|---|---|---|
| **.NET 10 / ASP.NET Core** | Day-job and interview stack; strong typing; built-in DI, middleware, Identity | One language end to end; npm breadth; fewer copy-paste starters (more deliberate code) | Node (splits depth), Django, Rust, Spring (language friction over design) | Targeting a non-.NET staff role |
| **EF Core** | Migrations versioned with code; transactions fit checkout; Postgres locking and constraints supported | SQL visibility; N+1 risk — hence explicit `FOR UPDATE`, slow-query pass, real-Postgres tests | Dapper alone (hand-written everything, more ways to get transactions wrong) | A hot path needs hand-tuned SQL (use raw SQL there, keep EF elsewhere) |
| **PostgreSQL 17** | ACID across reservation, order, and outbox; row locks; check and unique constraints; full-text — no extra service | Horizontal write scale; dialect differs from SQL Server | MongoDB (weaker integrity for money and stock), SQL Server (valid, but managed Postgres is simpler and cheaper on DO) | Load exceeds primary + replicas, or a workload needs a dedicated engine beside it |
| **Redis 7, cache only** | Read-heavy catalog; rate-limit counters; future SignalR backplane | Simplicity; invalidation bugs; every read needs a Postgres fallback | Redis as lock or stock truth (a flush = oversell); in-memory cache (breaks with replicas) | — |
| **Expo one codebase** | Only credible way for one Windows developer to ship web + Android + iOS; stays in React; EAS removes the Mac | Web SEO vs Next.js; heavier web bundle; DOM-only libraries unavailable; SDK upgrades move together | Three native codebases (scope), Capacitor (thin wrapper, store-rejection risk), Flutter (new language), Next.js + native (two codebases) | SEO measured as the bottleneck → add a Next.js storefront on the same API |
| **TypeScript, Metro, Expo Router** | One typed API client for all platforms; file-based routing | Vite's faster web loop and plugins | — | — |
| **Tailwind via NativeWind** (owner decision 2026-11-03) | One utility vocabulary for web, iOS and Android through `className`; design tokens in one config instead of scattered `StyleSheet` literals; faster screen building | Zero-config `StyleSheet`; a build step in Babel, Metro and Jest that can break on Expo SDK upgrades; `rem` differs by platform (14px native), so exact sizes need `px` tokens | Plain `StyleSheet` (no shared tokens), Tamagui / gluestack (own component systems, heavier lock-in), Tailwind v4 + NativeWind v5 (release candidate only on 2026-11-03) | NativeWind 5 is stable and supports the current Expo SDK → move to Tailwind v4 in its own step; or NativeWind lags an Expo SDK upgrade → hold the SDK or fall back to `StyleSheet` for new code |
| **TanStack Query + Zustand** | Server state with caching and invalidation; tiny UI state without Redux | Redux devtools; needs query-key discipline | Redux (boilerplate), bare `useState` (collapses at scale) | — |
| **Identity + JWT + rotating refresh** | Tested hashing, lockout, and roles; short tokens limit leak damage | Session simplicity; revocation is not free; per-platform secure storage | Hand-rolled auth; hosted provider (less learning, extra vendor) | Need for SSO / social login at scale |
| **Stripe PaymentIntents + webhooks** | Keeps PCI scope small; mature test mode and CLI; event IDs make idempotency testable | Fees, lock-in, webhook latency (honest `PendingPayment` UI), reconciliation work | Own card handling (PCI), PayPal only, cash on delivery (skips the lesson) | Need another method → second `IPaymentGateway` |
| **`IEmailSender`, Spaces + CDN, SignalR** | Swappable provider; images out of Postgres; .NET-native real-time | Deliverability setup (SPF/DKIM) not automated; SignalR needs a backplane at scale | Images in Postgres, inline email, separate WebSocket service | — |
| **In-process workers** | One deploy, one log stream, same transaction rules | Independent scaling; shares the API process | Separate worker, broker | Background load measured as the bottleneck → same code, own process |
| **Serilog + OTel, Testcontainers** | Traceable in minutes; real-Postgres tests catch lock and constraint bugs | Slower tests, needs Docker | In-memory EF (hides exactly these bugs) | — |
| **Jest + `jest-expo` + RNTL** (owner-approved 2026-11-03) | Expo's official, SDK-pinned runner; mocks native modules; `expo-router/testing-library` renders real routes; same Babel transform as the app | Speed and native ESM of Vitest; a real device or browser | Vitest (no maintained RN/Expo preset), Detox/Maestro and Playwright E2E (device/browser infra a solo Windows dev can't run per PR) | A bug class appears that only a device or browser reproduces → add on-demand E2E against EAS / web export |
| **DigitalOcean + Docker + Actions** | Managed DB backups and failover; portable image; no deploy on red | AWS/Azure breadth | Azure (more cost and complexity at this scale), raw VPS (ops burden, backup risk) | A needed managed service is missing — Docker + standard Postgres keep the move cheap |

