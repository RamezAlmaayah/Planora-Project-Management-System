using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.VModel;
using Planora.Application.Common.Security;
using Planora.Application.Common.VModel;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.VModel;

public sealed class VModelArtifactService : IVModelArtifactService
{
    private const int TitleMaximumLength = 200;
    private const int DescriptionMaximumLength = 4000;
    private const int SourceReferenceMaximumLength = 500;
    private const int IdentifierRetryLimit = 3;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdentifierLocks =
        new(StringComparer.Ordinal);

    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;

    public VModelArtifactService(ApplicationDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DesignArtifactSummary>> GetDesignArtifactsAsync(
        int projectId, string? search = null,
        CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeSearch(search);
        IQueryable<DesignArtifact> query = _context.DesignArtifacts.AsNoTracking()
            .Where(x => x.ProjectId == projectId);
        if (normalized.Length > 0)
            query = query.Where(x => x.Identifier.Contains(normalized) ||
                                     x.Title.Contains(normalized) ||
                                     x.Description.Contains(normalized));
        return await query.OrderBy(x => x.Identifier)
            .Select(x => new DesignArtifactSummary
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                Identifier = x.Identifier,
                Title = x.Title,
                Type = x.Type,
                LinkedRequirementCount = x.Requirements.Count,
                CreatorName = _context.Users.Where(user => user.Id == x.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? x.CreatedByUserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToListAsync(cancellationToken);
    }

    public async Task<DesignArtifactDetails?> GetDesignArtifactAsync(
        int projectId, int artifactId,
        CancellationToken cancellationToken = default)
    {
        DesignArtifactDetails? result = await _context.DesignArtifacts.AsNoTracking()
            .Where(x => x.Id == artifactId && x.ProjectId == projectId)
            .Select(x => new DesignArtifactDetails
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                ProjectName = x.Project.Name,
                Identifier = x.Identifier,
                Title = x.Title,
                Description = x.Description,
                Type = x.Type,
                LinkedRequirementCount = x.Requirements.Count,
                CreatorName = _context.Users.Where(user => user.Id == x.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? x.CreatedByUserId,
                UpdatedByName = x.UpdatedByUserId == null ? null : _context.Users
                    .Where(user => user.Id == x.UpdatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).SingleOrDefaultAsync(cancellationToken);
        if (result is null) return null;

        result.Requirements = await _context.DesignArtifactRequirements.AsNoTracking()
            .Where(x => x.DesignArtifactId == artifactId &&
                        x.DesignArtifact.ProjectId == projectId &&
                        x.Requirement.ProjectId == projectId)
            .OrderBy(x => x.Requirement.Identifier)
            .Select(x => new RequirementArtifactOption
            {
                Id = x.RequirementId,
                Identifier = x.Requirement.Identifier,
                Title = x.Requirement.Title,
                Status = x.Requirement.Status
            }).ToListAsync(cancellationToken);
        result.ImplementationArtifacts = await _context.ImplementationArtifactDesigns.AsNoTracking()
            .Where(x => x.DesignArtifactId == artifactId &&
                        x.DesignArtifact.ProjectId == projectId &&
                        x.ImplementationArtifact.ProjectId == projectId)
            .OrderBy(x => x.ImplementationArtifact.Identifier)
            .Select(x => new ImplementationArtifactSummary
            {
                Id = x.ImplementationArtifactId,
                ProjectId = x.ImplementationArtifact.ProjectId,
                Identifier = x.ImplementationArtifact.Identifier,
                Title = x.ImplementationArtifact.Title,
                Type = x.ImplementationArtifact.Type,
                SourceReference = x.ImplementationArtifact.SourceReference,
                LinkedDesignCount = x.ImplementationArtifact.Designs.Count,
                CreatorUserId = x.ImplementationArtifact.CreatedByUserId,
                CreatorName = _context.Users.Where(user =>
                        user.Id == x.ImplementationArtifact.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? x.ImplementationArtifact.CreatedByUserId,
                CreatedAt = x.ImplementationArtifact.CreatedAt,
                UpdatedAt = x.ImplementationArtifact.UpdatedAt
            }).ToListAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<RequirementArtifactOption>> GetApprovedRequirementOptionsAsync(
        int projectId, int? designArtifactId = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Requirement> query = _context.Requirements.AsNoTracking()
            .Where(x => x.ProjectId == projectId);
        if (designArtifactId.HasValue)
            query = query.Where(x => x.Status == RequirementStatus.Approved ||
                                     x.DesignArtifacts.Any(link =>
                                         link.DesignArtifactId == designArtifactId.Value &&
                                         link.DesignArtifact.ProjectId == projectId));
        else
            query = query.Where(x => x.Status == RequirementStatus.Approved);
        return await query.OrderBy(x => x.Identifier)
            .Select(x => new RequirementArtifactOption
            {
                Id = x.Id,
                Identifier = x.Identifier,
                Title = x.Title,
                Status = x.Status
            }).ToListAsync(cancellationToken);
    }

    public Task<string> GetNextDesignIdentifierPreviewAsync(
        int projectId, CancellationToken cancellationToken = default) =>
        GetNextDesignIdentifierAsync(projectId, cancellationToken);

    public async Task<ArtifactOperationResult> CreateDesignArtifactAsync(
        CreateDesignArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        string? validation = ValidateDesign(request.Title, request.Description, request.Type,
            request.RequirementIds);
        if (validation is not null) return Invalid(validation);
        var access = await ValidateAccessAsync(request.ProjectId, request.ActorUserId, cancellationToken);
        if (access.Error is not null) return access.Error;
        if (access.Project!.Status == ProjectStatus.Archived) return ReadOnly();
        if (!CanManage(access.Access!)) return Forbidden();
        var links = await ValidateRequirementsForCreateAsync(
            request.ProjectId, request.RequirementIds, cancellationToken);
        if (links.Error is not null) return Invalid(links.Error);

        SemaphoreSlim gate = IdentifierLocks.GetOrAdd(
            $"design:{request.ProjectId}", static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            for (int attempt = 1; attempt <= IdentifierRetryLimit; attempt++)
            {
                string identifier = await GetNextDesignIdentifierAsync(request.ProjectId, cancellationToken);
                var artifact = new DesignArtifact
                {
                    ProjectId = request.ProjectId,
                    Identifier = identifier,
                    Title = request.Title.Trim(),
                    Description = request.Description.Trim(),
                    Type = request.Type,
                    CreatedByUserId = request.ActorUserId,
                    CreatedAt = _clock.UtcNow
                };
                _context.DesignArtifacts.Add(artifact);
                var added = new List<object> { artifact };
                foreach (Requirement requirement in links.Requirements)
                {
                    var link = new DesignArtifactRequirement
                    {
                        DesignArtifact = artifact,
                        RequirementId = requirement.Id
                    };
                    _context.DesignArtifactRequirements.Add(link);
                    added.Add(link);
                    ActivityLog linkAudit = Audit("DesignRequirementLinked", nameof(DesignArtifact),
                        identifier, request.ProjectId, request.ActorUserId,
                        "Requirement linked to design artifact.",
                        new { RequirementIdentifier = requirement.Identifier });
                    _context.ActivityLogs.Add(linkAudit);
                    added.Add(linkAudit);
                }
                ActivityLog audit = Audit("DesignArtifactCreated", nameof(DesignArtifact),
                    identifier, request.ProjectId, request.ActorUserId,
                    "Design artifact created.", new { artifact.Identifier, artifact.Title, artifact.Type });
                _context.ActivityLogs.Add(audit);
                added.Add(audit);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return Success(artifact.Id, artifact.Identifier, "Design artifact created successfully.");
                }
                catch (DbUpdateException ex) when (IsIdentifierCollision(ex) && attempt < IdentifierRetryLimit)
                {
                    Detach(added);
                }
                catch (DbUpdateException ex) when (IsIdentifierCollision(ex))
                {
                    Detach(added);
                    return Conflict("The design identifier was allocated concurrently. Please try again.");
                }
            }
        }
        finally { gate.Release(); }
        return Conflict("The design identifier could not be allocated.");
    }

    public async Task<ArtifactOperationResult> UpdateDesignArtifactAsync(
        UpdateDesignArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        string? validation = ValidateDesign(request.Title, request.Description, request.Type,
            request.RequirementIds);
        if (validation is not null) return Invalid(validation);
        DesignArtifact? artifact = await _context.DesignArtifacts
            .Include(x => x.Requirements).ThenInclude(x => x.Requirement)
            .SingleOrDefaultAsync(x => x.Id == request.ArtifactId && x.ProjectId == request.ProjectId,
                cancellationToken);
        if (artifact is null) return NotFound("The design artifact was not found.");
        var access = await ValidateAccessAsync(request.ProjectId, request.ActorUserId, cancellationToken);
        if (access.Error is not null) return access.Error;
        if (access.Project!.Status == ProjectStatus.Archived) return ReadOnly();
        if (!CanManage(access.Access!)) return Forbidden();

        var links = await ValidateRequirementsForUpdateAsync(
            artifact, request.RequirementIds, cancellationToken);
        if (links.Error is not null) return Invalid(links.Error);

        var oldValues = new { artifact.Title, artifact.Type };
        artifact.Title = request.Title.Trim();
        artifact.Description = request.Description.Trim();
        artifact.Type = request.Type;
        artifact.UpdatedByUserId = request.ActorUserId;
        artifact.UpdatedAt = _clock.UtcNow;
        HashSet<int> selected = request.RequirementIds.ToHashSet();
        foreach (DesignArtifactRequirement link in artifact.Requirements
                     .Where(x => !selected.Contains(x.RequirementId)).ToList())
        {
            artifact.Requirements.Remove(link);
            _context.ActivityLogs.Add(Audit("DesignRequirementUnlinked", nameof(DesignArtifact),
                artifact.Identifier, artifact.ProjectId, request.ActorUserId,
                "Requirement unlinked from design artifact.",
                new { RequirementIdentifier = link.Requirement.Identifier }));
        }
        HashSet<int> existing = artifact.Requirements.Select(x => x.RequirementId).ToHashSet();
        foreach (Requirement requirement in links.Requirements.Where(x => !existing.Contains(x.Id)))
        {
            artifact.Requirements.Add(new DesignArtifactRequirement { RequirementId = requirement.Id });
            _context.ActivityLogs.Add(Audit("DesignRequirementLinked", nameof(DesignArtifact),
                artifact.Identifier, artifact.ProjectId, request.ActorUserId,
                "Requirement linked to design artifact.",
                new { RequirementIdentifier = requirement.Identifier }));
        }
        _context.ActivityLogs.Add(Audit("DesignArtifactEdited", nameof(DesignArtifact),
            artifact.Identifier, artifact.ProjectId, request.ActorUserId,
            "Design artifact updated.", new { Old = oldValues, New = new { artifact.Title, artifact.Type } }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(artifact.Id, artifact.Identifier, "Design artifact updated successfully.");
    }

    public async Task<IReadOnlyList<ImplementationArtifactSummary>> GetImplementationArtifactsAsync(
        int projectId, string? search = null,
        CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeSearch(search);
        IQueryable<ImplementationArtifact> query = _context.ImplementationArtifacts.AsNoTracking()
            .Where(x => x.ProjectId == projectId);
        if (normalized.Length > 0)
            query = query.Where(x => x.Identifier.Contains(normalized) ||
                                     x.Title.Contains(normalized) ||
                                     x.Description.Contains(normalized) ||
                                     (x.SourceReference != null && x.SourceReference.Contains(normalized)));
        return await query.OrderBy(x => x.Identifier)
            .Select(x => new ImplementationArtifactSummary
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                Identifier = x.Identifier,
                Title = x.Title,
                Type = x.Type,
                SourceReference = x.SourceReference,
                LinkedDesignCount = x.Designs.Count,
                CreatorUserId = x.CreatedByUserId,
                CreatorName = _context.Users.Where(user => user.Id == x.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? x.CreatedByUserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToListAsync(cancellationToken);
    }

    public async Task<ImplementationArtifactDetails?> GetImplementationArtifactAsync(
        int projectId, int artifactId,
        CancellationToken cancellationToken = default)
    {
        ImplementationArtifactDetails? result = await _context.ImplementationArtifacts.AsNoTracking()
            .Where(x => x.Id == artifactId && x.ProjectId == projectId)
            .Select(x => new ImplementationArtifactDetails
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                ProjectName = x.Project.Name,
                Identifier = x.Identifier,
                Title = x.Title,
                Description = x.Description,
                Type = x.Type,
                SourceReference = x.SourceReference,
                LinkedDesignCount = x.Designs.Count,
                CreatorUserId = x.CreatedByUserId,
                CreatorName = _context.Users.Where(user => user.Id == x.CreatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? x.CreatedByUserId,
                UpdatedByName = x.UpdatedByUserId == null ? null : _context.Users
                    .Where(user => user.Id == x.UpdatedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault(),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).SingleOrDefaultAsync(cancellationToken);
        if (result is null) return null;
        result.Designs = await _context.ImplementationArtifactDesigns.AsNoTracking()
            .Where(x => x.ImplementationArtifactId == artifactId &&
                        x.ImplementationArtifact.ProjectId == projectId &&
                        x.DesignArtifact.ProjectId == projectId)
            .OrderBy(x => x.DesignArtifact.Identifier)
            .Select(x => new DesignArtifactOption
            {
                Id = x.DesignArtifactId,
                Identifier = x.DesignArtifact.Identifier,
                Title = x.DesignArtifact.Title
            }).ToListAsync(cancellationToken);
        result.DerivedRequirements = await _context.ImplementationArtifactDesigns.AsNoTracking()
            .Where(x => x.ImplementationArtifactId == artifactId &&
                        x.ImplementationArtifact.ProjectId == projectId &&
                        x.DesignArtifact.ProjectId == projectId)
            .SelectMany(x => x.DesignArtifact.Requirements)
            .Where(x => x.Requirement.ProjectId == projectId)
            .Select(x => new RequirementArtifactOption
            {
                Id = x.RequirementId,
                Identifier = x.Requirement.Identifier,
                Title = x.Requirement.Title,
                Status = x.Requirement.Status
            }).Distinct().OrderBy(x => x.Identifier).ToListAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<DesignArtifactOption>> GetDesignArtifactOptionsAsync(
        int projectId, CancellationToken cancellationToken = default) =>
        await _context.DesignArtifacts.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Identifier)
            .Select(x => new DesignArtifactOption
            {
                Id = x.Id,
                Identifier = x.Identifier,
                Title = x.Title
            }).ToListAsync(cancellationToken);

    public Task<string> GetNextImplementationIdentifierPreviewAsync(
        int projectId, CancellationToken cancellationToken = default) =>
        GetNextImplementationIdentifierAsync(projectId, cancellationToken);

    public async Task<ArtifactOperationResult> CreateImplementationArtifactAsync(
        CreateImplementationArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        string? validation = ValidateImplementation(request.Title, request.Description,
            request.Type, request.SourceReference, request.DesignArtifactIds);
        if (validation is not null) return Invalid(validation);
        var access = await ValidateAccessAsync(request.ProjectId, request.ActorUserId, cancellationToken);
        if (access.Error is not null) return access.Error;
        if (access.Project!.Status == ProjectStatus.Archived) return ReadOnly();
        if (!CanCreateImplementation(access.Access!)) return Forbidden();
        var links = await ValidateDesignLinksAsync(request.ProjectId, request.DesignArtifactIds,
            cancellationToken);
        if (links.Error is not null) return Invalid(links.Error);

        SemaphoreSlim gate = IdentifierLocks.GetOrAdd(
            $"implementation:{request.ProjectId}", static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            for (int attempt = 1; attempt <= IdentifierRetryLimit; attempt++)
            {
                string identifier = await GetNextImplementationIdentifierAsync(
                    request.ProjectId, cancellationToken);
                var artifact = new ImplementationArtifact
                {
                    ProjectId = request.ProjectId,
                    Identifier = identifier,
                    Title = request.Title.Trim(),
                    Description = request.Description.Trim(),
                    Type = request.Type,
                    SourceReference = NormalizeOptional(request.SourceReference),
                    CreatedByUserId = request.ActorUserId,
                    CreatedAt = _clock.UtcNow
                };
                _context.ImplementationArtifacts.Add(artifact);
                var added = new List<object> { artifact };
                foreach (DesignArtifact design in links.Designs)
                {
                    var link = new ImplementationArtifactDesign
                    {
                        ImplementationArtifact = artifact,
                        DesignArtifactId = design.Id
                    };
                    _context.ImplementationArtifactDesigns.Add(link);
                    added.Add(link);
                    ActivityLog linkAudit = Audit("ImplementationDesignLinked",
                        nameof(ImplementationArtifact), identifier, request.ProjectId,
                        request.ActorUserId, "Design linked to implementation artifact.",
                        new { DesignIdentifier = design.Identifier });
                    _context.ActivityLogs.Add(linkAudit);
                    added.Add(linkAudit);
                }
                ActivityLog audit = Audit("ImplementationArtifactCreated",
                    nameof(ImplementationArtifact), identifier, request.ProjectId,
                    request.ActorUserId, "Implementation artifact created.",
                    new { artifact.Identifier, artifact.Title, artifact.Type,
                        HasSourceReference = artifact.SourceReference != null });
                _context.ActivityLogs.Add(audit);
                added.Add(audit);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return Success(artifact.Id, artifact.Identifier,
                        "Implementation artifact created successfully.");
                }
                catch (DbUpdateException ex) when (IsIdentifierCollision(ex) && attempt < IdentifierRetryLimit)
                {
                    Detach(added);
                }
                catch (DbUpdateException ex) when (IsIdentifierCollision(ex))
                {
                    Detach(added);
                    return Conflict("The implementation identifier was allocated concurrently. Please try again.");
                }
            }
        }
        finally { gate.Release(); }
        return Conflict("The implementation identifier could not be allocated.");
    }

    public async Task<ArtifactOperationResult> UpdateImplementationArtifactAsync(
        UpdateImplementationArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        string? validation = ValidateImplementation(request.Title, request.Description,
            request.Type, request.SourceReference, request.DesignArtifactIds);
        if (validation is not null) return Invalid(validation);
        ImplementationArtifact? artifact = await _context.ImplementationArtifacts
            .Include(x => x.Designs).ThenInclude(x => x.DesignArtifact)
            .SingleOrDefaultAsync(x => x.Id == request.ArtifactId && x.ProjectId == request.ProjectId,
                cancellationToken);
        if (artifact is null) return NotFound("The implementation artifact was not found.");
        var access = await ValidateAccessAsync(request.ProjectId, request.ActorUserId, cancellationToken);
        if (access.Error is not null) return access.Error;
        if (access.Project!.Status == ProjectStatus.Archived) return ReadOnly();
        if (!CanEditImplementation(access.Access!, artifact.CreatedByUserId, request.ActorUserId))
            return Forbidden();
        var links = await ValidateDesignLinksAsync(request.ProjectId, request.DesignArtifactIds,
            cancellationToken);
        if (links.Error is not null) return Invalid(links.Error);

        var oldValues = new { artifact.Title, artifact.Type,
            HasSourceReference = artifact.SourceReference != null };
        artifact.Title = request.Title.Trim();
        artifact.Description = request.Description.Trim();
        artifact.Type = request.Type;
        artifact.SourceReference = NormalizeOptional(request.SourceReference);
        artifact.UpdatedByUserId = request.ActorUserId;
        artifact.UpdatedAt = _clock.UtcNow;
        HashSet<int> selected = request.DesignArtifactIds.ToHashSet();
        foreach (ImplementationArtifactDesign link in artifact.Designs
                     .Where(x => !selected.Contains(x.DesignArtifactId)).ToList())
        {
            artifact.Designs.Remove(link);
            _context.ActivityLogs.Add(Audit("ImplementationDesignUnlinked",
                nameof(ImplementationArtifact), artifact.Identifier, artifact.ProjectId,
                request.ActorUserId, "Design unlinked from implementation artifact.",
                new { DesignIdentifier = link.DesignArtifact.Identifier }));
        }
        HashSet<int> existing = artifact.Designs.Select(x => x.DesignArtifactId).ToHashSet();
        foreach (DesignArtifact design in links.Designs.Where(x => !existing.Contains(x.Id)))
        {
            artifact.Designs.Add(new ImplementationArtifactDesign { DesignArtifactId = design.Id });
            _context.ActivityLogs.Add(Audit("ImplementationDesignLinked",
                nameof(ImplementationArtifact), artifact.Identifier, artifact.ProjectId,
                request.ActorUserId, "Design linked to implementation artifact.",
                new { DesignIdentifier = design.Identifier }));
        }
        _context.ActivityLogs.Add(Audit("ImplementationArtifactEdited",
            nameof(ImplementationArtifact), artifact.Identifier, artifact.ProjectId,
            request.ActorUserId, "Implementation artifact updated.",
            new { Old = oldValues, New = new { artifact.Title, artifact.Type,
                HasSourceReference = artifact.SourceReference != null } }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(artifact.Id, artifact.Identifier,
            "Implementation artifact updated successfully.");
    }

    private async Task<(Project? Project, ActorAccess? Access, ArtifactOperationResult? Error)>
        ValidateAccessAsync(int projectId, string actorUserId, CancellationToken cancellationToken)
    {
        if (projectId <= 0 || string.IsNullOrWhiteSpace(actorUserId))
            return (null, null, Invalid("The artifact request is invalid."));
        Project? project = await _context.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null) return (null, null, NotFound("The project was not found."));
        if (project.Methodology != ProjectMethodology.VModel)
            return (null, null, Forbidden("Artifacts are available only for V-Model projects."));
        List<string?> roles = await (from ur in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on ur.RoleId equals role.Id
            where ur.UserId == actorUserId select role.Name).ToListAsync(cancellationToken);
        if (!roles.Any(x => x is not null && SystemRoles.All.Contains(x)))
            return (null, null, Forbidden());
        ProjectMemberRole? projectRole = await _context.ProjectMembers.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.UserId == actorUserId)
            .Select(x => (ProjectMemberRole?)x.Role).SingleOrDefaultAsync(cancellationToken);
        bool admin = roles.Contains(SystemRoles.Admin, StringComparer.Ordinal);
        if (!admin && projectRole is null)
            return (null, null, Forbidden("You are not a member of this project."));
        return (project, new ActorAccess(admin,
            roles.Contains(SystemRoles.ProjectManager, StringComparer.Ordinal),
            roles.Contains(SystemRoles.Developer, StringComparer.Ordinal), projectRole), null);
    }

    private async Task<(List<Requirement> Requirements, string? Error)>
        ValidateRequirementsForCreateAsync(int projectId, IReadOnlyList<int> ids,
            CancellationToken cancellationToken)
    {
        List<Requirement> requirements = await LoadRequirementsAsync(projectId, ids, cancellationToken);
        if (requirements.Count != ids.Count)
            return ([], "Every linked requirement must belong to this project.");
        if (requirements.Any(x => x.Status != RequirementStatus.Approved))
            return ([], "Only Approved requirements may be linked to a design artifact.");
        return (requirements, null);
    }

    private async Task<(List<Requirement> Requirements, string? Error)>
        ValidateRequirementsForUpdateAsync(DesignArtifact artifact, IReadOnlyList<int> ids,
            CancellationToken cancellationToken)
    {
        List<Requirement> requirements = await LoadRequirementsAsync(
            artifact.ProjectId, ids, cancellationToken);
        if (requirements.Count != ids.Count)
            return ([], "Every linked requirement must belong to this project.");
        HashSet<int> existing = artifact.Requirements.Select(x => x.RequirementId).ToHashSet();
        if (requirements.Any(x => !existing.Contains(x.Id) && x.Status != RequirementStatus.Approved))
            return ([], "Only Approved requirements may be newly linked to a design artifact.");
        return (requirements, null);
    }

    private async Task<List<Requirement>> LoadRequirementsAsync(
        int projectId, IReadOnlyList<int> ids, CancellationToken cancellationToken) =>
        await _context.Requirements.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    private async Task<(List<DesignArtifact> Designs, string? Error)> ValidateDesignLinksAsync(
        int projectId, IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        List<DesignArtifact> designs = await _context.DesignArtifacts.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        return designs.Count == ids.Count
            ? (designs, null)
            : ([], "Every linked design artifact must belong to this project.");
    }

    private static string? ValidateDesign(string title, string description,
        DesignArtifactType type, IReadOnlyList<int>? requirementIds)
    {
        string? common = ValidateCommon(title, description);
        if (common is not null) return common;
        if (!Enum.IsDefined(type)) return "The design artifact type is invalid.";
        return ValidateIds(requirementIds, "requirement");
    }

    private static string? ValidateImplementation(string title, string description,
        ImplementationArtifactType type, string? sourceReference,
        IReadOnlyList<int>? designIds)
    {
        string? common = ValidateCommon(title, description);
        if (common is not null) return common;
        if (!Enum.IsDefined(type)) return "The implementation artifact type is invalid.";
        if (sourceReference?.Trim().Length > SourceReferenceMaximumLength)
            return $"Source reference must be {SourceReferenceMaximumLength} characters or fewer.";
        return ValidateIds(designIds, "design artifact");
    }

    private static string? ValidateCommon(string title, string description)
    {
        string normalizedTitle = title?.Trim() ?? string.Empty;
        string normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedTitle.Length == 0) return "Title is required.";
        if (normalizedTitle.Length > TitleMaximumLength)
            return $"Title must be {TitleMaximumLength} characters or fewer.";
        if (normalizedDescription.Length == 0) return "Description is required.";
        if (normalizedDescription.Length > DescriptionMaximumLength)
            return $"Description must be {DescriptionMaximumLength} characters or fewer.";
        return null;
    }

    private static string? ValidateIds(IReadOnlyList<int>? ids, string resource)
    {
        if (ids is null || ids.Count == 0)
            return $"Select at least one linked {resource}.";
        if (ids.Any(x => x <= 0)) return $"A linked {resource} is invalid.";
        if (ids.Distinct().Count() != ids.Count)
            return $"A linked {resource} was selected more than once.";
        return null;
    }

    private async Task<string> GetNextDesignIdentifierAsync(int projectId,
        CancellationToken cancellationToken) =>
        NextIdentifier("DES-", await _context.DesignArtifacts.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Identifier.StartsWith("DES-"))
            .Select(x => x.Identifier).ToListAsync(cancellationToken));

    private async Task<string> GetNextImplementationIdentifierAsync(int projectId,
        CancellationToken cancellationToken) =>
        NextIdentifier("IMP-", await _context.ImplementationArtifacts.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Identifier.StartsWith("IMP-"))
            .Select(x => x.Identifier).ToListAsync(cancellationToken));

    private static string NextIdentifier(string prefix, IEnumerable<string> identifiers)
    {
        int highest = 0;
        foreach (string identifier in identifiers)
            if (int.TryParse(identifier.AsSpan(prefix.Length), out int value) && value > highest)
                highest = value;
        return $"{prefix}{checked(highest + 1):D3}";
    }

    private ActivityLog Audit(string action, string resourceType, string resourceId,
        int projectId, string actorUserId, string description, object? values) => new()
        {
            ActorUserId = actorUserId,
            ProjectId = projectId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Description = description,
            NewValues = values is null ? null : JsonSerializer.Serialize(values),
            CreatedAt = _clock.UtcNow
        };

    private static bool CanManage(ActorAccess access) => access.IsAdmin ||
        access.HasProjectManagerRole && access.ProjectRole == ProjectMemberRole.ProjectManager;
    private static bool CanCreateImplementation(ActorAccess access) => CanManage(access) ||
        access.HasDeveloperRole && access.ProjectRole == ProjectMemberRole.Developer;
    private static bool CanEditImplementation(ActorAccess access, string ownerId, string actorId) =>
        CanManage(access) || access.HasDeveloperRole &&
        access.ProjectRole == ProjectMemberRole.Developer && ownerId == actorId;

    private static bool IsIdentifierCollision(DbUpdateException exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is SqlException sql && sql.Number is 2601 or 2627) return true;
            current = current.InnerException;
        }
        return false;
    }

    private void Detach(IEnumerable<object> entities)
    {
        foreach (object entity in entities.Reverse())
            _context.Entry(entity).State = EntityState.Detached;
    }

    private static string NormalizeSearch(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= TitleMaximumLength
            ? normalized : normalized[..TitleMaximumLength];
    }
    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
    private static ArtifactOperationResult Success(int id, string identifier, string message) =>
        ArtifactOperationResult.Success(id, identifier, message);
    private static ArtifactOperationResult Invalid(string message) =>
        ArtifactOperationResult.Failed(ArtifactOperationFailure.Validation, message);
    private static ArtifactOperationResult NotFound(string message) =>
        ArtifactOperationResult.Failed(ArtifactOperationFailure.NotFound, message);
    private static ArtifactOperationResult Forbidden(string message = "You are not authorized to manage this artifact.") =>
        ArtifactOperationResult.Failed(ArtifactOperationFailure.Forbidden, message);
    private static ArtifactOperationResult ReadOnly() =>
        ArtifactOperationResult.Failed(ArtifactOperationFailure.ReadOnly,
            "Archived projects are read-only.");
    private static ArtifactOperationResult Conflict(string message) =>
        ArtifactOperationResult.Failed(ArtifactOperationFailure.Conflict, message);

    private sealed record ActorAccess(bool IsAdmin, bool HasProjectManagerRole,
        bool HasDeveloperRole, ProjectMemberRole? ProjectRole);
}
