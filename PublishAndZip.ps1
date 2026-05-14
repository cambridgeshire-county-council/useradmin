[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [string]$ZipName = "PSScriptWebApp-publish.zip"
)

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot ".")
$projectFile = Join-Path $projectRoot "PSScriptWebApp.csproj"
$publishDir = Join-Path $projectRoot $OutputDir
$zipPath = Join-Path $projectRoot $ZipName

Write-Host "Publishing $projectFile to $publishDir ($Configuration)" -ForegroundColor Cyan
& dotnet publish $projectFile -c $Configuration -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating zip: $zipPath" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Done. Zip ready: $zipPath" -ForegroundColor Green
