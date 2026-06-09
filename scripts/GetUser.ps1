[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "SAM account name")]
    [string]$SamAccountName
)

$user = Get-ADUser -Identity $SamAccountName -Properties GivenName, Surname, UserPrincipalName, DisplayName, Description, Enabled, Department, Title, Mail |
    Select-Object SamAccountName, Name, GivenName, Surname, UserPrincipalName, DisplayName, Description, Enabled, Department, Title, Mail

Write-Output ($user | ConvertTo-Json -Depth 4)