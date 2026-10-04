using System.ComponentModel.DataAnnotations;
using Planora.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Planora.Web.ViewModels.Tasks;

public sealed class QaReviewTaskViewModel
{
    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }
    [Range(1, int.MaxValue)]
    public int SprintId { get; set; }
    [Range(1, int.MaxValue)]
    public int TaskId { get; set; }
    [EnumDataType(typeof(QaReviewResult))]
    public QaReviewResult Result { get; set; }
    [StringLength(3000)]
    public string? Notes { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
}
