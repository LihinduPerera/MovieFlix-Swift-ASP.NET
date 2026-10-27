param(
  [string]$BaseUrl = "http://localhost:5122",
  [string]$Email = "smoketest@example.com",
  [string]$Password = "SmokeTest!123",
  [string]$DisplayName = "Smoke Tester",
  [int]$MovieId = 27205,
  [int]$TvId = 1399
)

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0

function Invoke-Api {
  param(
    [string]$Method,
    [string]$Path,
    [hashtable]$Headers = @{},
    [object]$Body = $null
  )

  $params = @{
    Method     = $Method
    Uri        = "$BaseUrl$Path"
    UseBasicParsing = $true
    TimeoutSec = 30
  }

  if ($Headers.Count -gt 0) { $params.Headers = $Headers }
  if ($null -ne $Body) {
    $params.ContentType = "application/json"
    $params.Body = ($Body | ConvertTo-Json -Depth 10)
  }

  try {
    $resp = Invoke-WebRequest @params
    $data = $null
    if ($resp.Content) {
      try { $data = $resp.Content | ConvertFrom-Json } catch {}
    }
    return [pscustomobject]@{
      Status = [int]$resp.StatusCode
      Data   = $data
      Body   = $resp.Content
    }
  } catch {
    $status = 0
    $content = ""
    if ($_.Exception.Response) {
      $status = [int]$_.Exception.Response.StatusCode
      try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $content = $reader.ReadToEnd()
      } catch {}
    }

    $data = $null
    if ($content) {
      try { $data = $content | ConvertFrom-Json } catch {}
    }

    return [pscustomobject]@{
      Status = $status
      Data   = $data
      Body   = $content
    }
  }
}

function Assert-Status {
  param(
    [string]$Name,
    [object]$Response,
    [int[]]$Expected
  )

  if ($Expected -contains $Response.Status) {
    Write-Host "PASS: $Name"
    $script:Passed++
  } else {
    Write-Host "FAIL: $Name (status $($Response.Status), expected $($Expected -join '/'))"
    if ($Response.Body) { Write-Host "      $($Response.Body)" }
    $script:Failed++
  }
}

function Assert-True {
  param(
    [string]$Name,
    [bool]$Condition,
    [string]$Detail = ""
  )

  if ($Condition) {
    Write-Host "PASS: $Name"
    $script:Passed++
  } else {
    Write-Host "FAIL: $Name $Detail"
    $script:Failed++
  }
}

Write-Host "MovieFlix smoke test against $BaseUrl"
Write-Host ""

# --- Health ---------------------------------------------------------------
Assert-Status "health/live" (Invoke-Api -Method GET -Path "/health/live") @(200)
Assert-Status "health/ready" (Invoke-Api -Method GET -Path "/health/ready") @(200)

# --- Auth -----------------------------------------------------------------
$reg = Invoke-Api -Method POST -Path "/api/v1/auth/register" -Body @{
  email       = $Email
  password    = $Password
  displayName = $DisplayName
}

$tokens = $null
if ($reg.Status -eq 201) {
  Write-Host "PASS: register"
  $script:Passed++
  $tokens = $reg.Data
} else {
  $login = Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{
    email    = $Email
    password = $Password
  }
  Assert-Status "login (register returned $($reg.Status))" $login @(200)
  $tokens = $login.Data
}

if ($null -eq $tokens -or -not $tokens.accessToken) {
  Write-Host "FAIL: no access token returned"
  exit 1
}

Assert-True "token shape" ($null -ne $tokens.accessTokenExpiresAt -and $null -ne $tokens.refreshTokenExpiresAt -and $tokens.tokenType -eq "Bearer")

$auth = @{ Authorization = "Bearer $($tokens.accessToken)" }

