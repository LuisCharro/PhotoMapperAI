# PhotoMapperAI Desktop UI Guide

## Overview

The desktop app wraps the CLI/core services in an Avalonia UI. It supports:

- step-by-step Extract, Map, and Generate workflow
- batch automation for multi-team runs
- preview generation and crop refinement
- session save/load through the default app-data session file
- manual unmapped-player repair from the UI

Run it with:

```bash
dotnet run --project src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj
```

## Main Modes

### Step-by-step mode

Three screens:

1. Extract
2. Map
3. Generate

The main window propagates outputs forward when possible:

- a successful extract can prefill the map CSV path
- a successful map can prefill the generate CSV path and photo directory

### Batch mode

Batch mode combines extract, map, generate, preview, and team selection into one screen for multi-team processing.

## Extract View

The extract step mirrors the CLI `extract` command.

Primary inputs:

- SQL file path
- connection string path
- team ID
- output CSV name

Team extraction is supported in the CLI via `--extractTeams`; the step-by-step UI is primarily oriented around player extraction, while batch mode handles team list loading separately.

## Map View

The map step mirrors the CLI `map` command.

Key capabilities:

- photo directory selection
- optional filename pattern
- optional photo manifest
- provider-aware name model selection
- AI fallback toggles:
  - `Use AI`
  - `AI only`
  - `AI second pass`
  - `AI trace`
- manual unmapped-player repair dialog

Model tiers currently exposed in the UI:

- free-tier/cloud candidates
- local Ollama models
- paid providers from config

Current default threshold in the UI is `0.65`. The CLI still enforces a minimum threshold of `0.8`, so values below that are raised during command execution.

## Generate View

The generate step mirrors the CLI `generatephotos` command and adds preview tooling.

Key capabilities:

- face-detection model selection
- size profile support
- all-sizes generation
- output profile alias selection: `none`, `test`, `prod`
- portrait-only mode
- placeholder image support
- single-player verification through `Only Player`
- crop-offset controls
- preview generation
- custom preview crop-frame presets
- auto-preview

Platform defaults:

- macOS: `apple-vision`
- Windows/Linux: `opencv-yunet`

Current model groups in the UI:

- Recommended
- Local Vision
- Advanced
- Paid models placeholder/config-driven list

## Preview Behavior

Preview generation now runs the shared generation logic in-process rather than launching a fresh CLI process.

Implications:

- faster repeated previews
- better parity between preview output and final generated output
- shared cache reuse where supported

Preview crop-frame presets are persisted through local UI settings.

## Batch Automation

Batch mode is the most feature-rich workflow.

It supports:

- loading teams from database or CSV
- per-team selection
- team photo-directory validation
- mapping configuration
- generate configuration
- preview and refinement before running the full batch
- persisted batch session JSON alongside output

Useful batch settings:

- `Use AI`
- `AI only`
- `AI second pass`
- `Download missing OpenCV DNN files`
- size profile path
- generate all sizes
- crop offsets
- preview crop frame

## Session Handling

The UI save/load actions use a default app-data location instead of asking for an arbitrary file path.

That keeps the flow simple, but it also means:

- there is one default session location per user/profile
- explicit file-picker based session export/import is not implemented

## Known Limitations

- The UI is a wrapper over the same core logic, so missing models/config files still fail the same preflight checks as the CLI.
- Some model lists are config-driven and may differ from static documentation examples.
- Dated planning docs in `docs/planning/` may describe older UI states.

## Related Docs

- [`../../README.md`](../../README.md)
- [`NAME_MAPPING_PIPELINE.md`](NAME_MAPPING_PIPELINE.md)
- [`FACE_DETECTION_GUIDE.md`](FACE_DETECTION_GUIDE.md)
- [`../testing/TEST_CONFIGURATION.md`](../testing/TEST_CONFIGURATION.md)
