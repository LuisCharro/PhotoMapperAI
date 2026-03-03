# Overnight Implementation Workflow

## Overview

Working on `feature/phase1-implementation` branch with professional small commits implementing all 3 stages of PhotoMapperAI.

## Branch Information

**Branch:** `feature/phase1-implementation`
**Base:** Current state with all documentation updated
**Model:** zai/glm-4.7 (5 hours available) → fallback to free tiers → local Ollama → Codex 5.3

## 3 Implementation Stages

### Stage 1: Extract Data
**Commit Strategy:** Small, logical commits

1. **Commit: Create .NET solution structure**
   - `dotnet new sln -n PhotoMapperAI`
   - `dotnet new console -n PhotoMapperAI -o src/PhotoMapperAI`
   - Add project to solution
   - Add NuGet packages

2. **Commit: Define core data models**
   - `PlayerRecord.cs`
   - `PhotoMetadata.cs`
   - `MappingResult.cs`
   - `FaceLandmarks.cs`

3. **Commit: Create CLI framework**
   - Program.cs with McMaster.CommandLineUtils
   - Define subcommands: extract, map, generatePhotos, benchmark
   - Basic help/usage text

4. **Commit: Implement Extract command**
   - Read SQL file
   - Execute SQL query (SQLite/SQL Server/PostgreSQL)
   - Export to CSV
   - Test with sample data

5. **Commit: Extract command tests**
   - Unit tests for SQL reading
   - Unit tests for CSV export
   - Integration tests with synthetic data
   - Run tests and fix issues

### Stage 2: Map Photos to Players
**Commit Strategy:** Small, logical commits

6. **Commit: Name matching interface and service**
   - `INameMatchingService.cs`
   - `MatchResult.cs`
   - `OllamaNameMatchingService.cs`

7. **Commit: Ollama client wrapper**
   - `OllamaClient.cs`
   - API calls to http://localhost:11434
   - Model configuration

8. **Commit: Filename pattern parser**
   - `FilenameParser.cs`
   - Auto-detection logic
   - User template parsing
   - Photo manifest loading

9. **Commit: Map command implementation**
   - Load photos and CSV
   - Direct ID matching
   - AI name matching for unmatched
   - Update CSV with results

10. **Commit: Name matching benchmarks**
    - Test with multiple models (qwen2.5:7b, qwen3:8b, llava:7b)
    - Collect metrics: accuracy, confidence scores, speed
    - Generate benchmark report
    - Run tests and fix issues

### Stage 3: Generate Portraits
**Commit Strategy:** Small, logical commits

11. **Commit: Face detection interface**
    - `IFaceDetectionService.cs`
    - `OpenCVDNNFaceDetectionService.cs`
    - `OllamaFaceDetectionService.cs`

12. **Commit: OpenCV integration**
    - Load model files from appsettings.json
    - Face detection using DNN model
    - Eye detection using Haar cascades
    - Face/eye landmark extraction

13. **Commit: Portrait cropping logic**
    - `PortraitCropper.cs`
    - Crop strategies:
      - Both eyes → center on eyes
      - One eye → center + offset
      - No eyes → face center
      - No face → center crop
    - `IImageProcessor.cs` interface

14. **Commit: GeneratePhotos command**
    - Load mapping CSV
    - Detect faces/eyes (configurable model)
    - Calculate portrait crop
    - Generate portrait files
    - **NEW:** `-crop` parameter (Generic, AI)

15. **Commit: Face detection benchmarks**
    - Test with OpenCV DNN, Haar Cascades, Ollama Vision (llava:7b, qwen3-vl)
    - Collect metrics: accuracy, speed, face/eye detection rates
    - Generate benchmark report
    - Run tests and fix issues

16. **Commit: Final polish**
    - Color console output
    - Progress indicators
    - Error handling
    - Documentation updates

## Model Usage Strategy

### Available Models (in order of preference):

**Tier 1: Paid Model**
- ⚡ **zai/glm-4.7** (5 hours available)
  - Use for: All development tasks

**Tier 2: Free Tier Models** (when glm-4.7 exhausted)
- ⚡ **Google Antigravity** (7 models)
  - gemini-3-flash
- 🐲 **Qwen Portal** (2 models)
  - qwen-portal/coder-model
