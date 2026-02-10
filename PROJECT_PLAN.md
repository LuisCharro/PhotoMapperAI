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
│       ├── Services/            # Core business logic
│       │   ├── DatabaseExtractor.cs
│       │   ├── PhotoMatcher.cs
│       │   ├── NameMatchingService.cs (Ollama LLM)
│       │   └── PortraitCropper.cs (Ollama Vision)
│       ├── Models/              # Data models
│       │   ├── PlayerRecord.cs
│       │   ├── PhotoMetadata.cs
│       │   └── MappingResult.cs
│       ├── Utils/               # Helpers
│       │   ├── CsvHelper.cs
│       │   ├── ConsoleHelper.cs
│       │   └── OllamaClient.cs
│       └── Program.cs           # Entry point
├── data/                        # Sample data (gitignored)
└── tests/                       # Unit tests (later)
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
- Scans photo directory for `*.png` files
- Parses filename format: `{Fifa_Player_ID}_{FamilyName}_{SurName}.png`
- Creates dictionary: `key=Fifa_ID, value=PhotoMetadata`

**Mapping Logic:**
1. **Direct match**: CSV has Fifa_Player_ID already → skip
2. **Fuzzy match**: Use Ollama LLM to compare CSV names with photo names
3. **Confidence threshold**: Accept match only if >= 0.9

### 3. NameMatchingService (Ollama LLM)
- Connects to local Ollama instance (`http://localhost:11434`)
- Uses model: `qwen2.5:7b` or similar
- **Prompt strategy**: "Are these the same person? Player A: [name from CSV], Player B: [name from photo]. Respond with probability 0-1."

### 4. PortraitCropper Service (Ollama Vision)
- Reads full-body photo
- Uses Qwen Vision model (`qwen2.5-vision`)
- Detects face landmarks (eyes, head position)
- Calculates crop area based on eye coordinates
- Outputs cropped portrait

**Crop Logic (AI-based):**
- Find both eyes → calculate midpoint
- Crop area: from forehead to chest, centered on eye midpoint
- Output format: configurable (JPG/PNG)

### 5. ConsoleHelper
- Color-coded output for:
  - Green: Successfully mapped
  - Yellow: Low confidence (< 0.9)
  - Red: Unmatched players
  - Cyan: Unused photos
  - Progress bars for processing

## CLI Commands

```bash
# Extract
PhotoMapperAI extract \
  -inputSqlPath data/spain_players.sql \
  -connectionStringPath config/connstring.txt \
  -teamId 10 \
  -outputName SpainTeam.csv

# Map
PhotoMapperAI map \
  -inputCsvPath output/SpainTeam.csv \
  -photosDir photos/SpainTeam

# Generate Photos
PhotoMapperAI generatePhotos \
  -inputCsvPath output/SpainTeam.csv \
  -processedPhotosOutputPath portraits/SpainTeam \
  -format jpg
```

## Ollama Integration

### Setup
```bash
# Install Ollama from https://ollama.ai
# Pull required models
ollama pull qwen2.5:7b           # Name matching
ollama pull qwen2.5-vision      # Face detection
```

### API Usage

**Name Matching:**
```csharp
POST http://localhost:11434/api/generate
{
  "model": "qwen2.5:7b",
  "prompt": "Are these the same person? Player A: 'Rodríguez Sánchez, Isco', Player B: 'Rodríguez Sánchez, Francisco Román Alarcón'. Return probability 0-1.",
  "stream": false
}
```

**Face Detection:**
```csharp
POST http://localhost:11434/api/generate
{
  "model": "qwen2.5-vision",
  "prompt": "Detect face and eye positions. Return JSON with coordinates for left_eye, right_eye, face_center.",
  "images": ["base64_encoded_image"],
  "stream": false
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

## Next Steps

### Phase 1: Foundation (Current)
- [x] Create repo and structure
- [ ] Set up .NET solution and project
- [ ] Implement CLI argument parsing (System.CommandLine or McMaster.CommandLineUtils)
- [ ] Create base models (PlayerRecord, PhotoMetadata)

### Phase 2: Extract Command
- [ ] SQL file reader
- [ ] Database connection handling
- [ ] CSV export with placeholder columns

### Phase 3: Map Command
- [ ] Photo directory scanner
- [ ] Filename parser
- [ ] Ollama LLM client
- [ ] Name matching service with confidence scoring
- [ ] CSV update logic

### Phase 4: GeneratePhotos Command
- [ ] Ollama Vision client
- [ ] Face/eye detection
- [ ] Portrait crop calculation
- [ ] Image processing (ImageSharp or SixLabors.ImageSharp)

### Phase 5: Polish
- [ ] Color console output
- [ ] Progress indicators
- [ ] Error handling and logging
- [ ] Unit tests

## Libraries to Consider

- **CLI**: `System.CommandLine` (Microsoft, recommended)
- **CSV**: `CsvHelper` (JoshClose)
- **Image Processing**: `SixLabors.ImageSharp`
- **Ollama**: `OllamaSharp` or custom HTTP client
- **Database**: `Microsoft.Data.SqlClient` or `Npgsql` (PostgreSQL)

## Environment Setup Note

Currently, .NET SDK is not installed on the Mac. Options:

1. **Install .NET SDK on Mac**:
   ```bash
   brew install dotnet
   ```

2. **Work on Windows machine** (if preferred):
   - Copy repo from GitHub
   - Work in Visual Studio

3. **Plan architecture first** (current approach):
   - Define all components and interfaces
   - Create project files when ready

For now, we'll plan the architecture in detail, then decide when to create the actual .NET project.
