# Current-to-Target Capability Map

## Assessment Scope
This map records current implementation evidence and distinguishes the approved tactical treatment from the strategic successor treatment. A current PowerShell or AD dependency is real for the tactical lifetime, but does not imply the same dependency belongs in the strategic architecture.

| Capability | Current entry and implementation | Inputs / outputs | Reads and mutations | Current dependencies and risk | Tactical treatment | Strategic treatment |
| --- | --- | --- | --- | --- | --- |
| New User | `Users/New`; `UsersController.New` and `StreamNew`; `NewUser.ps1` | Identity, UPN, SAM account, address, organization, manager; streamed status and generated password | Creates remote mailbox/user; updates attributes; assigns manager and group membership | AD DS, Exchange management snap-in, local PowerShell/domain host; privileged child process; plaintext password disclosure | Retain the supported hybrid process; validate CCCS923/CCCS653 dependencies, secure execution, close gaps, and test with approved identities. | Typed cloud-native provisioning workflow using Entra/Graph and Exchange Online capabilities; do not return plaintext passwords by default. |
| Search Users | `Users/Search`; `Search`; `Search.ps1` | Name fragment; JSON name/SAM/UPN/status results | `Get-ADUser` only | AD DS, PowerShell; broad directory read through host identity | Retain AD-backed implementation, secure/test it, and confirm the hybrid source of truth. | Graph/Entra-native `IUserDirectory`. |
| User Details | search result link to `Users/Details`; `GetUser.ps1` | SAM account; profile and enabled-state JSON | `Get-ADUser` only | AD DS, PowerShell | Retain and test read-only details against current AD/hybrid data. | Graph/Entra-native typed profile query. |
| Mark for Deletion search | `Users/MarkForDeletion`; `SearchForDeletion.ps1` | Name fragment; users and `extensionAttribute3` state | `Get-ADUser` only | AD DS custom attribute; unclear lifecycle semantics | Retain current workflow while documenting `extensionAttribute3` semantics and testing safely. | Typed deletion-workflow query with explicit lifecycle state. |
| Mark User for Deletion | modal on `Users/MarkForDeletion`; `MarkUser`; `MarkForDeletion.ps1` | SAM account, optional notes; status message | `Set-ADUser` date marker and `info` note | AD DS write; process identity has direct write privilege | Secure and support the current hybrid mutation with approved test identities and audit/logging improvements. | Typed workflow state and notes service with approval and audit. |
| Unmark User | same modal; `UnmarkUser`; `UnmarkForDeletion.ps1` | SAM account, optional notes; status message | clears AD marker and updates/clears note | AD DS write; weak audit trail | Retain only as operationally required; test and audit the current hybrid behavior. | Typed lifecycle reversal with authorization and audit. |
| Marked-for-Deletion list | `Users/MarkedForDeletion`; `GetMarkedForDeletion.ps1` | all/week/month filter; user list | `Get-ADUser` only | AD DS custom attribute and application-side date filter | Retain AD-backed list and validate date/filter behavior. | Query an explicit deletion-workflow store or supported directory state. |
| Delete User | Delete All Displayed form; `DeleteMarked`; `DeleteUser.ps1` | displayed SAM accounts; count status | `Remove-ADUser -Confirm:$false` per supplied user | irreversible destructive AD operation; bulk action and limited audit/approval | Keep disabled or tightly controlled until approved test identities, confirmation, recovery, and UAT evidence exist. | Explicit typed destructive operation with approval, audit, retention, and tenant-defined semantics. |
| User notes | modal fetch `GetUserNotes`; `GetUserNotes.ps1` | SAM account; JSON note | `Get-ADUser` `info` only; mark/unmark write same field | AD attribute used as operational note store | Retain only if required by the hybrid process; document privacy and retention. | Dedicated notes/data boundary after ownership and retention decisions. |
| Generic Script List | `Scripts/Index`, `Details`, `Execute`, `Stream`; `PowerShellService` | parsed parameters for explicitly catalogued operational scripts; output/SSE | Executes reviewed operational scripts under host identity | generic privileged code execution; broad authority surface despite POST/CSRF/path/parameter hardening | Retain for the tactical business process behind an explicit risk-classified catalogue; mutation/destructive scripts require deliberate operator confirmation and NewUser sensitive output is sanitised. Whether mutation scripts should remain directly available is still a User Admin business-process decision. | Remove in favour of typed business operations. |
| Generic Script List | `Scripts/Index`, `Details`, `Execute`, `Stream`; `PowerShellService` | parsed parameters for explicitly catalogued operational scripts; output/SSE | Executes reviewed operational scripts under host identity | generic privileged code execution; broad authority surface despite POST/CSRF/path/parameter hardening | Retain for the tactical business process behind an explicit risk-classified catalogue; mutation/destructive scripts require deliberate operator confirmation, NewUser sensitive output is sanitised, and execution is captured in tactical daily JSONL audit files. Whether mutation scripts should remain directly available is still a User Admin business-process decision. | Remove in favour of typed business operations and central strategic telemetry. |

