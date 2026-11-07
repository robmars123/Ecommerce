## 6. Backend modules

### 6.1 Modules

| Module | Owns | Public contract | Must never |
|---|---|---|---|
| Identity | Users, roles, refresh tokens | `IUserReader`, `ICurrentUser` | Store order or payment data |
| Catalog | Products, variants, categories, image metadata, search data | `IProductReader`, `ProductPublished` | Change stock |
| Inventory | Stock, reservations | `IInventoryReader`, `IInventoryReserver` (reserve / release / commit) | Charge a card or create an order |
| Cart | User carts, guest merge, validation snapshot | `ICartReader` | Calculate final tax or shipping |
| Orders | Order aggregate, totals, status machine, shipments, returns | `IOrderReader`, `OrderPlaced`, `OrderPaid`, `OrderShipped` | Call Stripe directly |
| Payments | PaymentIntents, webhooks, refunds | `IPaymentGateway`, `PaymentSucceeded`, `PaymentFailed` | Change inventory or order items |
| Notifications | Email templates, email + SignalR consumers | Consumes events only | Write order state |
| Admin | Read models, dashboard projector, audit log | Reads via other modules' contracts | Bypass contracts to edit tables |

Each module is **two projects** (owner decision, Nathaniel, 2026-11-03 — "Module + Contracts", §13.2): `Ecommerce.<Module>` (implementation, `internal` by default) and `Ecommerce.<Module>.Contracts` (the "Public contract" column above: reader/reserver interfaces, integration events, DTOs). A contract that no other module consumes yet is added in the slice that first needs it; until then the Contracts project may be empty.

### 6.2 Repository layout and layers

```
backend/
  src/
    Ecommerce.Api/                    # host only: Program.cs, middleware, observability, health endpoints,
                                      # SystemClock, DI + module composition, module controller discovery,
                                      # design-time DbContext factory (dotnet-ef startup project)
    Ecommerce.SharedKernel/           # cross-cutting abstractions, no EF/ASP.NET/Npgsql: IClock, DomainException,
                                      # later Result/errors, shared Money, outbox/unit-of-work abstractions
    Ecommerce.Persistence/            # the one EcommerceDbContext, IModuleDbModel, DatabaseOptions,
                                      # Postgres readiness check, the single migration stream (Migrations/);
                                      # later the outbox/idempotency/audit platform tables (§7.1)
    Modules/
      Catalog/
        Ecommerce.Catalog/            # CatalogModule.cs (the only public type) +
                                      #   Domain/ Application/ Infrastructure/ (EF configs, read store, dev seed) Api/ (controllers)
        Ecommerce.Catalog.Contracts/  # public: IProductReader (Slice 2: variant lookup + admin variant page), ProductPublished, …
      Inventory/
        Ecommerce.Inventory/          # InventoryModule.cs + Domain/ Application/ Infrastructure/ (stock reader, row locks, expiry worker) Api/ (admin)
        Ecommerce.Inventory.Contracts/ # public: IInventoryReader, StockStatus, IInventoryReserver (reserve in Slice 2, commit in Slice 4)
      Identity/
        Ecommerce.Identity/           # IdentityModule.cs + Domain/ (User, refresh families/tokens, reset tokens) Application/
                                      #   Infrastructure/ (EF configs, repositories, JWT, password hashing) Api/ (auth controller)
        Ecommerce.Identity.Contracts/ # public: IdentityRoles, AuthorizationPolicies (later IUserReader, ICurrentUser)
      <Module>/ …                     # Cart, Orders, Payments, Notifications, Admin — same shape when their slice lands
  tests/
    Ecommerce.<Module>.Tests/         # per-module unit tests: Domain/ and Application/ (fakes), InternalsVisibleTo
    Ecommerce.ArchitectureTests/      # reference, visibility, layer, clock, and model-ownership rules (below)
    Ecommerce.Api.IntegrationTests/   # WebApplicationFactory + Testcontainers Postgres, real HTTP
frontend/
  apps/store/                         # Expo app (§5)
docs/
```

