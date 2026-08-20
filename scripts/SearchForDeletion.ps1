param(
    [Parameter(Mandatory = $true)]
    [string]$search
)

$results = Get-ADUser -Filter "Name -like '*$search*'" `
    -Properties UserPrincipalName, Enabled, extensionAttribute3 |
    Select-Object Name, SamAccountName, UserPrincipalName, Enabled, extensionAttribute3

Write-Output (@($results) | ConvertTo-Json -Depth 4)
