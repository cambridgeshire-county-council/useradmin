param(
    [Parameter(Mandatory = $true)]
    [string]$SamAccountName
)

# Confirm the user exists before attempting deletion
$User = Get-ADUser -Filter "SamAccountName -eq '$SamAccountName'" -Properties DisplayName

if ($null -eq $User) {
    Write-Output "User '$SamAccountName' not found."
    exit 1
}

Write-Output "Found user: $($User.DisplayName) ($SamAccountName)"

Remove-ADUser -Identity $SamAccountName -Confirm:$false

Write-Output "User '$SamAccountName' has been deleted successfully."
