# CCCS923 Deployment Evidence Template

Copy this template for an approved deployment record. Do not paste passwords, tokens, generated passwords, notes, or unnecessary personal/AD output.

## Ownership
- Analyst:
- Date/time UTC:
- Change/approval reference:
- Operational owner:

## Release
- Main SHA:
- GitHub Actions RunId:
- Run number:
- Run result:
- Test result:
- Vulnerability status:
- Artefact name:
- SHA256:

## Readiness
- Readiness script path/version:
- Overall result:
- `[BLOCK]` findings:
- `[WARN]` findings:
- Remediation reference and approval:

## Server/IIS
- CCCS923 OS/build:
- App-pool identity type/name:
- App pool state:
- Site path:
- Bindings:
- HTTPS certificate evidence:
- Windows Authentication:
- Anonymous Authentication:
- .NET 10 runtime:
- ASP.NET Core Hosting Bundle/ANCM:
- Windows PowerShell:
- ActiveDirectory module/commands:

## Exchange Hybrid
- CCCS659 DNS result:
- Exchange snap-in registration:
- Snap-in load result:
- `New-RemoteMailbox` discovery:
- Management path evidence:
- No mailbox creation performed: yes/no

## Configuration and audit
- AllowedUsers present/count/placeholder absent:
- ScriptsPath:
- External audit directory:
- Audit directory exists:
- Relevant ACL evidence:
- Retention decision/reference:

## Deployment
- Backup path:
- Deploy command/RunId:
- Deployment result:
- Rollback result/test:

## Smoke and UAT
- App pool started:
- HTTPS response/authenticated identity:
- UserAdmin branding:
- Catalogue result:
- Audit Started/completion evidence:
- Search Users:
- User Details:
- Mark-for-Deletion search only:
- Marked-for-Deletion list:
- User Notes:

## Controlled mutation approval
- Approval reference:
- Approved test identity/reference:
- Evidence location:
- Not performed in this handover: yes/no

## Acceptance
- Operational acceptance:
- Support/escalation route:
- Outstanding issues:
- Sign-off:
