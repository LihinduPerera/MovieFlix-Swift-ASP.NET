# MovieFlix API Contract (v1)

Client-facing contract for the MovieFlix ASP.NET Core API. Base path: `/api/v1`.
All request and response bodies are `application/json`; errors are
`application/problem+json` (RFC 7807).

| Item | Value |
| --- | --- |
| Framework | ASP.NET Core on **.NET 9** (`net9.0`); API and tests |
| API version | `1.0`, URL-prefixed (`/api/v1`) and reported in `api-supported-versions` |
| Database | SQLite by default; PostgreSQL selectable (`Database:Provider`) |
| Migration set | One clean `InitialMediaSchema` (SQLite-generated) |
| Auth | ASP.NET Core Identity + JWT bearer; refresh-token rotation |
| Local URLs | `http://localhost:5122`, `https://localhost:7014` (launch profile) |
| Swagger (Development only) | `/swagger` |
| Health | `/health`, `/health/live`, `/health/ready` |

> PostgreSQL note: the committed migrations are SQLite-generated. A separate,
> validated PostgreSQL migration set is required before claiming production
> PostgreSQL support. The provider switch exists, but is not field-proven.

---

## 1. Conventions

### Media identity

A public title is identified by **`(mediaType, tmdbId)`**:

- `mediaType`: `"movie" | "tv"` (lowercase on the wire and in the database).
- `tmdbId`: positive integer TMDB id.

Local database ids for media are never exposed. Numeric ids appear only for
user-owned resources (reviews, collections, search-history entries) and the
authenticated user (`id` in `/me` and auth responses).

### Enums

Enums are serialized as camelCase strings.

| Enum | Accepted values | Default |
| --- | --- | --- |
| route `mediaType` | `movie`, `tv` | required |
| `type` (search) | `multi`, `movie`, `tv` | `multi` |
| `mediaType` (trending filter) | `all`, `movie`, `tv` | `all` |
| `window` (trending) | `day`, `week` | `week` |
| `mediaType` (library/history filter) | `movie`, `tv` | no filter |
| `sort` (favorites/watchlist) | `added_desc`, `added_asc`, `title_asc`, `title_desc`, `release_desc` | `added_desc` |
| `theme` (preferences) | `system`, `light`, `dark` | `system` |

Invalid enum/model values (including an invalid route `mediaType`) return
**400** with an `errors` dictionary. Unknown routes such as a non-integer
`tmdbId` do not match and return **404**.

### Language and region

Every TMDB-backed public endpoint accepts explicit:

- `language`: BCP-47-shaped tag, regex `^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$`.
- `region`: two-letter ISO 3166-1 code, regex `^[A-Za-z]{2}$`, uppercased.

Omitted or blank values fall back to configuration (`Tmdb:DefaultLanguage` =
`en-US`, `Tmdb:DefaultRegion` = `US`). Invalid values return 400.

### Dates and timestamps

- Dates: `yyyy-MM-dd` (JSON `DateOnly`).
- Timestamps: UTC ISO 8601 values.
- `progressSeconds` is a non-negative integer.

### Pagination

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

- TMDB discovery endpoints use TMDB page semantics: `page` is 1–500, the
  upstream `totalCount`/`totalPages` are passed through, and `pageSize` is
  always `20`. Some pages can contain fewer than 20 items after filtering
  (search drops people; trending `all` drops non-movie/TV entries).
- User list endpoints use database pagination: `page >= 1`,
  `pageSize` 1–100 (default 20).

### Validation

Request DTOs use DataAnnotations plus explicit range/consistency checks.
Validation failures return 400 Problem Details with an `errors` dictionary.
Keys follow the bound property name (for example `Query`, `Page`, `Rating`)
or the explicit field name for custom checks (for example `sort`,
`seasonNumber`, `mediaType`).

