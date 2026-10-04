using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planora.Application.Common.Security;

namespace Planora.Infrastructure.Identity;

public static class IdentityBootstrapper
{
    public static async Task SeedAdminAsync(
        IServiceProvider serviceProvider)
    {
        using var scope =
            serviceProvider.CreateScope();

        var logger =
            scope.ServiceProvider
                .GetRequiredService<
                    ILoggerFactory>()
                .CreateLogger(
                    "IdentityBootstrapper");

        var options =
            scope.ServiceProvider
                .GetRequiredService<
                    IOptions<BootstrapAdminOptions>>()
                .Value;

        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            logger.LogWarning(
                "Bootstrap admin is enabled but no email was configured.");

            return;
        }

        var email =
            options.Email.Trim();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<
                    RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(
                SystemRoles.Admin))
        {
            var roleResult =
                await roleManager.CreateAsync(
                    new IdentityRole(
                        SystemRoles.Admin));

            if (!roleResult.Succeeded)
            {
                logger.LogError(
                    "Unable to create the Admin role.");

                return;
            }
        }

        var existingUser =
            await userManager.FindByEmailAsync(
                email);

        if (existingUser is not null)
        {
            if (!await userManager.IsInRoleAsync(
                    existingUser,
                    SystemRoles.Admin))
            {
                var addRoleResult =
                    await userManager.AddToRoleAsync(
                        existingUser,
                        SystemRoles.Admin);

                if (!addRoleResult.Succeeded)
                {
                    logger.LogError(
                        "Unable to assign Admin role to bootstrap user {UserId}.",
                        existingUser.Id);

                    return;
                }
            }

            if (!existingUser.EmailConfirmed)
            {
                existingUser.EmailConfirmed = true;

                await userManager.UpdateAsync(
                    existingUser);
            }

            logger.LogInformation(
                "Bootstrap admin account is ready for user {UserId}.",
                existingUser.Id);

            return;
        }

        if (string.IsNullOrWhiteSpace(
            options.Password))
        {
            logger.LogWarning(
                "Bootstrap admin account does not exist and no password was configured.");

            return;
        }

        var admin =
            new ApplicationUser
            {
                FullName =
                    string.IsNullOrWhiteSpace(
                        options.FullName)
                        ? "Planora Administrator"
                        : options.FullName.Trim(),

                UserName = email,

                Email = email,

                EmailConfirmed = true,

                CreatedAt = DateTime.UtcNow
            };

        var createResult =
            await userManager.CreateAsync(
                admin,
                options.Password);

        if (!createResult.Succeeded)
        {
            foreach (var error
                     in createResult.Errors)
            {
                logger.LogError(
                    "Bootstrap admin creation failed: {Code}.",
                    error.Code);
            }

            return;
        }

        var roleAssignmentResult =
            await userManager.AddToRoleAsync(
                admin,
                SystemRoles.Admin);

        if (!roleAssignmentResult.Succeeded)
        {
            await userManager.DeleteAsync(
                admin);

            logger.LogError(
                "Bootstrap admin was created but Admin role assignment failed.");

            return;
        }

        logger.LogInformation(
            "Bootstrap admin account created successfully for user {UserId}.",
            admin.Id);
    }
}