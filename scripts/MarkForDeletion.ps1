param(
    [Parameter(Mandatory = $true)]
    [string]$SamAccountName,

    [Parameter(Mandatory = $false)]
    [string]$Notes
)

$user = Get-ADUser -Filter "SamAccountName -eq '$SamAccountName'" -Properties extensionAttribute2

if ($null -eq $user) {
    Write-Output "User '$SamAccountName' not found."
    exit 1
}

$replace = @{ extensionAttribute2 = (Get-Date -Format 'yyyy-MM-dd') }
$clear = @()

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $clear += 'info'
} else {
    $replace['info'] = $Notes
}

if ($clear.Count -gt 0) {
    Set-ADUser -Identity $SamAccountName -Replace $replace -Clear $clear
} else {
    Set-ADUser -Identity $SamAccountName -Replace $replace
}

Write-Output "User '$SamAccountName' has been marked for deletion."
