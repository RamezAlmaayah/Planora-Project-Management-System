using System.Net;
using Planora.Application.Abstractions.Common;
using Planora.Web.Email;

namespace Planora.IntegrationTests;

public sealed class PlanoraEmailTemplateTests
{
    [Fact]
    public void Confirmation_UsesBrandedEmailSafeLayoutAndCorrectActionUrl()
    {
        const string callbackUrl = "https://planora.example/Account/ConfirmEmail?userId=user-1&code=encoded-token";

        string html = PlanoraEmailTemplates.Confirmation("Ava", callbackUrl);

        Assert.Contains("Confirm your Planora email", html);
        Assert.Contains("Welcome to Planora", html);
        Assert.Contains("Confirm Email", html);
        Assert.Contains("Project Management Platform", html);
        Assert.Contains("Confirm your email address to finish setting up your account", html);
        Assert.Contains("role=\"presentation\"", html);
        Assert.Contains("max-width:600px", html);
        Assert.Contains("font-size:28px;line-height:36px", html);
        Assert.Contains("padding:16px 30px", html);
        Assert.Contains("background-color:#f5f8fa", html);
        Assert.Contains("Planora Project Management Platform", html);
        Assert.Contains("cid:planora-confirm-email", html);
        Assert.DoesNotContain("local SMTP rendering test", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, CountOccurrences(html, "href=\"https://planora.example/Account/ConfirmEmail?userId=user-1&amp;code=encoded-token\""));
        Assert.Contains("Hi Ava,", html);
        Assert.Contains("ignore this email", html);
    }

    [Fact]
    public void PasswordReset_UsesCorrectActionUrlAndDoesNotIncludePassword()
    {
        const string callbackUrl = "https://planora.example/Account/ResetPassword?email=ava%40example.test&amp;code=encoded-token";

        string html = PlanoraEmailTemplates.PasswordReset(null, WebUtility.HtmlDecode(callbackUrl)!);

        Assert.Contains("Reset your Planora password", html);
        Assert.Contains("Reset Password", html);
        Assert.Contains("cid:planora-reset-password", html);
        Assert.Contains("Use the button below to choose a new password", html);
        Assert.Contains("For your security", html);
        Assert.Equal(2, CountOccurrences(html, "href=\"https://planora.example/Account/ResetPassword?email=ava%40example.test&amp;code=encoded-token\""));
        Assert.DoesNotContain("password=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Illustrations_AreEmbeddedPngResourcesForTheirEmailFlows()
    {
        EmailInlineResource confirmation = PlanoraEmailTemplates.ConfirmationIllustration;
        EmailInlineResource passwordReset = PlanoraEmailTemplates.PasswordResetIllustration;

        Assert.Equal("planora-confirm-email", confirmation.ContentId);
        Assert.Equal("planora-reset-password", passwordReset.ContentId);
        Assert.Equal("image/png", confirmation.MediaType);
        Assert.Equal("image/png", passwordReset.MediaType);
        Assert.True(confirmation.Content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.True(passwordReset.Content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
    }

    [Fact]
    public void UserContentAndUrlAreEncodedAndUnsafeUrlSchemesAreRejected()
    {
        const string callbackUrl = "https://planora.example/action?value=\" onmouseover=\"alert(1)";

        string html = PlanoraEmailTemplates.Confirmation("<img src=x onerror=alert(1)>", callbackUrl);

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.DoesNotContain("href=\"https://planora.example/action?value=\" onmouseover", html);
        Assert.Contains("&quot;", html);
        Assert.Throws<ArgumentException>(() => PlanoraEmailTemplates.PasswordReset(null, "javascript:alert(1)"));
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}
