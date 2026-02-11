# Changelog

All notable changes to PhotoMapperAI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-11

### Added

#### Core Features
- Database-agnostic data extraction from any SQL database
- AI-powered name matching using local Ollama LLMs
- Flexible face detection with multiple approaches (OpenCV DNN, YOLOv8-Face, Ollama Vision)
- Automated portrait generation with configurable dimensions (default: 200x300px)
- Filename pattern detection for automatic photo metadata extraction
- Photo manifest file support for flexible metadata mapping
- Benchmarking capabilities with comparison reports
- Rich CLI experience with color-coded console output

#### Data Extraction
- `extract` command for exporting player data via SQL queries
- Support for multiple SQL dialects (MySQL, PostgreSQL, SQL Server, SQLite)
- Team-based filtering with `-teamId` parameter
- Custom CSV output naming with `-outputName` parameter
- Automatic column mapping with FIFA_Player_ID and Valid_Mapping placeholders

#### Name Mapping
- `map` command for mapping photos to player records
- Direct ID matching for exact matches
- Fuzzy name matching using Ollama LLMs
- Confidence-based validation (default threshold: 0.9)
- Automatic pattern detection for filename-based metadata
- Photo manifest support for custom metadata mappings
- Progress tracking with detailed console output
- Unmatched players and unused photos reporting

#### Portrait Generation
- `generatePhotos` command for portrait cropping
- Multiple face detection approaches:
  - OpenCV DNN (traditional computer vision)
  - YOLOv8-Face (deep learning)
  - Ollama Vision (LLaVA, Qwen3-VL models)
  - Center crop (fastest, no AI)
- Portrait-only mode for reusing existing face detections
- Configurable portrait dimensions (default: 200x300px)
- Multiple output formats (jpg, png)
- Progress indicators with detailed processing status
- Automatic fallback to alternative detection methods

#### Cross-Platform Support
- Full macOS support with bash/zsh scripts
- Full Windows support with PowerShell 7+ scripts
- Full Linux support with bash scripts
- Cross-platform .NET 8.0 runtime
- Consistent CLI experience across all platforms

#### Documentation
- Comprehensive README.md with getting started guide
- ARCHITECTURE_DECISIONS.md with technical decisions
- WORKFLOW.md with detailed workflow documentation
- Sample SQL queries for all major database systems
- Connection string templates
- Inline help for all commands (`--help`)

### Phase 1: Git Cleanup
- Removed 49 temporary test photos from temp_mapping_photos/
- Removed test_output_spain/ and test_output_switzerland/ directories
- Removed test_photos_extended/ directory
- Removed test CSV files and test manifests
- Updated .gitignore with test_output_* pattern
- Clean repository ready for production release

### Phase 2: Data Workflow
- Successfully processed 49 players (24 Spain + 25 Switzerland)
- 100% mapping success rate with confidence scores
- Validated name matching accuracy with real player data
- Tested workflow with FIFA photo naming convention
- Confirmed database-agnostic extraction from multiple sources

### Phase 3: Portrait Generation
- Generated all 49 portraits with consistent 200x300px dimensions
- Tested and validated multiple face detection approaches:
  - OpenCV DNN for traditional computer vision
  - YOLOv8-Face for deep learning-based detection
  - LLaVA and Qwen3-VL for AI-powered detection
- Implemented portrait-only mode for reusing detections
- Added center crop option for fast processing
- Improved face detection accuracy with fallback mechanisms

### Phase 4: PowerShell Scripts
- Created PowerShell scripts for all operations
- Full Windows compatibility with PowerShell 7+
- Cross-platform documentation and examples
- Tested on macOS, Windows, and Linux
- Consistent behavior across all platforms

### Performance Improvements
- Optimized name matching with local LLMs (Ollama)
- Fast face detection with GPU acceleration (when available)
- Multi-threaded processing for batch operations
- Efficient memory usage for large photo sets
- Caching mechanisms for repeated face detections

### Testing
- Validated all 49 players with 100% success rate
- Tested face detection on multiple photo types
- Benchmarking framework for model comparison
- Integration tests for all commands
- Cross-platform compatibility tests

### Documentation Updates
- Added comprehensive README.md
- Created ARCHITECTURE_DECISIONS.md
- Added WORKFLOW.md with detailed examples
- Created samples/ with SQL templates
- Added inline help for all CLI commands

### Configuration
- appsettings.template.json for configuration template
- Multiple SQL examples in samples/sql-examples/
- Connection string templates
- Configurable confidence thresholds
- Configurable portrait dimensions

## Technical Details

### Dependencies
- .NET 8.0 SDK
- Ollama (for AI features)
- OpenCV (for computer vision)
- YOLOv8-Face models
- PowerShell 7+ (Windows)

### Build System
- .NET solution file (PhotoMapperAI.sln)
- Project references properly configured
- NuGet packages for dependencies
- Cross-platform build support

### Code Quality
- Clean code structure with proper separation of concerns
- Comprehensive error handling
- Detailed logging and progress reporting
- Modular design for easy extension

## Migration Notes

No migration required - this is the initial stable release.

## Upgrade Path

No upgrade path needed - this is version 1.0.0.

---

## [Unreleased]

### Planned Features
- Web UI for non-technical users
- Batch processing for multiple teams
- Cloud LLM support (OpenAI, Anthropic)
- Additional face detection models
- Custom portrait dimensions
- Watermarking support
- Docker container support
- Automated testing pipeline

---

[1.0.0]: https://github.com/LuisCharro/PhotoMapperAI/releases/tag/v1.0.0
