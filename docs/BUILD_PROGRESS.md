# MovieFlix Backend â€” Build Progress

> Living log of the backend build-out. **Never use README.md for this** â€” README is the
> public overview; this is the working diary (what we did, where we stopped, what's left).

**Branch:** `dev` (never sync/push, never touch `main`)
**Started:** 2026-10-15

---

## Decisions locked (from planning)

| Decision | Choice |
|---|---|
| Identity | Migrate to **full ASP.NET Core Identity** (IdentityDbContext) |
| User key type | **Default string (GUID)** keys |
| Movie storage | **Keep local `Movies` cache table** â€” userâ†’movie links reference `Movie.Id` |
| Scope | **Full feature set** (all 10 features + cross-cutting + ops) |
| Stop-before-Swift target | End of Phase 3 = usable backend for SwiftUI v2 |

---

## Status legend

â¬œ Not started Â· ðŸŸ¦ In progress Â· âœ… Done Â· â¸ï¸ Paused/blocked

---

## Phase 0 â€” Secret hygiene âœ…

- [x] Add `UserSecretsId` to `MovieFlix.Api.csproj`
- [x] Move `Jwt:Key` + `Tmdb:AccessToken` out of `appsettings.json` â†’ User Secrets
- [x] Update README run instructions
- [x] Secrets never committed (verified git history)
- [x] `appsettings.json` no longer git-ignored

## Phase 1 â€” Identity migration âœ…

- [x] 1a. Install `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- [x] 1b. Create `ApplicationUser : IdentityUser`
- [x] 1c. `MovieFlixDbContext : IdentityDbContext<ApplicationUser>` (keep Movie/Favorite config, `base.OnModelCreating`, `Favorite.UserId` â†’ string)
- [x] 1d. Rewrite `AuthService` on `UserManager<ApplicationUser>`
- [x] 1e. Update `JwtService` + `CurrentUserService` for string user IDs
- [x] 1f. Role + admin seeding (DatabaseSeeder)
- [x] 1g. Reset DB + regenerate migrations (Identity + Movies + Favorites + RefreshTokens)
- [x] 1h. Update tests to use UserManager + ApplicationUser
- [x] Refresh tokens (model + `/auth/refresh` + `/auth/logout` + rotation + revocation)

## Phase 2 â€” Core user features âœ…

- [x] 2a. Watchlist â€” `/api/v1/me/watchlist` (add, list, remove; TMDB-id add)
- [x] 2b. Movie Ratings (+ community average) â€” `/api/v1/me/ratings`, `/api/v1/movies/{id}/ratings`
- [x] 2c. Watch History (continue watching / recently watched) â€” `/api/v1/me/history`
- [x] 2d. Reviews (CRUD + ownership + pagination) â€” `/api/v1/movies/{id}/reviews`, `/api/v1/reviews/{id}`
- [x] 2e. Collections (many-to-many) â€” `/api/v1/me/collections`
- [x] 2f. Search History â€” `/api/v1/me/search-history`
- [x] 2g. User Preferences â€” `/api/v1/me/preferences`

## Phase 3 â€” TMDB resilience & caching âœ…

- [x] 3a. Polly (retry + timeout + circuit breaker) via `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`)
- [x] 3b. Output caching (memory-backed; `tmdb-search` + `tmdb-details` policies)
- [x] 3c. Extract shared `IMovieCacheService` (removed duplicate upsert from Favorite/Watchlist)
- [x] 3d. Background cache warm-up (`MovieCacheWarmupService`, opt-in via `Tmdb:Warmup:Enabled`)

## Phase 4 â€” Professional API polish âœ…

- [x] CORS (configurable `Cors:AllowedOrigins`, `MovieFlixClient` policy)
- [x] Rate limiting (configurable global + strict `auth` policy; `appsettings.json` `RateLimiting` section)
- [x] Serilog structured logging (`Serilog.AspNetCore` + `WriteTo: Console`)
- [x] Health checks (`/health`, EF Core `AddDbContextCheck`)
- [x] API versioning formalized (`Asp.Versioning.Mvc`, `[ApiVersion("1.0")]`, reports `api-supported-versions`)
- [x] Validation polish (FluentValidation intentionally skipped â€” DataAnnotations used)
- [x] Renames: `MovieQueyRequest` â†’ `MovieQueryRequest`, `AuthController.login` â†’ `Login`
- [x] Hermetic tests: `appsettings.Testing.json` + `Testing` env (no dependency on dev user-secrets)

## Phase 5 â€” Production ops âœ…

- [x] Dockerfile (multi-stage, non-root, curl for health probe) + `.dockerignore`
- [x] docker-compose (API + optional Postgres behind a profile, `/health` probe)
- [x] Postgres provider option (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Database:Provider`)
- [x] GitHub Actions CI (restore/build/test on push + PR)
- [x] Secrets-in-prod docs â€” `docs/OPERATIONS.md` (env vars, Key Vault)
- [x] Backup / observability notes â€” `docs/OPERATIONS.md`
- [ ] âš ï¸ Docker + Postgres paths authored but **not executed** (no Docker on the dev machine) â€” validate before relying on them

