using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planora.Application.Abstractions.Ai;
using Planora.Application.Common.Ai;

namespace Planora.Infrastructure.Gemini;

public sealed class GeminiClient : IGeminiClient
{
    private const int MaximumAttempts = 4;
    private const int MaximumResponseCharacters = 1_000_000;
    private const int MaximumErrorResponseCharacters = 64_000;
    private const int MaximumProviderErrorMessageCharacters = 1_000;
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.Model) &&
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _);

    public async Task<GeminiClientResult> GenerateJsonAsync(
        GeminiClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return GeminiClientResult.Failed(
                GeminiClientFailure.ConfigurationUnavailable,
                "AI configuration is unavailable.");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return GeminiClientResult.Failed(
                GeminiClientFailure.InvalidResponse,
                "The AI request was invalid.");

        string endpoint = $"{_options.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(_options.Model)}:generateContent";
        int timeoutSeconds = Math.Clamp(_options.TimeoutSeconds, 5, 120);
        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
                message.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
                message.Content = JsonContent.Create(new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = request.Prompt } } }
                    },
                    generationConfig = new
                    {
                        responseFormat = new
                        {
                            text = new
                            {
                                mimeType = "APPLICATION_JSON"
                            }
                        },
                        temperature = 0.2,
                        maxOutputTokens = 8192
                    }
                });

                using HttpResponseMessage response = await _httpClient.SendAsync(
                    message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    ProviderErrorMetadata providerError = await ReadProviderErrorAsync(
                        response, timeout.Token);
                    _logger.LogWarning(
                        "Gemini request failed with HTTP status {StatusCode} on attempt {Attempt}. " +
                        "Provider error code: {ProviderErrorCode}; status: {ProviderErrorStatus}; " +
                        "message: {ProviderErrorMessage}.",
                        (int)response.StatusCode,
                        attempt,
                        providerError.Code,
                        providerError.Status,
                        providerError.Message);
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        return GeminiClientResult.Failed(
                            GeminiClientFailure.Authentication,
                            "AI configuration is unavailable.");
                    bool transient = response.StatusCode == HttpStatusCode.RequestTimeout ||
                        (int)response.StatusCode >= 500;
                    if (transient && attempt < MaximumAttempts)
                    {
                        await DelayBeforeRetryAsync(
                            response, providerError.RetryDelay, attempt, cancellationToken);
                        continue;
                    }
                    return GeminiClientResult.Failed(
                        response.StatusCode == HttpStatusCode.TooManyRequests
                            ? GeminiClientFailure.RateLimited
                            : GeminiClientFailure.Provider,
                        "AI requirement generation is temporarily unavailable.");
                }

                string envelope = await response.Content.ReadAsStringAsync(timeout.Token);
                if (envelope.Length > MaximumResponseCharacters)
                    return GeminiClientResult.Failed(
                        GeminiClientFailure.InvalidResponse,
                        "Gemini returned an invalid response. Please try again.");
                try
                {
                    return ParseSuccessfulResponse(envelope);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "Gemini returned a malformed response envelope.");
                    return GeminiClientResult.Failed(
                        GeminiClientFailure.InvalidResponse,
                        "Gemini returned an invalid response. Please try again.");
                }
                catch (InvalidOperationException exception)
                {
                    _logger.LogWarning(exception, "Gemini response envelope was missing required fields.");
                    return GeminiClientResult.Failed(
                        GeminiClientFailure.InvalidResponse,
                        "Gemini returned an invalid response. Please try again.");
                }
                catch (KeyNotFoundException exception)
                {
                    _logger.LogWarning(exception, "Gemini response envelope was missing required properties.");
                    return GeminiClientResult.Failed(
                        GeminiClientFailure.InvalidResponse,
                        "Gemini returned an invalid response. Please try again.");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini request timed out on attempt {Attempt}.", attempt);
                if (attempt < MaximumAttempts)
                {
                    await DelayBeforeRetryAsync(null, null, attempt, cancellationToken);
                    continue;
                }
                return GeminiClientResult.Failed(
                    GeminiClientFailure.Timeout,
                    "AI requirement generation timed out. Please try again.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Gemini connection failed on attempt {Attempt}.", attempt);
                if (attempt < MaximumAttempts)
                {
                    await DelayBeforeRetryAsync(null, null, attempt, cancellationToken);
                    continue;
                }
                return GeminiClientResult.Failed(
                    GeminiClientFailure.Connection,
                    "AI requirement generation is temporarily unavailable.");
            }
        }
        return GeminiClientResult.Failed(
            GeminiClientFailure.Provider,
            "AI requirement generation is temporarily unavailable.");
    }

    private GeminiClientResult ParseSuccessfulResponse(string envelope)
    {
        using JsonDocument document = JsonDocument.Parse(envelope);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("candidates", out JsonElement candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            string blockReason = root.TryGetProperty("promptFeedback", out JsonElement feedback)
                ? ReadSafeString(feedback, "blockReason")
                : "unavailable";
            _logger.LogWarning(
                "Gemini returned no response candidates. Prompt block reason: {BlockReason}.",
                blockReason);
            return GeminiClientResult.Failed(
                GeminiClientFailure.InvalidResponse,
                "Gemini returned an invalid response. Please try again.");
        }

        JsonElement candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out JsonElement content) ||
            !content.TryGetProperty("parts", out JsonElement parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning(
                "Gemini response candidate contained no content. Finish reason: {FinishReason}.",
                ReadSafeString(candidate, "finishReason"));
            return GeminiClientResult.Failed(
                GeminiClientFailure.InvalidResponse,
                "Gemini returned an invalid response. Please try again.");
        }

        string json = string.Concat(parts.EnumerateArray()
            .Where(part => part.ValueKind == JsonValueKind.Object &&
                part.TryGetProperty("text", out JsonElement text) &&
                text.ValueKind == JsonValueKind.String)
            .Select(part => part.GetProperty("text").GetString()));
        return string.IsNullOrWhiteSpace(json)
            ? GeminiClientResult.Failed(
                GeminiClientFailure.InvalidResponse,
                "Gemini returned an empty response. Please try again.")
            : GeminiClientResult.Success(json);
    }

    private static Task DelayBeforeRetryAsync(
        HttpResponseMessage? response,
        TimeSpan? providerErrorDelay,
        int completedAttempt,
        CancellationToken cancellationToken)
    {
        TimeSpan? retryAfter = response?.Headers.RetryAfter?.Delta ?? providerErrorDelay;
        TimeSpan delay = retryAfter is { } providerDelay && providerDelay > TimeSpan.Zero
            ? providerDelay + TimeSpan.FromSeconds(1) +
                TimeSpan.FromMilliseconds(Random.Shared.Next(50, 251))
            : TimeSpan.FromSeconds(Math.Pow(2, completedAttempt - 1)) +
                TimeSpan.FromMilliseconds(Random.Shared.Next(50, 251));
        if (delay > TimeSpan.FromSeconds(60)) delay = TimeSpan.FromSeconds(60);
        return Task.Delay(delay, cancellationToken);
    }

    private static async Task<ProviderErrorMetadata> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            string payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (payload.Length > MaximumErrorResponseCharacters)
                return ProviderErrorMetadata.Unavailable;

            using JsonDocument document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("error", out JsonElement error) ||
                error.ValueKind != JsonValueKind.Object)
                return ProviderErrorMetadata.Unavailable;

            int? code = error.TryGetProperty("code", out JsonElement codeElement) &&
                codeElement.TryGetInt32(out int parsedCode)
                    ? parsedCode
                    : null;
            string status = ReadSafeString(error, "status");
            string message = ReadSafeString(error, "message");
            return new ProviderErrorMetadata(code, status, message, ReadRetryDelay(error));
        }
        catch (JsonException)
        {
            return ProviderErrorMetadata.Unavailable;
        }
        catch (InvalidOperationException)
        {
            return ProviderErrorMetadata.Unavailable;
        }
    }

    private static string ReadSafeString(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
            return "unavailable";

        string normalized = string.Join(' ', (value.GetString() ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized)
            ? "unavailable"
            : normalized[..Math.Min(normalized.Length, MaximumProviderErrorMessageCharacters)];
    }

    private static TimeSpan? ReadRetryDelay(JsonElement error)
    {
        if (!error.TryGetProperty("details", out JsonElement details) ||
            details.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement detail in details.EnumerateArray())
        {
            if (detail.ValueKind != JsonValueKind.Object ||
                !detail.TryGetProperty("retryDelay", out JsonElement retryDelay) ||
                retryDelay.ValueKind != JsonValueKind.String)
                continue;
            string value = retryDelay.GetString() ?? string.Empty;
            string numericValue;
            double multiplier;
            if (value.EndsWith("ms", StringComparison.Ordinal))
            {
                numericValue = value[..^2];
                multiplier = 0.001;
            }
            else if (value.EndsWith('s'))
            {
                numericValue = value[..^1];
                multiplier = 1;
            }
            else
            {
                continue;
            }
            if (double.TryParse(
                    numericValue,
                    System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double duration) &&
                duration > 0)
                return TimeSpan.FromSeconds(duration * multiplier);
        }
        return null;
    }

    private sealed record ProviderErrorMetadata(
        int? Code,
        string Status,
        string Message,
        TimeSpan? RetryDelay)
    {
        public static ProviderErrorMetadata Unavailable { get; } =
            new(null, "unavailable", "unavailable", null);
    }
}


