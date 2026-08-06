FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.config global.json Catalog.sln ./
COPY src/Catalog.Accessors/Catalog.Accessors.csproj src/Catalog.Accessors/
COPY src/Catalog.Engines/Catalog.Engines.csproj src/Catalog.Engines/
COPY src/Catalog.DatabaseMigrator/Catalog.DatabaseMigrator.csproj src/Catalog.DatabaseMigrator/
RUN dotnet restore src/Catalog.DatabaseMigrator/Catalog.DatabaseMigrator.csproj

COPY src/ src/
RUN dotnet publish src/Catalog.DatabaseMigrator/Catalog.DatabaseMigrator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.DatabaseMigrator.dll"]
