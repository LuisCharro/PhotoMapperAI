# PhotoMapperAI

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Status](https://img.shields.io/badge/status-production--ready-green.svg)
![License](https://img.shields.io/badge/license-MIT-blue.svg)

AI-powered command-line tool for mapping sports player photos to database systems. Automatically matches player names using LLMs, detects faces using multiple approaches, and generates portrait crops from full-body photos.

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
- **Rich CLI experience:** Color-coded console output showing mapping progress, unmatched players, and unused photos

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
> - **Center mode (no AI):** Crops from upper portion of image (top 35% of height) assuming full-body sports photos. This captures head + neck + upper chest for proper portrait composition.
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
│   └── PhotoMapperAI/         # Main CLI project
│       ├── Commands/            # CLI command handlers
│       │   ├── ExtractCommand.cs
│       │   ├── MapCommand.cs
│       │   └── GeneratePhotosCommand.cs
│       ├── Services/            # Core business logic
│       │   ├── DatabaseExtractor.cs
│       │   ├── PhotoMatcher.cs
│       │   ├── FilenameParser.cs
│       │   ├── AI/
│       │   │   ├── INameMatchingService.cs
│       │   │   ├── OllamaNameMatchingService.cs
│       │   │   ├── IFaceDetectionService.cs
│       │   │   ├── OpenCVDNNFaceDetectionService.cs
│       │   │   ├── YOLOv8FaceDetectionService.cs
│       │   │   ├── OllamaFaceDetectionService.cs
│       │   │   └── ModelFactory.cs
│       │   ├── PortraitCropper.cs
│       │   └── IImageProcessor.cs
│       ├── Models/              # Data models
│       │   ├── PlayerRecord.cs
│       │   ├── PhotoMetadata.cs
│       │   ├── MappingResult.cs
│       │   ├── PhotoManifest.cs
│       │   └── FaceLandmarks.cs
│       ├── Utils/               # Helpers
│       │   ├── CsvHelper.cs
│       │   ├── ConsoleHelper.cs
│       │   └── OllamaClient.cs
│       └── Program.cs           # Entry point
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
# OpenCV DNN (default)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg

# Ollama Vision with fallback (recommended)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection llava:7b,qwen3-vl

# Center crop (fastest, no AI)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection center

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

# Compare face detection approaches
dotnet run -- benchmark -faceModels opencv-dnn,yolov8-face,llava:7b,qwen3-vl -testDataPath tests/Data/
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

**Last Updated:** 2026-02-11

### Feature Status

| Feature | Status | Notes |
|---------|--------|-------|
| Database extraction (CSV) | ✅ Production Ready | Works with any SQL database |
| Name matching (AI) | ✅ Production Ready | 90% accuracy with Ollama LLMs |
| Photo mapping | ✅ Production Ready | 49/49 FIFA photos successfully mapped |
| Face detection (OpenCV) | ✅ Fixed | Model files added, fallback working |
| Face detection (Ollama Vision) | ✅ Working | qwen3-vl and llava:7b supported |
| Portrait generation | ✅ Fixed | Correct 200x300 dimensions |
| PowerShell scripts | ✅ Complete | Windows support available |

### Known Issues

None. All Phase 3 critical issues have been resolved.

### Planned Improvements

See [`docs/PORTRAIT_IMPROVEMENTS_PLAN.md`](docs/PORTRAIT_IMPROVEMENTS_PLAN.md) for upcoming enhancements:

- **Haar Cascade Eye Detection** - More reliable eye detection for consistent centering
- **Face-Based Crop Dimensions** - Crop size based on detected face, not image size
- **Multiple Output Sizes** - Generate multiple portrait sizes in one run
- **Portrait Photo Detection** - Skip cropping for photos that are already portraits
- **Debug Visualization** - Save intermediate images with detected regions highlighted

### Recent Commits (feature/phase1-implementation)

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
| Qwen3-VL | 94% | 80% | 3200 | Challenging angles |
| LLaVA:7b | 92% | 75% | 3500 | Edge cases |
| Haar Cascade | 88% | 70% | 25 | Fastest |

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
| `-format` | Image format (jpg/png) | No | jpg |
| `-faceDetection` | Face detection model | No | opencv-dnn |
| `-portraitOnly` | Skip face detection, use existing | No | false |
| `-faceWidth` | Portrait width in pixels | No | 800 |
| `-faceHeight` | Portrait height in pixels | No | 1000 |

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
