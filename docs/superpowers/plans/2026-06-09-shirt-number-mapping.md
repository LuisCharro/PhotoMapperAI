# Shirt-Number Mapping (2026 World Cup) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in shirt-number matching so 2026 World Cup photos (no name in the filename, only a shirt number + FIFA id) can be mapped to player records, while name matching stays the default for all other competitions.

**Architecture:** Extract enriches the per-team CSV with `ShirtNumber` and `Function` columns sourced from cesim `CompetitorContestData` (the squad incl. the head coach). `map` gains a `--matchBy shirt` parameter that runs a new deterministic, exact shirt-number match phase (no AI) and writes the FIFA id from the filename into `External_Player_ID`. `map` stays DB-free; all changes are localized.

**Tech Stack:** .NET 10, C#, xUnit, CsvHelper 33.1.0, McMaster.Extensions.CommandLineUtils, System.Data.SqlClient.

**Spec:** `docs/superpowers/specs/2026-06-09-shirt-number-mapping-design.md`

---

## File Structure

| File | Responsibility | Change |
|------|----------------|--------|
| `src/PhotoMapperAI/Models/PhotoMetadata.cs` | Photo metadata parsed from filename | Add `ShirtCode` |
| `src/PhotoMapperAI/Utils/FilenameParser.cs` | Filename → metadata | Add WC regex; populate `ShirtCode` |
| `src/PhotoMapperAI/Models/PlayerRecord.cs` | Player row | Add `ShirtNumber`, `Function` |
| `src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs` | CSV read/write + DB extract | New optional columns; read from DB |
| `src/PhotoMapperAI/Models/MappingResult.cs` | Match result + method enum | Add `MatchMethod.ShirtNumberMatch` |
| `src/PhotoMapperAI/Commands/MapCommand.cs` | Map orchestration (`MapCommandLogic`) | `matchBy` param + shirt phase + `MapResult.ShirtMatches` |
| `src/PhotoMapperAI/Commands/Program.cs` | CLI commands | `--matchBy` option on `MapCommand` |
| `samples/sql-examples/CesimSquadWithShirtFromCompetition_PhotoMapper_2026_WC_men.sql` | Repo copy of WC extract SQL | Create |
| `tests/.../Utils/FilenameParserTests.cs` | Parser tests | Add WC cases |
| `tests/.../Services/Database/DatabaseExtractorCsvTests.cs` | CSV round-trip tests | Create |
| `tests/.../Commands/MapCommandLogicTests.cs` | Map tests | Add shirt-mode case |

**Out of scope (UI follow-up):** Avalonia Map/Batch toggle, `FilenamePatternSettings` preset, automatic `HC → coach` matching. The coach is mapped manually in the UI later.

---

## Task 1: WC filename parsing

**Files:**
- Modify: `src/PhotoMapperAI/Models/PhotoMetadata.cs`
- Modify: `src/PhotoMapperAI/Utils/FilenameParser.cs`
- Test: `tests/PhotoMapperAI.Tests/Utils/FilenameParserTests.cs`

- [ ] **Step 1: Add `ShirtCode` to `PhotoMetadata`**

In `src/PhotoMapperAI/Models/PhotoMetadata.cs`, add this property after `SurName`:

```csharp
    /// <summary>
    /// Raw shirt/code field parsed from the filename (e.g. "01".."26" or "HC").
    /// Null when the filename pattern carries no shirt code.
    /// </summary>
    public string? ShirtCode { get; set; }
```

- [ ] **Step 2: Write the failing parser tests**

In `tests/PhotoMapperAI.Tests/Utils/FilenameParserTests.cs`, add inside the class:

```csharp
    [Fact]
    public void ParseAutoDetect_WcPattern_Numbered_ParsesShirtAndFifaId()
    {
        var result = FilenameParser.ParseAutoDetect("BRA-H-01-M1-PPROFILE-308370-MK.png");

        Assert.NotNull(result);
        Assert.Equal("01", result!.ShirtCode);
        Assert.Equal("308370", result.External_Player_ID);
    }

    [Fact]
    public void ParseAutoDetect_WcPattern_Coach_ParsesHcAndFifaId()
    {
        var result = FilenameParser.ParseAutoDetect("BRA-H-HC-M1-PPROFILE-174348-MK.png");

        Assert.NotNull(result);
        Assert.Equal("HC", result!.ShirtCode);
        Assert.Equal("174348", result.External_Player_ID);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~FilenameParserTests.ParseAutoDetect_WcPattern"`
