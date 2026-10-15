# MovieFlix Backend — Build Progress

> Living log of the backend build-out. **Never use README.md for this** — README is the
> public overview; this is the working diary (what we did, where we stopped, what's left).

**Branch:** `dev` (never sync/push, never touch `main`)
**Started:** 2026-10-15

---

## Decisions locked (from planning)

| Decision | Choice |
|---|---|
| Identity | Migrate to **full ASP.NET Core Identity** (IdentityDbContext) |
| User key type | **Default string (GUID)** keys |
| Movie storage | **Keep local `Movies` cache table** — user→movie links reference `Movie.Id` |
| Scope | **Full feature set** (all 10 features + cross-cutting + ops) |
| Stop-before-Swift target | End of Phase 3 = usable backend for SwiftUI v2 |

---

## Status legend

⬜ Not started · 🟦 In progress · ✅ Done · ⏸️ Paused/blocked

---

## Phase 0 — Secret hygiene ✅

- [x] Add `UserSecretsId` to `MovieFlix.Api.csproj`
- [x] Move `Jwt:Key` + `Tmdb:AccessToken` out of `appsettings.json` → User Secrets
- [x] Update README run instructions
- [x] Secrets never committed (verified git history)
- [x] `appsettings.json` no longer git-ignored

## Phase 1 — Identity migration ✅

- [x] 1a. Install `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- [x] 1b. Create `ApplicationUser : IdentityUser`
- [x] 1c. `MovieFlixDbContext : IdentityDbContext<ApplicationUser>` (keep Movie/Favorite config, `base.OnModelCreating`, `Favorite.UserId` → string)
- [x] 1d. Rewrite `AuthService` on `UserManager<ApplicationUser>`
- [x] 1e. Update `JwtService` + `CurrentUserService` for string user IDs
- [x] 1f. Role + admin seeding (DatabaseSeeder)
- [x] 1g. Reset DB + regenerate migrations (Identity + Movies + Favorites + RefreshTokens)
- [x] 1h. Update tests to use UserManager + ApplicationUser
- [x] Refresh tokens (model + `/auth/refresh` + `/auth/logout` + rotation + revocation)

## Phase 2 — Core user features ✅

- [x] 2a. Watchlist — `/api/v1/me/watchlist` (add, list, remove; TMDB-id add)
- [x] 2b. Movie Ratings (+ community average) — `/api/v1/me/ratings`, `/api/v1/movies/{id}/ratings`
- [x] 2c. Watch History (continue watching / recently watched) — `/api/v1/me/history`
- [x] 2d. Reviews (CRUD + ownership + pagination) — `/api/v1/movies/{id}/reviews`, `/api/v1/reviews/{id}`
- [x] 2e. Collections (many-to-many) — `/api/v1/me/collections`
- [x] 2f. Search History — `/api/v1/me/search-history`
- [x] 2g. User Preferences — `/api/v1/me/preferences`

## Phase 3 — TMDB resilience & caching ⬜

- [ ] 3a. Polly (retry + timeout + circuit breaker)
- [ ] 3b. Response caching (memory/distributed)
- [ ] 3c. Extract shared `IMovieCacheService` from favorite upsert pattern
- [ ] 3d. *(optional)* Background cache warm-up

## Phase 4 — Professional API polish ⬜

- [ ] CORS
- [ ] Rate limiting
- [ ] Serilog structured logging
- [ ] Health checks (`/health`)
- [ ] API versioning formalized
- [ ] Validation polish (FluentValidation optional)
- [ ] Renames: `MovieQueyRequest` → `MovieQueryRequest`, `AuthController.login` → `Login`

## Phase 5 — Production ops ⬜

- [ ] Dockerfile + docker-compose
- [ ] Postgres provider option
- [ ] GitHub Actions CI (build + test)
- [ ] Secrets-in-prod docs (env vars / Key Vault)
- [ ] Backup / observability notes

---

## Session log

### 2026-10-15 — Session start
- Read full backend + SwiftUI v1. Audited against 9-level plan.
- Locked decisions (see table above).
- Kicked off Phase 0 (secret hygiene) → Phase 1 (Identity).
- Created this progress doc.

### 2026-10-16 — Phase 2 complete
- **2a Watchlist**: `WatchlistItem` (unique UserId+MovieId), `WatchlistService`
  (+ TMDB-id add reusing the cache-upsert pattern), `WatchlistController`.
- **2b Ratings**: `MovieRating` (1–5, unique per user+movie), upsert + community
  average via DB aggregate, `RatingsController` (public community endpoint).
- **2c Watch History**: `WatchHistoryEntry` (progress + completed), Continue
  Watching + Recently Watched (paged), clear/remove, `HistoryController`.
- **2d Reviews**: `Review` (1 per user+movie), CRUD with ownership
  (`ForbiddenAccessException` → 403), paginated public listing,
  `ReviewAlreadyExistsException` → 409, `ReviewsController`.
- **2e Collections**: `Collection` + `CollectionMovie` (many-to-many), ownership
  checks, add/remove movies, `DuplicateResourceException` → 409,
  `CollectionsController`.
- **2f Search History**: `SearchHistoryEntry` (case-insensitive dedupe via
  `NormalizedQuery`), recent/remove/clear, `SearchHistoryController`.
- **2g User Preferences**: `UserPreferences` (1:1), get-or-create defaults,
  validated update (language/region/theme), `PreferencesController`.
- New shared exceptions + `GlobalExceptionHandler` branches (403/409).
- Migrations: `AddWatchlist`, `AddMovieRatings`, `AddWatchHistory`, `AddReviews`,
  `AddCollections`, `AddSearchHistory`, `AddUserPreferences`.
- Tests: **47 passing** (was 17 at end of Phase 1).
