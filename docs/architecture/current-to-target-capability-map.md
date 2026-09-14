# Current-to-Target Capability Map

## Assessment Scope
This map records current implementation evidence only. A current PowerShell or AD dependency does not imply hybrid infrastructure is required in the target state.

| Capability | Current entry and implementation | Inputs / outputs | Reads and mutations | Current dependencies and risk | Target classification |
| --- | --- | --- | --- | --- | --- |
| New User | `Users/New`; `UsersController.New` and `StreamNew`; `NewUser.ps1` | Identity, UPN, SAM account, address, organization, manager; streamed status and generated password | Creates remote mailbox/user; updates attributes; assigns manager and group membership | AD DS, Exchange management snap-in, local PowerShell/domain host; privileged child process; plaintext password disclosure | E: process decisions, then A Graph/Entra-native, B Exchange Online-native, C Azure boundary as needed |
| Search Users | `Users/Search`; `Search`; `Search.ps1` | Name fragment; JSON name/SAM/UPN/status results | `Get-ADUser` only | AD DS, PowerShell; broad directory read through host identity | A: Graph/Entra-native |
| User Details | search result link to `Users/Details`; `GetUser.ps1` | SAM account; profile and enabled-state JSON | `Get-ADUser` only | AD DS, PowerShell | A: Graph/Entra-native |
| Mark for Deletion search | `Users/MarkForDeletion`; `SearchForDeletion.ps1` | Name fragment; users and `extensionAttribute3` state | `Get-ADUser` only | AD DS custom attribute; unclear lifecycle semantics | E: define lifecycle, then A or D |
| Mark User for Deletion | modal on `Users/MarkForDeletion`; `MarkUser`; `MarkForDeletion.ps1` | SAM account, optional notes; status message | `Set-ADUser` date marker and `info` note | AD DS write; process identity has direct write privilege | E: define retention/workflow, then A or D |
| Unmark User | same modal; `UnmarkUser`; `UnmarkForDeletion.ps1` | SAM account, optional notes; status message | clears AD marker and updates/clears note | AD DS write; weak audit trail | E: define lifecycle/audit, then A or D |
| Marked-for-Deletion list | `Users/MarkedForDeletion`; `GetMarkedForDeletion.ps1` | all/week/month filter; user list | `Get-ADUser` only | AD DS custom attribute and application-side date filter | E: define lifecycle, then A or D |
| Delete User | Delete All Displayed form; `DeleteMarked`; `DeleteUser.ps1` | displayed SAM accounts; count status | `Remove-ADUser -Confirm:$false` per supplied user | irreversible destructive AD operation; bulk action and limited audit/approval | E: explicit business, retention, recovery and approval decision; then A/B if supported |
| User notes | modal fetch `GetUserNotes`; `GetUserNotes.ps1` | SAM account; JSON note | `Get-ADUser` `info` only; mark/unmark write same field | AD attribute used as operational note store | D or A after data ownership/retention decision |
| Generic Script List | `Scripts/Index`, `Details`, `Execute`, `Stream`; `PowerShellService` | parsed arbitrary checked-in script parameters; output/SSE | Executes allow-listed file under host identity | generic privileged code execution; even after POST/CSRF/path/parameter hardening it remains a broad authority surface | F: remove after typed capabilities replace required journeys |
| Basic.ps1 | Script List | no input; version output | none external | local PowerShell/runtime diagnostic | F: demo/diagnostic removal candidate |
| ExampleScript.ps1 | Script List | first/last; greeting | none external | demo wording references account creation but only prints | F: demo removal candidate |
| TimeDemo.ps1 | Script List | no input; time output after delay | none external | local PowerShell runtime | F: demo removal candidate |
| dir.ps1 | Script List | no input; local directory listing | reads application-host filesystem | leaks host filesystem context | F: diagnostic removal candidate |
| Error.ps1 | Script List | no input | intended error path; contains `Write-Ootput` typo | broken/demo behavior | F: removal candidate |

## Current Cross-Cutting Dependencies

| Dependency | Current use | Target treatment |
| --- | --- | --- |
| Active Directory Domain Services | user lookup, profile, state marker, notes, deletion | Replace business outcomes with Graph/Entra-native APIs where tenant authority supports them. |
| Exchange hybrid/on-prem management | `New-RemoteMailbox` and Exchange snap-in | Determine Exchange Online provisioning outcome; use supported Exchange Online-native capability only if Graph cannot satisfy it. |
| Local Windows host and PowerShell | process execution, scripts, filesystem demo | Eliminate from primary business path. Azure Function is optional only for a proven privileged cloud operation. |
| Windows Negotiate and `Authorization:AllowedUsers` | browser authentication and allow-list | Transitional debt; replace with Entra auth and application roles/groups. |
| appsettings.json | PowerShell path/policy and domain user allow-list | Azure App Service configuration; Key Vault only for secrets that cannot be removed. |
| GitHub workflow and Deploy.ps1 | build artifact then manual IIS deployment | Retain CI validation; replace IIS artifact deployment with OIDC-authenticated Azure deployment in a later approved increment. |

## New User Decomposition

`NewUser.ps1` currently combines separate outcomes:

1. Validate/create an identity and apply local naming rules: first/last name, display name, SAM account, UPN, alias, and remote routing address.
2. Generate a random plaintext password and return it to the operator.
3. Create a remote mailbox in a hard-coded on-premises OU.
4. Verify the AD object exists.
5. Populate office, address, country, title, department, company, and manager attributes.
6. Add a fixed licensing-related group membership.
7. Return streamed success/failure text.

A cloud-native design should split these into typed commands and policies. The hard-coded OU, remote routing address, Exchange snap-in, SAM-account format, and fixed group require tenant/business review. Generating and returning a plaintext password must not be assumed acceptable; preferred onboarding may instead use Entra Temporary Access Pass, invitation/onboarding, passwordless registration, or an approved secure credential-delivery process.

## Current Product Classification
- Production-intent: dedicated new user, search/details, deletion marking/listing/deletion, notes.
- Transitional technical debt: Windows authentication allow-list, process-account privilege, AD attributes as workflow state, generic runner.
- Demo/diagnostic/obsolete candidates: Basic, ExampleScript, TimeDemo, `dir`, Error, and the generic Script List once business journeys are replaced.
