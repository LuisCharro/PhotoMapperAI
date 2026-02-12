# PhotoMapperAI

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Status](https://img.shields.io/badge/status-production--ready-green.svg)
![License](https://img.shields.io/badge/license-MIT-blue.svg)

AI-powered tool for mapping sports player photos to database systems. Available as both **command-line tool** and **desktop GUI application**. Automatically matches player names using LLMs, detects faces using multiple approaches, and generates portrait crops from full-body photos.

## Problem Solved

Sports organizations receive photo sets from external sources (e.g., FIFA) with limited metadata (typically just `PlayerID_FamilyName_Surname.png`). Integrating these into internal database systems requires:

1. **Data Extraction:** Export player data from internal database to CSV format
2. **Name Matching:** Map external photos to internal player records using AI
3. **Face Detection:** Detect faces and eyes in photos with configurable models
4. **Portrait Generation:** Crop full-body photos to portrait format

This tool automates the entire workflow, making it database-agnostic and system-independent.

## Features

- **Database-agnostic extraction:** Export player data from any database via SQL queries to CSV format
- **AI-powered name matching:** Uses local LLMs (Ollama) for fuzzy name matching with confidence scores
- **Flexible face detection:** Multiple approaches (OpenCV DNN, YOLOv8-Face, Ollama Vision) selectable via parameters
- **Automated portrait cropping:** Crops full-body photos to portrait format using AI-based face/eye detection
- **Filename pattern support:** Automatic pattern detection OR photo manifest file for flexible metadata extraction
- **Benchmarking capabilities:** Test different models and generate comparison reports
- **Cross-platform CI:** Automatic build + test validation on macOS and Windows (GitHub Actions)
- **Rich CLI experience:** Color-coded console output showing mapping progress, unmatched players, and unused photos

## Ollama Local Model Policy

For local inference stability on laptops, the tool enforces a local-model policy before Ollama requests:

- If the requested model is local, other running local models are unloaded first.
- Models ending with `:cloud` are ignored by this policy and are never unloaded.
- If the requested model is a cloud model (`:cloud`), no local models are unloaded.

## Workflow

### Step 1: Extract Data
```bash
PhotoMapperAI extract -inputSqlPath path/to/playersByTeam.sql -teamId 10 -outputName SpainTeam.csv
```
Runs a user-provided SQL query to export player data from the internal database to CSV format. Includes placeholder columns for `Fifa_Player_ID` and `Valid_Mapping`.

### Step 2: Map Photos to Players
```bash
# Using filename pattern detection
PhotoMapperAI map -inputCsvPath path/to/SpainTeam.csv -photosDir path/to/photos/SpainTeam

# Using photo manifest (alternative)
PhotoMapperAI map -inputCsvPath path/to/SpainTeam.csv -photosDir path/to/photos/SpainTeam -photoManifest manifest.json

# Provider-prefixed model syntax (cloud abstraction foundation)
PhotoMapperAI map -inputCsvPath path/to/SpainTeam.csv -photosDir path/to/photos/SpainTeam -nameModel ollama:qwen2.5:7b
PhotoMapperAI map -inputCsvPath path/to/SpainTeam.csv -photosDir path/to/photos/SpainTeam -nameModel openai:gpt-4o-mini
PhotoMapperAI map -inputCsvPath path/to/SpainTeam.csv -photosDir path/to/photos/SpainTeam -nameModel anthropic:claude-3-5-sonnet
```

- Loads all photo files and extracts metadata
- **Flexible options:** Uses automatic pattern detection OR user-specified pattern OR photo manifest file
- Attempts direct ID matching first
- For unmatched players, uses AI (Ollama LLM) for fuzzy name matching
- Validates matches with confidence threshold (default: 0.9)
- Updates CSV with `Fifa_Player_ID` and `Valid_Mapping` columns

### Step 3: Generate Portraits (with Face Detection)
```bash
# Using OpenCV DNN
PhotoMapperAI generatePhotos \
  -inputCsvPath path/to/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg \
  -faceDetection opencv-dnn

# Using Ollama Vision with fallback (recommended)
PhotoMapperAI generatePhotos \
  -inputCsvPath path/to/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg \
  -faceDetection llava:7b,qwen3-vl

# Using center crop (fastest, no AI)
PhotoMapperAI generatePhotos \
  -inputCsvPath path/to/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg \
  -faceDetection center

# Portrait only mode (reuse existing face detections)
PhotoMapperAI generatePhotos \
  -inputCsvPath path/to/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg \
  -portraitOnly
```

- Reads mapping CSV
- **Multiple face detection options:** OpenCV, YOLOv8, Ollama Vision, center crop
- **Automatic fallback:** Use comma-separated models (e.g., `llava:7b,qwen3-vl`) for reliability
- Detects faces and eyes in full-body photos
- Calculates portrait crop area based on eye position (or face center as fallback)
- Outputs portrait photos named with **PlayerId** (internal system ID, not ExternalId)

> **Note:** Portrait crop behavior:
> - **With AI face detection:** Crops around eyes/face for optimal framing (head + neck + bit of chest)
> - **Center mode (no AI):** Crops from upper portion of image (top 22% of height) assuming full-body sports photos. This captures head + neck + bit of chest for proper portrait composition.
> - All portraits are resized to exact dimensions (default: 200x300 pixels)
>
> **See:** [`docs/FACE_DETECTION_GUIDE.md`](docs/FACE_DETECTION_GUIDE.md) for detailed model comparison and best practices.

## Tech Stack

- **.NET 10** (Core) - Command-line application
- **Ollama** - Local LLMs for name matching and vision models for face detection
- **OpenCV** - Computer vision library (DNN, YOLOv8-Face) for face detection
- **Qwen Vision Model** - AI-based face/eye detection for portrait cropping
- **CSV processing** - Read/write player mappings
- **Command-line parser** - Rich CLI interface with subcommands
- **GitHub Actions** - Cross-platform CI pipeline

## Benchmark Comparison

Use the compare command to validate Windows vs macOS benchmark results:

```bash
PhotoMapperAI benchmark-compare \
  --baseline benchmark-results/benchmark-20260212-075152.json \
  --candidate benchmark-results/<windows-file>.json \
  --faceModel opencv-dnn
```

## External Real-Data Validation (No Data Commit)

If you keep private test data outside this repo (for example in `PhotoMapperAI_ExternalData`), use:

1. Config template: `samples/external_validation.config.template.json`
2. Runner script: `scripts/run_external_validation.py`

Example:

```bash
python3 scripts/run_external_validation.py --config samples/external_validation.config.template.json
```

This will:
- prepare team CSVs (from source players CSV, or synthesize from filenames if needed),
- run `map`,
- run `generatephotos`,
- compare generated portrait IDs against expected portrait IDs,
- write a report in the configured `outputRoot`.

### Validation Suite Runner (Overwrite + All/Single Preset)

To run the predefined validation presets and always overwrite previous results:

```bash
# Run all presets:
python3 scripts/run_validation_suite.py --run all

# Run only one preset:
python3 scripts/run_validation_suite.py --run opencv
python3 scripts/run_validation_suite.py --run llava
```

Preset keys:
- `run`
- `opencv`
- `llava`

Behavior:
- If target output folder exists, it is deleted and recreated.
- At the end, paths to each run report/generated folders are printed for manual review.
- A cross-run comparison report is generated at:
  `/Users/luis/Repos/PhotoMapperAI_ExternalData/RealDataValidation/validation_runs_comparison.md`

## Architecture

### Modular AI Services

All AI components implement interfaces for easy swapping and testing:

```csharp
// Name Matching Interface
public interface INameMatchingService
{
    Task<MatchResult> CompareNamesAsync(string name1, string name2);
    string ModelName { get; }
}

// Implementations
- OllamaNameMatchingService ("qwen2.5:7b", "qwen3:8b", "llava:7b")
- (Future: OpenAI, Claude, etc.)

// Face Detection Interface
public interface IFaceDetectionService
{
    Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath);
    string ModelName { get; }
}

// Implementations
- OpenCVDNNFaceDetectionService ("opencv-dnn")
- YOLOv8FaceDetectionService ("yolov8-face")
- OllamaFaceDetectionService ("llava:7b", "qwen3-vl")
- (Future: Haar Cascade fallback)
```

### Face Detection Strategy

**Multi-tier approach:**

```
Try YOLOv8-Face (highest accuracy)
  ↓ Success?
Use detected landmarks
  ↓ Failure?
Try OpenCV DNN (good accuracy, fast)
  ↓ Success?
Use detected landmarks
  ↓ Failure?
Try Ollama Vision (handles edge cases)
  ↓ Success?
Use detected landmarks
  ↓ Failure?
Use center crop fallback (last resort)
```

**Fallback Portrait Logic:**
- **Both eyes detected:** Calculate eye midpoint → crop centered on eyes
- **One eye detected:** Use face center + offset
- **No eyes detected:** Use face center
- **No face detected:** Use center crop

## Project Structure

```
PhotoMapperAI/
├── src/
│   ├── PhotoMapperAI/            # Main CLI project (core library)
│   │   ├── Commands/            # CLI command handlers
│   │   │   ├── ExtractCommand.cs
│   │   │   ├── MapCommand.cs
│   │   │   └── GeneratePhotosCommand.cs
│   │   ├── Services/            # Core business logic
│   │   │   ├── DatabaseExtractor.cs
│   │   │   ├── PhotoMatcher.cs
│   │   │   ├── FilenameParser.cs
│   │   │   ├── AI/
│   │   │   │   ├── INameMatchingService.cs
│   │   │   │   ├── OllamaNameMatchingService.cs
│   │   │   │   ├── IFaceDetectionService.cs
│   │   │   │   ├── OpenCVDNNFaceDetectionService.cs
│   │   │   │   ├── YOLOv8FaceDetectionService.cs
│   │   │   │   ├── OllamaFaceDetectionService.cs
│   │   │   │   └── ModelFactory.cs
│   │   │   ├── PortraitCropper.cs
│   │   │   └── IImageProcessor.cs
│   │   ├── Models/              # Data models
│   │   │   ├── PlayerRecord.cs
│   │   │   ├── PhotoMetadata.cs
│   │   │   ├── MappingResult.cs
│   │   │   ├── PhotoManifest.cs
│   │   │   └── FaceLandmarks.cs
│   │   ├── Utils/               # Helpers
│   │   │   ├── CsvHelper.cs
│   │   │   ├── ConsoleHelper.cs
│   │   │   └── OllamaClient.cs
│   │   └── Program.cs           # CLI entry point
│   └── PhotoMapperAI.UI/        # Desktop GUI (Avalonia)
│       ├── ViewModels/            # MVVM ViewModels
│       │   ├── MainWindowViewModel.cs
│       │   ├── ExtractStepViewModel.cs
│       │   ├── MapStepViewModel.cs
│       │   └── GenerateStepViewModel.cs
│       ├── Views/                # Avalonia XAML views
│       │   ├── MainWindow.axaml
│       │   ├── ExtractStepView.axaml
│       │   ├── MapStepView.axaml
│       │   └── GenerateStepView.axaml
│       ├── App.axaml             # Application resources
│       ├── Program.cs            # GUI entry point
│       └── ViewLocator.cs        # ViewModel-to-View mapping
├── tests/                       # Unit tests + Benchmark tests
│   └── PhotoMapperAI.Tests/
│       ├── Models/
│       ├── Services/
│       ├── AI/
│       ├── Benchmarks/
│       └── Data/
├── docs/
│   ├── ARCHITECTURE_DECISIONS.md
│   ├── TESTING_STRATEGY.md
│   └── MODEL_BENCHMARKS.md
├── data/                        # Sample data (gitignored)
└── samples/                     # Example configs and templates
```

## Getting Started

PhotoMapperAI is available in two modes:

### Desktop GUI Application (Recommended for Beginners)

**Cross-platform desktop application** with step-by-step wizard interface.

**Quick Start:**
```bash
# From the PhotoMapperAI directory
dotnet run --project src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
```

**Features:**
- Visual step-by-step workflow (Extract → Map → Generate)
- File browser dialogs for easy file selection
- Real-time progress indicators
- All CLI parameters with friendly UI controls
- Session save/load for continuing work later

**Known Issues (GUI):**
- Session save/load currently uses default app data path (no file picker yet)

**Documentation:** See [`GUIDE.md`](GUIDE.md) for complete GUI documentation.

### Command-Line Interface (For Automation & Batch Processing)

**Full-featured CLI** for scripting and automation.

### Prerequisites

- **.NET 10 SDK**
  ```bash
  # macOS (Homebrew)
  brew install dotnet

  # Windows (from https://dotnet.microsoft.com/download)
  # Download and install .NET 10 SDK
  ```

- **[Ollama](https://ollama.ai/)** installed and running
  ```bash
  # macOS
  brew install ollama

  # Start Ollama
  ollama serve

  # Pull models
  ollama pull qwen2.5:7b              # Name matching
  ollama pull qwen3:8b              # Better name matching
  ollama pull llava:7b               # Vision model (text+vision)
  ollama pull qwen3-vl              # Better vision model
  ```

- **OpenCV** (optional, for faster face detection)
  - Included via NuGet: `OpenCvSharp4`
  - Model files must be downloaded manually (see [`docs/OPENCV_MODELS.md`](docs/OPENCV_MODELS.md))
  - Configure paths in `appsettings.json` (copy from `appsettings.template.json`)

### Building

```bash
dotnet build
```

### Usage

#### Extract
```bash
# Extract player data
dotnet run -- extract -inputSqlPath data.sql -outputName team.csv
```

> **Note:** SQL query examples for different databases are available in `samples/sql-examples/`. See [`samples/sql-examples/README.md`](samples/sql-examples/README.md) for:
> - SQL Server, MySQL, PostgreSQL, SQLite examples
> - Guide to adapt queries to your database schema
> - Required output column format
> - Common parameter syntax differences

#### Map (3 approaches)
```bash
# 1. Automatic pattern detection
dotnet run -- map -inputCsvPath team.csv -photosDir ./photos

# 2. User-specified pattern
dotnet run -- map -inputCsvPath team.csv -photosDir ./photos -filenamePattern "{id}_{family}_{sur}.png"

# 3. Photo manifest
dotnet run -- map -inputCsvPath team.csv -photosDir ./photos -photoManifest manifest.json
```

#### Generate Photos (face detection options)
```bash
# OpenCV DNN (fast, good accuracy)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection opencv-dnn

# Ollama Vision with fallback (recommended for best results)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection llava:7b,qwen3-vl

# Qwen3-VL only (best for challenging angles)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection qwen3-vl

# Center crop (fastest, no AI - uses upper-body crop)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection center

# Haar Cascade (fastest with eye detection - may have issues on macOS)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection haar-cascade

# Portrait only (reuse existing detections)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -portraitOnly
```

#### Benchmark
```bash
# Compare name matching models
dotnet run -- benchmark -nameModels qwen2.5:7b,qwen3:8b,llava:7b -testDataPath ./tests/Data

# Compare face detection models
dotnet run -- benchmark -faceModels opencv-dnn,yolov8-face,llava:7b,qwen3-vl -testDataPath ./tests/Data

# Compare both name matching and face detection
dotnet run -- benchmark -nameModels qwen2.5:7b,qwen3:8b -faceModels opencv-dnn,llava:7b -testDataPath ./tests/Data
```

## Test Data

**Test data is local-only and should not be committed to the repository.** Create your own test data structure following this pattern:

```
Test Data (local, not in repo):
├── DataExtraction/           # Database to CSV
│   ├── sql_queries/            # SQL query templates
│   ├── database.db            # SQLite test database
│   └── expected_output/       # Expected CSV outputs
│
├── NameMatch/                 # CSV + Photos → Mapping
│   ├── photo_names/            # Test photo files (0 bytes, placeholders)
│   ├── photo_manifest.json    # Alternative: Photo metadata manifest
│   └── expected_output/       # Expected mapping results
│
├── FaceRecognition/          # Face/eye detection
│   ├── players.csv             # Player data (3+ members)
│   ├── photos/                 # Test photos (head to knees)
│   └── expected_output/       # Expected detection results (JSON)
│
└── PortraitExtraction/        # Portraits from detected faces
    ├── players.csv             # Reuse from FaceRecognition
    ├── photos/                 # Photos with face regions
    └── expected_output/       # Expected portrait crops
```

## Current Status

**Last Updated:** 2026-02-12

### Feature Status

| Feature | Status | Notes |
|---------|--------|-------|
| Database extraction (CSV) | ✅ Production Ready | Works with any SQL database |
| Name matching (AI) | ✅ Production Ready | 90% accuracy with Ollama LLMs |
| Photo mapping | ✅ Production Ready | 49/49 FIFA photos successfully mapped |
| Face detection (OpenCV) | ✅ Fixed | Model files added, fallback working |
| Face detection (Ollama Vision) | ✅ Working | qwen3-vl and llava:7b supported |
| Portrait generation | ✅ Fixed | Correct 200x300 dimensions |
| Desktop GUI (Avalonia) | 🚧 In Progress | Core workflow works; see GUI known issues |
| PowerShell scripts | ✅ Complete | Windows support available |

### Known Issues

No critical CLI issues currently.

GUI known issues:
- Session save/load currently uses default app data path (no file picker yet).

### Planned Improvements

See [`docs/PORTRAIT_IMPROVEMENTS_PLAN.md`](docs/PORTRAIT_IMPROVEMENTS_PLAN.md) for detailed enhancement plans.

**Completed:**
- ✅ **Face-Based Crop Dimensions** - Crop size based on detected face (2x width, 3x height)
- ✅ **Portrait Photo Detection** - Skip cropping for photos that are already portraits
- ✅ **Eye Position Centering** - Eyes positioned at 35% from top for standard composition

**Pending:**
- ⏳ **Haar Cascade Eye Detection** - Created but has native library issues on macOS
- ⏳ **Multiple Output Sizes** - Generate multiple portrait sizes in one run
- ⏳ **Debug Visualization** - Save intermediate images with detected regions highlighted

### Recent Commits (feature/phase1-implementation)

- `5bc313f` - Fix: Improve portrait crop with face-based dimensions and eye positioning
- `8c9ba8f` - Fix face detection initialization logic
- `d81f103` - Fix default portrait dimensions from 800x1000 to 200x300

### Documentation

- [`PROGRESS.md`](PROGRESS.md) - Development progress and tasks
- [`TEST_SESSION.md`](TEST_SESSION.md) - Test session logs and findings
- [`PHASE3_VALIDATION_REPORT.md`](PHASE3_VALIDATION_REPORT.md) - Detailed Phase 3 validation
- [`RELEASE_NOTES.md`](RELEASE_NOTES.md) - v1.0.0 release notes and features
- [`CHANGELOG.md`](CHANGELOG.md) - Version history and detailed changes
- [`docs/`](docs/) - Technical documentation


**To use local test data:** See [`docs/TEST_CONFIGURATION.md`](docs/TEST_CONFIGURATION.md) for detailed setup instructions. Use the provided template (`test-config.template.json`) and configure it with your local paths.

**Important:**
- Never commit real player data or personal database connection strings
- Use synthetic data for all tests
- Photo files can be 0-byte placeholders for filename extraction testing
- For face detection testing, use publicly available stock photos

## Filename Pattern Extraction

**Problem:** Photo filenames have varying patterns per photo set.

**Solution:** 3-tier approach

1. **Automatic Detection:** Try common regex patterns
   ```csharp
   // Pattern examples:
   // {id}_{family}_{sur}.png
   // {sur}-{family}-{id}.jpg
   // {family}, {sur} - {id}.png
   ```

2. **User-Specified Pattern:** Template-based parsing
   ```bash
   -filenamePattern "{id}_{family}_{sur}.png"
   ```

3. **Photo Manifest:** JSON file for complex cases
   ```json
   {
     "photo.png": {
       "id": "12345",
       "fullName": "Messi Lionel"
     }
   }
   ```

## Benchmarking & Model Comparison

### Name Matching Models

| Model | Accuracy | Speed (ms) | Use Case |
|-------|----------|-------------|----------|
| qwen2.5:7b | 92% | 850 | Default, good balance |
| qwen3:8b | 95% | 920 | Production, best accuracy |
| llava:7b | 88% | 1100 | Text+vision tasks |
| llama3.2:3b | 85% | 520 | Speed-critical |

### Face Detection Approaches

| Model | Face Detection | Both Eyes | Speed (ms) | Use Case |
|-------|----------------|-----------|-------------|----------|
| YOLOv8-Face | 97% | 88% | 65 | Best accuracy |
| OpenCV DNN | 95% | 82% | 45 | Good speed/accuracy |
| Qwen3-VL | 94% | 80% | 3200 | Challenging angles (LLM) |
| LLaVA:7b | 92% | 75% | 3500 | Edge cases (LLM) |
| Haar Cascade | 88% | 70% | 25 | Fastest (⚠️ macOS issues) |
| Center | N/A | N/A | 1 | No AI, upper-body crop |

> **Note:** LLM-based models (Qwen3-VL, LLaVA:7b) use Ollama Vision API and require the models to be pulled via `ollama pull <model>`.

## Use Cases

- **Team Sports Organizations:** National teams, club teams, and sports federations managing player photos
- **Sports Competitions:** Events and tournaments requiring photo imports from external sources
- **Sports Media:** Media organizations managing athlete photo databases
- **Team Management Systems:** Any organization needing to import external photos into internal systems

## CLI Parameters Reference

### Extract
| Parameter | Description | Required |
|-----------|-------------|------------|
| `-inputSqlPath` | Path to SQL file | Yes |
| `-connectionStringPath` | Path to connection string file | Yes |
| `-teamId` | Team ID filter | Yes |
| `-outputName` | Output CSV filename | Yes |

### Map
| Parameter | Description | Required | Default |
|-----------|-------------|------------|---------|
| `-inputCsvPath` | Path to CSV file | Yes | - |
| `-photosDir` | Path to photos directory | Yes | - |
| `-filenamePattern` | Filename pattern template | No | Auto-detect |
| `-photoManifest` | Path to photo manifest JSON | No | - |
| `-nameModel` | Ollama model for name matching | No | qwen2.5:7b |
| `-confidenceThreshold` | Minimum confidence for match | No | 0.9 |

### Generate Photos
| Parameter | Description | Required | Default |
|-----------|-------------|------------|---------|
| `-inputCsvPath` | Path to CSV file | Yes | - |
| `-processedPhotosOutputPath` | Output path for portraits | Yes | - |
| `-photosDir` | Directory containing source photos | Yes | - |
| `-format` | Image format (jpg/png) | No | jpg |
| `-faceDetection` | Face detection model (see below) | No | llava:7b,qwen3-vl |
| `-portraitOnly` | Skip face detection, use existing | No | false |
| `-faceWidth` | Portrait width in pixels | No | 200 |
| `-faceHeight` | Portrait height in pixels | No | 300 |

**Face Detection Models:**
| Model | Description |
|-------|-------------|
| `opencv-dnn` | OpenCV DNN neural network (fast, good accuracy) |
| `yolov8-face` | YOLOv8 face detection (best accuracy) |
| `llava:7b` | Ollama LLaVA 7B vision model (LLM-based) |
| `qwen3-vl` | Ollama Qwen3-VL vision model (LLM-based, best for angles) |
| `haar-cascade` | OpenCV Haar Cascade (fastest, may have macOS issues) |
| `center` | No AI, upper-body crop from top 22% |
| Comma-separated | Fallback chain, e.g., `llava:7b,qwen3-vl,center` |

### Benchmark
| Parameter | Description | Required | Default |
|-----------|-------------|------------|---------|
| `-nameModels` | Comma-separated name models | No | All |
| `-faceModels` | Comma-separated face models | No | All |
| `-testDataPath` | Path to test data | Yes | - |
| `-outputPath` | Path for benchmark results | No | benchmark-results/ |

## Contributing

Contributions are welcome! This is an open-source project for the sports community.

Areas for contribution:
- Additional face detection models
- Support for more databases
- More filename pattern examples
- Benchmark data and test cases

## Troubleshooting

### Common Issues

If you encounter problems, check [`docs/EDGE_CASES.md`](docs/EDGE_CASES.md) for:

- **Name matching issues:** Transliteration differences, name order variations, nicknames
- **Photo file problems:** No photo found, multiple photos, unsupported formats
- **Face detection failures:** No face detected, multiple faces, extreme angles
- **Performance issues:** Slow processing, memory constraints
- **Database connection problems:** Connection strings, parameter syntax
- **CSV issues:** Encoding problems, invalid formats

### Quick Fixes

| Problem | Quick Solution |
|---------|----------------|
| Face detection too slow | Use `-d center` or `-d llava:7b` instead of `qwen3-vl` |
| No photo found | Check filename pattern or use `-photoManifest` |
| Unrecognized photo format | Convert to PNG/JPG (supported: .png, .jpg, .jpeg, .bmp) |
| Name matching fails | Lower confidence threshold with `-t 0.7` |
| Memory issues | Reduce parallel degree: `-par -pd 2` |

### Getting Help

For more detailed troubleshooting:
- See [`docs/EDGE_CASES.md`](docs/EDGE_CASES.md) - Comprehensive edge cases guide
- See [`docs/FACE_DETECTION_GUIDE.md`](docs/FACE_DETECTION_GUIDE.md) - Face detection model guide
- See [`samples/sql-examples/README.md`](samples/sql-examples/README.md) - SQL query adaptation guide
- Report issues: https://github.com/LuisCharro/PhotoMapperAI/issues

## License

TBD

## Author

[Luis Charro](https://github.com/LuisCharro)

## Acknowledgments

- **Ollama** - Local LLM and vision models
- **OpenCV** - Computer vision library
- **Microsoft** - .NET and System.CommandLine
- **Josh Close** - CsvHelper library
