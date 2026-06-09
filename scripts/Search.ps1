[CmdletBinding()]
param (
    [Parameter(Mandatory=$true,HelpMessage="Search")][string]$search
)

$results = Get-ADUser -Filter "Name -like '*$search*'" -Properties UserPrincipalName, Enabled |
    Select-Object Name, SamAccountName, UserPrincipalName, Enabled

# Always emit an array so the app can parse consistently for 0, 1, or many results.
Write-Output (@($results) | ConvertTo-Json -Depth 4)