using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Wrapper for Ollama API calls.
/// </summary>
public class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new Ollama client.
    /// </summary>
    /// <param name="baseUrl">Ollama base URL (default: http://localhost:11434)</param>
    /// <param name="timeoutMinutes">Request timeout in minutes (default: 5)</param>
    public OllamaClient(string baseUrl = "http://localhost:11434", int timeoutMinutes = 5)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(timeoutMinutes) };
    }

    /// <summary>
    /// Sends a chat completion request to Ollama.
    /// </summary>
    /// <param name="modelName">Model name (e.g., qwen2.5:7b)</param>
    /// <param name="prompt">Prompt text to send</param>
    /// <param name="systemPrompt">Optional system prompt for model behavior</param>
    /// <param name="temperature">Temperature (0.0 to 1.0, lower = more deterministic)</param>
    /// <returns>Response text</returns>
    public async Task<string> ChatAsync(
        string modelName,
        string prompt,
        string? systemPrompt = null,
        double temperature = 0.3)
    {
        await EnsureSingleActiveModelAsync(modelName);

        var requestBody = new
        {
            model = modelName,
            messages = new List<object>(),
            stream = false,
            options = new
            {
                temperature = temperature
            }
        };

        // Add system prompt if provided
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            requestBody.messages.Add(new
            {
                role = "system",
                content = systemPrompt
            });
        }

        // Add user prompt
        requestBody.messages.Add(new
        {
            role = "user",
            content = prompt
        });

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
        await EnsureSuccessOrThrowAsync(response, modelName, "/v1/chat/completions");

        var responseJson = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

        return responseData?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    /// <summary>
    /// Sends a vision request with image to Ollama Vision models.
    /// </summary>
    /// <param name="modelName">Vision model name (e.g., qwen3-vl, llava:7b)</param>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="prompt">Prompt text</param>
    /// <param name="cancellationToken">Optional cancellation token for timeout</param>
    /// <returns>Response text</returns>
    public async Task<string> VisionAsync(
        string modelName,
        string imagePath,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        await EnsureSingleActiveModelAsync(modelName, cancellationToken);

        // Convert image to base64
        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var base64Image = Convert.ToBase64String(imageBytes);

        var requestBody = new
        {
            model = modelName,
            messages = new List<object>
            {
                new
                {
                    role = "user",
                    content = prompt,
                    images = new List<string> { base64Image }
                }
            },
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, modelName, "/api/chat");

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        
        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var text))
        {
            return text.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if Ollama server is available.
    /// </summary>
    /// <returns>True if server responds</returns>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets list of available models from Ollama.
    /// </summary>
    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OllamaTagsResponse>(responseJson);

            return data?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Enforces local-model policy in Ollama:
    /// - Cloud models (suffix :cloud) are ignored and never unloaded
    /// - For local required models, unload other running local models
    /// - For cloud required models, do not unload local models
    /// </summary>
    private async Task EnsureSingleActiveModelAsync(string requiredModel, CancellationToken cancellationToken = default)
    {
        var runningModels = await GetRunningModelsAsync(cancellationToken);
        var toUnload = OllamaModelPolicy.GetLocalModelsToUnload(runningModels, requiredModel);

        foreach (var model in toUnload)
        {
            await UnloadModelAsync(model, cancellationToken);
        }
    }

    /// <summary>
    /// Returns currently loaded/running models from Ollama.
    /// </summary>
    private async Task<List<string>> GetRunningModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/ps", cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<OllamaPsResponse>(responseJson);

            var models = new List<string>();
            if (data?.Models == null)
                return models;

            foreach (var m in data.Models)
            {
                if (!string.IsNullOrWhiteSpace(m.Name))
                    models.Add(m.Name);
                else if (!string.IsNullOrWhiteSpace(m.Model))
                    models.Add(m.Model);
            }

            return models.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            // Best effort: if ps endpoint is unavailable, continue without model management.
            return new List<string>();
        }
    }

    /// <summary>
    /// Unloads a model from Ollama memory.
    /// </summary>
    private async Task UnloadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = modelName,
            prompt = string.Empty,
            stream = false,
            keep_alive = 0
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string modelName, string endpoint)
    {
        if (response.IsSuccessStatusCode)
            return;

        var statusCode = (int)response.StatusCode;
        var responseBody = await response.Content.ReadAsStringAsync();
        var compactBody = string.IsNullOrWhiteSpace(responseBody)
            ? "no response body"
            : responseBody.Replace('\n', ' ').Replace('\r', ' ').Trim();

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new OllamaQuotaExceededException(
                $"Ollama Cloud quota exhausted for model '{modelName}' (HTTP 409). " +
                "Wait for quota reset or switch model. " +
                $"Endpoint: {endpoint}. Response: {compactBody}");
        }

        throw new HttpRequestException(
            $"Ollama request failed for model '{modelName}' (HTTP {statusCode}) on {endpoint}. " +
            $"Response: {compactBody}");
    }

    #region Response Models

    private class OllamaResponse
    {
        [JsonPropertyName("choices")]
        public List<OllamaChoice>? Choices { get; set; }
    }

    private class OllamaChoice
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel>? Models { get; set; }
    }

    private class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private class OllamaPsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaPsModel>? Models { get; set; }
    }

    private class OllamaPsModel
    {
        // Depending on Ollama version, /api/ps may expose "name" or "model".
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    #endregion
}
