param(
    [Parameter(Mandatory = $true)]
    [string]$SamAccountName
)

# Confirm the user exists and is currently marked before attempting deletion
$User = Get-ADUser -Filter "SamAccountName -eq '$SamAccountName'" -Properties extensionAttribute3

if ($null -eq $User) {
    Write-Output "The requested account was not found."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($User.extensionAttribute3)) {
    Write-Output "The requested account is not marked for deletion."
    exit 1
}

Remove-ADUser -Identity $SamAccountName -Confirm:$false

Write-Output "The marked account was deleted successfully."
