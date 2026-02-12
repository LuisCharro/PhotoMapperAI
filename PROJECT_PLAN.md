# Project Plan - PhotoMapperAI

## Architecture Overview

```
PhotoMapperAI/
├── src/
│   └── PhotoMapperAI/           # Main CLI project
│       ├── Commands/            # CLI command handlers
│       │   ├── ExtractCommand.cs
│       │   ├── MapCommand.cs
│       │   └── GeneratePhotosCommand.cs
│       ├── Services/            # Core business logic (modular, swappable)
│       │   ├── DatabaseExtractor.cs
│       │   ├── PhotoMatcher.cs
│       │   ├── AI/
│       │   │   ├── INameMatchingService.cs
│       │   │   ├── OllamaNameMatchingService.cs
│       │   │   ├── IFaceDetectionService.cs
│       │   │   ├── OpenCVFaceDetectionService.cs
│       │   │   ├── OllamaFaceDetectionService.cs (alternative)
│       │   │   └── ModelFactory.cs
│       │   ├── PortraitCropper.cs
│       │   └── IImageProcessor.cs
│       ├── Models/              # Data models
│       │   ├── PlayerRecord.cs
│       │   ├── PhotoMetadata.cs
│       │   ├── MappingResult.cs
│       │   └── PhotoManifest.cs
│       ├── Utils/               # Helpers
│       │   ├── CsvHelper.cs
│       │   ├── ConsoleHelper.cs
│       │   └── OllamaClient.cs
│       └── Program.cs           # Entry point
├── tests/                       # Unit tests + Benchmark tests
│   ├── PhotoMapperAI.Tests/
│   │   ├── NameMatchingBenchmarks.cs
│   │   ├── FaceDetectionBenchmarks.cs
│   │   └── TestDataGenerator.cs
│   └── benchmark-results/       # Test results documentation
├── docs/
│   └── MODEL_BENCHMARKS.md       # Success rate tables and findings
├── data/                        # Sample data (gitignored)
└── samples/                     # Example configs and templates
```

## Core Components

### 1. DatabaseExtractor Service
- Reads SQL file from disk
- Executes SQL query with connection string
- Outputs CSV with columns: `UserId, FamilyName, SurName, Fifa_Player_ID, Valid_Mapping`

**Parameters:**
- `-inputSqlPath`: Path to SQL file
- `-connectionStringPath`: Path to connection string file
- `-teamId`: Team ID filter
- `-outputName`: Output CSV filename

### 2. PhotoMatcher Service
- Scans photo directory for image files
- **Flexible filename parsing:**
  - Default: `{ExternalId}_{FamilyName}_{SurName}.png` (FIFA-style)
  - Alternative: Use photo manifest file for metadata
- Creates dictionary: `key=ExternalId, value=PhotoMetadata`

**Mapping Logic:**
1. **Direct match**: CSV has Fifa_Player_ID already → skip
2. **Fuzzy match**: Use pluggable AI service (Ollama LLM) to compare names
3. **Confidence threshold**: Accept match only if >= 0.9 (configurable)
4. **Model-agnostic**: Can test different LLMs

**Photo Manifest Support:**
```bash
# When photo filenames don't contain IDs
PhotoMapperAI map -inputCsvPath team.csv -photosDir ./photos -photoManifest ./manifest.json
```

### 3. NameMatchingService (Modular AI)

**Interface:**
```csharp
public interface INameMatchingService
{
    Task<MatchResult> CompareNamesAsync(string name1, string name2);
    string ModelName { get; }
}
```

**Implementations:**
- `OllamaNameMatchingService` - Uses Ollama (qwen2.5, qwen3, gemma2, llama3.2)
- `OpenAINameMatchingService` - Uses OpenAI API (optional)
- `ClaudeNameMatchingService` - Uses Anthropic API (optional)

**Model Selection:**
```bash
# Use default model
PhotoMapperAI map -inputCsvPath team.csv -photosDir ./photos

# Specify model
PhotoMapperAI map -inputCsvPath team.csv -photosDir ./photos -nameModel qwen2.5:7b
PhotoMapperAI map -inputCsvPath team.csv -photosDir ./photos -nameModel qwen3:8b
PhotoMapperAI map -inputCsvPath team.csv -photosDir ./photos -nameModel gemma2:9b
```

**Ollama Models to Test:**
- `qwen2.5:7b` - Base model, installed
- `qwen3:8b` - Newer version, better reasoning
- `llama3.2:3b` - Faster but less accurate
- `gemma2:9b` - Good alternative
- `llava:7b` - Can also do text tasks (vision model)