Expected: FAIL (`ShirtCode` is null / `result` is null — no pattern matches yet).

- [ ] **Step 4: Add the WC regex and populate `ShirtCode`**

In `src/PhotoMapperAI/Utils/FilenameParser.cs`, add this as the **first** entry in the `_patterns` array (before "Pattern 1"), so the most specific pattern wins:

```csharp
        // Pattern 0: WC FIFA format {team}-{g}-{shirt}-{variant}-PPROFILE-{id}-{suffix}.png
        // e.g. BRA-H-01-M1-PPROFILE-308370-MK.png ; coach uses HC instead of a number.
        // {shirt} captures 01..26 or HC; {id} is the FIFA player id.
        new Regex(@"^(?<team>[A-Za-z]+)-(?<g>[A-Za-z]+)-(?<shirt>[A-Za-z0-9]{1,3})-(?<variant>[A-Za-z0-9]+)-PPROFILE-(?<id>\d+)-(?<suffix>[A-Za-z0-9]+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),
```

Then, in `BuildMetadata`, set `ShirtCode` from the optional `shirt` group. Replace the `return new PhotoMetadata { ... }` block with:

```csharp
        return new PhotoMetadata
        {
            FileName = filename,
            External_Player_ID = match.Groups["id"].Value,
            FamilyName = familyName,
            SurName = surName,
            FullName = $"{familyName} {surName}".Trim(),
            ShirtCode = match.Groups["shirt"].Success ? match.Groups["shirt"].Value : null,
            Source = source,
            PatternUsed = patternUsed
        };
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~FilenameParserTests"`
Expected: PASS (new WC tests pass; all existing parser tests still pass — the WC regex requires the literal `-PPROFILE-` segment so it cannot match older formats).

- [ ] **Step 6: Commit**

```bash
git add src/PhotoMapperAI/Models/PhotoMetadata.cs src/PhotoMapperAI/Utils/FilenameParser.cs tests/PhotoMapperAI.Tests/Utils/FilenameParserTests.cs
git commit -m "feat(map): parse WC FIFA filename pattern (shirt code + FIFA id)"
```

---

## Task 2: CSV schema — `ShirtNumber` and `Function`

**Files:**
- Modify: `src/PhotoMapperAI/Models/PlayerRecord.cs`
- Modify: `src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs`
- Test: `tests/PhotoMapperAI.Tests/Services/Database/DatabaseExtractorCsvTests.cs` (create)

- [ ] **Step 1: Add `ShirtNumber` and `Function` to `PlayerRecord`**

In `src/PhotoMapperAI/Models/PlayerRecord.cs`, add after the `External_Player_ID` property (before `ValidMapping`):

```csharp
    /// <summary>
    /// Shirt number from the source competition data. Empty/null when unknown
    /// (e.g. the coach, or before the squad shirt data is released).
    /// </summary>
    public string? ShirtNumber { get; set; }

    /// <summary>
    /// Function/role from the source competition data (e.g. FootballForward,
    /// FootballKeeper, FootballCoach). Null for sources that do not provide it.
    /// </summary>
    public string? Function { get; set; }
```

- [ ] **Step 2: Write the failing CSV round-trip tests**

Create `tests/PhotoMapperAI.Tests/Services/Database/DatabaseExtractorCsvTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~DatabaseExtractorCsvTests"`
Expected: FAIL. `ReadCsvAsync_CsvWithShirtColumns` fails (ShirtNumber/Function not mapped); `WriteCsvAsync_Includes...` may fail until the properties exist on `PlayerRecord` (Step 1 covers the property; this step proves the read mapping is missing). The legacy test should already pass.

