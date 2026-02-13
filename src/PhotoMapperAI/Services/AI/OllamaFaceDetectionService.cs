using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Image;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service using Ollama Vision models.
/// </summary>
public class OllamaFaceDetectionService : IFaceDetectionService
{
    private readonly OllamaClient _client;
    private readonly IImageProcessor _imageProcessor;
    private readonly string _modelName;

    /// <summary>
    /// Creates a new Ollama vision face detection service.
    /// </summary>
    /// <param name="ollamaBaseUrl">Ollama server URL (default: http://localhost:11434)</param>
    /// <param name="modelName">Vision model to use (e.g., qwen3-vl, llava:7b)</param>
    /// <param name="imageProcessor">Image processor for loading images</param>
    public OllamaFaceDetectionService(
        string ollamaBaseUrl = "http://localhost:11434",
        string modelName = "qwen3-vl",
        IImageProcessor? imageProcessor = null)
    {
        _client = new OllamaClient(ollamaBaseUrl);
        _modelName = modelName;
        _imageProcessor = imageProcessor ?? new ImageProcessor();
    }

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string ModelName => _modelName;

    /// <summary>
    /// Detects face and eye landmarks in a photo using Ollama Vision.
    /// </summary>
    public async Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath)
    {
        var startTime = DateTime.UtcNow;
        const int maxRetries = 2;
        const int retryDelayMs = 1000;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"[OllamaVision] Attempt {attempt + 1}/{maxRetries + 1} for {_modelName}");

                // Load image to get dimensions
                var (width, height) = await _imageProcessor.GetImageDimensionsAsync(imagePath);

                // Build prompt for face detection
                var prompt = BuildFaceDetectionPrompt(width, height);

                // Get image base64 for vision input
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var extension = Path.GetExtension(imagePath).TrimStart('.');
                var mimeType = extension.ToLower() switch
                {
                    "jpg" or "jpeg" => "image/jpeg",
                    "png" => "image/png",
                    "bmp" => "image/bmp",
                    _ => "image/jpeg"
                };
                var base64Image = Convert.ToBase64String(imageBytes);

                // Call Ollama Vision API with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var response = await _client.VisionAsync(_modelName, imagePath, prompt, cts.Token);

                // Parse response
                var landmarks = ParseFaceDetectionResponse(response, width, height);
                landmarks.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                landmarks.ModelUsed = ModelName;

                Console.WriteLine($"[OllamaVision] ✓ Face detected: {landmarks.FaceDetected}, Confidence: {landmarks.FaceConfidence:F2}");
                return landmarks;
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine($"[OllamaVision] ✗ Timeout on attempt {attempt + 1}: {ex.Message}");

                if (attempt == maxRetries)
                {
                    return new FaceLandmarks
                    {
                        FaceDetected = false,
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        ModelUsed = ModelName,
                        Metadata = new Dictionary<string, string>
                        {
                            { "error", $"Request timeout after {maxRetries + 1} attempts" },
                            { "attempts", $"{maxRetries + 1}" }
                        }
                    };
                }

                // Wait before retry
                await Task.Delay(retryDelayMs * (attempt + 1));
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[OllamaVision] ✗ HTTP error on attempt {attempt + 1}: {ex.Message}");

                if (attempt == maxRetries)
                {
                    return new FaceLandmarks
                    {
                        FaceDetected = false,
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        ModelUsed = ModelName,
                        Metadata = new Dictionary<string, string>
                        {
                            { "error", $"HTTP request failed: {ex.Message}" },
                            { "attempts", $"{maxRetries + 1}" }
                        }
                    };
                }

                await Task.Delay(retryDelayMs * (attempt + 1));
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"[OllamaVision] ✗ File not found: {ex.FileName}");
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = ModelName,
                    Metadata = new Dictionary<string, string>
                    {
                        { "error", $"File not found: {ex.FileName}" }
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OllamaVision] ✗ Unexpected error on attempt {attempt + 1}: {ex.Message}");

                if (attempt == maxRetries)
                {
                    return new FaceLandmarks
                    {
                        FaceDetected = false,
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        ModelUsed = ModelName,
                        Metadata = new Dictionary<string, string>
                        {
                            { "error", ex.Message },
                            { "exception_type", ex.GetType().Name },
                            { "attempts", $"{maxRetries + 1}" },
                            { "stack_trace", ex.StackTrace ?? string.Empty }
                        }
                    };
                }

                await Task.Delay(retryDelayMs * (attempt + 1));
            }
        }

        // Should never reach here, but compiler needs it
        return new FaceLandmarks
        {
            FaceDetected = false,
            ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
            ModelUsed = ModelName,
            Metadata = new Dictionary<string, string>
            {
                { "error", "Unknown error in DetectFaceLandmarksAsync" }
            }
        };
    }

    /// <summary>
    /// Batch detects faces in multiple photos.
    /// </summary>
    public async Task<List<FaceLandmarks>> DetectFaceLandmarksBatchAsync(List<string> imagePaths)
    {
        var results = new List<FaceLandmarks>();

        foreach (var imagePath in imagePaths)
        {
            var result = await DetectFaceLandmarksAsync(imagePath);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Checks if Ollama Vision service is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var availableModels = await _client.GetAvailableModelsAsync();
            return availableModels.Contains(_modelName);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Initializes the Ollama service.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        return await IsAvailableAsync();
    }

    #region Private Methods

    /// <summary>
    /// Builds a prompt for face and eye detection.
    /// </summary>
    private static string BuildFaceDetectionPrompt(int imageWidth, int imageHeight)
    {
        return $@"You are a face and eye detection expert for sports player photos.

Analyze the image and detect:
1. Is there a face? (true/false)
2. Are both eyes visible? (true/false)
3. Face rectangle (x, y, width, height) in pixels
4. Left eye position (x, y) in pixels (if visible)
5. Right eye position (x, y) in pixels (if visible)
6. Face center point (x, y) in pixels
7. Confidence score (0.0 to 1.0)

Image dimensions: {imageWidth}x{imageHeight} pixels.

Return your answer as a JSON object with:
{{
""faceDetected"": true/false,
""bothEyesDetected"": true/false,
""faceRect"": {{""x"": 0, ""y"": 0, ""width"": 0, ""height"": 0}},
""leftEye"": {{""x"": 0, ""y"": 0}},
""rightEye"": {{""x"": 0, ""y"": 0}},
""faceCenter"": {{""x"": 0, ""y"": 0}},
""confidence"": 0.0

If no face detected, return:
{{
""faceDetected"": false,
""bothEyesDetected"": false,
""confidence"": 0.0
}}";
    }

    /// <summary>
    /// Parses Ollama Vision response into FaceLandmarks.
    /// </summary>
    private static FaceLandmarks ParseFaceDetectionResponse(string response, int imageWidth, int imageHeight)
    {
        try
        {
            // Extract JSON from response (LLM may add extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0)
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ModelUsed = "OllamaVision",
                    Metadata = new Dictionary<string, string>
                    {
                        { "parse_error", "Could not extract JSON from response" },
                        { "raw_response", response }
                    }
                };
            }

            var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var data = System.Text.Json.JsonSerializer.Deserialize<VisionResponse>(jsonString);

            if (data == null)
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ModelUsed = "OllamaVision"
                };
            }

            var landmarks = new FaceLandmarks
            {
                FaceDetected = data.FaceDetected,
                BothEyesDetected = data.BothEyesDetected,
                ModelUsed = "OllamaVision",
                FaceConfidence = data.Confidence
            };

            // Parse face rectangle
            if (data.FaceDetected && data.FaceRect != null)
            {
                landmarks.FaceRect = new PhotoMapperAI.Models.Rectangle(
                    ScaleCoord(data.FaceRect.X, imageWidth),
                    ScaleCoord(data.FaceRect.Y, imageHeight),
                    ScaleCoord(data.FaceRect.Width, imageWidth),
                    ScaleCoord(data.FaceRect.Height, imageHeight)
                );
            }

            // Parse left eye
            if (data.LeftEye != null)
            {
                landmarks.LeftEye = new PhotoMapperAI.Models.Point(
                    ScaleCoord(data.LeftEye.X, imageWidth),
                    ScaleCoord(data.LeftEye.Y, imageHeight)
                );
            }

            // Parse right eye
            if (data.RightEye != null)
            {
                landmarks.RightEye = new PhotoMapperAI.Models.Point(
                    ScaleCoord(data.RightEye.X, imageWidth),
                    ScaleCoord(data.RightEye.Y, imageHeight)
                );
            }

            // Parse face center
            if (data.FaceCenter != null)
            {
                landmarks.FaceCenter = new PhotoMapperAI.Models.Point(
                    ScaleCoord(data.FaceCenter.X, imageWidth),
                    ScaleCoord(data.FaceCenter.Y, imageHeight)
                );
            }

            return landmarks;
        }
        catch (Exception ex)
        {
            return new FaceLandmarks
            {
                FaceDetected = false,
                ModelUsed = "OllamaVision",
                Metadata = new Dictionary<string, string>
                {
                    { "parse_error", "Failed to parse JSON" },
                    { "exception", ex.Message },
                    { "raw_response", response }
                }
            };
        }
    }

    private static int ScaleCoord(double value, int dimension)
    {
        // If value is between 0 and 1, treat as normalized float
        if (value > 0 && value < 1.0)
        {
            return (int)(value * dimension);
        }
        return (int)value;
    }

    #endregion

    #region JSON Response Models

    private class VisionResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("faceDetected")]
        public bool FaceDetected { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("bothEyesDetected")]
        public bool BothEyesDetected { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("faceRect")]
        public Rect? FaceRect { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("leftEye")]
        public VisionPoint? LeftEye { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("rightEye")]
        public VisionPoint? RightEye { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("faceCenter")]
        public VisionPoint? FaceCenter { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    private class Rect
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public double X { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("y")]
        public double Y { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("width")]
        public double Width { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("height")]
        public double Height { get; set; }
    }

    private class VisionPoint
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public double X { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("y")]
        public double Y { get; set; }
    }

    #endregion
}
