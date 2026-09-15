param(
  [string]$Owner = "cambridgeshire-county-council",
  [string]$Repo = "useradmin",
  [string]$Branch = "main",
  [string]$Token = $env:GITHUB_TOKEN,
  [string]$RunId,
  [string]$AppPool = "ScriptRunner",
  [string]$SitePath = "C:\inetpub\ScriptRunner",
  [string]$WorkDir = "C:\temp\psscriptwebapp"
)

$ErrorActionPreference = "Stop"

function ConvertTo-NormalizedPath([string]$Path) {
  return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Test-IsChildPath([string]$Child, [string]$Parent) {
  $childPath = ConvertTo-NormalizedPath $Child
  $parentPath = ConvertTo-NormalizedPath $Parent
  return $childPath.StartsWith($parentPath + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Test-ProductionConfiguration {
  param([string]$SettingsPath, [string]$AuditRoot, [string]$DeploymentRoot)

  if (-not (Test-Path $SettingsPath)) { throw "Production appsettings.json not found: $SettingsPath" }
  $settings = Get-Content $SettingsPath -Raw | ConvertFrom-Json
  $allowedUsers = @($settings.Authorization.AllowedUsers)
  if ($allowedUsers.Count -eq 0 -or $allowedUsers -contains 'CONTOSO\jdoe') { throw "Production Authorization.AllowedUsers is missing, empty, or contains the placeholder." }
  if ([string]::IsNullOrWhiteSpace($AuditRoot) -or -not [IO.Path]::IsPathRooted($AuditRoot)) { throw "AuditLogging.Directory must be an absolute path." }
  if (Test-IsChildPath $AuditRoot $DeploymentRoot) { throw "AuditLogging.Directory must be outside the replaceable site directory." }
  if (-not (Test-Path $AuditRoot)) { throw "External audit directory does not exist: $AuditRoot" }
  if ([string]::IsNullOrWhiteSpace([string]$settings.PowerShell.ScriptsPath)) { throw "PowerShell.ScriptsPath is missing from production configuration." }
  Write-Host "      Production configuration validated (AllowedUsers count: $($allowedUsers.Count); audit path: $AuditRoot)"
}

Write-Host "=== PSScriptWebApp Deploy ===" -ForegroundColor Cyan
Write-Host "Repo:     $Owner/$Repo ($Branch)"
Write-Host "App pool: $AppPool"
Write-Host "Site:     $SitePath"
Write-Host "Work dir: $WorkDir"
if ($RunId) { Write-Host "Requested run: $RunId" }
Write-Host ""

Write-Host "[1/8] Creating work directory: $WorkDir"
New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null

$headers = @{ Authorization = "Bearer $Token"; "X-GitHub-Api-Version" = "2022-11-28" }

Write-Host "[2/8] Fetching latest successful workflow run on '$Branch'..."
$run = if ($RunId) {
  Invoke-RestMethod -Uri "https://api.github.com/repos/$Owner/$Repo/actions/runs/$RunId" -Headers $headers
} else {
  $runsUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs?branch=$Branch&status=success&per_page=1"
  (Invoke-RestMethod -Uri $runsUrl -Headers $headers).workflow_runs[0]
}
if (-not $run) { throw "No successful workflow run found." }
if ($run.conclusion -ne 'success' -or $run.head_branch -ne $Branch) { throw "Selected workflow run is not a successful run for '$Branch'." }
Write-Host "      Run #$($run.run_number) — $($run.display_title) ($($run.created_at))"
Write-Host "      Run ID: $($run.id)"
Write-Host "      Head SHA: $($run.head_sha)"

Write-Host "[3/8] Locating artifact 'webapp-zip' for run $($run.id)..."
$artUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs/$($run.id)/artifacts"
$artifact = (Invoke-RestMethod -Uri $artUrl -Headers $headers).artifacts |
  Where-Object { $_.name -eq "webapp-zip" -and -not $_.expired } |
  Select-Object -First 1
if (-not $artifact) { throw "Artifact webapp-zip not found." }
$artifactSizeMb = [math]::Round($artifact.size_in_bytes / 1048576, 1)
Write-Host "      Found artifact id $($artifact.id) ($artifactSizeMb MB)"

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
if ($appZip.BaseName -notmatch [regex]::Escape($run.head_sha.Substring(0, 8))) { throw "Artefact filename does not match selected workflow head SHA." }

Write-Host "[6/8] Verifying SHA256 checksum of $($appZip.Name)..."
$expected = (Get-Content $shaFile.FullName).Split(" ")[0].Trim()
$actual = (Get-FileHash $appZip.FullName -Algorithm SHA256).Hash
if ($expected -ne $actual) { throw "Checksum mismatch. Expected: $expected  Actual: $actual" }
Write-Host "      Checksum OK."

$backup = Join-Path $WorkDir ("backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$liveAppSettings = Join-Path $SitePath "appsettings.json"
$validPreviousApplication = (Test-Path (Join-Path $SitePath 'PSScriptWebApp.dll')) -and (Test-Path (Join-Path $SitePath 'web.config'))
if (Test-Path $liveAppSettings) {
  $liveSettings = Get-Content $liveAppSettings -Raw | ConvertFrom-Json
  Test-ProductionConfiguration -SettingsPath $liveAppSettings -AuditRoot ([string]$liveSettings.AuditLogging.Directory) -DeploymentRoot $SitePath
} else {
  throw "Production configuration validation failed before IIS changes: appsettings.json is missing."
}
if (Test-Path $SitePath) {
  Write-Host "[7/8] Backing up current site from $SitePath to $backup..."
  New-Item -ItemType Directory -Path $backup -Force | Out-Null
  Copy-Item "$SitePath\*" $backup -Recurse -Force
  Write-Host "      Backup complete."
} else {
  Write-Host "[7/8] No existing site at $SitePath — skipping backup."
}

Write-Host "[8/8] Deploying..."
Import-Module WebAdministration
if ((Get-WebAppPoolState -Name $AppPool).Value -ne "Stopped") {
  Write-Host "      Stopping app pool '$AppPool'..."
  Stop-WebAppPool -Name $AppPool
} else {
  Write-Host "      App pool '$AppPool' is already stopped."
}

try {
  $preservedAppSettings = Join-Path $WorkDir "appsettings.preserved.json"
  if (Test-Path $liveAppSettings) {
    Write-Host "      Preserving existing appsettings.json..."
    Copy-Item $liveAppSettings $preservedAppSettings -Force
  }

  Write-Host "      Clearing site directory: $SitePath"
  if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }

  Write-Host "      Extracting $($appZip.Name) to $SitePath..."
  Expand-Archive -Path $appZip.FullName -DestinationPath $SitePath -Force

  if (Test-Path $preservedAppSettings) {
    Write-Host "      Restoring preserved appsettings.json..."
    Copy-Item $preservedAppSettings $liveAppSettings -Force
  }

  if ((Get-WebAppPoolState -Name $AppPool).Value -ne "Started") {
    Write-Host "      Starting app pool '$AppPool'..."
    Start-WebAppPool -Name $AppPool
  }

  Write-Host ""
  Write-Host "=== Deploy complete ===" -ForegroundColor Green
}
catch {
  Write-Warning "Deploy failed. Attempting rollback from $backup..."
  if ($validPreviousApplication -and (Test-Path $backup)) {
    if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }
    Write-Host "      Copying $backup to $SitePath..."
    Copy-Item "$backup\*" $SitePath -Recurse -Force
    Write-Host "      Rollback complete."
  } else {
    Write-Warning "First deployment failed; no valid previous application exists. Leaving app pool stopped."
    if ((Get-WebAppPoolState -Name $AppPool).Value -ne "Stopped") {
      Stop-WebAppPool -Name $AppPool
    }
    throw
  }
  if ((Get-WebAppPoolState -Name $AppPool).Value -ne "Started") {
    Start-WebAppPool -Name $AppPool
  }
  throw
}
