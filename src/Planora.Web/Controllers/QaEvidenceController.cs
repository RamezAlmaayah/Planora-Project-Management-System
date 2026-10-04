using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Application.Abstractions.Tasks;
using Planora.Domain.Entities;
using Planora.Infrastructure.Persistence;

namespace Planora.Web.Controllers;

[Authorize]
[Route("QaEvidence")]
public sealed class QaEvidenceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IQaEvidenceStorage _storage;
    private readonly ILogger<QaEvidenceController> _logger;

    public QaEvidenceController(ApplicationDbContext db, IQaEvidenceStorage storage,
        ILogger<QaEvidenceController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var file = await FindAsync(id, cancellationToken);
        if (file is null) return NotFound();
        if (!await CanViewAsync(file, cancellationToken)) return Forbid();
        try
        {
            var stream = await _storage.OpenReadAsync(file.StorageKey, cancellationToken);
            if (stream is null) return NotFound();
            return File(stream, ContentType(file.Extension), file.OriginalFileName,
                enableRangeProcessing: file.Extension == ".mp4");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read QA evidence {EvidenceId}.", id);
            return NotFound();
        }
    }

    [HttpPost("{id:int}/Delete")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var file = await _db.QaEvidenceFiles.Include(item => item.QaReview)
            .ThenInclude(review => review.TaskItem).ThenInclude(task => task.SprintBacklogItem)
            .ThenInclude(link => link.Sprint)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (file is null) return NotFound();
        _db.QaEvidenceFiles.Remove(file);
        _db.ActivityLogs.Add(new ActivityLog
        {
            ActorUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            ProjectId = file.QaReview.TaskItem.SprintBacklogItem.Sprint.ProjectId,
            Action = "QaEvidenceDeleted",
            ResourceType = nameof(QaEvidence),
            ResourceId = file.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Description = "QA evidence removed by an administrator.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        try { await _storage.DeleteAsync(file.StorageKey, cancellationToken); }
        catch (Exception ex) { _logger.LogError(ex, "Could not remove QA evidence storage object {EvidenceId}.", id); }
        return RedirectToAction("Details", "Tasks", new
        {
            projectId = file.QaReview.TaskItem.SprintBacklogItem.Sprint.ProjectId,
            sprintId = file.QaReview.TaskItem.SprintBacklogItem.SprintId,
            id = file.QaReview.TaskItemId
        });
    }

    private Task<EvidenceAccess?> FindAsync(int id, CancellationToken ct) =>
        _db.QaEvidenceFiles.AsNoTracking().Where(file => file.Id == id)
            .Select(file => new EvidenceAccess
            {
                Id = file.Id,
                QaReviewId = file.QaReviewId,
                TaskId = file.QaReview.TaskItemId,
                ProjectId = file.QaReview.TaskItem.SprintBacklogItem.Sprint.ProjectId,
                AssignedUserId = file.QaReview.TaskItem.AssignedUserId,
                UploadedByUserId = file.UploadedByUserId,
                StorageKey = file.StorageKey,
                OriginalFileName = file.OriginalFileName,
                Extension = file.Extension
            }).SingleOrDefaultAsync(ct);

    private async Task<bool> CanViewAsync(EvidenceAccess file, CancellationToken ct)
    {
        if (User.IsInRole(SystemRoles.Admin)) return true;
        string? userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return false;
        if (file.AssignedUserId == userId && User.IsInRole(SystemRoles.Developer)) return true;
        bool managementMember = await _db.ProjectMembers.AsNoTracking().AnyAsync(member =>
            member.ProjectId == file.ProjectId && member.UserId == userId &&
            (member.Role == Domain.Enums.ProjectMemberRole.ProjectManager ||
             member.Role == Domain.Enums.ProjectMemberRole.ScrumMaster), ct);
        if (managementMember) return true;
        return file.UploadedByUserId == userId && User.IsInRole(SystemRoles.QaTester) &&
            await _db.ProjectMembers.AsNoTracking().AnyAsync(member =>
                member.ProjectId == file.ProjectId && member.UserId == userId &&
                member.Role == Domain.Enums.ProjectMemberRole.QaTester, ct);
    }

    private static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
        ".mp4" => "video/mp4", ".pdf" => "application/pdf",
        _ => "text/plain"
    };

    private sealed class EvidenceAccess
    {
        public int Id { get; init; }
        public int QaReviewId { get; init; }
        public int TaskId { get; init; }
        public int ProjectId { get; init; }
        public string? AssignedUserId { get; init; }
        public string UploadedByUserId { get; init; } = string.Empty;
        public string StorageKey { get; init; } = string.Empty;
        public string OriginalFileName { get; init; } = string.Empty;
        public string Extension { get; init; } = string.Empty;
    }
}
