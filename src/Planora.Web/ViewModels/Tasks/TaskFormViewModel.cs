using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Planora.Application.Common.Tasks;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Tasks;

public sealed class TaskFormViewModel
{
    public int Id { get; set; }
    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }
    [Range(1, int.MaxValue)]
    public int SprintId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Select a sprint backlog item.")]
    [Display(Name = "Backlog item")]
    public int SprintBacklogItemId { get; set; }
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;
    [Required, StringLength(3000)]
    public string Description { get; set; } = string.Empty;
    [EnumDataType(typeof(PriorityLevel))]
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    [StringLength(450), Display(Name = "Assignee")]
    public string? AssignedUserId { get; set; }
    [DataType(DataType.Date)]
    public DateTime? Deadline { get; set; }
    [BindNever, ValidateNever]
    public string ProjectName { get; set; } = string.Empty;
    [BindNever, ValidateNever]
    public string SprintName { get; set; } = string.Empty;
    [BindNever, ValidateNever]
    public bool CanSave { get; set; }
    [BindNever, ValidateNever]
    public IReadOnlyList<TaskBacklogOption> BacklogOptions { get; set; } = Array.Empty<TaskBacklogOption>();
    [BindNever, ValidateNever]
    public IReadOnlyList<TaskAssigneeOption> AssigneeOptions { get; set; } = Array.Empty<TaskAssigneeOption>();
}
