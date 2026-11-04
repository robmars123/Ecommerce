# Structural refactor — Module + Contracts projects: Plan

**Author:** Rob (architect) · **Date:** 2026-11-03 · **Verdict: READY**
**Owner decision:** Nathaniel chose the "Module + Contracts" layout (architecture §6.2, §13.2). This is **not a slice** — it does not start 1b, adds no feature, and must not change behaviour. One deployable modular monolith; stack lock (§2) unchanged; no new NuGet/npm package.

**Non-negotiable acceptance (Rob re-checks every one in review):**

1. Same HTTP contract — the dev-DB responses captured in `artifacts/refactor-baseline/*.json` are identical after the refactor (only `traceId` may differ).
2. Every one of the 172 baseline tests still exists and passes (moved, never weakened; assertions unchanged); total count only grows. Baseline list: `artifacts/refactor-baseline/tests.txt`.
3. `has-pending-model-changes` clean, and the idempotent migration script is **byte-identical** to `artifacts/refactor-baseline/migrate.sql`.
4. The dev DB (`127.0.0.1:5434`) still works: `database update` applies nothing; `/health/ready` 200.
5. `docker build` succeeds. Frontend untouched (no file under `frontend/` edited).
6. No git write command. Move files with plain filesystem moves (`mv`/`Move-Item`), **not** `git mv` (it writes the index).

Baseline already captured by Rob (2026-11-03, pre-refactor tree, `artifacts/` is gitignored):

| File | What |
|---|---|
| `artifacts/refactor-baseline/tests.txt` | 172 fully-qualified test names (`dotnet test --list-tests`): Domain 44 · Application 48 · Integration 80 |
| `artifacts/refactor-baseline/migrate.sql` | `migrations script --idempotent` output (164 lines) |
| `artifacts/refactor-baseline/{products,products-search,products-category,categories,product-blue-shirt,products-400,product-404}.json` | Dev API responses: `/api/v1/products?pageSize=100`, `?search=shirt&sort=price-asc`, `?category=clothing&page=2&pageSize=5`, `/api/v1/categories`, `/api/v1/products/blue-shirt`, `?pageSize=0` with `X-Correlation-ID: baseline-1`, `/api/v1/products/retired-scarf` with `X-Correlation-ID: baseline-2` |

---

## 1. Target layout

```
backend/src/
  Ecommerce.Api/                         host (Microsoft.NET.Sdk.Web)
    Program.cs
    Composition/ModuleComposition.cs     AddEcommerceModules + ModuleAssemblies + AddModuleControllers
    Composition/ModuleControllerFeatureProvider.cs
    Composition/DesignTimeDbContextFactory.cs
    Health/HealthEndpoints.cs
    Observability/{CorrelationId,CorrelationIdMiddleware,SerilogSetup}.cs
    Time/SystemClock.cs
    Properties/launchSettings.json, appsettings*.json   (unchanged)
  Ecommerce.SharedKernel/
    Time/IClock.cs
    Domain/DomainException.cs
  Ecommerce.Persistence/
    EcommerceDbContext.cs
    IModuleDbModel.cs
    DatabaseOptions.cs
    PersistenceServiceCollectionExtensions.cs
    Health/HealthCheckTags.cs
    Health/PostgresReadinessHealthCheck.cs
    Migrations/ (InitialBaseline, AddCatalog, snapshot)
  Modules/
    Catalog/
      Ecommerce.Catalog/
        CatalogModule.cs                 the ONLY public type
        Domain/{Category,Product,ProductImage,ProductVariant,Slug}.cs
        Application/{CatalogBrowseService,CatalogResults,CatalogRows,ICatalogReadStore,IImageUrlResolver,ProductSearchCriteria,ProductSort,StockStatusRollup}.cs
        Infrastructure/{CatalogImageOptions,CatalogReadStore,ConfiguredImageUrlResolver}.cs
        Infrastructure/Persistence/{CatalogDbModel,CategoryConfiguration,ProductConfiguration,ProductImageConfiguration,ProductVariantConfiguration}.cs
        Infrastructure/DevSeed/{CatalogDevSeedData,CatalogDevSeedOptions,CatalogDevSeeder,CatalogDevSeedHostedService}.cs
        Api/{CatalogResponses,CategoriesController,ProductsController}.cs
      Ecommerce.Catalog.Contracts/       csproj only — no types yet (IProductReader arrives in Slice 3, ProductPublished when first consumed)
    Inventory/
      Ecommerce.Inventory/
        InventoryModule.cs               the ONLY public type
        Infrastructure/UnknownInventoryReader.cs
      Ecommerce.Inventory.Contracts/
        IInventoryReader.cs
        StockStatus.cs
backend/tests/
  Directory.Build.props, test.runsettings    (unchanged)
  Ecommerce.Catalog.Tests/            Domain/ + Application/ unit tests (moved)
  Ecommerce.Inventory.Tests/          UnknownInventoryReader tests (new)
  Ecommerce.ArchitectureTests/        all architecture + clock rules (moved + new)
  Ecommerce.Api.IntegrationTests/     unchanged location; usings updated
```

Deleted after their content moves: `backend/src/Ecommerce.Domain/`, `Ecommerce.Application/`, `Ecommerce.Infrastructure/`, `backend/tests/Ecommerce.Domain.Tests/`, `Ecommerce.Application.Tests/` (projects and their `bin/obj`). Deleting moved source is not a data change.

---

## 2. Project files (exact)

Root `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `backend/tests/Directory.Build.props`, `test.runsettings`: **unchanged** (no new package versions; every package below is already pinned centrally).

**`backend/src/Ecommerce.SharedKernel/Ecommerce.SharedKernel.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

**`backend/src/Ecommerce.Persistence/Ecommerce.Persistence.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Ecommerce.SharedKernel\Ecommerce.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" />
  </ItemGroup>
</Project>
```

**`backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/Ecommerce.Inventory.Contracts.csproj`** and **`backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/Ecommerce.Catalog.Contracts.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\Ecommerce.SharedKernel\Ecommerce.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

**`backend/src/Modules/Inventory/Ecommerce.Inventory/Ecommerce.Inventory.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Ecommerce.SharedKernel\Ecommerce.SharedKernel.csproj" />
    <ProjectReference Include="..\Ecommerce.Inventory.Contracts\Ecommerce.Inventory.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Ecommerce.Inventory.Tests" />
  </ItemGroup>
</Project>
```
(The framework reference gives `IServiceCollection`/`IConfiguration`; it is the shared framework, not a package. Inventory does not reference Persistence until Slice 2 needs it.)

**`backend/src/Modules/Catalog/Ecommerce.Catalog/Ecommerce.Catalog.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Ecommerce.SharedKernel\Ecommerce.SharedKernel.csproj" />
    <ProjectReference Include="..\..\..\Ecommerce.Persistence\Ecommerce.Persistence.csproj" />
    <ProjectReference Include="..\Ecommerce.Catalog.Contracts\Ecommerce.Catalog.Contracts.csproj" />
    <ProjectReference Include="..\..\Inventory\Ecommerce.Inventory.Contracts\Ecommerce.Inventory.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Ecommerce.Catalog.Tests" />
    <InternalsVisibleTo Include="Ecommerce.Api.IntegrationTests" />
  </ItemGroup>
