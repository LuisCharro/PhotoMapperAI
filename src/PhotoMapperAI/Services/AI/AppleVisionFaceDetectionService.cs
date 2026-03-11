using PhotoMapperAI.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service for macOS using Apple's Vision framework via a small Swift helper.
/// </summary>
public sealed class AppleVisionFaceDetectionService : IFaceDetectionService
{
    private static readonly SemaphoreSlim HelperBuildLock = new(1, 1);

    private readonly string _helperSourcePath;
    private readonly string _helperBinaryPath;
    private bool _initialized;

    public AppleVisionFaceDetectionService()
    {
        var helperDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "Caches",
            "PhotoMapperAI",
            "apple-vision");

        Directory.CreateDirectory(helperDir);

        _helperBinaryPath = Path.Combine(helperDir, "face-detection-helper");
        _helperSourcePath = ResolveHelperSourcePath();
    }

    public string ModelName => "apple-vision";

    public async Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath)
    {
        var startTime = Stopwatch.StartNew();

        if (!_initialized && !await InitializeAsync())
        {
            return BuildFailureResult(startTime.ElapsedMilliseconds, "Apple Vision helper is not initialized.");
        }

        if (!File.Exists(imagePath))
        {
            return BuildFailureResult(startTime.ElapsedMilliseconds, $"Image file not found: {imagePath}");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _helperBinaryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add(imagePath);

            using var process = Process.Start(psi);
            if (process == null)
            {
                return BuildFailureResult(startTime.ElapsedMilliseconds, "Failed to start Apple Vision helper.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return BuildFailureResult(
                    startTime.ElapsedMilliseconds,
                    string.IsNullOrWhiteSpace(stderr)
                        ? $"Apple Vision helper exited with code {process.ExitCode}."
                        : stderr.Trim());
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return BuildFailureResult(startTime.ElapsedMilliseconds, "Apple Vision helper returned no output.");
            }

            var payload = JsonSerializer.Deserialize<HelperPayload>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
            {
                return BuildFailureResult(startTime.ElapsedMilliseconds, "Apple Vision helper returned invalid JSON.");
            }

            if (!string.IsNullOrWhiteSpace(payload.Error))
            {
                return BuildFailureResult(startTime.ElapsedMilliseconds, payload.Error);
            }

            var result = new FaceLandmarks
            {
                FaceDetected = payload.FaceDetected,
                BothEyesDetected = payload.BothEyesDetected,
                FaceConfidence = payload.FaceConfidence,
                ProcessingTimeMs = startTime.ElapsedMilliseconds,
                ModelUsed = ModelName
            };

            if (payload.FaceRect != null)
            {
                result.FaceRect = new Rectangle(
                    payload.FaceRect.X,
                    payload.FaceRect.Y,
                    payload.FaceRect.Width,
                    payload.FaceRect.Height);
            }

            if (payload.FaceCenter != null)
            {
                result.FaceCenter = new Point(payload.FaceCenter.X, payload.FaceCenter.Y);
            }

            if (payload.LeftEye != null)
            {
                result.LeftEye = new Point(payload.LeftEye.X, payload.LeftEye.Y);
            }

            if (payload.RightEye != null)
            {
                result.RightEye = new Point(payload.RightEye.X, payload.RightEye.Y);
            }

            if (result.FaceDetected && result.FaceRect != null && (result.LeftEye == null || result.RightEye == null))
            {
                // Preserve the existing crop behavior when only face bounds are available.
                var faceRect = result.FaceRect;
                var eyeY = Clamp(faceRect.Y + (int)Math.Round(faceRect.Height * 0.40), 0, faceRect.Y + faceRect.Height - 1);
                result.LeftEye ??= new Point(faceRect.X + (int)Math.Round(faceRect.Width * 0.30), eyeY);
                result.RightEye ??= new Point(faceRect.X + (int)Math.Round(faceRect.Width * 0.70), eyeY);
                result.BothEyesDetected = true;
                result.Metadata["eyes"] = "estimated_from_face_rect";
            }
            else if (result.LeftEye != null && result.RightEye != null)
            {
                result.BothEyesDetected = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            return BuildFailureResult(startTime.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<List<FaceLandmarks>> DetectFaceLandmarksBatchAsync(List<string> imagePaths)
    {
        var results = new List<FaceLandmarks>(imagePaths.Count);
        foreach (var imagePath in imagePaths)
        {
            results.Add(await DetectFaceLandmarksAsync(imagePath));
        }

        return results;
    }

    public async Task<bool> IsAvailableAsync()
    {
        return await InitializeAsync();
    }

    public async Task<bool> InitializeAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _initialized = false;
            return false;
        }

        await HelperBuildLock.WaitAsync();
        try
        {
            if (!File.Exists(_helperSourcePath))
            {
                _initialized = false;
                return false;
            }

            var needsBuild = !File.Exists(_helperBinaryPath)
                || File.GetLastWriteTimeUtc(_helperBinaryPath) < File.GetLastWriteTimeUtc(_helperSourcePath);

            if (!needsBuild)
            {
                _initialized = true;
                return true;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "swiftc",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            psi.ArgumentList.Add("-O");
            psi.ArgumentList.Add("-framework");
            psi.ArgumentList.Add("Vision");
            psi.ArgumentList.Add("-framework");
            psi.ArgumentList.Add("Foundation");
            psi.ArgumentList.Add("-framework");
            psi.ArgumentList.Add("CoreGraphics");
            psi.ArgumentList.Add("-framework");
            psi.ArgumentList.Add("ImageIO");
            psi.ArgumentList.Add(_helperSourcePath);
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(_helperBinaryPath);

            using var process = Process.Start(psi);
            if (process == null)
            {
                _initialized = false;
                return false;
            }

            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stderr = await stderrTask;

            _initialized = process.ExitCode == 0 && File.Exists(_helperBinaryPath);
            if (!_initialized && !string.IsNullOrWhiteSpace(stderr))
            {
                Console.WriteLine($"[AppleVision] Helper build failed: {stderr.Trim()}");
            }

            return _initialized;
        }
        finally
        {
            HelperBuildLock.Release();
        }
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    private static string ResolveHelperSourcePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDirectory, "Resources", "AppleVision", "FaceDetectionHelper.swift"),
            Path.Combine(baseDirectory, "AppleVision", "FaceDetectionHelper.swift"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "PhotoMapperAI", "Resources", "AppleVision", "FaceDetectionHelper.swift")
        };

        var path = candidatePaths.FirstOrDefault(File.Exists);
        return path ?? candidatePaths[0];
    }

    private FaceLandmarks BuildFailureResult(long elapsedMs, string error)
    {
        return new FaceLandmarks
        {
            FaceDetected = false,
            ProcessingTimeMs = elapsedMs,
            ModelUsed = ModelName,
            Metadata = new Dictionary<string, string>
            {
                ["error"] = error
            }
        };
    }

    private sealed class HelperPayload
    {
        public bool FaceDetected { get; set; }
        public bool BothEyesDetected { get; set; }
        public double FaceConfidence { get; set; }
        public HelperRect? FaceRect { get; set; }
        public HelperPoint? LeftEye { get; set; }
        public HelperPoint? RightEye { get; set; }
        public HelperPoint? FaceCenter { get; set; }
        public string? Error { get; set; }
    }

    private sealed class HelperRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private sealed class HelperPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
