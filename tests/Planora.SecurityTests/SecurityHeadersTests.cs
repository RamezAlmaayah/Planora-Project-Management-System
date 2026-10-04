using Microsoft.AspNetCore.Http;
using Planora.Web.Middleware;
using Xunit;

namespace Planora.SecurityTests;

public class SecurityHeadersTests
{
    [Fact]
    public async Task SecurityHeadersMiddleware_ShouldAddSecurityHeaders()
    {
        var context =
            new DefaultHttpContext();

        var middleware =
            new SecurityHeadersMiddleware(
                _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "nosniff",
            context.Response.Headers[
                "X-Content-Type-Options"].ToString());

        Assert.Equal(
            "DENY",
            context.Response.Headers[
                "X-Frame-Options"].ToString());

        Assert.Equal(
            "strict-origin-when-cross-origin",
            context.Response.Headers[
                "Referrer-Policy"].ToString());

        Assert.Contains(
            "camera=()",
            context.Response.Headers[
                "Permissions-Policy"].ToString());

        Assert.Contains(
            "default-src 'self'",
            context.Response.Headers[
                "Content-Security-Policy"].ToString());
    }
}