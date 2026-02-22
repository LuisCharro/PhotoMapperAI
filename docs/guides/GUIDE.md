# PhotoMapperAI - Desktop GUI Application

Cross-platform desktop application (Avalonia UI) for managing photo mapping workflows with a friendly step-by-step interface.

## Overview

The PhotoMapperAI GUI provides a visual interface for all CLI commands with session management capabilities. Users can work through the 3-step workflow and save their progress to continue later.

## Features

- **Step-by-step wizard interface** - Extract → Map → Generate
- **Batch Automation view** - Process multiple teams in one run
- **Session persistence** - Save/Load sessions to continue work later
- **Visual feedback** - Real-time status updates and progress indicators
- **All CLI parameters** - Complete access to all command options
- **AI model management** - Refresh and check model availability
- **Filename pattern presets** - Save and reuse patterns
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
{first}_{last}_{id}.jpg    - FirstName_LastName_ID (FIFA/Euro format)
{id}_{first}_{last}.png    - ID_FirstName_LastName
{first}-{last}-{id}.jpg    - FirstName-LastName-ID (dash separated)
{id}.jpg                   - ID only
```

**Pattern Presets:**
The GUI includes a preset system for saving and reusing patterns:
- Select from dropdown to use a saved pattern
- Edit the pattern and click "Save Preset" to update
- Click "New" to create a new preset with current pattern
- Presets are stored in `appsettings.local.json`

**Legacy Placeholders:** `{sur}` (first name) and `{family}` (last name) are still supported for backward compatibility.

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
| Face Detection Model | Model for detecting faces | No | llava:7b |
| Portrait Width | Output portrait width in pixels | No | 200 |
| Portrait Height | Output portrait height in pixels | No | 300 |
| Size Profile Path | Optional JSON profile for legacy-compatible dimensions | No | - |
| Generate All Profile Sizes | Generate all variants from profile into subfolders | No | false |
| Output Profile | Optional alias for output root (`none`/`test`/`prod`) | No | none |
| Portrait Only | Skip face detection, use existing detections | No | false |

**Face Detection Models:**
- `llava:7b` - LLaVA 7B vision model (recommended default)
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
6. Optional: select a size profile JSON for legacy-compatible dimensions
7. Optional: enable "Generate all profile sizes" to create all variants
8. Optional: choose output profile (`none`/`test`/`prod`)
9. If no size profile is selected, customize portrait dimensions manually
10. Optionally enable portrait-only mode to reuse detections
11. Click "Generate" to create portraits
12. View progress bar and status updates

**Output:**
- Portrait images named by internal PlayerId
- Statistics: portraits generated, portraits failed
- Progress indicator shows completion percentage

## Batch Automation View

The Batch Automation view provides a streamlined interface for processing multiple teams in one run. It combines all three steps (Extract, Map, Generate) into a single workflow.

### Accessing Batch Automation

Click the "Batch Automation" tab in the main navigation.

### Configuration Sections

#### 1. Database Configuration
| Field | Description | Required |
|-------|-------------|----------|
| Connection String | Database connection string | Yes |
| Teams SQL File | SQL query to get team list | Yes |
| Players SQL File | SQL query to get players per team | Yes |

#### 2. Paths Configuration
| Field | Description | Required |
|-------|-------------|----------|
| CSV Output Directory | Where to store intermediate CSV files | Yes |
| Photos Directory | Root directory containing team photo folders | Yes |
| Output Directory | Where to store generated portraits | Yes |
| Team Subdirectories | Check if photos are in team-named subfolders | - |

#### 3. Processing Settings

**Name Matching:**
- Enable AI for unmatched players
- AI-only mode (skip deterministic matching)
- AI Second Pass for remaining unmatched
- Model selection via tabs (Free Tier, Local, Paid)
- Refresh Models / Check Model buttons
- Confidence threshold slider (0.80 - 1.0)

**Face Detection:**
- Model selection via tabs (Recommended, Local Vision, Advanced)
- Download missing OpenCV DNN files option

**Output Size:**
- Size profile JSON (optional)
- Manual width/height dimensions

**Crop Offset (Optional):**
- Horizontal/vertical offset sliders
- Preset selection

**Photo Filename Parsing:**
- Pattern presets dropdown
- Custom pattern input
- Save/New preset buttons

#### 4. Team List

**Loading Teams:**
- Load from Database - Uses SQL query
- Load from CSV - Import existing team list
- Save to CSV - Export team list
- Refresh Status - Re-validate photo directories
- Clear - Remove all teams

**Managing Teams:**
- Select All / Deselect All buttons
- Check/uncheck individual teams in the "Run" column
- Teams without photo directories are flagged

#### 5. Execute

**Actions:**
1. Configure all settings
2. Load teams from database or CSV
3. Verify teams have photo directories (click "Refresh Status")
4. Click "Start Batch Processing"
5. Monitor progress and logs

**Progress Display:**
- Overall progress bar
- Teams completed/failed/skipped counts
- Per-team status in grid
- Execution log (collapsible)

### Batch Session State

Batch runs automatically save session state to:
```
<OutputDirectory>/batch_session_<timestamp>.json
```

This includes:
- All configuration settings
- Per-team results (players extracted, mapped, photos generated)
- Error messages for failed teams
- Timestamps

## Session Management

### Save Session

Save your current progress to continue later.

**Status:** ✅ **Implemented** (saved to default app-data location)

**Current Behavior:**
- Saves all step configurations to JSON
- Saves step completion status
- Saves key processing results (counts/statistics)
- Default path:
  - macOS: `~/Library/Application Support/PhotoMapperAI/session.json`
  - Windows: `%AppData%\\PhotoMapperAI\\session.json`

**Usage:**
1. Click "💾 Save Session" in header bar
2. Session is saved to default app-data location

### Load Session

Restore a previously saved session.

**Status:** ✅ **Implemented** (loads from default app-data location)

**Current Behavior:**
- Loads all saved configurations from JSON
- Restores step completion status
- Restores file paths, parameters, and saved counters

**Usage:**
1. Click "📂 Load Session" in header bar
2. App loads the session from default app-data location
3. All configurations and progress are restored

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
- Start with `llava:7b` (stable default)
- If you need legacy outputs, use a size profile JSON
- Use "Generate all profile sizes" only when you need all variants
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
│   ├── GenerateStepViewModel.cs  # Step 3: Generate portraits
│   ├── BatchAutomationViewModel.cs # Batch processing
│   └── ViewModelBase.cs          # Base ViewModel class
├── Views/                   # Avalonia XAML views
│   ├── MainWindow.axaml          # Main window
│   ├── ExtractStepView.axaml     # Step 1 UI
│   ├── MapStepView.axaml         # Step 2 UI
│   ├── GenerateStepView.axaml    # Step 3 UI
│   └── BatchAutomationView.axaml # Batch processing UI
├── Models/                  # UI-specific models
│   └── BatchTeamItem.cs          # Team item for batch view
├── App.axaml               # Application resources
├── Program.cs              # Entry point
└── ViewLocator.cs          # ViewModel-to-View mapping
```

### Future Enhancements

- [x] Implement Save/Load Session feature (default path)
- [x] Add batch processing for multiple teams (BatchAutomationView)
- [x] Add diagnostic tools for model testing (Refresh/Check Model buttons)
- [x] Support custom filename pattern presets
- [ ] Add preview images for each step
- [ ] Dark/light theme toggle
- [ ] Export processing reports

## Known Issues & TODOs

### Code Issues (from review)

1. **Session file picker UX** - Save/Load uses default app-data path only. Add file picker if explicit path control is required.
2. **Dependency wiring** - ViewModels still construct services directly instead of using DI container.

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
