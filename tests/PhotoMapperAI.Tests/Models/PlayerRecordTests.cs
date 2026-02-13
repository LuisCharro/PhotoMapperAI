using PhotoMapperAI.Models;
using Xunit;

namespace PhotoMapperAI.Tests.Models;

/// <summary>
/// Unit tests for PlayerRecord model.
/// </summary>
public class PlayerRecordTests
{
    #region FullName Property

    [Fact]
    public void FullName_WithFamilyAndSur_ReturnsCombined()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = "Smith",
            SurName = "John"
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("Smith John", fullName);
    }

    [Fact]
    public void FullName_WithOnlyFamilyName_ReturnsFamilyName()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = "Smith",
            SurName = ""
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("Smith", fullName);
    }

    [Fact]
    public void FullName_WithOnlySurName_ReturnsSurName()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = "",
            SurName = "John"
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("John", fullName);
    }

    [Fact]
    public void FullName_WithEmptyNames_ReturnsEmptyString()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = "",
            SurName = ""
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("", fullName);
    }

    [Fact]
    public void FullName_WithExtraSpaces_PreservesInternalSpaces()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = " Smith ",
            SurName = " John "
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("Smith   John", fullName); // Preserves internal whitespace, trims only ends
    }

    [Fact]
    public void FullName_WithSpecialCharacters_PreservesThem()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = "Rodríguez Sánchez",
            SurName = "Francisco Román"
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("Rodríguez Sánchez Francisco Román", fullName);
    }

    [Fact]
    public void FullName_WithMultipleSpaces_HandlesCorrectly()
    {
        // Arrange
        var record = new PlayerRecord
        {
            FamilyName = "Smith",
            SurName = "John William"
        };

        // Act
        var fullName = record.FullName;

        // Assert
        Assert.Equal("Smith John William", fullName);
    }

    #endregion

    #region Default Values

    [Fact]
    public void Constructor_Defaults_FamilyNameEmpty()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.Equal("", record.FamilyName);
    }

    [Fact]
    public void Constructor_Defaults_SurNameEmpty()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.Equal("", record.SurName);
    }

    [Fact]
    public void Constructor_Defaults_ExternalIdNull()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.Null(record.ExternalId);
    }

    [Fact]
    public void Constructor_Defaults_ValidMappingFalse()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.False(record.ValidMapping);
    }

    [Fact]
    public void Constructor_Defaults_ConfidenceZero()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.Equal(0.0, record.Confidence);
    }

    [Fact]
    public void Constructor_Defaults_PlayerIdZero()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.Equal(0, record.PlayerId);
    }

    [Fact]
    public void Constructor_Defaults_TeamIdZero()
    {
        // Arrange & Act
        var record = new PlayerRecord();

        // Assert
        Assert.Equal(0, record.TeamId);
    }

    #endregion

    #region Property Assignment

    [Fact]
    public void PropertyAssignment_AllProperties_AssignedCorrectly()
    {
        // Arrange & Act
        var record = new PlayerRecord
        {
            PlayerId = 123,
            TeamId = 456,
            FamilyName = "Smith",
            SurName = "John",
            ExternalId = "789",
            ValidMapping = true,
            Confidence = 0.95
        };

        // Assert
        Assert.Equal(123, record.PlayerId);
        Assert.Equal(456, record.TeamId);
        Assert.Equal("Smith", record.FamilyName);
        Assert.Equal("John", record.SurName);
        Assert.Equal("789", record.ExternalId);
        Assert.True(record.ValidMapping);
        Assert.Equal(0.95, record.Confidence);
    }

    #endregion
}
