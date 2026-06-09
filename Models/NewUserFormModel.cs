using System.ComponentModel.DataAnnotations;

namespace PSScriptWebApp.Models;

public class NewUserFormModel
{
    [Required]
    [StringLength(100)]
    [RegularExpression(@"^[A-Za-z][A-Za-z\s\-']*$", ErrorMessage = "First name can only contain letters, spaces, apostrophes, and hyphens.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^[A-Za-z][A-Za-z\s\-']*$", ErrorMessage = "Last name can only contain letters, spaces, apostrophes, and hyphens.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "User Principal Name (Email Address)")]
    public string UserPrincipalName { get; set; } = string.Empty;

    [Required]
    [StringLength(5, MinimumLength = 5)]
    [RegularExpression(@"^[A-Za-z]{2}\d{3}$", ErrorMessage = "SAM account name must be in the format LLNNN (for example AB123).")]
    [Display(Name = "SAM Account Name")]
    public string SamAccountName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    [Display(Name = "Extension Attribute 2")]
    public string ExtensionAttribute2Value { get; set; } = string.Empty;

    public bool? ExecutionSucceeded { get; set; }
    public string? ExecutionOutput { get; set; }
    public string? ExecutionError { get; set; }
}