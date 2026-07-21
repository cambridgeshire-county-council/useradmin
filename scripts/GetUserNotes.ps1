param(
    [Parameter(Mandatory = $true)]
    [string]$SamAccountName
)

$user = Get-ADUser -Filter "SamAccountName -eq '$SamAccountName'" -Properties info

if ($null -eq $user) {
    Write-Error "User '$SamAccountName' not found."
    exit 1
}

Write-Output (@{ SamAccountName = $SamAccountName; Notes = $user.info } | ConvertTo-Json)
