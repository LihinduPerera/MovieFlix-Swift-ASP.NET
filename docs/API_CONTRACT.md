# MovieFlix API Contract (v1)

This document is the client-facing contract for the MovieFlix ASP.NET Core API. The base path is `/api/v1`. All examples use `application/json` and UTC timestamps with `Z`.

**Framework:** .NET 9 (net9.0).  
**Database:** SQLite by default (committed migrations are SQLite-generated). A separate Postgres migration set must be generated and validated before a real Postgres deployment; Postgres production support is not claimed until that is complete.

## Environments and base URL
- Local (Development): `https://localhost:5001` or `http://localhost:5000`
- Health: `GET /health` (alias), `GET /health/live`, `GET /health/ready`

## Authentication
- Scheme: `Authorization: Bearer <accessToken>`
- Tokens: issued by `/api/v1/auth/*`. Access tokens are short-lived JWTs; refresh tokens are rotated.
- Anonymous access is allowed on public media/search routes; authenticated user state is returned when a valid token is provided.

## Token responses
`LoginResponse` (returned by register/login/refresh):
```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```
- `expiresIn` is seconds until access token expiry (UTC, ISO 8601 context implied by JWT `exp`).
- Refresh tokens are single-use and rotated; reuse is treated as a token compromise and revokes the refresh token chain (see below).

## Auth endpoints
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/register` | Public (rate-limited) | Creates account, assigns `User` role, issues tokens. Returns 201 + `LoginResponse`. |
| POST | `/api/v1/auth/login` | Public (rate-limited) | Returns 200 + `LoginResponse` on success. Invalid credentials → 401 ProblemDetails (generic). Locked out → 423. |
| POST | `/api/v1/auth/refresh` | Public (rate-limited) | Rotates tokens. Returns 200 + `LoginResponse`. Invalid/reused → 401 and reuse revokes the chain. |
| POST | `/api/v1/auth/logout` | Bearer | Returns 204. Revokes all refresh tokens for the user. |

Auth policy: applied to register/login/refresh. Lockout: `DefaultLockoutTimeSpan` 5 min, `MaxFailedAccessAttempts` 5. AccessFailed increments on bad password; failure count resets on success. Refresh rotation is atomic/conditional to prevent concurrent refresh reuse from succeeding twice.

## ProblemDetails
All errors use `application/problem+json` with RFC7807 fields. Validation errors use `errors` dictionary:
```json
{
  "type": "https://example.com/problems/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["A valid email address is required."]
  },
  "traceId": "..."
}
```
Common types/statuses: 400 validation/invalid params, 401 unauthorized, 403 forbidden, 404 not found, 409 conflict, 423 locked, 429 rate limited (+ `Retry-After` header), 502 bad gateway, 503 service unavailable.

## Pagination
`PagedResponse<T>`:
```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```
Defaults/limits follow endpoint docs; ordering is documented per list endpoint.

## Enums and formats
- `mediaType`: `"movie" \| "tv"` (lowercase). Persisted as strings.
- `search type`: `"multi" \| "movie" \| "tv"`
- `trending window`: `"day" \| "week"`
- Dates: `yyyy-MM-dd`. Timestamps: UTC ISO 8601 with `Z`.
- Public identity: `(mediaType, tmdbId)`. No local media IDs exposed publicly. User IDs only in `/api/v1/me` and auth responses.

## Images (TMDB)
Recommended sizes: posters `w342`/`w500`, backdrops `w780`, `original` when needed. Paths are raw TMDB relative paths as returned.

## Search and discovery (public)
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/search?query=&type=multi|movie|tv&page=1&language=en-US&region=US` | Public | Multi filters out person results. |
| GET | `/api/v1/media/trending?mediaType=all|movie|tv&window=day|week&page=1&language=en-US` | Public | Output cached (varies by params). |
| GET | `/api/v1/media/{mediaType}/popular?page=1&language=en-US&region=US` | Public |  |
| GET | `/api/v1/media/{mediaType}/top-rated?page=1&language=en-US&region=US` | Public |  |
| GET | `/api/v1/media/movie/now-playing?page=1&language=en-US&region=US` | Public |  |
| GET | `/api/v1/media/movie/upcoming?page=1&language=en-US&region=US` | Public |  |
| GET | `/api/v1/media/tv/on-the-air?page=1&language=en-US&region=US` | Public |  |
| GET | `/api/v1/media/tv/airing-today?page=1&language=en-US&region=US` | Public |  |
| GET | `/api/v1/genres/{mediaType}?language=en-US` | Public |  |