**Prompt Strategy:**
```
You are a name matching assistant. Compare these two player names and determine if they represent the same person.

Player A: "Rodríguez Sánchez, Francisco Román Alarcón"
Player B: "Rodríguez Sánchez, Isco"

Return ONLY a number from 0 to 1 representing the probability they are the same person, where 1 is certain and 0 is impossible.
```

### 4. FaceDetectionService (Modular AI)

**Interface:**
```csharp
public interface IFaceDetectionService
{
    Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath);
    string ModelName { get; }
}

public class FaceLandmarks
{
    public Rect FaceRect { get; set; }      // Full face bounding box
    public Point LeftEye { get; set; }      // Left eye coordinates
    public Point RightEye { get; set; }     // Right eye coordinates
    public Point FaceCenter { get; set; }   // Face center
    public bool BothEyesDetected { get; set; }
}
```

**Implementations:**

#### A. OpenCV DNN Face Detection (Primary)
- Uses pre-trained deep learning models (DNN)
- Models: OpenCV DNN Face Detector or YOLOv8-Face
- **Pros:** Fast, precise, local, no API costs
- **Cons:** Requires model files, can miss extreme angles
- **Models to test:**
  - OpenCV DNN (res10_300x300_ssd_iter_140000.caffemodel)
  - YOLOv8-Face (better accuracy)
  - MTCNN (Multi-task Cascaded CNN)

#### B. Ollama Vision (Alternative)
- Uses LLaVA, Qwen3-VL, or Llama3.2-Vision
- **Pros:** Can handle challenging angles, understands context
- **Cons:** Slower, less precise for pixel coordinates
- **Models to test:**
  - `llava:7b` - General vision model
  - `qwen3-vl:7b` - Better vision-language model
  - `llama3.2-vision:11b` - Latest model

**Model Selection:**
```bash
# Use default (OpenCV DNN)
PhotoMapperAI generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits

# Use Ollama Vision
PhotoMapperAI generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -faceDetection llava:7b
PhotoMapperAI generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -faceDetection qwen3-vl
```

**Improved Detection Strategy:**
1. **First attempt:** OpenCV DNN face detector
2. **If no face:** Try Haar cascade (faster but less accurate)
3. **If no face:** Try Ollama Vision (slower but can handle edge cases)
4. **If one eye detected:** Calculate crop based on face center + offset
5. **If no eyes:** Calculate crop based on face rectangle

### 5. PortraitCropper Service
- Receives FaceLandmarks from face detection service
- Calculates optimal portrait crop area
- Handles edge cases (partial face, extreme angles)

**Crop Logic:**
```csharp
if (landmarks.BothEyesDetected)
{
    // Use eye midpoint for precise centering
    var eyeMidpoint = CalculateMidpoint(landmarks.LeftEye, landmarks.RightEye);
    cropRect = new Rect(
        eyeMidpoint.X - (faceRect.Width * 0.6),  // Width: 120% of face width
        eyeMidpoint.Y - (faceRect.Height * 0.8),  // Top: Start above eyes
        faceRect.Width * 1.2,
        faceRect.Height * 2.5                     // Height: Include upper body
    );
}
else if (landmarks.FaceRect != null)
{
    // Use face center if eyes not detected
    var faceCenter = landmarks.FaceCenter;
    cropRect = new Rect(
        faceCenter.X - (faceRect.Width * 0.5),
        faceCenter.Y - (faceRect.Height * 0.5),
        faceRect.Width * 1.5,
        faceRect.Height * 2.5
    );
}
else
{
    // Fallback: Center crop (last resort)
    cropRect = CalculateCenterCrop(imageWidth, imageHeight, portraitRatio);
}
```

### 6. ConsoleHelper
- Color-coded output for:
  - Green: Successfully mapped
  - Yellow: Low confidence (< 0.9)
  - Red: Unmatched players
  - Cyan: Unused photos
  - Magenta: Face detection issues
  - Progress bars for processing
  - Model used (for testing documentation)

## CLI Commands

