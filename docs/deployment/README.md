# CCCS923 Deployment Handover

This is the starting point for the Applications Analyst.

UserAdmin is the tactical browser application for approved Service Desk identity operations. It runs as an IIS application on **CCCS923**, in the Nutanix VPC, at `C:\inetpub\ScriptRunner` in the `ScriptRunner` app pool. The current operating model depends on Active Directory and hybrid Exchange capability involving **CCCS659**. The exact technical management path from CCCS923 to CCCS659 must be proven; the existence of CCCS659 alone is not sufficient evidence.

The application targets **.NET 10 LTS** and requires the matching ASP.NET Core Hosting Bundle/ANCM on CCCS923. Production configuration is the deployed `appsettings.json`, with the production `Authorization.AllowedUsers` list and an external `AuditLogging.Directory`. Audit files must be outside `C:\inetpub\ScriptRunner`, because deployment replaces that directory. The exact external path, ACL, and retention remain deployment-readiness decisions.

The current hostname is `uatoolbox.ccc.cambridgeshire.gov.uk`. The deployment pipeline is GitHub Actions `Build Deploy Zip` followed by the manually invoked `Deploy.ps1`, which verifies the artefact checksum, backs up the site, preserves `appsettings.json`, replaces the site and rolls back on failure.

## Sequence

1. Readiness checks.
2. Approved remediation, if required.
3. Identify the approved GitHub Actions artefact and checksum.
4. Deploy with the pinned RunId where available.
5. Post-deployment smoke test.
6. Read-only UAT.
7. Explicitly approved controlled mutation testing.
8. Handover and sign-off.

## Stop and escalate if

- Exchange PowerShell capability involving CCCS659 cannot be proven.
- The app-pool execution identity is unknown or unexpected.
- Production `AllowedUsers` is missing, empty, or contains the placeholder `CONTOSO\\jdoe`.
- The external audit path is unavailable or not writable by the app pool.
- The .NET 10 Hosting Bundle/ANCM is absent.
- Windows Authentication is not configured.
- The deployment artefact SHA or workflow run cannot be reconciled.
- Rollback cannot be guaranteed.
- Unexpected AD or Exchange behaviour occurs.

## Supporting material

- [CCCS923 readiness script](../../tools/Test-CCCS923Readiness.ps1)
- [Deployment runbook](cccs923-deployment-runbook.md)
- [Evidence template](cccs923-deployment-evidence.md)
- [Remediation guide](cccs923-remediation-guide.md)
- [Deployment script](../../Deploy.ps1)
- [Application README](../../README.md)
- [Tactical/strategic architecture](../architecture/azure-native-target.md)