</Project>
```
Module projects use `Microsoft.NET.Sdk` (not `.Web`) so no ASP.NET implicit global usings leak into `Domain/`.

**`backend/src/Ecommerce.Api/Ecommerce.Api.csproj`** — keep the `UserSecretsId` exactly; replace the references:
```xml
<ItemGroup>
  <ProjectReference Include="..\Ecommerce.SharedKernel\Ecommerce.SharedKernel.csproj" />
  <ProjectReference Include="..\Ecommerce.Persistence\Ecommerce.Persistence.csproj" />
  <ProjectReference Include="..\Modules\Catalog\Ecommerce.Catalog\Ecommerce.Catalog.csproj" />
  <ProjectReference Include="..\Modules\Inventory\Ecommerce.Inventory\Ecommerce.Inventory.csproj" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Serilog.AspNetCore" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
</ItemGroup>
```
(`Design` is required in the dotnet-ef **startup** project; decision D7.)

**Test projects** (same shape as today: `OutputType Exe`, `IsPackable false`, `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`, `<Using Include="Xunit" />`):

| Project | ProjectReference | Extra |
|---|---|---|
| `backend/tests/Ecommerce.Catalog.Tests` | `..\..\src\Modules\Catalog\Ecommerce.Catalog\Ecommerce.Catalog.csproj` | — |
| `backend/tests/Ecommerce.Inventory.Tests` | `..\..\src\Modules\Inventory\Ecommerce.Inventory\Ecommerce.Inventory.csproj` | — |
| `backend/tests/Ecommerce.ArchitectureTests` | `..\..\src\Ecommerce.Api\Ecommerce.Api.csproj` | `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (for `ControllerBase`) |
| `backend/tests/Ecommerce.Api.IntegrationTests` | unchanged (`Ecommerce.Api`) | unchanged |

**`Ecommerce.slnx`**
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="backend/src/Ecommerce.Api/Ecommerce.Api.csproj" />
    <Project Path="backend/src/Ecommerce.Persistence/Ecommerce.Persistence.csproj" />
    <Project Path="backend/src/Ecommerce.SharedKernel/Ecommerce.SharedKernel.csproj" />
  </Folder>
  <Folder Name="/src/Modules/" />
  <Folder Name="/src/Modules/Catalog/">
    <Project Path="backend/src/Modules/Catalog/Ecommerce.Catalog/Ecommerce.Catalog.csproj" />
    <Project Path="backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/Ecommerce.Catalog.Contracts.csproj" />
  </Folder>
  <Folder Name="/src/Modules/Inventory/">
    <Project Path="backend/src/Modules/Inventory/Ecommerce.Inventory/Ecommerce.Inventory.csproj" />
    <Project Path="backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/Ecommerce.Inventory.Contracts.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="backend/tests/Ecommerce.Api.IntegrationTests/Ecommerce.Api.IntegrationTests.csproj" />
    <Project Path="backend/tests/Ecommerce.ArchitectureTests/Ecommerce.ArchitectureTests.csproj" />
    <Project Path="backend/tests/Ecommerce.Catalog.Tests/Ecommerce.Catalog.Tests.csproj" />
    <Project Path="backend/tests/Ecommerce.Inventory.Tests/Ecommerce.Inventory.Tests.csproj" />
  </Folder>
</Solution>
```
(If `dotnet sln`/the slnx parser rejects the empty `/src/Modules/` folder element, drop that one line — nested folder names alone are valid.)

**`Dockerfile`** — restore layer lists every src project:
```dockerfile
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY backend/src/Ecommerce.SharedKernel/Ecommerce.SharedKernel.csproj backend/src/Ecommerce.SharedKernel/
COPY backend/src/Ecommerce.Persistence/Ecommerce.Persistence.csproj backend/src/Ecommerce.Persistence/
COPY backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/Ecommerce.Catalog.Contracts.csproj backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/
COPY backend/src/Modules/Catalog/Ecommerce.Catalog/Ecommerce.Catalog.csproj backend/src/Modules/Catalog/Ecommerce.Catalog/
COPY backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/Ecommerce.Inventory.Contracts.csproj backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/
COPY backend/src/Modules/Inventory/Ecommerce.Inventory/Ecommerce.Inventory.csproj backend/src/Modules/Inventory/Ecommerce.Inventory/
COPY backend/src/Ecommerce.Api/Ecommerce.Api.csproj backend/src/Ecommerce.Api/
RUN dotnet restore backend/src/Ecommerce.Api/Ecommerce.Api.csproj
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Ecommerce.Api/Ecommerce.Api.csproj -c Release -o /app/publish --no-restore
```
Runtime stage unchanged (`Ecommerce.Api.dll`). **`.dockerignore`**: unchanged (it already excludes `backend/tests/`, `docs/`, `frontend/`, `slice-plans/`, `artifacts/`, `bin`, `obj`; nothing new needs excluding).

**`.github/workflows/ci.yml`** — only the two dotnet-ef steps change:
```yaml
      - name: Migration drift check
        run: dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --configuration Release --no-build
      - name: Idempotent migration script
        run: dotnet tool run dotnet-ef migrations script --idempotent --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --configuration Release --no-build --output artifacts/migrations/migrate.sql
```
Restore/build/test steps already target `Ecommerce.slnx` — unchanged.

---

## 3. Contracts and composition (exact signatures)

### 3.1 SharedKernel
- `namespace Ecommerce.SharedKernel.Time; public interface IClock { DateTimeOffset UtcNow { get; } }` (unchanged body).
- `namespace Ecommerce.SharedKernel.Domain; public sealed class DomainException(string message) : Exception(message);` (unchanged body).
- Nothing else. No `Result`/`Money` types now (no consumer yet — no dead code).

### 3.2 Persistence
```csharp
namespace Ecommerce.Persistence;

/// A module's EF model contribution. Implemented internally by each module; registered by Add<M>Module.
public interface IModuleDbModel
{
    void Configure(ModelBuilder modelBuilder);
}

public sealed class EcommerceDbContext : DbContext
{
    private readonly IReadOnlyList<IModuleDbModel> _modules;

    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options, IEnumerable<IModuleDbModel> modules)
        : base(options)
    {
        _modules = modules.ToList();
        if (_modules.Count == 0)
            throw new InvalidOperationException("No module models are registered; the EF model would be empty.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var module in _modules) module.Configure(modelBuilder);
    }
}
```
- `DatabaseOptions` (public, unchanged body).
- `PersistenceServiceCollectionExtensions` (public static):
  - `AddEcommercePersistence(this IServiceCollection services)` — the existing `DatabaseOptions` registration (`Configure<IConfiguration>`, `Validate`, message `"ConnectionStrings:Postgres is not configured."`, `ValidateOnStart`) and `AddDbContext<EcommerceDbContext>((sp, o) => o.UseNpgsql(...))` — moved verbatim from `Infrastructure/DependencyInjection.cs`. **No** `MigrationsAssembly` call (migrations live in the context's assembly).
  - `AddPostgresReadinessCheck(this IHealthChecksBuilder builder)` — moved verbatim (name `"postgres"`, `Unhealthy`, tag `HealthCheckTags.Ready`, 5 s timeout).
- `Health/HealthCheckTags` public (host uses it); `Health/PostgresReadinessHealthCheck` **internal** (body unchanged).
- Model cache: the model is built once per context type; every registration in a process supplies the same module set, so the default cache key is correct. Note for future work: never vary the module set per scope.

### 3.3 Inventory
```csharp
// Ecommerce.Inventory.Contracts (public, bodies unchanged)
namespace Ecommerce.Inventory.Contracts;
public interface IInventoryReader { Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct); }
public enum StockStatus { Unknown, InStock, Low, Out }   // order unchanged — wire values are names via JsonStringEnumConverter

