# PhotoMapperAI - Project Rules

## Overview
PhotoMapperAI is a .NET Core CLI application for mapping sports player photos to database systems using AI-powered name matching and face detection.

## Tech Stack
- **Framework:** .NET 10 (Core)
- **Language:** C#
- **CLI:** System.CommandLine or Spectre.Console
- **Image Processing:** OpenCV (OpenCvSharp4)
- **AI:** Ollama, OpenAI APIs
- **Testing:** xUnit, FluentAssertions

## Project Structure
```
PhotoMapperAI/
├── src/
│   ├── PhotoMapperAI/        # Main CLI application
│   │   ├── Commands/         # CLI commands (map, generate, benchmark)
│   │   ├── Services/        # Business logic
│   │   │   ├── AI/          # AI/LLM services
│   │   │   ├── Database/   # Data access
│   │   │   └── Image/      # Image processing
│   │   ├── Models/          # Data models
│   │   └── Utils/           # Utilities
│   └── PhotoMapperAI.Tests/ # Test project
├── docs/                     # Documentation
├── scripts/                  # Automation scripts
└── models/                   # Embedded ML models
```

## Key Commands

### Map Command
```bash
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- map \
  --inputCsvPath players.csv \
  --photosDir ./photos \
  --nameModel openai:gpt-4o \
  --confidenceThreshold 0.80
```

### Generate Photos Command
```bash
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- generate \
  --inputCsvPath mapped_players.csv \
  --processedPhotosOutputPath ./portraits \
  --faceDetection opencv-dnn
```

### Benchmark Command
```bash
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- benchmark \
  --photosDir ./photos \
  --models opencv-dnn,yolov8-face,llava:7b
```

## Development

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- [command]
```

### Test
```bash
dotnet test
```

### Publish (Self-Contained)
```bash
# macOS
dotnet publish -c Release --self-contained true -r osx-arm64

# Linux
dotnet publish -c Release --self-contained true -r linux-x64

# Windows
dotnet publish -c Release --self-contained true -r win-x64
```

## Configuration

### appsettings.json
Place in project root or user profile:
```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "qwen2.5:7b"
  },
  "NameMatching": {
    "ConfidenceThreshold": 0.8,
    "MaxRetries": 3
  },
  "FaceDetection": {
    "DefaultModel": "opencv-dnn",
    "Models": ["opencv-dnn", "yolov8-face"]
  }
}
```

## Rules Files

- `10-csharp-style.md` — C# coding standards
- `20-dotnet-cli.md` — CLI patterns and DI
- `30-computer-vision.md` — OpenCV and face detection
- `40-testing.md` — Testing strategies
- `50-commits-prs.md` — Commit conventions

## Dependencies

### NuGet Packages
- `Spectre.Console` — Rich CLI output
- `OpenCvSharp4` — OpenCV bindings
- `OpenCvSharp4.runtime.osx-arm64` — Platform-specific OpenCV
- `Microsoft.Extensions.DependencyInjection` — DI container
- `Microsoft.Extensions.Configuration` — Configuration
- `Microsoft.Extensions.Logging` — Logging

## AI Providers

### Ollama (Local)
- Models: `qwen2.5:7b`, `llava:7b`, `llama3.1:8b`
- Config: Set `Ollama:BaseUrl` in appsettings.json

### OpenAI (Cloud)
- Models: `gpt-4o`, `gpt-4o-mini`
- Config: Set `OPENAI_API_KEY` environment variable

## Validation

### CSV Output
The map command produces CSV with:
- `PlayerId` — Internal database ID
- `External_Player_ID` — Matched external photo ID
- `Valid_Mapping` — Whether confidence threshold met

### External Validation
```bash
cd /path/to/external-validation-data
python3 validate_map_output.py \
    --output /path/to/mapped_players.csv \
    --reference Competition2024/Csvs/mapped_players.csv
```

## External Data
- External validation data should live outside this repository.
- Do not commit real player data, private database dumps, or private validation assets.
