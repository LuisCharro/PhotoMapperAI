using System.Net.Http;
using System.Text.Json;

namespace PhotoMapperAI.Services.Diagnostics;

public static class OpenCvModelDownloader
{
    private const string PrototxtFileName = "res10_ssd_deploy.prototxt";
    private const string WeightsFileName = "res10_300x300_ssd_iter_140000.caffemodel";

    private static readonly string[] DefaultPrototxtUrls =
    {
        "https://raw.githubusercontent.com/opencv/opencv/master/samples/dnn/face_detector/deploy.prototxt",
        "https://github.com/opencv/opencv/raw/master/samples/dnn/face_detector/deploy.prototxt"
    };

    private static readonly string[] DefaultWeightsUrls =
    {
        "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel",
        "https://github.com/opencv/opencv_3rdparty/raw/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel",
        "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector/res10_300x300_ssd_iter_140000.caffemodel",
        "https://github.com/opencv/opencv_3rdparty/raw/dnn_samples_face_detector/res10_300x300_ssd_iter_140000.caffemodel",
        "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_models_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel",
        "https://github.com/opencv/opencv_3rdparty/raw/dnn_models_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel",
        "https://github.com/opencv/opencv_3rdparty/raw/master/dnn_models_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel",
        "https://raw.githubusercontent.com/opencv/opencv_3rdparty/master/dnn_models_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel",
        "https://raw.githubusercontent.com/opencv/opencv_3rdparty/master/res10_300x300_ssd_iter_140000.caffemodel"
    };

    public static async Task<(bool Success, List<string> Downloaded, string Error)> EnsureModelsAsync(string modelsPath)
    {
        var downloaded = new List<string>();

        try
        {
            Directory.CreateDirectory(modelsPath);

            var prototxtPath = Path.Combine(modelsPath, PrototxtFileName);
            var weightsPath = Path.Combine(modelsPath, WeightsFileName);

            using var http = new HttpClient();

            var (prototxtUrls, weightsUrls) = LoadUrlsFromSettings();

            if (!File.Exists(prototxtPath))
            {
                await DownloadFileAsync(http, prototxtUrls, prototxtPath);
                downloaded.Add(prototxtPath);
            }

            if (!File.Exists(weightsPath))
            {
                await DownloadFileAsync(http, weightsUrls, weightsPath);
                downloaded.Add(weightsPath);
            }

            return (true, downloaded, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, downloaded, ex.Message);
        }
    }

    private static async Task DownloadFileAsync(HttpClient http, IEnumerable<string> urls, string destinationPath)
    {
        Exception? lastError = null;

        foreach (var url in urls)
        {
            try
            {
                using var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(destinationPath);
                await stream.CopyToAsync(fileStream);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError != null)
            throw lastError;
    }

    private static (IReadOnlyList<string> PrototxtUrls, IReadOnlyList<string> WeightsUrls) LoadUrlsFromSettings()
    {
        var settingsPath = ResolveSettingsPath();
        if (string.IsNullOrWhiteSpace(settingsPath))
            return (DefaultPrototxtUrls, DefaultWeightsUrls);

        try
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("OpenCV", out var openCv))
                return (DefaultPrototxtUrls, DefaultWeightsUrls);

            if (!openCv.TryGetProperty("Downloads", out var downloads))
                return (DefaultPrototxtUrls, DefaultWeightsUrls);

            var prototxtUrls = ReadUrlArray(downloads, "PrototxtUrls").ToList();
            var weightsUrls = ReadUrlArray(downloads, "WeightsUrls").ToList();

            if (prototxtUrls.Count == 0)
                prototxtUrls.AddRange(DefaultPrototxtUrls);

            if (weightsUrls.Count == 0)
                weightsUrls.AddRange(DefaultWeightsUrls);

            return (prototxtUrls, weightsUrls);
        }
        catch
        {
            return (DefaultPrototxtUrls, DefaultWeightsUrls);
        }
    }

    private static IEnumerable<string> ReadUrlArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var urls = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    urls.Add(value);
            }
        }

        return urls;
    }

    private static string? ResolveSettingsPath()
    {
        var candidates = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.template.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.local.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.template.json")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
