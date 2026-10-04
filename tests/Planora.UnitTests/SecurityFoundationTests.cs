using Planora.Application.Common.Security;
using Xunit;

namespace Planora.UnitTests;

public class SecurityFoundationTests
{
    [Fact]
    public void SystemRoles_ShouldContainAllRequiredRoles()
    {
        Assert.Contains(SystemRoles.Admin, SystemRoles.All);
        Assert.Contains(SystemRoles.ProjectManager, SystemRoles.All);
        Assert.Contains(SystemRoles.ScrumMaster, SystemRoles.All);
        Assert.Contains(SystemRoles.Developer, SystemRoles.All);
        Assert.Contains(SystemRoles.QaTester, SystemRoles.All);

        Assert.Equal(5, SystemRoles.All.Length);
    }
}