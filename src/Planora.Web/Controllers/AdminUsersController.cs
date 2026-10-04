using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Identity;
using Planora.Application.Common.Identity;
using Planora.Application.Common.Security;
using Planora.Web.ViewModels.AdminUsers;
using Planora.Web.Presence;

namespace Planora.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("Admin/Users")]
public sealed class AdminUsersController : Controller
{
    private readonly IAdminUserService _adminUsers;
    private readonly UserPresenceRegistry _presence;

    public AdminUsersController(IAdminUserService adminUsers, UserPresenceRegistry presence)
    {
        _adminUsers = adminUsers;
        _presence = presence;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? role,
        string? status,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        bool? isDisabled = status?.ToLowerInvariant() switch
        {
            "enabled" => false,
            "disabled" => true,
            _ => null
        };

        AdminUserListResult result = await _adminUsers.GetUsersAsync(
            new AdminUserListQuery
            {
                Search = search,
                Role = SystemRoles.All.Contains(role, StringComparer.Ordinal) ? role : null,
                IsDisabled = isDisabled,
                Page = page,
                PageSize = 20
            },
            cancellationToken);
        foreach (var user in result.Users) user.IsOnline = _presence.IsOnline(user.Id);

        return View(new AdminUserIndexViewModel
        {
            Search = search,
            Role = role,
            Status = status,
            Result = result
        });
    }

    [HttpGet("Create")]
    public IActionResult Create() => View(new CreateAdminUserViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateAdminUserViewModel model,
        CancellationToken cancellationToken)
    {
        if (!SystemRoles.All.Contains(model.GlobalRole, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(model.GlobalRole), "Select a valid global role.");

        if (!ModelState.IsValid)
            return View(model);

        AdminUserOperationResult result = await _adminUsers.CreateAsync(
            new CreateAdminUserRequest
            {
                FullName = model.FullName,
                Email = model.Email,
                Password = model.Password,
                GlobalRole = model.GlobalRole,
                ActorUserId = CurrentUserId()
            },
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to create the user.");
            return View(model);
        }

        TempData["SuccessMessage"] = "User account created successfully.";
        return RedirectToAction(nameof(Details), new { id = result.UserId });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        AdminUserDetails? user = await _adminUsers.GetUserAsync(id, cancellationToken);
        if (user is null) return NotFound();
        user.IsOnline = _presence.IsOnline(user.Id);

        ViewData["CurrentUserId"] = CurrentUserId();
        return View(user);
    }

    [HttpPost("{id}/Role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(
        string id,
        string globalRole,
        CancellationToken cancellationToken)
    {
        AdminUserOperationResult result = await _adminUsers.ChangeGlobalRoleAsync(
            new ChangeAdminUserRoleRequest
            {
                UserId = id,
                GlobalRole = globalRole,
                ActorUserId = CurrentUserId()
            },
            cancellationToken);

        return RedirectWithResult(id, result, "Global role updated successfully.");
    }

    [HttpPost("{id}/Disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(string id, CancellationToken cancellationToken)
    {
        AdminUserOperationResult result = await _adminUsers.DisableAsync(
            StateRequest(id), cancellationToken);
        return RedirectWithResult(id, result, "User account disabled successfully.");
    }

    [HttpPost("{id}/Enable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(string id, CancellationToken cancellationToken)
    {
        AdminUserOperationResult result = await _adminUsers.EnableAsync(
            StateRequest(id), cancellationToken);
        return RedirectWithResult(id, result, "User account enabled successfully.");
    }

    [HttpPost("{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        AdminUserOperationResult result = await _adminUsers.DeleteAsync(
            StateRequest(id), cancellationToken);

        if (!result.Succeeded)
            return RedirectWithResult(id, result, string.Empty);

        TempData["SuccessMessage"] = "User account permanently deleted.";
        return RedirectToAction(nameof(Index));
    }

    private AdminUserStateRequest StateRequest(string userId) => new()
    {
        UserId = userId,
        ActorUserId = CurrentUserId()
    };

    private IActionResult RedirectWithResult(
        string userId,
        AdminUserOperationResult result,
        string successMessage)
    {
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.ErrorMessage ?? "The operation could not be completed.";

        return RedirectToAction(nameof(Details), new { id = userId });
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated Admin identity is required.");
}