### Problem Details

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Bad Request",
  "errors": {
    "Page": ["The field Page must be between 1 and 500."]
  },
  "traceId": "0HN..."
}
```

Status codes used by the contract:

| Status | Meaning |
| --- | --- |
| 200 | Success |
| 201 | Resource created (register, review, collection) |
| 204 | Success with no body (delete, logout, admin refresh) |
| 400 | Validation failure or invalid enum/query value |
| 401 | Missing/invalid/expired bearer token, bad credentials, invalid refresh token |
| 403 | Authenticated but not allowed (role or ownership) |
| 404 | Route/resource not found, or a TMDB title/season not found |
| 409 | Duplicate review, duplicate collection name, duplicate account |
| 423 | Account locked after repeated failed logins |
| 429 | Rate limit exceeded; `Retry-After` header is present |
| 502 | TMDB returned an unsuccessful response |
| 503 | TMDB unavailable/timeout/rate-limited (`Retry-After` when TMDB supplied one) |
| 500 | Unexpected server error (no internals exposed) |

### Upstream TMDB failures

Every TMDB-backed endpoint can return:

- **404** when TMDB has no title/season for the id.
- **502** for other non-success TMDB responses.
- **503** for network failures, timeouts, open circuit breaker, or TMDB 429.
  A `Retry-After` header is included for TMDB 429 responses.

TMDB calls use a standard resilience pipeline (3 retries with exponential
backoff and jitter, 5 s attempt timeout, 20 s total timeout, circuit breaker).

---

## 2. Authentication

- Scheme: `Authorization: Bearer <accessToken>`.
- Access tokens are JWTs (default lifetime **60 minutes**).
- Refresh tokens are single-use, rotated on every refresh, and expire after
  **7 days** by default. Reusing an already-rotated token revokes the refresh
  chain.
- Anonymous access is allowed on public discovery/details routes; when a valid
  bearer token is present, caller-specific fields are populated.
- Protected routes return **401** Problem Details when the token is missing or
  invalid, and **403** when the caller lacks the required role/ownership.

### Token response

`LoginResponse` (register, login, refresh):

```json
{
  "accessToken": "<jwt>",
  "accessTokenExpiresAt": "2026-10-26T12:00:00Z",
  "refreshToken": "<opaque-token>",
  "refreshTokenExpiresAt": "2026-11-02T11:00:00Z",
  "tokenType": "Bearer",
  "user": {
    "id": "<identity-user-id>",
    "email": "user@example.com",
    "displayName": "User",
    "createdAt": "2026-10-20T08:00:00Z",
    "roles": ["User"]
  }
}
```

There is no `expiresIn` field; use the absolute `...ExpiresAt` values.

### Auth endpoints

| Method | Path | Auth | Success | Notes |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/auth/register` | Public (auth limiter) | 201 `LoginResponse` | Email, display name, password. Password: 8–100 chars and must include upper, lower, digit, non-alphanumeric. New accounts get role `User`. Duplicate email → 409. |
| POST | `/api/v1/auth/login` | Public (auth limiter) | 200 `LoginResponse` | Invalid credentials → 401 (generic message). Locked account → 423. Failed attempts increment the lockout counter; success resets it. |
| POST | `/api/v1/auth/refresh` | Public (auth limiter) | 200 `LoginResponse` | Rotates the refresh token. Invalid/reused token → 401; reuse revokes the chain. |
| POST | `/api/v1/auth/logout` | Bearer (auth limiter) | 204 | Revokes all active refresh tokens for the caller. |

Lockout: 5 failed attempts, 5-minute lockout. Roles seeded at startup:
`Admin`, `User`. An initial admin can be created from `Seed:AdminEmail` /
`Seed:AdminPassword`; those settings are absent by default.

---

## 3. Search and discovery (public)