```bash
# Extract
PhotoMapperAI extract \
  -inputSqlPath data/spain_players.sql \
  -connectionStringPath config/connstring.txt \
  -teamId 10 \
  -outputName SpainTeam.csv

# Map (default model)
PhotoMapperAI map \
  -inputCsvPath output/SpainTeam.csv \
  -photosDir photos/SpainTeam

# Map (specific model)
PhotoMapperAI map \
  -inputCsvPath output/SpainTeam.csv \
  -photosDir photos/SpainTeam \
  -nameModel qwen3:8b

# Map (with photo manifest)
PhotoMapperAI map \
  -inputCsvPath output/SpainTeam.csv \
  -photosDir photos/SpainTeam \
  -photoManifest data/manifest.json

# Generate Photos (default OpenCV)
PhotoMapperAI generatePhotos \
  -inputCsvPath output/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg

# Generate Photos (with Ollama Vision)
PhotoMapperAI generatePhotos \
  -inputCsvPath output/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg \
  -faceDetection llava:7b

# Benchmark mode (for testing)
PhotoMapperAI benchmark \
  -nameModels qwen2.5:7b,qwen3:8b,llava:7b \
  -faceModels opencv-dnn,yolov8-face,llava:7b \
  -testDataPath tests/samples/ \
  -outputPath benchmark-results/
```

## Testing & Benchmarking Strategy

### Test Suite Structure
```
tests/
├── PhotoMapperAI.Tests/
│   ├── Models/
│   │   ├── NameMatchingTests.cs        # Unit tests for name matching
│   │   ├── FaceDetectionTests.cs       # Unit tests for face detection
│   │   └── PortraitCropperTests.cs     # Unit tests for cropping logic
│   ├── Benchmarks/
│   │   ├── NameMatchingBenchmark.cs    # Compare models on test data
│   │   └── FaceDetectionBenchmark.cs   # Compare face detection approaches
│   └── Data/
│       ├── sample-players.csv         # Test player names
│       ├── sample-names.json          # Known matches for validation
│       └── test-photos/               # Sample photos (NOT in repo)
└── benchmark-results/
    ├── name-matching-results.md        # Table of model performances
    └── face-detection-results.md       # Table of detection success rates
```

### Benchmark Output Format

**Name Matching Results (MODEL_BENCHMARKS.md):**
```markdown
## Name Matching Model Benchmarks

Test Dataset: 50 players, 200 name comparisons
Date: 2025-02-11

| Model | Accuracy | Avg Confidence | False Positives | False Negatives | Speed (ms) | Notes |
|-------|----------|----------------|-----------------|-----------------|-------------|-------|
| qwen2.5:7b | 92% | 0.94 | 3 | 4 | 850 | Good balance |
| qwen3:8b | 95% | 0.96 | 1 | 2 | 920 | Best accuracy |
| llava:7b | 88% | 0.91 | 5 | 6 | 1100 | Slower |
| llama3.2:3b | 85% | 0.89 | 6 | 7 | 520 | Fastest |

**Winner:** qwen3:8b (best accuracy, acceptable speed)
```

**Face Detection Results (MODEL_BENCHMARKS.md):**
```markdown
## Face Detection Model Benchmarks

Test Dataset: 100 full-body player photos
Date: 2025-02-11

| Model | Face Detection | Both Eyes | One Eye | No Face | Speed (ms) | Notes |
|-------|----------------|-----------|---------|---------|-------------|-------|
| OpenCV DNN | 95% | 82% | 10% | 5% | 45 | Best overall |
| YOLOv8-Face | 97% | 88% | 7% | 3% | 65 | Highest accuracy |
| LLaVA:7b | 92% | 75% | 15% | 8% | 3500 | Slow |
| Haar Cascade | 88% | 70% | 12% | 12% | 25 | Fastest |

**Winner:** YOLOv8-Face (best accuracy, good speed)
**Fallback:** Haar Cascade (for speed when YOLOv8 not available)
```

### Test Data Generation

```csharp
public class TestDataGenerator
{
    // Generate synthetic name pairs with known match status
    public List<NamePair> GenerateNamePairs(int count);

    // Create variations of names (nicknames, different orderings, etc.)
    public List<string> CreateNameVariations(string originalName);
}
```

## Ollama Models

### Recommended Models for Testing

**Name Matching:**
```bash
ollama pull qwen2.5:7b           # Already installed
ollama pull qwen3:8b             # Newer, better reasoning
ollama pull gemma2:9b            # Good alternative
ollama pull llama3.2:3b          # Fast, less accurate
```

**Face Detection (Vision):**
```bash
ollama pull llava:7b             # General vision model
ollama pull qwen3-vl:7b         # Better vision-language
ollama pull llama3.2-vision:11b  # Latest, largest
```

### API Usage Examples