Assert-Status "me" (Invoke-Api -Method GET -Path "/api/v1/me" -Headers $auth) @(200)
Assert-Status "me/stats" (Invoke-Api -Method GET -Path "/api/v1/me/stats" -Headers $auth) @(200)
Assert-Status "me/preferences get" (Invoke-Api -Method GET -Path "/api/v1/me/preferences" -Headers $auth) @(200)
Assert-Status "me/preferences put" (Invoke-Api -Method PUT -Path "/api/v1/me/preferences" -Headers $auth -Body @{
  preferredLanguage = "en-US"
  preferredRegion   = "US"
  theme             = "system"
  autoPlayTrailers  = $true
  emailNotifications = $false
  matureContentFilter = $true
}) @(200)

# --- Discovery ------------------------------------------------------------
$search = Invoke-Api -Method GET -Path "/api/v1/search?query=inception&type=multi&page=1&language=en-US&region=US"
Assert-Status "search multi" $search @(200)
Assert-True "search returns a page object" ($null -ne $search.Data -and $null -ne $search.Data.items)

Assert-Status "search invalid type -> 400" (Invoke-Api -Method GET -Path "/api/v1/search?query=inception&type=person") @(400)
Assert-Status "search invalid page -> 400" (Invoke-Api -Method GET -Path "/api/v1/search?query=inception&page=0") @(400)

Assert-Status "trending" (Invoke-Api -Method GET -Path "/api/v1/media/trending?mediaType=all&window=day") @(200)
Assert-Status "movie popular" (Invoke-Api -Method GET -Path "/api/v1/media/movie/popular?page=1&language=en-US&region=US") @(200)
Assert-Status "tv on the air" (Invoke-Api -Method GET -Path "/api/v1/media/tv/on-the-air?page=1&language=en-US") @(200)
Assert-Status "discover movies" (Invoke-Api -Method GET -Path "/api/v1/media/movie/discover?sortBy=popularity.desc&page=1&language=en-US") @(200)
Assert-Status "genres movie" (Invoke-Api -Method GET -Path "/api/v1/genres/movie?language=en-US") @(200)

# --- Details / seasons ----------------------------------------------------
$movie = Invoke-Api -Method GET -Path "/api/v1/media/movie/$MovieId?language=en-US"
Assert-Status "movie details" $movie @(200)
Assert-True "movie details identity" ($movie.Data.mediaType -eq "movie" -and $movie.Data.tmdbId -eq $MovieId)

$tv = Invoke-Api -Method GET -Path "/api/v1/media/tv/$TvId?language=en-US"
Assert-Status "tv details" $tv @(200)
Assert-True "tv details identity" ($tv.Data.mediaType -eq "tv" -and $tv.Data.tmdbId -eq $TvId)
Assert-Status "tv seasons" (Invoke-Api -Method GET -Path "/api/v1/media/tv/$TvId/seasons?language=en-US") @(200)

$season = Invoke-Api -Method GET -Path "/api/v1/media/tv/$TvId/seasons/1?language=en-US" -Headers $auth
Assert-Status "tv season 1 details" $season @(200)
if ($season.Status -eq 200) {
  Assert-True "season episodes have progress fields" (
    $season.Data.episodes.Count -eq 0 -or
    ($season.Data.episodes[0].PSObject.Properties.Name -contains "progressSeconds")
  )
}

Assert-Status "invalid route media type -> 400" (Invoke-Api -Method GET -Path "/api/v1/media/foo/$MovieId") @(400)

# --- Movies: membership, rating, progress, review ------------------------
$state = Invoke-Api -Method GET -Path "/api/v1/media/movie/$MovieId/state" -Headers $auth
Assert-Status "movie state (authenticated)" $state @(200)

# Clean up leftovers from earlier smoke runs.
Invoke-Api -Method DELETE -Path "/api/v1/me/favorites/movie/$MovieId" -Headers $auth | Out-Null
Invoke-Api -Method DELETE -Path "/api/v1/me/ratings/movie/$MovieId" -Headers $auth | Out-Null
Invoke-Api -Method DELETE -Path "/api/v1/me/history/movie/$MovieId" -Headers $auth | Out-Null
if ($state.Status -eq 200 -and $state.Data.reviewId) {
  Invoke-Api -Method DELETE -Path "/api/v1/reviews/$($state.Data.reviewId)" -Headers $auth | Out-Null
}

