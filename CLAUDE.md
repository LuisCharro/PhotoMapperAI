# PhotoMapperAI - Claude Code Instructions

## Overview
PhotoMapperAI maps sports player photos to database records using AI-powered name matching and face detection. It ships as a CLI (automation/batch) and an Avalonia desktop UI (step-by-step and batch usage).

## Tech Stack
- **Framework:** .NET 10 (Core)
- **Language:** C#
- **CLI:** Spectre.Console
- **Desktop UI:** Avalonia
- **Image Processing:** OpenCV (OpenCvSharp4)
- **AI / LLM providers:** Ollama, OpenAI, Anthropic, ZAI, MiniMax

## Project Structure
```
PhotoMapperAI/
├── src/PhotoMapperAI/        # CLI and core services
│   ├── Commands/             # CLI commands (extract, map, generatephotos, benchmark, benchmark-compare)
│   ├── Services/             # Business logic (AI, Database, Image, Diagnostics)
│   ├── Models/               # Data models
│   └── Utils/                # Utilities
├── src/PhotoMapperAI.UI/     # Avalonia desktop UI
├── tests/                    # Unit tests (PhotoMapperAI.Tests)
├── samples/                  # SQL and config templates
├── scripts/                  # Validation and operational helpers
├── Validation/               # Validation runners and stats
└── docs/                     # Documentation
```

## Commands

### Build & Run
```bash
dotnet build PhotoMapperAI.sln
# CLI
dotnet run --project src/PhotoMapperAI -- map --help
# Desktop UI
dotnet run --project src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
```

### Test
```bash
dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj
```

### Publish (Self-Contained)
```bash
dotnet publish -c Release --self-contained true -r osx-arm64
```

## Key Files
- `Commands/MapCommand.cs` — Player name mapping
- `Commands/GeneratePhotosCommand.cs` — Portrait generation
- `Services/AI/` — Name-matching services (Ollama, OpenAI, Anthropic, ZAI, MiniMax) AND face-detection services (OpenCV YuNet/DNN, Apple Vision, Haar, Ollama vision) plus their factories
- `Services/Image/ImageProcessor.cs` — OpenCV crop/processing

## Rules
See `.kilocode/rules/`:
- `10-csharp-style.md` — C# coding standards
- `20-dotnet-cli.md` — CLI patterns
- `30-computer-vision.md` — Face detection
- `40-testing.md` — Testing
- `50-commits-prs.md` — Commits
- `60-avalonia-mvvm.md` — Avalonia UI / MVVM
- `70-dotnet10-photomapperai-rules.md` — .NET 10 project-specific rules

## Dependencies
- Spectre.Console
- Avalonia (desktop UI)
- OpenCvSharp4
- Microsoft.Extensions.DependencyInjection

## Validation
```bash
python3 scripts/run_validation_suite.py
python3 scripts/run_external_validation.py
```
