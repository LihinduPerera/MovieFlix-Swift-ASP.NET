# MovieFlix

MovieFlix is a unified movie and TV discovery platform with a SwiftUI client and an ASP.NET Core REST API.

> **Project status:** The ASP.NET Core backend is actively being developed and tested. The SwiftUI V2 client is part of the ongoing project. Features and structure may change as development continues.

## Current backend features

- **Movies and TV from TMDB** — search (multi/movie/tv), trending/feeds, details, seasons/episodes with metadata from TMDB.
- **Episode-level watch tracking** — per-episode progress, next-episode resolution, and Continue Watching across movies and TV.
- **REST API** — versioned endpoints under `/api/v1`; the API reports its supported versions via the `api-supported-versions` response header.
- **User accounts and authentication** — ASP.NET Core Identity with password hashing, JWT-based login, refresh-token rotation, and role-based authorization.
- **Library parity** — favorites, watchlist, ratings (1–5 with community average), reviews, collections, watch history, search history, and user preferences for both movies and TV.
- **Database persistence** — Entity Framework Core with SQLite by default (Postgres configurable).
- **TMDB resilience and caching** — automatic retries, timeouts, circuit breaker, and output caching for TMDB-backed responses.
- **Rate limiting** — a global per-client request budget plus a stricter limit on the login, register, and refresh endpoints (configurable).
- **Health checks** — `/health`, `/health/live`, `/health/ready` endpoints.
- **Structured logging** — request/response logging via Serilog writing structured events to the console.
- **CORS** — configurable allowed origins for a future web front-end.
- **Validation and error handling** — consistent Problem Details responses for API errors, including invalid input and missing records.
- **Authorization** — protected operations using JWT authentication and role-based access rules.
- **Automated tests** — 76 hermetic integration/unit tests using xUnit and an isolated SQLite database (no external network calls, fake TMDB service).
- **One clean schema** — a single `InitialMediaSchema` EF Core migration; the database is recreated from scratch and media types are stored as lowercase `movie`/`tv`.

See [docs/API_CONTRACT.md](docs/API_CONTRACT.md) for the complete client-facing contract and [docs/openapi.json](docs/openapi.json) for the OpenAPI document.

## Tech stack

| Area | Technologies |
| --- | --- |
| iOS client | Swift, SwiftUI |
| Backend | C#, ASP.NET Core Web API, **.NET 9** |
| Database | SQLite (default), PostgreSQL (configurable) |
| Data access | Entity Framework Core |
| Authentication | ASP.NET Core Identity, JWT bearer authentication, refresh tokens, role-based authorization |
| External movie data | [The Movie Database (TMDB)](https://www.themoviedb.org/) |
| Resilience | Polly via `Microsoft.Extensions.Http.Resilience` (retry, timeout, circuit breaker) |
| Caching | ASP.NET Core output caching |
| API documentation | Swagger / OpenAPI |
| Testing | xUnit, ASP.NET Core integration testing |
| Logging | Serilog (structured console logging) |
| API versioning | Asp.Versioning (v1, reported on responses) |
| Rate limiting | ASP.NET Core rate limiting |
| Health checks | ASP.NET Core health checks |
| Deployment | Docker, docker-compose, GitHub Actions CI |

## Repository structure

``` text
MovieFlix-Swift-ASP.NET/
├── frontend/
│   ├── v1/                 # Initial SwiftUI version
│   └── v2/                 # SwiftUI client under development
└── backend/
    ├── MovieFlix.Api/      # ASP.NET Core Web API
    └── MovieFlix.Api.Tests/# Automated tests
```

## How it fits together

``` text
SwiftUI app
    |
    | HTTP requests / JSON
    v
ASP.NET Core REST API
    |                 \
    v                  v
SQLite/Postgres DB    TMDB API
(users, media cache,
 favorites, watchlist,
 ratings, reviews,
 collections, history,
 episode progress)
```

## Run the backend locally

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A TMDB API key (read token) for endpoints that use TMDB
- Git

### 1. Clone and configure secrets

```bash
git clone https://github.com/LihinduPerera/MovieFlix-Swift-ASP.NET.git
cd MovieFlix-Swift-ASP.NET/backend/MovieFlix.Api
dotnet user-secrets set "Tmdb:AccessToken" "<your-tmdb-read-token>"
dotnet user-secrets set "Jwt:Key" "<a-strong-random-key>"
```

### 2. Build and run

```bash
cd ../..
dotnet build backend/MovieFlix.sln
dotnet run --project backend/MovieFlix.Api/MovieFlix.Api.csproj
```

The launch profiles serve `http://localhost:5122` and
`https://localhost:7014`; Swagger UI is at `/swagger` in Development. On first
startup the app applies the single `InitialMediaSchema` migration and seeds the
`Admin`/`User` roles. To recreate the database from scratch, stop the API and
delete `MovieFlix.db` (plus `-wal`/`-shm`); startup recreates it. See
[Database operations](docs/OPERATIONS.md#4-database-sqlite-vs-postgresql) for
explicit `dotnet ef database update` steps.

### 3. Test

```bash
dotnet test backend/MovieFlix.sln
```

The suite runs in the `Testing` environment against an isolated SQLite
database with a fake TMDB service; it requires no secrets and makes no external
network calls. Expected result: **76 passed, 0 failed**.

See [docs/OPERATIONS.md](docs/OPERATIONS.md) for full configuration (Postgres, Docker, env vars, migrations).

## API overview (high level)

Base path: `/api/v1`. Key areas: auth, search/discovery, media details (movies/TV + seasons/episodes), `/me` profile/library (favorites/watchlist/ratings/history/continue/reviews/collections/search-history/preferences), admin media refresh, health. Full details in [docs/API_CONTRACT.md](docs/API_CONTRACT.md).

## Acknowledgements

- [The Movie Database (TMDB)](https://www.themoviedb.org/) for movie/TV data. This product uses the TMDB API but is not endorsed or certified by TMDB.

**Built while learning SwiftUI, C#, and ASP.NET Core.**
