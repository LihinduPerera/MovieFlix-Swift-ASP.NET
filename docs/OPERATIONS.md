# MovieFlix — Operations Guide

How the backend is configured, deployed, observed, and backed up. This sits
alongside `docs/BUILD_PROGRESS.md` (the build diary) and the top-level
`README.md` (project overview).

> **Status note:** the Docker assets and PostgreSQL provider path in this guide
> are authored to production standards but were **not executed locally** (Docker
> is not installed on the development machine). Validate them in a container
> environment before relying on them.

**Runtime:** ASP.NET Core on **.NET 9** (`net9.0`). The API, tests, Docker base
images, and CI all stay on .NET 9.

---

## 1. Environments

ASP.NET Core reads `appsettings.{ENVIRONMENT}.json` on top of
`appsettings.json`, where `{ENVIRONMENT}` comes from `ASPNETCORE_ENVIRONMENT`
(defaults to `Development` locally).

| Environment   | File                          | Behaviour |
| ------------- | ----------------------------- | --------- |
| `Development` | `appsettings.Development.json` | Swagger UI at `/swagger`, detailed EF errors + sensitive data logging, loads **user secrets** |
| `Testing`     | `appsettings.Testing.json`    | Integration-test host only; quiet logging, relaxed rate limits, self-contained test secrets |
| `Production`  | —                             | No Swagger, no EF diagnostics, secrets must come from the environment |

`appsettings.Testing.json` exists so the test suite never depends on a
developer's machine secrets or on network access. The test host selects it via
`builder.UseEnvironment("Testing")`.

---

## 2. Configuration & secrets

Secrets are **never** stored in `appsettings.json` (that file holds only
non-secret defaults). They are supplied per environment:

| Key | Local development | Production |
| --- | --- | --- |
| `Jwt:Key` | `dotnet user-secrets set` | `Jwt__Key` env var / secret manager |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` defaults | override only if needed |
| `Tmdb:AccessToken` | `dotnet user-secrets set` | `Tmdb__AccessToken` env var / secret manager |
| `ConnectionStrings:Postgres` | `dotnet user-secrets set` | `ConnectionStrings__Postgres` env var |

### Non-secret settings worth knowing

| Key | Default | Notes |
| --- | --- | --- |
| `Jwt:AccessTokenMinutes` | `60` | Access-token lifetime |
| `Jwt:RefreshTokenDays` | `7` | Refresh-token lifetime; tokens rotate on use |
| `Tmdb:BaseUrl` | `https://api.themoviedb.org/3/` | TMDB API root |
| `Tmdb:DefaultLanguage` | `en-US` | Fallback when `language` is omitted |
| `Tmdb:DefaultRegion` | `US` | Fallback when `region` is omitted |
| `Tmdb:Warmup:Enabled` | `false` | Opt-in startup cache warm-up |
| `Tmdb:Warmup:WarmSeasons` | `true` | Warm every non-special season (one TMDB call per season) |
| `Database:Provider` | `Sqlite` | `Sqlite` or `Postgres` |
| `RateLimiting:GlobalPermitLimit` | `120` | Per 60 s per user/IP |
| `RateLimiting:AuthPermitLimit` | `10` | Per 60 s per IP on `/api/v1/auth/*` |
| `ForwardedHeaders:Enabled` | `false` | Honor `X-Forwarded-*` from trusted proxies |
| `Seed:AdminEmail` / `Seed:AdminPassword` | absent | Optional initial admin account |

### The double-underscore convention

Configuration providers flatten nested keys with `__`, so everything in
`appsettings.json` can be set from the environment:

```bash
export Jwt__Key="<strong-key>"
export Tmdb__AccessToken="<tmdb-read-token>"
export Database__Provider="Postgres"
export ConnectionStrings__Postgres="Host=db;Port=5432;Database=movieflix;Username=movieflix;Password=..."
export Cors__AllowedOrigins__0="https://app.example.com"
export RateLimiting__AuthPermitLimit="5"
export ForwardedHeaders__Enabled="true"
export ForwardedHeaders__KnownProxies__0="10.0.0.10"
```

### Production secret storage options

- **Environment variables** — simplest; pair with the secret store of your
  orchestrator (systemd unit, ECS task definition, Kubernetes Secret).
- **Azure Key Vault / AWS Secrets Manager / HashiCorp Vault** — mount the
  secrets as configuration, e.g. the Azure Key Vault provider:

  ```bash
  dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
  ```

  ```csharp
  builder.Configuration.AddAzureKeyVault(
      new Uri("https://<vault>.vault.azure.net/"),
      new DefaultAzureCredential());
  ```

  Key Vault uses `--` for nesting, so `Jwt--Key` maps to `Jwt:Key`.
- **Docker Compose** — secrets are read from `.env` (see `.env.example`),
  which is git-ignored. Compose fails fast if `JWT_KEY` or
  `TMDB_ACCESS_TOKEN` is missing.

