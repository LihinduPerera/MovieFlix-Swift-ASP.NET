# MovieFlix â€” Operations Guide

How the backend is configured, deployed, observed and backed up. This sits
alongside `docs/BUILD_PROGRESS.md` (the build diary) and the top-level
`README.md` (project overview).

> **Status note:** the Docker assets and PostgreSQL provider path in this guide
> are authored to production standards but were **not executed locally** (Docker
> is not installed on the development machine). Validate them in a container
> environment before relying on them.

---

## 1. Environments

ASP.NET Core reads `appsettings.{ENVIRONMENT}.json` on top of
`appsettings.json`, where `{ENVIRONMENT}` comes from `ASPNETCORE_ENVIRONMENT`
(defaults to `Development` locally).

| Environment   | File                     | Behaviour                                                                 |
| ------------- | ------------------------ | ------------------------------------------------------------------------- |
| `Development` | `appsettings.Development.json` | Swagger UI, detailed EF errors + sensitive data logging, loads **user secrets** |
| `Testing`     | `appsettings.Testing.json`     | Used only by the integration-test host; quiet logging, relaxed rate limits, self-contained test secrets |
| `Production`  | â€”                        | No Swagger, no EF diagnostics, secrets must come from the environment     |

`appsettings.Testing.json` exists so the test suite never depends on a
developer's machine secrets or on network access. The test host selects it
explicitly via `builder.UseEnvironment("Testing")`.

---

## 2. Configuration & secrets

Secrets are **never** stored in `appsettings.json` (that file holds only
non-secret defaults). They are supplied per environment:

