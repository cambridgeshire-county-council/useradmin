param(
    [Parameter(Mandatory = $true)]
    [string]$search
)

$results = Get-ADUser -Filter "Name -like '*$search*'" `
    -Properties UserPrincipalName, Enabled, extensionAttribute2 |
    Select-Object Name, SamAccountName, UserPrincipalName, Enabled, extensionAttribute2

Write-Output (@($results) | ConvertTo-Json -Depth 4)
