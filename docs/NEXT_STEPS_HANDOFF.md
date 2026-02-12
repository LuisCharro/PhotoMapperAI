# Next Steps Handoff

Last updated: 2026-02-12

This file is the single source of truth for "what is left" for any agent continuing work.

## Commit Continuation Map (Windows Handoff)

Starting point used by cross-machine work:
- `91e6ee520d4f949a08c07048048608590d1a0e93`

Mandatory follow-up commits after that baseline:
1. `293ba5e` - fix crop regression (face-detected images must not keep full-body frame)
2. `96b4f77` - add external-data validation suite and documentation

If a Windows clone is checked out at `91e6ee5`, move to the current state with:

```powershell
git checkout feature/phase1-implementation
git pull
```

If local history is pinned and cannot pull directly:

```powershell
git cherry-pick 293ba5e
git cherry-pick 96b4f77
```

Important:
- `Validation_Run_llava_fixed` is historical and not part of the current normal workflow.
- Canonical validation run set is now: `Validation_Run`, `Validation_Run_opencv`, `Validation_Run_llava`.

## Current State

- CLI: implemented and working (`extract`, `map`, `generatephotos`, `benchmark`, `benchmark-compare`)
- GUI: implemented and working (3-step flow + diagnostics + session + reporting)
- CI: implemented (`.github/workflows/dotnet-ci.yml`) for macOS + Windows
- Ollama policy: implemented for local-only unload behavior, cloud models (`:cloud`) ignored
- Cloud provider abstraction: scaffolded (`openai:`, `anthropic:`) but API calls are not implemented yet

## Pending Work (Priority Order)

1. Run Windows benchmark and compare with macOS baseline
2. Expand face benchmark dataset (beyond current 5 labeled images)
3. Implement real OpenAI and Anthropic API integration
4. Choose and execute next product feature (batch processing, Docker, portrait presets, etc.)

## Task 1: Windows Benchmark Compare

Baseline file:
- `benchmark-results/benchmark-20260212-080146.json`

Recommended command on Windows:

```powershell
.\scripts\run-benchmark-compare.ps1 -Baseline "benchmark-results/benchmark-20260212-080146.json" -FaceModel "opencv-dnn"
```

Done criteria:
- New benchmark JSON created in `benchmark-results/`
- Comparison output captured and summarized in `docs/MODEL_BENCHMARKS.md`

## Task 2: Expand Face Benchmark Dataset

Current labels file:
- `tests/Data/FaceDetection/face_expected.csv`

Rules:
- Add more real samples (different angles, lighting, partial faces, no-face cases)
- Keep labels in `face_expected.csv` (`image_file,expected_faces`)
- Ensure paths can be resolved by benchmark loader

Done criteria:
- Dataset size significantly increased (suggested minimum: 30 images)
- Benchmark run on macOS and Windows with updated dataset
- Findings documented in `docs/MODEL_BENCHMARKS.md`

## Task 3: Real Cloud Provider Integration

Current scaffold files:
- `src/PhotoMapperAI/Services/AI/OpenAINameMatchingService.cs`
- `src/PhotoMapperAI/Services/AI/AnthropicNameMatchingService.cs`

Required implementation:
- Replace placeholder responses with real HTTP client calls
- Use `OPENAI_API_KEY` and `ANTHROPIC_API_KEY`
- Keep `INameMatchingService` contract unchanged
- Preserve fallback/error metadata behavior

Done criteria:
- `openai:<model>` and `anthropic:<model>` complete real comparisons
- Unit tests for success/failure/configuration missing scenarios
- README usage examples validated

## Quick Validation Checklist

Run after any significant change:

```bash
dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj
dotnet build src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj
```

## External Private Dataset Harness

For private real data outside this repo (not committed), use:
- Config template: `samples/external_validation.config.template.json`
- Runner: `scripts/run_external_validation.py`

Command:

```bash
python3 scripts/run_external_validation.py --config <your-local-config.json>
```

Recommendation:
- Copy template to a local file (for example `external_validation.local.config.json`) and adjust paths.
- Keep local config out of git (`*.local.config.json` is ignored).

Windows/macOS common command (run canonical suite with overwrite):

```bash
python3 scripts/run_validation_suite.py --run all
```
