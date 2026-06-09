using System.ComponentModel.DataAnnotations;

namespace PSScriptWebApp.Models;

public class UserSearchViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Search")]
    public string Search { get; set; } = string.Empty;

    public List<UserSearchResultItem> Results { get; set; } = new();
    public string? Error { get; set; }
}

public class UserSearchResultItem
{
    public string Name { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string? UserPrincipalName { get; set; }
    public bool? Enabled { get; set; }
}