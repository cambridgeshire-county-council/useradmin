# CCCS923 Deployment Runbook

This runbook is for a competent Applications Analyst. It describes the tactical IIS deployment; it does not authorise mutation testing.

## Gate 0 — Approved release

Record the approved `main` SHA, GitHub Actions `RunId`, run number, result, test result, vulnerability status, artefact name and SHA256 checksum. Prefer a pinned deployment:

```powershell
.\Deploy.ps1 -RunId <APPROVED_RUN_ID> -Token $env:GITHUB_TOKEN
```

PASS: successful main workflow, reconciled head SHA and checksum. STOP if the run, SHA, artefact or checksum cannot be reconciled.

## Gate 1 — Readiness

Run on CCCS923 without elevation changes or remediation:

```powershell
.\tools\Test-CCCS923Readiness.ps1 | Tee-Object C:\Temp\UserAdmin-Readiness.txt
```

Record the output. PASS requires no `[BLOCK]` findings. STOP and remediate before continuing. The script is observational and does not install, configure, deploy or mutate anything.

## Gate 2 — Prerequisites

Confirm IIS, ASP.NET Core/.NET 10 Hosting Bundle and ANCM, Windows Authentication, Windows PowerShell, ActiveDirectory/RSAT, the `ScriptRunner` app pool, site path, bindings, production configuration and external audit path. Record app-pool identity and relevant ACL entries. ACL inspection is not proof of effective application write access.

Remediation examples are in [cccs923-remediation-guide.md](cccs923-remediation-guide.md). Do not apply them without an approved change and maintenance window.

## Gate 3 — Exchange Hybrid proof

Record DNS resolution for `CCCS659`, Exchange snap-in registration/load result, and `Get-Command New-RemoteMailbox` discovery from the execution environment used by UserAdmin. Do **not** invoke `New-RemoteMailbox`. If command discovery fails, New User cannot run unchanged on CCCS923.

The existence of CCCS659 does not prove the technical management path from CCCS923.

## Gate 4 — Transport security

Record HTTP and HTTPS bindings and certificate state. HTTPS is required before controlled mutation acceptance or production sign-off. An earlier read-only technical proof requires explicit internal approval and documented risk.

## Gate 5 — Production configuration

The live `C:\inetpub\ScriptRunner\appsettings.json` must contain:

```json
{
  "Authorization": { "AllowedUsers": [ "<approved-domain-user>" ] },
  "PowerShell": { "ScriptsPath": "scripts" },
  "AuditLogging": { "Directory": "<absolute-path-outside-site>" }
}
```

Use real approved values only on the server; never put them in the repository or evidence template. `AllowedUsers` must not be empty or contain `CONTOSO\\jdoe`. Audit data must not live under `C:\inetpub\ScriptRunner`, because deployment replaces that tree. Retention is not defined by this project and requires information-governance approval.

## Gate 6 — Deploy approved artefact

Run the pinned release after Gates 0–5:

```powershell
.\Deploy.ps1 -RunId <APPROVED_RUN_ID> -Token $env:GITHUB_TOKEN
```

Do not paste tokens into evidence. Record run ID/number, head SHA, artefact name/checksum, backup path and result. `Deploy.ps1` validates production configuration before stopping IIS, preserves `appsettings.json`, verifies SHA256, and rolls back where a valid backup exists. On first deployment failure without a backup it leaves the app pool stopped.

## Gate 7 — Smoke proof

Confirm app pool `Started`, HTTPS response, Windows Authentication and expected operator identity, UserAdmin branding, no server error, the nine-script catalogue, and Started/completion audit records in the external audit directory. Do not run a business script.

## Gate 8 — Read-only UAT

With an approved operator, test Search Users, User Details, Mark-for-Deletion search only, Marked-for-Deletion list, and User Notes. Do not Mark, Unmark, create New User or Delete. Record sanitised outcomes only; do not copy personal data, notes or credentials into the repository.

## Gate 9 — Controlled mutation acceptance

Requires Phil's explicit approval and a specifically agreed disposable/test identity. Recommended order: mark the test identity; confirm/audit; unmark; confirm/audit; create the agreed disposable user; confirm AD and hybrid Exchange outcome; audit; mark the new account; delete only that approved account; confirm/audit. Do not perform this as part of this handover increment. Bulk/multi-account deletion is not transactionally atomic.

## Gate 10 — Acceptance and handover

Record operational acceptance, approved operator control, audit retention decision, operational owner, escalation route, rollback evidence, known issues, tactical lifetime, and strategic Azure-native replacement context.
