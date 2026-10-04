using Planora.Infrastructure.Identity;
using Xunit;

namespace Planora.IntegrationTests;

public class IdentityConfigurationTests
{
    [Fact]
    public void BootstrapAdminOptions_ShouldUseCorrectSectionName()
    {
        Assert.Equal(
            "BootstrapAdmin",
            BootstrapAdminOptions.SectionName);
    }
}