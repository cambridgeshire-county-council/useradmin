<#
.SYNOPSIS
  Example Script
.DESCRIPTION
  To demonstrate how the application runs PowerShell scripts
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory=$true,HelpMessage="The users first name")][string]$first,
    [Parameter(Mandatory=$true,HelpMessage="The users last name")][string]$last
)


Write-Output "*** Starting Account Creation Script ***"
Write-Output "Hello $first $last!"