---

## Session log

### 2026-10-15 â€” Session start
- Read full backend + SwiftUI v1. Audited against 9-level plan.
- Locked decisions (see table above).
- Kicked off Phase 0 (secret hygiene) â†’ Phase 1 (Identity).
- Created this progress doc.

### 2026-10-16 â€” Phase 2 complete
- **2a Watchlist**: `WatchlistItem` (unique UserId+MovieId), `WatchlistService`
  (+ TMDB-id add reusing the cache-upsert pattern), `WatchlistController`.
- **2b Ratings**: `MovieRating` (1â€“5, unique per user+movie), upsert + community
  average via DB aggregate, `RatingsController` (public community endpoint).
- **2c Watch History**: `WatchHistoryEntry` (progress + completed), Continue
  Watching + Recently Watched (paged), clear/remove, `HistoryController`.
- **2d Reviews**: `Review` (1 per user+movie), CRUD with ownership
  (`ForbiddenAccessException` â†’ 403), paginated public listing,
  `ReviewAlreadyExistsException` â†’ 409, `ReviewsController`.
- **2e Collections**: `Collection` + `CollectionMovie` (many-to-many), ownership
  checks, add/remove movies, `DuplicateResourceException` â†’ 409,
  `CollectionsController`.
- **2f Search History**: `SearchHistoryEntry` (case-insensitive dedupe via
  `NormalizedQuery`), recent/remove/clear, `SearchHistoryController`.
- **2g User Preferences**: `UserPreferences` (1:1), get-or-create defaults,
  validated update (language/region/theme), `PreferencesController`.
- New shared exceptions + `GlobalExceptionHandler` branches (403/409).
- Migrations: `AddWatchlist`, `AddMovieRatings`, `AddWatchHistory`, `AddReviews`,
  `AddCollections`, `AddSearchHistory`, `AddUserPreferences`.
- Tests: **47 passing** (was 17 at end of Phase 1).

### 2026-10-16 â€” Phase 3 complete (TMDB resilience & caching)
- **3a Resilience**: added `Microsoft.Extensions.Http.Resilience` (pinned 9.10.0) and
  `AddStandardResilienceHandler` on the TMDB `HttpClient` â€” retry (3, exponential +
  jitter), attempt timeout (5s), total timeout (20s), circuit breaker
  (50% / 5 min-throughput / 30s sampling / 15s break). `TmdbService` translates
  Polly `TimeoutRejectedException` + `BrokenCircuitException` into
  `TmdbServiceException` (â†’ 502).
- **3b Output caching**: `AddOutputCache` + `UseOutputCache`; policies
  `tmdb-search` (5 min, vary by query/page) and `tmdb-details` (1 h, vary by
  route `tmdbId`) applied to the two TMDB-backed GETs in `MoviesController`.
- **3c Shared cache**: new `IMovieCacheService`/`MovieCacheService` (check â†’ TMDB
  fetch outside the transaction â†’ re-check inside â†’ insert). `FavoriteService` and
  `WatchlistService` now delegate to it; removed the duplicated TMDB-upsert code
  and their direct `ITmdbService` dependency.
- **3d Warm-up**: `MovieCacheWarmupService : BackgroundService` pre-caches
  configured TMDB ids; **off by default** (`Tmdb:Warmup:Enabled=false`) so local
  runs and tests make no surprise network calls.
- Tests: **50 passing** (added `MovieCacheServiceTests` with a fake `ITmdbService`).

### 2026-10-16 â€” Phase 4 complete (professional API polish) â€” commit `49dc818`
- **Renames**: `MovieQueyRequest` â†’ `MovieQueryRequest` (new file + refs in
  `MoviesController`/`IMovieService`/`MovieService`); `AuthController.login` â†’ `Login`
  (already correct, no change).
