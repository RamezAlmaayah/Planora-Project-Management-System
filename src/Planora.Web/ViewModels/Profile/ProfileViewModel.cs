using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Planora.Web.ViewModels.Profile;

public sealed class ProfileViewModel
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? ProfileImagePath { get; set; }

    public IFormFile? ProfileImage { get; set; }
}
