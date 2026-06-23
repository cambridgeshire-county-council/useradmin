param(
    [Parameter(Mandatory = $true)]
    [string]$FirstName,

    [Parameter(Mandatory = $true)]
    [string]$LastName,

    [Parameter(Mandatory = $false)]
    [string]$DisplayName = "",

    [Parameter(Mandatory = $true)]
    [string]$UserPrincipalName,

    [Parameter(Mandatory = $true)]
    [string]$SamAccountName,

    [Parameter(Mandatory = $false)]
    [string]$Office = "",

    [Parameter(Mandatory = $false)]
    [string]$StreetAddress = "",

    [Parameter(Mandatory = $false)]
    [string]$POBox = "",

    [Parameter(Mandatory = $false)]
    [string]$PostalCode = "",

    [Parameter(Mandatory = $false)]
    [string]$Country = "UK",

    [Parameter(Mandatory = $false)]
    [string]$JobTitle = "",

    [Parameter(Mandatory = $false)]
    [string]$Department = "",

    [Parameter(Mandatory = $false)]
    [string]$Company = "Cambridgeshire County Council",

    [Parameter(Mandatory = $false)]
    [string]$Manager = ""
)

# Use provided DisplayName or derive from first/last name
if ([string]::IsNullOrWhiteSpace($DisplayName)) {
    $DisplayName = "$FirstName $LastName"
}

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
    $SetParams = @{
        Identity    = $SamAccountName
        DisplayName = $DisplayName
        ErrorAction = 'Stop'
    }

    if (-not [string]::IsNullOrWhiteSpace($Office))        { $SetParams['Office']        = $Office }
    if (-not [string]::IsNullOrWhiteSpace($StreetAddress)) { $SetParams['StreetAddress'] = $StreetAddress }
    if (-not [string]::IsNullOrWhiteSpace($POBox))         { $SetParams['POBox']         = $POBox }
    if (-not [string]::IsNullOrWhiteSpace($PostalCode))    { $SetParams['PostalCode']    = $PostalCode }
    if (-not [string]::IsNullOrWhiteSpace($Country))       { $SetParams['Country']       = $Country }
    if (-not [string]::IsNullOrWhiteSpace($JobTitle))      { $SetParams['Title']         = $JobTitle }
    if (-not [string]::IsNullOrWhiteSpace($Department))    { $SetParams['Department']    = $Department }
    if (-not [string]::IsNullOrWhiteSpace($Company))       { $SetParams['Company']       = $Company }

    Set-ADUser @SetParams

    if (-not [string]::IsNullOrWhiteSpace($Manager)) {
        Set-ADUser -Identity $SamAccountName -Manager $Manager -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to update AD user properties: $_"
    exit 1
}

Write-Output "Account created successfully."
Write-Output "Generated Password: $Password"
