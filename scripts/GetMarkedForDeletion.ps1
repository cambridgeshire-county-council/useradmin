param()

$results = Get-ADUser -Filter "extensionAttribute2 -like '*'" `
    -Properties UserPrincipalName, Enabled, extensionAttribute2 |
    Select-Object Name, SamAccountName, UserPrincipalName, Enabled, extensionAttribute2

Write-Output (@($results) | ConvertTo-Json -Depth 4)