All routes below are anonymous-friendly and output-cached as noted.

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/search` | 200 `PagedResponse<MediaSummaryResponse>` | Query params: `query` (required, 1–200), `type` (`multi` default), `page` (1–500), `language`, `region`. `multi` filters out person results. Cache: 5 min. |
| GET | `/api/v1/media/trending` | 200 `PagedResponse<MediaSummaryResponse>` | `mediaType` (`all` default), `window` (`week` default), `page` (1–500), `language`. Cache: 10 min. |
| GET | `/api/v1/media/{mediaType}/popular` | 200 `PagedResponse<MediaSummaryResponse>` | `page`, `language`, `region`. Cache: 10 min. |
| GET | `/api/v1/media/{mediaType}/top-rated` | 200 `PagedResponse<MediaSummaryResponse>` | Same as popular. Cache: 10 min. |
| GET | `/api/v1/media/movie/now-playing` | 200 `PagedResponse<MediaSummaryResponse>` | Movies only. `page`, `language`, `region`. Cache: 10 min. |
| GET | `/api/v1/media/movie/upcoming` | 200 `PagedResponse<MediaSummaryResponse>` | Movies only. Same params. Cache: 10 min. |
| GET | `/api/v1/media/tv/on-the-air` | 200 `PagedResponse<MediaSummaryResponse>` | TV only. Same params. Cache: 10 min. |
| GET | `/api/v1/media/tv/airing-today` | 200 `PagedResponse<MediaSummaryResponse>` | TV only. Same params. Cache: 10 min. |
| GET | `/api/v1/media/{mediaType}/discover` | 200 `PagedResponse<MediaSummaryResponse>` | `genreId` (>=1), `year` (1888–2200), `minRating` (0–10), `sortBy` (TMDB expression such as `popularity.desc`), `page` (1–500), `language`, `region`. Omitted filters use TMDB defaults. Cache: 10 min. |
| GET | `/api/v1/genres/{mediaType}` | 200 `List<GenreResponse>` | `language`. Cache: 24 h. |

Discovery uses `include_adult=false` for TMDB discover/search. Region and
language apply to titles and release-date/availability lists; the API does not
translate user content.

### Output-cache summary for public routes

| Policy | TTL | Varies by |
| --- | --- | --- |
| `tmdb-search` | 5 min | query, type, page, language, region |
| `tmdb-trending` | 10 min | mediaType, window, page, language |
| `tmdb-feed` | 10 min | path (`mediaType`/feed) + page, language, region |
| `tmdb-discover` | 10 min | genreId, year, minRating, sortBy, page, language, region |
| `tmdb-details` | 1 h | route mediaType + tmdbId, language |
| `tmdb-season` | 6 h | route tmdbId, language — **season summaries only** |
| `tmdb-genres` | 24 h | route mediaType, language |

Never output-cached: season **details** (they may include per-user progress),
`/state`, ratings, reviews, all `/api/v1/me/*`, auth, and admin routes.

---

## 4. Media details, seasons, state, ratings, reviews

| Method | Path | Auth | Success | Notes |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/media/{mediaType}/{tmdbId}` | Public | 200 `MediaDetailsResponse` | `language`. Always refreshes metadata from TMDB and upserts it locally. Cache: 1 h. Missing title → 404. |
| GET | `/api/v1/media/tv/{tmdbId}/seasons` | Public | 200 `List<SeasonSummaryResponse>` | `language`. Season summaries from cached TV metadata; **season 0 specials excluded**; ordered by season number. Cache: 6 h. |
| GET | `/api/v1/media/tv/{tmdbId}/seasons/{seasonNumber}` | Public / optional bearer | 200 `SeasonDetailsResponse` | `language`. Lazily fetches and caches the season/episodes if needed. Authenticated callers also receive per-episode `progressSeconds`/`completed`; anonymous callers get `0`/`false`. Missing show/season → 404. **Not output-cached.** |
| GET | `/api/v1/media/{mediaType}/{tmdbId}/state` | Public / optional bearer | 200 `MediaStateResponse` | Never calls TMDB and never 404s for an unknown-but-valid id. Anonymous callers get default false/0/null values. If authenticated but the title is not cached, defaults are also returned. |
| GET | `/api/v1/media/{mediaType}/{tmdbId}/ratings` | Public / optional bearer | 200 `CommunityRatingResponse` | 404 if the title has never been cached. A cached title with zero ratings returns `averageRating: 0`, `ratingCount: 0`, `userRating: null`. |
| GET | `/api/v1/media/{mediaType}/{tmdbId}/reviews` | Public / optional bearer | 200 `PagedResponse<ReviewResponse>` | `page` (>=1, default 1), `pageSize` (1–100, default 20). Newest first. 404 if the title has never been cached. Public; `isMine` is true for the caller's review when authenticated. |
| POST | `/api/v1/media/{mediaType}/{tmdbId}/reviews` | Bearer | 201 `ReviewResponse` | Body `{ "content": "..." }` (1–2000 chars, trimmed). Fetches/caches the title if needed. One review per user per title; duplicate → 409. `Location: /api/v1/reviews/{reviewId}`. |

### Details mapping limits

- Genres: all returned genres.
- Cast: top 20 by TMDB `order`.
- Crew: only key jobs (`Director`, `Writer`, `Screenplay`, `Story`, `Producer`,
  `Executive Producer`, `Director of Photography`, `Editor`,
  `Original Music Composer`), at most 20.
- Videos: YouTube only (all types/statuses returned by TMDB).
- Similar: same `mediaType` only, first 12.
- Seasons: non-special seasons only (`seasonNumber > 0`), ordered ascending.

### Reviews privacy and ownership

- Reviews are public. Author **display name** is exposed; author user ids are
  never exposed.
- Update/delete require ownership: missing review → 404, non-owner → 403.
- A title that has never been cached yields 404 for the review list; a cached
  title with no reviews yields an empty page.

---

## 5. Profile (`/api/v1/me`)

All routes require a bearer token.

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me` | 200 `MeResponse` | Caller profile. |
| PATCH | `/api/v1/me` | 200 `MeResponse` | Body `{ "displayName": "..." }` (1–100 chars, trimmed). |
| GET | `/api/v1/me/stats` | 200 `StatsResponse` | Aggregate counts. `moviesWatched` counts completed movie states; `showsWatched` counts completed show states; `episodesWatched` counts completed episode states. |

---

## 6. Library

All routes require a bearer token. Membership writes are **idempotent**:
repeating a `PUT` returns 200 and repeating a `DELETE` returns 204.

### Favorites

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/favorites` | 200 `PagedResponse<SavedMediaResponse>` | `mediaType` optional filter, `page`, `pageSize` (1–100), `sort`. Invalid `sort` → 400. |
| PUT | `/api/v1/me/favorites/{mediaType}/{tmdbId}` | 200 `SavedMediaResponse` | Fetches/caches title metadata if needed (may return 404/502/503). Duplicate add returns the existing row and keeps its original `savedAt`. |
| DELETE | `/api/v1/me/favorites/{mediaType}/{tmdbId}` | 204 | No-op if absent. |

### Watchlist

Same shapes and rules as favorites under `/api/v1/me/watchlist`.

| Method | Path | Success |
| --- | --- | --- |
| GET | `/api/v1/me/watchlist` | 200 `PagedResponse<SavedMediaResponse>` |
| PUT | `/api/v1/me/watchlist/{mediaType}/{tmdbId}` | 200 `SavedMediaResponse` |
| DELETE | `/api/v1/me/watchlist/{mediaType}/{tmdbId}` | 204 |

### Ratings

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/ratings/{mediaType}/{tmdbId}` | 200 `MediaRatingResponse` | 404 when the caller has not rated the title. |
| PUT | `/api/v1/me/ratings/{mediaType}/{tmdbId}` | 200 `MediaRatingResponse` | Body `{ "rating": 1..5 }`; upsert. Out-of-range → 400 (`Rating`). Fetches/caches title metadata if needed. |
| DELETE | `/api/v1/me/ratings/{mediaType}/{tmdbId}` | 204 | Idempotent. |

### Watch history and episode progress

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/history` | 200 `PagedResponse<WatchHistoryItemResponse>` | `mediaType` optional, `page`, `pageSize` (1–100). Newest `lastWatchedAt` first. |
| GET | `/api/v1/me/history/continue` | 200 `List<WatchHistoryItemResponse>` | `limit` clamped 1–50 (default 10). See rules below. |
| GET | `/api/v1/me/history/tv/{tmdbId}` | 200 `TvHistoryResponse` | 404 when the show has never been cached. Includes all tracked episode states (completed and in progress) plus `nextEpisode` and `showCompleted`. |
| PUT | `/api/v1/me/history/{mediaType}/{tmdbId}` | 200 `WatchHistoryItemResponse` | Body `{ progressSeconds, completed, seasonNumber?, episodeNumber? }`. For movies, episode fields must be absent. For TV, supplying both episode fields delegates to episode tracking and returns the parent history item; supplying neither updates only the aggregate and preserves the last episode. |
| PUT | `/api/v1/me/history/tv/{tmdbId}/episodes/{seasonNumber}/{episodeNumber}` | 200 `EpisodeProgressResponse` | Body `{ progressSeconds, completed }`. `seasonNumber` must be >= 1. Invalid/unknown episode → 404. |
| DELETE | `/api/v1/me/history/{mediaType}/{tmdbId}` | 204 | Removes the aggregate state; for TV also removes all episode states for that show. Idempotent. |
| DELETE | `/api/v1/me/history` | 204 | Clears all watch history and episode states for the caller. |

Progress rules:

- `progressSeconds >= 0`; `completed` is explicit (no percentage auto-complete).
- Movie + `seasonNumber`/`episodeNumber` → 400 (`mediaType`: "Episode
  tracking is only supported for TV shows.").
- TV + only one episode field → 400 (`seasonNumber`/`episodeNumber` must be
  supplied together).
- Completing an episode sets its progress to the episode runtime in seconds
  when TMDB provides a positive runtime; otherwise the supplied progress is
  kept.
- Season 0 specials cannot be tracked (400 `seasonNumber`).
- Next episode = first not-completed episode with `seasonNumber > 0` and no
  future air date, ordered by season then episode. Resolution uses cached
  episode metadata only; callers that need a not-yet-cached season must fetch
  its details (or advance an episode, which auto-caches the next season at a
  season boundary).
- `showCompleted` is true only when the last completed write leaves no next
  episode, no future episodes, and no uncached non-special seasons. The show
  aggregate `completed` flag follows this.
- Metadata refreshes never delete watch states or episode progress.

Continue Watching returns only unfinished states:

- Movies: `completed == false` and `progressSeconds > 0`.
- TV: `completed == false` and (`progressSeconds > 0` or a last episode is
  tracked).

### Owned reviews

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/reviews` | 200 `PagedResponse<ReviewResponse>` | `page`, `pageSize` (1–100). Newest updated first; every item has `isMine: true`. |
| PUT | `/api/v1/reviews/{reviewId}` | 200 `ReviewResponse` | Body `{ "content": "..." }` (1–2000). Owner only: 404 missing, 403 non-owner. |
| DELETE | `/api/v1/reviews/{reviewId}` | 204 | Owner only: 404 missing, 403 non-owner. |

### Collections

| Method | Path | Auth | Success | Notes |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/me/collections` | Bearer | 200 `PagedResponse<CollectionSummaryResponse>` | `page`, `pageSize` (1–100). Owner's collections, newest updated first. |
| POST | `/api/v1/me/collections` | Bearer | 201 `CollectionSummaryResponse` | Body `{ "name": "...", "description": "..." }` (name 1–100, description max 500; both trimmed). Duplicate name per owner, case-insensitive → 409. `Location: /api/v1/collections/{id}`. |
| GET | `/api/v1/collections/{collectionId}` | Public / optional bearer | 200 `CollectionDetailResponse` | Readable by numeric id by anyone. Missing → 404. `isOwner` is true only for the owner. Exposes `ownerDisplayName`, never the owner user id. |
| PUT | `/api/v1/me/collections/{collectionId}` | Bearer | 200 `CollectionSummaryResponse` | Same body/validation as create. Owner only: 404 missing, 403 non-owner, 409 duplicate name. |
| DELETE | `/api/v1/me/collections/{collectionId}` | Bearer | 204 | Owner only: 404 missing, 403 non-owner. |
| PUT | `/api/v1/me/collections/{collectionId}/items/{mediaType}/{tmdbId}` | Bearer | 200 `CollectionDetailResponse` | Idempotent. Fetches/caches title metadata if needed. Owner only: 404 missing, 403 non-owner. Returns the full updated detail. |
| DELETE | `/api/v1/me/collections/{collectionId}/items/{mediaType}/{tmdbId}` | Bearer | 204 | Idempotent. Owner only: 404 missing, 403 non-owner. |

Collection privacy: there is no public collection index and no `isPrivate`
field. A collection is readable by id (including anonymously); only the owner
can list, modify, delete, or change its items. `coverPosterPaths` is derived
from up to four newest items that have posters; it is never stored.

### Search history

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/search-history` | 200 `List<SearchHistoryResponse>` | `limit` clamped 1–50; values <= 0 use 10. Newest first. |
| POST | `/api/v1/me/search-history` | 200 `SearchHistoryResponse` | Body `{ "query": "..." }` (1–200, trimmed). Repeated queries are deduplicated case-insensitively; the existing row's `searchedAt` is updated. 200, not 201. |
| DELETE | `/api/v1/me/search-history/{entryId}` | 204 | Only the caller's entry; missing/not-owned → 404. |
| DELETE | `/api/v1/me/search-history` | 204 | Clears all of the caller's entries. |

### Preferences

| Method | Path | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/me/preferences` | 200 `PreferencesResponse` | Auto-creates defaults on first read. |
| PUT | `/api/v1/me/preferences` | 200 `PreferencesResponse` | Full replace. Required: `preferredLanguage` (BCP-47, e.g. `en`/`en-US`), `preferredRegion` (2 letters), `theme` (`system|light|dark`); booleans. |

---

## 7. Admin

| Method | Path | Auth | Success | Notes |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/admin/media/{mediaType}/{tmdbId}/refresh` | Bearer + `Admin` role | 204 | Forces a metadata refresh from TMDB using `Tmdb:DefaultLanguage`. Does not delete user library/history/progress rows; links remain attached to the same media item. 401 without a token, 403 without the role, 404/502/503 from TMDB. |

There is no public media delete endpoint.

---

## 8. DTO reference

### Media

`MediaSummaryResponse` (used by every list and nested reference):

```json
{
  "tmdbId": 27205,
  "mediaType": "movie",
  "title": "Inception",
  "originalTitle": "Inception",
  "overview": "...",
  "posterPath": "/poster.jpg",
  "backdropPath": "/backdrop.jpg",
  "releaseDate": "2010-07-15",
  "voteAverage": 8.4,
  "voteCount": 35000,
  "popularity": 120.5
}
```

`MediaDetailsResponse` extends the summary with:

```json
{
  "tagline": "...",
  "originalLanguage": "en",
  "adult": false,
  "status": "Released",
  "runtimeMinutes": 148,
  "numberOfSeasons": null,
  "numberOfEpisodes": null,
  "firstAirDate": null,
  "lastAirDate": null,
  "genres": [{ "id": 28, "name": "Action" }],
  "credits": {
    "cast": [
      { "id": 1, "name": "Actor", "character": "Role", "profilePath": "/p.jpg", "order": 0 }
    ],
    "crew": [
      { "id": 2, "name": "Crew", "job": "Director", "department": "Directing", "profilePath": null }
    ]
  },
  "videos": [
    {
      "id": "abc",
      "key": "YouTubeId",
      "name": "Trailer",
      "site": "YouTube",
      "type": "Trailer",
      "official": true,
      "language": "en",
      "publishedAt": "2026-01-01T00:00:00Z"
    }
  ],
  "similar": [ /* MediaSummaryResponse */ ],
  "seasons": [
    {
      "seasonNumber": 1,
      "name": "Season 1",
      "overview": "...",
      "posterPath": "/s1.jpg",
      "airDate": "2020-01-01",
      "episodeCount": 10
    }
  ]
}
```

`SeasonSummaryResponse` contains only `seasonNumber`, `name`, `overview`,
`posterPath`, `airDate`, `episodeCount`.

`SeasonDetailsResponse`:

```json
{
  "seasonNumber": 1,
  "name": "Season 1",
  "overview": "...",
  "posterPath": "/s1.jpg",
  "airDate": "2020-01-01",
  "episodes": [
    {
      "seasonNumber": 1,
      "episodeNumber": 1,
      "name": "Pilot",
      "stillPath": "/still.jpg",
      "tmdbId": 123,
      "overview": "...",
      "airDate": "2020-01-01",
      "runtimeMinutes": 52,
      "voteAverage": 8.1,
      "voteCount": 100,
      "progressSeconds": 0,
      "completed": false
    }
  ]
}
```

`EpisodeSummaryResponse` (nested in state/history): `seasonNumber`,
`episodeNumber`, `name`, `stillPath`.

### State and history

`MediaStateResponse`:

```json
{
  "tmdbId": 1399,
  "mediaType": "tv",
  "isFavorite": false,
  "isInWatchlist": true,
  "userRating": 4,
  "reviewId": 12,
  "hasReview": true,
  "progressSeconds": 600,
  "completed": false,
  "lastEpisode": { "seasonNumber": 1, "episodeNumber": 2, "name": "Episode 2", "stillPath": "/s.jpg" },
  "nextEpisode": { "seasonNumber": 1, "episodeNumber": 3, "name": "Episode 3", "stillPath": "/s.jpg" }
}
```

`SavedMediaResponse`: `{ "media": MediaSummaryResponse, "savedAt": "..." }`.

`WatchHistoryItemResponse`: `media` (summary), `progressSeconds`, `completed`,
`lastWatchedAt`, `lastEpisode` (episode summary or null), `nextEpisode`
(episode summary or null).

`EpisodeProgressResponse`: `media` (summary), `episode` (full episode),
`nextEpisode` (full episode or null), `showCompleted`.

`TvHistoryResponse`: `media` (summary),
`episodes: [{ "episode": EpisodeResponse, "watchedAt": "..." }]`,
`nextEpisode` (full episode or null), `showCompleted`.

### Ratings and reviews

`MediaRatingResponse`: `media` (summary), `rating` (1–5), `ratedAt`.

`CommunityRatingResponse`: `media` (summary), `averageRating` (rounded to 2
decimals; `0` with no ratings), `ratingCount`, `userRating` (nullable).

`ReviewResponse`: `id`, `media` (summary), `content`, `authorDisplayName`,
`isMine`, `createdAt`, `updatedAt`. Author user ids are never included.

### Collections

`CollectionSummaryResponse`:

```json
{
  "id": 1,
  "name": "Sci-Fi",
  "description": "...",
  "itemCount": 2,
  "coverPosterPaths": ["/a.jpg", "/b.jpg"],
  "createdAt": "...",
  "updatedAt": "..."
}
```

`CollectionDetailResponse` adds `ownerDisplayName`, `isOwner`, and
`items: [{ "media": MediaSummaryResponse, "addedAt": "..." }]`.

### Profile and preferences

`MeResponse` / auth `user`: `id`, `email`, `displayName`, `createdAt`,
`roles` (array).

`StatsResponse`: `favorites`, `watchlist`, `ratings`, `reviews`,
`collections`, `moviesWatched`, `showsWatched`, `episodesWatched`.

`PreferencesResponse`: `preferredLanguage`, `preferredRegion`, `theme`,
`autoPlayTrailers`, `emailNotifications`, `matureContentFilter`, `updatedAt`.

`SearchHistoryResponse`: `id`, `query`, `searchedAt`.

`GenreResponse`: `id`, `name`.

---

## 9. Cross-cutting behavior

### Rate limiting

- Global fixed-window limiter: 120 requests per 60 seconds per authenticated
  user id, otherwise per remote IP.
- `auth` policy: 10 requests per 60 seconds per remote IP, applied to every
  `/api/v1/auth/*` action.
- Rejections return **429** Problem Details with `Retry-After` (seconds).
- Limits are configurable via `RateLimiting:GlobalPermitLimit`,
  `GlobalWindowSeconds`, `AuthPermitLimit`, `AuthWindowSeconds`.

### Response compression and CORS

- Brotli/Gzip compression is enabled for JSON and Problem Details, including
  over HTTPS.
- CORS uses the `MovieFlixClient` policy; origins come from
  `Cors:AllowedOrigins` (empty by default, so no browser origin is allowed).

### Forwarded headers

Disabled by default. When `ForwardedHeaders:Enabled` is true, the API honors
`X-Forwarded-For`/`X-Forwarded-Proto` only for configured known proxies and
networks. Enable it behind a trusted reverse proxy so rate limiting sees the
real client IP.

### Health checks

| Path | Meaning |
| --- | --- |
| `/health/live` | Liveness; no dependency checks |
| `/health/ready` | Readiness; includes the EF Core database check |
| `/health` | Alias for readiness (all registered checks) |

Healthy responses are 200; unhealthy responses are 503.

### Versioning

Responses include `api-supported-versions: 1.0`. The default version is 1.0
when the URL omits a version, but clients should always use `/api/v1`.

---

## 10. Non-goals / limitations

Not implemented: email/password reset, OAuth/social login, avatars/upload,
account deletion/export, notifications, payments, social features, offline
downloads, admin UI, and push notifications. The SwiftUI frontend is not part
of this API contract. PostgreSQL production support is not claimed until a
Postgres-specific migration set is generated and validated.