## Current Cross-Cutting Dependencies

| Dependency | Current use | Tactical treatment | Strategic treatment |
| --- | --- | --- | --- |
| Active Directory Domain Services | user lookup, profile, state marker, notes, deletion | Current dependency to support and secure. | Replace with Graph/Entra-native APIs when authority permits. |
| Exchange hybrid/on-prem management | `New-RemoteMailbox` and Exchange snap-in | CCCS653 dependency to validate and support. | Exchange Online-native capability or explicit removal decision. |
| Local Windows host and PowerShell | process execution, scripts, filesystem demo | Harden and reduce exposure. | Eliminate from primary business path; Azure Function only for a proven narrow need. |
| Windows Negotiate and `Authorization:AllowedUsers` | browser authentication and allow-list | Retain while current process requires it, with security controls. | Replace with Entra auth and application roles/groups. |
| appsettings.json | PowerShell path/policy and domain user allow-list | Manage safely on CCCS923. | App Service configuration; Key Vault only for secrets that cannot be removed. |
| GitHub workflow and Deploy.ps1 | build artifact then manual IIS deployment | Retain controlled CI/package/deploy process. | Replace deployment with Azure OIDC CI/CD. |

## New User Decomposition

`NewUser.ps1` currently combines separate outcomes:

1. Validate/create an identity and apply local naming rules: first/last name, display name, SAM account, UPN, alias, and remote routing address.
2. Generate a random plaintext password and return it to the operator.
3. Create a remote mailbox in a hard-coded on-premises OU.
4. Verify the AD object exists.
5. Populate office, address, country, title, department, company, and manager attributes.
6. Add a fixed licensing-related group membership.
7. Return streamed success/failure text.

Tactical treatment is to retain the current supported hybrid process while validating dependencies, business behavior, security, and approved test identities. A strategic design should split these into typed commands and policies. The hard-coded OU, remote routing address, Exchange snap-in, SAM-account format, and fixed group require tenant/business review. Generating and returning a plaintext password must not be assumed acceptable; preferred onboarding may instead use Entra Temporary Access Pass, invitation/onboarding, passwordless registration, or an approved secure credential-delivery process.

## Current Product Classification
- Production-intent: dedicated new user, search/details, deletion marking/listing/deletion, notes.
- Tactical transitional platform: CCCS923/Nutanix, CCCS653/hybrid Exchange, Windows authentication allow-list, process-account privilege, AD attributes as workflow state, generic runner.
- Strategic successor direction: Entra authentication, Graph/M365 APIs, managed identity, typed operations, Azure hosting.
- Known demo/diagnostic scripts have been removed from the operational repository. The tactical Generic Script List remains transitional and is narrowed by explicit catalogue review; it remains a strategic removal candidate once typed business operations replace it.

## Two-Horizon Roadmap

### Horizon 1 — Tactical Delivery
1. Functional inventory and proof.
2. Local read-only integration testing.
3. Close functional gaps.
4. Secure operational scripts.
5. Remove demo/test exposure.
6. Improve audit and logging.
7. Upgrade .NET 9 to .NET 10 LTS.
8. CCCS923 readiness, including CCCS653/hybrid dependency validation.
9. Deploy to CCCS923.
10. User Admin UAT.
11. Controlled proof of New User, mark, unmark, and delete with approved test identities.
12. Production acceptance and runbook.

### Horizon 2 — Strategic Successor
1. Reassess when the identity/infrastructure roadmap changes.
2. Entra authentication and authorization.
3. Graph and managed identity.
4. Typed business operations.
5. Azure App Service/Azure-native deployment.
6. Remove hybrid execution dependency.
7. Assess identity resolution/master-data opportunity.

### Identity Resolution & Organisational Master Data
This future opportunity is not current scope. Assess probabilistic entity resolution, potentially using Splink; duplicate/stale identity detection; canonical `Person`, `Employment`, `Position`, `Org Unit`, and `Digital Identity` entities; HR, ERP, and Entra source integration; human-reviewed remediation; and pre-provisioning duplicate detection.
