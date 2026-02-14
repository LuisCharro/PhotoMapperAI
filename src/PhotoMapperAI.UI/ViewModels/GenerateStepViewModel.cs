using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Commands;

namespace PhotoMapperAI.UI.ViewModels;

public partial class GenerateStepViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    public GenerateStepViewModel()
    {
        // Prefer a convenient default profile path when available.
        // User can clear it to use manual single-size mode.
        var defaultProfile = ResolveDefaultSizeProfilePath();
        if (!string.IsNullOrWhiteSpace(defaultProfile))
        {
            SizeProfilePath = defaultProfile;
        }
    }

    [ObservableProperty]
    private string _inputCsvPath = string.Empty;

    [ObservableProperty]
    private string _photosDirectory = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _imageFormat = "jpg";

    [ObservableProperty]
    private string _faceDetectionModel = "opencv-dnn";

    [ObservableProperty]
    private string _sizeProfilePath = string.Empty;

    public bool ManualSizeControlsEnabled => string.IsNullOrWhiteSpace(SizeProfilePath);
    public bool CanUseAllSizes => !string.IsNullOrWhiteSpace(SizeProfilePath);

    [ObservableProperty]
    private bool _allSizes;

    [ObservableProperty]
    private string _outputProfile = "none";

    [ObservableProperty]
    private int _portraitWidth = 200;

    [ObservableProperty]
    private int _portraitHeight = 300;

    [ObservableProperty]
    private bool _portraitOnly;

    [ObservableProperty]
    private bool _downloadOpenCvModels;

    [ObservableProperty]
    private bool _writeDebugArtifacts = true;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingStatus = string.Empty;

    [ObservableProperty]
    private int _portraitsGenerated;

    [ObservableProperty]
    private int _portraitsFailed;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isCheckingModel;

    [ObservableProperty]
    private string _modelDiagnosticStatus = string.Empty;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    public ObservableCollection<string> LogLines { get; } = new();

    public List<string> ImageFormats { get; } = new() { "jpg", "png" };

    public List<string> OutputProfiles { get; } = new() { "none", "test", "prod" };

    public List<string> FaceDetectionModels { get; } = new()
    {
        "opencv-dnn",
        "llava:7b",
        "qwen3-vl",
        "yolov8-face",
        "haar-cascade",
        "center"
    };

    [RelayCommand]
    private async Task BrowseCsvFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearInputCsv()
    {
        InputCsvPath = string.Empty;
        IsComplete = false;
    }

    [RelayCommand]
    private async Task BrowsePhotosDirectory()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearPhotosDirectory()
    {
        PhotosDirectory = string.Empty;
        IsComplete = false;
    }

    [RelayCommand]
    private async Task BrowseOutputDirectory()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearOutputDirectory()
    {
        OutputDirectory = string.Empty;
        IsComplete = false;
    }

    [RelayCommand]
    private void ClearSizeProfilePath()
    {
        SizeProfilePath = string.Empty;
        AllSizes = false;
        IsComplete = false;
    }

    partial void OnSizeProfilePathChanged(string value)
    {
        OnPropertyChanged(nameof(ManualSizeControlsEnabled));
        OnPropertyChanged(nameof(CanUseAllSizes));

        if (string.IsNullOrWhiteSpace(value))
        {
            AllSizes = false;
        }
    }

    [RelayCommand]
    private async Task LoadPreviewImage()
    {
        await Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(PhotosDirectory) || !Directory.Exists(PhotosDirectory))
        {
            PreviewStatus = "Select a valid photos directory to load preview.";
            PreviewImage = null;
            return;
        }

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp"
        };

        var firstImagePath = Directory.EnumerateFiles(PhotosDirectory, "*.*", SearchOption.AllDirectories)
            .FirstOrDefault(path => supportedExtensions.Contains(Path.GetExtension(path)));

        if (string.IsNullOrWhiteSpace(firstImagePath))
        {
            PreviewStatus = "No supported images found in photos directory.";
            PreviewImage = null;
            return;
        }

        try
        {
            await using var stream = File.OpenRead(firstImagePath);
            PreviewImage = new Bitmap(stream);
            PreviewStatus = $"Preview loaded: {Path.GetFileName(firstImagePath)}";
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Failed to load preview: {ex.Message}";
            PreviewImage = null;
        }
    }

    [RelayCommand]
    private async Task CheckFaceModel()
    {
        if (IsProcessing)
            return;

        IsCheckingModel = true;
        ModelDiagnosticStatus = $"Checking model '{FaceDetectionModel}'...";

        try
        {
            var preflight = await PreflightChecker.CheckGenerateAsync(FaceDetectionModel, DownloadOpenCvModels);
            if (!preflight.IsOk)
            {
                ModelDiagnosticStatus = preflight.BuildMessage();
                return;
            }

            var warningMessage = preflight.BuildWarningMessage();
            ModelDiagnosticStatus = string.IsNullOrWhiteSpace(warningMessage)
                ? $"✓ Face detection model ready: {FaceDetectionModel}"
                : $"✓ Face detection model ready: {FaceDetectionModel}\n{warningMessage}";
        }
        catch (Exception ex)
        {
            ModelDiagnosticStatus = $"✗ Model check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingModel = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteGenerate()
    {
        if (string.IsNullOrWhiteSpace(InputCsvPath) ||
            string.IsNullOrWhiteSpace(PhotosDirectory) ||
            string.IsNullOrWhiteSpace(OutputDirectory))
        {
            ProcessingStatus = "Please select CSV file, photos directory, and output directory";
            return;
        }

        LogLines.Clear();
        IsProcessing = true;
        IsComplete = false;
        PortraitsGenerated = 0;
        PortraitsFailed = 0;
        ProcessingStatus = "Generating portraits...";
        Progress = 0;
        _cancellationTokenSource = new CancellationTokenSource();

        var selectedFaceDetectionModel = FaceDetectionModel;

        try
        {
            var preflight = await PreflightChecker.CheckGenerateAsync(selectedFaceDetectionModel, DownloadOpenCvModels);
            if (!preflight.IsOk)
            {
                ProcessingStatus = preflight.BuildMessage();
                IsProcessing = false;
                return;
            }

            var warningMessage = preflight.BuildWarningMessage();
            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                ModelDiagnosticStatus = warningMessage;
            }

            var resolvedService = FaceDetectionServiceFactory.Create(selectedFaceDetectionModel);
            AppendLog($"Resolved face detection service: {resolvedService.ModelName}");

            // Execute generation
            var baseOutputDirectory = OutputDirectory;
            if (!string.Equals(OutputProfile, "none", StringComparison.OrdinalIgnoreCase))
            {
                baseOutputDirectory = ResolveOutputProfile(OutputProfile, OutputDirectory);
                AppendLog($"Using output profile '{OutputProfile}' => {baseOutputDirectory}");
            }

            var variants = new List<(string key, int width, int height, string outputDir)>();

            if (!string.IsNullOrWhiteSpace(SizeProfilePath))
            {
                var profile = LoadSizeProfile(SizeProfilePath);
                AppendLog($"Using size profile '{profile.Name}' with {profile.Variants.Count} variants");

                if (AllSizes)
                {
                    variants.AddRange(profile.Variants.Select(v => (
                        v.Key,
                        v.Width,
                        v.Height,
                        Path.Combine(baseOutputDirectory, string.IsNullOrWhiteSpace(v.OutputSubfolder) ? v.Key : v.OutputSubfolder)
                    )));
                }
                else
                {
                    // Prefer a legacy-compatible default when profile is selected but all-sizes is off.
                    var v = profile.Variants.FirstOrDefault(x =>
                                string.Equals(x.Key, "x200x300", StringComparison.OrdinalIgnoreCase)
                                || (x.Width == 200 && x.Height == 300))
                            ?? profile.Variants.First();
                    variants.Add((v.Key, v.Width, v.Height, baseOutputDirectory));
                }
            }
            else
            {
                variants.Add(("single", PortraitWidth, PortraitHeight, baseOutputDirectory));
            }

            var totalGenerated = 0;
            var totalFailed = 0;
            var anyFailure = false;

            foreach (var variant in variants)
            {
                Directory.CreateDirectory(variant.outputDir);
                AppendLog($"Running variant '{variant.key}' => {variant.width}x{variant.height} -> {variant.outputDir}");
                AppendLog(BuildGenerateCommandPreview(
                    InputCsvPath,
                    PhotosDirectory,
                    variant.outputDir,
                    ImageFormat,
                    selectedFaceDetectionModel,
                    PortraitOnly,
                    variant.width,
                    variant.height,
                    DownloadOpenCvModels));

                var log = new Progress<string>(AppendLog);

                if (WriteDebugArtifacts)
                {
                    WriteVariantDebugArtifact(
                        variant.key,
                        variant.width,
                        variant.height,
                        variant.outputDir,
                        selectedFaceDetectionModel);
                }

                var result = await ExecuteVariantViaCliAsync(
                    InputCsvPath,
                    PhotosDirectory,
                    variant.outputDir,
                    ImageFormat,
                    selectedFaceDetectionModel,
                    PortraitOnly,
                    variant.width,
                    variant.height,
                    DownloadOpenCvModels,
                    _cancellationTokenSource.Token,
                    log);

                totalGenerated += result.PortraitsGenerated;
                totalFailed += result.PortraitsFailed;

                if (result.IsCancelled)
                {
                    ProcessingStatus = $"⚠ Generation cancelled ({result.ProcessedPlayers}/{result.TotalPlayers} processed)";
                    IsComplete = false;
                    return;
                }

                if (result.ExitCode != 0)
                {
                    anyFailure = true;
                    AppendLog($"Variant '{variant.key}' failed with exit code {result.ExitCode}");
                }
            }

            PortraitsGenerated = totalGenerated;
            PortraitsFailed = totalFailed;

            if (anyFailure)
            {
                ProcessingStatus = $"✗ Generation completed with failures ({PortraitsGenerated} generated, {PortraitsFailed} failed)";
                IsComplete = false;
                return;
            }

            ProcessingStatus = $"✓ Generated {PortraitsGenerated} portraits ({PortraitsFailed} failed)";
            IsComplete = true;
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            ProcessingStatus = "⚠ Generation cancelled";
            IsComplete = false;
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error: {ex.Message}";
            IsComplete = false;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void CancelGenerate()
    {
        if (IsProcessing)
        {
            _cancellationTokenSource?.Cancel();
            ProcessingStatus = "Cancelling generation...";
        }
    }

    private void AppendLog(string message)
    {
        if (LogLines.Count >= 200)
        {
            LogLines.RemoveAt(0);
        }

        LogLines.Add(message);
    }

    private async Task<GeneratePhotosResult> ExecuteVariantViaCliAsync(
        string inputCsvPath,
        string photosDir,
        string outputDir,
        string format,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        bool downloadOpenCvModels,
        CancellationToken cancellationToken,
        IProgress<string>? log)
    {
        var args = BuildGenerateArgs(inputCsvPath, photosDir, outputDir, format, faceDetectionModel, portraitOnly, width, height, downloadOpenCvModels);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        var generated = 0;
        var failed = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync();
            if (line == null)
                break;

            log?.Report(line);

            var m = Regex.Match(line, @"Generated\s+(\d+)\s+portraits\s+\((\d+)\s+failed\)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                generated = int.Parse(m.Groups[1].Value);
                failed = int.Parse(m.Groups[2].Value);
            }
        }

        var stdErr = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            foreach (var line in stdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                log?.Report(line.TrimEnd('\r'));
            }
        }

        await process.WaitForExitAsync(cancellationToken);

        return new GeneratePhotosResult
        {
            ExitCode = process.ExitCode,
            PortraitsGenerated = generated,
            PortraitsFailed = failed,
            IsCancelled = cancellationToken.IsCancellationRequested
        };
    }

    private static List<string> BuildGenerateArgs(
        string inputCsvPath,
        string photosDir,
        string outputDir,
        string format,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        bool downloadOpenCvModels)
    {
        var args = new List<string>
        {
            "run", "--project", "src/PhotoMapperAI", "--", "generatephotos",
            "--inputCsvPath", inputCsvPath,
            "--photosDir", photosDir,
            "--processedPhotosOutputPath", outputDir,
            "--format", format,
            "--faceDetection", faceDetectionModel,
            "--faceWidth", width.ToString(),
            "--faceHeight", height.ToString(),
            "--noCache"
        };

        if (portraitOnly)
            args.Add("--portraitOnly");

        if (downloadOpenCvModels)
            args.Add("--downloadOpenCvModels");

        return args;
    }

    private void WriteVariantDebugArtifact(string key, int width, int height, string outputDir, string selectedFaceDetectionModel)
    {
        try
        {
            var payload = new
            {
                utc = DateTime.UtcNow,
                workingDirectory = Directory.GetCurrentDirectory(),
                inputCsvPath = InputCsvPath,
                photosDirectory = PhotosDirectory,
                outputDirectory = outputDir,
                imageFormat = ImageFormat,
                faceDetectionModel = selectedFaceDetectionModel,
                portraitOnly = PortraitOnly,
                width,
                height,
                downloadOpenCvModels = DownloadOpenCvModels,
                executionMode = "external-cli",
                args = BuildGenerateArgs(InputCsvPath, PhotosDirectory, outputDir, ImageFormat, selectedFaceDetectionModel, PortraitOnly, width, height, DownloadOpenCvModels)
            };

            Directory.CreateDirectory(outputDir);
            var path = Path.Combine(outputDir, "_gui_run_debug.json");
            File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            AppendLog($"Debug artifact: {path}");
        }
        catch (Exception ex)
        {
            AppendLog($"Debug artifact write failed: {ex.Message}");
        }
    }

    private static string BuildGenerateCommandPreview(
        string inputCsvPath,
        string photosDir,
        string outputDir,
        string format,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        bool downloadOpenCvModels)
    {
        var parts = BuildGenerateArgs(inputCsvPath, photosDir, outputDir, format, faceDetectionModel, portraitOnly, width, height, downloadOpenCvModels);
        return $"Working directory: {Directory.GetCurrentDirectory()}\nCommand: dotnet " + string.Join(" ", parts.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));
    }

    private static string ResolveOutputProfile(string profile, string baseOutputPath)
    {
        var normalized = (profile ?? "none").Trim().ToLowerInvariant();
        return normalized switch
        {
            "none" => baseOutputPath,
            "test" => Environment.GetEnvironmentVariable("PHOTOMAPPER_OUTPUT_TEST") ?? Path.Combine(baseOutputPath, "test"),
            "prod" => Environment.GetEnvironmentVariable("PHOTOMAPPER_OUTPUT_PROD") ?? Path.Combine(baseOutputPath, "prod"),
            _ => throw new InvalidOperationException($"Unsupported output profile '{profile}'. Use none/test/prod.")
        };
    }

    private static string? ResolveDefaultSizeProfilePath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "samples", "size_profiles.default.json"),
            Path.Combine(AppContext.BaseDirectory, "samples", "size_profiles.default.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "size_profiles.default.json")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static UiSizeProfile LoadSizeProfile(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Size profile file not found: {profilePath}");
        }

        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<UiSizeProfile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (profile == null || profile.Variants == null || profile.Variants.Count == 0)
        {
            throw new InvalidOperationException("Size profile must contain at least one variant.");
        }

        foreach (var variant in profile.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.Key) || variant.Width <= 0 || variant.Height <= 0)
            {
                throw new InvalidOperationException("Each size profile variant requires key, width > 0, and height > 0.");
            }
        }

        return profile;
    }

    private sealed class UiSizeProfile
    {
        public string Name { get; set; } = "default";
        public List<UiSizeVariant> Variants { get; set; } = new();
    }

    private sealed class UiSizeVariant
    {
        public string Key { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string? OutputSubfolder { get; set; }
    }
}
