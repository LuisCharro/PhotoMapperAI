using McMaster.Extensions.CommandLineUtils;
using System.Text.Json;

namespace PhotoMapperAI.Commands;

/// <summary>
/// Benchmark compare command - compare benchmark result files.
/// </summary>
[Command("benchmark-compare", Description = "Compare two benchmark JSON result files", ExtendedHelpText = @"
Compares benchmark result files and prints metric deltas.
Useful for checking Windows vs macOS benchmark differences.

Examples:
  photomapperai benchmark-compare --baseline benchmark-results/benchmark-20260212-075152.json --candidate benchmark-results/windows-benchmark.json
  photomapperai benchmark-compare --baseline mac.json --candidate windows.json --faceModel opencv-dnn
")]
public class BenchmarkCompareCommand
{
    [Option(ShortName = "b", LongName = "baseline", Description = "Baseline benchmark JSON path (e.g. macOS)")]
    public string BaselinePath { get; set; } = string.Empty;

    [Option(ShortName = "c", LongName = "candidate", Description = "Candidate benchmark JSON path (e.g. Windows)")]
    public string CandidatePath { get; set; } = string.Empty;

    [Option(ShortName = "f", LongName = "faceModel", Description = "Face model filter (default: first result)")]
    public string? FaceModel { get; set; }

    [Option(ShortName = "n", LongName = "nameModel", Description = "Name model filter (default: first result)")]
    public string? NameModel { get; set; }

    public int OnExecute()
    {
        if (!File.Exists(BaselinePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Baseline file not found: {BaselinePath}");
            Console.ResetColor();
            return 1;
        }

        if (!File.Exists(CandidatePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Candidate file not found: {CandidatePath}");
            Console.ResetColor();
            return 1;
        }

        try
        {
            var baseline = LoadResults(BaselinePath);
            var candidate = LoadResults(CandidatePath);

            Console.WriteLine("Benchmark Comparison");
            Console.WriteLine("====================");
            Console.WriteLine($"Baseline : {BaselinePath}");
            Console.WriteLine($"Candidate: {CandidatePath}");
            Console.WriteLine();

            CompareNameMatching(baseline, candidate, NameModel);
            Console.WriteLine();
            CompareFaceDetection(baseline, candidate, FaceModel);

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error comparing benchmark files: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static BenchmarkResults LoadResults(string path)
    {
        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<BenchmarkResults>(json);
        if (result == null)
            throw new InvalidOperationException($"Invalid benchmark JSON: {path}");

        return result;
    }

    private static void CompareNameMatching(BenchmarkResults baseline, BenchmarkResults candidate, string? modelName)
    {
        var baseResult = SelectNameResult(baseline, modelName);
        var candidateResult = SelectNameResult(candidate, modelName);

        if (baseResult == null || candidateResult == null)
        {
            Console.WriteLine("Name Matching: no comparable results found.");
            return;
        }

        Console.WriteLine($"Name Matching ({baseResult.ModelName} vs {candidateResult.ModelName})");
        PrintDelta("Accuracy", baseResult.Accuracy, candidateResult.Accuracy, asPercent: true);
        PrintDelta("Avg Time (ms)", baseResult.AverageProcessingTimeMs, candidateResult.AverageProcessingTimeMs);
        PrintDelta("Avg Confidence", baseResult.AverageConfidence, candidateResult.AverageConfidence, asPercent: true);
        PrintDelta("Test Count", baseResult.TestCount, candidateResult.TestCount, asPercent: false, isCount: true);
    }

    private static void CompareFaceDetection(BenchmarkResults baseline, BenchmarkResults candidate, string? modelName)
    {
        var baseResult = SelectFaceResult(baseline, modelName);
        var candidateResult = SelectFaceResult(candidate, modelName);

        if (baseResult == null || candidateResult == null)
        {
            Console.WriteLine("Face Detection: no comparable results found.");
            return;
        }

        Console.WriteLine($"Face Detection ({baseResult.ModelName} vs {candidateResult.ModelName})");
        PrintDelta("Accuracy", baseResult.Accuracy, candidateResult.Accuracy, asPercent: true);
        PrintDelta("Avg Time (ms)", baseResult.AverageProcessingTimeMs, candidateResult.AverageProcessingTimeMs);
        PrintDelta("Avg Confidence", baseResult.AverageConfidence, candidateResult.AverageConfidence, asPercent: true);
        PrintDelta("Test Count", baseResult.TestCount, candidateResult.TestCount, asPercent: false, isCount: true);
    }

    private static NameMatchingBenchmark? SelectNameResult(BenchmarkResults results, string? modelName)
    {
        if (results.NameMatchingResults.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            return results.NameMatchingResults
                .FirstOrDefault(r => string.Equals(r.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        }

        return results.NameMatchingResults.FirstOrDefault();
    }

    private static FaceDetectionBenchmark? SelectFaceResult(BenchmarkResults results, string? modelName)
    {
        if (results.FaceDetectionResults.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            return results.FaceDetectionResults
                .FirstOrDefault(r => string.Equals(r.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        }

        return results.FaceDetectionResults.FirstOrDefault();
    }

    private static void PrintDelta(string metricName, double baseline, double candidate, bool asPercent = false, bool isCount = false)
    {
        var delta = candidate - baseline;
        var deltaPrefix = delta >= 0 ? "+" : string.Empty;

        Console.Write($"  {metricName}: ");
        if (isCount)
        {
            Console.Write($"{candidate:0} (baseline {baseline:0}, delta {deltaPrefix}{delta:0})");
        }
        else if (asPercent)
        {
            Console.Write($"{candidate:P2} (baseline {baseline:P2}, delta {deltaPrefix}{delta:P2})");
        }
        else
        {
            Console.Write($"{candidate:0.##} (baseline {baseline:0.##}, delta {deltaPrefix}{delta:0.##})");
        }

        Console.WriteLine();
    }
}