- **Packages** (API csproj): `Asp.Versioning.Mvc` 8.1.1, `Serilog.AspNetCore` 9.0.0,
  `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 9.0.20.
- **Serilog**: `builder.Host.UseSerilog(...)` reading the `Serilog` config section +
  `WriteTo: Console`; `app.UseSerilogRequestLogging()` after the exception handler.
  In the `Testing` environment the minimum level is forced to Warning to keep
  test output clean.
- **API versioning**: `AddApiVersioning(...).AddMvc()` (the `.AddMvc()` call is what
  actually wires versioning into MVC â€” without it `ReportApiVersions` silently does
  nothing, caught by a test). Default 1.0, `AssumeDefaultVersionWhenUnspecified`,
  `ReportApiVersions`; every controller now carries `[ApiVersion("1.0")]` and the URL
  contract `/api/v1/...` is unchanged.
- **CORS**: `Cors:AllowedOrigins` config â†’ `MovieFlixClient` policy (empty by default
  so native clients are unaffected).
- **Rate limiting**: `AddRateLimiter` with a global fixed-window partitioner
  (per authenticated user id, else per IP) + a named `auth` policy (per IP) applied to
  `/auth/register|login|refresh` via `[EnableRateLimiting("auth")]`; rejection â†’ 429.
  Limits live in `RateLimitingSettings` bound from `appsettings.json`.
- **Health checks**: `AddHealthChecks().AddDbContextCheck<MovieFlixDbContext>` exposed
  at `/health`.
- **Hermetic tests (important fix)**: the test host now runs in the `Testing`
  environment with a new `appsettings.Testing.json`. Previously tests silently depended
  on the developer's user-secrets for `Jwt:Key` (user secrets only load in Development),
  which would break in CI. The Testing file supplies a test JWT key/issuer/audience, a
  dummy TMDB token and relaxed rate limits. New `CrossCuttingIntegrationTests` covers
  `/health` â†’ Healthy, the `api-supported-versions` header, 401 on protected routes, and
  the `auth` limiter returning 429 (limit overridden via `UseSetting`).
- Tests: **54 passing**; run time dropped from ~17s to ~4s because EF console logging and
  Serilog are silenced in `Testing`.

### 2026-10-16 â€” Phase 5 complete (production ops)
- **Postgres provider option**: added `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4 and
  made the provider configurable via `Database:Provider` (`Sqlite` default, `Postgres`
  opt-in) reading `ConnectionStrings:DefaultConnection` / `ConnectionStrings:Postgres`.
  A missing Postgres connection string now fails fast with a clear error. Tests keep
  using SQLite through the factory override.
  - âš ï¸ **Not verified against a live Postgres** (no Docker/DB on this machine). Also,
    the committed migrations are SQLite-generated â€” a provider-specific migration set
    must be generated before a real Postgres deployment (documented in OPERATIONS.md Â§4).
- **Docker**: `backend/Dockerfile` (multi-stage SDKâ†’aspnet, `dotnet publish`, curl for
  the health probe, runs as non-root `$APP_UID`, listens on 8080, `Production` env) +
  `backend/.dockerignore` (excludes `bin/`, `obj/`, `*.db`).
- **Compose**: root `docker-compose.yml` â€” `api` service (SQLite persisted in a named
  volume at `/data`, `/health` probe, secrets from `.env` with fail-fast `${VAR:?}`)
  plus an optional `db` (Postgres 17) behind the `postgres` profile. `.env.example`
  template; `.env` git-ignored (root `.gitignore` added).
- **CI**: `.github/workflows/ci.yml` â€” restore/build/test on push+PR to `dev`/`main`,
  NuGet cache, read-only permissions. Dormant until pushed (per project rule: no pushes).
- **Docs**: new `docs/OPERATIONS.md` (environments, config/secret table, double-underscore
  env convention, Key Vault, Docker, SQLite vs Postgres + migrations, health/logging/rate
  limiting, backups, hardening).
- Tests: **54 passing**, build clean with the extra provider package.
- âš ï¸ **Still outstanding:** rotate the leaked TMDB token on themoviedb.org.

### 2026-10-25 — Unified movies+TV migration (completed by agent)
- **.NET 9** confirmed: API and tests target net9.0; SDK 9.0.318 verified.
- **Schema reset**: deleted old movie-only migrations and local DB files; created single clean InitialMediaSchema migration in ackend/MovieFlix.Api/Migrations/ (and copy in Data/Migrations/).
- **Domain**: unified MediaItem (movie/tv), TV seasons/episodes, user links refactored to MediaItemId (Favorite, WatchlistItem, MediaRating, Review, CollectionItem, MediaWatchState, EpisodeWatchState).
- **TMDB**: extended service for multi/search/feeds/details/seasons/genres; media cache service; output caching varies by query/route params.
- **API**: new public/media/me routes for movies+TV, episode progress/next-episode/Continue Watching, idempotent membership writes (PUT/DELETE), anonymous-safe state.
- **Auth/validation/errors**: standardized ProblemDetails, rate limiting, forwarded headers, response compression, XML docs/Swagger.
- **Tests**: rewrote test suite with fake ITmdbService, SQLite in-memory/WebApplicationFactory; all tests passing with no external network calls.
- **Docs**: added docs/API_CONTRACT.md, docs/openapi.json (exported from Swagger), updated README.md, docs/OPERATIONS.md notes, added scripts/smoke.ps1. dotnet build and dotnet test green.

