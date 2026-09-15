# CCCS923 Remediation Guide

These are human-approved examples only. Do not run them as part of readiness. Substitute approved placeholders, confirm change authority, maintenance window, rollback and reboot requirements first.

## .NET 10 Hosting Bundle

Obtain the approved Microsoft .NET 10 Hosting Bundle installer from the approved software source and run it under the server change process. Verify ASP.NET Core Module V2 afterwards. A reboot may be required.

```powershell
# PLACEHOLDER: approved installer path and change reference required
Start-Process -FilePath '<PATH_TO_APPROVED_DOTNET_10_HOSTING_BUNDLE_EXE>' -ArgumentList '/install','/quiet','/norestart' -Wait
```

## IIS components

Use approved Windows Server feature remediation for IIS and required authentication components. This may require a reboot and IIS restart.

```powershell
# PLACEHOLDER: run only under approved change
Install-WindowsFeature -Name Web-Server,Web-Windows-Auth,Web-Mgmt-Console
```

## Windows Authentication

Enable Windows Authentication and disable Anonymous Authentication for the approved site. This requires an IIS restart and change approval.

```powershell
# PLACEHOLDER site name; inspect first and apply only when approved
Set-WebConfigurationProperty -Filter system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true -PSPath 'IIS:\Sites\<SITE_NAME>'
Set-WebConfigurationProperty -Filter system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $false -PSPath 'IIS:\Sites\<SITE_NAME>'
```

## RSAT / ActiveDirectory

Install the approved RSAT feature only if readiness confirms it is absent and a server change is approved. A reboot may be required. Do not install automatically from `Test-CCCS923Readiness.ps1`.

```powershell
# PLACEHOLDER: approved change only
Install-WindowsFeature -Name RSAT-AD-PowerShell
```

## External audit directory

Create an external directory, choose retention with Information Governance, and grant the actual app-pool identity append/write access. The identity is intentionally a placeholder here. Directory creation and ACL changes may require a maintenance window.

```powershell
# PLACEHOLDER path and identity; do not use these values without approval
New-Item -ItemType Directory -Path '<EXTERNAL_AUDIT_PATH>'
$acl = Get-Acl '<EXTERNAL_AUDIT_PATH>'
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule('<APP_POOL_IDENTITY>','Modify','ContainerInherit,ObjectInherit','None','Allow')
$acl.AddAccessRule($rule)
Set-Acl -Path '<EXTERNAL_AUDIT_PATH>' -AclObject $acl
```

## HTTPS binding/certificate

Obtain an approved certificate and thumbprint, then add the HTTPS binding for the approved hostname. Never guess a thumbprint. This requires a maintenance window and IIS restart/reload.

```powershell
# PLACEHOLDER hostname/thumbprint/site; certificate import and binding require approval
New-WebBinding -Name '<SITE_NAME>' -Protocol https -Port 443 -HostHeader 'uatoolbox.ccc.cambridgeshire.gov.uk'
# PLACEHOLDER: bind the approved certificate thumbprint using the approved IIS procedure
```

## ScriptRunner app pool/site

Create or configure the app pool and site only if readiness finds them absent and the deployment change is approved. Confirm the final identity, path, bindings and authentication before deployment.

```powershell
# PLACEHOLDER values; no production identity is prescribed here
New-WebAppPool -Name 'ScriptRunner'
New-Item 'IIS:\Sites\<SITE_NAME>'
```

Never place real service-account names, passwords, certificate thumbprints, tokens or retention periods in this guide.
