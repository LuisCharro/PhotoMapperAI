using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Database;

namespace PhotoMapperAI.Tests.Services.Database;

public class DatabaseExtractorCsvTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"extractor-csv-{Guid.NewGuid():N}");

    public DatabaseExtractorCsvTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ReadCsvAsync_LegacyCsvWithoutShirtColumns_ReadsWithoutError()
    {
        var path = Path.Combine(_dir, "legacy.csv");
        await File.WriteAllTextAsync(
            path,
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID\n" +
            "59452,8437,Neymar,,\n");

        var extractor = new DatabaseExtractor();
        var players = await extractor.ReadCsvAsync(path);

        var player = Assert.Single(players);
        Assert.Equal(59452, player.PlayerId);
        Assert.Null(player.ShirtNumber);
        Assert.Null(player.Function);
    }

    [Fact]
    public async Task ReadCsvAsync_CsvWithShirtColumns_PopulatesShirtAndFunction()
    {
        var path = Path.Combine(_dir, "wc.csv");
        await File.WriteAllTextAsync(
            path,
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID,ShirtNumber,Function\n" +
            "83725,8437,Becker,Alisson Ramses,,1,FootballKeeper\n" +
            "29213,8437,Ancelotti,Carlo,,,FootballCoach\n");

        var extractor = new DatabaseExtractor();
        var players = await extractor.ReadCsvAsync(path);

        Assert.Equal(2, players.Count);
        Assert.Equal("1", players[0].ShirtNumber);
        Assert.Equal("FootballKeeper", players[0].Function);
        Assert.True(string.IsNullOrEmpty(players[1].ShirtNumber));
        Assert.Equal("FootballCoach", players[1].Function);
    }

    [Fact]
    public async Task WriteCsvAsync_IncludesShirtNumberAndFunctionColumns()
    {
        var path = Path.Combine(_dir, "out.csv");
        var players = new List<PlayerRecord>
        {
            new() { PlayerId = 83725, TeamId = 8437, FamilyName = "Becker", SurName = "Alisson", ShirtNumber = "1", Function = "FootballKeeper" }
        };

        await DatabaseExtractor.WriteCsvAsync(players, path);
        var text = await File.ReadAllTextAsync(path);

        Assert.Contains("ShirtNumber", text);
        Assert.Contains("Function", text);
        Assert.Contains(",1,FootballKeeper", text);
    }
}