// Ecommerce.Inventory
namespace Ecommerce.Inventory;
public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Slice 2 replaces this registration with the inventory_stock reader.
        services.AddSingleton<IInventoryReader, UnknownInventoryReader>();
        return services;
    }
}
// Infrastructure/UnknownInventoryReader.cs: namespace Ecommerce.Inventory.Infrastructure; internal sealed class (body unchanged)
```
`configuration` is unused today; keep the parameter for the uniform module signature (owner-specified), and suppress nothing — an unused parameter is not a warning in C#.

### 3.4 Catalog
```csharp
namespace Ecommerce.Catalog;

public static class CatalogModule
{
    public const string DevSeedEnabledKey = CatalogDevSeedOptions.EnabledKey;   // "Catalog:DevSeed:Enabled"

    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, CatalogDbModel>());
        // then, verbatim from Infrastructure/DependencyInjection.cs:
        //   CatalogImageOptions (Configure<IConfiguration> from "Catalog:Images:BaseUrl", Validate with the same message, ValidateOnStart)
        //   IImageUrlResolver -> ConfiguredImageUrlResolver (singleton)
        //   ICatalogReadStore -> CatalogReadStore (scoped)
        //   CatalogBrowseService (scoped), CatalogDevSeeder (scoped)
        return services;
    }

    /// Registers the startup seeder. The host decides whether the environment allows it.
    public static IServiceCollection AddCatalogDevSeed(this IServiceCollection services)
    {
        services.AddHostedService<CatalogDevSeedHostedService>();
        return services;
    }
}

// Infrastructure/Persistence/CatalogDbModel.cs
internal sealed class CatalogDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CatalogDbModel).Assembly,
            t => t.Namespace == typeof(CatalogDbModel).Namespace);
}
```
Every other Catalog type becomes `internal` (domain entities, `Slug`, application service/records/ports/enum/`ProductSortNames`, infrastructure, dev seed, controllers, response records and `CatalogResponseMapping`). Members keep their current accessibility keywords (a `public` member of an `internal` type is fine). `ProductImage.Create` / `ProductVariant.Create` stay `internal`.

**Compile gotcha:** in `Category.cs` and `Product.cs` replace `Catalog.Slug.IsValid(slug)` with `Domain.Slug.IsValid(slug)` (inside namespace `Ecommerce.Catalog.Domain`, the simple name `Catalog` now binds to namespace `Ecommerce.Catalog`). No other body changes.

`CatalogDevSeedHostedService` moves to `Infrastructure/DevSeed/`, becomes `internal sealed`, body unchanged (it uses `EcommerceDbContext` from `Ecommerce.Persistence`, `IClock` from SharedKernel).

### 3.5 Host
```csharp
// Composition/ModuleComposition.cs
namespace Ecommerce.Api.Composition;
public static class ModuleComposition
{
    public static readonly IReadOnlyList<Assembly> ModuleAssemblies =
        [typeof(CatalogModule).Assembly, typeof(InventoryModule).Assembly];

    public static IServiceCollection AddEcommerceModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddCatalogModule(configuration).AddInventoryModule(configuration);

    public static IMvcBuilder AddModuleControllers(this IMvcBuilder mvc) =>
        mvc.ConfigureApplicationPartManager(m =>
        {
            foreach (var assembly in ModuleAssemblies)
                if (!m.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == assembly))
                    m.ApplicationParts.Add(new AssemblyPart(assembly));
            m.FeatureProviders.Add(new ModuleControllerFeatureProvider(ModuleAssemblies));
        });
}

// Composition/ModuleControllerFeatureProvider.cs — adds internal [ApiController] types of module assemblies
internal sealed class ModuleControllerFeatureProvider(IReadOnlyCollection<Assembly> moduleAssemblies) : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo t) =>
        base.IsController(t) ||
        (moduleAssemblies.Contains(t.Assembly)
         && t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false, IsNested: false }
         && typeof(ControllerBase).IsAssignableFrom(t)
         && t.IsDefined(typeof(ApiControllerAttribute), inherit: true));
}
```
`ControllerFeatureProvider.PopulateFeature` skips types already in the feature, so the default provider plus this one never duplicate a controller. Internal controllers keep public (primary) constructors, which `ActivatorUtilities` requires.

```csharp
// Composition/DesignTimeDbContextFactory.cs — dotnet-ef only; same composition as runtime
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EcommerceDbContext>
{
    // The non-secret local Compose database (unchanged from the old factory). --connection overrides it.
    private const string LocalConnection = "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce";

    public EcommerceDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = LocalConnection })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEcommercePersistence();
        services.AddEcommerceModules(configuration);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<EcommerceDbContext>();
    }
}
```

```csharp
// Time/SystemClock.cs
namespace Ecommerce.Api.Time;
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow(); }
```

**`Program.cs`** — only these lines change; middleware order, ProblemDetails customization, CORS, JSON options, Serilog, `public partial class Program;` stay byte-for-byte:
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddModuleControllers();
// … CORS unchanged …
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddEcommercePersistence();
builder.Services.AddEcommerceModules(builder.Configuration);

if (builder.Configuration.GetValue<bool>(CatalogModule.DevSeedEnabledKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            $"{CatalogModule.DevSeedEnabledKey} is true but the environment is '{builder.Environment.EnvironmentName}'. " +
            "Dev seeding is allowed only in Development.");
    }
    builder.Services.AddCatalogDevSeed();
}

builder.Services.AddHealthChecks().AddPostgresReadinessCheck();
```
The exception message text must stay identical (a startup test asserts it). `HealthEndpoints.cs`: `using Ecommerce.Persistence.Health;` only.

---

## 4. Old → new move table (mechanical)

"ns" = new namespace. "int" = make the type `internal`. Bodies unchanged unless noted.

