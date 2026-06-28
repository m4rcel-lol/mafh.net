# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY M5FileHost.slnx ./
COPY src/M5FileHost.Core/M5FileHost.Core.csproj src/M5FileHost.Core/
COPY src/M5FileHost.Infrastructure/M5FileHost.Infrastructure.csproj src/M5FileHost.Infrastructure/
COPY src/M5FileHost.Web/M5FileHost.Web.csproj src/M5FileHost.Web/
COPY src/M5FileHost.Worker/M5FileHost.Worker.csproj src/M5FileHost.Worker/
COPY src ./src
# Restore after copying the source so stale host bin/obj directories can never
# replace the container-generated NuGet assets. .dockerignore still excludes
# those directories to keep the build context small.
RUN find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf '{}' + \
    && dotnet restore src/M5FileHost.Web/M5FileHost.Web.csproj \
    && dotnet restore src/M5FileHost.Worker/M5FileHost.Worker.csproj
RUN dotnet publish src/M5FileHost.Web/M5FileHost.Web.csproj -c Release --no-restore -o /out/web /p:UseAppHost=false \
    && dotnet publish src/M5FileHost.Worker/M5FileHost.Worker.csproj -c Release --no-restore -o /out/worker /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime-base
RUN apk add --no-cache ffmpeg gifsicle 7zip libgomp \
    && mkdir -p /data/uploads /data/keys \
    && chown -R "$APP_UID:$APP_UID" /data
WORKDIR /app
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_EnableDiagnostics=0

FROM runtime-base AS web
COPY --from=build --chown=$APP_UID:$APP_UID /out/web ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "M5FileHost.Web.dll"]

FROM runtime-base AS worker
COPY --from=build --chown=$APP_UID:$APP_UID /out/worker ./
ENTRYPOINT ["dotnet", "M5FileHost.Worker.dll"]
