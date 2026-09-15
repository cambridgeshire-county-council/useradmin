[CmdletBinding()]
param(
  [string]$ServerName = 'CCCS923',
  [string]$AppPool = 'ScriptRunner',
  [string]$SitePath = 'C:\inetpub\ScriptRunner',
  [string]$HostName = 'uatoolbox.ccc.cambridgeshire.gov.uk',
  [string]$ExchangeHybridServer = 'CCCS659'
)
$ErrorActionPreference = 'Continue'
$blocks = 0
$warnings = 0
function Check([string]$level, [string]$message) {
  if ($level -eq 'BLOCK') { $script:blocks++ }
  if ($level -eq 'WARN') { $script:warnings++ }
  Write-Host "[$level] $message"
}
function HasCommand([string]$name) { $null -ne (Get-Command $name -ErrorAction SilentlyContinue) }
Write-Host "=== CCCS923 UserAdmin readiness (observational only) ===" -ForegroundColor Cyan
Write-Host "Target: $ServerName | AppPool: $AppPool | Site: $SitePath | Exchange Hybrid: $ExchangeHybridServer"
Write-Host "SERVER"
$computer = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
Check INFO "Computer: $($computer.Name); OS: $($os.Caption) $($os.Version) build $($os.BuildNumber)"
Check INFO "Architecture: $env:PROCESSOR_ARCHITECTURE; PowerShell: $($PSVersionTable.PSVersion)"
Check INFO "Elevated: $([bool](([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)))"
$drive = Get-PSDrive -Name (Split-Path $env:SystemRoot -Qualifier).TrimEnd(':') -ErrorAction SilentlyContinue
if ($drive) { Check INFO "Free space: $([math]::Round($drive.Free / 1GB, 1)) GB" }
Write-Host "IIS"
$iis = Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue
if ($iis -and $iis.InstallState -eq 'Installed') { Check PASS 'IIS Web-Server is installed.' } else { Check BLOCK 'IIS Web-Server is not confirmed.' }
Import-Module WebAdministration -ErrorAction SilentlyContinue
$pool = Get-Item "IIS:\AppPools\$AppPool" -ErrorAction SilentlyContinue
if (-not $pool) { Check BLOCK "App pool '$AppPool' does not exist." } else {
  $model = Get-ItemProperty $pool.PSPath -Name processModel -ErrorAction SilentlyContinue
  Check INFO "App pool state: $((Get-WebAppPoolState $AppPool -ErrorAction SilentlyContinue).Value); identity type: $($model.identityType); configured identity: $($model.userName)"
  Check INFO "32-bit applications: $($model.enable32BitAppOnWin64); managed runtime: $($pool.managedRuntimeVersion)"
}
$bindings = Get-WebBinding -ErrorAction SilentlyContinue | Where-Object { $_.bindingInformation -match $HostName }
if ($bindings) { $bindings | ForEach-Object { Check INFO "Binding: $($_.protocol) $($_.bindingInformation)" } } else { Check WARN "No binding matched $HostName." }
Write-Host ".NET / ANCM"
$runtimes = dotnet --list-runtimes 2>$null
if ($runtimes -match 'Microsoft\.NETCore\.App 10\.' -and $runtimes -match 'Microsoft\.AspNetCore\.App 10\.') { Check PASS '.NET 10 runtimes are installed.' } else { Check BLOCK '.NET 10 runtimes are not both installed.' }
if (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll") { Check PASS 'ASP.NET Core Module V2 / ANCM is present.' } else { Check BLOCK 'ASP.NET Core Module V2 / ANCM is not confirmed.' }
Write-Host 'WINDOWS POWERSHELL'
if (HasCommand powershell.exe) { Check PASS 'powershell.exe is available.' } else { Check BLOCK 'powershell.exe is unavailable.' }
Check INFO "Execution policy: $((Get-ExecutionPolicy -List | Out-String).Trim())"
Write-Host 'ACTIVE DIRECTORY (discovery only)'
if (Get-Module -ListAvailable ActiveDirectory) { Check PASS 'ActiveDirectory module is available.' } else { Check BLOCK 'ActiveDirectory module is unavailable.' }
foreach ($command in @('Get-ADUser','Set-ADUser','Remove-ADUser','Add-ADPrincipalGroupMembership')) { if (HasCommand $command) { Check INFO "$command is available (not invoked)." } else { Check WARN "$command is unavailable." } }
Write-Host 'EXCHANGE HYBRID (critical gate)'
try { $dns = Resolve-DnsName $ExchangeHybridServer -ErrorAction Stop | Select-Object -First 1; Check PASS "$ExchangeHybridServer resolves to $($dns.IPAddress)." } catch { Check BLOCK "$ExchangeHybridServer DNS resolution failed." }
if (Get-PSSnapin -Registered -ErrorAction SilentlyContinue | Where-Object Name -eq 'Microsoft.Exchange.Management.PowerShell.SnapIn') { Check INFO 'Exchange management snap-in is registered.' } else { Check BLOCK 'Exchange management snap-in is not registered.' }
if (HasCommand New-RemoteMailbox) { Check PASS 'New-RemoteMailbox is discoverable (not invoked).' } else { Check BLOCK 'New-RemoteMailbox is unavailable; New User cannot run unchanged on CCCS923.' }
Write-Host 'CONFIGURATION / FILESYSTEM'
$settingsPath = Join-Path $SitePath 'appsettings.json'
if (-not (Test-Path $settingsPath)) { Check BLOCK 'Production appsettings.json is missing.' } else {
  try {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $allowed = @($settings.Authorization.AllowedUsers)
    if ($allowed.Count -gt 0 -and $allowed -notcontains 'CONTOSO\jdoe') { Check PASS "AllowedUsers present; count $($allowed.Count)." } else { Check BLOCK 'AllowedUsers is missing, empty, or placeholder.' }
    $audit = [string]$settings.AuditLogging.Directory
    if (-not [IO.Path]::IsPathRooted($audit)) { Check BLOCK 'AuditLogging.Directory is not absolute.' } elseif ($audit.TrimEnd('\') -like "$($SitePath.TrimEnd('\'))*") { Check BLOCK 'Audit path is inside the replaceable site directory.' } elseif (-not (Test-Path $audit)) { Check BLOCK 'External audit directory does not exist.' } else { Check PASS 'External audit directory is configured and exists.' }
    if ($settings.PowerShell.ScriptsPath) { Check INFO 'PowerShell.ScriptsPath is configured.' } else { Check BLOCK 'PowerShell.ScriptsPath is missing.' }
  } catch { Check BLOCK 'appsettings.json could not be parsed safely.' }
}
if (Test-Path $SitePath) { Check INFO "Site ACL entries observed: $((Get-Acl $SitePath).Access.Count); effective write access is not proven." } else { Check BLOCK 'Site path does not exist.' }
Write-Host 'NETWORK'
try { $dns = Resolve-DnsName $HostName -ErrorAction Stop | Select-Object -First 1; Check INFO "$HostName resolves to $($dns.IPAddress)." } catch { Check WARN "$HostName DNS resolution failed." }
Check INFO "GITHUB_TOKEN: $(if ($env:GITHUB_TOKEN) { 'present' } else { 'absent' })"
Write-Host '=== SUMMARY ===' -ForegroundColor Cyan
Write-Host "Blocks: $blocks; Warnings: $warnings"
if ($blocks -eq 0) { Write-Host '[PASS] No blocking readiness findings observed.' } else { Write-Host '[BLOCK] Remediate all blocking findings and rerun.' }
