using PhotoMapperAI.Models;
using PhotoMapperAI.Services;
using PhotoMapperAI.Services.AI;
using System.Text.Json;

namespace PhotoMapperAI.Commands;

/// <summary>
/// Benchmark command logic - compare AI model performance
/// </summary>
public class BenchmarkCommandLogic
{
    private readonly IImageProcessor _imageProcessor;

    /// <summary>
    /// Creates a new benchmark command logic handler.
    /// </summary>
    public BenchmarkCommandLogic(IImageProcessor imageProcessor)
    {
        _imageProcessor = imageProcessor;
    }

    /// <summary>
    /// Executes the benchmark command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? nameModels,
        string? faceModels,
        string testDataPath,
        string outputPath)
    {
        Console.WriteLine("Benchmark Command");
        Console.WriteLine("================");
        Console.WriteLine($"Test Data: {testDataPath}");
        Console.WriteLine($"Output Path: {outputPath}");
        Console.WriteLine();

        try
        {
            // Parse model lists
            var nameModelList = ParseModelList(nameModels);
            var faceModelList = ParseModelList(faceModels);

            // Create output directory
            Directory.CreateDirectory(outputPath);

            var results = new BenchmarkResults
            {
                Timestamp = DateTime.UtcNow,
                TestDataPath = testDataPath,
                NameMatchingResults = new List<NameMatchingBenchmark>(),
                FaceDetectionResults = new List<FaceDetectionBenchmark>()
            };

            // Benchmark name matching models
            if (nameModelList.Count > 0)
            {
                Console.WriteLine($"Benchmarking {nameModelList.Count} name matching models...");
                Console.WriteLine();

                foreach (var model in nameModelList)
                {
                    Console.WriteLine($"Testing: {model}");
                    var result = await BenchmarkNameMatchingModel(model, testDataPath);
                    results.NameMatchingResults.Add(result);
                    PrintNameMatchingResult(result);
                    Console.WriteLine();
                }
            }

            // Benchmark face detection models
            if (faceModelList.Count > 0)
            {
                Console.WriteLine($"Benchmarking {faceModelList.Count} face detection models...");
                Console.WriteLine();

                foreach (var model in faceModelList)
                {
                    Console.WriteLine($"Testing: {model}");
                    var result = await BenchmarkFaceDetectionModel(model, testDataPath);
                    results.FaceDetectionResults.Add(result);
                    PrintFaceDetectionResult(result);
                    Console.WriteLine();
                }
            }

            // Save results
            var resultsPath = Path.Combine(outputPath, $"benchmark-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(resultsPath, json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Benchmark complete! Results saved to: {resultsPath}");
            Console.ResetColor();

            // Print summary
            PrintSummary(results);

            return 0;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    #region Private Methods

    /// <summary>
    /// Benchmarks a single name matching model.
    /// </summary>
    private async Task<NameMatchingBenchmark> BenchmarkNameMatchingModel(string modelName, string testDataPath)
    {
        var result = new NameMatchingBenchmark
        {
            ModelName = modelName,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Create service
            var service = new OllamaNameMatchingService(modelName: modelName, confidenceThreshold: 0.7);

            // Load test pairs
            var testPairs = LoadNameMatchingTestPairs(testDataPath);
            if (testPairs.Count == 0)
            {
                result.Error = "No test pairs found";
                return result;
            }

            // Run comparisons
            var testResults = new List<NameMatchTestResult>();
            foreach (var pair in testPairs)
            {
                var startTime = DateTime.UtcNow;
                var matchResult = await service.CompareNamesAsync(pair.Name1, pair.Name2);
                var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                testResults.Add(new NameMatchTestResult
                {
                    Name1 = pair.Name1,
                    Name2 = pair.Name2,
                    ExpectedMatch = pair.ExpectedMatch,
                    Confidence = matchResult.Confidence,
                    IsMatch = matchResult.IsMatch,
                    ProcessingTimeMs = duration
                });
            }

            // Calculate metrics
            var correctMatches = testResults.Count(r => r.IsMatch == r.ExpectedMatch);
            var accuracy = testResults.Count > 0 ? (double)correctMatches / testResults.Count : 0.0;
            var avgTime = testResults.Count > 0 ? testResults.Average(r => r.ProcessingTimeMs) : 0.0;
            var matchedResults = testResults.Where(r => r.IsMatch).ToList();
            var avgConfidence = matchedResults.Count > 0 ? matchedResults.Average(r => r.Confidence) : 0.0;

            result.TestCount = testResults.Count;
            result.CorrectMatches = correctMatches;
            result.Accuracy = accuracy;
            result.AverageProcessingTimeMs = avgTime;
            result.AverageConfidence = avgConfidence;
            result.TestResults = testResults.Take(10).ToList(); // Keep top 10 for summary
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = (long)(result.EndTime - result.StartTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = (long)(result.EndTime - result.StartTime).TotalMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// Benchmarks a single face detection model.
    /// </summary>
    private async Task<FaceDetectionBenchmark> BenchmarkFaceDetectionModel(string modelName, string testDataPath)
    {
        var result = new FaceDetectionBenchmark
        {
            ModelName = modelName,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Create and initialize service
            IFaceDetectionService? service = modelName.ToLower() switch
            {
                "opencv-dnn" => new OpenCVDNNFaceDetectionService(),
                var ollama when modelName.Contains("llava") || modelName.Contains("qwen") => new OllamaFaceDetectionService(modelName: modelName),
                _ => throw new ArgumentException($"Unknown face detection model: {modelName}")
            };

            if (service is OpenCVDNNFaceDetectionService cvService)
            {
                var initialized = await cvService.InitializeAsync();
                if (!initialized)
                {
                    result.Error = "Failed to initialize OpenCV DNN";
                    return result;
                }
            }

            // Load test images
            var testImages = LoadFaceDetectionTestImages(testDataPath);
            if (testImages.Count == 0)
            {
                result.Error = "No test images found";
                return result;
            }

            // Run detection
            var testResults = new List<FaceDetectionTestResult>();
            foreach (var imagePath in testImages)
            {
                var startTime = DateTime.UtcNow;
                var landmarks = await service.DetectFaceLandmarksAsync(imagePath);
                var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                testResults.Add(new FaceDetectionTestResult
                {
                    ImagePath = imagePath,
                    ExpectedFaces = GetExpectedFaceCount(imagePath),
                    DetectedFaces = landmarks.FaceDetected ? 1 : 0,
                    ProcessingTimeMs = duration,
                    FaceConfidence = landmarks.FaceConfidence,
                    Error = landmarks.Metadata?.GetValueOrDefault("error")
                });
            }

            // Calculate metrics
            var correctDetections = testResults.Count(r => r.DetectedFaces == r.ExpectedFaces);
            var accuracy = testResults.Count > 0 ? (double)correctDetections / testResults.Count : 0.0;
            var avgTime = testResults.Count > 0 ? testResults.Average(r => r.ProcessingTimeMs) : 0.0;
            var faceDetections = testResults.Where(r => r.DetectedFaces > 0).ToList();
            var avgConfidence = faceDetections.Count > 0 ? faceDetections.Average(r => r.FaceConfidence) : 0.0;

            result.TestCount = testResults.Count;
            result.CorrectDetections = correctDetections;
            result.Accuracy = accuracy;
            result.AverageProcessingTimeMs = avgTime;
            result.AverageConfidence = avgConfidence;
            result.TestResults = testResults.Take(10).ToList();
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = (long)(result.EndTime - result.StartTime).TotalMilliseconds;

            // Cleanup
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = (long)(result.EndTime - result.StartTime).TotalMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// Loads name matching test pairs from test data.
    /// </summary>
    private List<NameMatchingTestPair> LoadNameMatchingTestPairs(string testDataPath)
    {
        // Look for name_pairs.csv in test data
        var pairsPath = Path.Combine(testDataPath, "name_pairs.csv");
        if (!File.Exists(pairsPath))
        {
            // Return synthetic test data
            return new List<NameMatchingTestPair>
            {
                new() { Name1 = "Rodríguez Sánchez", Name2 = "Rodriguez Sanchez", ExpectedMatch = true },
                new() { Name1 = "Ramos Sergio", Name2 = "Sergio Ramos", ExpectedMatch = true },
                new() { Name1 = "Iniesta Andrés", Name2 = "Andres Iniesta", ExpectedMatch = true },
                new() { Name1 = "Luis García", Name2 = "Luis Martínez", ExpectedMatch = false }
            };
        }

        var lines = File.ReadAllLines(pairsPath);
        var pairs = new List<NameMatchingTestPair>();

        foreach (var line in lines.Skip(1)) // Skip header
        {
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                pairs.Add(new NameMatchingTestPair
                {
                    Name1 = parts[0].Trim(),
                    Name2 = parts[1].Trim(),
                    ExpectedMatch = bool.Parse(parts[2].Trim())
                });
            }
        }

        return pairs;
    }

    /// <summary>
    /// Loads face detection test images from test data.
    /// </summary>
    private List<string> LoadFaceDetectionTestImages(string testDataPath)
    {
        var photosPath = Path.Combine(testDataPath, "photos");
        if (!Directory.Exists(photosPath))
        {
            // Use test photos from current directory
            if (Directory.Exists("./photos"))
            {
                return Directory.GetFiles("./photos", "*.*").ToList();
            }
            return new List<string>();
        }

        return Directory.GetFiles(photosPath, "*.*").ToList();
    }

    /// <summary>
    /// Gets expected face count for an image (placeholder logic).
    /// </summary>
    private int GetExpectedFaceCount(string imagePath)
    {
        // For now, assume all test images should have at least 1 face
        return 1;
    }

    /// <summary>
    /// Parses comma-separated model list.
    /// </summary>
    private static List<string> ParseModelList(string? models)
    {
        if (string.IsNullOrEmpty(models))
            return new List<string>();

        return models.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim())
            .ToList();
    }

    /// <summary>
    /// Prints name matching benchmark result.
    /// </summary>
    private static void PrintNameMatchingResult(NameMatchingBenchmark result)
    {
        if (result.Error != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Error: {result.Error}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"  Tests: {result.TestCount}");
        Console.WriteLine($"  Accuracy: {result.Accuracy:P1}");
        Console.WriteLine($"  Avg Time: {result.AverageProcessingTimeMs:F0}ms");
        Console.WriteLine($"  Avg Confidence: {result.AverageConfidence:P1}");
        Console.WriteLine($"  Total Time: {result.DurationMs}ms");
    }

    /// <summary>
    /// Prints face detection benchmark result.
    /// </summary>
    private static void PrintFaceDetectionResult(FaceDetectionBenchmark result)
    {
        if (result.Error != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Error: {result.Error}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"  Tests: {result.TestCount}");
        Console.WriteLine($"  Accuracy: {result.Accuracy:P1}");
        Console.WriteLine($"  Avg Time: {result.AverageProcessingTimeMs:F0}ms");
        Console.WriteLine($"  Avg Confidence: {result.AverageConfidence:P1}");
        Console.WriteLine($"  Total Time: {result.DurationMs}ms");
    }

    /// <summary>
    /// Prints benchmark summary.
    /// </summary>
    private static void PrintSummary(BenchmarkResults results)
    {
        Console.WriteLine();
        Console.WriteLine("====================");
        Console.WriteLine("BENCHMARK SUMMARY");
        Console.WriteLine("====================");
        Console.WriteLine();

        if (results.NameMatchingResults.Count > 0)
        {
            Console.WriteLine("Name Matching:");
            var bestNameModel = results.NameMatchingResults.OrderByDescending(r => r.Accuracy).First();
            Console.WriteLine($"  Best Accuracy: {bestNameModel.ModelName} ({bestNameModel.Accuracy:P1})");
            var fastestNameModel = results.NameMatchingResults.OrderBy(r => r.AverageProcessingTimeMs).First();
            Console.WriteLine($"  Fastest: {fastestNameModel.ModelName} ({fastestNameModel.AverageProcessingTimeMs:F0}ms)");
            Console.WriteLine();
        }

        if (results.FaceDetectionResults.Count > 0)
        {
            Console.WriteLine("Face Detection:");
            var bestFaceModel = results.FaceDetectionResults.OrderByDescending(r => r.Accuracy).First();
            Console.WriteLine($"  Best Accuracy: {bestFaceModel.ModelName} ({bestFaceModel.Accuracy:P1})");
            var fastestFaceModel = results.FaceDetectionResults.OrderBy(r => r.AverageProcessingTimeMs).First();
            Console.WriteLine($"  Fastest: {fastestFaceModel.ModelName} ({fastestFaceModel.AverageProcessingTimeMs:F0}ms)");
        }
    }

    #endregion
}

#region Benchmark Models

public class BenchmarkResults
{
    [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("testDataPath")]
    public string TestDataPath { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("nameMatchingResults")]
    public List<NameMatchingBenchmark> NameMatchingResults { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("faceDetectionResults")]
    public List<FaceDetectionBenchmark> FaceDetectionResults { get; set; } = new();
}

public class NameMatchingBenchmark
{
    [System.Text.Json.Serialization.JsonPropertyName("modelName")]
    public string ModelName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("testCount")]
    public int TestCount { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("correctMatches")]
    public int CorrectMatches { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("averageProcessingTimeMs")]
    public double AverageProcessingTimeMs { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("averageConfidence")]
    public double AverageConfidence { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string? Error { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("testResults")]
    public List<NameMatchTestResult>? TestResults { get; set; }
}

public class FaceDetectionBenchmark
{
    [System.Text.Json.Serialization.JsonPropertyName("modelName")]
    public string ModelName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("testCount")]
    public int TestCount { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("correctDetections")]
    public int CorrectDetections { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("averageProcessingTimeMs")]
    public double AverageProcessingTimeMs { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("averageConfidence")]
    public double AverageConfidence { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string? Error { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("testResults")]
    public List<FaceDetectionTestResult>? TestResults { get; set; }
}

public class NameMatchingTestPair
{
    public string Name1 { get; set; } = string.Empty;
    public string Name2 { get; set; } = string.Empty;
    public bool ExpectedMatch { get; set; }
}

public class NameMatchTestResult
{
    [System.Text.Json.Serialization.JsonPropertyName("name1")]
    public string Name1 { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("name2")]
    public string Name2 { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expectedMatch")]
    public bool ExpectedMatch { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("isMatch")]
    public bool IsMatch { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("processingTimeMs")]
    public long ProcessingTimeMs { get; set; }
}

public class FaceDetectionTestResult
{
    [System.Text.Json.Serialization.JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expectedFaces")]
    public int ExpectedFaces { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("detectedFaces")]
    public int DetectedFaces { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("processingTimeMs")]
    public long ProcessingTimeMs { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("faceConfidence")]
    public double FaceConfidence { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string? Error { get; set; }
}

#endregion