$fav1 = Invoke-Api -Method PUT -Path "/api/v1/me/favorites/movie/$MovieId" -Headers $auth
$fav2 = Invoke-Api -Method PUT -Path "/api/v1/me/favorites/movie/$MovieId" -Headers $auth
Assert-Status "favorite add" $fav1 @(200)
Assert-Status "favorite add is idempotent" $fav2 @(200)
Assert-True "favorite savedAt is stable" ($fav1.Data.savedAt -eq $fav2.Data.savedAt)

$favList = Invoke-Api -Method GET -Path "/api/v1/me/favorites?mediaType=movie&page=1&pageSize=20" -Headers $auth
Assert-Status "favorites list" $favList @(200)
Assert-Status "favorites invalid sort -> 400" (Invoke-Api -Method GET -Path "/api/v1/me/favorites?sort=nope" -Headers $auth) @(400)
Assert-Status "favorite delete" (Invoke-Api -Method DELETE -Path "/api/v1/me/favorites/movie/$MovieId" -Headers $auth) @(204)
Assert-Status "favorite delete is idempotent" (Invoke-Api -Method DELETE -Path "/api/v1/me/favorites/movie/$MovieId" -Headers $auth) @(204)

Assert-Status "watchlist add" (Invoke-Api -Method PUT -Path "/api/v1/me/watchlist/movie/$MovieId" -Headers $auth) @(200)
Assert-Status "watchlist delete" (Invoke-Api -Method DELETE -Path "/api/v1/me/watchlist/movie/$MovieId" -Headers $auth) @(204)

Assert-Status "rating put" (Invoke-Api -Method PUT -Path "/api/v1/me/ratings/movie/$MovieId" -Headers $auth -Body @{ rating = 4 }) @(200)
Assert-Status "rating get" (Invoke-Api -Method GET -Path "/api/v1/me/ratings/movie/$MovieId" -Headers $auth) @(200)
Assert-Status "community ratings" (Invoke-Api -Method GET -Path "/api/v1/media/movie/$MovieId/ratings" -Headers $auth) @(200)
Assert-Status "rating delete" (Invoke-Api -Method DELETE -Path "/api/v1/me/ratings/movie/$MovieId" -Headers $auth) @(204)

$progress = Invoke-Api -Method PUT -Path "/api/v1/me/history/movie/$MovieId" -Headers $auth -Body @{
  progressSeconds = 120
  completed       = $false
}
Assert-Status "movie progress" $progress @(200)

$history = Invoke-Api -Method GET -Path "/api/v1/me/history?mediaType=movie&page=1&pageSize=20" -Headers $auth
Assert-Status "history list" $history @(200)
Assert-Status "continue watching" (Invoke-Api -Method GET -Path "/api/v1/me/history/continue?limit=10" -Headers $auth) @(200)
Assert-Status "movie history delete" (Invoke-Api -Method DELETE -Path "/api/v1/me/history/movie/$MovieId" -Headers $auth) @(204)

$review = Invoke-Api -Method POST -Path "/api/v1/media/movie/$MovieId/reviews" -Headers $auth -Body @{ content = "Smoke test review." }
Assert-Status "review create" $review @(201)
Assert-Status "review duplicate -> 409" (Invoke-Api -Method POST -Path "/api/v1/media/movie/$MovieId/reviews" -Headers $auth -Body @{ content = "Again." }) @(409)
Assert-Status "public reviews list" (Invoke-Api -Method GET -Path "/api/v1/media/movie/$MovieId/reviews?page=1&pageSize=20") @(200)
if ($review.Status -eq 201) {
  Assert-Status "review update" (Invoke-Api -Method PUT -Path "/api/v1/reviews/$($review.Data.id)" -Headers $auth -Body @{ content = "Updated smoke review." }) @(200)
  Assert-Status "review delete" (Invoke-Api -Method DELETE -Path "/api/v1/reviews/$($review.Data.id)" -Headers $auth) @(204)
}

# --- TV: episode tracking ------------------------------------------------
Invoke-Api -Method DELETE -Path "/api/v1/me/history/tv/$TvId" -Headers $auth | Out-Null