Invalid enum/route values return 400 ProblemDetails.

## Media details (public)
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/media/{mediaType}/{tmdbId}?language=en-US` | Public | `MediaDetailsResponse`. Movies have `runtimeMinutes`, empty `seasons`. TV includes `numberOfSeasons`, `numberOfEpisodes`, dates, seasons. Cast limited to top 20, key crew, YouTube videos only, first 12 similar. |
| GET | `/api/v1/media/tv/{tmdbId}/seasons?language=en-US` | Public | Season summaries. |
| GET | `/api/v1/media/tv/{tmdbId}/seasons/{seasonNumber}?language=en-US` | Public | `SeasonDetailsResponse`. If authenticated, populates per-episode `progressSeconds`/`completed`. |
| GET | `/api/v1/media/{mediaType}/{tmdbId}/state` | Public/Optional | `MediaStateResponse`. Anonymous returns false/null defaults. |
| GET | `/api/v1/media/{mediaType}/{tmdbId}/ratings` | Public/Optional | Community average/count; caller's rating when authenticated. |
| GET | `/api/v1/media/{mediaType}/{tmdbId}/reviews?page=1&pageSize=20` | Public | Paginated reviews. |
| POST | `/api/v1/media/{mediaType}/{tmdbId}/reviews` | Bearer | 201. Duplicate → 409. |

## Profile (/me)
All require auth.
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me` | Bearer | `MeResponse` (id/email/displayName/createdAt/roles). |
| PATCH | `/api/v1/me` | Bearer | Body `{ "displayName": "..." }` → `MeResponse`. |
| GET | `/api/v1/me/stats` | Bearer | Counts: favorites, watchlist, ratings, reviews, collections, moviesWatched, showsWatched, episodesWatched (watched counts require completion as implemented). |

## Library
All require auth, paginated where noted.
### Favorites
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/favorites?mediaType=&page=1&pageSize=20&sort=` | Bearer | Paginated. |
| PUT | `/api/v1/me/favorites/{mediaType}/{tmdbId}` | Bearer | 200 `SavedMediaResponse`, idempotent. |
| DELETE | `/api/v1/me/favorites/{mediaType}/{tmdbId}` | Bearer | 204, idempotent. |

### Watchlist
Same as favorites under `/api/v1/me/watchlist`.

### Ratings
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/ratings/{mediaType}/{tmdbId}` | Bearer | 200 or 404. |
| PUT | `/api/v1/me/ratings/{mediaType}/{tmdbId}` | Bearer | Body `{ "rating": 1..5 }` → 200, upsert. |
| DELETE | `/api/v1/me/ratings/{mediaType}/{tmdbId}` | Bearer | 204, idempotent. |

