using System.Text.Json;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class OllamaHealthService(LlamaOptions options)
{
    public async Task<OllamaHealthDto> CheckAsync(CancellationToken ct = default)
    {
        var result = new OllamaHealthDto
        {
            Configured = options.IsConfigured,
            BaseUrl = string.IsNullOrWhiteSpace(options.Endpoint) ? "not configured" : options.Endpoint,
            ModelId = options.Model,
            CheckedAt = DateTime.UtcNow
        };
        if (!options.IsConfigured) return result;

        var baseUri = BuildOllamaBase(options.Endpoint);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            using var response = await http.GetAsync($"{baseUri}/api/tags", ct);
            if (!response.IsSuccessStatusCode)
            {
                result.Error = $"Ollama returned HTTP {(int)response.StatusCode}.";
                return result;
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
            {
                foreach (var model in models.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String) result.InstalledModels.Add(name.GetString() ?? string.Empty);
                }
            }
            result.Reachable = true;
            if (!result.InstalledModels.Any(m => m.StartsWith(options.Model, StringComparison.OrdinalIgnoreCase)))
                result.Error = $"Configured model '{options.Model}' was not found in the local Ollama model list.";
            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"Ollama not reachable at {baseUri}: {ex.Message}";
            return result;
        }
    }

    private static string BuildOllamaBase(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        return trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? trimmed[..^3].TrimEnd('/') : trimmed;
    }
}
