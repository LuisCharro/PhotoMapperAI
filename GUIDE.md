# PhotoMapperAI - Desktop GUI Application

Cross-platform desktop application (Avalonia UI) for managing photo mapping workflows with a friendly step-by-step interface.

## Overview

The PhotoMapperAI GUI provides a visual interface for all CLI commands with session management capabilities. Users can work through the 3-step workflow and save their progress to continue later.

## Features

- **Step-by-step wizard interface** - Extract → Map → Generate
- **Session persistence** - Save/Load sessions to continue work later
- **Visual feedback** - Real-time status updates and progress indicators
- **All CLI parameters** - Complete access to all command options
- **Cross-platform** - Windows, macOS, Linux support

## Getting Started

### Running the GUI

```bash
# From the PhotoMapperAI directory
dotnet run --project src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
```

Or build and run the executable:

```bash
# Build
dotnet build src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj

# Run
dotnet src/PhotoMapperAI.UI/bin/Debug/net10.0/PhotoMapperAI.UI.dll
```

### Prerequisites

- .NET 10 SDK (or .NET 10 Runtime for running the built executable)
- Ollama running (for AI features)
- OpenCV models downloaded (optional, for traditional face detection)

## Workflow

### Step 1: Extract Player Data

Extract player data from your database to CSV format.

**Parameters:**

| Field | Description | Required | Default |
|-------|-------------|------------|---------|
| SQL File Path | Path to SQL query file | Yes | - |
| Connection String Path | Path to file containing database connection string | Yes | - |
| Team ID | Team identifier for filtering | Yes | 0 |
| Output Filename | Name of output CSV file | No | players.csv |
| Database Type | Database system type | No | SqlServer |

**Database Types:**
- SqlServer
- MySQL
- PostgreSQL
- SQLite

**Actions:**
1. Browse to select SQL file
2. Browse to select connection string file
3. Enter Team ID
4. Optionally customize output filename and database type
5. Click "Extract" to generate CSV
6. View progress in status area

**Output:**
- CSV file with columns: `UserId, FamilyName, SurName, Fifa_Player_ID, Valid_Mapping`
- Player count displayed after extraction

### Step 2: Map Photos to Players

Map external photos to internal player records using AI-powered name matching.

**Parameters:**

| Field | Description | Required | Default |
|-------|-------------|------------|---------|
| Input CSV Path | Path to player CSV file (from Step 1) | Yes | - |
| Photos Directory | Directory containing player photos | Yes | - |
| Filename Pattern | Template for extracting IDs from filenames | No | Auto-detect |
| Use Photo Manifest | Toggle to use manifest file instead of pattern | No | false |
| Photo Manifest Path | Path to JSON manifest file (if enabled) | No | - |
| Name Model | Ollama model for AI name matching | No | qwen2.5:7b |
| Confidence Threshold | Minimum confidence for valid match (0-1) | No | 0.9 |

**Name Models:**
- `qwen2.5:7b` - Good balance of speed and accuracy
- `qwen3:8b` - Best accuracy (recommended)
- `llava:7b` - Can handle text+vision tasks

**Filename Pattern Syntax:**
```
{id}_{family}_{sur}.png  - Standard FIFA format
{sur}-{family}-{id}.jpg   - Alternative format
{family}, {sur} - {id}.png - Custom separator
```

**Photo Manifest Format (JSON):**
```json
{
  "photo.png": {
    "externalId": "12345",
    "fullName": "Messi Lionel",
    "teamId": "10"
  }
}
```

**Actions:**
1. Browse to select CSV file (from Step 1)
2. Browse to select photos directory
3. Optionally specify filename pattern OR enable photo manifest
4. If using manifest, browse to select JSON file
5. Select name matching model
6. Adjust confidence threshold if needed
7. Click "Map" to match photos to players
8. View progress: "Processing: player 5/49"

**Output:**
- Updated CSV with `Fifa_Player_ID` and `Valid_Mapping` columns filled
- Statistics: players processed, players matched, match rate

### Step 3: Generate Portraits

Generate portrait photos from full-body images using face detection.

**Parameters:**

