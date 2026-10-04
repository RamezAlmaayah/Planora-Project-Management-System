using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Planora.Infrastructure.Identity;
using Planora.Web.ViewModels.Profile;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ApplicationUser? user =
            await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            ProfileImagePath = user.ProfileImagePath
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(
        ProfileViewModel model)
    {
        ApplicationUser? user =
            await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        model.Email = user.Email ?? string.Empty;
        model.ProfileImagePath = user.ProfileImagePath;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.FullName = model.FullName.Trim();

        if (model.ProfileImage is not null &&
            model.ProfileImage.Length > 0)
        {
            const long maxSize = 5 * 1024 * 1024;

            if (model.ProfileImage.Length > maxSize)
            {
                ModelState.AddModelError(
                    nameof(model.ProfileImage),
                    "Profile image must be 5 MB or smaller.");

                return View(model);
            }

            string extension =
                Path.GetExtension(
                    model.ProfileImage.FileName)
                .ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    nameof(model.ProfileImage),
                    "Only JPG, PNG and WEBP images are allowed.");

                return View(model);
            }

            string folder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "profiles");

            Directory.CreateDirectory(folder);

            if (!string.IsNullOrWhiteSpace(
                    user.ProfileImagePath))
            {
                DeleteExistingImage(
                    user.ProfileImagePath);
            }

            string fileName =
                $"{Guid.NewGuid():N}{extension}";

            string fullPath =
                Path.Combine(
                    folder,
                    fileName);

            await using FileStream stream =
                System.IO.File.Create(fullPath);

            await model.ProfileImage
                .CopyToAsync(stream);

            user.ProfileImagePath =
                $"/uploads/profiles/{fileName}";
        }

        IdentityResult result =
            await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to update profile.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Profile updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemovePhoto()
    {
        ApplicationUser? user =
            await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        if (!string.IsNullOrWhiteSpace(
                user.ProfileImagePath))
        {
            DeleteExistingImage(
                user.ProfileImagePath);

            user.ProfileImagePath = null;

            await _userManager.UpdateAsync(user);
        }

        TempData["SuccessMessage"] =
            "Profile photo removed.";

        return RedirectToAction(nameof(Index));
    }

    private void DeleteExistingImage(
        string relativePath)
    {
        string relative =
            relativePath.TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

        string fullPath =
            Path.Combine(
                _environment.WebRootPath,
                relative);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
