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
function Check([string]$level, [string]$message) { if ($level -eq 'BLOCK') { $script:blocks++ }; if ($level -eq 'WARN') { $script:warnings++ }; Write-Host "[$level] $message" }
function HasCommand([string]$name) { $null -ne (Get-Command $name -ErrorAction SilentlyContinue) }
function FullPath([string]$path) { [IO.Path]::GetFullPath($path).TrimEnd('\') }
function Test-IsSameOrChildPath([string]$child, [string]$parent) {
  $childFull = FullPath $child
  $parentFull = FullPath $parent
  return $childFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or $childFull.StartsWith($parentFull + '\', [StringComparison]::OrdinalIgnoreCase)
}
Write-Host "=== CCCS923 UserAdmin readiness (observational only) ===" -ForegroundColor Cyan
Write-Host "Target: $ServerName | AppPool: $AppPool | Site: $SitePath | Host: $HostName | Exchange Hybrid: $ExchangeHybridServer"
Write-Host 'SERVER'
$computer = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$os = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
if ($computer.Name -ne $ServerName) { Check BLOCK "Readiness script is running on '$($computer.Name)', not requested host '$ServerName'." } else { Check PASS "Running on requested host $ServerName." }
Check INFO "OS: $($os.Caption) $($os.Version) build $($os.BuildNumber); architecture $env:PROCESSOR_ARCHITECTURE; PowerShell $($PSVersionTable.PSVersion)"
Check INFO "Elevated: $([bool](([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)))"
$drive = Get-PSDrive -Name (Split-Path $env:SystemRoot -Qualifier).TrimEnd(':') -ErrorAction SilentlyContinue
if ($drive) { Check INFO "Free space: $([math]::Round($drive.Free / 1GB, 1)) GB" }
Write-Host 'IIS'
$iis = Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue
if ($iis -and $iis.InstallState -eq 'Installed') { Check PASS 'IIS Web-Server is installed.' } else { Check BLOCK 'IIS Web-Server is not confirmed.' }
Import-Module WebAdministration -ErrorAction SilentlyContinue
$pool = Get-Item "IIS:\AppPools\$AppPool" -ErrorAction SilentlyContinue
if (-not $pool) { Check BLOCK "App pool '$AppPool' does not exist." } else {
  $model = Get-ItemProperty $pool.PSPath -Name processModel -ErrorAction SilentlyContinue
  $state = (Get-WebAppPoolState $AppPool -ErrorAction SilentlyContinue).Value
  Check INFO "App pool state: $state; identity type: $($model.identityType); configured identity: $($model.userName)"
  Check INFO "32-bit enabled: $($pool.enable32BitAppOnWin64); managed runtime: $($pool.managedRuntimeVersion)"
}
$site = Get-Website -ErrorAction SilentlyContinue | Where-Object { $_.PhysicalPath -eq $SitePath -or (Get-WebBinding -Name $_.Name -ErrorAction SilentlyContinue | Where-Object { $_.bindingInformation -match ":$HostName$" }) } | Select-Object -First 1
if (-not $site) { Check BLOCK "No IIS site matched physical path '$SitePath' or host '$HostName'." } else {
  $discoveredPhysicalPath = FullPath $site.PhysicalPath
  if (-not $discoveredPhysicalPath.Equals((FullPath $SitePath), [StringComparison]::OrdinalIgnoreCase)) { Check BLOCK 'IIS site physical path does not match expected UserAdmin site path.' }
  else { Check INFO "Site: $($site.Name); physical path: $discoveredPhysicalPath; state: $($site.State)" }
  $bindings = @(Get-WebBinding -Name $site.Name -ErrorAction SilentlyContinue)
  $hostBindings = $bindings | Where-Object { (($_.bindingInformation -split ':')[-1]) -ieq $HostName }
  $http = $hostBindings | Where-Object protocol -eq 'http'
  $https = $hostBindings | Where-Object protocol -eq 'https'
  if ($http) { Check INFO 'HTTP binding present.' } else { Check WARN 'No HTTP binding present.' }
  if (-not $https) { Check BLOCK 'No HTTPS binding present for the configured hostname.' }
  elseif ($https | Where-Object { $_.certificateHash }) { Check PASS 'HTTPS certificate binding confirmed for the configured hostname.' }
  else { Check BLOCK 'HTTPS certificate binding could not be confirmed.' }
  $windowsAuth = Get-WebConfigurationProperty -PSPath "IIS:\Sites\$($site.Name)" -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -ErrorAction SilentlyContinue
  $anonymous = Get-WebConfigurationProperty -PSPath "IIS:\Sites\$($site.Name)" -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -ErrorAction SilentlyContinue
  $windowsAuthValue = if ($windowsAuth.PSObject.Properties['Value']) { $windowsAuth.Value } else { $windowsAuth }
  $anonymousValue = if ($anonymous.PSObject.Properties['Value']) { $anonymous.Value } else { $anonymous }
  if ($windowsAuthValue -eq $true) { Check PASS 'Windows Authentication enabled.' } else { Check BLOCK 'Windows Authentication disabled or unknown.' }
  if ($anonymousValue -eq $false) { Check PASS 'Anonymous Authentication disabled.' } else { Check BLOCK 'Anonymous Authentication enabled or unknown.' }
}
Write-Host '.NET / ANCM'
$runtimes = dotnet --list-runtimes 2>$null
if ($runtimes -match 'Microsoft\.NETCore\.App 10\.' -and $runtimes -match 'Microsoft\.AspNetCore\.App 10\.') { Check PASS '.NET 10 runtimes are installed.' } else { Check BLOCK '.NET 10 runtimes are not both installed.' }
if (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll") { Check PASS 'ASP.NET Core Module V2 / ANCM is present.' } else { Check BLOCK 'ASP.NET Core Module V2 / ANCM is not confirmed.' }
Write-Host 'WINDOWS POWERSHELL'
if (HasCommand powershell.exe) { Check PASS 'Windows PowerShell is available.' } else { Check BLOCK 'powershell.exe is unavailable.' }
Check INFO "Execution policy: $((Get-ExecutionPolicy -List | Out-String).Trim())"
Write-Host 'ACTIVE DIRECTORY (discovery only)'
if (Get-Module -ListAvailable ActiveDirectory) { Check PASS 'ActiveDirectory module is available.' } else { Check BLOCK 'ActiveDirectory module is unavailable.' }
foreach ($command in @('Get-ADUser','Set-ADUser','Remove-ADUser','Add-ADPrincipalGroupMembership')) { if (HasCommand $command) { Check INFO "$command is available (not invoked)." } else { Check WARN "$command is unavailable." } }
Write-Host 'EXCHANGE HYBRID (critical gate)'
try { $dns = Resolve-DnsName $ExchangeHybridServer -ErrorAction Stop | Select-Object -First 1; Check PASS "$ExchangeHybridServer resolves to $($dns.IPAddress)." } catch { Check BLOCK "$ExchangeHybridServer DNS resolution failed." }
if (-not (HasCommand powershell.exe)) { Check BLOCK 'Cannot perform isolated Windows PowerShell Exchange proof.' } else {
  $exchangeProbe = @'
$registered = Get-PSSnapin -Registered -ErrorAction SilentlyContinue | Where-Object Name -eq 'Microsoft.Exchange.Management.PowerShell.SnapIn'
if ($registered) { 'REGISTERED' } else { 'NOT_REGISTERED' }
try {
  Add-PSSnapin Microsoft.Exchange.Management.PowerShell.SnapIn -ErrorAction Stop
  'LOADED'
  Get-Command New-RemoteMailbox -ErrorAction Stop | Out-Null
  'COMMAND_FOUND'
  exit 0
} catch {
  'CAPABILITY_FAILED'
  exit 1
}
'@
  $probeOutput = & powershell.exe -NoProfile -NonInteractive -Command $exchangeProbe 2>$null
  if ($probeOutput -contains 'REGISTERED') { Check INFO 'Exchange snap-in registered.' } else { Check BLOCK 'Exchange snap-in is not registered.' }
  if ($probeOutput -contains 'LOADED') { Check PASS 'Exchange snap-in load succeeded in isolated Windows PowerShell.' } else { Check BLOCK 'Exchange snap-in load failed in isolated Windows PowerShell.' }
  if ($probeOutput -contains 'COMMAND_FOUND') { Check PASS 'New-RemoteMailbox command discovery succeeded (not invoked).' } else { Check BLOCK 'New-RemoteMailbox unavailable; New User cannot run unchanged on CCCS923.' }
}
Write-Host 'CONFIGURATION / FILESYSTEM'
$settingsPath = Join-Path $SitePath 'appsettings.json'
if (-not (Test-Path $settingsPath)) { Check BLOCK 'Production appsettings.json is missing.' } else {
  try {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $allowed = @($settings.Authorization.AllowedUsers)
    if ($allowed.Count -gt 0 -and $allowed -notcontains 'CONTOSO\jdoe') { Check PASS "AllowedUsers present; count $($allowed.Count)." } else { Check BLOCK 'AllowedUsers is missing, empty, or placeholder.' }
    $audit = [string]$settings.AuditLogging.Directory
    if (-not [IO.Path]::IsPathRooted($audit)) { Check BLOCK 'AuditLogging.Directory is not absolute.' }
    elseif (Test-IsSameOrChildPath $audit $SitePath) { Check BLOCK 'Audit path is the site directory or inside the replaceable site directory.' }
    elseif (-not (Test-Path $audit)) { Check BLOCK 'External audit directory does not exist.' }
    else {
      Check PASS 'External audit directory is configured and exists.'
      $acl = Get-Acl $audit -ErrorAction SilentlyContinue
      if ($acl) {
        $poolIdentity = ''
        if ($model -and $model.identityType -eq 'ApplicationPoolIdentity') { $poolIdentity = "IIS AppPool\$AppPool" } elseif ($model) { $poolIdentity = [string]$model.userName }
        $relevant = @($acl.Access | Where-Object { $poolIdentity -and $_.IdentityReference -like "*$poolIdentity*" })
        Check INFO "Audit ACL entries for app-pool identity '$poolIdentity': $($relevant.Count); ACL inspection is evidence only, not proof of effective write access."
      } else { Check WARN 'Audit directory ACL could not be read.' }
    }
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
