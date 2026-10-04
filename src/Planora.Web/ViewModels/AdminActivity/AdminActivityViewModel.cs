namespace Planora.Web.ViewModels.AdminActivity;

public sealed class AdminActivityViewModel
{
    public string? UserId { get; set; }
    public int? ProjectId { get; set; }
    public string? Action { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ActivityEntry> Entries { get; set; } = [];
    public IReadOnlyList<ActivityUserOption> Users { get; set; } = [];
    public IReadOnlyList<ActivityProjectOption> Projects { get; set; } = [];
    public IReadOnlyList<string> Actions { get; set; } = [];
    public IReadOnlyList<PresenceRow> Presence { get; set; } = [];
}

public sealed record ActivityEntry(int Id, string Actor, string Action, string ResourceType,
    string ResourceId, string? ProjectName, DateTime CreatedAt);
public sealed record ActivityUserOption(string Id, string Name);
public sealed record ActivityProjectOption(int Id, string Name);
public sealed record PresenceRow(string Name, string Role, bool Online, DateTime? LastSeenAt);
