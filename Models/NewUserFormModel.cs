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
    [StringLength(256)]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

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

    [StringLength(256)]
    [Display(Name = "Office")]
    public string? Office { get; set; }

    [StringLength(512)]
    [Display(Name = "Street Address")]
    public string? StreetAddress { get; set; }

    [StringLength(64)]
    [Display(Name = "PO Box")]
    public string? POBox { get; set; }

    [StringLength(20)]
    [Display(Name = "Postcode")]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    [Display(Name = "Country")]
    public string Country { get; set; } = "UK";

    [StringLength(256)]
    [Display(Name = "Job Title")]
    public string? JobTitle { get; set; }

    [StringLength(256)]
    [Display(Name = "Department")]
    public string? Department { get; set; }

    [StringLength(256)]
    [Display(Name = "Company")]
    public string Company { get; set; } = "Cambridgeshire County Council";

    [StringLength(256)]
    [Display(Name = "Manager (SAM Account Name)")]
    public string? Manager { get; set; }

    public bool? ExecutionSucceeded { get; set; }
    public string? ExecutionOutput { get; set; }
    public string? ExecutionError { get; set; }
}
