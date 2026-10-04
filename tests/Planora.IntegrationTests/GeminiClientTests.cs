using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planora.Application.Common.Ai;
using Planora.Infrastructure.Gemini;

namespace Planora.IntegrationTests;

public sealed class GeminiClientTests
{
    [Fact]
    public async Task GenerateJsonUsesConfiguredEndpointHeaderAndWorkingStructuredRequest()
    {
        var handler = new RecordingHandler(_ => SuccessResponse("{\"status\":\"ok\"}"));
        var client = CreateClient(handler);

        GeminiClientResult result = await client.GenerateJsonAsync(new GeminiClientRequest
        {
            Prompt = "Return a JSON status object."
        });

        Assert.True(result.Succeeded);
        Assert.Equal("{\"status\":\"ok\"}", result.Json);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("test-api-key", Assert.Single(handler.ApiKeyValues));

        using JsonDocument request = JsonDocument.Parse(handler.Body!);
        JsonElement root = request.RootElement;
        Assert.Equal("user", root.GetProperty("contents")[0].GetProperty("role").GetString());
        Assert.Equal(
            "Return a JSON status object.",
            root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
        JsonElement config = root.GetProperty("generationConfig");
        Assert.Equal(
            "APPLICATION_JSON",
            config.GetProperty("responseFormat").GetProperty("text").GetProperty("mimeType").GetString());
        Assert.Equal(0.2, config.GetProperty("temperature").GetDouble());
        Assert.Equal(8192, config.GetProperty("maxOutputTokens").GetInt32());
        Assert.DoesNotContain("test-api-key", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransientUnavailableResponseUsesBoundedRetryAndCanRecover()
    {
        int calls = 0;
        var handler = new RecordingHandler(_ =>
        {
            calls++;
            if (calls == 1)
            {
                var unavailable = ErrorResponse(
                    HttpStatusCode.ServiceUnavailable,
                    503,
                    "UNAVAILABLE",
                    "This model is currently experiencing high demand.");
                unavailable.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
                return unavailable;
            }
            return SuccessResponse("{\"status\":\"ok\"}");
        });

        GeminiClientResult result = await CreateClient(handler).GenerateJsonAsync(
            new GeminiClientRequest { Prompt = "Return JSON." });

        Assert.True(result.Succeeded);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ProviderQuotaFailureDoesNotMultiplyRequests()
    {
        int calls = 0;
        var handler = new RecordingHandler(_ =>
        {
            calls++;
            if (calls == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """
                        {"error":{"code":429,"status":"RESOURCE_EXHAUSTED","message":"Please retry later.","details":[{"@type":"type.googleapis.com/google.rpc.RetryInfo","retryDelay":"1ms"}]}}
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }
            return SuccessResponse("{\"status\":\"ok\"}");
        });

        GeminiClientResult result = await CreateClient(handler).GenerateJsonAsync(
            new GeminiClientRequest { Prompt = "Return JSON." });

        Assert.False(result.Succeeded);
        Assert.Equal(GeminiClientFailure.RateLimited, result.Failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ProviderErrorLoggingContainsSafeMetadataButNoKeyOrPrompt()
    {
        var logger = new CapturingLogger<GeminiClient>();
        var handler = new RecordingHandler(_ => ErrorResponse(
            HttpStatusCode.BadRequest,
            400,
            "INVALID_ARGUMENT",
            "A generation configuration property is invalid."));

        GeminiClientResult result = await CreateClient(handler, logger).GenerateJsonAsync(
            new GeminiClientRequest { Prompt = "private project prompt" });

        Assert.False(result.Succeeded);
        string log = Assert.Single(logger.Messages);
        Assert.Contains("HTTP status 400", log);
        Assert.Contains("400", log);
        Assert.Contains("INVALID_ARGUMENT", log);
        Assert.Contains("A generation configuration property is invalid.", log);
        Assert.DoesNotContain("test-api-key", log, StringComparison.Ordinal);
        Assert.DoesNotContain("private project prompt", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultipleResponseTextPartsAreCombinedBeforeParsing()
    {
        var response = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = "{\"status\":" }, new { text = "\"ok\"}" } }
                    },
                    finishReason = "STOP"
                }
            }
        };
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.OK, response));

        GeminiClientResult result = await CreateClient(handler).GenerateJsonAsync(
            new GeminiClientRequest { Prompt = "Return JSON." });

        Assert.True(result.Succeeded);
        Assert.Equal("{\"status\":\"ok\"}", result.Json);
    }

    private static GeminiClient CreateClient(
        HttpMessageHandler handler,
        ILogger<GeminiClient>? logger = null) => new(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-api-key",
                Model = "gemini-3.8-flash",
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                TimeoutSeconds = 10
            }),
            logger ?? new CapturingLogger<GeminiClient>());

    private static HttpResponseMessage SuccessResponse(string json) => JsonResponse(
        HttpStatusCode.OK,
        new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = json } } }, finishReason = "STOP" }
            }
        });

    private static HttpResponseMessage ErrorResponse(
        HttpStatusCode statusCode,
        int code,
        string status,
        string message) => JsonResponse(statusCode, new { error = new { code, status, message } });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object value) => new(statusCode)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public IReadOnlyList<string> ApiKeyValues { get; private set; } = [];
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKeyValues = request.Headers.TryGetValues("x-goog-api-key", out IEnumerable<string>? values)
                ? values.ToList()
                : [];
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
