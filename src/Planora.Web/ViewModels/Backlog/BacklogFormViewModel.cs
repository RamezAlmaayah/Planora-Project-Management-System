using System.ComponentModel.DataAnnotations;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Backlog;

public sealed class BacklogFormViewModel
{
    public int? Id { get; set; }

    public int ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(3000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public PriorityLevel Priority { get; set; }
        = PriorityLevel.Medium;

    [Required]
    public BacklogItemStatus Status { get; set; }
        = BacklogItemStatus.Draft;

    public bool IsAssignedToSprint { get; set; }
}
