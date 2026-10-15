FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props NuGet.config global.json Catalog.sln ./
COPY src/Clients/Api/Catalog.Clients.Api.csproj src/Clients/Api/
COPY src/Infrastructure/Sql/Catalog.Infrastructure.Sql.csproj src/Infrastructure/Sql/
COPY src/Domain/Catalog.Domain.csproj src/Domain/
COPY src/Application/Catalog.Application.csproj src/Application/
COPY src/Clients/DatabaseMigrator/Catalog.Clients.DatabaseMigrator.csproj src/Clients/DatabaseMigrator/
RUN dotnet restore src/Clients/Api/Catalog.Clients.Api.csproj

COPY src/ src/
RUN dotnet publish src/Clients/Api/Catalog.Clients.Api.csproj \
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
ENTRYPOINT ["dotnet", "Catalog.Clients.Api.dll"]
