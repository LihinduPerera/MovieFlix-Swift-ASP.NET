param(
  [string]$BaseUrl = "http://localhost:5000",
  [string]$Email = "smoketest@example.com",
  [string]$Password = "SmokeTest!123",
  [string]$DisplayName = "Smoke Tester"
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
  param(
    [string]$Method,
    [string]$Path,
    [hashtable]$Headers = @{},
    [object]$Body = $null
  )
  $uri = "$BaseUrl$Path"
  $params = @{
    Method = $Method
    Uri = $uri
    Headers = $Headers
    ContentType = "application/json"
  }
  if ($Body -ne $null) {
    $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
  }
  try {
    $resp = Invoke-RestMethod @params
    return @{ ok = $true; data = $resp; status = 200 }
  } catch {
    $status = $_.Exception.Response.StatusCode.value__
    $body = $null
    try {
      $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
      $body = $reader.ReadToEnd()
    } catch {}
    return @{ ok = $false; status = $status; body = $body }
  }
}

Write-Host "MovieFlix smoke test against $BaseUrl"

# Register or login
$reg = Invoke-Api -Method POST -Path "/api/v1/auth/register" -Body @{ email = $Email; password = $Password; displayName = $DisplayName }
$tokens = $null
if ($reg.ok) {
  $tokens = $reg.data
  Write-Host "PASS: register"
} else {
  $login = Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{ email = $Email; password = $Password }
  if ($login.ok) {
    $tokens = $login.data
    Write-Host "PASS: login (after register failed)"
  } else {
    Write-Host "FAIL: auth"
    exit 1
  }
}

$auth = @{ Authorization = "Bearer $($tokens.accessToken)" }

# Search multi
$s = Invoke-Api -Method GET -Path "/api/v1/search?query=inception&type=multi&page=1&language=en-US&region=US"
if ($s.ok) { Write-Host "PASS: search multi" } else { Write-Host "FAIL: search multi ($($s.status))"; exit 1 }

# Movie details (Inception tmdb 27205 commonly)
$m = Invoke-Api -Method GET -Path "/api/v1/media/movie/27205?language=en-US"
if ($m.ok) { Write-Host "PASS: movie details" } else { Write-Host "FAIL: movie details ($($m.status))"; exit 1 }

# TV details (Game of Thrones 1399)
$t = Invoke-Api -Method GET -Path "/api/v1/media/tv/1399?language=en-US"
if ($t.ok) { Write-Host "PASS: tv details" } else { Write-Host "FAIL: tv details ($($t.status))"; exit 1 }

# Favorite movie
$fav = Invoke-Api -Method PUT -Path "/api/v1/me/favorites/movie/27205" -Headers $auth
if ($fav.ok) { Write-Host "PASS: favorite" } else { Write-Host "FAIL: favorite ($($fav.status))"; exit 1 }

# State
$st = Invoke-Api -Method GET -Path "/api/v1/media/movie/27205/state" -Headers $auth
if ($st.ok) { Write-Host "PASS: state" } else { Write-Host "FAIL: state ($($st.status))"; exit 1 }

# TV episode progress
$ep = Invoke-Api -Method PUT -Path "/api/v1/me/history/tv/1399/episodes/1/1" -Headers $auth -Body @{ progressSeconds = 60; completed = $false }
if ($ep.ok) { Write-Host "PASS: episode progress" } else { Write-Host "FAIL: episode progress ($($ep.status))"; exit 1 }

# Continue watching
$cw = Invoke-Api -Method GET -Path "/api/v1/me/history/continue?limit=10" -Headers $auth
if ($cw.ok) { Write-Host "PASS: continue watching" } else { Write-Host "FAIL: continue watching ($($cw.status))"; exit 1 }

Write-Host "All smoke steps passed"
exit 0
