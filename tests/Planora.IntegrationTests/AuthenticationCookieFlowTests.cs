using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planora.Application.Common.Security;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Web.Security;
using Planora.Application.Abstractions.Common;

namespace Planora.IntegrationTests;

public sealed class AuthenticationCookieFlowTests
{
    private const string CookieName = "__Host-Planora.Auth";

    [Fact]
    public async Task RegistrationConfirmationRecoveryAndResetCompleteWithProtectedForms()
    {
        string database = $"lifecycle-{Guid.NewGuid():N}";
        await using var factory = new PlanoraWebFactory(database);
        using (var scope = factory.Services.CreateScope())
        {
            // SQL migrations seed this role; the isolated InMemory fixture does not.
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(SystemRoles.Developer))).Succeeded);
        }
        using var client = factory.CreateClient(ClientOptions());
        const string email = "lifecycle@example.test";
        const string initial = "Planora!2345";
        const string replacement = "Planora!6789";
        async Task<HttpResponseMessage> Post(string path, params (string Name, string Value)[] fields)
        {
            string html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();
            return await client.PostAsync(path, Form(html, fields));
        }
        using var registration = await Post("/Account/Register", ("FullName", "Lifecycle User"),
            ("Email", email), ("Password", initial), ("ConfirmPassword", initial));
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        Assert.Equal("/Account/RegisterPending", RedirectTarget(registration));
        Assert.Single(factory.Mail.Bodies);

        using var pendingLogin = await Post("/Account/Login", ("Email", email), ("Password", initial));
        Assert.Equal(HttpStatusCode.OK, pendingLogin.StatusCode);
        string confirmation = EmailLink(factory.Mail.Bodies.Single(), "ConfirmEmail");
        using var confirmed = await client.GetAsync(confirmation);
        Assert.Contains("Email verified", await confirmed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var duplicate = await Post("/Account/Register", ("FullName", "Lifecycle User"),
            ("Email", email), ("Password", initial), ("ConfirmPassword", initial));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Unable to create an account", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var unknownRecovery = await Post("/Account/ForgotPassword", ("Email", "unknown@example.test"));
        using var knownRecovery = await Post("/Account/ForgotPassword", ("Email", email));
        Assert.Equal(unknownRecovery.StatusCode, knownRecovery.StatusCode);
        Assert.Equal(RedirectTarget(unknownRecovery), RedirectTarget(knownRecovery));
        Assert.Equal(2, factory.Mail.Bodies.Count);

        string reset = EmailLink(factory.Mail.Bodies.Last(), "ResetPassword");
        var query = QueryHelpers.ParseQuery(new Uri("https://localhost" + reset).Query);
        using var changed = await Post(reset, ("Email", email), ("Code", query["code"].ToString()),
            ("Password", replacement), ("ConfirmPassword", replacement));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Contains("Password updated", await changed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        // Use another instance so the replay check is not masked by the recovery rate limit.
        await using var replayHost = new PlanoraWebFactory(database);
        using var replayClient = replayHost.CreateClient(ClientOptions());
        string replayHtml = await (await replayClient.GetAsync(reset)).Content.ReadAsStringAsync();
        using var reused = await replayClient.PostAsync(reset, Form(replayHtml,
            ("Email", email), ("Code", query["code"].ToString()),
            ("Password", initial), ("ConfirmPassword", initial)));
        Assert.Contains("Reset link invalid", await reused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var oldPassword = await Post("/Account/Login", ("Email", email), ("Password", initial));
        Assert.Equal(HttpStatusCode.OK, oldPassword.StatusCode);
        using var login = await Post("/Account/Login", ("Email", email), ("Password", replacement));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var home = await client.GetAsync(RedirectTarget(login));
        Assert.StartsWith("/Dashboard", RedirectTarget(home), StringComparison.Ordinal);
        using var dashboard = await client.GetAsync(RedirectTarget(home));
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        using var reload = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.OK, reload.StatusCode);
        string dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        using var logout = await client.PostAsync("/Account/Logout", Form(dashboardHtml));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var protectedPage = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    [Theory]
    [InlineData("/Account/AccessDenied", HttpStatusCode.Forbidden)]
    [InlineData("/not-found-audit", HttpStatusCode.NotFound)]
    public async Task FriendlyGetErrorPagesPreserveTheirStatus(string path, HttpStatusCode expected)
    {
        await using var factory = new PlanoraWebFactory();
        using var client = factory.CreateClient(ClientOptions());
        using var response = await client.GetAsync(path);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    private static string EmailLink(string body, string action)
    {
        var match = Regex.Match(body, "href=\"(https://localhost/Account/" + action + "[^\"]+)\"");
        Assert.True(match.Success, "The captured test email must contain the expected account action.");
        return new Uri(WebUtility.HtmlDecode(match.Groups[1].Value)).PathAndQuery;
    }

    private sealed class CapturedEmail : IEmailService
    {
        public List<string> Bodies { get; } = [];
        public Task SendEmailAsync(string toEmail, string subject, string htmlBody,
            CancellationToken cancellationToken = default, EmailInlineResource? inlineResource = null)
        {
            Bodies.Add(htmlBody);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AuthStylesheetReturnsCssContentInsteadOfEmptySuccess()
    {
        await using var factory = new PlanoraWebFactory();
        using var client = factory.CreateClient(ClientOptions());
        using var response = await client.GetAsync("/css/auth.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(".auth-v3-input-wrap", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    public async Task AntiforgeryFormFromOneInstanceCanBeSubmittedToAnother(string path)
    {
        string database = $"shared-form-{Guid.NewGuid():N}";
        string certificate = TestCertificate();
        await using var first = new PlanoraWebFactory(database, certificate);
        await using var second = new PlanoraWebFactory(database, certificate);
        using var browser = first.CreateClient(ClientOptions());
        using var otherInstance = second.CreateClient(ClientOptions());
        using var page = await browser.GetAsync(path);
        string html = await page.Content.ReadAsStringAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = Form(html) };
        request.Headers.Add("Cookie", Cookies(page));
        using var result = await otherInstance.SendAsync(request);

        // Empty required fields reach MVC model validation after successful antiforgery validation.
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task SharedEncryptedKeysKeepLoginCookieValidAcrossInstancesAndRestarts()
    {
        string database = $"shared-login-{Guid.NewGuid():N}";
        string certificate = TestCertificate();
        await using var first = new PlanoraWebFactory(database, certificate);
        await using var second = new PlanoraWebFactory(database, certificate);
        await first.CreateConfirmedUserAsync();
        using var browser = first.CreateClient(ClientOptions());
        using var otherInstance = second.CreateClient(ClientOptions());
        using var page = await browser.GetAsync("/Account/Login");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = Form(await page.Content.ReadAsStringAsync(),
                ("Email", PlanoraWebFactory.Email), ("Password", PlanoraWebFactory.Password))
        };
        request.Headers.Add("Cookie", Cookies(page));
        using var login = await otherInstance.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // A third independently created host represents a replacement container.
        await using var replacement = new PlanoraWebFactory(database, certificate);
        using var replacementBrowser = replacement.CreateClient(ClientOptions());
        using var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/Dashboard");
        dashboardRequest.Headers.Add("Cookie", Cookies(login));
        using var dashboard = await replacementBrowser.SendAsync(dashboardRequest);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);

        using var scope = replacement.Services.CreateScope();
        var keys = await scope.ServiceProvider.GetRequiredService<DataProtectionKeyDbContext>()
            .DataProtectionKeys.ToListAsync();
        Assert.NotEmpty(keys);
        Assert.True(keys.All(key => key.Xml?.Contains("encryptedSecret", StringComparison.Ordinal) == true),
            "Shared keys must be encrypted at rest.");
    }

    private static WebApplicationFactoryClientOptions ClientOptions() => new()
    {
        AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
    };

    private static string Cookies(HttpResponseMessage response) =>
        string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));

    private static string TestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Planora Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    public async Task PostWithoutAntiforgeryPreservesBadRequestInsteadOfMethodNotAllowed(string path)
    {
        await using var factory = new PlanoraWebFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.PostAsync(path,
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

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
        private readonly string _databaseName;
        private readonly string? _certificate;
        public CapturedEmail Mail { get; } = new();

        public PlanoraWebFactory(string? databaseName = null, string? certificate = null)
        {
            _databaseName = databaseName ?? $"auth-flow-{Guid.NewGuid():N}";
            _certificate = certificate;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("DataProtection:PersistToSqlServer", (_certificate is not null).ToString());
            if (_certificate is not null)
                builder.UseSetting("DataProtection:CertificateBase64", _certificate);
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\mssqllocaldb;Database=PlanoraAuthFlow;Trusted_Connection=True");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\mssqllocaldb;Database=PlanoraAuthFlow;Trusted_Connection=True",
                    ["DataProtection:PersistToSqlServer"] = (_certificate is not null).ToString(),
                    ["DataProtection:CertificateBase64"] = _certificate
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(Mail);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                if (_certificate is not null)
                {
                    services.RemoveAll<DbContextOptions<DataProtectionKeyDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<DataProtectionKeyDbContext>>();
                    services.RemoveAll<DataProtectionKeyDbContext>();
                    services.AddDbContext<DataProtectionKeyDbContext>(options =>
                        options.UseInMemoryDatabase(_databaseName + "-keys"));
                }
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
