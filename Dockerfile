FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.config global.json Catalog.sln ./
COPY src/Catalog.Api/Catalog.Api.csproj src/Catalog.Api/
COPY src/Catalog.Accessors/Catalog.Accessors.csproj src/Catalog.Accessors/
COPY src/Catalog.Contracts/Catalog.Contracts.csproj src/Catalog.Contracts/
COPY src/Catalog.Engines/Catalog.Engines.csproj src/Catalog.Engines/
COPY src/Catalog.Managers/Catalog.Managers.csproj src/Catalog.Managers/
COPY src/Catalog.DatabaseMigrator/Catalog.DatabaseMigrator.csproj src/Catalog.DatabaseMigrator/
RUN dotnet restore src/Catalog.Api/Catalog.Api.csproj

COPY src/ src/
RUN dotnet publish src/Catalog.Api/Catalog.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
