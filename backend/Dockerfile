# Multi-stage build for the MovieFlix API.
# Build context is the `backend/` directory:
#   docker build -t movieflix-api .
#
# NOTE: Docker is not installed on the dev machine this was authored on, so
# this file has not been executed locally — verify with `docker build` before
# relying on it.

# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file first so `restore` is cached while only source changes.
COPY MovieFlix.Api/MovieFlix.Api.csproj MovieFlix.Api/
RUN dotnet restore MovieFlix.Api/MovieFlix.Api.csproj

# Publish the API in Release mode (apphost disabled: we run via `dotnet`).
COPY MovieFlix.Api/ MovieFlix.Api/
RUN dotnet publish MovieFlix.Api/MovieFlix.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# curl is installed so the container can serve a real healthcheck against the
# app's /health endpoint (see docker-compose.yml).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The container runs as the built-in non-root user ($APP_UID = 1654 in the
# .NET base images). Prepare a writable data directory for SQLite.
ENV APP_UID=1654
RUN mkdir -p /data && chown $APP_UID /data

COPY --from=build /app/publish .

# Secrets (Jwt:Key, Tmdb:AccessToken, connection strings) are supplied at run
# time via environment variables using the double-underscore convention, e.g.
#   Jwt__Key=...  Tmdb__AccessToken=...  ConnectionStrings__Postgres=...
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "MovieFlix.Api.dll"]