| Field | Description | Required | Default |
|-------|-------------|------------|---------|
| Input CSV Path | Path to mapped CSV file (from Step 2) | Yes | - |
| Photos Directory | Directory containing source photos | Yes | - |
| Output Directory | Path for generated portraits | Yes | - |
| Image Format | Output image format | No | jpg |
| Face Detection Model | Model for detecting faces | No | llava:7b,qwen3-vl |
| Portrait Width | Output portrait width in pixels | No | 200 |
| Portrait Height | Output portrait height in pixels | No | 300 |
| Portrait Only | Skip face detection, use existing detections | No | false |

**Face Detection Models:**
- `llava:7b,qwen3-vl` - Ollama Vision with fallback (recommended)
- `llava:7b` - LLaVA 7B vision model
- `qwen3-vl` - Qwen3-VL vision model
- `opencv-dnn` - OpenCV DNN neural network (fast)
- `yolov8-face` - YOLOv8 face detection (best accuracy)
- `haar-cascade` - OpenCV Haar Cascade (fastest, may have issues on macOS)
- `center` - No AI, upper-body crop from top 22%

**Actions:**
1. Browse to select CSV file (from Step 2)
2. Browse to select source photos directory
3. Browse to select output directory for portraits
4. Select image format (jpg/png)
5. Choose face detection model from dropdown
6. Optionally customize portrait dimensions
7. Optionally enable portrait-only mode to reuse detections
8. Click "Generate" to create portraits
9. View progress bar and status updates

**Output:**
- Portrait images named by internal PlayerId
- Statistics: portraits generated, portraits failed
- Progress indicator shows completion percentage

## Session Management

### Save Session

Save your current progress to continue later.

**Status:** 🚧 **Under Development** (feature placeholder exists, implementation pending)

**Planned Features:**
- Save all step configurations to JSON file
- Save step completion status
- Save file paths and parameter values
- Save processing results (player counts, statistics)

**Usage:**
1. Click "💾 Save Session" in header bar
2. Choose session file location
3. Session saved as `.session.json`

### Load Session

Restore a previously saved session.

**Status:** 🚧 **Under Development** (feature placeholder exists, implementation pending)

**Planned Features:**
- Load all configurations from session file
- Restore step completion status
- Restore file paths and parameter values
- Display saved processing results

**Usage:**
1. Click "📂 Load Session" in header bar
2. Select previously saved `.session.json` file
3. All configurations and progress restored

## Navigation

### Step Progress Indicator

The blue bar at the top shows the 3-step workflow:

- **Step 1 (Extract)** - Green when active or completed
- **Step 2 (Map)** - Green when active or completed
- **Step 3 (Generate)** - Green when active or completed

### Back/Next/Finish Buttons

- **◀ Back** - Navigate to previous step (disabled on Step 1)
- **Next ▶** - Navigate to next step (enabled only when current step is complete)
- **✓ Finish** - Complete workflow and display summary (available on Step 3)

## Tips & Best Practices

### Step 1 Tips
- Use generic connection string file for security (don't hardcode passwords)
- Test SQL query in database tool first to ensure it works
- Use descriptive output filenames (e.g., `spain_team.csv`, `switzerland_team.csv`)

### Step 2 Tips
- Start with auto-detect for filename patterns (it works for most cases)
- Use photo manifest for complex naming conventions
- Lower confidence threshold (0.7-0.8) if too many unmatched players
- Use `qwen3:8b` for best name matching accuracy

### Step 3 Tips
- Use `llava:7b,qwen3-vl` fallback for best results
- For speed: use `center` mode (no AI) if photos are well-framed
- For accuracy: use `yolov8-face` if OpenCV models are available
- Test with a few photos first before processing full dataset

## Troubleshooting

### Common Issues

**"Please select CSV file" error**
- Ensure you've browsed and selected the file (don't just type the path)
- Click the browse button next to the field

**"No photos found"**
- Check that photos directory is correct
- Ensure photos have supported formats (.png, .jpg, .jpeg, .bmp)
- Verify directory permissions

**"Model not initialized" error**
- Ensure Ollama is running: `ollama serve`
- Verify models are pulled: `ollama list`
- For OpenCV models, check files are in correct location

**"0 players matched"**
- Check filename pattern matches actual photo filenames
- Try lowering confidence threshold
- Use photo manifest if pattern detection fails

## Technical Details

