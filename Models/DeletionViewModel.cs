using System.ComponentModel.DataAnnotations;

namespace PSScriptWebApp.Models;

public class DeletionSearchViewModel
{
    [StringLength(100)]
    [Display(Name = "Search")]
    public string? Search { get; set; }

    public List<DeletionSearchResultItem> Results { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? Error { get; set; }
}

public class DeletionSearchResultItem
{
    public string Name { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string? UserPrincipalName { get; set; }
    public bool? Enabled { get; set; }
    public string? ExtensionAttribute2 { get; set; }
    public bool IsMarked => !string.IsNullOrWhiteSpace(ExtensionAttribute2);
}

public class MarkedForDeletionViewModel
{
    public string Filter { get; set; } = "all";
    public List<MarkedUserItem> Users { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? Error { get; set; }
}

public class MarkedUserItem
{
    public string Name { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string? UserPrincipalName { get; set; }
    public bool? Enabled { get; set; }
    public string? ExtensionAttribute2 { get; set; }
    public DateTime? MarkedDate =>
        DateTime.TryParse(ExtensionAttribute2, out var d) ? d : null;
}