- ☁️ **Ollama Cloud** (6 models)
  - kimi-k2.5:cloud
  - glm-4.7:cloud
  - qwen3-coder:480b-cloud
  - (etc.)

**Tier 3: Local Ollama Models**
- ✅ **qwen2.5:7b-instruct** (already installed)
- ✅ **qwen3:8b** (already installed)
- ✅ **qwen3-vl** (already installed)
- ✅ **llava:7b** (already installed)

**Tier 4: Last Resort**
- 🧠 **openai-codex/gpt-5.3-codex**
  - Use only if all free tiers exhausted

### Model Selection Logic

```csharp
// Pseudo-code for model selection
if (timeRemaining(glm-4.7) > 0) {
    return glm-4.7;
} else if (hasQuota(gemini-3-flash)) {
    return gemini-3-flash;
} else if (hasQuota(qwen-portal-coder)) {
    return qwen-portal-coder;
} else if (hasQuota(ollama-cloud-models)) {
    return ollama-cloud-model;
} else if (modelAvailable("qwen2.5:7b")) {
    return "ollama://qwen2.5:7b";
} else if (modelAvailable("qwen3:8b")) {
    return "ollama://qwen3:8b";
} else {
    return "openai-codex/gpt-5.3-codex";
}
```

## Cron Job Workflow

### Cron Job Configuration

Cron jobs will automatically check progress and continue work:

**Every 15 minutes:**
- Read last commit message
- Check current stage
- Plan next step
- Continue implementation

**Cron Schedule:**
```
*/15 * * * * cd /Users/luis/.openclaw/workspace && ~/check-progress.sh
```

### Progress Tracking

Create `WORKFLOW.md` in repo root:
- Current stage
- Current task
- Last commit hash
- Next steps
- Blocked issues

### Auto-Resume Logic

```bash
# check-progress.sh
cd /Users/luis/Repos/PhotoMapperAI

# Read last commit
LAST_COMMIT=$(git log -1 --pretty=format:"%h %s")

# Read workflow status
STAGE=$(grep "Current Stage:" WORKFLOW.md | cut -d: -f2)

# Decide next action based on last commit and stage
# Continue implementation
```

## Commit Message Standards

Professional commit messages:

```
feat(extract): Add SQL file reader for database queries

fix(map): Handle empty photo directory gracefully

feat(portrait): Implement both-eyes centered cropping

test(matching): Add unit tests for name matching service

perf(benchmark): Add metrics collection for model comparison
```

Format: `[type](scope): description`
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code refactoring
- `test`: Tests
- `docs`: Documentation
- `perf`: Performance improvement
- `style`: Code style (formatting)
- `build`: Build system or dependency changes

## Test Data for Overnight Work

### Stage 1 (Extract)
- Synthetic database with Teams and Players tables
- SQLite test database in `/Users/luis/Repos/FakeData_PhotoMapperAI/DataExtraction/`
- 3 teams (small, medium, large)

### Stage 2 (Map)
- Photo names with various patterns
- 20 name pairs (15 matches, 5 non-matches)
- Test with qwen2.5:7b, qwen3:8b, llava:7b

### Stage 3 (Portraits)
- 10 photos (frontal, side view, extreme angles)
- Face detection tests with OpenCV DNN, Haar, Ollama Vision
- Portrait cropping tests

## Quality Checklist

Before moving to next stage:
- ✅ All commits pushed to branch
- ✅ Unit tests pass
- ✅ Integration tests pass
- ✅ Code follows C# conventions
- ✅ Comments in English
- ✅ No hardcoded paths (use appsettings.json)
- ✅ Error handling in place
- ✅ Benchmark metrics collected

## Emergency Stop

If something goes wrong:
1. Check `WORKFLOW.md` for current state
2. Check git log for last successful commit
3. Review changes: `git diff HEAD~1`
4. Stop cron job: `crontab -e` and comment out the line
5. Contact Luis in the morning

## Success Criteria

**In the morning, expect:**
- ✅ All 3 stages implemented
- ✅ All tests passing
- ✅ Benchmark reports generated
- ✅ Working tool that can:
  - Extract data from database
  - Map photos to players using AI
  - Generate portraits using OpenCV/AI
- ✅ Branch ready for PR to main