**Project reference rules** (compiler- and test-enforced; a violation fails `dotnet test`):

| Project | May reference (projects) | Packages / frameworks |
|---|---|---|
| `Ecommerce.SharedKernel` | nothing | none |
| `Ecommerce.<M>.Contracts` | `Ecommerce.SharedKernel` only | none |
| `Ecommerce.Persistence` | `Ecommerce.SharedKernel` | EF Core, Npgsql, health checks |
| `Ecommerce.<M>` | `SharedKernel`, `Persistence`, its own `Contracts`, other modules' `*.Contracts` — **never another module's implementation project, never `Ecommerce.Api`** | as its slice plan names; `Microsoft.AspNetCore.App` framework reference for controllers |
| `Ecommerce.Api` (host) | everything in `src/`; it must reference every module | Serilog, EF Core Design (design-time only, `PrivateAssets=all`) |

**Inside a module** the layers are namespaces/folders, `Ecommerce.<M>.Domain` / `.Application` / `.Infrastructure` / `.Api`. Dependencies point one way: **Api → Application → Domain**; Infrastructure implements Application interfaces; Domain depends only on `SharedKernel` and itself (not even its own or another module's Contracts); Application may also use Contracts (its own and other modules'); Api (controllers) never uses Infrastructure or EF. Only `<M>Module.cs` at the module root wires them together. A `using` that points the wrong way means the slice is wrong.

**Visibility.** Every implementation type is `internal`. The only public type in `Ecommerce.<M>` is the static `<M>Module` class: `Add<M>Module(IServiceCollection, IConfiguration)` plus any host-facing extras (Catalog: `DevSeedEnabledKey`, `AddCatalogDevSeed()`). `InternalsVisibleTo` is granted only to `Ecommerce.<M>.Tests` and, where HTTP fixtures need domain factories, `Ecommerce.Api.IntegrationTests` — never to another module. Controllers are internal; the host's `ModuleComposition.AddModuleControllers()` adds each module assembly (`ModuleComposition.ModuleAssemblies`) as an application part and registers `ModuleControllerFeatureProvider`, which discovers their internal `[ApiController]` types.

**Persistence.** One `EcommerceDbContext` (in `Ecommerce.Persistence`), one migration stream, one `__EFMigrationsHistory`, `public` schema, table names unchanged. Each module contributes its EF configuration through an internal `IModuleDbModel` registered by `Add<M>Module`; the context applies every registered model. Entity types are `internal` to their module, so no module can name — let alone `Set<T>()` — another module's entity (rule 8 by the compiler). Checkout (§8.2) gets Inventory + Orders + Payments + outbox in one transaction because every module in a request scope shares the same scoped context and connection; the transaction abstraction (`SharedKernel/Persistence/IUnitOfWork`, implemented by `Persistence/EfUnitOfWork`, which also translates concurrency and unique-constraint errors) landed early in Slice 1b, because Identity needs row locks. Slices 2 and 4 extend it. Migrations: `dotnet-ef … --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`.

**Architecture tests** (`Ecommerce.ArchitectureTests`, reflection + csproj/source scan, no extra package): project-reference table above (csproj `ProjectReference` and compiled assembly references); SharedKernel/Contracts free of EF, ASP.NET Core, Npgsql, Serilog, Stripe, Redis; the only exported type of a module implementation is `<M>Module`; `InternalsVisibleTo` only to allowed test assemblies; per-module layer rules (type signatures + source scan, no `global using` in modules); controllers live only in `Ecommerce.<M>.Api`; each `IModuleDbModel` configures only its own module's entities, all in `.Domain`; the host composes every module; no system-clock reads outside `Ecommerce.Api` (rule 9). Each rule evaluator has a self-test that proves it fails on a violating input.


