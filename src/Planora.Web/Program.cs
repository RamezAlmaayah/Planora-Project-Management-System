using QuestPDF.Infrastructure;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Infrastructure;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Web.Authorization;
using Planora.Web.Identity;
using Planora.Web.Middleware;
using Planora.Web.Presence;
using Planora.Web.Security;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

bool isVercel = builder.Configuration["VERCEL"] == "1";

if (builder.Environment.IsProduction())
{
    var connection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Production requires ConnectionStrings__DefaultConnection for a remote SQL Server database.");

    var sql = new SqlConnectionStringBuilder(connection);
    string server = sql.DataSource;
    if (server.Contains("localdb", StringComparison.OrdinalIgnoreCase)
        || server.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || server.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || server.StartsWith(@".\", StringComparison.OrdinalIgnoreCase)
        || server is "." or "(local)")
        throw new InvalidOperationException("Production cannot use a local SQL Server database.");
}

if (isVercel)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        // Vercel is the sole ingress to this container; its proxy addresses are dynamic.
        // Never enable this trust policy on a publicly reachable standalone container.
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddPlanoraDataProtection(builder.Configuration,
    requireSharedKeys: isVercel && builder.Environment.IsProduction());

// =========================================================
// QUESTPDF
// =========================================================

QuestPDF.Settings.License =
    LicenseType.Community;

// =========================================================
// SERILOG
// =========================================================

var logConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override(
        "Microsoft",
        LogEventLevel.Warning)
    .MinimumLevel.Override(
        "Microsoft.AspNetCore",
        LogEventLevel.Warning)
    .MinimumLevel.Override(
        "Microsoft.AspNetCore.Mvc.ViewFeatures.Filters",
        LogEventLevel.Information)
    .MinimumLevel.Override(
        "Microsoft.EntityFrameworkCore",
        LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console();

if (builder.Configuration.GetValue("Logging:FileEnabled", true))
{
    logConfiguration.WriteTo.File(
        path: "logs/planora-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true);
}

Log.Logger = logConfiguration.CreateLogger();

builder.Host.UseSerilog();

// =========================================================
// MVC
// =========================================================

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<UserPresenceRegistry>();


// =========================================================
// INFRASTRUCTURE
// =========================================================

builder.Services.AddInfrastructure(
    builder.Configuration);

// =========================================================
// ASP.NET CORE IDENTITY
// =========================================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // User
        options.User.RequireUniqueEmail = true;

        // Password
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Lockout
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        // Email confirmation
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<PlanoraSignInManager>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// =========================================================
// SECURE AUTHENTICATION COOKIE
// =========================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name =
        "__Host-Planora.Auth";

    options.Cookie.HttpOnly = true;

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;

    options.Cookie.SameSite =
        SameSiteMode.Lax;

    options.Cookie.Path = "/";

    options.ExpireTimeSpan =
        TimeSpan.FromMinutes(30);

    options.SlidingExpiration = false;

    options.LoginPath =
        "/Account/Login";

    options.LogoutPath =
        "/Account/Logout";

    options.AccessDeniedPath =
        "/Account/AccessDenied";

    options.ReturnUrlParameter =
        "returnUrl";
});

// =========================================================
// AUTHORIZATION
// =========================================================

builder.Services.AddAuthorization(options =>
{
    // -----------------------------------------------------
    // Global Role Policies
    // -----------------------------------------------------

    options.AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy =>
            policy.RequireRole(
                SystemRoles.Admin));

    options.AddPolicy(
        AuthorizationPolicies.CanManageProjects,
        policy =>
            policy.RequireRole(
                SystemRoles.Admin,
                SystemRoles.ProjectManager));

    options.AddPolicy(
        AuthorizationPolicies.CanManageScrum,
        policy =>
            policy.RequireRole(
                SystemRoles.Admin,
                SystemRoles.ProjectManager,
                SystemRoles.ScrumMaster));

    options.AddPolicy(
        AuthorizationPolicies.CanPerformQa,
        policy =>
            policy.RequireRole(
                SystemRoles.Admin,
                SystemRoles.QaTester));

    options.AddPolicy(
        AuthorizationPolicies.CanDevelop,
        policy =>
            policy.RequireRole(
                SystemRoles.Admin,
                SystemRoles.Developer));

    options.AddPolicy(
        AuthorizationPolicies.CanUseAi,
        policy =>
            policy.RequireRole(
                SystemRoles.Admin,
                SystemRoles.ProjectManager,
                SystemRoles.ScrumMaster));

    // -----------------------------------------------------
    // Project Membership
    // -----------------------------------------------------

    options.AddPolicy(
        AuthorizationPolicies.ProjectMember,
        policy =>
            policy.AddRequirements(
                new ProjectMemberRequirement()));

    // -----------------------------------------------------
    // Project-Level Roles
    // -----------------------------------------------------

    options.AddPolicy(
        AuthorizationPolicies.ProjectManagement,
        policy =>
            policy.AddRequirements(
                new ProjectRoleRequirement(
                    ProjectMemberRole.ProjectManager)));

    options.AddPolicy(
        AuthorizationPolicies.ScrumManagement,
        policy =>
            policy.AddRequirements(
                new ProjectRoleRequirement(
                    ProjectMemberRole.ProjectManager,
                    ProjectMemberRole.ScrumMaster)));

    options.AddPolicy(
        AuthorizationPolicies.ProjectDevelopment,
        policy =>
            policy.AddRequirements(
                new ProjectRoleRequirement(
                    ProjectMemberRole.Developer)));

    options.AddPolicy(
        AuthorizationPolicies.ProjectQa,
        policy =>
            policy.AddRequirements(
                new ProjectRoleRequirement(
                    ProjectMemberRole.QaTester)));
});

