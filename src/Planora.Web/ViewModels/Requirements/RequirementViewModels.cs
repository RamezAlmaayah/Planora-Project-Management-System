using System.ComponentModel.DataAnnotations;
using Planora.Application.Common.Requirements;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Requirements;

public sealed class RequirementIndexViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool CanManage { get; init; }
    public string? Search { get; init; }
    public RequirementType? Type { get; init; }
    public RequirementStatus? Status { get; init; }
    public PriorityLevel? Priority { get; init; }
    public IReadOnlyList<RequirementSummary> Requirements { get; init; } = [];
}

public sealed class CreateRequirementViewModel
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(RequirementType))]
    public RequirementType Type { get; set; } = RequirementType.Functional;

    [EnumDataType(typeof(PriorityLevel))]
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    [StringLength(3000)]
    public string? Rationale { get; set; }

    [StringLength(2000)]
    public string? Preconditions { get; set; }

    [StringLength(2000)]
    public string? ExceptionScenario { get; set; }

    [EnumDataType(typeof(NfrCategory))]
    public NfrCategory? NfrCategory { get; set; }

    public List<int> RelatedFunctionalRequirementIds { get; set; } = [];
    public IReadOnlyList<RequirementOption> FunctionalRequirementOptions { get; set; } = [];
    public string FunctionalIdentifierPreview { get; set; } = "FR-001";
    public string NonFunctionalIdentifierPreview { get; set; } = "NFR-001";
}

public sealed class EditRequirementViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public RequirementType Type { get; set; }
    public RequirementStatus ExpectedStatus { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(PriorityLevel))]
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    [StringLength(3000)]
    public string? Rationale { get; set; }

    [StringLength(2000)]
    public string? Preconditions { get; set; }

    [StringLength(2000)]
    public string? ExceptionScenario { get; set; }

    [EnumDataType(typeof(NfrCategory))]
    public NfrCategory? NfrCategory { get; set; }

    public List<int> RelatedFunctionalRequirementIds { get; set; } = [];
    public IReadOnlyList<RequirementOption> FunctionalRequirementOptions { get; set; } = [];
}

public sealed class RequirementDetailsViewModel
{
    public RequirementDetails Requirement { get; init; } = new();
    public bool IsReadOnly { get; init; }
    public bool CanManage { get; init; }
    public bool CanEdit { get; init; }
    public IReadOnlyList<RequirementStatus> AllowedTargets { get; init; } = [];
    public IReadOnlyList<RequirementOption> DependencyOptions { get; init; } = [];
}

public sealed class TransitionRequirementViewModel
{
    public int ProjectId { get; set; }
    public int RequirementId { get; set; }
    public RequirementStatus ExpectedStatus { get; set; }
    public RequirementStatus TargetStatus { get; set; }
}

public sealed class AddRequirementDependencyViewModel
{
    public int ProjectId { get; set; }
    public int RequirementId { get; set; }

    [Range(1, int.MaxValue)]
    public int DependsOnRequirementId { get; set; }
}

public sealed class RemoveRequirementDependencyViewModel
{
    public int ProjectId { get; set; }
    public int RequirementId { get; set; }
    public int DependencyId { get; set; }
}

public sealed class AddRequirementTraceViewModel
{
    public int ProjectId { get; set; }
    public int RequirementId { get; set; }

    [EnumDataType(typeof(RequirementTraceStage))]
    public RequirementTraceStage Stage { get; set; } = RequirementTraceStage.Design;

    [Required, StringLength(100)]
    public string ReferenceCode { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
}

public sealed class RemoveRequirementTraceViewModel
{
    public int ProjectId { get; set; }
    public int RequirementId { get; set; }
    public int TraceId { get; set; }
}

public sealed class RequirementTraceabilityViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public IReadOnlyList<RequirementTraceabilityItem> Requirements { get; init; } = [];
}
