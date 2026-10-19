# MovieFlix 🎬

MovieFlix is a movie-discovery app that I'm rebuilding as I learn
**SwiftUI and ASP.NET Core**. The project is evolving from an initial
SwiftUI app into a client--server application with a dedicated REST API,
database persistence, and authentication.

The goal is not just to make the app work, but to understand the
engineering decisions behind it and build a foundation that can grow.

> **Project status:** The ASP.NET Core backend is actively being
> developed and tested. The SwiftUI V2 client is part of the ongoing
> project. Features and structure may change as development continues.

## ✨ Current backend features

-   **Movie data from TMDB** --- search for movies and retrieve movie
    details through the backend.
-   **REST API** --- versioned endpoints under `/api/v1`.
-   **User accounts and authentication** --- ASP.NET Core Identity with
    password hashing, JWT-based login, refresh-token rotation, and
    role-based authorization.
-   **Favorites** --- add, list, and remove a user's favorite movies.
-   **Watchlist** --- a separate "watch later" list per user.
-   **Ratings** --- 1--5 star ratings with a community average per movie.
-   **Watch history** --- playback progress, a Continue Watching rail, and
    a paginated Recently Watched list.
-   **Reviews** --- create, read, update, and delete movie reviews with
    ownership rules and pagination.
-   **Collections** --- user-defined collections that group movies
    (many-to-many).
-   **Search history** --- recent, de-duplicated search terms per user.
-   **User preferences** --- language, region, theme, and toggles per user.
-   **Database persistence** --- Entity Framework Core with SQLite.
-   **Validation and error handling** --- consistent Problem Details
    responses for API errors, including invalid input and missing
    records.
-   **Authorization** --- protected operations using JWT authentication
    and role-based access rules.
-   **Automated tests** --- service tests and API integration tests
    using xUnit and an isolated SQLite in-memory database.

The backend test suite currently contains **47 passing test cases**.

