namespace Planora.Web.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var traceId = context.TraceIdentifier;

            _logger.LogError(
                exception,
                "Unhandled exception. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
                traceId,
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType =
                "text/html; charset=utf-8";

            context.Response.Headers.CacheControl =
                "no-store, no-cache";

            var filePath = Path.Combine(
                _environment.WebRootPath,
                "errors",
                "500.html");

            if (File.Exists(filePath))
            {
                var html =
                    await File.ReadAllTextAsync(
                        filePath,
                        context.RequestAborted);

                html = html.Replace(
                    "{{TRACE_ID}}",
                    System.Net.WebUtility.HtmlEncode(traceId));

                await context.Response.WriteAsync(
                    html,
                    context.RequestAborted);

                return;
            }

            await context.Response.WriteAsync(
                """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <title>Something Went Wrong - Planora</title>
                </head>
                <body>
                    <h1>Something went wrong</h1>
                    <p>An unexpected error occurred. Please try again.</p>
                </body>
                </html>
                """,
                context.RequestAborted);
        }
    }
}