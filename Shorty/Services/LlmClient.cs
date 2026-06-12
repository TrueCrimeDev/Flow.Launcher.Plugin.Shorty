using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using Shorty.Models;

namespace Shorty.Services;

public sealed class LlmClient
{
    private const string ShortyStylePrompt =
        "Format responses in markdown for Flow Launcher's preview pane. " +
        "Keep prose tight — 2–4 short paragraphs by default — but use fenced code blocks (```lang\\n...```) for any code, " +
        "and every fenced code block must use the most specific language tag: powershell, bash, batch, csharp, xaml, " +
        "typescript, javascript, autohotkey, diff, json, yaml, toml, html, css, sql, python, c, cpp, rust, markdown, " +
        "mermaid, or text for plain logs only. Never emit a bare triple-backtick fence. " +
        "bullet lists where they aid scanning, and short ## headings only when the answer has 2+ distinct parts. " +
        "Never apologize, never preamble. When a preset overrides the format, follow the preset.";

    private static readonly HttpClient HttpClient = new();

    public async Task<(string Text, string Model)> AskAsync(
        Settings settings,
        Preset? preset,
        string prompt,
        CancellationToken cancellationToken)
    {
        var apiKey = (settings.ApiKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new LlmClientException("No API key configured", "Set Shorty's API key in Flow settings.");
        }

        var model = string.IsNullOrWhiteSpace(preset?.Model) ? settings.DefaultModel : preset!.Model.Trim();
        var systemParts = new List<string> { ShortyStylePrompt };
        if (!string.IsNullOrWhiteSpace(preset?.System))
        {
            systemParts.Add(preset.System.Trim());
        }

        var payload = new
        {
            model,
            stream = false,
            temperature = preset?.Temperature ?? 0.2,
            messages = new[]
            {
                new { role = "system", content = string.Join("\n\n", systemParts) },
                new { role = "user", content = prompt }
            }
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.EndpointUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmClientException("Transport error", $"Timeout after {settings.RequestTimeoutSeconds}s") { SourceException = ex };
        }
        catch (HttpRequestException ex)
        {
            throw new LlmClientException("Transport error", $"{ex.GetType().Name}: {ex.Message}") { SourceException = ex };
        }

        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new LlmClientException($"Error {(int)response.StatusCode}", ExtractErrorMessage(body));
        }

        return (ExtractAnswer(body), model);
    }

    public async Task<string> TestConnectionAsync(Settings settings, Preset? preset, CancellationToken cancellationToken)
    {
        var result = await AskAsync(settings, preset, "Reply with exactly: pong", cancellationToken).ConfigureAwait(false);
        return result.Text;
    }

    private static string ExtractAnswer(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return content ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new LlmClientException("Response parse error", $"{ex.GetType().Name}: {ex.Message}") { SourceException = ex };
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? body;
                }

                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? body;
                }
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(body) ? "Provider returned an empty error response." : body;
    }
}

public sealed class LlmClientException(string title, string detail) : Exception(detail)
{
    public string Title { get; } = title;

    public string Detail { get; } = detail;

    public Exception? SourceException { get; init; }
}
