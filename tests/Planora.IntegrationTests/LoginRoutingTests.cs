using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.Account;

namespace Planora.IntegrationTests;

public sealed class LoginRoutingTests
{
    [Fact]
    public void AnonymousLoginGetReturnsLoginPageWithOkStatus()
    {
        var controller = CreateController(authenticated: false);
        var action = typeof(AccountController).GetMethod(
            nameof(AccountController.Login),
            [typeof(string)]);

        Assert.NotNull(action);
        Assert.NotNull(action.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());

        var result = Assert.IsType<ViewResult>(controller.Login());

        Assert.IsType<LoginViewModel>(result.Model);
        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        Assert.Null(result.ViewName);
    }

    [Fact]
    public void AnonymousLoginGetDoesNotRedirectToLandingPage()
    {
        var controller = CreateController(authenticated: false);

        var result = controller.Login();

        Assert.IsNotType<RedirectToActionResult>(result);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void LandingLoginLinkTargetsAccountLoginRoute()
    {
        var repositoryRoot = GetRepositoryRoot();
        var layoutPath = Path.Combine(
            repositoryRoot,
            "src",
            "Planora.Web",
            "Views",
            "Shared",
            "_PublicLayout.cshtml");
        var layout = File.ReadAllText(layoutPath);

        Assert.Contains("asp-controller=\"Account\"", layout, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Login\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticatedLoginGetKeepsExistingRedirectWithoutLoopingToLogin()
    {
        var controller = CreateController(authenticated: true);

        var result = Assert.IsType<RedirectToActionResult>(controller.Login());

        Assert.Equal("Home", result.ControllerName);
        Assert.Equal("Index", result.ActionName);
        Assert.NotEqual(nameof(AccountController.Login), result.ActionName);
    }

    [Theory]
    [InlineData(nameof(AccountController.Register), typeof(RegisterViewModel))]
    [InlineData(nameof(AccountController.ForgotPassword), typeof(ForgotPasswordViewModel))]
    public void AnonymousRelatedAuthenticationGetFlowsRemainAvailable(
        string actionName,
        Type expectedModelType)
    {
        var controller = CreateController(authenticated: false);
        var action = typeof(AccountController).GetMethod(actionName, Type.EmptyTypes);

        Assert.NotNull(action);
        Assert.NotNull(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());

        var result = Assert.IsType<ViewResult>(action.Invoke(controller, null));
        Assert.IsType(expectedModelType, result.Model);
        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
    }

    private static AccountController CreateController(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "authenticated-user")],
                authenticationType: "Test")
            : new ClaimsIdentity();

        return new AccountController(
            null!,
            null!,
            null!,
            null!,
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private static string GetRepositoryRoot(
        [CallerFilePath] string sourceFilePath = "")
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(sourceFilePath)!,
                "..",
                ".."));
    }
}
