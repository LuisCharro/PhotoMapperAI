using System.Text.Json;
using System.Text.Json.Serialization;

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
        response.EnsureSuccessStatusCode();

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
        // Convert image to base64
        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var base64Image = Convert.ToBase64String(imageBytes);

        // Detect image type
        var extension = Path.GetExtension(imagePath).TrimStart('.');
        var mimeType = extension.ToLower() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "bmp" => "image/bmp",
            _ => "image/jpeg"
        };

        var requestBody = new
        {
            model = modelName,
            messages = new List<object>
            {
                new
                {
                    role = "user",
                    content = new List<object>
                    {
                        new
                        {
                            type = "text",
                            text = prompt
                        },
                        new
                        {
                            type = "image_url",
                            image_url = $"data:{mimeType};base64,{base64Image}"
                        }
                    }
                }
            },
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

        return responseData?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
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

    #endregion
}