- [ ] **Step 4: Add optional columns to the CSV DTOs and map them on read**

In `src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs`:

In `PlayerRecordCsv`, add after `External_Player_ID`:

```csharp
        [Optional]
        public string? ShirtNumber { get; set; }
        [Optional]
        public string? Function { get; set; }
```

In `PlayerRecordCsvExtended`, add after `External_Player_ID`:

```csharp
        [Optional]
        public string? ShirtNumber { get; set; }
        [Optional]
        public string? Function { get; set; }
```

(`[Optional]` is `CsvHelper.Configuration.Attributes.OptionalAttribute`; the `using` is already present. It makes a missing header/field non-fatal so legacy CSVs still load.)

In `ReadCsvAsync`, set the two fields in the `new PlayerRecord { ... }` initializer:

```csharp
                    ShirtNumber = record.ShirtNumber,
                    Function = record.Function,
```

In `ReadExistingMappedCsvAsync`, add to its `new PlayerRecord { ... }`:

```csharp
                    ShirtNumber = record.ShirtNumber,
                    Function = record.Function,
```

In `ReadExistingMappedCsvRowsAsync`, add to its `new PlayerRecord { ... }`:

```csharp
                    ShirtNumber = record.ShirtNumber,
                    Function = record.Function,
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~DatabaseExtractorCsvTests"`
Expected: PASS (all three).

- [ ] **Step 6: Run the full suite to confirm no regression**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj`
Expected: PASS. (Existing `MapCommandLogicTests` verify output by re-reading via `ReadCsvAsync` and asserting field values, not exact header strings, so the two extra output columns do not break them.)

- [ ] **Step 7: Commit**

```bash
git add src/PhotoMapperAI/Models/PlayerRecord.cs src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs tests/PhotoMapperAI.Tests/Services/Database/DatabaseExtractorCsvTests.cs
git commit -m "feat(extract): add optional ShirtNumber/Function CSV columns"
```

---

## Task 3: Extract reads `ShirtNumber`/`Function` from the database

**Files:**
- Modify: `src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs` (`ExtractPlayersToCsvAsync`)

No unit test (requires a live DB). This is exercised by the end-to-end verification in Task 7 (extract → `csv2` → compare).

- [ ] **Step 1: Read the optional DB columns and set them on the player**

In `ExtractPlayersToCsvAsync`, inside the `while (await reader.ReadAsync())` loop, after the `External_Player_ID` line, add:

```csharp
                var shirtNumber = GetOptionalString(reader, "ShirtNumber");
                var function = GetOptionalString(reader, "Function");
```

Then add the two fields to the `new PlayerRecord { ... }` initializer:

```csharp
                    External_Player_ID = External_Player_ID,
                    ShirtNumber = shirtNumber,
                    Function = function
```

(`GetOptionalString` already returns null for missing columns, so SQL templates without these columns keep working.)

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs
git commit -m "feat(extract): read ShirtNumber/Function from query results"
```

---

## Task 4: Shirt-number matching in `MapCommandLogic`

**Files:**
- Modify: `src/PhotoMapperAI/Models/MappingResult.cs` (`MatchMethod`)
- Modify: `src/PhotoMapperAI/Commands/MapCommand.cs` (`MapResult`, `ExecuteAsync`, new private methods)
- Test: `tests/PhotoMapperAI.Tests/Commands/MapCommandLogicTests.cs`

- [ ] **Step 1: Add the `ShirtNumberMatch` enum value**

In `src/PhotoMapperAI/Models/MappingResult.cs`, add to `MatchMethod` after `DirectIdMatch`:

```csharp
    /// <summary>
    /// Exact shirt-number match (filename shirt code == player shirt number)
    /// </summary>
    ShirtNumberMatch,
```

- [ ] **Step 2: Add `ShirtMatches` to `MapResult`**

In `src/PhotoMapperAI/Commands/MapCommand.cs`, add to the `MapResult` class after `StringMatches`:

```csharp
    public int ShirtMatches { get; set; }
```

- [ ] **Step 3: Write the failing map test**

