# Testing & Validation

## Testing Strategy

### Test Projects
```
PhotoMapperAI.Tests/
├── Unit/           # Unit tests
├── Integration/    # Integration tests
└── Validation/     # Output validation
```

### Running Tests
```bash
# All tests
dotnet test

# Specific project
dotnet test PhotoMapperAI.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Unit Testing

### Framework
- Use xUnit (built into .NET)
- Use FluentAssertions for assertions

### Structure
```csharp
public class PlayerMappingServiceTests
{
    private readonly PlayerMappingService _service;

    public PlayerMappingServiceTests()
    {
        _service = new PlayerMappingService();
    }

    [Fact]
    public void MatchPlayer_WithExactName_ReturnsHighConfidence()
    {
        // Arrange
        var playerName = "Pedri";
        var photoName = "10_Pedri.png";

        // Act
        var result = _service.MatchPlayer(playerName, photoName);

        // Assert
        result.Confidence.Should().BeGreaterThan(0.9);
    }

    [Theory]
    [InlineData("Pedri", "Pedri", 0.95)]
    [InlineData("Pedri", "Pedro", 0.75)]
    public void MatchPlayer_VariousNames_ReturnsExpectedConfidence(
        string player, string photo, double expectedMin)
    {
        var result = _service.MatchPlayer(player, photo);
        result.Confidence.Should().BeGreaterOrEqualTo(expectedMin);
    }
}
```

### Naming
- `MethodName_Scenario_ExpectedResult`
- Use `[Theory]` for parameterized tests

## Integration Testing

### Database Tests
```csharp
public class DatabaseServiceIntegrationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public DatabaseServiceIntegrationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetPlayersAsync_ReturnsPlayers()
    {
        using var context = _fixture.CreateContext();
        var service = new DatabaseService(context);

        var players = await service.GetPlayersAsync(teamId: 10);

        players.Should().NotBeEmpty();
    }
}
```

### File System Tests
```csharp
[Fact]
public void ProcessImages_FromDirectory_OutputsCorrectStructure()
{
    // Arrange
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempDir);
    // ... create test files

    // Act
    var result = _processor.ProcessDirectory(tempDir);

    // Assert
    result.ProcessedCount.Should().Be(3);
    // Cleanup
    Directory.Delete(tempDir, true);
}
```

## Output Validation

### CSV Validation
```csharp
[Fact]
public void MapCommand_OutputCsv_HasRequiredColumns()
{
    // Run map command
    var output = File.ReadAllText("output.csv");

    // Validate columns
    var columns = output.Split('\n').First();
    columns.Should().Contain("PlayerId");
    columns.Should().Contain("External_Player_ID");
    columns.Should().Contain("Valid_Mapping");
}
```

### Image Validation
```csharp
[Fact]
public void GeneratePhotos_OutputImages_AreCorrectSize()
{
    var images = Directory.GetFiles("output", "*.jpg");
    
    foreach (var image in images)
    {
        using var img = Cv2.ImRead(image);
        img.Width.Should().Be(200);
        img.Height.Should().Be(300);
    }
}
```

## Test Data

### Using Test Fixtures
```csharp
public class TestData
{
    public static string SampleCsv => """
        PlayerId,PlayerName,TeamId
        1,Pedri,10
        2,Lamine Yamal,10
        """;
    
    public static string SamplePhotoDir => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "TestData", "Photos"
    );
}
```

### Mocking AI Services
```csharp
public class MockAiService : IAiService
{
    public Task<MatchResult> MatchPlayerAsync(string name, string photo)
    {
        return Task.FromResult(new MatchResult
        {
            Confidence = 0.95,
            PlayerId = 1
        });
    }
}
```

## Validation Scripts

### External Validation
```bash
# Run validation against reference data
python3 validate_map_output.py \
    --output mapped_players.csv \
    --reference Competition2024/Csvs/mapped_players.csv
```

### Benchmark Comparison
```bash
# Compare Windows vs macOS results
dotnet run --project PhotoMapperAI -- benchmark-compare \
    --baseline benchmark-results/baseline.json \
    --candidate benchmark-results/candidate.json
```

## Build & CI

### Pre-Commit
```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### CI Pipeline
```yaml
# .github/workflows/test.yml
- name: Build
  run: dotnet build --configuration Release

- name: Test
  run: dotnet test --configuration Release

- name: Pack
  run: dotnet pack --configuration Release
```
