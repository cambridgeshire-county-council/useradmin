param(
  [string]$Owner = "YOUR_ORG_OR_USER",
  [string]$Repo = "PSScriptWebApp",
  [string]$Branch = "main",
  [string]$Token = $env:GITHUB_TOKEN,
  [string]$AppPool = "PSScriptWebAppPool",
  [string]$SitePath = "C:\inetpub\wwwroot\PSScriptWebApp",
  [string]$WorkDir = "C:\deploy\psscriptwebapp"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null

$headers = @{ Authorization = "Bearer $Token"; "X-GitHub-Api-Version" = "2022-11-28" }

# Latest successful run on main
$runsUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs?branch=$Branch&status=success&per_page=1"
$run = (Invoke-RestMethod -Uri $runsUrl -Headers $headers).workflow_runs[0]
if (-not $run) { throw "No successful workflow run found." }

# Get artifacts for run
$artUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs/$($run.id)/artifacts"
$artifact = (Invoke-RestMethod -Uri $artUrl -Headers $headers).artifacts |
  Where-Object { $_.name -eq "webapp-zip" -and -not $_.expired } |
  Select-Object -First 1
if (-not $artifact) { throw "Artifact webapp-zip not found." }

$artifactZip = Join-Path $WorkDir "artifact.zip"
Invoke-RestMethod -Uri $artifact.archive_download_url -Headers $headers -OutFile $artifactZip

$extractDir = Join-Path $WorkDir "artifact"
if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
Expand-Archive -Path $artifactZip -DestinationPath $extractDir -Force

$appZip = Get-ChildItem $extractDir -Filter "*.zip" | Select-Object -First 1
$shaFile = Get-ChildItem $extractDir -Filter "*.sha256" | Select-Object -First 1
if (-not $appZip -or -not $shaFile) { throw "App zip or sha256 not found inside artifact." }

# Verify checksum
$expected = (Get-Content $shaFile.FullName).Split(" ")[0].Trim()
$actual = (Get-FileHash $appZip.FullName -Algorithm SHA256).Hash
if ($expected -ne $actual) { throw "Checksum mismatch." }

# Backup current
$backup = Join-Path $WorkDir ("backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
if (Test-Path $SitePath) {
  New-Item -ItemType Directory -Path $backup -Force | Out-Null
  Copy-Item "$SitePath\*" $backup -Recurse -Force
}

Import-Module WebAdministration
Stop-WebAppPool -Name $AppPool

try {
  if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }
  Expand-Archive -Path $appZip.FullName -DestinationPath $SitePath -Force
  Start-WebAppPool -Name $AppPool
  Write-Host "Deploy complete."
}
catch {
  Write-Warning "Deploy failed. Attempting rollback..."
  if (Test-Path $backup) {
    if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }
    Copy-Item "$backup\*" $SitePath -Recurse -Force
  }
  Start-WebAppPool -Name $AppPool
  throw
}