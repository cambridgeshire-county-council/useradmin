param()

$results = Get-ADUser -Filter "extensionAttribute3 -like '*'" `
    -Properties UserPrincipalName, Enabled, extensionAttribute3 |
    Select-Object Name, SamAccountName, UserPrincipalName, Enabled, extensionAttribute3

Write-Output (@($results) | ConvertTo-Json -Depth 4)
