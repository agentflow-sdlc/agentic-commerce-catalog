FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.config global.json Catalog.sln ./
COPY src/Infrastructure/Sql/Catalog.Infrastructure.Sql.csproj src/Infrastructure/Sql/
COPY src/Application/Catalog.Application.csproj src/Application/
COPY src/Domain/Catalog.Domain.csproj src/Domain/
COPY src/Clients/DatabaseMigrator/Catalog.Clients.DatabaseMigrator.csproj src/Clients/DatabaseMigrator/
RUN dotnet restore src/Clients/DatabaseMigrator/Catalog.Clients.DatabaseMigrator.csproj

COPY src/ src/
RUN dotnet publish src/Clients/DatabaseMigrator/Catalog.Clients.DatabaseMigrator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Clients.DatabaseMigrator.dll"]