**Name Matching:**
```csharp
POST http://localhost:11434/api/generate
{
  "model": "qwen3:8b",
  "prompt": "You are a name matching assistant. Compare these two player names...\n\nPlayer A: \"Rodríguez Sánchez, Francisco Román Alarcón\"\nPlayer B: \"Rodríguez Sánchez, Isco\"\n\nReturn ONLY a number from 0 to 1 representing the probability they are the same person.",
  "stream": false,
  "options": {
    "temperature": 0.1
  }
}
```

**Face Detection (Ollama Vision):**
```csharp
POST http://localhost:11434/api/generate
{
  "model": "qwen3-vl:7b",
  "prompt": "Analyze this image of a person. Return a JSON object with:\n- face_rect: [x, y, width, height] bounding box of the face\n- left_eye: [x, y] coordinates of left eye\n- right_eye: [x, y] coordinates of right eye\n- face_center: [x, y] center point of the face\n- both_eyes_detected: true/false\n\nRespond ONLY with valid JSON, no other text.",
  "images": ["base64_encoded_image"],
  "stream": false
}
```

**Response Example:**
```json
{
  "response": "{\n  \"face_rect\": [250, 180, 120, 140],\n  \"left_eye\": [280, 220],\n  \"right_eye\": [330, 225],\n  \"face_center\": [305, 240],\n  \"both_eyes_detected\": true\n}",
  "done": true
}
```

## CSV Format

### Input (After Extract)
| UserId | FamilyName | SurName | Fifa_Player_ID | Valid_Mapping |
|--------|------------|---------|----------------|---------------|
| 1001   | Rodríguez  | Isco    | null           | null          |
| 1002   | Ramos      | Sergio  | null           | null          |

### Output (After Map)
| UserId | FamilyName | SurName | Fifa_Player_ID | Valid_Mapping |
|--------|------------|---------|----------------|---------------|
| 1001   | Rodríguez  | Isco    | 25891          | 0.95          |
| 1002   | Ramos      | Sergio  | 26401          | 1.0           |

### Photo Manifest Format (JSON)
```json
{
  "Player1_FullName.png": {
    "externalId": "12345",
    "fullName": "Messi Lionel",
    "teamId": "10"
  },
  "Player2_FullName.png": {
    "externalId": "67890",
    "fullName": "Ramos Sergio",
    "teamId": "10"
  }
}
```

## NuGet Packages

```xml
<PackageReference Include="CsvHelper" Version="30.0.1" />
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
<PackageReference Include="OpenCvSharp4" Version="4.9.0" />
<PackageReference Include="OpenCvSharp4.runtime.osx" Version="4.9.0" />
<PackageReference Include="OllamaSharp" Version="1.4.0" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.0" />
<PackageReference Include="Npgsql" Version="8.0.3" />
<PackageReference Include="McMaster.Extensions.CommandLineUtils" Version="4.1.1" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
```

**Runtime-specific packages (for deployment):**
- `OpenCvSharp4.runtime.win` - Windows
- `OpenCvSharp4.runtime.linux` - Linux
- `OpenCvSharp4.runtime.osx` - macOS (development)

## OpenCV Face Detection Improvements

### Problem with Previous Tool
- Only used basic Haar Cascade
- Missed eyes in some cases
- Failed to detect faces in extreme angles

### Solutions to Implement

1. **Multiple Model Cascade:**
   ```
   Try OpenCV DNN → Success? → Use landmarks
   Fail? → Try YOLOv8-Face → Success? → Use landmarks
   Fail? → Try Ollama Vision → Success? → Use landmarks
   Fail? → Use center crop fallback
   ```

2. **Better Haar Cascades:**
   - Try multiple cascade files
   - `haarcascade_frontalface_alt2.xml`
   - `haarcascade_frontalface_default.xml`
   - `haarcascade_profileface.xml` (side views)

3. **DNN Models:**
   - Use OpenCV's `dnn` module with pre-trained models
   - ResNet-10 SSD face detector (`res10_300x300_ssd_iter_140000.caffemodel`)
   - More accurate than Haar cascades

4. **YOLOv8-Face:**
   - State-of-the-art face detection
   - Better accuracy for challenging angles
   - Slightly slower than OpenCV DNN

## Development Phases

### Phase 1: Foundation (Complete)
- [x] Create repo and structure
- [x] Set up .NET solution and project
- [x] Implement CLI argument parsing (McMaster.CommandLineUtils)
- [x] Create base models and interfaces
- [x] Set up dependency injection (via Logic handlers)
- [x] Create Avalonia UI project structure
- [x] Implement MVVM ViewModels for 3-step wizard

### Phase 2: Extract Command (Complete)
- [x] SQL file reader
- [x] Database connection handling (SQL Server)
- [x] CSV export with placeholder columns

