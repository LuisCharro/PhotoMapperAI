using PhotoMapperAI.Services.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PhotoMapperAI.Services.Diagnostics;

public sealed class PreflightResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> MissingOllamaModels { get; } = new();
    public List<string> MissingOpenCvFiles { get; } = new();

    public bool IsOk => Errors.Count == 0;

    public string BuildMessage(bool includeWarnings = true)
    {
        var lines = new List<string>();

        if (Errors.Count > 0)
        {
            lines.Add("Preflight failed:");
            lines.AddRange(Errors.Select(e => $"- {e}"));
        }

        if (MissingOllamaModels.Count > 0)
        {
            lines.Add("Missing Ollama models:");
            lines.AddRange(MissingOllamaModels.Select(m => $"- {m}"));
        }

        if (MissingOpenCvFiles.Count > 0)
        {
            lines.Add("Missing OpenCV DNN files:");
            lines.AddRange(MissingOpenCvFiles.Select(f => $"- {f}"));
        }

        if (includeWarnings && Warnings.Count > 0)
        {
            lines.Add("Warnings:");
            lines.AddRange(Warnings.Select(w => $"- {w}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string BuildWarningMessage()
    {
        if (MissingOllamaModels.Count == 0 && MissingOpenCvFiles.Count == 0 && Warnings.Count == 0)
            return string.Empty;

        var lines = new List<string>();

        if (MissingOllamaModels.Count > 0)
        {
            lines.Add("Missing Ollama models:");
            lines.AddRange(MissingOllamaModels.Select(m => $"- {m}"));
        }

        if (MissingOpenCvFiles.Count > 0)
        {
            lines.Add("Missing OpenCV DNN files:");
            lines.AddRange(MissingOpenCvFiles.Select(f => $"- {f}"));
        }

        if (Warnings.Count > 0)
        {
            lines.Add("Warnings:");
            lines.AddRange(Warnings.Select(w => $"- {w}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class PreflightChecker
{
    public static async Task<PreflightResult> CheckMapAsync(bool useAi, string nameModel)
    {
        var result = new PreflightResult();

        if (!useAi)
            return result;

        var modelsToCheck = new[] { nameModel };
        var (ollamaAvailable, missingModels) = await CheckOllamaAsync(modelsToCheck);

        if (!ollamaAvailable)
        {
            result.Errors.Add("Ollama server is not reachable at http://localhost:11434");
            return result;
        }

        if (missingModels.Count > 0)
        {
            result.MissingOllamaModels.AddRange(missingModels);
            result.Errors.Add("Required Ollama name model is missing.");
        }

        return result;
    }

    public static async Task<PreflightResult> CheckGenerateAsync(string faceDetectionModel, bool downloadOpenCvModels = false)
    {
        var result = new PreflightResult();
        var models = faceDetectionModel
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();

        if (models.Count == 0)
        {
            result.Errors.Add("No face detection model specified.");
            return result;
        }

        var availableCount = 0;
        var ollamaCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            if (IsCenterOrHaar(model))
            {
                availableCount++;
                continue;
            }

            if (IsOpenCvDnnModel(model))
            {
                var baseModelsPath = Path.Combine(AppContext.BaseDirectory, "models");
                var (modelsPath, prototxtPath, weightsPath) = OpenCVDNNFaceDetectionService.GetResolvedModelPaths(baseModelsPath);
                var missingFiles = new List<string>();

                if (!File.Exists(prototxtPath))
                    missingFiles.Add(prototxtPath);

                if (!File.Exists(weightsPath))
                    missingFiles.Add(weightsPath);

                if (missingFiles.Count > 0 && downloadOpenCvModels)
                {
                    var download = await OpenCvModelDownloader.EnsureModelsAsync(modelsPath);
                    if (!download.Success)
                    {
                        result.Warnings.Add($"Failed to download OpenCV DNN files: {download.Error}");
                    }
                    else if (download.Downloaded.Count > 0)
                    {
                        result.Warnings.Add("Downloaded missing OpenCV DNN files.");
                    }

                    missingFiles = new List<string>();
                    if (!File.Exists(prototxtPath))
                        missingFiles.Add(prototxtPath);
                    if (!File.Exists(weightsPath))
                        missingFiles.Add(weightsPath);
                }

                if (missingFiles.Count == 0)
                {
                    availableCount++;
                }
                else
                {
                    foreach (var file in missingFiles)
                    {
                        if (!result.MissingOpenCvFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                            result.MissingOpenCvFiles.Add(file);
                    }

                    result.Warnings.Add($"OpenCV DNN files missing for model '{model}' (models path: {modelsPath}).");
                }

                continue;
            }

            if (IsOllamaModel(model))
            {
                ollamaCandidates.Add(model);
                continue;
            }

            ollamaCandidates.Add(model);
        }

        if (ollamaCandidates.Count > 0)
        {
            var (ollamaAvailable, missingModels) = await CheckOllamaAsync(ollamaCandidates);

            if (!ollamaAvailable)
            {
                result.Errors.Add("Ollama server is not reachable at http://localhost:11434");
            }
            else
            {
                foreach (var model in ollamaCandidates)
                {
                    if (!missingModels.Contains(model, StringComparer.OrdinalIgnoreCase))
                        availableCount++;
                }

                result.MissingOllamaModels.AddRange(missingModels);
                if (missingModels.Count > 0)
                {
                    result.Warnings.Add("Some Ollama face detection models are missing.");
                }
            }
        }

        if (availableCount == 0)
        {
            result.Errors.Add("No face detection models are available.");
        }

        return result;
    }

    private static bool IsOpenCvDnnModel(string model)
    {
        return string.Equals(model, "opencv-dnn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model, "yolov8-face", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCenterOrHaar(string model)
    {
        return string.Equals(model, "center", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model, "haar", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model, "haar-cascade", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOllamaModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        return model.Contains(':', StringComparison.OrdinalIgnoreCase)
            || model.Contains("llava", StringComparison.OrdinalIgnoreCase)
            || model.Contains("qwen3-vl", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(bool Available, List<string> MissingModels)> CheckOllamaAsync(IEnumerable<string> requiredModels)
    {
        var client = new OllamaClient();
        if (!await client.IsAvailableAsync())
            return (false, new List<string>());

        var availableModels = await client.GetAvailableModelsAsync();
        var missing = new List<string>();

        foreach (var model in requiredModels)
        {
            if (OllamaModelPolicy.IsCloudModel(model))
                continue;

            var exists = availableModels.Any(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                missing.Add(model);
        }

        return (true, missing);
    }
}
