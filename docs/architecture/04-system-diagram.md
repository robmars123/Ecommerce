## 4. System diagram

```mermaid
flowchart TB
    Clients[Expo app<br/>Web · Android · iOS<br/>Customer / Support / Admin]
    CDN[CDN<br/>Web bundle + product images]
    API[ASP.NET Core API<br/>Modular monolith + hosted workers]
    PG[(PostgreSQL 17<br/>System of record)]
    Redis[(Redis 7<br/>Cache only)]
    Stripe[Stripe]
    Email[Email provider]
    Spaces[Object storage]

    Clients --> CDN
    Clients --> API
    Clients -- confirm payment<br/>client_secret only --> Stripe
    API --> PG
    API --> Redis
    API --> Stripe
    Stripe -- signed webhook --> API
    API --> Email
    API --> Spaces
    Spaces --> CDN
```

- Clients never touch Postgres, Stripe secret keys, or object storage credentials.
- The signed webhook is the only path by which the outside world changes payment state.
- Workers (expiry, outbox consumers, projectors) run in the API process in v1.


