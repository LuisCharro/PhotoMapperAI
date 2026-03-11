# PhotoMapperAI Release Notes

## v1.3.0

Released: 2026-03-11

## Highlights

- Faster GUI preview by running shared generation logic in-process instead of spawning a new CLI process for each preview.
- Manual unmapped-player repair flow in both the map view and the batch automation view.
- Platform-aware face-detection defaults:
  - macOS prefers `apple-vision`
  - Windows/Linux prefer `opencv-yunet`
- Preview crop-frame presets and auto-preview improvements in generate and batch flows.
- Better size-profile and output-profile handling in the UI generate flow.
- CI stability improvement from skipping an obsolete Apple Vision integration test that depended on local-only fixtures.

## User-Visible Improvements

### Desktop UI

- Step-by-step and batch workflows both expose the current map/generate capabilities.
- Preview generation now stays aligned with final portrait generation behavior.
- Session save/load remains available through the default app-data session file.
- Manual repair avoids direct CSV editing for common unmapped-photo cleanup.

### Mapping

- Deterministic-first matching remains the default behavior.
- AI fallback supports local, provider-prefixed, and paid model identifiers.
- Optional second-pass AI matching is available for unresolved rows.

### Portrait Generation

- Current face-detection options include `apple-vision`, `opencv-yunet`, `opencv-dnn`, `yolov8-face`, `haar-cascade`, `center`, and Ollama vision models.
- Size profiles, multi-size output, crop-frame overrides, placeholder handling, and single-player verification are all part of the current command surface.

## Release Checklist

- Finalize release artifacts.
- Re-run build, tests, and any external validation needed for the release candidate.
- Publish the release and tag `v1.3.0`.

---

## v1.0.1

Released: 2026-02-12

### Summary

`v1.0.1` introduced the desktop UI and established the initial GUI workflow on top of the existing CLI.

### Included

- Avalonia desktop application
- step-by-step Extract, Map, and Generate workflow
- early session persistence
- model diagnostics and UI wiring for core commands

For the detailed shipped change list, see [`CHANGELOG.md`](CHANGELOG.md).

---

## v1.0.0

Released: 2026-02-11

### Summary

`v1.0.0` was the first stable CLI release covering extraction, mapping, portrait generation, and supporting documentation/templates.

For the detailed shipped change list, see [`CHANGELOG.md`](CHANGELOG.md).
