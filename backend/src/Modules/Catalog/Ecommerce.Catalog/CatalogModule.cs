using Ecommerce.Catalog.Api.Admin;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Infrastructure;
using Ecommerce.Catalog.Infrastructure.DevSeed;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Projections;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Catalog;

public static class CatalogModule
{
    public const string DevSeedEnabledKey = CatalogDevSeedOptions.EnabledKey;

    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, CatalogDbModel>());

        services.AddOptions<CatalogImageOptions>()
            .Configure<IConfiguration>((o, c) => o.BaseUrl = c["Catalog:Images:BaseUrl"] ?? "")
            .Validate(CatalogImageOptions.IsValid, "Catalog:Images:BaseUrl must be empty or an absolute http/https URL.")
            .ValidateOnStart();
        services.AddSingleton<IImageUrlResolver, ConfiguredImageUrlResolver>();
        services.AddOptions<CatalogImageStorageOptions>()
            .Configure<IConfiguration>((o, c) =>
            {
                o.ServiceUrl = c["Catalog:Images:Storage:ServiceUrl"] ?? "";
                o.Region = c["Catalog:Images:Storage:Region"] ?? "";
                o.Bucket = c["Catalog:Images:Storage:Bucket"] ?? "";
                o.AccessKeyId = c["Catalog:Images:Storage:AccessKeyId"] ?? "";
                o.SecretAccessKey = c["Catalog:Images:Storage:SecretAccessKey"] ?? "";
            })
            .Validate(CatalogImageStorageOptions.IsValid, "Catalog:Images:Storage must be all blank or fully set with an https ServiceUrl.")
            .ValidateOnStart();
        services.AddSingleton<IImageStorage, SpacesImageStorage>();
        services.AddScoped<ICatalogReadStore, CatalogReadStore>();
        services.AddScoped<IProductReader, ProductReader>();
        services.AddScoped<ICatalogSearch, CatalogSearch>();
        services.AddScoped<CatalogBrowseService>();
        services.AddScoped<CatalogSalesProjector>();
        services.AddScoped<IProjector>(sp => sp.GetRequiredService<CatalogSalesProjector>());
        services.AddScoped<IProductSalesReader>(sp => sp.GetRequiredService<CatalogSalesProjector>());
        services.AddScoped<CatalogDevSeeder>();

        // Admin catalog (Slice 1b).
        services.AddScoped<IProductWriteStore, ProductWriteStore>();
        services.AddScoped<ICategoryWriteStore, CategoryWriteStore>();
        services.AddScoped<IAdminCatalogReadStore, AdminCatalogReadStore>();
        services.AddScoped<AdminProductService>();
        services.AddScoped<AdminCategoryService>();
        services.AddScoped<AdminProductImageService>();
        services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
        services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();
        return services;
    }

    /// <summary>Registers the startup seeder. The host decides whether the environment allows it.</summary>
    public static IServiceCollection AddCatalogDevSeed(this IServiceCollection services)
    {
        services.AddHostedService<CatalogDevSeedHostedService>();
        return services;
    }
}
