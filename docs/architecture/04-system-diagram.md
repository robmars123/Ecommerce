## 4. System diagram

```mermaid
flowchart TB
    Clients[Expo app<br/>Web · Android · iOS<br/>Customer / Support / Admin]
    CDN[CDN<br/>Web bundle; optional in front of images]
    API[ASP.NET Core API<br/>Modular monolith + hosted workers]
    PG[(PostgreSQL 17<br/>System of record)]
    Redis[(Redis 7<br/>Cache only)]
    Stripe[Stripe]
    Email[Email provider]

    Clients --> CDN
    Clients --> API
    Clients -- confirm payment<br/>client_secret only --> Stripe
    API --> PG
    API --> Redis
    API --> Stripe
    Stripe -- signed webhook --> API
    API --> Email
    CDN -. optional .-> API
```

- Clients never touch Postgres or Stripe secret keys. Product images are served by the API from Postgres (`image_blobs`, §2).
- The signed webhook is the only path by which the outside world changes payment state.
- Workers (expiry, outbox consumers, projectors) run in the API process in v1.


