FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates openssl \
    && rm -rf /var/lib/apt/lists/*

COPY NuGet.Config common.props OrderXChange.sln ./
COPY src/ ./src/
COPY modules/ ./modules/

RUN dotnet restore ./src/OrderXChange.HttpApi.Host/OrderXChange.HttpApi.Host.csproj

WORKDIR /src
RUN dotnet publish ./src/OrderXChange.HttpApi.Host/OrderXChange.HttpApi.Host.csproj -c Release -o /app/publish

ARG OPENIDDICT_PFX_PASSPHRASE=0ae7c713-43f1-4428-9273-ac71bd31a284
RUN openssl req -x509 -newkey rsa:2048 -sha256 -nodes \
    -keyout /tmp/openiddict.key -out /tmp/openiddict.crt -days 3650 \
    -subj "/CN=localhost" \
    && openssl pkcs12 -export -out /app/publish/openiddict.pfx \
    -inkey /tmp/openiddict.key -in /tmp/openiddict.crt \
    -passout pass:${OPENIDDICT_PFX_PASSPHRASE}

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "OrderXChange.HttpApi.Host.dll"]
