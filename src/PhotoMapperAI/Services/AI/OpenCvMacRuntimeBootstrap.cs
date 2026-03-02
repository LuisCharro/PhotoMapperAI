using System.Runtime.InteropServices;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Ensures OpenCV native dependencies are preloaded on macOS before OpenCvSharp initializes.
/// This avoids dyld resolution issues when the app is launched via the dotnet host.
/// </summary>
internal static class OpenCvMacRuntimeBootstrap
{
    private const int RtldNow = 0x2;
    private const int RtldGlobal = 0x8;

    private static readonly object Sync = new();
    private static bool _initialized;

    private static readonly string[] DependencyOrder =
    {
        "libpng16.16.dylib",
        "libfreetype.6.dylib",
        "libgraphite2.3.2.1.dylib",
        "libpcre2-8.0.dylib",
        "libintl.8.dylib",
        "libglib-2.0.0.dylib",
        "libharfbuzz.0.dylib",
        "libsz.2.0.1.dylib",
        "libhdf5.310.dylib",
        "libdc1394.26.dylib",
        "libomp.dylib",
        "libOpenCvSharpExtern.dylib"
    };

    public static void EnsureInitialized()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        lock (Sync)
        {
            if (_initialized)
                return;

            var searchDirs = ResolveSearchDirectories()
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ConfigureDyldPaths(searchDirs);
            PreloadDependencies(searchDirs);

            _initialized = true;
        }
    }

    private static void ConfigureDyldPaths(IReadOnlyList<string> searchDirs)
    {
        if (searchDirs.Count == 0)
            return;

        var prepend = string.Join(":", searchDirs);
        PrependEnvPath("DYLD_LIBRARY_PATH", prepend);
        PrependEnvPath("DYLD_FALLBACK_LIBRARY_PATH", prepend);
    }

    private static void PreloadDependencies(IReadOnlyList<string> searchDirs)
    {
        foreach (var library in DependencyOrder)
        {
            var loaded = false;
            foreach (var dir in searchDirs)
            {
                var fullPath = Path.Combine(dir, library);
                if (!File.Exists(fullPath))
                    continue;

                try
                {
                    var handle = dlopen(fullPath, RtldNow | RtldGlobal);
                    if (handle == IntPtr.Zero)
                        continue;

                    loaded = true;
                    break;
                }
                catch
                {
                    // Continue trying other candidate paths.
                }
            }

            if (!loaded)
            {
                try
                {
                    var handle = dlopen(library, RtldNow | RtldGlobal);
                    if (handle != IntPtr.Zero)
                        continue;
                }
                catch
                {
                    // Best-effort preload. OpenCvSharp init will provide final diagnostics if needed.
                }
            }
        }
    }

    private static List<string> ResolveSearchDirectories()
    {
        var result = new List<string>();
        var baseDir = AppContext.BaseDirectory;
        var cwd = Directory.GetCurrentDirectory();

        AddIfNotEmpty(result, Path.Combine(baseDir, "runtimes", "osx-arm64", "native"));
        AddIfNotEmpty(result, Path.Combine(baseDir, "..", "libs"));

        AddIfNotEmpty(result, Path.Combine(cwd, "src", "PhotoMapperAI.UI", "bin", "Debug", "net10.0", "runtimes", "osx-arm64", "native"));
        AddIfNotEmpty(result, Path.Combine(cwd, "src", "PhotoMapperAI.UI", "bin", "Debug", "libs"));

        AddParentCandidates(result, baseDir, "runtimes/osx-arm64/native", maxDepth: 8);
        AddParentCandidates(result, baseDir, "libs", maxDepth: 8);
        AddParentCandidates(result, cwd, "runtimes/osx-arm64/native", maxDepth: 8);
        AddParentCandidates(result, cwd, "libs", maxDepth: 8);

        return result;
    }

    private static void AddParentCandidates(List<string> destinations, string startPath, string childPath, int maxDepth)
    {
        var current = new DirectoryInfo(startPath);
        for (var i = 0; i < maxDepth && current != null; i++)
        {
            AddIfNotEmpty(destinations, Path.Combine(current.FullName, childPath));
            current = current.Parent;
        }
    }

    private static void AddIfNotEmpty(List<string> destinations, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            destinations.Add(Path.GetFullPath(path));
        }
        catch
        {
            // Ignore malformed paths.
        }
    }

    private static void PrependEnvPath(string variable, string prependValue)
    {
        var current = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(current))
        {
            Environment.SetEnvironmentVariable(variable, prependValue);
            return;
        }

        var values = current.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (values.Contains(prependValue, StringComparer.Ordinal))
            return;

        Environment.SetEnvironmentVariable(variable, $"{prependValue}:{current}");
    }

    [DllImport("libdl")]
    private static extern IntPtr dlopen(string path, int mode);
}
