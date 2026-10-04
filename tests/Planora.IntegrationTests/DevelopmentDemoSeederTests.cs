using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;

namespace Planora.IntegrationTests;

public sealed class DevelopmentDemoSeederTests
{
    [Fact]
    public async Task DevelopmentSeederCreatesRepresentativeDataOnceAndHashesPasswords()
    {
        var (provider, environment, configuration) = CreateServices(Environments.Development);
        await using (provider)
        {
            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            await DevelopmentDemoSeeder.SeedAsync(provider, configuration, environment);
            await DevelopmentDemoSeeder.SeedAsync(provider, configuration, environment);

            await using var verifyScope = provider.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Equal(5, await db.Roles.CountAsync());
            Assert.Equal(5, await db.Users.CountAsync());
            Assert.Equal(2, await db.Projects.CountAsync());
            Assert.Equal(8, await db.ProjectMembers.CountAsync());
            Assert.Equal(4, await db.TaskItems.CountAsync());
            Assert.Equal(1, await db.Issues.CountAsync());
            Assert.Equal(1, await db.TaskComments.CountAsync());
            Assert.Equal(1, await db.Notifications.CountAsync());
            Assert.Equal(4, await db.RequirementTraces.CountAsync());
            var admin = await userManager.FindByEmailAsync("admin.demo@planora.local");
            Assert.NotNull(admin);
            Assert.NotEqual("DemoPassword2026!", admin.PasswordHash);
            Assert.True(await userManager.CheckPasswordAsync(admin, "DemoPassword2026!"));
        }
    }

    [Fact]
    public async Task DevelopmentSeederDoesNotRunOutsideDevelopment()
    {
        var (provider, environment, configuration) = CreateServices(Environments.Production);
        await using (provider)
        {
            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            await DevelopmentDemoSeeder.SeedAsync(provider, configuration, environment);
            await using var verifyScope = provider.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Users.ToListAsync());
            Assert.Empty(await db.Projects.ToListAsync());
        }
    }

    private static (ServiceProvider Provider, IHostEnvironment Environment, IConfiguration Configuration)
        CreateServices(string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["DevelopmentDemoSeed:Password"] = "DemoPassword2026!" })
            .Build();
        var collection = new ServiceCollection();
        collection.AddLogging();
        string databaseName = Guid.NewGuid().ToString();
        collection.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        collection.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        var environment = new TestEnvironment { EnvironmentName = environmentName };
        return (collection.BuildServiceProvider(), environment, configuration);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Planora.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
