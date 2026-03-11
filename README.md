# PhotoMapperAI

![Version](https://img.shields.io/badge/version-1.3.0-blue.svg)
![Status](https://img.shields.io/badge/status-production--ready-green.svg)
![License](https://img.shields.io/badge/license-MIT-blue.svg)

PhotoMapperAI maps external player photos to internal player records and generates portrait crops. The repository contains:

- a CLI for automation and batch workflows
- an Avalonia desktop UI for step-by-step and batch usage
- supporting docs, validation scripts, and SQL/query templates

## What It Does

Typical workflow:

1. `extract` exports players or teams from your database into CSV.
2. `map` links photos to CSV rows using direct ID matches, deterministic name matching, and optional AI fallback.
3. `generatephotos` creates portraits from the mapped CSV with configurable face detection and output sizing.
4. `benchmark` and `benchmark-compare` help compare model behavior and regression results.

## Current Command Surface

The CLI entry point is `photomapperai` and exposes these subcommands:

- `extract`
- `map`
- `generatephotos`
- `benchmark`
- `benchmark-compare`

Examples:

```bash
dotnet run --project src/PhotoMapperAI -- extract \
  --inputSqlPath samples/sql-examples/sql-server-players.sql \
  --connectionStringPath samples/connection_string_template.txt \
  --teamId 10 \
  --outputName team.csv

dotnet run --project src/PhotoMapperAI -- map \
  --inputCsvPath team.csv \
  --photosDir ./photos \
  --useAI \
  --aiSecondPass \
  --nameModel qwen2.5:7b

dotnet run --project src/PhotoMapperAI -- generatephotos \
  --inputCsvPath team.csv \
  --photosDir ./photos \
  --processedPhotosOutputPath ./portraits \
  --format jpg \
  --faceDetection opencv-yunet
```

## Mapping Highlights

- Direct ID matches are applied first when `External_Player_ID` already exists.
- Deterministic matching runs globally for unresolved rows before any AI calls.
- AI is optional and can run as fallback with `--useAI`, `--aiSecondPass`, or `--aiOnly`.
- Provider-aware model identifiers are supported, including local and cloud-backed variants such as:
  - `qwen2.5:7b`
  - `ollama:qwen2.5:7b`
  - `openai:gpt-5-mini`
  - `anthropic:claude-3-5-sonnet`
  - `zai:glm-4.5`
  - `minimax:MiniMax-M2.5`

See [`docs/guides/NAME_MAPPING_PIPELINE.md`](docs/guides/NAME_MAPPING_PIPELINE.md) for the current mapping logic and tuning notes.

## Portrait Generation Highlights

- Face detection models currently wired in code include:
  - `apple-vision`
  - `opencv-yunet`
  - `opencv-dnn`
  - `yolov8-face`
  - `haar-cascade`
  - `center`
  - Ollama vision models such as `llava:7b` and `qwen3-vl`
- Fallback chains are supported through comma-separated values, for example:
  - `opencv-yunet,llava:7b,center`
- Size profiles are supported through `--sizeProfile` and `--allSizes`.
- Output profile aliases are supported through `--outputProfile` with `none`, `test`, and `prod`.
- Crop-frame overrides are supported through `--cropFrameWidth` and `--cropFrameHeight`.
- Placeholder handling is supported through `--placeholderImage` or per-variant `placeholderPath` in a size profile.
- Single-player verification runs are supported through `--onlyPlayer`.

See [`docs/guides/FACE_DETECTION_GUIDE.md`](docs/guides/FACE_DETECTION_GUIDE.md) for the current generate flow.

## Desktop UI

Run the UI with:

```bash
dotnet run --project src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
```

Current UI capabilities:

- step-by-step Extract, Map, and Generate workflow
- batch automation for multi-team processing
- preview generation with the shared generation logic
- custom preview crop-frame presets
- session save/load to the default app-data location
- manual unmapped-player repair dialog in map and batch flows
- face-detection defaults that are platform-aware
  - macOS prefers `apple-vision`
  - Windows/Linux prefer `opencv-yunet`

See [`docs/guides/GUIDE.md`](docs/guides/GUIDE.md).

## Configuration Files

Relevant root-level files:

- [`appsettings.template.json`](appsettings.template.json): template for OpenCV paths, Ollama defaults, preview presets, and output profiles
- [`size_profiles.json`](size_profiles.json): sample multi-size generation profile
- [`test-config.template.json`](test-config.template.json): template for local-only validation data layout

Notes:

- the UI project copies `size_profiles.json` and `appsettings.json` into its output
- OpenCV download URLs and model filenames are defined in `appsettings.json`
- output profile aliases can be sourced from config and overridden by environment variables where supported by the resolver

## Build and Test

Build:

```bash
dotnet build PhotoMapperAI.sln
```

Run tests:

```bash
dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj
```

Useful scripts:

- `scripts/run_external_validation.py`
- `scripts/run_validation_suite.py`
- `scripts/compare_portrait_sets.py`
- `scripts/download-opencv-models.ps1`
- `scripts/download-opencv-models.sh`

## Repository Layout

```text
src/PhotoMapperAI          CLI and core services
src/PhotoMapperAI.UI       Avalonia desktop UI
tests/PhotoMapperAI.Tests  Unit tests
docs/                      Guides, testing notes, reports, planning docs
samples/                   SQL and config templates
scripts/                   Validation and operational helpers
```

## Documentation

- [`docs/README.md`](docs/README.md): documentation index
- [`CHANGELOG.md`](CHANGELOG.md): release history and unreleased changes
- [`RELEASE_NOTES.md`](RELEASE_NOTES.md): release draft notes

## License

[MIT](LICENSE)