| Old path (under `backend/`) | New path (under `backend/`) | ns / visibility / edits |
|---|---|---|
| `src/Ecommerce.Application/Abstractions/IClock.cs` | `src/Ecommerce.SharedKernel/Time/IClock.cs` | `Ecommerce.SharedKernel.Time` · public |
| `src/Ecommerce.Domain/Common/DomainException.cs` | `src/Ecommerce.SharedKernel/Domain/DomainException.cs` | `Ecommerce.SharedKernel.Domain` · public |
| `src/Ecommerce.Domain/DomainAssembly.cs` | — delete | replaced by `typeof(CatalogModule).Assembly` in arch tests |
| `src/Ecommerce.Application/ApplicationAssembly.cs` | — delete | same |
| `src/Ecommerce.Infrastructure/Persistence/EcommerceDbContext.cs` | `src/Ecommerce.Persistence/EcommerceDbContext.cs` | `Ecommerce.Persistence` · public · **rewritten per §3.2** |
| — | `src/Ecommerce.Persistence/IModuleDbModel.cs` | new, §3.2 |
| `src/Ecommerce.Infrastructure/Persistence/DatabaseOptions.cs` | `src/Ecommerce.Persistence/DatabaseOptions.cs` | `Ecommerce.Persistence` · public |
| `src/Ecommerce.Infrastructure/Persistence/HealthCheckTags.cs` | `src/Ecommerce.Persistence/Health/HealthCheckTags.cs` | `Ecommerce.Persistence.Health` · public |
| `src/Ecommerce.Infrastructure/Persistence/PostgresReadinessHealthCheck.cs` | `src/Ecommerce.Persistence/Health/PostgresReadinessHealthCheck.cs` | `Ecommerce.Persistence.Health` · int |
| `src/Ecommerce.Infrastructure/DependencyInjection.cs` | split → `src/Ecommerce.Persistence/PersistenceServiceCollectionExtensions.cs` (DB + readiness), `CatalogModule.cs` (catalog), `InventoryModule.cs` (inventory), `Program.cs` (`IClock`) | then delete |
| `src/Ecommerce.Infrastructure/Persistence/DesignTimeDbContextFactory.cs` | `src/Ecommerce.Api/Composition/DesignTimeDbContextFactory.cs` | `Ecommerce.Api.Composition` · public · **rewritten per §3.5** |
| `src/Ecommerce.Infrastructure/Persistence/Migrations/20261103043930_InitialBaseline.cs` | `src/Ecommerce.Persistence/Migrations/20261103043930_InitialBaseline.cs` | `Ecommerce.Persistence.Migrations` |
| `…/Migrations/20261103043930_InitialBaseline.Designer.cs` | `src/Ecommerce.Persistence/Migrations/…Designer.cs` | ns as above; `using Ecommerce.Persistence;` replaces `using Ecommerce.Infrastructure.Persistence;` |
| `…/Migrations/20261103185341_AddCatalog.cs` | `src/Ecommerce.Persistence/Migrations/20261103185341_AddCatalog.cs` | ns as above |
| `…/Migrations/20261103185341_AddCatalog.Designer.cs` | `src/Ecommerce.Persistence/Migrations/…Designer.cs` | ns + using as above; replace every `"Ecommerce.Domain.Catalog.` string with `"Ecommerce.Catalog.Domain.` |
| `…/Migrations/EcommerceDbContextModelSnapshot.cs` | `src/Ecommerce.Persistence/Migrations/EcommerceDbContextModelSnapshot.cs` | ns + using + same string replace |
| `src/Ecommerce.Infrastructure/Time/SystemClock.cs` | `src/Ecommerce.Api/Time/SystemClock.cs` | `Ecommerce.Api.Time` · public |
| `src/Ecommerce.Application/Inventory/IInventoryReader.cs` | `src/Modules/Inventory/Ecommerce.Inventory.Contracts/IInventoryReader.cs` | `Ecommerce.Inventory.Contracts` · public |
| `src/Ecommerce.Application/Inventory/StockStatus.cs` | `src/Modules/Inventory/Ecommerce.Inventory.Contracts/StockStatus.cs` | `Ecommerce.Inventory.Contracts` · public |
| `src/Ecommerce.Infrastructure/Inventory/UnknownInventoryReader.cs` | `src/Modules/Inventory/Ecommerce.Inventory/Infrastructure/UnknownInventoryReader.cs` | `Ecommerce.Inventory.Infrastructure` · int |
| — | `src/Modules/Inventory/Ecommerce.Inventory/InventoryModule.cs` | new, §3.3 |
| `src/Ecommerce.Domain/Catalog/{Category,Product,ProductImage,ProductVariant,Slug}.cs` | `src/Modules/Catalog/Ecommerce.Catalog/Domain/` | `Ecommerce.Catalog.Domain` · int · `using Ecommerce.SharedKernel.Domain;` · `Catalog.Slug` → `Domain.Slug` |
| `src/Ecommerce.Application/Catalog/*.cs` (8 files) | `src/Modules/Catalog/Ecommerce.Catalog/Application/` | `Ecommerce.Catalog.Application` · int · usings → `Ecommerce.Catalog.Domain`, `Ecommerce.Inventory.Contracts` |
| `src/Ecommerce.Infrastructure/Catalog/{CatalogImageOptions,CatalogReadStore,ConfiguredImageUrlResolver}.cs` | `src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/` | `Ecommerce.Catalog.Infrastructure` · int |
| `src/Ecommerce.Infrastructure/Catalog/Persistence/*Configuration.cs` (4) | `src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/Persistence/` | `Ecommerce.Catalog.Infrastructure.Persistence` · already int |
| — | `src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/Persistence/CatalogDbModel.cs` | new, §3.4 |
| `src/Ecommerce.Infrastructure/Catalog/DevSeed/*.cs` (3) | `src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/DevSeed/` | `Ecommerce.Catalog.Infrastructure.DevSeed` · int (`CatalogDevSeedData` already int) |
| `src/Ecommerce.Api/Catalog/CatalogDevSeedHostedService.cs` | `src/Modules/Catalog/Ecommerce.Catalog/Infrastructure/DevSeed/CatalogDevSeedHostedService.cs` | same ns · int |
| `src/Ecommerce.Api/Catalog/{CatalogResponses,CategoriesController,ProductsController}.cs` | `src/Modules/Catalog/Ecommerce.Catalog/Api/` | `Ecommerce.Catalog.Api` · int · usings → `Ecommerce.Catalog.Application`, `Ecommerce.Inventory.Contracts`, add `Microsoft.AspNetCore.Http` if `StatusCodes` no longer resolves (no Web-SDK implicit usings) |
| — | `src/Modules/Catalog/Ecommerce.Catalog/CatalogModule.cs` | new, §3.4 · public |
| — | `src/Ecommerce.Api/Composition/{ModuleComposition,ModuleControllerFeatureProvider}.cs` | new, §3.5 |
| `tests/Ecommerce.Domain.Tests/Catalog/*.cs` (5) | `tests/Ecommerce.Catalog.Tests/Domain/` | `Ecommerce.Catalog.Tests.Domain`; usings → `Ecommerce.Catalog.Domain`, `Ecommerce.SharedKernel.Domain` |
| `tests/Ecommerce.Application.Tests/Catalog/*.cs` (3) | `tests/Ecommerce.Catalog.Tests/Application/` | `Ecommerce.Catalog.Tests.Application`; usings → `Ecommerce.Catalog.Application`, `Ecommerce.Inventory.Contracts` |
| `tests/Ecommerce.Domain.Tests/DomainArchitectureTests.cs` | `tests/Ecommerce.ArchitectureTests/LayerRuleTests.cs` | rewritten per §5 (the old assertion is kept as a stronger per-module rule) |
| `tests/Ecommerce.Application.Tests/ApplicationArchitectureTests.cs` | `tests/Ecommerce.ArchitectureTests/LayerRuleTests.cs` | same |
| `tests/Ecommerce.Application.Tests/ClockRuleTests.cs` | `tests/Ecommerce.ArchitectureTests/ClockRuleTests.cs` | widened per §5 |
| `tests/Ecommerce.Api.IntegrationTests/**` | same paths | usings only, plus the two helper edits below |

