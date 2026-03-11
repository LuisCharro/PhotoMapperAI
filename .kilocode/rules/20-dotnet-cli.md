# .NET CLI & Command Patterns

## Project Structure

```
PhotoMapperAI/
├── Commands/           # CLI command implementations
├── Services/           # Business logic
│   ├── AI/            # AI/LLM services
│   ├── Database/      # Data access
│   └── Image/         # Image processing
├── Models/            # Data models
├── Utils/             # Utilities
└── Resources/         # Embedded resources
```

## Command Pattern

### Base Command Structure
```csharp
[Command(Name = "map", Description = "Map photos to players")]
public class MapCommand : async ICammand<int>
{
    [Option(LongName = "inputCsvPath", ShortName = "i", Description = "Input CSV path")]
    public string InputCsvPath { get; set; }

    [Option(LongName = "photosDir", Description = "Photos directory")]
    public string PhotosDir { get; set; }

    public async Task<int> ExecuteAsync()
    {
        // Implementation
        return 0;
    }
}
```

### Command Options
- Use descriptive option names
- Provide sensible defaults
- Add validation in `ExecuteAsync`

### Option Attributes
- `LongName` — full name (--inputCsvPath)
- `ShortName` — single char (-i)
- `Description` — help text
- `Required` — enforce presence

## Dependency Injection

### Registering Services
```csharp
// In Program.cs or service configuration
services.AddSingleton<IPlayerService, PlayerService>();
services.AddScoped<IDatabaseService, DatabaseService>();
services.AddTransient<IImageProcessor, OpenCvImageProcessor>();
```

### Constructor Injection
```csharp
public class MapCommand
{
    private readonly IPlayerService _playerService;
    private readonly IConfiguration _configuration;

    public MapCommand(IPlayerService playerService, IConfiguration configuration)
    {
        _playerService = playerService;
        _configuration = configuration;
    }
}
```

## Configuration

### appsettings.json
```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "qwen2.5:7b"
  },
  "NameMatching": {
    "ConfidenceThreshold": 0.8
  },
  "FaceDetection": {
    "DefaultModel": "opencv-dnn"
  }
}
```

### Accessing Configuration
```csharp
// Via constructor
public MapCommand(IConfiguration configuration)
{
    var threshold = configuration.GetValue<double>("NameMatching:ConfidenceThreshold");
}
```

## Logging

### ILogger Injection
```csharp
public class MapCommand
{
    private readonly ILogger<MapCommand> _logger;

    public MapCommand(ILogger<MapCommand> logger)
    {
        _logger = logger;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogInformation("Starting mapping process");
        // ...
    }
}
```

### Log Levels
- `LogError` — Exceptions, failures
- `LogWarning` — Recoverable issues
- `LogInformation` — Progress, milestones
- `LogDebug` — Detailed debugging

## Console Output

### Color Coding
Use `AnsiConsole` from Spectre.Console:
```csharp
AnsiConsole.MarkupLine("[green]Success:[/] Mapped {Count} players", count);
AnsiConsole.MarkupLine("[yellow]Warning:[/] {Count} unmatched", count);
AnsiConsole.MarkupLine("[red]Error:[/] {Message}", message);
```

### Progress Reporting
```csharp
var progress = new ProgressBar();
progress.Report(0.5); // 50% complete
```

## Error Handling

### Global Exception Handler
```csharp
// In Program.cs
try
{
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}
```

### Validation
```csharp
if (!File.Exists(inputCsvPath))
{
    AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {inputCsvPath}");
    return 1;
}
```
