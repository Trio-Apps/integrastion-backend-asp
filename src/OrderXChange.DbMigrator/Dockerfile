FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NuGet.Config common.props OrderXChange.sln ./
COPY src/ ./src/
COPY modules/ ./modules/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./src/OrderXChange.DbMigrator/OrderXChange.DbMigrator.csproj
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish ./src/OrderXChange.DbMigrator/OrderXChange.DbMigrator.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish ./
COPY src/OrderXChange.DbMigrator/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
