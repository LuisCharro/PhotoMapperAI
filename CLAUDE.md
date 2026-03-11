# PhotoMapperAI - Claude Code Instructions

## Overview
PhotoMapperAI is a .NET Core CLI application for mapping sports player photos to database systems using AI-powered name matching and face detection.

## Tech Stack
- **Framework:** .NET 10 (Core)
- **Language:** C#
- **CLI:** Spectre.Console
- **Image Processing:** OpenCV (OpenCvSharp4)
- **AI:** Ollama, OpenAI APIs

## Project Structure
```
PhotoMapperAI/
├── src/PhotoMapperAI/
│   ├── Commands/         # CLI commands (map, generate, benchmark)
│   ├── Services/         # Business logic (AI, Database, Image)
│   ├── Models/           # Data models
│   └── Utils/            # Utilities
└── docs/                 # Documentation
```

## Commands

### Build & Run
```bash
dotnet build
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- map --help
```

### Publish (Self-Contained)
```bash
dotnet publish -c Release --self-contained true -r osx-arm64
```

## Key Files
- `Commands/MapCommand.cs` — Player name mapping
- `Commands/GeneratePhotosCommand.cs` — Portrait generation
- `Services/AI/` — AI/LLM integration
- `Services/Image/` — OpenCV processing

## Rules
See `.kilocode/rules/`:
- `10-csharp-style.md` — C# coding standards
- `20-dotnet-cli.md` — CLI patterns
- `30-computer-vision.md` — Face detection
- `40-testing.md` — Testing
- `50-commits-prs.md` — Commits

## Dependencies
- Spectre.Console
- OpenCvSharp4
- Microsoft.Extensions.DependencyInjection

## Validation
```bash
python3 ../PhotoMapperAI_ExternalData_Test/validate_map_output.py \
    --output mapped.csv --reference reference.csv
```
