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

## Phase 0 — Secret hygiene ⬜

- [ ] Add `UserSecretsId` to `MovieFlix.Api.csproj`
- [ ] Move `Jwt:Key` + `Tmdb:AccessToken` out of `appsettings.json` → User Secrets
- [ ] Add placeholders / env-var fallback in `appsettings.json`
- [ ] **User action:** rotate the leaked TMDB token on themoviedb.org
- [ ] Update README run instructions

## Phase 1 — Identity migration ⬜

- [ ] 1a. Install `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- [ ] 1b. Create `ApplicationUser : IdentityUser`
- [ ] 1c. `MovieFlixDbContext : IdentityDbContext<ApplicationUser>` (keep Movie/Favorite config, `base.OnModelCreating`, `Favorite.UserId` → string)
- [ ] 1d. Rewrite `AuthService` on `UserManager<ApplicationUser>`
- [ ] 1e. Update `JwtService` + `CurrentUserService` for string user IDs
- [ ] 1f. Role + admin seeding
- [ ] 1g. Reset DB + regenerate migrations (Identity + Movies + Favorites)
- [ ] 1h. Update the 6 tests + add register→login→favorites happy path
- [ ] Refresh tokens (model + `/auth/refresh` + rotation)

## Phase 2 — Core user features ⬜

- [ ] 2a. Watchlist
- [ ] 2b. Movie Ratings (+ community average)
- [ ] 2c. Watch History (continue watching / recently watched)
- [ ] 2d. Reviews (CRUD + ownership + pagination)
- [ ] 2e. Collections (many-to-many)
- [ ] 2f. Search History
- [ ] 2g. User Preferences

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
