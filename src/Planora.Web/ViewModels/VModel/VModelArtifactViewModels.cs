using System.ComponentModel.DataAnnotations;
using Planora.Application.Common.VModel;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.VModel;

public sealed class DesignArtifactIndexViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool CanManage { get; init; }
    public string? Search { get; init; }
    public IReadOnlyList<DesignArtifactSummary> Artifacts { get; init; } = [];
}

public sealed class DesignArtifactDetailsViewModel
{
    public DesignArtifactDetails Artifact { get; init; } = new();
    public bool IsReadOnly { get; init; }
    public bool CanEdit { get; init; }
}

public sealed class DesignArtifactFormViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
    [EnumDataType(typeof(DesignArtifactType))]
    public DesignArtifactType Type { get; set; } = DesignArtifactType.SystemDesign;
    public List<int> RequirementIds { get; set; } = [];
    public IReadOnlyList<RequirementArtifactOption> RequirementOptions { get; set; } = [];
}

public sealed class ImplementationArtifactIndexViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool CanCreate { get; init; }
    public string? Search { get; init; }
    public IReadOnlyList<ImplementationArtifactSummary> Artifacts { get; init; } = [];
}

public sealed class ImplementationArtifactDetailsViewModel
{
    public ImplementationArtifactDetails Artifact { get; init; } = new();
    public bool IsReadOnly { get; init; }
    public bool CanEdit { get; init; }
}

public sealed class ImplementationArtifactFormViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
    [EnumDataType(typeof(ImplementationArtifactType))]
    public ImplementationArtifactType Type { get; set; } = ImplementationArtifactType.Module;
    [StringLength(500)] public string? SourceReference { get; set; }
    public List<int> DesignArtifactIds { get; set; } = [];
    public IReadOnlyList<DesignArtifactOption> DesignOptions { get; set; } = [];
}
