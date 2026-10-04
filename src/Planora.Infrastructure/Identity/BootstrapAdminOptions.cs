namespace Planora.Infrastructure.Identity;

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public bool Enabled { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = "Planora Administrator";

    public string Password { get; set; } = string.Empty;
}