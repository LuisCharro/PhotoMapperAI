# Next Steps Handoff

Last updated: 2026-02-13

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
- Cloud provider integration: implemented for `openai:` and `anthropic:` name matching

## Pending Work (Priority Order)

1. Run Windows benchmark and compare with macOS baseline
2. Expand face benchmark dataset (beyond current 5 labeled images)
3. Add additional hosted name-matching providers (priority: Cerebras, OpenRouter, Groq) with provider-specific auth/diagnostics
4. Choose and execute next product feature (batch processing, Docker, portrait presets, etc.)

## Required: Model and File Checks

Add a preflight check (CLI + GUI) to validate local dependencies before running map/generate:

- Ollama:
	- Verify the Ollama server is reachable.
	- List required models that are missing.
	- Enforce local-only model policy (stop other local models if a new one is requested, keep cloud models running).
- OpenCV DNN:
	- Verify the presence of model files used by `opencv-dnn` face detection.
	- If missing, show the exact expected paths and filenames.

This should be reported clearly to the user before the command starts.

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

## Task 3: Additional Hosted Provider Integration (Cerebras/OpenRouter/Groq Priority)

Current scaffold files:
- `src/PhotoMapperAI/Services/AI/OpenAINameMatchingService.cs`
- `src/PhotoMapperAI/Services/AI/AnthropicNameMatchingService.cs`
- `src/PhotoMapperAI/Services/AI/NameMatchingServiceFactory.cs`

Required implementation:
- Add one or more new providers with free-tier options (starting with Cerebras, OpenRouter, and Groq)
- Keep provider prefix routing via `-nameModel` (for example `cerebras:<model>`)
- Implement key handling and preflight checks (CLI + GUI memory-only overrides)
- Keep `INameMatchingService` contract unchanged
- Preserve usage metadata extraction (`usage_prompt_tokens`, `usage_completion_tokens`, `usage_total_tokens`)
- Add explicit error handling for provider quota/limit errors similar to current Ollama/OpenAI behavior

Initial Cerebras candidate models to benchmark (from local Kilo provider UI snapshot):
- `zai-glm-4.7`
- `qwen-3-235b-a22b-instruct-2507`
- `llama-3.3-70b`
- `qwen-3-32b`
- `gpt-oss-120b`

Initial OpenRouter candidate models to benchmark (free-tier options from local Kilo provider UI snapshot):
- `z-ai/glm-4.5-air:free`
- `openrouter/free`
- `stepfun/step-3.5-flash:free`
- `arcee-ai/trinity-large-preview:free`
- `arcee-ai/trinity-mini:free`
- `deepseek/deepseek-r1-0528:free`
- `google/gemma-3-12b-it:free`
- `google/gemma-3-27b-it:free`
- `google/gemma-3-4b-it:free`

Initial Groq candidate models to benchmark (free-tier focused):
- `openai/gpt-oss-20b`
- `openai/gpt-oss-120b`
- `meta-llama/llama-3.3-70b-versatile`
- `deepseek-r1-distill-llama-70b`

Done criteria:
- At least one new provider (`cerebras:<model>`, `openrouter:<model>`, or `groq:<model>`) runs end-to-end in MAP command
- Unit tests for success/failure/configuration missing scenarios
- README usage examples validated
- Benchmark note added in `docs/NAME_MAPPING_PIPELINE.md` (quality + cost snapshot)

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