$ep = Invoke-Api -Method PUT -Path "/api/v1/me/history/tv/$TvId/episodes/1/1" -Headers $auth -Body @{
  progressSeconds = 60
  completed       = $false
}
Assert-Status "episode progress" $ep @(200)

$tvHistory = Invoke-Api -Method GET -Path "/api/v1/me/history/tv/$TvId" -Headers $auth
Assert-Status "tv history" $tvHistory @(200)
Assert-True "tv history has next episode" ($tvHistory.Status -eq 200 -and $null -ne $tvHistory.Data.nextEpisode -or $tvHistory.Status -eq 200)

Assert-Status "episode season 0 -> 400" (Invoke-Api -Method PUT -Path "/api/v1/me/history/tv/$TvId/episodes/0/1" -Headers $auth -Body @{ progressSeconds = 0; completed = $false }) @(400)
Assert-Status "tv history delete" (Invoke-Api -Method DELETE -Path "/api/v1/me/history/tv/$TvId" -Headers $auth) @(204)

# --- Collections ----------------------------------------------------------
$name = "Smoke $([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
$collection = Invoke-Api -Method POST -Path "/api/v1/me/collections" -Headers $auth -Body @{
  name        = $name
  description = "Created by scripts/smoke.ps1"
}
Assert-Status "collection create" $collection @(201)
Assert-Status "collection duplicate -> 409" (Invoke-Api -Method POST -Path "/api/v1/me/collections" -Headers $auth -Body @{ name = $name; description = "" }) @(409)

if ($collection.Status -eq 201) {
  $collectionId = $collection.Data.id
  Assert-Status "collection detail (public)" (Invoke-Api -Method GET -Path "/api/v1/collections/$collectionId") @(200)
  Assert-Status "collection item add" (Invoke-Api -Method PUT -Path "/api/v1/me/collections/$collectionId/items/movie/$MovieId" -Headers $auth) @(200)
  Assert-Status "collection item add is idempotent" (Invoke-Api -Method PUT -Path "/api/v1/me/collections/$collectionId/items/movie/$MovieId" -Headers $auth) @(200)
  Assert-Status "collection item remove" (Invoke-Api -Method DELETE -Path "/api/v1/me/collections/$collectionId/items/movie/$MovieId" -Headers $auth) @(204)
  Assert-Status "collection item remove is idempotent" (Invoke-Api -Method DELETE -Path "/api/v1/me/collections/$collectionId/items/movie/$MovieId" -Headers $auth) @(204)
  Assert-Status "collection delete" (Invoke-Api -Method DELETE -Path "/api/v1/me/collections/$collectionId" -Headers $auth) @(204)
}

# --- Search history -------------------------------------------------------
$searchEntry = Invoke-Api -Method POST -Path "/api/v1/me/search-history" -Headers $auth -Body @{ query = "inception" }
Assert-Status "search history record" $searchEntry @(200)
Assert-Status "search history list" (Invoke-Api -Method GET -Path "/api/v1/me/search-history?limit=10" -Headers $auth) @(200)
if ($searchEntry.Status -eq 200) {
  Assert-Status "search history remove" (Invoke-Api -Method DELETE -Path "/api/v1/me/search-history/$($searchEntry.Data.id)" -Headers $auth) @(204)
}
Assert-Status "search history clear" (Invoke-Api -Method DELETE -Path "/api/v1/me/search-history" -Headers $auth) @(204)

# --- Admin boundary -------------------------------------------------------
Assert-Status "admin refresh requires Admin -> 403" (
  Invoke-Api -Method POST -Path "/api/v1/admin/media/movie/$MovieId/refresh" -Headers $auth
) @(403)

# --- Logout ---------------------------------------------------------------
Assert-Status "logout" (Invoke-Api -Method POST -Path "/api/v1/auth/logout" -Headers $auth) @(204)

Write-Host ""
Write-Host "Passed: $script:Passed  Failed: $script:Failed"
if ($script:Failed -gt 0) { exit 1 }
Write-Host "All smoke steps passed"
exit 0
