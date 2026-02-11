using System.Text.Json;
using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Service for caching face detection results to avoid redundant processing.
/// </summary>
public class FaceDetectionCache
{
    private readonly string _cacheFilePath;
    private readonly Dictionary<string, CacheEntry> _cache;
    private readonly object _lock = new();
    private bool _modified = false;

    /// <summary>
    /// Cache entry structure.
    /// </summary>
    private class CacheEntry
    {
        public string ImagePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public FaceLandmarks Landmarks { get; set; } = new();
        public string Model { get; set; } = string.Empty;
    }

    /// <summary>
    /// Creates a new face detection cache instance.
    /// </summary>
    /// <param name="cacheFilePath">Path to cache file</param>
    public FaceDetectionCache(string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
        _cache = new Dictionary<string, CacheEntry>();
        LoadCache();
    }

    /// <summary>
    /// Tries to get cached face landmarks for an image.
    /// </summary>
    /// <param name="imagePath">Path to image file</param>
    /// <param name="model">Face detection model name</param>
    /// <returns>Cached landmarks if available and valid, null otherwise</returns>
    public FaceLandmarks? GetCachedLandmarks(string imagePath, string model)
    {
        lock (_lock)
        {
            var fileInfo = new FileInfo(imagePath);

            if (!fileInfo.Exists)
            {
                return null;
            }

            var key = GetCacheKey(imagePath, model);

            if (!_cache.TryGetValue(key, out var entry))
            {
                return null;
            }

            // Check if cache entry is still valid (file not modified)
            if (entry.FileSize != fileInfo.Length || entry.LastModified != fileInfo.LastWriteTimeUtc)
            {
                _cache.Remove(key);
                _modified = true;
                return null;
            }

            return entry.Landmarks;
        }
    }

    /// <summary>
    /// Caches face landmarks for an image.
    /// </summary>
    /// <param name="imagePath">Path to image file</param>
    /// <param name="landmarks">Face landmarks to cache</param>
    /// <param name="model">Face detection model name</param>
    public void CacheLandmarks(string imagePath, FaceLandmarks landmarks, string model)
    {
        lock (_lock)
        {
            var fileInfo = new FileInfo(imagePath);

            var entry = new CacheEntry
            {
                ImagePath = imagePath,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                Landmarks = landmarks,
                Model = model
            };

            var key = GetCacheKey(imagePath, model);
            _cache[key] = entry;
            _modified = true;
        }
    }

    /// <summary>
    /// Saves cache to disk if modified.
    /// </summary>
    public void SaveCache()
    {
        lock (_lock)
        {
            if (!_modified)
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(_cacheFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_cache.Values, options);
                File.WriteAllText(_cacheFilePath, json);

                _modified = false;
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Warning: Failed to save face detection cache: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public (int TotalEntries, int ValidEntries) GetStatistics()
    {
        lock (_lock)
        {
            return (_cache.Count, _cache.Values.Count(e => File.Exists(e.ImagePath)));
        }
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _cache.Clear();
            _modified = true;
            SaveCache();
            
            // Also delete the file if it exists
            if (File.Exists(_cacheFilePath))
            {
                try { File.Delete(_cacheFilePath); } catch { }
            }
        }
    }

    /// <summary>
    /// Generates a cache key for an image path and model.
    /// </summary>
    private string GetCacheKey(string imagePath, string model)
    {
        // Use absolute path and model name as key
        return $"{Path.GetFullPath(imagePath).ToLowerInvariant()}|{model.ToLowerInvariant()}";
    }

    /// <summary>
    /// Loads cache from disk.
    /// </summary>
    private void LoadCache()
    {
        if (!File.Exists(_cacheFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var entries = JsonSerializer.Deserialize<List<CacheEntry>>(json);

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var key = GetCacheKey(entry.ImagePath, entry.Model);
                    _cache[key] = entry;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail - cache will be rebuilt
            Console.WriteLine($"Warning: Failed to load face detection cache: {ex.Message}");
            _cache.Clear();
        }
    }
}
