using Microsoft.AspNetCore.Identity;

namespace Planora.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public string? ProfileImagePath { get; set; }

    public bool IsDisabled { get; set; }

    public DateTime? DisabledAt { get; set; }
}