**Test-compile rules** (expected CS0050/CS0051 fixes — never touch an assertion):
- A `public` test-helper whose signature exposes a now-internal type becomes `internal` (e.g. `CatalogTestEnv`, `VariantSpec`, `ImageSpec`, `FakeClock` may stay public — `IClock` is public). xUnit test classes stay `public`; their test methods only use internal types in bodies. `StockStatusRollupTests` keeps its `TheoryData<StockStatus[], StockStatus>` (`StockStatus` is public).
- `CatalogDevSeedTests`: `Ecommerce.Domain.Catalog.X` fully-qualified names → `Ecommerce.Catalog.Domain.X`.
- `CatalogSchemaTests.CatalogModelHost`: it constructs `EcommerceDbContext` directly, which now needs the module models. Replace the body so `Scope` owns an `ApiFactory(ApiFactory.Unreachable)` and resolves `EcommerceDbContext` from `factory.Services.CreateScope()` (dispose scope and factory in `Dispose`). The test's assertions (exact table list, snake_case columns) are unchanged.
- `StartupTests`: `using Ecommerce.Api.Time;` for `SystemClock`; `IClock` from `Ecommerce.SharedKernel.Time`.
- `CatalogTestEnv`, `MigrationTests`, `HealthTests`: `using Ecommerce.Persistence;`.

---

## 5. Tests

Baseline 172 → after ≥ 172 + new. Every baseline test name keeps its **class and method name**; only the namespace/assembly prefix changes (Ecommerce.Domain.Tests.Catalog → Ecommerce.Catalog.Tests.Domain, Ecommerce.Application.Tests.Catalog → Ecommerce.Catalog.Tests.Application, the three arch/clock tests → Ecommerce.ArchitectureTests). Marco produces `artifacts/refactor-after/tests.txt` the same way and a mapping proof: `sed` off the namespace prefix on both lists, then `comm -23 baseline after` must be empty except the three rewritten arch/clock tests (`DomainArchitectureTests.Domain_references_no_project_or_infrastructure_assembly`, `ApplicationArchitectureTests.Application_references_only_Domain_among_project_assemblies`, `ClockRuleTests.Domain_and_Application_source_never_read_the_system_clock`), each of which must map to a named replacement below.

### 5.1 Ecommerce.ArchitectureTests (new project) — no new package

Shared helper `SolutionLayout` (internal): find the repo root by `Ecommerce.slnx` (existing `FindSolutionRoot` logic); enumerate `backend/src/**/*.csproj` (skip `bin`/`obj`); **classify every project** by path and name — `Host` (`backend/src/Ecommerce.Api`), `SharedKernel`, `Persistence`, `ModuleImpl(M)` (`backend/src/Modules/<M>/Ecommerce.<M>/`), `ModuleContracts(M)` (`backend/src/Modules/<M>/Ecommerce.<M>.Contracts/`). Any csproj that fits no class, or whose folder name ≠ assembly name, is a failure. Load assemblies with `Assembly.Load(name)` (all are in the test output via the `Ecommerce.Api` reference).

Pure rule evaluators (internal static, unit-testable): `ReferenceRules.Violations(ProjectKind from, IEnumerable<ProjectKind> to)` and `LayerRules.Violations(...)`. They return a list of human-readable violations; tests assert the list is empty and print it on failure.

