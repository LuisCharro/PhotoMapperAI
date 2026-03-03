# Test Configuration Guide

## Overview

Test data is local-only and should not be committed to the repository. This ensures:

1. **Privacy:** Real player data never gets committed
2. **Flexibility:** Each developer can have their own test data structure
3. **Clean Repository:** No large test files in git history

## Setting Up Local Test Data

### Step 1: Create Test Data Directory

Choose a location outside the repository (recommended):

```bash
# Example (macOS/Linux)
mkdir -p ~/test-data/PhotoMapperAI
mkdir -p ~/test-data/PhotoMapperAI/{DataExtraction,NameMatch,FaceRecognition,PortraitExtraction}
```

### Step 2: Create Test Config

1. Copy the template:
```bash
cp test-config.template.json test-config.local.json
```

2. Update paths in `test-config.local.json`:
```json
{
  "testData": {
    "rootPath": "/Users/yourname/test-data/PhotoMapperAI",
    "dataExtraction": {
      "sqlQueries": "sql_queries/",
      ...
    }
  }
}
```

### Step 3: Add to .gitignore

The `.gitignore` already includes:
- `test-configs/` - Directory for local configs
- `*.test-config.json` - Config files with ".test-config" suffix
- `local-test-settings.json` - Local settings files
- `benchmark-results/*.md` - Benchmark reports (may contain internal metrics)

## Test Data Structure

### 1. DataExtraction

```
DataExtraction/
├── sql_queries/
│   ├── get_players.sql
│   └── get_teams.sql
├── database.db            # SQLite test database
└── expected_output/
    └── team_a_players.csv
```

**Purpose:** Test database extraction to CSV.

**Content:**
- Simple database with Teams and Players tables
- 3-25 players per team
- Various database types (SQLite, SQL Server, PostgreSQL)

### 2. NameMatch

```
NameMatch/
├── photo_names/           # Photo files (0 bytes, placeholders)
│   ├── 12345_Rodriguez_Isco.png
│   ├── 25891_Messi_Lionel.png
│   └── ...
├── photo_manifest.json    # Alternative metadata source
├── test_cases.csv         # Name matching test cases
└── expected_output/
    └── mapping_results.csv
```

**Purpose:** Test filename pattern extraction and name matching.

**Content:**
- Photo files with various naming patterns
- Photo manifest JSON (for non-standard filenames)
- Test cases with expected match results
- 20-50 name pairs (matches and non-matches)

### 3. FaceRecognition

```
FaceRecognition/
├── players.csv           # Player data (3+ members)
├── photos/               # Real photos (head to knees)
│   ├── player1.jpg
│   ├── player2.jpg
│   └── ...
├── expected_face_detections.json
└── expected_output/
    └── detection_results.csv
```

**Purpose:** Test face and eye detection.

**Content:**
- Player CSV with metadata
- Real photos (not necessarily football players)
- Expected detection results (face rect, eye positions, model used)
- Various scenarios: frontal, side view, extreme angles, lighting

### 4. PortraitExtraction

```
PortraitExtraction/
├── portrait_inputs.csv   # Face detection results from FaceRecognition
├── photos/              # Reuse from FaceRecognition
└── expected_output/
    ├── 1_portrait.jpg
    ├── 2_portrait.jpg
    └── ...
```

**Purpose:** Test portrait cropping from detected faces.

**Content:**
- CSV with face detection results
- Photos from FaceRecognition step
- Expected portrait crops

## Running Tests

### With Test Config

```bash
# Run tests using local config
dotnet test -- TestConfig=test-config.local.json

# Run benchmarks
dotnet run -- benchmark -config test-config.local.json
```

### Manual Testing

```bash
# Extract command
PhotoMapperAI extract \
  -inputSqlPath ~/test-data/PhotoMapperAI/DataExtraction/sql_queries/get_players.sql \
  -connectionStringPath ~/test-data/PhotoMapperAI/connection.txt \
  -teamId 1 \
  -outputName team_a.csv

# Map command
PhotoMapperAI map \
  -inputCsvPath ~/test-data/PhotoMapperAI/DataExtraction/expected_output/team_a.csv \
  -photosDir ~/test-data/PhotoMapperAI/NameMatch/photo_names

# Generate photos command
PhotoMapperAI generatePhotos \
  -inputCsvPath ~/test-data/PhotoMapperAI/FaceRecognition/players.csv \
  -processedPhotosOutputPath ~/test-data/PhotoMapperAI/PortraitExtraction/portraits \
  -format jpg \
  -faceDetection opencv-dnn
```

## Important Notes

### Do NOT Commit

❌ Real player data
❌ Personal database connection strings
❌ Test photos containing real people
❌ Benchmark results with internal metrics

### OK to Commit

✅ Test SQL query templates (synthetic data only)
✅ Sample CSV templates (no real data)
✅ Test config templates (without actual paths)
✅ Expected output templates (synthetic data)

### Photo Files

For filename extraction testing: 0-byte placeholder files are fine.

For face detection testing: Use publicly available stock photos from:
- Unsplash (https://unsplash.com)
- Pexels (https://pexels.com)
- Pixabay (https://pixabay.com)

## Sample Test Data Generation

See `docs/TESTING_STRATEGY.md` for:
- Test case generation guidelines
- Benchmark methodology
- Expected result formats
- Model comparison framework
