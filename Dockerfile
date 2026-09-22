FROM --platform=$BUILDPLATFORM node:22-bookworm-slim AS node
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=node /usr/local/ /usr/local/
COPY . .
RUN dotnet tool restore \
    && npm ci --no-audit --no-fund \
    && npm run build:client \
    && dotnet publish Server/Server.fsproj -c Release -o /out -p:SkipClientBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Demo.Server.dll"]