| Key                        | Local development                  | Production                                   |
| -------------------------- | ---------------------------------- | -------------------------------------------- |
| `Jwt:Key`                  | `dotnet user-secrets set`          | `Jwt__Key` env var / secret manager          |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` (defaults)    | override only if needed                      |
| `Tmdb:AccessToken`         | `dotnet user-secrets set`          | `Tmdb__AccessToken` env var / secret manager |
| `ConnectionStrings:Postgres` | `dotnet user-secrets set`        | `ConnectionStrings__Postgres` env var        |

### The double-underscore convention

Configuration providers flatten nested keys with `__`, so **everything in
`appsettings.json` can be set from the environment** without code changes:

```bash
export Jwt__Key="<strong-key>"
export Tmdb__AccessToken="<tmdb-read-token>"
export Database__Provider="Postgres"
export ConnectionStrings__Postgres="Host=db;Port=5432;Database=movieflix;Username=movieflix;Password=..."
export Cors__AllowedOrigins__0="https://app.example.com"
export RateLimiting__AuthPermitLimit="5"
```

### Production secret storage options

- **Environment variables** â€” simplest; pair with the secret store of your
  orchestrator (systemd unit, ECS task definition, Kubernetes Secret).
- **Azure Key Vault / AWS Secrets Manager / HashiCorp Vault** â€” mount the
  secrets as configuration, e.g. the Azure Key Vault configuration provider:

  ```bash
  dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
  ```

  ```csharp
  builder.Configuration.AddAzureKeyVault(
      new Uri("https://<vault>.vault.azure.net/"),
      new DefaultAzureCredential());
  ```

  Then reference the same logical keys (`Jwt--Key`, `Tmdb--AccessToken` â€”
  Key Vault uses `--` for nesting).
- **Docker Compose** â€” secrets are read from `.env` (see `.env.example`),
  which is git-ignored. Compose fails fast at startup if `JWT_KEY` or
  `TMDB_ACCESS_TOKEN` is missing.

> **Action item:** the TMDB token that was previously committed must be
> rotated at <https://www.themoviedb.org/settings/api>. Rotating invalidates
> the leaked value.

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
- The compose file defines a health check that polls `/health`.

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

| `Database:Provider` | Connection string key               | Default |
| ------------------- | ----------------------------------- | ------- |
| `Sqlite`            | `ConnectionStrings:DefaultConnection` | âœ… (development, tests) |
| `Postgres`          | `ConnectionStrings:Postgres`          | â€”      |

Setting `Database:Provider=Postgres` without `ConnectionStrings:Postgres`
throws a clear configuration error at startup rather than failing later.

> **Important:** the committed EF Core migrations were generated against
> SQLite. Applying them to PostgreSQL is not guaranteed. Before switching a
> deployment to Postgres, generate a provider-specific migration set:
>
> ```bash
> export Database__Provider=Postgres
> export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=movieflix;Username=movieflix;Password=..."
> dotnet ef migrations add AddPostgresSchema \
>     --project backend/MovieFlix.Api/MovieFlix.Api.csproj \
>     --context MovieFlixDbContext \
>     --output-dir Data/Migrations/Postgres
> ```
>
> Keep SQLite and Postgres migrations in **separate contexts/assemblies** â€”
> EF discovers every migration in an assembly, so mixing both providers in
> one set will break `MigrateAsync()`.

On startup the app runs `Database.MigrateAsync()`, which applies pending
migrations automatically.

---

## 5. Health, logging and traffic protection

- **Health:** `GET /health` returns `200 Healthy` when the database can be
  reached (EF Core `AddDbContextCheck`). Use it as the container/orchestrator
  probe.
- **Logging:** Serilog, configured from the `Serilog` section of
  `appsettings.json`, writes structured events to the console. Requests are
  logged by `UseSerilogRequestLogging()`. In the `Testing` environment the
  minimum level is raised to `Warning` to keep test output clean.
- **Rate limiting:** a global fixed-window budget per client plus a stricter
  `auth` policy applied to `/api/v1/Auth/register|login|refresh`. Exceeding
  the budget returns `429`. Limits are configurable:

  ```bash
  export RateLimiting__GlobalPermitLimit=120
  export RateLimiting__AuthPermitLimit=10
  ```

- **Resilience:** TMDB calls are wrapped in a standard resilience handler
  (retry with exponential backoff, attempt/total timeouts, circuit breaker).
  Responses are also output-cached (5 min for search, 1 h for details).
- **CORS:** allowed browser origins come from `Cors:AllowedOrigins`. Native
  clients are unaffected.

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

- **Rotate** the JWT signing key periodically; token lifetime is configured
  via `Jwt:AccessTokenMinutes` (access) and `Jwt:RefreshTokenDays` (refresh).
- **HTTPS:** the container terminates HTTP on 8080 â€” put it behind a reverse
  proxy (nginx/Caddy) or load balancer that terminates TLS.
- **CORS:** populate `Cors:AllowedOrigins` before enabling a web front-end.
- **Postgres migrations** need a dedicated migration set (see Â§4) before a
  production Postgres deployment.
- **Observability beyond logs:** add OpenTelemetry traces/metrics and export
  to a backend of your choice; the Serilog + health-check foundation is
  already in place.
- **CI:** `.github/workflows/ci.yml` restores, builds and tests on push/PR.
  Consider adding a container-image build job once the Docker image is
  validated.

## MovieFlix v2 notes (movies + TV)
- **Target framework:** .NET 9 (net9.0). Keep SDK/runtime/Docker/CI/Packages compatible with .NET 9.
- **Migrations:** committed migrations are SQLite-generated (InitialMediaSchema). Before real Postgres deployment, generate and validate a separate Postgres migration set (do not claim Postgres production support until done).
- **TMDB warmup:** use { mediaType, tmdbId } items in Tmdb:Warmup:Items. Warm details/seasons as configured; avoid warming episodes.
- **Output caching:** varies by query/route params; authenticated/user-specific responses are not output-cached.
- **Liveness/Readiness:** GET /health/live (self), GET /health/ready (DB), GET /health alias.
- **Token rotation:** refresh uses atomic/conditional rotation; reuse revokes the refresh token chain.

