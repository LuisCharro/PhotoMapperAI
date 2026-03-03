namespace PhotoMapperAI.Services.Diagnostics;

/// <summary>
/// Resolves output profile aliases (test/prod) to concrete directories.
/// </summary>
public static class OutputProfileResolver
{
    public static string Resolve(string profile, string baseOutputPath)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new ArgumentException("Output profile cannot be empty.", nameof(profile));
        }

        var normalized = profile.Trim().ToLowerInvariant();
        return normalized switch
        {
            "test" => ResolveOrFallback("PHOTOMAPPER_OUTPUT_TEST", Path.Combine(baseOutputPath, "test")),
            "prod" => ResolveOrFallback("PHOTOMAPPER_OUTPUT_PROD", Path.Combine(baseOutputPath, "prod")),
            _ => throw new InvalidOperationException($"Unsupported output profile '{profile}'. Use 'test' or 'prod'.")
        };
    }

    private static string ResolveOrFallback(string envVar, string fallback)
    {
        var envValue = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue!;
        }

        return fallback;
    }
}
