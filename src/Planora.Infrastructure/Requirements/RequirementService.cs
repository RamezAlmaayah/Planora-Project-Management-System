using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Requirements;
using Planora.Application.Common.Requirements;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Requirements;

public sealed class RequirementService : IRequirementService
{
    private const int TitleMaximumLength = 200;
    private const int DescriptionMaximumLength = 4000;
    private const int RationaleMaximumLength = 3000;
    private const int PreconditionsMaximumLength = 2000;
    private const int ExceptionScenarioMaximumLength = 2000;
    private const int ReferenceCodeMaximumLength = 100;
    private const int TraceDescriptionMaximumLength = 2000;
    private const int IdentifierRetryLimit = 3;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim>
        IdentifierLocks = new(StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;

    public RequirementService(ApplicationDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RequirementSummary>> GetProjectRequirementsAsync(
        int projectId,
        string? search = null,
        RequirementFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > TitleMaximumLength)
            normalizedSearch = normalizedSearch[..TitleMaximumLength];

        IQueryable<Requirement> query = _context.Requirements.AsNoTracking()
            .Where(requirement => requirement.ProjectId == projectId);

        if (normalizedSearch.Length > 0)
            query = query.Where(requirement =>
                requirement.Identifier.Contains(normalizedSearch) ||
                requirement.Title.Contains(normalizedSearch) ||
                requirement.Description.Contains(normalizedSearch));
        if (filter?.Type is not null)
            query = query.Where(requirement => requirement.Type == filter.Type);
        if (filter?.Status is not null)
            query = query.Where(requirement => requirement.Status == filter.Status);
        if (filter?.Priority is not null)
            query = query.Where(requirement => requirement.Priority == filter.Priority);

        return await query
            .OrderBy(requirement => requirement.Identifier)
            .Select(requirement => new RequirementSummary
            {
                Id = requirement.Id,
                ProjectId = requirement.ProjectId,
                Identifier = requirement.Identifier,
                Title = requirement.Title,
                Type = requirement.Type,
                NfrCategory = requirement.NfrCategory,
                Priority = requirement.Priority,
                Status = requirement.Status,
                CreatorName = _context.Users
                    .Where(user => user.Id == requirement.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? requirement.CreatedByUserId,
                CreatedAt = requirement.CreatedAt,
                UpdatedAt = requirement.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RequirementDetails?> GetByIdAsync(
        int projectId,
        int requirementId,
        CancellationToken cancellationToken = default)
    {
        RequirementDetails? details = await _context.Requirements.AsNoTracking()
            .Where(requirement =>
                requirement.Id == requirementId &&
                requirement.ProjectId == projectId)
            .Select(requirement => new RequirementDetails
            {
                Id = requirement.Id,
                ProjectId = requirement.ProjectId,
                ProjectName = requirement.Project.Name,
                Identifier = requirement.Identifier,
                Title = requirement.Title,
                Description = requirement.Description,
                Type = requirement.Type,
                NfrCategory = requirement.NfrCategory,
                Priority = requirement.Priority,
                Rationale = requirement.Rationale,
                Preconditions = requirement.Preconditions,
                ExceptionScenario = requirement.ExceptionScenario,
                Status = requirement.Status,
                CreatorName = _context.Users
                    .Where(user => user.Id == requirement.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? requirement.CreatedByUserId,
                UpdatedByName = requirement.UpdatedByUserId == null
                    ? null
                    : _context.Users
                        .Where(user => user.Id == requirement.UpdatedByUserId)
                        .Select(user => user.FullName != string.Empty
                            ? user.FullName
                            : user.UserName ?? user.Email ?? user.Id)
                        .FirstOrDefault(),
                CreatedAt = requirement.CreatedAt,
                UpdatedAt = requirement.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (details is null)
            return null;

        details.Dependencies = await _context.RequirementDependencies.AsNoTracking()
            .Where(link =>
                link.RequirementId == requirementId &&
                link.Requirement.ProjectId == projectId &&
                link.DependsOnRequirement.ProjectId == projectId)
            .OrderBy(link => link.DependsOnRequirement.Identifier)
            .Select(link => new RequirementDependencySummary
            {
                LinkId = link.Id,
                RequirementId = link.DependsOnRequirementId,
                Identifier = link.DependsOnRequirement.Identifier,
                Title = link.DependsOnRequirement.Title,
                Type = link.DependsOnRequirement.Type
            })
            .ToListAsync(cancellationToken);

        details.Dependents = await _context.RequirementDependencies.AsNoTracking()
            .Where(link =>
                link.DependsOnRequirementId == requirementId &&
                link.Requirement.ProjectId == projectId &&
                link.DependsOnRequirement.ProjectId == projectId)
            .OrderBy(link => link.Requirement.Identifier)
            .Select(link => new RequirementDependencySummary
            {
                LinkId = link.Id,
                RequirementId = link.RequirementId,
                Identifier = link.Requirement.Identifier,
                Title = link.Requirement.Title,
                Type = link.Requirement.Type
            })
            .ToListAsync(cancellationToken);

        details.RelatedFunctionalRequirements = details.Dependencies
            .Where(dependency => dependency.Type == RequirementType.Functional)
            .ToList();

        details.Traces = await _context.RequirementTraces.AsNoTracking()
            .Where(trace =>
                trace.RequirementId == requirementId &&
                trace.Requirement.ProjectId == projectId)
            .OrderBy(trace => trace.Stage)
            .ThenBy(trace => trace.ReferenceCode)
            .Select(trace => new RequirementTraceSummary
            {
                Id = trace.Id,
                Stage = trace.Stage,
                ReferenceCode = trace.ReferenceCode,
                Description = trace.Description,
                CreatedAt = trace.CreatedAt
            })
            .ToListAsync(cancellationToken);

        details.DesignArtifacts = await LoadDesignArtifactsAsync(
            projectId, requirementId, cancellationToken);
        details.ImplementationArtifacts = details.DesignArtifacts
            .SelectMany(design => design.ImplementationArtifacts)
            .DistinctBy(implementation => implementation.Id)
            .OrderBy(implementation => implementation.Identifier)
            .ToList();

        return details;
    }

    public async Task<IReadOnlyList<RequirementOption>> GetDependencyOptionsAsync(
        int projectId,
        int excludedRequirementId,
        CancellationToken cancellationToken = default) =>
        await _context.Requirements.AsNoTracking()
            .Where(requirement =>
                requirement.ProjectId == projectId &&
                requirement.Id != excludedRequirementId)
            .OrderBy(requirement => requirement.Identifier)
            .Select(requirement => new RequirementOption
            {
                Id = requirement.Id,
                Identifier = requirement.Identifier,
                Title = requirement.Title
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RequirementOption>> GetFunctionalRequirementOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default) =>
        await _context.Requirements.AsNoTracking()
            .Where(requirement =>
                requirement.ProjectId == projectId &&
                requirement.Type == RequirementType.Functional)
            .OrderBy(requirement => requirement.Identifier)
            .Select(requirement => new RequirementOption
            {
                Id = requirement.Id,
                Identifier = requirement.Identifier,
                Title = requirement.Title
            })
            .ToListAsync(cancellationToken);

    public Task<string> GetIdentifierPreviewAsync(
        int projectId,
        RequirementType type,
        CancellationToken cancellationToken = default) =>
        GetNextIdentifierAsync(projectId, type, cancellationToken);

    public async Task<IReadOnlyList<RequirementTraceabilityItem>> GetTraceabilityAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        List<RequirementSummary> requirements = await _context.Requirements.AsNoTracking()
            .Where(requirement => requirement.ProjectId == projectId)
            .OrderBy(requirement => requirement.Identifier)
            .Select(requirement => new RequirementSummary
            {
                Id = requirement.Id,
                ProjectId = requirement.ProjectId,
                Identifier = requirement.Identifier,
                Title = requirement.Title,
                Type = requirement.Type,
                Status = requirement.Status
            })
            .ToListAsync(cancellationToken);

        var dependencyRows = await _context.RequirementDependencies.AsNoTracking()
            .Where(link =>
                link.Requirement.ProjectId == projectId &&
                link.DependsOnRequirement.ProjectId == projectId)
            .Select(link => new
            {
                SourceId = link.RequirementId,
                Summary = new RequirementDependencySummary
                {
                    LinkId = link.Id,
                    RequirementId = link.DependsOnRequirementId,
                    Identifier = link.DependsOnRequirement.Identifier,
                    Title = link.DependsOnRequirement.Title,
                    Type = link.DependsOnRequirement.Type
                }
            })
            .ToListAsync(cancellationToken);

        var traceRows = await _context.RequirementTraces.AsNoTracking()
            .Where(trace => trace.Requirement.ProjectId == projectId)
            .Select(trace => new
            {
                trace.RequirementId,
                Summary = new RequirementTraceSummary
                {
                    Id = trace.Id,
                    Stage = trace.Stage,
                    ReferenceCode = trace.ReferenceCode,
                    Description = trace.Description,
                    CreatedAt = trace.CreatedAt
                }
            })
            .ToListAsync(cancellationToken);

        var designRows = await _context.DesignArtifactRequirements.AsNoTracking()
            .Where(link =>
                link.Requirement.ProjectId == projectId &&
                link.DesignArtifact.ProjectId == projectId)
            .OrderBy(link => link.DesignArtifact.Identifier)
            .Select(link => new
            {
                link.RequirementId,
                Design = new RequirementDesignArtifactSummary
                {
                    Id = link.DesignArtifactId,
                    Identifier = link.DesignArtifact.Identifier,
                    Title = link.DesignArtifact.Title,
                    Type = link.DesignArtifact.Type
                }
            })
            .ToListAsync(cancellationToken);

        var implementationRows = await _context.ImplementationArtifactDesigns.AsNoTracking()
            .Where(link =>
                link.DesignArtifact.ProjectId == projectId &&
                link.ImplementationArtifact.ProjectId == projectId)
            .OrderBy(link => link.ImplementationArtifact.Identifier)
            .Select(link => new
            {
                link.DesignArtifactId,
                Implementation = new RequirementImplementationArtifactSummary
                {
                    Id = link.ImplementationArtifactId,
                    Identifier = link.ImplementationArtifact.Identifier,
                    Title = link.ImplementationArtifact.Title,
                    Type = link.ImplementationArtifact.Type
                }
            })
            .ToListAsync(cancellationToken);

        List<int> implementationIds = implementationRows
            .Select(row => row.Implementation.Id)
            .Distinct()
            .ToList();
        var testRows = await _context.VModelTestCaseImplementationArtifacts.AsNoTracking()
            .Where(link =>
                implementationIds.Contains(link.ImplementationArtifactId) &&
                link.VModelTestCase.ProjectId == projectId &&
                link.ImplementationArtifact.ProjectId == projectId)
            .OrderBy(link => link.VModelTestCase.Identifier)
            .Select(link => new
            {
                link.ImplementationArtifactId,
                TestCaseId = link.VModelTestCaseId,
                link.VModelTestCase.Identifier,
                link.VModelTestCase.Title,
                link.VModelTestCase.TestLevel
            })
            .ToListAsync(cancellationToken);
        List<int> testCaseIds = testRows.Select(row => row.TestCaseId).Distinct().ToList();
        var executionRows = await _context.VModelTestExecutions.AsNoTracking()
            .Where(execution => testCaseIds.Contains(execution.VModelTestCaseId))
            .Select(execution => new
            {
                execution.VModelTestCaseId,
                execution.Result,
                execution.ExecutedAt,
                execution.Id
            })
            .ToListAsync(cancellationToken);
        var latestExecutions = executionRows
            .GroupBy(row => row.VModelTestCaseId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.ExecutedAt)
                    .ThenByDescending(row => row.Id)
                    .First());

        var acceptancePhase = await _context.VModelPhases.AsNoTracking()
            .Where(phase =>
                phase.ProjectId == projectId &&
                phase.PhaseType == VModelPhaseType.AcceptanceValidation)
            .Select(phase => new { phase.Status, phase.Id })
            .SingleOrDefaultAsync(cancellationToken);
        RequirementPhaseValidationSummary? latestValidation = null;
        if (acceptancePhase is not null)
        {
            latestValidation = await _context.VModelPhaseValidations.AsNoTracking()
                .Where(validation => validation.VModelPhaseId == acceptancePhase.Id)
                .OrderByDescending(validation => validation.ValidatedAt)
                .ThenByDescending(validation => validation.Id)
                .Select(validation => new RequirementPhaseValidationSummary
                {
                    PhaseType = VModelPhaseType.AcceptanceValidation,
                    Result = validation.Result,
                    ValidatedAt = validation.ValidatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        return requirements.Select(requirement =>
        {
            List<RequirementDesignArtifactSummary> designs = designRows
                .Where(row => row.RequirementId == requirement.Id)
                .Select(row => row.Design)
                .Select(design =>
                {
                    design.ImplementationArtifacts = implementationRows
                        .Where(row => row.DesignArtifactId == design.Id)
                        .Select(row => row.Implementation)
                        .DistinctBy(item => item.Id)
                        .OrderBy(item => item.Identifier)
                        .ToList();
                    foreach (RequirementImplementationArtifactSummary implementation in design.ImplementationArtifacts)
                        implementation.TestCases = testRows
                            .Where(row => row.ImplementationArtifactId == implementation.Id)
                            .Select(row =>
                            {
                                latestExecutions.TryGetValue(row.TestCaseId, out var execution);
                                return new RequirementTestCaseSummary
                                {
                                    Id = row.TestCaseId,
                                    Identifier = row.Identifier,
                                    Title = row.Title,
                                    TestLevel = row.TestLevel,
                                    LatestResult = execution?.Result,
                                    LastExecutedAt = execution?.ExecutedAt
                                };
                            })
                            .DistinctBy(item => item.Id)
                            .OrderBy(item => item.Identifier)
                            .ToList();
                    return design;
                })
                .ToList();
            RequirementCoverageState coverage = CalculateCoverage(
                designs,
                acceptancePhase?.Status,
                latestValidation);
            return new RequirementTraceabilityItem
            {
                RequirementId = requirement.Id,
                Identifier = requirement.Identifier,
                Title = requirement.Title,
                Type = requirement.Type,
                Status = requirement.Status,
                Dependencies = dependencyRows
                    .Where(row => row.SourceId == requirement.Id)
                    .Select(row => row.Summary)
                    .OrderBy(item => item.Identifier)
                    .ToList(),
                Traces = traceRows
                    .Where(row => row.RequirementId == requirement.Id)
                    .Select(row => row.Summary)
                    .OrderBy(item => item.Stage)
                    .ThenBy(item => item.ReferenceCode)
                    .ToList(),
                DesignArtifacts = designs,
                LatestPhaseValidation = latestValidation,
                CoverageState = coverage
            };
        }).ToList();
    }

    private static RequirementCoverageState CalculateCoverage(
        IReadOnlyList<RequirementDesignArtifactSummary> designs,
        VModelPhaseStatus? acceptancePhaseStatus,
        RequirementPhaseValidationSummary? latestValidation)
    {
        if (designs.Count == 0)
            return RequirementCoverageState.MissingDesign;
        List<RequirementImplementationArtifactSummary> implementations = designs
            .SelectMany(design => design.ImplementationArtifacts)
            .DistinctBy(implementation => implementation.Id)
            .ToList();
        if (implementations.Count == 0)
            return RequirementCoverageState.MissingImplementation;
        List<RequirementTestCaseSummary> tests = implementations
            .SelectMany(implementation => implementation.TestCases)
            .DistinctBy(test => test.Id)
            .ToList();
        if (tests.Count == 0)
            return RequirementCoverageState.MissingTest;
        if (tests.Any(test => test.LatestResult is null))
            return RequirementCoverageState.NotVerified;
        if (tests.Any(test => test.LatestResult == VModelTestResult.Fail))
            return RequirementCoverageState.VerificationFailed;
        if (acceptancePhaseStatus != VModelPhaseStatus.Completed)
            return RequirementCoverageState.Verified;
        if (latestValidation is null)
            return RequirementCoverageState.ValidationPending;
        return latestValidation.Result == VModelValidationResult.Pass
            ? RequirementCoverageState.FullyValidated
            : RequirementCoverageState.ValidationFailed;
    }

    private async Task<IReadOnlyList<RequirementDesignArtifactSummary>> LoadDesignArtifactsAsync(
        int projectId,
        int requirementId,
        CancellationToken cancellationToken)
    {
        List<RequirementDesignArtifactSummary> designs = await _context
            .DesignArtifactRequirements.AsNoTracking()
            .Where(link =>
                link.RequirementId == requirementId &&
                link.Requirement.ProjectId == projectId &&
                link.DesignArtifact.ProjectId == projectId)
            .OrderBy(link => link.DesignArtifact.Identifier)
            .Select(link => new RequirementDesignArtifactSummary
            {
                Id = link.DesignArtifactId,
                Identifier = link.DesignArtifact.Identifier,
                Title = link.DesignArtifact.Title,
                Type = link.DesignArtifact.Type
            })
            .ToListAsync(cancellationToken);

        if (designs.Count == 0)
            return designs;

        List<int> designIds = designs.Select(design => design.Id).ToList();
        var implementations = await _context.ImplementationArtifactDesigns.AsNoTracking()
            .Where(link =>
                designIds.Contains(link.DesignArtifactId) &&
                link.DesignArtifact.ProjectId == projectId &&
                link.ImplementationArtifact.ProjectId == projectId)
            .Select(link => new
            {
                link.DesignArtifactId,
                Summary = new RequirementImplementationArtifactSummary
                {
                    Id = link.ImplementationArtifactId,
                    Identifier = link.ImplementationArtifact.Identifier,
                    Title = link.ImplementationArtifact.Title,
                    Type = link.ImplementationArtifact.Type
                }
            })
            .ToListAsync(cancellationToken);
        foreach (RequirementDesignArtifactSummary design in designs)
            design.ImplementationArtifacts = implementations
                .Where(row => row.DesignArtifactId == design.Id)
                .Select(row => row.Summary)
                .DistinctBy(item => item.Id)
                .OrderBy(item => item.Identifier)
                .ToList();

        List<int> implementationIds = implementations
            .Select(row => row.Summary.Id)
            .Distinct()
            .ToList();
        var tests = await _context.VModelTestCaseImplementationArtifacts.AsNoTracking()
            .Where(link =>
                implementationIds.Contains(link.ImplementationArtifactId) &&
                link.VModelTestCase.ProjectId == projectId &&
                link.ImplementationArtifact.ProjectId == projectId)
            .Select(link => new
            {
                link.ImplementationArtifactId,
                link.VModelTestCaseId,
                link.VModelTestCase.Identifier,
                link.VModelTestCase.Title,
                link.VModelTestCase.TestLevel
            })
            .ToListAsync(cancellationToken);
        List<int> testIds = tests.Select(test => test.VModelTestCaseId).Distinct().ToList();
        var executions = await _context.VModelTestExecutions.AsNoTracking()
            .Where(execution => testIds.Contains(execution.VModelTestCaseId))
            .Select(execution => new
            {
                execution.VModelTestCaseId,
                execution.Result,
                execution.ExecutedAt,
                execution.Id
            })
            .ToListAsync(cancellationToken);
        var latestExecutions = executions
            .GroupBy(execution => execution.VModelTestCaseId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(execution => execution.ExecutedAt)
                    .ThenByDescending(execution => execution.Id)
                    .First());
        foreach (RequirementImplementationArtifactSummary implementation in designs
                     .SelectMany(design => design.ImplementationArtifacts)
                     .DistinctBy(item => item.Id))
            implementation.TestCases = tests
                .Where(test => test.ImplementationArtifactId == implementation.Id)
                .Select(test =>
                {
                    latestExecutions.TryGetValue(test.VModelTestCaseId, out var execution);
                    return new RequirementTestCaseSummary
                    {
                        Id = test.VModelTestCaseId,
                        Identifier = test.Identifier,
                        Title = test.Title,
                        TestLevel = test.TestLevel,
                        LatestResult = execution?.Result,
                        LastExecutedAt = execution?.ExecutedAt
                    };
                })
                .DistinctBy(test => test.Id)
                .OrderBy(test => test.Identifier)
                .ToList();
        return designs;
    }

    public async Task<RequirementOperationResult> CreateAsync(
        CreateRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Type))
            return Invalid("The requirement type is invalid.");
        string? validation = ValidateTypeSpecificContent(
            request.Type,
            request.Title,
            request.Description,
            request.Rationale,
            request.Preconditions,
            request.ExceptionScenario,
            request.NfrCategory,
            request.RelatedFunctionalRequirementIds,
            request.Priority);
        if (validation is not null)
            return Invalid(validation);

        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();

        var relatedValidation = await ValidateRelatedFunctionalRequirementsAsync(
            request.ProjectId,
            request.Type,
            request.RelatedFunctionalRequirementIds,
            cancellationToken);
        if (relatedValidation.Error is not null)
            return Invalid(relatedValidation.Error);

        string lockKey = $"{request.ProjectId}:{(int)request.Type}";
        SemaphoreSlim identifierLock = IdentifierLocks.GetOrAdd(
            lockKey, static _ => new SemaphoreSlim(1, 1));
        await identifierLock.WaitAsync(cancellationToken);
        try
        {
            for (int attempt = 1; attempt <= IdentifierRetryLimit; attempt++)
            {
                string identifier = await GetNextIdentifierAsync(
                    request.ProjectId, request.Type, cancellationToken);
                var requirement = new Requirement
                {
                    ProjectId = request.ProjectId,
                    Identifier = identifier,
                    Title = request.Type == RequirementType.Functional
                        ? request.Title.Trim()
                        : BuildNfrTitle(request.Description),
                    Description = request.Description.Trim(),
                    Type = request.Type,
                    Priority = request.Priority,
                    Rationale = NormalizeOptional(request.Rationale),
                    Preconditions = request.Type == RequirementType.Functional
                        ? NormalizeOptional(request.Preconditions)
                        : null,
                    ExceptionScenario = request.Type == RequirementType.Functional
                        ? NormalizeOptional(request.ExceptionScenario)
                        : null,
                    NfrCategory = request.Type == RequirementType.NonFunctional
                        ? request.NfrCategory
                        : null,
                    Status = RequirementStatus.Draft,
                    CreatedByUserId = request.ActorUserId,
                    CreatedAt = _clock.UtcNow
                };
                var audit = CreateAudit(
                    "RequirementCreated",
                    requirement,
                    request.ActorUserId,
                    "Requirement created.",
                    null,
                    new
                    {
                        requirement.Identifier,
                        requirement.Title,
                        requirement.Type,
                        requirement.Priority,
                        requirement.Status
                    });
                _context.Requirements.Add(requirement);
                _context.ActivityLogs.Add(audit);
                var addedEntities = new List<object> { requirement, audit };

                foreach (Requirement related in relatedValidation.Requirements)
                {
                    var link = new RequirementDependency
                    {
                        Requirement = requirement,
                        DependsOnRequirementId = related.Id,
                        CreatedByUserId = request.ActorUserId,
                        CreatedAt = _clock.UtcNow
                    };
                    var linkAudit = CreateAudit(
                        "RequirementDependencyAdded",
                        requirement,
                        request.ActorUserId,
                        "Related functional requirement added.",
                        null,
                        new { RelatedIdentifier = related.Identifier });
                    _context.RequirementDependencies.Add(link);
                    _context.ActivityLogs.Add(linkAudit);
                    addedEntities.Add(link);
                    addedEntities.Add(linkAudit);
                }

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return Success(requirement, "Requirement created successfully.");
                }
                catch (DbUpdateException exception)
                    when (IsIdentifierCollision(exception) && attempt < IdentifierRetryLimit)
                {
                    DetachAddedEntities(addedEntities);
                }
                catch (DbUpdateException exception)
                    when (IsIdentifierCollision(exception))
                {
                    DetachAddedEntities(addedEntities);
                    return Conflict(
                        "The requirement identifier was allocated concurrently. Please try again.");
                }
            }
        }
        finally
        {
            identifierLock.Release();
        }

        return Conflict("The requirement identifier could not be allocated.");
    }

    public async Task<RequirementBatchOperationResult> CreateAiGeneratedBatchAsync(
        CreateAiGeneratedRequirementsBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Requirements.Count is < 1 or > 20)
            return BatchFailure(
                RequirementOperationFailure.Validation,
                "Select between 1 and 20 generated requirements.");

        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return BatchFailure(authorization.Error.Failure, authorization.Error.Message);
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return BatchFailure(RequirementOperationFailure.ReadOnly, "Archived projects are read-only.");
        if (!CanManage(authorization.Access!))
            return BatchFailure(
                RequirementOperationFailure.Forbidden,
                "You are not authorized to manage requirements.");

        foreach (CreateRequirementBatchItem item in request.Requirements)
        {
            if (!Enum.IsDefined(item.Type))
                return BatchFailure(
                    RequirementOperationFailure.Validation,
                    "A selected requirement type is invalid.");
            string? validation = ValidateTypeSpecificContent(
                item.Type, item.Title, item.Description, item.Rationale,
                item.Preconditions, item.ExceptionScenario, item.NfrCategory,
                item.RelatedFunctionalRequirementIds, item.Priority);
            if (validation is not null)
                return BatchFailure(RequirementOperationFailure.Validation, validation);
            var related = await ValidateRelatedFunctionalRequirementsAsync(
                request.ProjectId, item.Type, item.RelatedFunctionalRequirementIds,
                cancellationToken);
            if (related.Error is not null)
                return BatchFailure(RequirementOperationFailure.Validation, related.Error);
        }

        IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var ids = new List<int>(request.Requirements.Count);
            foreach (CreateRequirementBatchItem item in request.Requirements)
            {
                RequirementOperationResult result = await CreateAsync(new CreateRequirementRequest
                {
                    ProjectId = request.ProjectId,
                    ActorUserId = request.ActorUserId,
                    Title = item.Title,
                    Description = item.Description,
                    Type = item.Type,
                    Priority = item.Priority,
                    Rationale = item.Rationale,
                    Preconditions = item.Preconditions,
                    ExceptionScenario = item.ExceptionScenario,
                    NfrCategory = item.NfrCategory,
                    RelatedFunctionalRequirementIds = item.RelatedFunctionalRequirementIds
                }, cancellationToken);
                if (!result.Succeeded)
                {
                    if (transaction is not null)
                        await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    return BatchFailure(result.Failure, result.Message);
                }
                ids.Add(result.RequirementId!.Value);
            }

            _context.ActivityLogs.Add(new ActivityLog
            {
                ActorUserId = request.ActorUserId,
                ProjectId = request.ProjectId,
                Action = "AiGeneratedRequirementsSaved",
                ResourceType = "AiRequirementBatch",
                ResourceId = request.ProjectId.ToString(),
                Description = $"Saved {ids.Count} reviewed AI-generated requirement suggestions.",
                NewValues = JsonSerializer.Serialize(new { Count = ids.Count }),
                CreatedAt = _clock.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return new RequirementBatchOperationResult
            {
                Succeeded = true,
                Message = $"{ids.Count} requirements saved successfully.",
                RequirementIds = ids
            };
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<RequirementOperationResult> UpdateAsync(
        UpdateRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ExpectedStatus))
            return Invalid("The expected requirement status is invalid.");

        Requirement? requirement = await FindRequirementAsync(
            request.ProjectId, request.RequirementId, cancellationToken);
        if (requirement is null)
            return NotFound();
        string? validation = ValidateTypeSpecificContent(
            requirement.Type,
            request.Title,
            request.Description,
            request.Rationale,
            request.Preconditions,
            request.ExceptionScenario,
            request.NfrCategory,
            request.RelatedFunctionalRequirementIds,
            request.Priority);
        if (validation is not null)
            return Invalid(validation);
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();
        if (requirement.Status != request.ExpectedStatus)
            return Conflict("The requirement changed before your edit. Refresh and try again.");
        if (requirement.Status is not (RequirementStatus.Draft or RequirementStatus.Rejected))
            return ReadOnly("Requirement content is read-only in its current status.");

        var relatedValidation = await ValidateRelatedFunctionalRequirementsAsync(
            request.ProjectId,
            requirement.Type,
            request.RelatedFunctionalRequirementIds,
            cancellationToken,
            requirement.Id);
        if (relatedValidation.Error is not null)
            return Invalid(relatedValidation.Error);

        var oldValues = new
        {
            requirement.Title,
            requirement.Priority,
            requirement.NfrCategory,
            HasRationale = !string.IsNullOrWhiteSpace(requirement.Rationale),
            HasPreconditions = !string.IsNullOrWhiteSpace(requirement.Preconditions),
            HasExceptionScenario = !string.IsNullOrWhiteSpace(requirement.ExceptionScenario)
        };
        requirement.Title = requirement.Type == RequirementType.Functional
            ? request.Title.Trim()
            : BuildNfrTitle(request.Description);
        requirement.Description = request.Description.Trim();
        requirement.Priority = request.Priority;
        requirement.Rationale = NormalizeOptional(request.Rationale);
        requirement.Preconditions = requirement.Type == RequirementType.Functional
            ? NormalizeOptional(request.Preconditions)
            : null;
        requirement.ExceptionScenario = requirement.Type == RequirementType.Functional
            ? NormalizeOptional(request.ExceptionScenario)
            : null;
        requirement.NfrCategory = requirement.Type == RequirementType.NonFunctional
            ? request.NfrCategory
            : null;
        requirement.UpdatedByUserId = request.ActorUserId;
        requirement.UpdatedAt = _clock.UtcNow;
        if (requirement.Type == RequirementType.NonFunctional)
            await SynchronizeRelatedFunctionalRequirementsAsync(
                requirement,
                relatedValidation.Requirements,
                request.ActorUserId,
                cancellationToken);
        _context.ActivityLogs.Add(CreateAudit(
            "RequirementEdited",
            requirement,
            request.ActorUserId,
            "Requirement content updated.",
            oldValues,
            new
            {
                requirement.Title,
                requirement.Priority,
                requirement.NfrCategory,
                HasRationale = !string.IsNullOrWhiteSpace(requirement.Rationale),
                HasPreconditions = !string.IsNullOrWhiteSpace(requirement.Preconditions),
                HasExceptionScenario = !string.IsNullOrWhiteSpace(requirement.ExceptionScenario)
            }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(requirement, "Requirement updated successfully.");
    }

    public async Task<RequirementOperationResult> TransitionAsync(
        TransitionRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ExpectedStatus) ||
            !Enum.IsDefined(request.TargetStatus))
            return Invalid("The requirement status is invalid.");

        Requirement? requirement = await FindRequirementAsync(
            request.ProjectId, request.RequirementId, cancellationToken);
        if (requirement is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();
        if (requirement.Status != request.ExpectedStatus)
            return Conflict("The requirement status changed. Refresh and try again.");
        if (!IsAllowedTransition(requirement.Status, request.TargetStatus))
            return Invalid("That requirement status transition is not allowed.");

        RequirementStatus oldStatus = requirement.Status;
        requirement.Status = request.TargetStatus;
        requirement.UpdatedByUserId = request.ActorUserId;
        requirement.UpdatedAt = _clock.UtcNow;
        _context.ActivityLogs.Add(CreateAudit(
            "RequirementStatusChanged",
            requirement,
            request.ActorUserId,
            "Requirement status changed.",
            new { Status = oldStatus },
            new { requirement.Status }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(requirement, "Requirement status updated successfully.");
    }

    public async Task<RequirementOperationResult> AddDependencyAsync(
        AddRequirementDependencyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RequirementId <= 0 || request.DependsOnRequirementId <= 0)
            return Invalid("The dependency request is invalid.");

        Requirement? requirement = await FindRequirementAsync(
            request.ProjectId, request.RequirementId, cancellationToken);
        if (requirement is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();
        if (request.RequirementId == request.DependsOnRequirementId)
            return Invalid("A requirement cannot depend on itself.");

        Requirement? dependency = await _context.Requirements.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == request.DependsOnRequirementId &&
                candidate.ProjectId == request.ProjectId,
                cancellationToken);
        if (dependency is null)
            return Invalid("The selected dependency does not belong to this project.");
        if (requirement.Type == RequirementType.NonFunctional &&
            dependency.Type != RequirementType.Functional)
            return Invalid(
                "A non-functional requirement may relate only to functional requirements.");
        if (await _context.RequirementDependencies.AsNoTracking().AnyAsync(link =>
                link.RequirementId == request.RequirementId &&
                link.DependsOnRequirementId == request.DependsOnRequirementId,
                cancellationToken))
            return Conflict("This dependency already exists.");
        if (await CreatesDependencyCycleAsync(
                request.ProjectId,
                request.RequirementId,
                request.DependsOnRequirementId,
                cancellationToken))
            return Invalid("This dependency would create a circular relationship.");

        var link = new RequirementDependency
        {
            RequirementId = request.RequirementId,
            DependsOnRequirementId = request.DependsOnRequirementId,
            CreatedByUserId = request.ActorUserId,
            CreatedAt = _clock.UtcNow
        };
        _context.RequirementDependencies.Add(link);
        _context.ActivityLogs.Add(CreateAudit(
            "RequirementDependencyAdded",
            requirement,
            request.ActorUserId,
            "Requirement dependency added.",
            null,
            new { DependsOnIdentifier = dependency.Identifier }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(requirement, "Dependency added successfully.", link.Id);
    }

    public async Task<RequirementOperationResult> RemoveDependencyAsync(
        RemoveRequirementDependencyRequest request,
        CancellationToken cancellationToken = default)
    {
        Requirement? requirement = await FindRequirementAsync(
            request.ProjectId, request.RequirementId, cancellationToken);
        if (requirement is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();

        RequirementDependency? link = await _context.RequirementDependencies
            .Include(item => item.DependsOnRequirement)
            .SingleOrDefaultAsync(item =>
                item.Id == request.DependencyId &&
                item.RequirementId == request.RequirementId &&
                item.Requirement.ProjectId == request.ProjectId &&
                item.DependsOnRequirement.ProjectId == request.ProjectId,
                cancellationToken);
        if (link is null)
            return NotFound("The dependency was not found.");

        _context.RequirementDependencies.Remove(link);
        _context.ActivityLogs.Add(CreateAudit(
            "RequirementDependencyRemoved",
            requirement,
            request.ActorUserId,
            "Requirement dependency removed.",
            new { DependsOnIdentifier = link.DependsOnRequirement.Identifier },
            null));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(requirement, "Dependency removed successfully.");
    }

    public async Task<RequirementOperationResult> AddTraceAsync(
        AddRequirementTraceRequest request,
        CancellationToken cancellationToken = default)
    {
        string referenceCode = request.ReferenceCode?.Trim() ?? string.Empty;
        string? description = NormalizeOptional(request.Description);
        if (!Enum.IsDefined(request.Stage))
            return Invalid("The traceability stage is invalid.");
        if (referenceCode.Length == 0)
            return Invalid("Reference code is required.");
        if (referenceCode.Length > ReferenceCodeMaximumLength)
            return Invalid($"Reference code must be {ReferenceCodeMaximumLength} characters or fewer.");
        if (description?.Length > TraceDescriptionMaximumLength)
            return Invalid($"Trace description must be {TraceDescriptionMaximumLength} characters or fewer.");

        Requirement? requirement = await FindRequirementAsync(
            request.ProjectId, request.RequirementId, cancellationToken);
        if (requirement is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();
        if (await _context.RequirementTraces.AsNoTracking().AnyAsync(trace =>
                trace.RequirementId == request.RequirementId &&
                trace.Stage == request.Stage &&
                trace.ReferenceCode == referenceCode,
                cancellationToken))
            return Conflict("This traceability reference already exists.");

        var trace = new RequirementTrace
        {
            RequirementId = request.RequirementId,
            Stage = request.Stage,
            ReferenceCode = referenceCode,
            Description = description,
            CreatedByUserId = request.ActorUserId,
            CreatedAt = _clock.UtcNow
        };
        _context.RequirementTraces.Add(trace);
        _context.ActivityLogs.Add(CreateAudit(
            "TraceabilityLinkAdded",
            requirement,
            request.ActorUserId,
            "Traceability reference added.",
            null,
            new { trace.Stage, trace.ReferenceCode }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(requirement, "Traceability reference added successfully.", trace.Id);
    }

    public async Task<RequirementOperationResult> RemoveTraceAsync(
        RemoveRequirementTraceRequest request,
        CancellationToken cancellationToken = default)
    {
        Requirement? requirement = await FindRequirementAsync(
            request.ProjectId, request.RequirementId, cancellationToken);
        if (requirement is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden();

        RequirementTrace? trace = await _context.RequirementTraces
            .SingleOrDefaultAsync(item =>
                item.Id == request.TraceId &&
                item.RequirementId == request.RequirementId &&
                item.Requirement.ProjectId == request.ProjectId,
                cancellationToken);
        if (trace is null)
            return NotFound("The traceability reference was not found.");

        _context.RequirementTraces.Remove(trace);
        _context.ActivityLogs.Add(CreateAudit(
            "TraceabilityLinkRemoved",
            requirement,
            request.ActorUserId,
            "Traceability reference removed.",
            new { trace.Stage, trace.ReferenceCode },
            null));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(requirement, "Traceability reference removed successfully.");
    }

    private async Task<Requirement?> FindRequirementAsync(
        int projectId,
        int requirementId,
        CancellationToken cancellationToken) =>
        await _context.Requirements.SingleOrDefaultAsync(requirement =>
            requirement.Id == requirementId &&
            requirement.ProjectId == projectId,
            cancellationToken);

    private async Task<(Project? Project, ActorAccess? Access, RequirementOperationResult? Error)>
        ValidateProjectAccessAsync(
            int projectId,
            string actorUserId,
            CancellationToken cancellationToken)
    {
        if (projectId <= 0 || string.IsNullOrWhiteSpace(actorUserId))
            return (null, null, Invalid("The requirement request is invalid."));

        Project? project = await _context.Projects.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
            return (null, null, NotFound("The project was not found."));
        if (project.Methodology != ProjectMethodology.VModel)
            return (null, null, Forbidden(
                "Requirements management is available only for V-Model projects."));

        List<string?> roles = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == actorUserId
            select role.Name)
            .ToListAsync(cancellationToken);
        if (!roles.Any(role => role is not null && SystemRoles.All.Contains(role)))
            return (null, null, Forbidden());

        ProjectMemberRole? projectRole = await _context.ProjectMembers.AsNoTracking()
            .Where(member =>
                member.ProjectId == projectId &&
                member.UserId == actorUserId)
            .Select(member => (ProjectMemberRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
        bool isAdmin = roles.Contains(SystemRoles.Admin, StringComparer.Ordinal);
        if (!isAdmin && projectRole is null)
            return (null, null, Forbidden("You are not a member of this project."));

        return (project, new ActorAccess(
            isAdmin,
            roles.Contains(SystemRoles.ProjectManager, StringComparer.Ordinal),
            projectRole), null);
    }

    private async Task<string> GetNextIdentifierAsync(
        int projectId,
        RequirementType type,
        CancellationToken cancellationToken)
    {
        string prefix = type == RequirementType.Functional ? "FR-" : "NFR-";
        List<string> identifiers = await _context.Requirements.AsNoTracking()
            .Where(requirement =>
                requirement.ProjectId == projectId &&
                requirement.Type == type &&
                requirement.Identifier.StartsWith(prefix))
            .Select(requirement => requirement.Identifier)
            .ToListAsync(cancellationToken);

        int highest = 0;
        foreach (string identifier in identifiers)
        {
            if (int.TryParse(identifier.AsSpan(prefix.Length), out int value) &&
                value > highest)
                highest = value;
        }

        return $"{prefix}{checked(highest + 1):D3}";
    }

    private async Task<bool> CreatesDependencyCycleAsync(
        int projectId,
        int sourceRequirementId,
        int targetRequirementId,
        CancellationToken cancellationToken)
    {
        var edges = await _context.RequirementDependencies.AsNoTracking()
            .Where(link =>
                link.Requirement.ProjectId == projectId &&
                link.DependsOnRequirement.ProjectId == projectId)
            .Select(link => new
            {
                Source = link.RequirementId,
                Target = link.DependsOnRequirementId
            })
            .ToListAsync(cancellationToken);

        Dictionary<int, List<int>> graph = edges
            .GroupBy(edge => edge.Source)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.Target).ToList());
        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        pending.Push(targetRequirementId);

        while (pending.Count > 0)
        {
            int current = pending.Pop();
            if (current == sourceRequirementId)
                return true;
            if (!visited.Add(current) || !graph.TryGetValue(current, out List<int>? next))
                continue;
            foreach (int requirementId in next)
                pending.Push(requirementId);
        }

        return false;
    }

    private static bool IsAllowedTransition(
        RequirementStatus source,
        RequirementStatus target) => source switch
        {
            RequirementStatus.Draft => target == RequirementStatus.UnderReview,
            RequirementStatus.UnderReview =>
                target is RequirementStatus.Approved or RequirementStatus.Rejected,
            RequirementStatus.Rejected => target == RequirementStatus.Draft,
            RequirementStatus.Approved => target == RequirementStatus.Deprecated,
            _ => false
        };

    private static bool CanManage(ActorAccess access) =>
        access.IsAdmin ||
        access.HasProjectManagerRole &&
        access.ProjectRole == ProjectMemberRole.ProjectManager;

    private static string? ValidateTypeSpecificContent(
        RequirementType type,
        string title,
        string description,
        string? rationale,
        string? preconditions,
        string? exceptionScenario,
        NfrCategory? nfrCategory,
        IReadOnlyList<int>? relatedFunctionalRequirementIds,
        PriorityLevel priority)
    {
        string normalizedTitle = title?.Trim() ?? string.Empty;
        string normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedDescription.Length == 0)
            return "Requirement description is required.";
        if (normalizedDescription.Length > DescriptionMaximumLength)
            return $"Requirement description must be {DescriptionMaximumLength} characters or fewer.";
        if (rationale?.Trim().Length > RationaleMaximumLength)
            return $"Requirement rationale must be {RationaleMaximumLength} characters or fewer.";
        if (!Enum.IsDefined(priority))
            return "Requirement priority is invalid.";

        if (type == RequirementType.Functional)
        {
            if (normalizedTitle.Length == 0)
                return "Requirement name is required for a functional requirement.";
            if (normalizedTitle.Length > TitleMaximumLength)
                return $"Requirement name must be {TitleMaximumLength} characters or fewer.";
            if (preconditions?.Trim().Length > PreconditionsMaximumLength)
                return $"Preconditions must be {PreconditionsMaximumLength} characters or fewer.";
            if (exceptionScenario?.Trim().Length > ExceptionScenarioMaximumLength)
                return $"Exception scenario must be {ExceptionScenarioMaximumLength} characters or fewer.";
            if (nfrCategory is not null)
                return "NFR category cannot be applied to a functional requirement.";
            if ((relatedFunctionalRequirementIds?.Count ?? 0) > 0)
                return "Related functional requirements apply only to non-functional requirements.";
            return null;
        }

        if (type != RequirementType.NonFunctional)
            return "The requirement type is invalid.";
        if (nfrCategory is null || !Enum.IsDefined(nfrCategory.Value))
            return "NFR category is required.";
        if (!string.IsNullOrWhiteSpace(preconditions) ||
            !string.IsNullOrWhiteSpace(exceptionScenario))
            return "Functional requirement fields cannot be applied to a non-functional requirement.";
        return null;
    }

    private async Task<(List<Requirement> Requirements, string? Error)>
        ValidateRelatedFunctionalRequirementsAsync(
            int projectId,
            RequirementType type,
            IReadOnlyList<int>? selectedIds,
            CancellationToken cancellationToken,
            int? sourceRequirementId = null)
    {
        if (type != RequirementType.NonFunctional)
            return ([], null);

        List<int> ids = selectedIds?.ToList() ?? [];
        if (ids.Any(id => id <= 0))
            return ([], "A related functional requirement is invalid.");
        if (ids.Distinct().Count() != ids.Count)
            return ([], "A related functional requirement was selected more than once.");
        if (ids.Count == 0)
            return ([], null);

        List<Requirement> requirements = await _context.Requirements.AsNoTracking()
            .Where(requirement =>
                ids.Contains(requirement.Id) &&
                requirement.ProjectId == projectId &&
                requirement.Type == RequirementType.Functional)
            .ToListAsync(cancellationToken);
        if (requirements.Count != ids.Count)
            return ([],
                "Every related requirement must be a functional requirement in this project.");

        if (sourceRequirementId.HasValue)
        {
            foreach (int targetId in ids)
            {
                if (await CreatesDependencyCycleAsync(
                        projectId,
                        sourceRequirementId.Value,
                        targetId,
                        cancellationToken))
                    return ([], "A related requirement would create a circular relationship.");
            }
        }

        return (requirements, null);
    }

    private async Task SynchronizeRelatedFunctionalRequirementsAsync(
        Requirement requirement,
        IReadOnlyList<Requirement> selectedRequirements,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        List<RequirementDependency> existing = await _context.RequirementDependencies
            .Include(link => link.DependsOnRequirement)
            .Where(link =>
                link.RequirementId == requirement.Id &&
                link.DependsOnRequirement.ProjectId == requirement.ProjectId &&
                link.DependsOnRequirement.Type == RequirementType.Functional)
            .ToListAsync(cancellationToken);
        HashSet<int> selectedIds = selectedRequirements
            .Select(item => item.Id)
            .ToHashSet();

        foreach (RequirementDependency link in existing
                     .Where(link => !selectedIds.Contains(link.DependsOnRequirementId)))
        {
            _context.RequirementDependencies.Remove(link);
            _context.ActivityLogs.Add(CreateAudit(
                "RequirementDependencyRemoved",
                requirement,
                actorUserId,
                "Related functional requirement removed.",
                new { RelatedIdentifier = link.DependsOnRequirement.Identifier },
                null));
        }

        HashSet<int> existingIds = existing
            .Select(link => link.DependsOnRequirementId)
            .ToHashSet();
        foreach (Requirement related in selectedRequirements
                     .Where(item => !existingIds.Contains(item.Id)))
        {
            _context.RequirementDependencies.Add(new RequirementDependency
            {
                RequirementId = requirement.Id,
                DependsOnRequirementId = related.Id,
                CreatedByUserId = actorUserId,
                CreatedAt = _clock.UtcNow
            });
            _context.ActivityLogs.Add(CreateAudit(
                "RequirementDependencyAdded",
                requirement,
                actorUserId,
                "Related functional requirement added.",
                null,
                new { RelatedIdentifier = related.Identifier }));
        }
    }

    private static string BuildNfrTitle(string description)
    {
        string normalized = description.Trim();
        int lineBreak = normalized.IndexOfAny(['\r', '\n']);
        if (lineBreak > 0)
            normalized = normalized[..lineBreak].Trim();
        return normalized.Length <= TitleMaximumLength
            ? normalized
            : normalized[..TitleMaximumLength].TrimEnd();
    }

    private void DetachAddedEntities(IEnumerable<object> entities)
    {
        foreach (object entity in entities.Reverse())
            _context.Entry(entity).State = EntityState.Detached;
    }

    private ActivityLog CreateAudit(
        string action,
        Requirement requirement,
        string actorUserId,
        string description,
        object? oldValues,
        object? newValues) => new()
        {
            ActorUserId = actorUserId,
            ProjectId = requirement.ProjectId,
            Action = action,
            ResourceType = nameof(Requirement),
            ResourceId = requirement.Identifier,
            Description = description,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CreatedAt = _clock.UtcNow
        };

    private static bool IsIdentifierCollision(DbUpdateException exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is SqlException sqlException &&
                sqlException.Number is 2601 or 2627)
                return true;
            current = current.InnerException;
        }
        return false;
    }

    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static RequirementOperationResult Success(
        Requirement requirement,
        string message,
        int? linkId = null) => RequirementOperationResult.Success(
            requirement.Id,
            requirement.Identifier,
            requirement.Status,
            message,
            linkId);

    private static RequirementOperationResult Invalid(string message) =>
        RequirementOperationResult.Failed(RequirementOperationFailure.Validation, message);

    private static RequirementOperationResult NotFound(
        string message = "The requirement was not found.") =>
        RequirementOperationResult.Failed(RequirementOperationFailure.NotFound, message);

    private static RequirementOperationResult Forbidden(
        string message = "You are not authorized to manage requirements.") =>
        RequirementOperationResult.Failed(RequirementOperationFailure.Forbidden, message);

    private static RequirementOperationResult ReadOnly(
        string message = "Archived projects are read-only.") =>
        RequirementOperationResult.Failed(RequirementOperationFailure.ReadOnly, message);

    private static RequirementOperationResult Conflict(string message) =>
        RequirementOperationResult.Failed(RequirementOperationFailure.Conflict, message);

    private static RequirementBatchOperationResult BatchFailure(
        RequirementOperationFailure failure,
        string message) => new() { Failure = failure, Message = message };

    private sealed record ActorAccess(
        bool IsAdmin,
        bool HasProjectManagerRole,
        ProjectMemberRole? ProjectRole);
}
