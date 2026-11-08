FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY backend/src/Ecommerce.SharedKernel/Ecommerce.SharedKernel.csproj backend/src/Ecommerce.SharedKernel/
COPY backend/src/Ecommerce.Persistence/Ecommerce.Persistence.csproj backend/src/Ecommerce.Persistence/
COPY backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/Ecommerce.Catalog.Contracts.csproj backend/src/Modules/Catalog/Ecommerce.Catalog.Contracts/
COPY backend/src/Modules/Catalog/Ecommerce.Catalog/Ecommerce.Catalog.csproj backend/src/Modules/Catalog/Ecommerce.Catalog/
COPY backend/src/Modules/Identity/Ecommerce.Identity.Contracts/Ecommerce.Identity.Contracts.csproj backend/src/Modules/Identity/Ecommerce.Identity.Contracts/
COPY backend/src/Modules/Identity/Ecommerce.Identity/Ecommerce.Identity.csproj backend/src/Modules/Identity/Ecommerce.Identity/
COPY backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/Ecommerce.Inventory.Contracts.csproj backend/src/Modules/Inventory/Ecommerce.Inventory.Contracts/
COPY backend/src/Modules/Inventory/Ecommerce.Inventory/Ecommerce.Inventory.csproj backend/src/Modules/Inventory/Ecommerce.Inventory/
COPY backend/src/Modules/Cart/Ecommerce.Cart.Contracts/Ecommerce.Cart.Contracts.csproj backend/src/Modules/Cart/Ecommerce.Cart.Contracts/
COPY backend/src/Modules/Cart/Ecommerce.Cart/Ecommerce.Cart.csproj backend/src/Modules/Cart/Ecommerce.Cart/
COPY backend/src/Modules/Notifications/Ecommerce.Notifications.Contracts/Ecommerce.Notifications.Contracts.csproj backend/src/Modules/Notifications/Ecommerce.Notifications.Contracts/
COPY backend/src/Modules/Notifications/Ecommerce.Notifications/Ecommerce.Notifications.csproj backend/src/Modules/Notifications/Ecommerce.Notifications/
COPY backend/src/Modules/Orders/Ecommerce.Orders.Contracts/Ecommerce.Orders.Contracts.csproj backend/src/Modules/Orders/Ecommerce.Orders.Contracts/
COPY backend/src/Modules/Orders/Ecommerce.Orders/Ecommerce.Orders.csproj backend/src/Modules/Orders/Ecommerce.Orders/
COPY backend/src/Modules/Payments/Ecommerce.Payments.Contracts/Ecommerce.Payments.Contracts.csproj backend/src/Modules/Payments/Ecommerce.Payments.Contracts/
COPY backend/src/Modules/Payments/Ecommerce.Payments/Ecommerce.Payments.csproj backend/src/Modules/Payments/Ecommerce.Payments/
COPY backend/src/Ecommerce.Api/Ecommerce.Api.csproj backend/src/Ecommerce.Api/
RUN dotnet restore backend/src/Ecommerce.Api/Ecommerce.Api.csproj
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Ecommerce.Api/Ecommerce.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ecommerce.Api.dll"]