> **Action item:** the TMDB token that was previously committed must be
> rotated at <https://www.themoviedb.org/settings/api>. Rotating invalidates
> the leaked value.

### Optional cache warm-up

Warm-up is disabled by default, so tests and local runs never make surprise
network calls. Enable it with a small set of titles:

```json
{
  "Tmdb": {
    "Warmup": {
      "Enabled": true,
      "WarmSeasons": true,
      "Items": [
        { "mediaType": "movie", "tmdbId": 27205 },
        { "mediaType": "tv", "tmdbId": 1399 }
      ]
    }
  }
}
```

Equivalent environment keys:
`Tmdb__Warmup__Enabled=true`,
`Tmdb__Warmup__Items__0__MediaType=tv`,
`Tmdb__Warmup__Items__0__TmdbId=1399`. Set `WarmSeasons=false` for large
shows. Warm-up failures are logged and never block API availability.

---

## 3. Deployment with Docker

The API ships as a multi-stage container image (`backend/Dockerfile`) running
as a non-root user, listening on port **8080**, with `/health` for probes.

```bash
cp .env.example .env          # fill in JWT_KEY and TMDB_ACCESS_TOKEN
docker compose up --build     # API on http://localhost:8080
curl http://localhost:8080/health
```

- The container sets `ASPNETCORE_ENVIRONMENT=Production`, so Swagger is off
  and secrets must come from the environment.
- SQLite data is persisted in the `movieflix-data` named volume mounted at
  `/data`, so the database survives container restarts.
- The compose health check polls `/health` (readiness).

### Using PostgreSQL instead

```bash
# In .env:
#   DATABASE_PROVIDER=Postgres
#   POSTGRES_PASSWORD=<strong-password>
docker compose --profile postgres up --build
```

The `db` service only starts under the `postgres` profile.

---

## 4. Database: SQLite vs PostgreSQL

The provider is selectable in code (see `Program.cs`):

| `Database:Provider` | Connection string key | Default |
| --- | --- | --- |
| `Sqlite` | `ConnectionStrings:DefaultConnection` | ✅ development, tests |
| `Postgres` | `ConnectionStrings:Postgres` | — |

Setting `Database:Provider=Postgres` without `ConnectionStrings:Postgres`
throws a clear configuration error at startup.

### Schema history

There is exactly **one** migration set: `InitialMediaSchema` in
`backend/MovieFlix.Api/Migrations/` (namespace `MovieFlix.Api.Migrations`).
Old movie-only migrations and the duplicate `Data/Migrations` snapshot were
deleted. The database stores `MediaItems.MediaType` and `Genres.MediaType` as
lowercase `movie`/`tv` strings.

On startup `DatabaseSeeder.InitializeAsync` runs `Database.MigrateAsync()`, so
pending migrations are applied automatically and the `Admin`/`User` roles are
seeded (plus the optional admin account). For a guaranteed clean database:

```bash
# Stop the API first, then delete the SQLite database and its sidecars.
rm -f MovieFlix.db MovieFlix.db-wal MovieFlix.db-shm

# Option A: create the schema explicitly.
dotnet ef database update \
  --project backend/MovieFlix.Api/MovieFlix.Api.csproj

# Option B: start the API; startup migration creates the schema.
dotnet run --project backend/MovieFlix.Api/MovieFlix.Api.csproj
```

Verify the schema after creation:

```sql
SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;
SELECT MediaType, COUNT(*) FROM MediaItems GROUP BY MediaType;
```

### PostgreSQL caveat

> **Important:** the committed EF Core migrations were generated against
> SQLite. Applying them to PostgreSQL is not guaranteed. Before switching a
> deployment to Postgres, generate a provider-specific migration set:
>
> ```bash
> export Database__Provider=Postgres
> export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=movieflix;Username=movieflix;Password=..."
> dotnet ef migrations add InitialPostgresSchema \
>     --project backend/MovieFlix.Api/MovieFlix.Api.csproj \
>     --context MovieFlixDbContext \
>     --output-dir Migrations/Postgres
> ```
>
> Keep SQLite and Postgres migrations in **separate contexts/assemblies** — EF
> discovers every migration in an assembly, and mixing both providers in one
> set breaks `MigrateAsync()`. Do not claim PostgreSQL production support until
> the separate set has been applied and exercised.

---

## 5. Health, logging and traffic protection

### Health endpoints

| Path | Checks | Use |
| --- | --- | --- |
| `/health/live` | None (process liveness) | Kubernetes liveness / fast process probe |
| `/health/ready` | Registered checks (EF Core database) | Kubernetes readiness |
| `/health` | Alias for readiness (all registered checks) | Docker/orchestrator default probe |

Healthy responses are `200`; an unreachable database makes readiness/`/health`
return `503`.

### Logging

Serilog reads the `Serilog` section of `appsettings.json` and writes
structured events to the console. Requests are logged by
`UseSerilogRequestLogging()`. In the `Testing` environment the minimum level is
raised to `Warning` to keep test output clean.

