using PhotoMapperAI.Models;
using Xunit;

namespace PhotoMapperAI.Tests.Models;

/// <summary>
/// Unit tests for MappingResult model.
/// </summary>
public class MappingResultTests
{
    #region IsValidMatch Property

    [Fact]
    public void IsValidMatch_ConfidenceAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.95,
            ConfidenceThreshold = 0.9
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidMatch_ConfidenceEqualsThreshold_ReturnsTrue()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.9,
            ConfidenceThreshold = 0.9
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidMatch_ConfidenceBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.85,
            ConfidenceThreshold = 0.9
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValidMatch_ZeroConfidence_ReturnsFalse()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.0,
            ConfidenceThreshold = 0.5
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValidMatch_PerfectConfidence_ReturnsTrue()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 1.0,
            ConfidenceThreshold = 0.9
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidMatch_CustomThreshold_UsesCustomValue()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.7,
            ConfidenceThreshold = 0.6
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.True(isValid); // 0.7 > 0.6
    }

    [Fact]
    public void IsValidMatch_VeryLowThreshold_AllowsLowConfidence()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.1,
            ConfidenceThreshold = 0.05
        };

        // Act
        var isValid = result.IsValidMatch;

        // Assert
        Assert.True(isValid); // 0.1 > 0.05
    }

    #endregion

    #region Default Values

    [Fact]
    public void Constructor_Defaults_PlayerIdNull()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Null(result.PlayerId);
    }

    [Fact]
    public void Constructor_Defaults_ExternalIdNull()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Null(result.ExternalId);
    }

    [Fact]
    public void Constructor_Defaults_PhotoFileNameEmpty()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Equal("", result.PhotoFileName);
    }

    [Fact]
    public void Constructor_Defaults_ConfidenceZero()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Constructor_Defaults_ConfidenceThreshold09()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Equal(0.9, result.ConfidenceThreshold);
    }

    [Fact]
    public void Constructor_Defaults_ModelUsedEmpty()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Equal("", result.ModelUsed);
    }

    [Fact]
    public void Constructor_Defaults_ProcessingTimeMsZero()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.Equal(0, result.ProcessingTimeMs);
    }

    [Fact]
    public void Constructor_Defaults_MetadataEmptyDictionary()
    {
        // Arrange & Act
        var result = new MappingResult();

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata);
    }

    #endregion

    #region Property Assignment

    [Fact]
    public void PropertyAssignment_AllProperties_AssignedCorrectly()
    {
        // Arrange & Act
        var result = new MappingResult
        {
            PlayerId = 123,
            ExternalId = "456",
            PhotoFileName = "photo.jpg",
            Confidence = 0.95,
            ConfidenceThreshold = 0.8,
            ModelUsed = "qwen2.5",
            Method = MatchMethod.AiNameMatching,
            ProcessingTimeMs = 150
        };
        result.Metadata["key"] = "value";

        // Assert
        Assert.Equal(123, result.PlayerId);
        Assert.Equal("456", result.ExternalId);
        Assert.Equal("photo.jpg", result.PhotoFileName);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal(0.8, result.ConfidenceThreshold);
        Assert.Equal("qwen2.5", result.ModelUsed);
        Assert.Equal(MatchMethod.AiNameMatching, result.Method);
        Assert.Equal(150, result.ProcessingTimeMs);
        Assert.Equal("value", result.Metadata["key"]);
    }

    #endregion

    #region Metadata Dictionary

    [Fact]
    public void Metadata_AddItems_ItemsPersist()
    {
        // Arrange
        var result = new MappingResult();

        // Act
        result.Metadata["key1"] = "value1";
        result.Metadata["key2"] = "value2";

        // Assert
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal("value1", result.Metadata["key1"]);
        Assert.Equal("value2", result.Metadata["key2"]);
    }

    [Fact]
    public void Metadata_RemoveItem_ItemRemoved()
    {
        // Arrange
        var result = new MappingResult();
        result.Metadata["key"] = "value";

        // Act
        result.Metadata.Remove("key");

        // Assert
        Assert.Empty(result.Metadata);
    }

    [Fact]
    public void Metadata_ClearAllItems_EmptyDictionary()
    {
        // Arrange
        var result = new MappingResult();
        result.Metadata["key1"] = "value1";
        result.Metadata["key2"] = "value2";

        // Act
        result.Metadata.Clear();

        // Assert
        Assert.Empty(result.Metadata);
    }

    [Fact]
    public void Metadata_ReplaceDictionary_NewDictionaryUsed()
    {
        // Arrange
        var result = new MappingResult();
        result.Metadata["old"] = "value";

        var newDict = new Dictionary<string, string>
        {
            ["new"] = "value2"
        };

        // Act
        result.Metadata = newDict;

        // Assert
        Assert.Single(result.Metadata);
        Assert.Equal("value2", result.Metadata["new"]);
        Assert.False(result.Metadata.ContainsKey("old"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ConfidenceThreshold_NegativeValue_Allowed()
    {
        // Arrange & Act
        var result = new MappingResult
        {
            Confidence = -0.1,
            ConfidenceThreshold = -0.5
        };

        // Assert
        Assert.True(result.IsValidMatch); // -0.1 > -0.5
    }

    [Fact]
    public void ConfidenceThreshold_GreaterThanOne_Allowed()
    {
        // Arrange & Act
        var result = new MappingResult
        {
            Confidence = 1.5,
            ConfidenceThreshold = 1.2
        };

        // Assert
        Assert.True(result.IsValidMatch); // 1.5 > 1.2
    }

    [Fact]
    public void ConfidenceThreshold_VerySmallValue_PrecisionTest()
    {
        // Arrange
        var result = new MappingResult
        {
            Confidence = 0.9000001,
            ConfidenceThreshold = 0.9
        };

        // Act & Assert
        Assert.True(result.IsValidMatch);
    }

    #endregion
}
