param(
    [Parameter(Mandatory = $true)]
    [string]$FirstName,

    [Parameter(Mandatory = $true)]
    [string]$LastName,

    [Parameter(Mandatory = $true)]
    [string]$UserPrincipalName,

    [Parameter(Mandatory = $true)]
    [string]$SamAccountName,

    [Parameter(Mandatory = $true)]
    [string]$Description,

    [Parameter(Mandatory = $true)]
    [string]$ExtensionAttribute2Value
)

# Calculated fields
$DisplayName          = "$FirstName $LastName"
$Alias                = "$($FirstName.ToLower()).$($LastName.ToLower())"
$RemoteRoutingAddress = "$Alias@cccandpcc.mail.microsoft.com"
$OU                   = "OU=Users,OU=Accounts,OU=CCC365,DC=CCC,DC=Cambridgeshire,DC=gov,DC=uk"

# Generate a random password (16 chars: upper, lower, digits, special)
$PasswordChars  = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%^&*'
$Password       = -join ((1..16) | ForEach-Object { $PasswordChars[(Get-Random -Maximum $PasswordChars.Length)] })

Add-PSSnapin Microsoft.Exchange.Management.PowerShell.SnapIn

$SecurePassword = $Password | ConvertTo-SecureString -AsPlainText -Force

try {
    New-RemoteMailbox -Name $DisplayName `
                      -Password $SecurePassword `
                      -UserPrincipalName $UserPrincipalName `
                      -SamAccountName $SamAccountName `
                      -Alias $Alias `
                      -RemoteRoutingAddress $RemoteRoutingAddress `
                      -OnPremisesOrganizationalUnit $OU `
                      -ErrorAction Stop
} catch {
    Write-Error "Failed to create remote mailbox: $_"
    exit 1
}

# Verify the AD user was actually created before proceeding
$User = Get-ADUser -Identity $SamAccountName -ErrorAction SilentlyContinue
if (-not $User) {
    Write-Error "User '$SamAccountName' was not found in Active Directory after mailbox creation. Aborting."
    exit 1
}

try {
    Get-ADUser $SamAccountName -Properties *
    Set-ADUser -Identity $SamAccountName -DisplayName $DisplayName -Description $Description -ErrorAction Stop
    Set-ADUser -Identity $SamAccountName -Replace @{extensionAttribute2 = $ExtensionAttribute2Value} -ErrorAction Stop
    Set-ADUser -Instance $User -ErrorAction Stop
} catch {
    Write-Error "Failed to update AD user properties: $_"
    exit 1
}

Write-Output "Account created successfully."
Write-Output "Generated Password: $Password"