### Output caching

All policies are in-memory output caching and apply only to public,
non-user-specific responses:

| Policy | TTL | Applied to |
| --- | --- | --- |
| `tmdb-search` | 5 min | `GET /api/v1/search` |
| `tmdb-trending` | 10 min | `GET /api/v1/media/trending` |
| `tmdb-feed` | 10 min | popular/top-rated/now-playing/upcoming/on-the-air/airing-today |
| `tmdb-discover` | 10 min | `GET /api/v1/media/{mediaType}/discover` |
| `tmdb-details` | 1 h | `GET /api/v1/media/{mediaType}/{tmdbId}` |
| `tmdb-season` | 6 h | `GET /api/v1/media/tv/{tmdbId}/seasons` |
| `tmdb-genres` | 24 h | `GET /api/v1/genres/{mediaType}` |

Season **details** (`.../seasons/{seasonNumber}`) are **not** output-cached
because authenticated responses include per-user episode progress. `/state`,
ratings, reviews, all `/api/v1/me/*`, auth, and admin routes are also never
cached. Restarting the API clears the in-memory cache.

### Rate limiting

A global fixed-window budget applies to every request (per authenticated user
id, otherwise per remote IP), and a stricter `auth` policy applies to every
`/api/v1/auth/*` action. Exceeding either budget returns `429` Problem Details
with `Retry-After`:

```bash
export RateLimiting__GlobalPermitLimit=120
export RateLimiting__GlobalWindowSeconds=60
export RateLimiting__AuthPermitLimit=10
export RateLimiting__AuthWindowSeconds=60
```

### TMDB resilience and error mapping

TMDB calls use a standard resilience handler: 3 retries with exponential
backoff and jitter, 5 s attempt timeout, 20 s total timeout, and a circuit
breaker (50% failure ratio, 5-request minimum throughput, 30 s sampling, 15 s
break). API mapping:

- TMDB 404 → API **404**
- TMDB 429 → API **503** with `Retry-After`
- Other TMDB non-success → API **502**
- Network failure/timeout/open circuit → API **503**

### CORS and compression

- Allowed browser origins come from `Cors:AllowedOrigins` (empty by default;
  native clients are unaffected).
- Brotli/Gzip compression is enabled for `application/json` and
  `application/problem+json`, including over HTTPS.

### Behind a reverse proxy

If every client appears to share one IP (rate limiting), HTTPS redirects or
generated URLs use the wrong scheme/host, or logs show the proxy address, the
forwarded-headers middleware is not active or the proxy is not trusted:

```bash
export ForwardedHeaders__Enabled=true
export ForwardedHeaders__KnownProxies__0="10.0.0.10"
# or a CIDR range:
export ForwardedHeaders__KnownNetworks__0="10.0.0.0/8"
```

The middleware honors `X-Forwarded-For` and `X-Forwarded-Proto`. Do **not**
enable it without restricting known proxies/networks — otherwise clients can
spoof their source IP and bypass IP-based rate limiting.

---

## 6. Backups

### SQLite

The database is a single file. With the default container setup it lives at
`/data/MovieFlix.db` inside the `movieflix-data` volume.

```bash
# Snapshot the volume
docker run --rm -v movieflix_movieflix-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/movieflix-$(date +%F).tar.gz -C /data .
```

For a consistent copy, stop the API (or take the snapshot quiesced) so the
`-wal`/`-shm` files are in sync.

### PostgreSQL

```bash
docker compose --profile postgres exec db \
  pg_dump -U movieflix -d movieflix > movieflix-$(date +%F).sql
```

Restore:

```bash
docker compose --profile postgres exec -T db \
  psql -U movieflix -d movieflix < movieflix-$(date +%F).sql
```

Schedule either with cron/a scheduled job, and store backups off-host.

---

## 7. Hardening & further work

- **Rotate** the JWT signing key periodically. Token lifetimes are configured
  via `Jwt:AccessTokenMinutes` and `Jwt:RefreshTokenDays`.
- **HTTPS:** the container terminates HTTP on 8080 — put it behind a reverse
  proxy (nginx/Caddy) or load balancer that terminates TLS, and configure
  forwarded headers as described above.
- **CORS:** populate `Cors:AllowedOrigins` before enabling a web front-end.
- **Database:** back up before migrations; rehearse the fresh-DB path in §4.
- **Postgres migrations** need a dedicated migration set before production.
- **Observability beyond logs:** add OpenTelemetry traces/metrics and export
  to a backend of your choice; the Serilog + health-check foundation is
  already in place.
- **CI:** `.github/workflows/ci.yml` restores, builds with `-warnaserror`, and
  tests on push/PR to `dev`/`main` using `9.0.x`. It uses the hermetic
  `Testing` environment, so no secrets or network TMDB calls are required.
- **Docker:** build and run the image at least once in CI or locally before
  relying on the compose topology.
