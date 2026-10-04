using System.ComponentModel.DataAnnotations;

namespace Planora.Web.ViewModels.AdminUsers;

public sealed class CreateAdminUserViewModel
{
    [Required, StringLength(150, MinimumLength = 2)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Initial Password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Global Role")]
    public string GlobalRole { get; set; } = string.Empty;
}
