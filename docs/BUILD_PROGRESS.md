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