### History and episode progress
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/history?mediaType=&page=1&pageSize=20` | Bearer | `PagedResponse<WatchHistoryItemResponse>`. |
| GET | `/api/v1/me/history/continue?limit=10` | Bearer | Continue Watching list (movies in progress not completed; TV with current/next). |
| PUT | `/api/v1/me/history/{mediaType}/{tmdbId}` | Bearer | Body `{ "progressSeconds": n, "completed": bool, "seasonNumber":?, "episodeNumber":? }`. Validates progress >= 0. |
| GET | `/api/v1/me/history/tv/{tmdbId}` | Bearer | Episode states + next episode. |
| PUT | `/api/v1/me/history/tv/{tmdbId}/episodes/{seasonNumber}/{episodeNumber}` | Bearer | Body `{ "progressSeconds": n, "completed": bool }` → `EpisodeProgressResponse`. |
| DELETE | `/api/v1/me/history/{mediaType}/{tmdbId}` | Bearer | 204; removes aggregate and TV episode states. |
| DELETE | `/api/v1/me/history` | Bearer | 204; clears all history and episode states. |

Episode rules: order by (SeasonNumber, EpisodeNumber), exclude season 0, exclude future air dates. `nextEpisode` is first uncompleted ordered episode. On complete, set progress to runtime if known; mark media completed when no next episode remains. Explicit `completed` flag only (no auto-complete threshold). Progress preserved on refresh.

### Reviews (owned)
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/reviews?page=1&pageSize=20` | Bearer | Paginated. |
| PUT | `/api/v1/reviews/{reviewId}` | Bearer | Update owned review. 403 if not owner. |
| DELETE | `/api/v1/reviews/{reviewId}` | Bearer | 204, owner only; 403 if not owner. |

### Collections
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/collections?page=1&pageSize=20` | Bearer | Paginated summaries. |
| POST | `/api/v1/me/collections` | Bearer | Body `{ "name": "...", "description": "..." }` → 201. Duplicate name (case-insensitive per owner) → 409. |
| GET | `/api/v1/collections/{id}` | Public/Optional | `CollectionDetailResponse` (public view). `isOwner` when authenticated. |
| PUT | `/api/v1/me/collections/{id}` | Bearer | 200. 403 if not owner. |
| DELETE | `/api/v1/me/collections/{id}` | Bearer | 204. 403 if not owner. |
| PUT | `/api/v1/me/collections/{id}/items/{mediaType}/{tmdbId}` | Bearer | 200 detail, idempotent. 403 if not owner. |
| DELETE | `/api/v1/me/collections/{id}/items/{mediaType}/{tmdbId}` | Bearer | 204, idempotent. 403 if not owner. |

### Search history
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| POST | `/api/v1/me/search-history` | Bearer | Body `{ "query": "..." }`. |
| GET | `/api/v1/me/search-history?limit=10` | Bearer | Recent. |
| DELETE | `/api/v1/me/search-history/{id}` | Bearer | 204. |
| DELETE | `/api/v1/me/search-history` | Bearer | 204; clear all. |

### Preferences
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/preferences` | Bearer | Preferences. |
| PUT | `/api/v1/me/preferences` | Bearer | Update; validated. |

## Admin
| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| POST | `/api/v1/admin/media/{mediaType}/{tmdbId}/refresh` | Bearer (Admin) | 204. Re-fetches metadata. No media delete endpoint exposed. |

## DTO examples (key)
MediaSummaryResponse includes tmdbId, mediaType, title, originalTitle, overview, posterPath, backdropPath, releaseDate, voteAverage, voteCount, popularity. MediaDetailsResponse adds tagline, originalLanguage, adult, status, runtimeMinutes/season/episode counts, first/last air dates, genres, credits (cast/crew), videos (YouTube only), similar (up to 12), seasons. MediaStateResponse includes membership/progress/lastEpisode/nextEpisode. ReviewResponse never exposes author user IDs. CollectionDetailResponse never exposes owner user IDs. EpisodeResponse includes progress/completed when authenticated context applies.

## Rate limiting & headers
Global + auth limiter; 429 returns ProblemDetails and `Retry-After`. Response compression (Brotli/Gzip), forwarded headers configurable, output caching for public TMDB responses (varies by all meaningful query params). Authenticated/user-specific responses are not output-cached.

## Non-goals / limitations
No email/password reset, OAuth, avatars, account deletion/export, notifications, payments, social features, offline downloads, admin UI, push notifications. Frontend not modified. Claimed Postgres production support requires separate validated Postgres migrations.
