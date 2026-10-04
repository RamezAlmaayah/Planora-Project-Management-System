using Planora.Domain.Enums;

namespace Planora.Domain.Entities;


public class ProjectMember
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ProjectMemberRole Role { get; set; }

    public DateTime JoinedAt { get; set; }

    public Project Project { get; set; } = null!;
}