| Test class | Test (method names) | Rule |
|---|---|---|
| `ProjectReferenceRuleTests` | `Every_backend_project_is_classified` | every src csproj is one of the 5 kinds; folder = assembly name; exactly the expected modules {Catalog, Inventory} each have both projects |
| | `Csproj_project_references_follow_the_module_rules` | parse `ProjectReference Include` (System.Xml.Linq), resolve to project names, apply the §6.2 table: SharedKernel → none; Contracts → SharedKernel only; Persistence → SharedKernel only; ModuleImpl(M) → SharedKernel, Persistence, Contracts(M), Contracts(any); never ModuleImpl(other), never Host |
| | `SharedKernel_and_contracts_have_no_package_or_framework_references` | no `PackageReference` / `FrameworkReference` in those csproj files |
| | `Module_projects_use_the_plain_sdk_and_declare_no_global_usings` | ModuleImpl/Contracts csproj `Sdk="Microsoft.NET.Sdk"`, no `<Using Include>` |
| | `Host_references_every_module_implementation` | Api csproj references every ModuleImpl |
| `AssemblyReferenceRuleTests` | `Compiled_assembly_references_follow_the_module_rules` | same table on `GetReferencedAssemblies()` for `Ecommerce.*` names (catches what the compiler actually linked) |
| | `SharedKernel_and_contracts_reference_no_infrastructure_framework` | no referenced assembly starting `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `Npgsql`, `Serilog`, `Stripe`, `StackExchange.Redis` (the old Domain test's list) |
| `ModuleVisibilityTests` | `A_module_implementation_exports_only_its_module_class` | `GetExportedTypes()` of `Ecommerce.<M>` == { `Ecommerce.<M>.<M>Module` } |
| | `InternalsVisibleTo_is_granted_only_to_allowed_test_assemblies` | `InternalsVisibleToAttribute` on every ModuleImpl/Contracts/Persistence/SharedKernel ⊆ { `Ecommerce.<M>.Tests`, `Ecommerce.Api.IntegrationTests` }; never another src assembly |
| | `Controllers_live_only_in_a_module_Api_namespace` | every `ControllerBase` subclass in any src assembly has namespace `Ecommerce.<M>.Api*` of its own module; none in the host |
| | `Host_composes_every_module_assembly` | `ModuleComposition.ModuleAssemblies` == the set of ModuleImpl assemblies |
| `LayerRuleTests` | `Domain_types_depend_only_on_System_SharedKernel_and_their_own_domain` | for every type in `Ecommerce.<M>.Domain*` (incl. nested/compiler-generated): base type, interfaces, fields (all binding flags), properties, method/ctor params + returns, generic args (recursive) must resolve to an **allowed** type: assembly name starts with `System` or equals `netstandard`, or `Ecommerce.SharedKernel`, or the same assembly with namespace `Ecommerce.<M>.Domain*` (replaces `DomainArchitectureTests`) |
| | `Application_types_depend_only_on_domain_application_contracts_and_SharedKernel` | same walk for `Ecommerce.<M>.Application*`: allowed System*, SharedKernel, any `*.Contracts`, own `.Domain*` / `.Application*`. Anything EF/ASP.NET/Npgsql/Serilog/Stripe/Redis/Persistence/own `.Infrastructure`/`.Api` fails (replaces `ApplicationArchitectureTests`) |
| | `Api_types_never_depend_on_infrastructure_or_ef` | `Ecommerce.<M>.Api*`: no own `.Infrastructure*`, no `Ecommerce.Persistence`, no `Microsoft.EntityFrameworkCore*`, no `Npgsql*` |
| | `Layer_source_files_never_name_a_forbidden_namespace` | source scan (method bodies are invisible to reflection): files under `Modules/<M>/Ecommerce.<M>/Domain/**` may contain `Ecommerce.` only as `Ecommerce.SharedKernel` or `Ecommerce.<M>.Domain`, and none of `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `Microsoft.Extensions`, `Npgsql`, `Serilog`, `Stripe`, `StackExchange.Redis`; files under `Application/**` none of those framework names and no `Ecommerce.<M>.Infrastructure`, `Ecommerce.<M>.Api`, `Ecommerce.Persistence`, or another module's non-Contracts namespace; files under `Api/**` no `.Infrastructure`, `Ecommerce.Persistence`, `Microsoft.EntityFrameworkCore`. Assert the scanned file set is non-empty for Catalog Domain, Application, Api |
| | `Module_sources_declare_no_global_usings_and_namespaces_match_folders` | no `global using` in any module source; every file's `namespace` = `Ecommerce.<M>` + folder path (so reflection checks map to folders) |
| `ModuleModelOwnershipTests` | `Each_module_db_model_configures_only_its_own_domain_entities` | resolve every `IModuleDbModel` from a `ServiceCollection` built with `AddEcommerceModules` (in-memory config); for each, `var mb = new ModelBuilder(); model.Configure(mb);` and assert every `mb.Model.GetEntityTypes()` CLR type is in `model.GetType().Assembly` with namespace `Ecommerce.<M>.Domain`; assert at least one model (Catalog) contributed entities |
| `ClockRuleTests` | `No_source_outside_the_host_reads_the_system_clock` | the old banned-token list (`DateTime.UtcNow`, `DateTime.Now`, `DateTime.Today`, `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`, `TimeProvider.System`) over every `.cs` in `Ecommerce.SharedKernel`, `Ecommerce.Persistence` (including Migrations), and `Modules/**`; assert the scanned set includes Catalog Domain + Application files (replaces the old Domain+Application-only test — strictly wider) |
| `RuleEvaluatorSelfTests` | `Reference_rules_reject_a_module_referencing_another_module_implementation` · `Reference_rules_reject_contracts_referencing_persistence` · `Reference_rules_reject_a_module_referencing_the_host` · `Reference_rules_accept_a_module_referencing_another_modules_contracts` · `Layer_rules_reject_a_domain_type_with_an_ef_dependency` · `Source_scan_rejects_an_ef_using_in_a_domain_file` | feed synthetic inputs to the pure evaluators; proves each rule **fails on a violation** (owner requirement) without editing real code |

**Show-it-fails (required in Marco's report, then reverted):** temporarily (a) add a `ProjectReference` from `Ecommerce.Inventory` to `Ecommerce.Catalog`, (b) add `using Microsoft.EntityFrameworkCore;` to `Catalog/Domain/Slug.cs`, (c) make `CatalogBrowseService` public. Paste the failing test names + messages for each, then revert and paste green. (Plain file edits, no git.)
*Review amendment (Rob, 2026-11-03):* (c) as written cannot compile — `CatalogBrowseService`'s constructor and members expose internal types, so the compiler (CS0050/CS0051) already rejects it. Accepted substitute: make a leaf type (`Domain/Slug`) public, which compiles and must fail `A_module_implementation_exports_only_its_module_class`. Added (d) in the fix round: `using Ecommerce.Inventory.Contracts;` in `Catalog/Domain/Slug.cs` must fail `Layer_source_files_never_name_a_forbidden_namespace`.

### 5.2 Ecommerce.Catalog.Tests
The 8 moved files (Domain 43 tests + Application 46 tests = 89; counts per baseline minus the 3 arch/clock tests). Assertions unchanged.

### 5.3 Ecommerce.Inventory.Tests (new — the project must not be empty, `TreatNoTestsAsError`)
- `UnknownInventoryReaderTests.Returns_Unknown_for_every_requested_variant`
- `UnknownInventoryReaderTests.Returns_one_entry_per_distinct_variant_id` (duplicate ids in → one key each)
- `UnknownInventoryReaderTests.Returns_an_empty_map_for_no_ids`

### 5.4 Ecommerce.Api.IntegrationTests — additions
- `ModuleCompositionTests.Runtime_model_matches_the_migration_snapshot` — `ApiFactory(ApiFactory.Unreachable)`, resolve `EcommerceDbContext`, `Assert.False(db.Database.HasPendingModelChanges())`. Together with the CI drift check (design-time factory vs snapshot) this proves runtime and design-time models agree.
- `ModuleCompositionTests.Runtime_context_discovers_both_existing_migrations` — `db.Database.GetMigrations()` contains `20261103043930_InitialBaseline` and `20261103185341_AddCatalog` (guards against a migrations-assembly mistake that would make readiness silently green).
- Existing tests that already prove the rest: `HealthTests.Ready_returns_503_when_migrations_are_pending`, `MigrationTests` (applies both, PG 17), all Catalog HTTP tests (prove internal controllers are discovered and the wire contract is unchanged), `StartupTests` (message text), `CatalogDevSeedTests` (seed guard + idempotency).

---

## 6. Order of operations (build green at the end of every phase)

Run after **each** phase: `dotnet build Ecommerce.slnx` (0 errors) and `dotnet test Ecommerce.slnx` (all green, count ≥ previous phase). Update `Ecommerce.slnx` as projects are added/removed in each phase.

0. **Baseline** — already captured (table at top). Do not overwrite `artifacts/refactor-baseline/`.
1. **SharedKernel.** Create project; move `IClock`, `DomainException`; add SharedKernel reference to the old Domain and Application projects; fix usings everywhere. Temporarily allow `Ecommerce.SharedKernel` in the old `DomainArchitectureTests`/`ApplicationArchitectureTests` allow-lists, and in phase 2 also `Ecommerce.Inventory.Contracts` for Application (those tests are replaced in phase 4 by strictly stronger ones; list each temporary allowance in the report).
2. **Inventory.** Create `Ecommerce.Inventory.Contracts` + `Ecommerce.Inventory`; move `IInventoryReader`, `StockStatus`, `UnknownInventoryReader`; `AddInventoryModule`; Application references `Inventory.Contracts`; Api references `Ecommerce.Inventory` and calls `AddInventoryModule`; remove the inventory registration from `AddInfrastructure`. Create `Ecommerce.Inventory.Tests` (§5.3).
3. **Persistence.** Create project; move `EcommerceDbContext` (rewritten), `DatabaseOptions`, health check + tags, migrations + snapshot (namespace/using change only in this phase — **no** entity-string rename yet); add `IModuleDbModel`; the old Infrastructure references Persistence and gets an internal `CatalogDbModel` registered from `AddInfrastructure`; move the design-time factory into `Ecommerce.Api/Composition` (it calls `AddEcommercePersistence` + `AddInfrastructure` for now); add `Design` to Api. Then run:
   `dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api` → "No changes", and the idempotent script → `diff` with the baseline is empty.
4. **Catalog + architecture tests.** Create `Ecommerce.Catalog.Contracts` + `Ecommerce.Catalog`; move all Catalog files from Domain/Application/Infrastructure/Api (§4), make them internal, add `CatalogModule`, `ModuleComposition`, `ModuleControllerFeatureProvider`; move `SystemClock` to the host; switch Program.cs to §3.5; switch the factory to `AddEcommerceModules`; rename entity strings in the AddCatalog designer + snapshot. Move the Domain/Application catalog tests into `Ecommerce.Catalog.Tests`. Create `Ecommerce.ArchitectureTests` with the full §5.1 set **in the same phase** — the 3 old arch/clock tests are deleted only when their replacements land. Delete the now-empty `Ecommerce.Domain`, `Ecommerce.Application`, `Ecommerce.Infrastructure` projects and the two old test projects. Re-run the drift check + script diff.
5. **Prove the rules bite + composition tests.** `RuleEvaluatorSelfTests` green; the show-it-fails exercise (§5.1) with pasted output, reverted; add the §5.4 integration tests.
6. **Build files and docs.** Dockerfile, CI, `.dockerignore` (verify unchanged), README already updated by Rob — re-check every path in README/architecture §6.2 against the real tree and report any mismatch for Rob (do not invent new names).
7. **Verification (paste real output):**
   - `dotnet build Ecommerce.slnx` · `dotnet test Ecommerce.slnx` (per-project lines) · `--list-tests` → `artifacts/refactor-after/tests.txt` + the name-mapping proof (§5).
   - `dotnet build -c Release` then the two CI dotnet-ef commands exactly as in `ci.yml` (`--configuration Release --no-build`) → drift clean; `diff artifacts/refactor-baseline/migrate.sql artifacts/migrations/migrate.sql` → empty.
   - Dev DB: `docker compose up -d postgres` (if not running); `dotnet tool run dotnet-ef database update --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api --connection "Host=localhost;Port=5434;Database=ecommerce;Username=ecommerce"` → "No migrations were applied. The database is already up to date."; `dotnet run --project backend/src/Ecommerce.Api --launch-profile http`; re-capture the 7 URLs (same headers) into `artifacts/refactor-after/`; `diff` each against the baseline after stripping `"traceId":"…"` → empty; `/health/ready` → 200. Stop the API.
   - `docker build -t ecommerce-api:refactor .` → success.
   - Frontend: no file edited; run `npx tsc --noEmit` and `npx expo export --platform web` in `frontend/apps/store` anyway (quality bar §4) and paste.

---

## 7. Decisions made (Rob)

| # | Decision | Reason |
|---|---|---|
| D1 | Keep **one** `EcommerceDbContext`, one migration stream, one `__EFMigrationsHistory`, `public` schema, table names unchanged. Modules contribute internal `IModuleDbModel`s. | §8.2/rule 3/rule 6: checkout spans Inventory + Orders + Payments + outbox in **one** transaction — one scoped context gives that by default; per-module contexts would need a shared-connection/`UseTransaction` protocol on the most critical path. Moving tables to schemas (`ALTER TABLE … SET SCHEMA`) breaks rule 18 (previous image reads `public.*`) and adds risk for no invariant gain. Existing migration IDs keep matching the dev DB's history rows. Rule 8 becomes compiler-enforced because entities are `internal`. |
| D2 | New project **`Ecommerce.Persistence`** (not in the owner's sketch) holds the context, `IModuleDbModel`, `DatabaseOptions`, readiness check, and migrations. | SharedKernel must stay free of EF (Contracts reference it — otherwise every Contracts consumer inherits EF). The shared context and later platform tables (outbox, idempotency_keys, audit_log, processed_events — §7.1) are not owned by any module. A project, not a package — no rule 16 impact. |
| D3 | Migrations stay in the context's assembly (`Ecommerce.Persistence`); design-time factory lives in the host and uses the same `AddEcommerceModules` as runtime; dotnet-ef = `--project Ecommerce.Persistence --startup-project Ecommerce.Api`. | Only the host references every module; one composition function means the drift check can't miss a module. No `MigrationsAssembly` override → no way to silently find zero migrations (and §5.4 tests it). |
| D4 | `SystemClock` moves to the host (`Ecommerce.Api/Time`); `IClock` registration in `Program.cs`. Clock rule widened to SharedKernel + Persistence + all modules. | Rule 9 — the only system-clock read sits in the composition root; widening the scan is strictly stronger than today's Domain+Application scan. |
| D5 | Module public surface = exactly one static `<M>Module` class (`Add<M>Module(IServiceCollection, IConfiguration)`; Catalog also `DevSeedEnabledKey`, `AddCatalogDevSeed()`). Everything else `internal`. | Owner requirement; one tested entry point per module. The Development-only guard stays in the host because only the host knows the environment, with the identical exception message. |
| D6 | Controllers are `internal`; host `ModuleControllerFeatureProvider` + explicit `AssemblyPart`s discover them. | MVC discovers only public controllers by default; public controllers would force their service, criteria, and result types public, leaking the implementation. |
| D7 | `Microsoft.EntityFrameworkCore.Design` added to `Ecommerce.Api` with `PrivateAssets="all"`. | dotnet-ef requires it in the startup project. Same package and pinned version already used by Infrastructure — not a new dependency (rule 16). Tradeoff: Design assemblies ship in the image (harmless; the standard ASP.NET template does the same). Recorded in §13.2. |
| D8 | `InternalsVisibleTo`: Catalog → `Ecommerce.Catalog.Tests`, `Ecommerce.Api.IntegrationTests`; Inventory → `Ecommerce.Inventory.Tests`. Never to another src assembly (architecture-tested). | Catalog HTTP fixtures build data through domain factories + `EcommerceDbContext` (never raw SQL, never the seeder) — they need the internal entities. Keeping those tests in `Ecommerce.Api.IntegrationTests` avoids a shared test-support project and keeps one Postgres fixture. |
| D9 | Catalog HTTP integration tests stay in `Ecommerce.Api.IntegrationTests`; module test projects hold Domain + Application unit tests. | Integration tests need `WebApplicationFactory<Program>` and the assembly-wide Postgres fixture; splitting them per module would duplicate the fixture or add a support project. |
| D10 | `Ecommerce.Catalog.Contracts` is created with **no types**. | Owner layout. Nothing outside Catalog consumes a Catalog contract yet; adding `IProductReader`/`ProductPublished` now would be dead code (quality bar §6). Slice 3 (`IProductReader`) and the first `ProductPublished` consumer fill it. |
| D11 | `Ecommerce.Inventory.Tests` gets 3 real tests of `UnknownInventoryReader`. | `TreatNoTestsAsError` fails an empty test project; the stub had no direct test. |
| D12 | Architecture rules via reflection + csproj XML + source scan (System.Reflection, System.Xml.Linq) — **no** NetArchTest/ArchUnitNET. | Rule 16: no package needed. IL references alone miss unused `ProjectReference`s, and signature reflection misses method bodies — so all three views are checked. Each evaluator has a self-test proving it fails on a violation. |
| D13 | Folder-per-layer purity replaces assembly-per-layer purity; the old Domain/Application assembly tests are replaced by per-module type-signature + source-scan tests, with `global using` banned in modules and module csprojs on the plain SDK. | The owner's layout puts layers in one assembly, so "Domain.dll references no EF" can't exist anymore. The replacement checks the same thing per type and per file, which also covers future modules automatically. |
| D14 | Test projects flat under `backend/tests/` (`Ecommerce.Catalog.Tests`, `Ecommerce.Inventory.Tests`, `Ecommerce.ArchitectureTests`). | Matches the owner's example; `backend/tests/Directory.Build.props` (runsettings) applies at that depth unchanged. |
| D15 | Root `Directory.Build.props` unchanged; `InternalsVisibleTo` declared explicitly per csproj. | Explicit is reviewable; a props-level wildcard would also grant IVT to Contracts/SharedKernel silently. |

Doc updates made by Rob in this plan (owner decision recorded): architecture §3.1 item 4, §3.2 row D, §6.1 (two projects per module), §6.2 (rewritten: layout, reference table, layers, visibility, persistence, architecture tests), §13.2 (new rows: backend project layout; DbContext layout re-argued; migrations location; module controllers; clock in the host — the old "DbContext layout" row is replaced), footer; README layout, dotnet-ef command, module-boundaries note. `docs/ecommerce-app-context.md` names no backend paths — no change.

---

## 8. Risks

| Risk | Mitigation |
|---|---|
| Readiness silently green if EF can't find migrations (wrong migrations assembly) | Migrations stay in the context assembly (D3); `HealthTests.Ready_returns_503_when_migrations_are_pending` + new `Runtime_context_discovers_both_existing_migrations` |
| Snapshot/designer entity-name strings drift → noisy future migration | Rename `Ecommerce.Domain.Catalog.*` → `Ecommerce.Catalog.Domain.*` in snapshot + AddCatalog designer; drift check clean; script byte-identical; `Runtime_model_matches_the_migration_snapshot` |
| Runtime and design-time models differ (module missing from one) | Single `AddEcommerceModules`; CI drift check (design-time) + `HasPendingModelChanges` test (runtime) |
| Internal controllers not discovered → every catalog route 404 | All Catalog HTTP integration tests + dev-DB response diff; `Controllers_live_only_in_a_module_Api_namespace` |
| JSON shape changes (internal records, enum names) | STJ serializes public properties of internal types identically; `JsonStringEnumConverter` unchanged; byte-diff of the 7 captured responses |
| dotnet-ef with `--startup-project Api` runs `Program` to probe the host before using the factory | Factory is found first; host probing failure is non-fatal in EF tools. If the tool reports it used something other than `DesignTimeDbContextFactory` (`--verbose`), report it as a Concern — do not change Program to suit the tool |
| `Catalog.Slug` name-binding change in the new namespace | Called out in §3.4 (compile error, not a silent change) |
| A module grants `InternalsVisibleTo` to another module — bypasses every boundary | `InternalsVisibleTo_is_granted_only_to_allowed_test_assemblies` |
| Architecture tests that pass vacuously (scan zero files / zero types) | Each scan asserts non-empty sets for Catalog Domain/Application/Api; `Every_backend_project_is_classified` asserts the exact module set; self-tests prove failure on violations |
| Slice 2 adds `inventory_stock`, breaking `CatalogSchemaTests`' exact table list | Not this refactor's concern; Slice 2 plan will scope that assertion to Catalog-owned entities (noted for Rob's Slice 2 plan) |
| Design assemblies in the image | Accepted, recorded (D7, §13.2); revisit with a separate migrations bundle in Slice 7 if image size matters |

---

## 9. Review gates Rob will check

Stack lock and no new package (`Directory.Packages.props` unchanged) · reference table holds in csproj **and** IL · exported-type and IVT rules · no behaviour change (response diffs, startup message, health, CORS, correlation, ProblemDetails) · migration script byte-identical, drift clean, dev DB "already up to date" · every baseline test present by class+method with unchanged assertions · self-tests + show-it-fails output · Dockerfile restore layer complete and `docker build` green · CI paths · docs match the real tree · no git write · AGENT_STATUS.

---

## 10. For Nathaniel / main session (Rob does not edit these files)

`docs/quality-bar.md` says no agent edits it, so the quality-bar changes below are proposed text for Nathaniel to apply. CLAUDE.md and the agent files likewise.

### 10.1 `docs/quality-bar.md`

§2, replace the first checkbox:
> `- [ ] Module boundaries respected — no cross-module `DbSet` or join, no logic in controllers (rules 7–8).`

with:
> `- [ ] Module boundaries respected (rules 7–8, architecture §6.2): a module references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts` — never another module's implementation project; implementation types are `internal` (the module's only public type is `<Module>Module`); no cross-module `DbSet` or join; no logic in controllers; new module code has tests in `Ecommerce.<Module>.Tests` and `Ecommerce.ArchitectureTests` is green.`

§3 Blocking, replace:
> `- [ ] **Boundaries:** module contracts only; thin controllers; dependency direction Api → Application → Domain.`

with:
> `- [ ] **Boundaries:** module contracts only — a module project references only SharedKernel, Persistence, and other modules' `*.Contracts`, never another module's implementation or the host; implementation types `internal`, `InternalsVisibleTo` only to the module's test project (and `Ecommerce.Api.IntegrationTests`); thin controllers; inside a module, dependency direction Api → Application → Domain (folders/namespaces); per-module tests live in `Ecommerce.<Module>.Tests`; architecture tests (`Ecommerce.ArchitectureTests`) exist for every rule, are green, and were not weakened.`

§4 item 2, after "`dotnet test` (domain + application)." insert:
> ` This includes the per-module test projects and `Ecommerce.ArchitectureTests` (module reference, visibility, layer, and clock rules — architecture §6.2).`

§4 item 4, append:
> ` Commands: `dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api`.`

### 10.2 `CLAUDE.md`

In "Rules you're most likely to break", replace:
> `- Thin controllers. No cross-module `DbSet` or join. `IClock`, never `DateTime.UtcNow`.`

with:
> `- Thin controllers. No cross-module `DbSet` or join. `IClock`, never `DateTime.UtcNow`.`
> `- A module (`backend/src/Modules/<M>/Ecommerce.<M>`) references only `Ecommerce.SharedKernel`, `Ecommerce.Persistence`, and other modules' `*.Contracts`. Implementation types are `internal`; the only public type is `<M>Module`. `Ecommerce.ArchitectureTests` enforces it (architecture §6.2).`

In "Commands", add after the `dotnet test` line:
> ``dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Ecommerce.Persistence --startup-project backend/src/Ecommerce.Api``

### 10.3 `~/.claude/agents/marco.md`

In "How you work", replace the **Rules** bullet's end "…thin controllers, module boundaries, `IClock`." with:
> `…thin controllers, module boundaries (architecture §6.2: a module references only SharedKernel, Persistence, and other modules' `*.Contracts`; implementation types `internal`; unit tests in `Ecommerce.<Module>.Tests`), `IClock`.`

### 10.4 `~/.claude/agents/rob.md`

In "Review mode", add a bullet after "Review the working-tree diff…":
> `- Boundaries (architecture §6.2): check csproj references and `Ecommerce.ArchitectureTests` — a module references only SharedKernel, Persistence, and other modules' `*.Contracts`; implementation types are `internal`; an architecture test changed in the diff gets the same scrutiny as a weakened invariant test.`


### 10.5 After Marco's build

If any project, folder, namespace, or command differs from this plan, Rob updates architecture §6.2/§13.2 and README in the review round, and re-issues §10 text with the final names.
