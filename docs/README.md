# PhotoMapperAI Documentation

This folder contains all technical documentation for PhotoMapperAI, organized into subfolders by topic.

## Documentation Structure

### 📁 [architecture/](architecture/)
Technical architecture decisions and design documentation.
- [`ARCHITECTURE_DECISIONS.md`](architecture/ARCHITECTURE_DECISIONS.md) - Key architectural decisions, technology choices, and trade-offs

### 📁 [guides/](guides/)
User guides and technical references.
- [`GUIDE.md`](guides/GUIDE.md) - Desktop GUI application guide
- [`FACE_DETECTION_GUIDE.md`](guides/FACE_DETECTION_GUIDE.md) - Face detection model comparison and best practices
- [`NAME_MAPPING_PIPELINE.md`](guides/NAME_MAPPING_PIPELINE.md) - Name mapping strategy and decision flow
- [`EDGE_CASES.md`](guides/EDGE_CASES.md) - Comprehensive edge cases and troubleshooting
- [`MODEL_BENCHMARKS.md`](guides/MODEL_BENCHMARKS.md) - Model performance benchmarks
- [`OPENCV_MODELS.md`](guides/OPENCV_MODELS.md) - OpenCV model setup and configuration
- [`OPENCV_DNN_STATUS.md`](guides/OPENCV_DNN_STATUS.md) - OpenCV DNN implementation status
- [`UI_GENERATE_SIZE_PROFILE_CHECKLIST.md`](guides/UI_GENERATE_SIZE_PROFILE_CHECKLIST.md) - UI size profile checklist

### 📁 [planning/](planning/)
Project planning and progress tracking.
- [`PROJECT_PLAN.md`](planning/PROJECT_PLAN.md) - Implementation plan and phases
- [`PROGRESS.md`](planning/PROGRESS.md) - Development progress and tasks
- [`WORKFLOW.md`](planning/WORKFLOW.md) - Development workflow documentation
- [`NEXT_STEPS_HANDOFF.md`](planning/NEXT_STEPS_HANDOFF.md) - Handoff document for next development session
- [`DEVELOPMENT_INTEGRATION_PLAN_2026-02-14.md`](planning/DEVELOPMENT_INTEGRATION_PLAN_2026-02-14.md) - Integration plan
- [`PLAYERPORTRAITMANAGER_PARITY_PLAN.md`](planning/PLAYERPORTRAITMANAGER_PARITY_PLAN.md) - Parity implementation plan
- [`PORTRAIT_IMPROVEMENTS_PLAN.md`](planning/PORTRAIT_IMPROVEMENTS_PLAN.md) - Portrait enhancement plans

### 📁 [reports/](reports/)
Validation reports and status documents.
- [`PHASE3_FINAL_REPORT.md`](reports/PHASE3_FINAL_REPORT.md) - Phase 3 final validation report
- [`PHASE3_VALIDATION_REPORT.md`](reports/PHASE3_VALIDATION_REPORT.md) - Phase 3 detailed validation
- [`PORTRAIT_CROP_FIX_SUMMARY.md`](reports/PORTRAIT_CROP_FIX_SUMMARY.md) - Portrait crop fix documentation
- [`WINDOWS_COMPATIBILITY_REPORT.md`](reports/WINDOWS_COMPATIBILITY_REPORT.md) - Windows compatibility validation
- [`PARITY_*.md`](reports/) - Various parity tracking and status documents

### 📁 [testing/](testing/)
Testing strategies and test session documentation.
- [`TESTING_STRATEGY.md`](testing/TESTING_STRATEGY.md) - Testing and benchmarking approach
- [`TEST_CONFIGURATION.md`](testing/TEST_CONFIGURATION.md) - Test configuration setup
- [`ANONYMIZED_VALIDATION.md`](testing/ANONYMIZED_VALIDATION.md) - In-repo anonymized CLI validation
- [`TEST_SESSION.md`](testing/TEST_SESSION.md) - Test session logs and findings
- [`TEST_SUMMARY_PHASE3.md`](testing/TEST_SUMMARY_PHASE3.md) - Phase 3 test summary
- [`TESTING_FINDINGS_REPORT.md`](testing/TESTING_FINDINGS_REPORT.md) - Comprehensive testing findings
- [`TESTING_EXTENDED_WORKFLOW.md`](testing/TESTING_EXTENDED_WORKFLOW.md) - Extended workflow test results

## Root Level Documentation

The following essential files remain at the repository root:
- [`README.md`](../README.md) - Main project documentation and getting started
- [`CHANGELOG.md`](../CHANGELOG.md) - Version history and changes
- [`RELEASE_NOTES.md`](../RELEASE_NOTES.md) - Release notes for each version
- [`PENDING_FEATURES.md`](../PENDING_FEATURES.md) - Planned and pending features

## External Resources

- **External Test Data**: [`PhotoMapperAI_ExternalData`](/Users/luis/Repos/PhotoMapperAI_ExternalData/README.md) - Real test data for validation (not in repo)

## Quick Links by Task

### Getting Started
1. [Main README](../README.md) - Start here
2. [GUI Guide](guides/GUIDE.md) - Desktop application guide
3. [Architecture Decisions](architecture/ARCHITECTURE_DECISIONS.md) - Understand the design

### Development
1. [Project Plan](planning/PROJECT_PLAN.md) - Implementation phases
2. [Progress Tracking](planning/PROGRESS.md) - Current status
3. [Next Steps](planning/NEXT_STEPS_HANDOFF.md) - What to work on next

### Troubleshooting
1. [Edge Cases](guides/EDGE_CASES.md) - Common issues and solutions
2. [Face Detection Guide](guides/FACE_DETECTION_GUIDE.md) - Face detection help
3. [Windows Compatibility](reports/WINDOWS_COMPATIBILITY_REPORT.md) - Windows-specific issues

### Testing
1. [Testing Strategy](testing/TESTING_STRATEGY.md) - How testing is organized
2. [Test Configuration](testing/TEST_CONFIGURATION.md) - Setting up tests
3. [Model Benchmarks](guides/MODEL_BENCHMARKS.md) - Model performance data
