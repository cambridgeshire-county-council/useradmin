param(
  [string]$Owner = "cambridgeshire-county-council",
  [string]$Repo = "useradmin",
  [string]$Branch = "main",
  [string]$Token = $env:GITHUB_TOKEN,
  [string]$AppPool = "ScriptRunner",
  [string]$SitePath = "C:\inetpub\wwwroot\PSScriptWebApp",
  [string]$WorkDir = "C:\temp\psscriptwebapp"
)

$ErrorActionPreference = "Stop"

Write-Host "=== PSScriptWebApp Deploy ===" -ForegroundColor Cyan
Write-Host "Repo:     $Owner/$Repo ($Branch)"
Write-Host "App pool: $AppPool"
Write-Host "Site:     $SitePath"
Write-Host "Work dir: $WorkDir"
Write-Host ""

Write-Host "[1/8] Creating work directory: $WorkDir"
New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null

$headers = @{ Authorization = "Bearer $Token"; "X-GitHub-Api-Version" = "2022-11-28" }

Write-Host "[2/8] Fetching latest successful workflow run on '$Branch'..."
$runsUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs?branch=$Branch&status=success&per_page=1"
$run = (Invoke-RestMethod -Uri $runsUrl -Headers $headers).workflow_runs[0]
if (-not $run) { throw "No successful workflow run found." }
Write-Host "      Run #$($run.run_number) — $($run.display_title) ($($run.created_at))"

Write-Host "[3/8] Locating artifact 'webapp-zip' for run $($run.id)..."
$artUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs/$($run.id)/artifacts"
$artifact = (Invoke-RestMethod -Uri $artUrl -Headers $headers).artifacts |
  Where-Object { $_.name -eq "webapp-zip" -and -not $_.expired } |
  Select-Object -First 1
if (-not $artifact) { throw "Artifact webapp-zip not found." }
Write-Host "      Found artifact id $($artifact.id) ($([math]::Round($artifact.size_in_bytes / 1MB, 1)) MB)"

$artifactZip = Join-Path $WorkDir "artifact.zip"
Write-Host "[4/8] Downloading artifact to: $artifactZip"
Invoke-RestMethod -Uri $artifact.archive_download_url -Headers $headers -OutFile $artifactZip
Write-Host "      Download complete."

$extractDir = Join-Path $WorkDir "artifact"
Write-Host "[5/8] Extracting artifact zip to: $extractDir"
if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
Expand-Archive -Path $artifactZip -DestinationPath $extractDir -Force

$appZip = Get-ChildItem $extractDir -Filter "*.zip" | Select-Object -First 1
$shaFile = Get-ChildItem $extractDir -Filter "*.sha256" | Select-Object -First 1
if (-not $appZip -or -not $shaFile) { throw "App zip or sha256 not found inside artifact." }
Write-Host "      Found app zip: $($appZip.Name)"

Write-Host "[6/8] Verifying SHA256 checksum of $($appZip.Name)..."
$expected = (Get-Content $shaFile.FullName).Split(" ")[0].Trim()
$actual = (Get-FileHash $appZip.FullName -Algorithm SHA256).Hash
if ($expected -ne $actual) { throw "Checksum mismatch. Expected: $expected  Actual: $actual" }
Write-Host "      Checksum OK."

$backup = Join-Path $WorkDir ("backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
if (Test-Path $SitePath) {
  Write-Host "[7/8] Backing up current site from $SitePath to $backup..."
  New-Item -ItemType Directory -Path $backup -Force | Out-Null
  Copy-Item "$SitePath\*" $backup -Recurse -Force
  Write-Host "      Backup complete."
} else {
  Write-Host "[7/8] No existing site at $SitePath — skipping backup."
}

Write-Host "[8/8] Deploying..."
Write-Host "      Stopping app pool '$AppPool'..."
Import-Module WebAdministration
Stop-WebAppPool -Name $AppPool

try {
  Write-Host "      Clearing site directory: $SitePath"
  if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }

  Write-Host "      Extracting $($appZip.Name) to $SitePath..."
  Expand-Archive -Path $appZip.FullName -DestinationPath $SitePath -Force

  Write-Host "      Starting app pool '$AppPool'..."
  Start-WebAppPool -Name $AppPool

  Write-Host ""
  Write-Host "=== Deploy complete ===" -ForegroundColor Green
}
catch {
  Write-Warning "Deploy failed. Attempting rollback from $backup..."
  if (Test-Path $backup) {
    if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }
    Write-Host "      Copying $backup to $SitePath..."
    Copy-Item "$backup\*" $SitePath -Recurse -Force
    Write-Host "      Rollback complete."
  } else {
    Write-Warning "No backup found — rollback skipped."
  }
  Start-WebAppPool -Name $AppPool
  throw
}