In `tests/PhotoMapperAI.Tests/Commands/MapCommandLogicTests.cs`, add a new test method to the class (it reuses the file's existing `TestWorkspace` and `NoOpNameMatchingService` helpers):

```csharp
    [Fact]
    public async Task ExecuteAsync_ShirtMode_MatchesByShirtNumber_LeavesCoachUnmapped()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_8437_Brasilien.csv",
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID,ShirtNumber,Function\n" +
            "83725,8437,Becker,Alisson,,1,FootballKeeper\n" +
            "59452,8437,Neymar,,,10,FootballForward\n" +
            "29213,8437,Ancelotti,Carlo,,,FootballCoach\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "BRA-H-01-M1-PPROFILE-308370-MK.png"), "p1");
        temp.WriteFile(Path.Combine("photos", "BRA-H-10-M1-PPROFILE-314197-MK.png"), "p2");
        temp.WriteFile(Path.Combine("photos", "BRA-H-HC-M1-PPROFILE-174348-MK.png"), "coach");

        var map = new MapCommandLogic(new NoOpNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: null,
            photoManifest: null,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: false,
            aiSecondPass: false,
            matchBy: "shirt");

        Assert.Equal(3, result.PlayersProcessed);
        Assert.Equal(2, result.ShirtMatches);
        Assert.Equal(2, result.PlayersMatched);

        var extractor = new DatabaseExtractor();
        var outputPlayers = await extractor.ReadCsvAsync(result.OutputPath);
        var becker = outputPlayers.Single(p => p.PlayerId == 83725);
        var neymar = outputPlayers.Single(p => p.PlayerId == 59452);
        var coach = outputPlayers.Single(p => p.PlayerId == 29213);

        Assert.Equal("308370", becker.External_Player_ID);
        Assert.Equal("314197", neymar.External_Player_ID);
        Assert.True(string.IsNullOrEmpty(coach.External_Player_ID));
        Assert.False(coach.ValidMapping);
    }
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~ExecuteAsync_ShirtMode"`
Expected: FAIL to compile — `ExecuteAsync` has no `matchBy` parameter yet.

- [ ] **Step 5: Add the `matchBy` parameter to `ExecuteAsync`**

In `src/PhotoMapperAI/Commands/MapCommand.cs`, add a new optional parameter at the **end** of the `ExecuteAsync` signature (after `IProgress<string>? log = null`):

```csharp
        IProgress<string>? log = null,
        string matchBy = "name")
```

- [ ] **Step 6: Compute shirt mode and log it**

In `ExecuteAsync`, just before `try`, after the existing `LogLine(...)` header block (after `LogLine($"AI Only: {aiOnly}");` and before `LogLine(string.Empty);`), add:

```csharp
        var shirtMode = string.Equals(matchBy, "shirt", StringComparison.OrdinalIgnoreCase);
        LogLine($"Match By: {(shirtMode ? "shirt" : "name")}");
```

- [ ] **Step 7: Declare the shirt counter alongside the other counters**

In `ExecuteAsync`, next to `var stringMatches = 0;`, add:

```csharp
            var shirtMatches = 0;
```

- [ ] **Step 8: Insert the shirt-match phase after Phase 1**

Immediately after `progress.Complete();` (end of Phase 1) and before the `// Phase 2: deterministic global assignment (optional)` comment, add:

```csharp
            // Phase 1b: shirt-number matching (opt-in via --matchBy shirt)
            if (shirtMode)
            {
                shirtMatches = ApplyShirtNumberMatches(
                    unmatchedPlayers,
                    remainingCandidates,
                    remainingByExternal_Player_ID,
                    confidenceThreshold,
                    results);
                LogLine($"✓ Shirt-number matched: {shirtMatches}");
            }
```

- [ ] **Step 9: Skip name/AI phases in shirt mode**

Change the Phase 2 guard from:

```csharp
            if (!aiOnly)
```
to:
```csharp
            if (!aiOnly && !shirtMode)
```

Change the Phase 3 logging guard from `if (useAi)` to:

```csharp
            if (useAi && !shirtMode)
```

Change the Phase 3 execution guard from:

```csharp
            if (useAi && unmatchedPlayers.Count > 0 && remainingCandidates.Count > 0)
```
to:
```csharp
            if (useAi && !shirtMode && unmatchedPlayers.Count > 0 && remainingCandidates.Count > 0)
```

(Players not matched by shirt remain in `unmatchedPlayers` and receive no-match results in the existing `foreach (var player in unmatchedPlayers)` block, so the coach is left unmapped.)

- [ ] **Step 10: Add `ShirtMatches` to the returned `MapResult`**

In the `return new MapResult { ... }`, add after `StringMatches = stringMatches,`:

```csharp
                ShirtMatches = shirtMatches,
```

- [ ] **Step 11: Add the `ApplyShirtNumberMatches` and `TryNormalizeShirt` methods**

In `src/PhotoMapperAI/Commands/MapCommand.cs`, add these methods next to `ApplyDeterministicGlobalMatches` (inside the `#region Private Methods`):

```csharp
    private int ApplyShirtNumberMatches(
        List<PlayerRecord> unmatchedPlayers,
        List<PhotoCandidate> remainingCandidates,
        Dictionary<string, PhotoCandidate> remainingByExternal_Player_ID,
        double confidenceThreshold,
        List<MappingResult> results)
    {
        // Index remaining candidates by normalized numeric shirt code from the filename.
        // Non-numeric codes (e.g. "HC") and empty codes are skipped.
        var candidatesByShirt = new Dictionary<int, PhotoCandidate>();
        foreach (var candidate in remainingCandidates)
        {
            if (TryNormalizeShirt(candidate.Metadata.ShirtCode, out var shirt) && !candidatesByShirt.ContainsKey(shirt))
            {
                candidatesByShirt[shirt] = candidate;
            }
        }

        var applied = 0;
        foreach (var player in unmatchedPlayers.ToList())
        {
            if (!TryNormalizeShirt(player.ShirtNumber, out var shirt))
                continue;
            if (!candidatesByShirt.TryGetValue(shirt, out var candidate))
                continue;
            if (!remainingCandidates.Contains(candidate))
                continue;

            ApplyMatch(player, candidate, confidenceThreshold, 1.0, out var result);
            result.Method = MatchMethod.ShirtNumberMatch;
            result.ModelUsed = "ShirtNumberMatch";
            result.Metadata["shirt"] = shirt.ToString(CultureInfo.InvariantCulture);
            results.Add(result);

            RemoveCandidate(candidate, remainingCandidates, remainingByExternal_Player_ID);
            unmatchedPlayers.Remove(player);
            candidatesByShirt.Remove(shirt);
            applied++;
        }

        return applied;
    }

    private static bool TryNormalizeShirt(string? value, out int shirt)
    {
        shirt = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out shirt);
    }
```

(`CultureInfo` and `NumberStyles` come from `System.Globalization`, already imported at the top of the file.)

- [ ] **Step 12: Run the test to verify it passes**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~ExecuteAsync_ShirtMode"`
Expected: PASS.

- [ ] **Step 13: Run the full suite**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj`
Expected: PASS.

- [ ] **Step 14: Commit**

```bash
git add src/PhotoMapperAI/Models/MappingResult.cs src/PhotoMapperAI/Commands/MapCommand.cs tests/PhotoMapperAI.Tests/Commands/MapCommandLogicTests.cs
git commit -m "feat(map): add --matchBy shirt deterministic shirt-number matching"
```

---

## Task 5: CLI wiring — `--matchBy` option

**Files:**
- Modify: `src/PhotoMapperAI/Commands/Program.cs` (`MapCommand`)

- [ ] **Step 1: Add the option property**

In `src/PhotoMapperAI/Commands/Program.cs`, in the `MapCommand` class, add after the `FilenamePattern` option (around line 188):

```csharp
    [Option(ShortName = "mb", LongName = "matchBy", Description = "Matching strategy: 'name' (default) or 'shirt' (shirt-number matching for 2026 WC photos)")]
    public string MatchBy { get; set; } = "name";
```

- [ ] **Step 2: Validate and pass it through**

In `MapCommand.OnExecuteAsync`, at the very start (before the `AiOnly` block), add validation:

```csharp
        if (!string.Equals(MatchBy, "name", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(MatchBy, "shirt", StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid --matchBy value '{MatchBy}'. Use 'name' or 'shirt'.");
            Console.ResetColor();
            return 1;
        }
```

In the `await logic.ExecuteAsync(...)` call, add the argument at the end (after `log: null`):

```csharp
            log: null,
            matchBy: MatchBy
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Smoke-test the help text**

Run: `dotnet run --project src/PhotoMapperAI -- map --help`
Expected: Output lists `--matchBy` with the description.

- [ ] **Step 5: Commit**

```bash
git add src/PhotoMapperAI/Commands/Program.cs
git commit -m "feat(cli): expose --matchBy name|shirt on the map command"
```

---

## Task 6: Repo copy of the WC extract SQL

**Files:**
- Create: `samples/sql-examples/CesimSquadWithShirtFromCompetition_PhotoMapper_2026_WC_men.sql`

- [ ] **Step 1: Create the SQL file**

Create `samples/sql-examples/CesimSquadWithShirtFromCompetition_PhotoMapper_2026_WC_men.sql` with this content (identical to the validated file in `C:\FIFA_Images\2026_WC`):

```sql
-- Cesim squad export for PhotoMapperAI (2026 World Cup, men)
-- Source of truth: cesim.dbo.CompetitorContestData
--   CompetitorContestDataTypeId = 1 -> Shirt Number
--   CompetitorContestDataTypeId = 2 -> Function Type (one row per squad member,
--                                       includes the head coach: Function = 'FootballCoach')
--
-- Parameters expected by PhotoMapperAI: @TeamId
-- Competition (contest) is the 2026 World Cup men's competition: ContestId = 5193
--
-- Output columns map to the PhotoMapperAI CSV schema, plus ShirtNumber and Function.
-- ShirtNumber is NULL/empty when cesim has no shirt data yet, and for the coach.

select
    c.compId            as PlayerId,
    func.CompetitorId   as TeamId,
    c.compName1         as FamilyName,
    c.compName2         as SurName,
    cast(null as nvarchar(50)) as External_Player_ID,
    shirt.Value         as ShirtNumber,
    func.Value          as [Function]
from
    cesim.dbo.CompetitorContestData func
    join cesim.dbo.Competitor c
        on c.compId = func.CompetitorMemberId
    left join cesim.dbo.CompetitorContestData shirt
        on shirt.CompetitorId            = func.CompetitorId
        and shirt.CompetitorMemberId     = func.CompetitorMemberId
        and shirt.ContestId              = func.ContestId
        and shirt.CompetitorContestDataTypeId = 1
where
    func.ContestId = 5193
    and func.CompetitorContestDataTypeId = 2
    and (isnull(@TeamId, 0) = 0 or func.CompetitorId = @TeamId)
order by
    case when shirt.Value is null then 999 else cast(shirt.Value as int) end,
    c.compName1, c.compName2;
```

- [ ] **Step 2: Commit**

```bash
git add samples/sql-examples/CesimSquadWithShirtFromCompetition_PhotoMapper_2026_WC_men.sql
git commit -m "docs(samples): add 2026 WC squad-with-shirt extract SQL"
```

---

## Task 7: End-to-end verification (Brasilien, console)

This task verifies the whole flow against real data in `C:\FIFA_Images\2026_WC`. It uses the cesim connection string the user authorized (read-only selects only). Read the console logs at each step — they report counts that confirm behavior.

- [ ] **Step 1: Build the CLI**

Run: `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj`
Expected: Build succeeded.

- [ ] **Step 2: Extract Brasilien into a fresh `csv2` folder**

PowerShell:
```powershell
New-Item -ItemType Directory -Force "C:\FIFA_Images\2026_WC\csv2" | Out-Null
dotnet run --project src/PhotoMapperAI -- extract `
  --inputSqlPath "C:\FIFA_Images\2026_WC\CesimSquadWithShirtFromCompetition_PhotoMapper_2026_WC_men.sql" `
  --connectionStringPath "C:\FIFA_Images\2026_WC\TestConnectionString.txt" `
  --teamId 8437 `
  --outputName "C:\FIFA_Images\2026_WC\csv2\players_8437_Brasilien.csv"
```
Expected log: `✓ Extracted 27 players to ...` (26 players + coach).

- [ ] **Step 3: Compare old vs new CSV**

Read `C:\FIFA_Images\2026_WC\csv\players_8437_Brasilien.csv` (old, 25 rows, no shirt/coach) and `C:\FIFA_Images\2026_WC\csv2\players_8437_Brasilien.csv` (new). Confirm the new file:
- has `ShirtNumber` and `Function` columns,
- has shirt numbers `1`..`26`,
- includes the coach row (`Ancelotti`, `FootballCoach`, empty `ShirtNumber`),
- includes `Ederson Silva` (shirt 2), who was missing from the old CSV.

- [ ] **Step 4: Map by shirt number**

PowerShell (run from a working dir where the mapped output should land, e.g. `C:\FIFA_Images\2026_WC\csv2`):
```powershell
dotnet run --project src/PhotoMapperAI -- map `
  --inputCsvPath "C:\FIFA_Images\2026_WC\csv2\players_8437_Brasilien.csv" `
  --photosDir "C:\FIFA_Images\2026_WC\src\Brasilien" `
  --matchBy shirt
```
Expected log: `Match By: shirt`, `✓ Shirt-number matched: 26`, `✓ Left unmapped: 1` (the coach). Read the mapped CSV (`players_8437_Brasilien_mapped.csv` in the current dir) and confirm 26 players have a FIFA id in `External_Player_ID` and `ValidMapping=True`; the coach row is unmapped.

- [ ] **Step 5: Generate portraits from the mapped CSV**

PowerShell:
```powershell
dotnet run --project src/PhotoMapperAI -- generatephotos `
  --inputCsvPath "C:\FIFA_Images\2026_WC\csv2\players_8437_Brasilien_mapped.csv" `
  --photosDir "C:\FIFA_Images\2026_WC\src\Brasilien" `
  --processedPhotosOutputPath "C:\FIFA_Images\2026_WC\csv2\portraits_Brasilien" `
  --format jpg `
  --faceDetection opencv-yunet
```
Expected: portrait crops produced for the mapped players; read the log for per-photo results and any face-detection fallbacks. (If OpenCV model files are missing, run `scripts/download-opencv-models.ps1` first — the log will say so.)

- [ ] **Step 6: Confirm name matching is unaffected (regression guard)**

Run a quick sanity check that the default path is unchanged: the full unit suite already covers it, but optionally map a name-based folder (e.g. a 2024_Euro team) **without** `--matchBy` and confirm `Match By: name` in the log and normal name-matching behavior.

- [ ] **Step 7: Final full test run**

Run: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj`
Expected: PASS.

---

## Self-Review Notes

- **Spec coverage:** Extract enrichment (Tasks 2,3,6), opt-in `--matchBy` default name (Tasks 4,5), WC filename pattern (Task 1), deterministic exact shirt match writing FIFA id to `External_Player_ID` (Task 4), graceful degradation when shirt data absent (shirt index empty → players stay unmapped; covered by the normalize-skip logic and verified in Task 7), coach left unmapped in CLI (Task 4 Step 9), name flow unchanged (Task 4 guards + Task 7 Step 6). All spec sections map to a task.
- **Naming consistency:** `ShirtCode` (PhotoMetadata, raw filename code incl. `HC`), `ShirtNumber` (PlayerRecord/CSV, cesim numeric), `Function` (role), `MatchMethod.ShirtNumberMatch`, `MapResult.ShirtMatches`, `ApplyShirtNumberMatches`, `TryNormalizeShirt`, CLI `MatchBy` / `--matchBy`, `ExecuteAsync(..., string matchBy = "name")` — used consistently across all tasks.
- **Deferred to UI follow-up (documented, not built):** `FilenamePatternSettings` WC preset, Map/Batch UI toggle, automatic `HC → coach`.
