using Planora.Application.Common.Identity;

namespace Planora.Web.ViewModels.AdminUsers;

public sealed class AdminUserIndexViewModel
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public AdminUserListResult Result { get; set; } = new();
}
