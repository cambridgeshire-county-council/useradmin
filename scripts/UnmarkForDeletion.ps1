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

$replace = @{}
$clear = @('extensionAttribute2')

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $clear += 'info'
} else {
    $replace['info'] = $Notes
}

if ($replace.Count -gt 0) {
    Set-ADUser -Identity $SamAccountName -Clear $clear -Replace $replace
} else {
    Set-ADUser -Identity $SamAccountName -Clear $clear
}

Write-Output "User '$SamAccountName' has been unmarked for deletion."
