param(
    [Parameter(Mandatory = $true)]
    [string]$SamAccountName
)

$user = Get-ADUser -Filter "SamAccountName -eq '$SamAccountName'" -Properties extensionAttribute2

if ($null -eq $user) {
    Write-Output "User '$SamAccountName' not found."
    exit 1
}

Set-ADUser -Identity $SamAccountName -Clear extensionAttribute2

Write-Output "User '$SamAccountName' has been unmarked for deletion."