### Phase 3: Modular AI Services (Complete)
- [x] Create INameMatchingService interface
- [x] Implement OllamaNameMatchingService
- [x] Create IFaceDetectionService interface
- [x] Implement OpenCVFaceDetectionService (DNN)
- [x] Implement OllamaFaceDetectionService (Vision LLMs)
- [x] Create ModelFactory for service selection

### Phase 4: Map Command (Complete)
- [x] Photo directory scanner with flexible parsing
- [x] Photo manifest reader
- [x] Name matching service integration (Two-tier: String + AI)
- [x] CSV update logic
- [x] Model selection CLI parameters

### Phase 5: GeneratePhotos Command (Complete)
- [x] Face detection service integration (OpenCV + Ollama Vision)
- [x] Portrait crop calculation with fallbacks (Center Crop)
- [x] Image processing (ImageSharp)
- [x] Model selection CLI parameters

### Phase 6: Testing & Benchmarking (In Progress)
- [x] Create test data generator
- [x] Implement benchmark command
- [ ] Document findings in MODEL_BENCHMARKS.md
- [ ] Expand test datasets

### Phase 7: Polish (Complete)
- [x] Color console output
- [x] Progress indicators and spinners
- [x] Error handling and logging
- [ ] Unit tests for non-AI components
- [x] Documentation and README updates
- [x] Windows compatibility validation report documented (`WINDOWS_COMPATIBILITY_REPORT.md`)

### Phase 8: Desktop GUI (In Progress)
- [x] Create Avalonia UI project
- [x] Implement 3-step wizard (Extract → Map → Generate)
- [x] Create ViewModels for all steps
- [x] Create XAML views for all steps
- [x] Wire up all CLI parameters to UI controls
- [x] Add file browser dialogs
- [x] Add progress indicators
- [x] Implement step navigation (Back/Next/Finish)
- [x] Implement Save/Load Session feature (default app-data path)
- [x] Add diagnostic tools for model testing (model availability checks in Map/Generate steps)
- [x] Add preview functionality (Generate step source-image preview)
- [ ] Theme support (dark/light)
- [x] Export processing reports (GUI header action exports markdown report)

#### Phase 8.1: Bug Fixes & Improvements (Required)
- [x] **Fix GenerateStepViewModel result handling** - `PortraitsGenerated` and `PortraitsFailed` are populated from command result
- [x] **Implement progress reporting** - Determinate progress added for map and generate workflows
- [x] **Add cancellation support** - Map and generate support cancellation in UI and command logic
- [x] **Remove duplicate MapResult class** - UI now uses shared command-layer `MapResult`

### Phase 9: Future Enhancements (Planned)
- [ ] Web UI for non-technical users
- [ ] Batch processing for multiple teams
- [ ] Cloud LLM support (OpenAI, Anthropic)
- [ ] Additional face detection models
- [ ] Custom portrait dimensions presets
- [ ] Watermarking support
- [ ] Docker container support
- [ ] Automated testing pipeline
- [ ] Unit tests for non-AI components

## Environment Setup

### .NET SDK Installation
```bash
# macOS (Homebrew)
brew install dotnet

# Verify installation
dotnet --version

# Windows (from https://dotnet.microsoft.com/download)
# Download and install .NET 10 SDK
```

### Ollama Installation
```bash
# macOS
brew install ollama

# Start Ollama
ollama serve

# Pull models
ollama pull qwen2.5:7b
ollama pull qwen3:8b
ollama pull llava:7b
ollama pull qwen3-vl:7b
```

### OpenCV Models
- Download pre-trained models from OpenCV models zoo
- Place in `models/` folder:
  - `res10_300x300_ssd_iter_140000.caffemodel`
  - `deploy.prototxt`
  - `haarcascade_frontalface_alt2.xml`
  - YOLOv8-Face model files

## Current Status

**Implementation Snapshot (2026-02-12):**
- ✅ CLI workflow implemented (`extract`, `map`, `generatephotos`, `benchmark`)
- ✅ Avalonia GUI project implemented with 3-step wizard and navigation
- ✅ Shared AI and image services integrated into both CLI and GUI flows
- 🚧 GUI hardening pending (progress reporting, cancellation, session command wiring)

**Immediate Next Steps:**
1. Fix `GenerateStepViewModel` result assignment and progress updates.
2. Remove duplicate `MapResult` in UI and reuse shared command result model.
3. Implement Save/Load session commands in `MainWindowViewModel`.
4. Add cancellation tokens for map/generate operations in UI.
