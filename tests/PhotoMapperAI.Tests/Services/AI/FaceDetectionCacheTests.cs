using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using Xunit;

namespace PhotoMapperAI.Tests.Services.AI;

/// <summary>
/// Unit tests for FaceDetectionCache service.
/// </summary>
public class FaceDetectionCacheTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _cacheFilePath;
    private readonly string _imagePath;

    public FaceDetectionCacheTests()
    {
        // Create temporary directory for tests
        _testDir = Path.Combine(Path.GetTempPath(), $"PhotoMapperAI_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _cacheFilePath = Path.Combine(_testDir, "cache.json");
        _imagePath = Path.Combine(_testDir, "test.jpg");
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_NewCache_InitializesEmpty()
    {
        // Arrange & Act
        var cache = new FaceDetectionCache(_cacheFilePath);

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(0, stats.TotalEntries);
    }

    [Fact]
    public void Constructor_LoadsExistingCache_RestoresEntries()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache1 = new FaceDetectionCache(_cacheFilePath);
        cache1.CacheLandmarks(_imagePath, landmarks, "test-model");
        cache1.SaveCache();

        // Act
        var cache2 = new FaceDetectionCache(_cacheFilePath);

        // Assert
        var stats = cache2.GetStatistics();
        Assert.Equal(1, stats.TotalEntries);
        Assert.Equal(1, stats.ValidEntries);

        var cached = cache2.GetCachedLandmarks(_imagePath, "test-model");
        Assert.NotNull(cached);
        Assert.Equal(landmarks.FaceRect!.X, cached.FaceRect!.X);
    }

    #endregion

    #region GetCachedLandmarks Tests

    [Fact]
    public void GetCachedLandmarks_NoCache_ReturnsNull()
    {
        // Arrange
        var cache = new FaceDetectionCache(_cacheFilePath);

        // Act
        var result = cache.GetCachedLandmarks(_imagePath, "test-model");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCachedLandmarks_CachedEntry_ReturnsLandmarks()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Act
        var result = cache.GetCachedLandmarks(_imagePath, "test-model");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(landmarks.FaceRect!.X, result.FaceRect!.X);
        Assert.Equal(landmarks.FaceRect!.Y, result.FaceRect!.Y);
    }

    [Fact]
    public void GetCachedLandmarks_DifferentModel_ReturnsNull()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "model1");

        // Act
        var result = cache.GetCachedLandmarks(_imagePath, "model2");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCachedLandmarks_FileModified_ReturnsNull()
    {
        // Arrange
        var originalTime = DateTime.UtcNow.AddHours(-1);
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, originalTime);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Modify the file
        var newTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(_imagePath, newTime);

        // Act
        var result = cache.GetCachedLandmarks(_imagePath, "test-model");

        // Assert
        Assert.Null(result);

        var stats = cache.GetStatistics();
        Assert.Equal(0, stats.TotalEntries); // Entry should be removed
    }

    [Fact]
    public void GetCachedLandmarks_FileSizeChanged_ReturnsNull()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Modify the file size
        File.WriteAllBytes(_imagePath, new byte[2000]);

        // Act
        var result = cache.GetCachedLandmarks(_imagePath, "test-model");

        // Assert
        Assert.Null(result);

        var stats = cache.GetStatistics();
        Assert.Equal(0, stats.TotalEntries); // Entry should be removed
    }

    [Fact]
    public void GetCachedLandmarks_FileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var cache = new FaceDetectionCache(_cacheFilePath);

        // Act
        var result = cache.GetCachedLandmarks("/nonexistent/path/image.jpg", "test-model");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CacheLandmarks Tests

    [Fact]
    public void CacheLandmarks_AddsEntry_IncrementsCount()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);

        // Act
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(1, stats.TotalEntries);
    }

    [Fact]
    public void CacheLandmarks_UpdatesExistingEntry_ReplacesLandmarks()
    {
        // Arrange
        var landmarks1 = CreateTestLandmarks(100, 100);
        var landmarks2 = CreateTestLandmarks(200, 200);
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks1, "test-model");

        // Act
        cache.CacheLandmarks(_imagePath, landmarks2, "test-model");

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(1, stats.TotalEntries); // Still only one entry

        var cached = cache.GetCachedLandmarks(_imagePath, "test-model");
        Assert.NotNull(cached);
        Assert.Equal(200, cached.FaceRect!.X); // Updated value
    }

    [Fact]
    public void CacheLandmarks_DifferentPaths_AddsMultipleEntries()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        var imagePath2 = Path.Combine(_testDir, "test2.jpg");
        CreateTestImage(1000, DateTime.UtcNow);
        CreateTestImage(imagePath2, 1500, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);

        // Act
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");
        cache.CacheLandmarks(imagePath2, landmarks, "test-model");

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(2, stats.TotalEntries);
    }

    #endregion

    #region SaveCache Tests

    [Fact]
    public void SaveCache_CreatesFile_WritesCorrectFormat()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Act
        cache.SaveCache();

        // Assert
        Assert.True(File.Exists(_cacheFilePath));

        var json = File.ReadAllText(_cacheFilePath);
        Assert.Contains("test-model", json);
        Assert.Contains(_imagePath.Replace("\\", "\\\\"), json); // Escape backslashes for JSON
    }

    [Fact]
    public void SaveCache_NotModified_DoesNotOverwrite()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");
        cache.SaveCache();

        var lastWriteTime = File.GetLastWriteTimeUtc(_cacheFilePath);

        // Act
        cache.SaveCache(); // No modifications since last save

        // Assert
        Assert.Equal(lastWriteTime, File.GetLastWriteTimeUtc(_cacheFilePath));
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_RemovesAllEntries_ZerosCount()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Act
        cache.ClearCache();

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(0, stats.TotalEntries);
    }

    [Fact]
    public void ClearCache_DeletesCacheFile_RemovesFile()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");
        cache.SaveCache();

        Assert.True(File.Exists(_cacheFilePath));

        // Act
        cache.ClearCache();

        // Assert
        Assert.False(File.Exists(_cacheFilePath));
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_EmptyCache_ReturnsZero()
    {
        // Arrange
        var cache = new FaceDetectionCache(_cacheFilePath);

        // Act
        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalEntries);
        Assert.Equal(0, stats.ValidEntries);
    }

    [Fact]
    public void GetStatistics_WithValidEntries_ReturnsCorrectCount()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");

        // Act
        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(1, stats.TotalEntries);
        Assert.Equal(1, stats.ValidEntries);
    }

    [Fact]
    public void GetStatistics_WithMissingFiles_ReturnsCorrectCount()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);
        cache.CacheLandmarks(_imagePath, landmarks, "test-model");
        cache.SaveCache();

        // Delete the image file
        File.Delete(_imagePath);

        // Act
        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(1, stats.TotalEntries); // Entry still exists
        Assert.Equal(0, stats.ValidEntries); // But file doesn't exist
    }

    #endregion

    #region Model-Specific Tests

    [Fact]
    public void ModelSpecific_Caching_SeparatesEntriesByModel()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache = new FaceDetectionCache(_cacheFilePath);

        // Act
        cache.CacheLandmarks(_imagePath, CreateTestLandmarks(100, 100), "model1");
        cache.CacheLandmarks(_imagePath, CreateTestLandmarks(200, 200), "model2");

        // Assert
        var stats = cache.GetStatistics();
        Assert.Equal(2, stats.TotalEntries); // Two entries (one per model)

        var result1 = cache.GetCachedLandmarks(_imagePath, "model1");
        var result2 = cache.GetCachedLandmarks(_imagePath, "model2");

        Assert.Equal(100, result1!.FaceRect!.X);
        Assert.Equal(200, result2!.FaceRect!.X);
    }

    #endregion

    #region Persistence Tests

    [Fact]
    public void Persistence_SaveAndLoad_RestoresCorrectly()
    {
        // Arrange
        var landmarks = CreateTestLandmarks();
        CreateTestImage(1000, DateTime.UtcNow);

        var cache1 = new FaceDetectionCache(_cacheFilePath);
        cache1.CacheLandmarks(_imagePath, landmarks, "test-model");
        cache1.SaveCache();

        // Act
        var cache2 = new FaceDetectionCache(_cacheFilePath);

        // Assert
        var result = cache2.GetCachedLandmarks(_imagePath, "test-model");
        Assert.NotNull(result);
        Assert.Equal(landmarks.FaceRect!.X, result.FaceRect!.X);
        Assert.Equal(landmarks.FaceRect!.Y, result.FaceRect!.Y);
        Assert.Equal(landmarks.FaceRect!.Width, result.FaceRect!.Width);
        Assert.Equal(landmarks.FaceRect!.Height, result.FaceRect!.Height);
    }

    #endregion

    #region Helper Methods

    private FaceLandmarks CreateTestLandmarks(int x = 100, int y = 100)
    {
        return new FaceLandmarks
        {
            FaceRect = new PhotoMapperAI.Models.Rectangle(x, y, 200, 200),
            LeftEye = new PhotoMapperAI.Models.Point(x + 70, y + 80),
            RightEye = new PhotoMapperAI.Models.Point(x + 130, y + 80)
        };
    }

    private void CreateTestImage(int size, DateTime lastModified)
    {
        CreateTestImage(_imagePath, size, lastModified);
    }

    private void CreateTestImage(string path, int size, DateTime lastModified)
    {
        var data = new byte[size];
        new Random().NextBytes(data);
        File.WriteAllBytes(path, data);
        File.SetLastWriteTimeUtc(path, lastModified);
    }

    #endregion
}
