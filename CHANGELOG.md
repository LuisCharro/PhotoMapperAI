# Changelog

All notable changes to PhotoMapperAI are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-03-11

### Added

- Manual unmapped-player mapping dialog for both map and batch flows.
- Apple Vision face-detection support for macOS environments.
- Preview crop-frame presets and auto-preview support in generate and batch views.
- Batch automation improvements for multi-team processing and persisted batch session state.
- Filename pattern preset management in the UI.
- Expanded model tier lists for local, free-tier, and paid mapping providers.

### Changed

- GUI preview now uses the shared generation logic in-process for better parity and lower latency.
- UI generate and batch flows now resolve size profiles and output profiles more consistently.
- Platform defaults for face detection are now platform-aware:
  - macOS: `apple-vision`
  - Windows/Linux: `opencv-yunet`

### Fixed

- Multiple GUI progress, cancellation, and result-reporting issues in map and generate flows.
- Several manual-mapping dialog build/wiring issues.
- Windows face-detection default selection in the UI.
- Obsolete Apple Vision CI integration test is now skipped.

[1.3.0]: https://github.com/LuisCharro/PhotoMapperAI/releases/tag/v1.3.0
## [1.0.1] - 2026-02-12

### Added

- Avalonia desktop UI for Extract, Map, and Generate workflows.
- Early session persistence and model diagnostics in the UI.
- Updated documentation for the GUI entry point and workflow.

### Fixed

- Initial layout and result-model issues in the first GUI release.

## [1.0.0] - 2026-02-11

### Added

- Initial stable CLI release with:
  - `extract`
  - `map`
  - `generatephotos`
  - `benchmark`
- SQL-driven database extraction into CSV.
- Deterministic and AI-assisted name mapping.
- Portrait generation with multiple face-detection strategies.
- Sample SQL/templates and supporting documentation.

[1.0.1]: https://github.com/LuisCharro/PhotoMapperAI/releases/tag/v1.0.1
[1.0.0]: https://github.com/LuisCharro/PhotoMapperAI/releases/tag/v1.0.0