## 🧰 Tech stack

  --------------------------------------------------------------------------
  Area                                Technologies
  ----------------------------------- --------------------------------------
  iOS client                          Swift, SwiftUI

  Backend                             C#, ASP.NET Core Web API, .NET 9

  Database                            SQLite

  Data access                         Entity Framework Core

  Authentication                      ASP.NET Core Identity, JWT bearer
                                      authentication, refresh tokens,
                                      role-based authorization

  External movie data                 [The Movie Database
                                      (TMDB)](https://www.themoviedb.org/)

  API documentation                   Swagger / OpenAPI

  Testing                             xUnit, ASP.NET Core integration
                                      testing
  --------------------------------------------------------------------------

## 🗂️ Repository structure

``` text
MovieFlix-Swift-ASP.NET/
├── frontend/
│   ├── v1/                 # Initial SwiftUI version
│   └── v2/                 # SwiftUI client under development
└── backend/
    ├── MovieFlix.Api/      # ASP.NET Core Web API
    └── MovieFlix.Api.Tests/# Automated tests
```

The exact contents may evolve as the project grows.

## 🏗️ How it fits together

``` text
SwiftUI app
    |
    | HTTP requests / JSON
    v
ASP.NET Core REST API
    |                 \
    v                  v
SQLite database       TMDB API
(users, movies,       (external movie data)
 favorites, watchlist,
 ratings, reviews,
 collections, ...)
```

The backend acts as the boundary between the iOS app, persisted user
data, and TMDB. This also provides a place to centralize validation,
authentication, authorization, and error handling.

## 🚀 Run the backend locally

### Prerequisites

-   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
-   A TMDB API key for endpoints that use TMDB
-   Git

### 1. Clone the repository

``` bash
git clone https://github.com/LihinduPerera/MovieFlix-Swift-ASP.NET.git
cd MovieFlix-Swift-ASP.NET
```

### 2. Configure the backend

Secrets are **not** stored in `appsettings.json` (that file only holds
non-secret defaults). Provide the two required secrets using **User
Secrets** (recommended for local development), environment variables, or
another ASP.NET Core configuration provider.

Using User Secrets (already wired up via the `UserSecretsId` in
`MovieFlix.Api.csproj`):

``` bash
cd backend/MovieFlix.Api

# TMDB API read access token (from https://www.themoviedb.org/settings/api)
dotnet user-secrets set "Tmdb:AccessToken" "<your-tmdb-token>"

# A strong, unique JWT signing key (>= 32 bytes) for local development
dotnet user-secrets set "Jwt:Key" "<your-strong-jwt-key>"
```

-   Do not commit real API keys, signing keys, passwords, or other
    secrets to Git. User Secrets are stored outside the repository.

> **Note:** `Jwt:Key`, `Tmdb:AccessToken`, and other secrets were removed
> from source control. If you are pulling this repo for the first time,
> the API will not authenticate or call TMDB until you set the secrets
> above.

### 3. Start the API

From the repository root, run:

``` bash
dotnet run --project .\backend\MovieFlix.Api\MovieFlix.Api.csproj
```

The local launch profile has been configured to use these development
URLs:

-   HTTP: `http://localhost:5122`
-   HTTPS: `https://localhost:7014`

Your active launch profile and configuration determine which URL is
available. Open Swagger if it is enabled in the current Development
configuration to explore the API.

### 4. Run the tests

From the repository root:

``` bash
dotnet test .\backend\MovieFlix.Api.Tests\MovieFlix.Api.Tests.csproj
```

The integration tests use an isolated SQLite in-memory database rather
than the development database.

## 🔌 API overview

The API uses the `/api/v1` route prefix. Representative endpoints
include:

  -----------------------------------------------------------------------------------------------------
  Purpose                 Endpoint                                              Access
  ----------------------- ----------------------------------------------------- -----------------------
  Register / log in       `POST /api/v1/Auth/register`, `POST /api/v1/Auth/login`  Public

  Refresh / log out       `POST /api/v1/Auth/refresh`, `POST /api/v1/Auth/logout`  Public / token

  Search movies through   `GET /api/v1/Movies/search?query=...`                 Public
  TMDB                                                                          

  Get TMDB movie details  `GET /api/v1/Movies/tmdb/{tmdbId}`                    Public

  List locally stored     `GET /api/v1/Movies`                                  Public
  movies                                                                        

  Get a locally stored    `GET /api/v1/Movies/{id}`                             Public
  movie                                                                         

  Manage local movie      `POST`, `PUT`, `DELETE /api/v1/Movies` routes         Restricted by
  records                                                                       authorization

  Manage favorites        `GET`, `POST`, `DELETE /api/v1/me/favorites`          Authenticated

  Manage watchlist        `GET`, `POST`, `DELETE /api/v1/me/watchlist`          Authenticated

  Rate movies             `GET`, `PUT`, `DELETE /api/v1/me/ratings/{movieId}`   Authenticated

  Community rating        `GET /api/v1/movies/{movieId}/ratings`                Public

  Watch history           `GET /api/v1/me/history`, `/continue`                 Authenticated

  Movie reviews           `GET`, `POST /api/v1/movies/{movieId}/reviews`        Public / Authenticated

  Edit / delete a review  `PUT`, `DELETE /api/v1/reviews/{reviewId}`            Authenticated (owner)

  Manage collections      `GET`, `POST`, `PUT`, `DELETE /api/v1/me/collections` Authenticated

  Search history          `GET`, `POST`, `DELETE /api/v1/me/search-history`     Authenticated

  User preferences        `GET`, `PUT /api/v1/me/preferences`                   Authenticated
  -----------------------------------------------------------------------------------------------------

Check the current controllers and Swagger documentation for exact
request bodies, route details, and authorization requirements, as the
API is still evolving.

## 🧪 Engineering practices I'm learning and applying

-   Keeping controllers, services, data access, and DTOs separated by
    responsibility.
-   Using asynchronous APIs for I/O work.
-   Validating incoming input and returning appropriate HTTP status
    codes.
-   Centralizing exception handling with Problem Details responses.
-   Enforcing database constraints and indexes where appropriate.
-   Protecting endpoints with authentication and authorization.
-   Testing behavior at both service and HTTP integration levels.
-   Using EF Core migrations to evolve the database schema.

This is a learning project, so I'm applying these practices
incrementally and refining the implementation as I learn more.

## 🛣️ What's next

-   Continue building the SwiftUI V2 client against the ASP.NET Core
    API.
-   Improve API test coverage and validation.
-   Refine configuration and deployment practices.
-   Continue reviewing security, maintainability, and performance as the
    project grows.

## 🙏 Acknowledgements

-   [The Movie Database (TMDB)](https://www.themoviedb.org/) for movie
    data.

This product uses the TMDB API but is not endorsed or certified by TMDB.

------------------------------------------------------------------------

**Built while learning SwiftUI, C#, and ASP.NET Core --- one step at a
time.**
