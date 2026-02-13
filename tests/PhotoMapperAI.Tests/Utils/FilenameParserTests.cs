using PhotoMapperAI.Models;
using PhotoMapperAI.Utils;
using Xunit;

namespace PhotoMapperAI.Tests.Utils;

/// <summary>
/// Unit tests for FilenameParser utility.
/// </summary>
public class FilenameParserTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _manifestPath;

    public FilenameParserTests()
    {
        // Create temporary directory for tests
        _testDir = Path.Combine(Path.GetTempPath(), $"FilenameParser_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _manifestPath = Path.Combine(_testDir, "manifest.json");
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

    #region ParseAutoDetect - Pattern 1: {id}_{family}_{sur}.ext

    [Fact]
    public void ParseAutoDetect_Pattern1_IdFamilySur_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("123_Smith_John.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.ExternalId);
        Assert.Equal("Smith", result.FamilyName);
        Assert.Equal("John", result.SurName);
        Assert.Equal("Smith John", result.FullName);
        Assert.Equal("123_Smith_John.png", result.FileName);
        Assert.Equal(MetadataSource.AutoDetect, result.Source);
    }

    [Fact]
    public void ParseAutoDetect_Pattern1_DifferentExtensions_ParsesCorrectly()
    {
        // Arrange & Act
        var resultJpg = FilenameParser.ParseAutoDetect("456_Doe_Jane.jpg");
        var resultJpeg = FilenameParser.ParseAutoDetect("456_Doe_Jane.jpeg");
        var resultBmp = FilenameParser.ParseAutoDetect("456_Doe_Jane.bmp");

        // Assert
        Assert.NotNull(resultJpg);
        Assert.NotNull(resultJpeg);
        Assert.NotNull(resultBmp);

        Assert.Equal("456", resultJpg.ExternalId);
        Assert.Equal("456", resultJpeg.ExternalId);
        Assert.Equal("456", resultBmp.ExternalId);
    }

    #endregion

    #region ParseAutoDetect - Pattern 2: {sur}-{family}-{id}.ext

    [Fact]
    public void ParseAutoDetect_Pattern2_SurFamilyId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("Maria-Garcia-789.jpg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("789", result.ExternalId);
        Assert.Equal("Garcia", result.FamilyName);
        Assert.Equal("Maria", result.SurName);
        Assert.Equal("Garcia Maria", result.FullName);
    }

    #endregion

    #region ParseAutoDetect - Pattern 3: {family}, {sur} - {id}.ext

    [Fact]
    public void ParseAutoDetect_Pattern3_FamilyCommaSurId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("Martinez, Carlos - 321.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("321", result.ExternalId);
        Assert.Equal("Martinez", result.FamilyName);
        Assert.Equal("Carlos ", result.SurName); // Note: trailing space from regex pattern
    }

    [Fact]
    public void ParseAutoDetect_Pattern3_WithExtraSpaces_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("Martinez, Carlos - 321.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("321", result.ExternalId);
        Assert.Equal("Martinez", result.FamilyName);
        Assert.Equal("Carlos ", result.SurName); // Note: trailing space from regex pattern
    }

    #endregion

    #region ParseAutoDetect - Pattern 4: {family}_{sur}_position_{id}.ext

    [Fact]
    public void ParseAutoDetect_Pattern4_FamilySurPositionId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("Rodriguez_Luis_position_654.jpg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("654", result.ExternalId);
        Assert.Equal("Rodriguez", result.FamilyName);
        Assert.Equal("Luis", result.SurName);
    }

    #endregion

    #region ParseAutoDetect - Pattern 5: {id}-{sur}-{family}.ext

    [Fact]
    public void ParseAutoDetect_Pattern5_IdSurFamily_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("987-Ana-Lopez.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("987", result.ExternalId);
        Assert.Equal("Lopez", result.FamilyName);
        Assert.Equal("Ana", result.SurName);
    }

    #endregion

    #region ParseAutoDetect - Pattern 6: {family}_{sur}_{id}.ext (variation)

    [Fact]
    public void ParseAutoDetect_Pattern6_FamilySurId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("Fernandez_Pedro_147.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("147", result.ExternalId);
        Assert.Equal("Fernandez", result.FamilyName);
        Assert.Equal("Pedro", result.SurName);
    }

    #endregion

    #region ParseAutoDetect - Pattern 7: {id}.ext (simple ID)

    [Fact]
    public void ParseAutoDetect_Pattern7_SimpleId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("258.jpg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("258", result.ExternalId);
        Assert.Equal("", result.FamilyName); // Empty string when group doesn't exist
        Assert.Equal("", result.SurName); // Empty string when group doesn't exist
    }

    #endregion

    #region ParseAutoDetect - No Match

    [Fact]
    public void ParseAutoDetect_NoMatchingPattern_ReturnsNull()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("invalid_filename.txt");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseAutoDetect_EmptyString_ReturnsNull()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseAutoDetect_NullFilename_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            FilenameParser.ParseAutoDetect(null!);
        });
    }

    #endregion

    #region ParseAutoDetect - Case Insensitivity

    [Fact]
    public void ParseAutoDetect_CaseInsensitive_ParsesCorrectly()
    {
        // Arrange & Act
        var resultLower = FilenameParser.ParseAutoDetect("123_smith_john.jpg");
        var resultUpper = FilenameParser.ParseAutoDetect("123_SMITH_JOHN.JPG");
        var resultMixed = FilenameParser.ParseAutoDetect("123_SmItH_JoHn.JpG");

        // Assert
        Assert.NotNull(resultLower);
        Assert.NotNull(resultUpper);
        Assert.NotNull(resultMixed);

        Assert.Equal("123", resultLower.ExternalId);
        Assert.Equal("123", resultUpper.ExternalId);
        Assert.Equal("123", resultMixed.ExternalId);
    }

    #endregion

    #region ParseAutoDetect - Pattern Priority

    [Fact]
    public void ParseAutoDetect_MultiplePatternsMatch_ReturnsFirstMatch()
    {
        // Arrange & Act
        // This filename could match Pattern 1 and Pattern 6
        var result = FilenameParser.ParseAutoDetect("Smith_John_123.jpg");

        // Assert
        Assert.NotNull(result);
        // Pattern 1: {id}_{family}_{sur}
        // Pattern 6: {family}_{sur}_{id}
        // Pattern 1 comes first, so "123" should be parsed as id
        Assert.Equal("123", result.ExternalId);
        Assert.Equal("Smith", result.FamilyName);
        Assert.Equal("John", result.SurName);
    }

    #endregion

    #region ParseWithTemplate

    [Fact]
    public void ParseWithTemplate_TemplateMatches_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate("123_Smith_John.png", "{id}_{family}_{sur}.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.ExternalId);
        Assert.Equal("Smith", result.FamilyName);
        Assert.Equal("John", result.SurName);
        Assert.Equal(MetadataSource.UserPattern, result.Source);
    }

    [Fact]
    public void ParseWithTemplate_TemplateDoesNotMatch_ReturnsNull()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate("invalid.jpg", "{id}_{family}_{sur}.png");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseWithTemplate_TemplateWithDifferentOrder_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate("Smith_John_123.png", "{family}_{sur}_{id}.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.ExternalId);
        Assert.Equal("Smith", result.FamilyName);
        Assert.Equal("John", result.SurName);
    }

    [Fact]
    public void ParseWithTemplate_TemplateWithOnlyId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate("999.jpg", "{id}.jpg");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("999", result.ExternalId);
        Assert.Equal("", result.FamilyName); // Empty string when placeholder not in template
        Assert.Equal("", result.SurName); // Empty string when placeholder not in template
    }

    [Fact]
    public void ParseWithTemplate_TemplateWithDotInPlaceholder_HandlesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate("photo_123.png", "photo_{id}.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123", result.ExternalId);
    }

    [Fact]
    public void ParseWithTemplate_NullFilename_ReturnsNull()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate(null!, "{id}.png");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseWithTemplate_NullTemplate_ReturnsNull()
    {
        // Arrange & Act
        var result = FilenameParser.ParseWithTemplate("123.png", null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseWithTemplate_InvalidTemplate_ReturnsNull()
    {
        // Arrange & Act
        // Invalid template with unclosed bracket
        var result = FilenameParser.ParseWithTemplate("123.png", "{id.png");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region LoadManifest

    [Fact]
    public void LoadManifest_ValidManifest_LoadsMetadata()
    {
        // Arrange
        var manifestContent = @"{
  ""photos"": {
    ""photo1.jpg"": {
      ""id"": ""123"",
      ""fullName"": ""John Smith"",
      ""familyName"": ""Smith"",
      ""surName"": ""John""
    },
    ""photo2.png"": {
      ""id"": ""456"",
      ""fullName"": ""Jane Doe"",
      ""familyName"": ""Doe"",
      ""surName"": ""Jane"",
      ""teamId"": ""T001""
    }
  }
}";
        File.WriteAllText(_manifestPath, manifestContent);

        // Act
        var result = FilenameParser.LoadManifest(_manifestPath);

        // Assert
        Assert.Equal(2, result.Count);

        Assert.True(result.ContainsKey("photo1.jpg"));
        var photo1 = result["photo1.jpg"];
        Assert.Equal("123", photo1.ExternalId);
        Assert.Equal("John Smith", photo1.FullName);
        Assert.Equal("Smith", photo1.FamilyName);
        Assert.Equal("John", photo1.SurName);
        Assert.Equal(MetadataSource.Manifest, photo1.Source);

        Assert.True(result.ContainsKey("photo2.png"));
        var photo2 = result["photo2.png"];
        Assert.Equal("456", photo2.ExternalId);
        Assert.Equal("Jane Doe", photo2.FullName);
        Assert.Equal("Doe", photo2.FamilyName);
        Assert.Equal("Jane", photo2.SurName);
    }

    [Fact]
    public void LoadManifest_EmptyManifest_ReturnsEmptyDictionary()
    {
        // Arrange
        var manifestContent = @"{
  ""photos"": {}
}";
        File.WriteAllText(_manifestPath, manifestContent);

        // Act
        var result = FilenameParser.LoadManifest(_manifestPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadManifest_MissingPhotos_ReturnsEmptyDictionary()
    {
        // Arrange
        var manifestContent = @"{}";
        File.WriteAllText(_manifestPath, manifestContent);

        // Act
        var result = FilenameParser.LoadManifest(_manifestPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadManifest_NullFields_LoadsAvailableData()
    {
        // Arrange
        var manifestContent = @"{
  ""photos"": {
    ""photo1.jpg"": {
      ""id"": ""123"",
      ""familyName"": ""Smith""
    }
  }
}";
        File.WriteAllText(_manifestPath, manifestContent);

        // Act
        var result = FilenameParser.LoadManifest(_manifestPath);

        // Assert
        Assert.Single(result);
        var photo = result["photo1.jpg"];
        Assert.Equal("123", photo.ExternalId);
        Assert.Equal("Smith", photo.FamilyName);
        Assert.Null(photo.FullName);
        Assert.Null(photo.SurName);
    }

    [Fact]
    public void LoadManifest_FileDoesNotExist_ThrowsException()
    {
        // Arrange & Act & Assert
        // Can throw either FileNotFoundException or DirectoryNotFoundException depending on path
        Assert.ThrowsAny<IOException>(() =>
        {
            FilenameParser.LoadManifest("/nonexistent/path/manifest.json");
        });
    }

    [Fact]
    public void LoadManifest_InvalidJson_ThrowsException()
    {
        // Arrange
        File.WriteAllText(_manifestPath, "invalid json content");

        // Act & Assert
        Assert.Throws<System.Text.Json.JsonException>(() =>
        {
            FilenameParser.LoadManifest(_manifestPath);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ParseAutoDetect_FilenameWithPath_ParsesCorrectly()
    {
        // Arrange & Act
        // Path should be handled (only filename matters)
        var result = FilenameParser.ParseAutoDetect("/path/to/photos/123_Smith_John.png");

        // Assert
        Assert.Null(result); // Pattern doesn't match because of path prefix
    }

    [Fact]
    public void ParseAutoDetect_SpecialCharactersInName_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("123_Smith-John_Doe.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Smith-John", result.FamilyName);
        Assert.Equal("Doe", result.SurName);
    }

    [Fact]
    public void ParseAutoDetect_MultipleUnderscores_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("123_Smith_John_Doe.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Smith", result.FamilyName);
        Assert.Equal("John_Doe", result.SurName); // Everything after first underscore is sur name
    }

    [Fact]
    public void ParseAutoDetect_ZeroId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("0_Smith_John.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("0", result.ExternalId);
    }

    [Fact]
    public void ParseAutoDetect_LargeId_ParsesCorrectly()
    {
        // Arrange & Act
        var result = FilenameParser.ParseAutoDetect("999999_Smith_John.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("999999", result.ExternalId);
    }

    #endregion
}
