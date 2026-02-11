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

        try
        {
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

            // Call Ollama Vision API
            var response = await _client.ChatAsync(_modelName, prompt, temperature: 0.2);

            // Parse response
            var landmarks = ParseFaceDetectionResponse(response, width, height);
            landmarks.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            landmarks.ModelUsed = ModelName;

            return landmarks;
        }
        catch (FileNotFoundException ex)
        {
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
            return new FaceLandmarks
            {
                FaceDetected = false,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                ModelUsed = ModelName,
                Metadata = new Dictionary<string, string>
                {
                    { "error", ex.Message },
                    { "raw_response", ex.ToString() }
                }
            };
        }
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
                landmarks.FaceRect = new Rectangle(
                    data.FaceRect.X,
                    data.FaceRect.Y,
                    data.FaceRect.Width,
                    data.FaceRect.Height
                );
            }

            // Parse left eye
            if (data.LeftEye != null)
            {
                landmarks.LeftEye = new Point(data.LeftEye.X, data.LeftEye.Y);
            }

            // Parse right eye
            if (data.RightEye != null)
            {
                landmarks.RightEye = new Point(data.RightEye.X, data.RightEye.Y);
            }

            // Parse face center
            if (data.FaceCenter != null)
            {
                landmarks.FaceCenter = new Point(data.FaceCenter.X, data.FaceCenter.Y);
            }

            // Calculate eye midpoint if both eyes detected
            if (landmarks.LeftEye.HasValue && landmarks.RightEye.HasValue)
            {
                var eyeMidpoint = new Point(
                    (landmarks.LeftEye.Value.X + landmarks.RightEye.Value.X) / 2,
                    (landmarks.LeftEye.Value.Y + landmarks.RightEye.Value.Y) / 2
                );
                // This will be used in FaceLandmarks.EyeMidpoint property
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
        public Point? LeftEye { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("rightEye")]
        public Point? RightEye { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("faceCenter")]
        public Point? FaceCenter { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    private class Rect
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public int X { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("y")]
        public int Y { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("width")]
        public int Width { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("height")]
        public int Height { get; set; }
    }

    private class Point
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public int X { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("y")]
        public int Y { get; set; }
    }

    #endregion
}
