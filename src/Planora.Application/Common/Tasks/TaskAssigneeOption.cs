using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class TaskAssigneeOption
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ProjectMemberRole Role { get; init; }
}
