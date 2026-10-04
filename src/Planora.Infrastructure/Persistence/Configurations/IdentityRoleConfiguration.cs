using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Application.Common.Security;

namespace Planora.Infrastructure.Persistence.Configurations;

public class IdentityRoleConfiguration
    : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> builder)
    {
        builder.HasData(
            new IdentityRole
            {
                Id = "role-admin",
                Name = SystemRoles.Admin,
                NormalizedName = SystemRoles.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "role-admin-v1"
            },
            new IdentityRole
            {
                Id = "role-project-manager",
                Name = SystemRoles.ProjectManager,
                NormalizedName = SystemRoles.ProjectManager.ToUpperInvariant(),
                ConcurrencyStamp = "role-project-manager-v1"
            },
            new IdentityRole
            {
                Id = "role-scrum-master",
                Name = SystemRoles.ScrumMaster,
                NormalizedName = SystemRoles.ScrumMaster.ToUpperInvariant(),
                ConcurrencyStamp = "role-scrum-master-v1"
            },
            new IdentityRole
            {
                Id = "role-developer",
                Name = SystemRoles.Developer,
                NormalizedName = SystemRoles.Developer.ToUpperInvariant(),
                ConcurrencyStamp = "role-developer-v1"
            },
            new IdentityRole
            {
                Id = "role-qa-tester",
                Name = SystemRoles.QaTester,
                NormalizedName = SystemRoles.QaTester.ToUpperInvariant(),
                ConcurrencyStamp = "role-qa-tester-v1"
            }
        );
    }
}