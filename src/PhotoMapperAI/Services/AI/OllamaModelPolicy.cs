namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Ollama model management policy helpers.
/// </summary>
public static class OllamaModelPolicy
{
    /// <summary>
    /// Cloud models are ignored by local model memory management.
    /// </summary>
    public static bool IsCloudModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return false;

        var normalized = modelName.Trim();

        // Cloud naming conventions in Ollama:
        // - suffix ":cloud" (e.g. kimi-k2.5:cloud)
        // - cloud tag in variant suffix (e.g. qwen3-coder:480b-cloud)
        return normalized.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calculates local models that should be unloaded before using requiredModel.
    /// </summary>
    public static List<string> GetLocalModelsToUnload(IEnumerable<string> runningModels, string requiredModel)
    {
        if (string.IsNullOrWhiteSpace(requiredModel))
            return new List<string>();

        // If required model is cloud, local-model memory policy does not apply.
        if (IsCloudModel(requiredModel))
            return new List<string>();

        var localRunning = runningModels
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Where(m => !IsCloudModel(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (localRunning.Count == 0)
            return new List<string>();

        return localRunning
            .Where(m => !string.Equals(m, requiredModel, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