### Technology Stack

- **.NET 10** - Application runtime
- **Avalonia UI** - Cross-platform desktop UI framework
- **CommunityToolkit.Mvvm** - MVVM framework for data binding
- **PhotoMapperAI Core** - Business logic shared with CLI

### Architecture

```
PhotoMapperAI.UI/
├── ViewModels/              # MVVM ViewModels
│   ├── MainWindowViewModel.cs    # Main window logic
│   ├── ExtractStepViewModel.cs   # Step 1: Extract data
│   ├── MapStepViewModel.cs       # Step 2: Map photos
│   ├── GenerateStepViewModel.cs   # Step 3: Generate portraits
│   └── ViewModelBase.cs        # Base ViewModel class
├── Views/                  # Avalonia XAML views
│   ├── MainWindow.axaml          # Main window
│   ├── ExtractStepView.axaml     # Step 1 UI
│   ├── MapStepView.axaml         # Step 2 UI
│   └── GenerateStepView.axaml   # Step 3 UI
├── App.axaml              # Application resources
├── Program.cs             # Entry point
└── ViewLocator.cs         # ViewModel-to-View mapping
```

### Future Enhancements

- [ ] Implement Save/Load Session feature
- [ ] Add batch processing for multiple teams
- [ ] Add diagnostic tools for model testing
- [ ] Support custom portrait size presets
- [ ] Add preview images for each step
- [ ] Dark/light theme toggle
- [ ] Export processing reports

## Known Issues & TODOs

### Code Issues (from review)

1. **GenerateStepViewModel.cs:131** - Result not properly used:
   ```csharp
   // Current (bug):
   ProcessingStatus = $"✓ Generated {PortraitsGenerated} portraits ({PortraitsFailed} failed)";
   
   // Should be:
   PortraitsGenerated = result.PortraitsGenerated;
   PortraitsFailed = result.PortraitsFailed;
   ProcessingStatus = $"✓ Generated {PortraitsGenerated} portraits ({PortraitsFailed} failed)";
   ```

2. **MapStepViewModel.cs:127-131** - Duplicate `MapResult` class:
   - This class duplicates the `MapResult` already defined in `PhotoMapperAI/Commands/MapCommand.cs`
   - Should use the shared model instead

3. **Progress reporting** - `Progress` property in GenerateStepViewModel is never updated during processing. Consider using `IProgress<T>` or events from the command logic.

4. **Cancellation support** - Long-running operations should support cancellation via `CancellationToken`.

5. **Session UX mismatch** - `SessionState` model exists, but Save/Load commands in `MainWindowViewModel` are still TODO placeholders. Keep labels and status copy explicit until behavior is wired.

### Architecture Suggestions

1. **Dependency Injection** - ViewModels currently create services directly. Consider using DI container for better testability.

2. **Service Locator Pattern** - The `CreateFaceDetectionService` factory method in GenerateStepViewModel should be moved to a dedicated service factory.

3. **Error Handling** - Consider adding a global error handler and user-friendly error messages instead of showing raw exception messages.

## Comparison with CLI

| Feature | GUI | CLI |
|---------|------|-----|
| All parameters exposed | ✅ Yes | ✅ Yes |
| Step-by-step workflow | ✅ Yes | ❌ No |
| Session persistence | 🚧 Partial | ❌ No |
| Visual progress | ✅ Yes | ⚠️ Text-based |
| Batch scripting | ❌ No | ✅ Yes |
| Automation | ❌ No | ✅ Yes |
| Beginner-friendly | ✅ Yes | ⚠️ Requires CLI knowledge |

## License

Same as PhotoMapperAI CLI - TBD

## Contributing

Contributions to the GUI are welcome! Areas for improvement:
- Session save/load implementation
- Additional UI polish
- Theme support
- Preview functionality
- Diagnostic tools

## Related Documentation

- [`README.md`](README.md) - Main README with CLI documentation
- [`PROJECT_PLAN.md`](PROJECT_PLAN.md) - Implementation plan and phases
- [`docs/FACE_DETECTION_GUIDE.md`](docs/FACE_DETECTION_GUIDE.md) - Face detection details
- [`docs/EDGE_CASES.md`](docs/EDGE_CASES.md) - Troubleshooting edge cases
