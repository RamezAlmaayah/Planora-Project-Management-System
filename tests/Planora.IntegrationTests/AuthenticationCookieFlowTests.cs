using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planora.Application.Common.Security;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;

namespace Planora.IntegrationTests;

public sealed class AuthenticationCookieFlowTests
{
    private const string CookieName = "__Host-Planora.Auth";

    [Fact]
    public async Task AdminLoginReachesDashboardAndLogoutAllowsFreshLogin()
    {
        await using var factory = new PlanoraWebFactory();
        await factory.CreateConfirmedUserAsync();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        HttpResponseMessage landing = await client.GetAsync("/");
        string landingHtml = await landing.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, landing.StatusCode);
        Assert.Contains("SMART PROJECT MANAGEMENT", landingHtml, StringComparison.Ordinal);

        HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string loginHtml = await loginPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);

        HttpResponseMessage login = await client.PostAsync(
            "/Account/Login",
            Form(
                loginHtml,
                ("Email", PlanoraWebFactory.Email),
                ("Password", PlanoraWebFactory.Password),
                ("RememberMe", "false")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), header =>
            header.StartsWith($"{CookieName}=", StringComparison.Ordinal) &&
            !header.StartsWith($"{CookieName}=;", StringComparison.Ordinal));

        string homeTarget = RedirectTarget(login);
        Assert.Equal("/", homeTarget);
        HttpResponseMessage authenticatedHome = await client.GetAsync(homeTarget);
        Assert.Equal(HttpStatusCode.Redirect, authenticatedHome.StatusCode);
        Assert.StartsWith("/Dashboard", RedirectTarget(authenticatedHome), StringComparison.Ordinal);

        HttpResponseMessage dashboard = await client.GetAsync(RedirectTarget(authenticatedHome));
        string dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains("<title>Dashboard - Planora</title>", dashboardHtml, StringComparison.Ordinal);
        Assert.Contains("Admin", dashboardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("landing-hero", dashboardHtml, StringComparison.Ordinal);

        HttpResponseMessage authenticatedLogin = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, authenticatedLogin.StatusCode);
        Assert.Contains("no-store", authenticatedLogin.Headers.CacheControl?.ToString(),
            StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage authenticatedRoot = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, authenticatedRoot.StatusCode);
        Assert.StartsWith("/Dashboard", RedirectTarget(authenticatedRoot), StringComparison.Ordinal);
        Assert.Contains("action=\"/Account/Logout\"", dashboardHtml, StringComparison.Ordinal);

        HttpResponseMessage logout = await client.PostAsync(
            "/Account/Logout",
            Form(dashboardHtml));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/Account/Login", logout.Headers.Location?.OriginalString);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), header =>
            header.StartsWith($"{CookieName}=;", StringComparison.Ordinal) &&
            (header.Contains("expires=", StringComparison.OrdinalIgnoreCase) ||
             header.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)));

        HttpResponseMessage freshLoginPage = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, freshLoginPage.StatusCode);
        Assert.Null(freshLoginPage.Headers.Location);

        HttpResponseMessage protectedPage = await client.GetAsync("/Dashboard/Index");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Equal("/Account/Login", protectedPage.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task LoginGetResponsesAreNotStoredAcrossAuthenticationChanges()
    {
        await using var factory = new PlanoraWebFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        HttpResponseMessage response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidCredentialsRemainOnLoginWithSafeError()
    {
        await using var factory = new PlanoraWebFactory();
        await factory.CreateConfirmedUserAsync();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string loginHtml = await loginPage.Content.ReadAsStringAsync();

        HttpResponseMessage response = await client.PostAsync(
            "/Account/Login",
            Form(
                loginHtml,
                ("Email", PlanoraWebFactory.Email),
                ("Password", "WrongPassword!234"),
                ("RememberMe", "false")));
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password.", html, StringComparison.Ordinal);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
            cookies.Any(header =>
                header.StartsWith($"{CookieName}=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task LocalReturnUrlIsHonoredAfterSuccessfulLogin()
    {
        await using var factory = new PlanoraWebFactory();
        await factory.CreateConfirmedUserAsync();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        const string url = "/Account/Login?returnUrl=%2FDashboard%2FIndex";
        HttpResponseMessage loginPage = await client.GetAsync(url);

        HttpResponseMessage login = await client.PostAsync(
            url,
            Form(
                await loginPage.Content.ReadAsStringAsync(),
                ("Email", PlanoraWebFactory.Email),
                ("Password", PlanoraWebFactory.Password),
                ("RememberMe", "false")));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Dashboard/Index", RedirectTarget(login));
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync(RedirectTarget(login))).StatusCode);
    }

    [Fact]
    public async Task ExternalReturnUrlIsIgnoredSafely()
    {
        await using var factory = new PlanoraWebFactory();
        await factory.CreateConfirmedUserAsync();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        const string url = "/Account/Login?returnUrl=https%3A%2F%2Fevil.example%2Fsteal";
        HttpResponseMessage loginPage = await client.GetAsync(url);

        HttpResponseMessage login = await client.PostAsync(
            url,
            Form(
                await loginPage.Content.ReadAsStringAsync(),
                ("Email", PlanoraWebFactory.Email),
                ("Password", PlanoraWebFactory.Password),
                ("RememberMe", "false")));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.DoesNotContain("evil.example", RedirectTarget(login),
            StringComparison.OrdinalIgnoreCase);
        HttpResponseMessage home = await client.GetAsync(RedirectTarget(login));
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.StartsWith("/Dashboard", RedirectTarget(home), StringComparison.Ordinal);
    }

    private static FormUrlEncodedContent Form(
        string html,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value);
        fields["__RequestVerificationToken"] = AntiforgeryToken(html);
        return new FormUrlEncodedContent(fields);
    }

    private static string AntiforgeryToken(string html)
    {
        Match match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The response did not contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string RedirectTarget(HttpResponseMessage response)
    {
        Uri location = Assert.IsType<Uri>(response.Headers.Location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private sealed class PlanoraWebFactory : WebApplicationFactory<Program>
    {
        public const string Email = "cookie-flow@example.test";
        public const string Password = "Planora!2345";
        private readonly string _databaseName = $"auth-flow-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\mssqllocaldb;Database=PlanoraAuthFlow;Trusted_Connection=True");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\mssqllocaldb;Database=PlanoraAuthFlow;Trusted_Connection=True"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }

        public async Task CreateConfirmedUserAsync()
        {
            using IServiceScope scope = Services.CreateScope();
            UserManager<ApplicationUser> users =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager<IdentityRole> roles =
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(SystemRoles.Admin))
                Assert.True((await roles.CreateAsync(
                    new IdentityRole(SystemRoles.Admin))).Succeeded);

            var user = new ApplicationUser
            {
                UserName = Email,
                Email = Email,
                FullName = "Cookie Flow User",
                EmailConfirmed = true
            };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, SystemRoles.Admin)).Succeeded);
        }
    }
}