// =========================================================
// AUTHORIZATION HANDLERS
// =========================================================

builder.Services.AddScoped<
    IAuthorizationHandler,
    ProjectMemberAuthorizationHandler>();

builder.Services.AddScoped<
    IAuthorizationHandler,
    ProjectRoleAuthorizationHandler>();

// =========================================================
// RATE LIMITING
// =========================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // -----------------------------------------------------
    // Login - 5 requests / minute / IP
    // -----------------------------------------------------

    options.AddPolicy(
        RateLimitPolicies.Login,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueLimit = 0,

                        AutoReplenishment = true
                    }));

    // -----------------------------------------------------
    // Registration - 3 requests / 10 minutes / IP
    // -----------------------------------------------------

    options.AddPolicy(
        RateLimitPolicies.Registration,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,

                        Window =
                            TimeSpan.FromMinutes(10),

                        QueueLimit = 0,

                        AutoReplenishment = true
                    }));

    // -----------------------------------------------------
    // Password Recovery - 3 requests / 10 minutes / IP
    // -----------------------------------------------------

    options.AddPolicy(
        RateLimitPolicies.PasswordRecovery,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,

                        Window =
                            TimeSpan.FromMinutes(10),

                        QueueLimit = 0,

                        AutoReplenishment = true
                    }));

    // AI generation - 3 requests / 5 minutes / authenticated user
    options.AddPolicy(
        RateLimitPolicies.AiRequirementGeneration,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    // -----------------------------------------------------
    // Custom 429 Page
    // -----------------------------------------------------

    options.OnRejected = async (
        context,
        cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        context.HttpContext.Response.ContentType =
            "text/html; charset=utf-8";

        var filePath = Path.Combine(
            builder.Environment.ContentRootPath,
            "wwwroot",
            "errors",
            "429.html");

        if (File.Exists(filePath))
        {
            var html =
                await File.ReadAllTextAsync(
                    filePath,
                    cancellationToken);

            await context.HttpContext.Response.WriteAsync(
                html,
                cancellationToken);

            return;
        }

        await context.HttpContext.Response.WriteAsync(
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Too Many Requests - Planora</title>
            </head>
            <body>
                <h1>Too many requests</h1>
                <p>Please wait and try again.</p>
            </body>
            </html>
            """,
            cancellationToken);
    };
});

// =========================================================
// BUILD APPLICATION
// =========================================================

var app = builder.Build();

if (isVercel)
{
    app.UseForwardedHeaders();
    app.Logger.LogWarning("Vercel local uploads, caches and presence are instance-local and are not durable across replacements or shared across instances.");
    if (app.Environment.IsProduction())
        app.Logger.LogInformation("Data Protection uses the shared SQL Server key ring with certificate encryption at rest.");
}

// =========================================================
// BOOTSTRAP ADMIN
// =========================================================

await IdentityBootstrapper.SeedAdminAsync(
    app.Services);

if (app.Environment.IsDevelopment())
{
    await DevelopmentDemoSeeder.SeedAsync(
        app.Services,
        app.Configuration,
        app.Environment);
}

// =========================================================
// PRODUCTION SECURITY
// =========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// =========================================================
// HTTPS
// =========================================================

app.UseHttpsRedirection();

// =========================================================
// SECURITY HEADERS
// =========================================================

app.UseMiddleware<SecurityHeadersMiddleware>();

// =========================================================
// GLOBAL EXCEPTION HANDLING
// =========================================================

app.UseMiddleware<GlobalExceptionMiddleware>();

// Re-execution preserves the request method. The display-only error endpoint is
// GET-only, so re-executing a rejected POST would replace its status with 405.
// Keep unsafe-method failures (including antiforgery 400s) intact.
app.UseWhen(
    context => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method),
    branch => branch.UseStatusCodePagesWithReExecute(
        "/Error/HandleStatusCode",
        "?code={0}"));

// =========================================================
// ROUTING
// =========================================================

app.UseRouting();

// =========================================================
// SERILOG REQUEST LOGGING
// =========================================================

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    options.GetLevel = (
        httpContext,
        elapsed,
        exception) =>
    {
        if (exception is not null
            || httpContext.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        if (httpContext.Response.StatusCode >= 400)
        {
            return LogEventLevel.Warning;
        }

        return LogEventLevel.Information;
    };
});

// =========================================================
// RATE LIMITING
// =========================================================

app.UseRateLimiter();

// =========================================================
// AUTHENTICATION
// =========================================================

app.UseAuthentication();

// =========================================================
// AUTHORIZATION
// =========================================================

app.UseAuthorization();

// =========================================================
// STATIC ASSETS
// =========================================================

app.MapStaticAssets();

// =========================================================
// MVC ROUTING
// =========================================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<AdminPresenceHub>("/hubs/admin-presence", options =>
    options.CloseOnAuthenticationExpiration = true);

// =========================================================
// APPLICATION START
// =========================================================

try
{
    Log.Information(
        "Starting Planora application.");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(
        ex,
        "Planora application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

