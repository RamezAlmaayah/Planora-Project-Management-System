using System.ComponentModel.DataAnnotations;

namespace Planora.Web.ViewModels.Sprints;

public sealed class SprintFormViewModel
{
    public int? Id { get; set; }
    public int ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Goal { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}
