namespace Planora.Web.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        var headers =
            context.Response.Headers;

        headers["X-Content-Type-Options"] =
            "nosniff";

        headers["X-Frame-Options"] =
            "DENY";

        headers["Referrer-Policy"] =
            "strict-origin-when-cross-origin";

        headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";

        headers["Cross-Origin-Opener-Policy"] =
            "same-origin";

        headers["Cross-Origin-Resource-Policy"] =
            "same-origin";

        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "object-src 'none'; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "img-src 'self' data:; " +
            "font-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self'; " +
            "connect-src 'self';";

        await _next(context);
    }
}