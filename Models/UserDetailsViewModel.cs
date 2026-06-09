namespace PSScriptWebApp.Models;

public class UserDetailsViewModel
{
    public string SamAccountName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool? Enabled { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public string? Mail { get; set; }
    public string? Error { get; set; }
}