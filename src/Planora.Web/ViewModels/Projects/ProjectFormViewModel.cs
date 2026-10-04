using System.ComponentModel.DataAnnotations;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Projects;

public sealed class ProjectFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(
        150,
        MinimumLength = 3,
        ErrorMessage = "Project name must be between 3 and 150 characters.")]
    [Display(Name = "Project Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(
        1000,
        ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ProjectMethodology Methodology { get; set; }

    [Required]
    public ProjectStatus Status { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; }

    public bool IsEditMode =>
        Id > 0;
}