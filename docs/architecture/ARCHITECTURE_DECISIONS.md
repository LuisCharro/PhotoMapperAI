# Architecture Decisions - PhotoMapperAI

## Overview

This document captures all key architectural decisions made during the design of PhotoMapperAI, including technology choices, design patterns, and trade-offs.

## Design Philosophy

1. **Modularity First:** Every AI component implements an interface for easy swapping
2. **Database-Agnostic:** Tool works with any database via SQL extraction
3. **Configurable:** Allow multiple approaches for same problem (e.g., face detection)
4. **Local-First:** Use local AI (Ollama) when possible to reduce costs and latency
5. **Testable:** Create synthetic data for all testing scenarios

## Technology Stack

| Component | Technology | Version | Rationale |
|-----------|-----------|----------|-----------|
| **Framework** | .NET | 10 | Modern C#, cross-platform, high performance |
| **CLI** | McMaster.CommandLineUtils | Latest | You already use it, proven in production |
| **CSV** | CsvHelper | Latest | Cross-platform, popular, well-documented |
| **Images** | ImageSharp | Latest | Cross-platform, .NET-native |
| **Computer Vision** | OpenCvSharp4 | Latest | Wrapper for OpenCV, supports DNN/YOLO |
| **AI** | Ollama | Latest | Local LLMs, free, flexible |
| **Testing** | BenchmarkDotNet | Latest | Professional benchmarking |
| **Database** | User-provided | N/A | SQL Server, PostgreSQL, etc. |

## Architectural Decisions

### 1. Modular AI Services

**Decision:** All AI components implement interfaces with explicit ModelName property.

**Rationale:**
- Easy to swap models without changing business logic
- Simplifies testing (mock implementations possible)
- Supports benchmarking multiple implementations
- Transparent to user about which model is being used

**Interfaces:**
```csharp
public interface INameMatchingService
{
    Task<MatchResult> CompareNamesAsync(string name1, string name2);
    string ModelName { get; }
}

public interface IFaceDetectionService
{
    Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath);
    string ModelName { get; }
}
```

### 2. Multi-Tier Face Detection Strategy

**Decision:** Implement cascaded fallback system with multiple detection approaches.

**Rationale:**
- Different approaches excel in different scenarios
- YOLOv8-Face: Highest accuracy but slower
- OpenCV DNN: Good balance of speed and accuracy
- Ollama Vision: Handles edge cases (extreme angles, occlusions)
- Fallback ensures 100% coverage (even with center crop)

**Trade-offs:**
- Complexity: Multiple services to maintain
- Performance: Fallbacks can be slow (especially Ollama)
- Configuration: User needs to understand options

**Fallback Strategy:**
```
Try YOLOv8-Face (best accuracy)
  ↓ Success?
Use detected landmarks
  ↓ Failure?
Try OpenCV DNN (good speed)
  ↓ Success?
Use detected landmarks
  ↓ Failure?
Try Ollama Vision (handles edge cases)
  ↓ Success?
Use detected landmarks
  ↓ Failure?
Use center crop fallback (last resort)
```

### 3. Filename Pattern Extraction

**Decision:** Support 3-tier approach (auto-detect, user-specified, manifest).

**Rationale:**
- Photo sets from different sources have different filename patterns
- Single approach won't work for all cases
- User should have flexibility when automatic detection fails

**Options:**

1. **Automatic Pattern Detection:**
   - Try common regex patterns
   - Use pattern with most components matched
   - Works for common formats

2. **User-Specified Template:**
   - CLI parameter: `-filenamePattern "{id}_{family}_{sur}.png"`
   - Parse filename using template variables
   - Explicit control for user

3. **Photo Manifest:**
   - JSON file maps filenames to metadata
   - Most flexible approach
   - Good when filenames are unpredictable

**Implementation:**
```csharp
public class FilenameParser
{
    // Try automatic detection first
    var pattern = DetectPattern(filename);
    if (pattern != null)
    {
        return ParseWithPattern(filename, pattern);
    }

    // Try user-specified template
    if (!string.IsNullOrEmpty(userTemplate))
    {
        return ParseWithTemplate(filename, userTemplate);
    }

    // Try photo manifest
    if (!string.IsNullOrEmpty(manifestPath))
    {
        return LookupInManifest(filename, manifestPath);
    }

    // Give up
    return null;
}
```

### 4. Portrait Cropping Logic

**Decision:** Implement fallback logic for different detection scenarios.

**Rationale:**
- Both eyes detected: Most accurate centering
- One eye detected: Use face center + offset
- No eyes detected: Use face center only
- No face detected: Use image center (last resort)

**Crop Logic:**
```csharp
public Rect CalculatePortraitRect(FaceLandmarks landmarks, int imageWidth, int imageHeight)
{
    // Case 1: Both eyes detected (best)
    if (landmarks.BothEyesDetected)
    {
        var eyeMidpoint = CalculateMidpoint(landmarks.LeftEye, landmarks.RightEye);
        return new Rect(
            eyeMidpoint.X - (landmarks.FaceRect.Width * 0.6),
            eyeMidpoint.Y - (landmarks.FaceRect.Height * 0.8),
            landmarks.FaceRect.Width * 1.2,
            landmarks.FaceRect.Height * 2.5
        );
    }

    // Case 2: One eye detected (good)
    else if (landmarks.LeftEye != null || landmarks.RightEye != null)
    {
        var eye = landmarks.LeftEye ?? landmarks.RightEye;
        var faceCenter = landmarks.FaceCenter;
        return new Rect(
            faceCenter.X - (landmarks.FaceRect.Width * 0.5),
            eye.Y - (landmarks.FaceRect.Height * 0.7),
            landmarks.FaceRect.Width * 1.5,
            landmarks.FaceRect.Height * 2.5
        );
    }

    // Case 3: No eyes but face detected (acceptable)
    else if (landmarks.FaceRect != null)
    {
        var faceCenter = landmarks.FaceCenter;
        return new Rect(
            faceCenter.X - (landmarks.FaceRect.Width * 0.5),
            faceCenter.Y - (landmarks.FaceRect.Height * 0.5),
            landmarks.FaceRect.Width * 1.5,
            landmarks.FaceRect.Height * 2.5
        );
    }

    // Case 4: No face detected (fallback)
    else
    {
        return CalculateCenterCrop(imageWidth, imageHeight, portraitRatio);
    }
}
```

### 5. Database-Agnostic Extraction

**Decision:** Use SQL queries with parameterized placeholders instead of direct database connections.

**Rationale:**
- Tool should work with any database
- Don't want to maintain database-specific code (SQL Server, PostgreSQL, Oracle, etc.)
- User provides connection string and SQL query
- User is responsible for correct SQL

**Implementation:**
```csharp
public class DatabaseExtractor
{
    public async Task<List<PlayerRecord>> ExtractPlayersAsync(
        string connectionString,
        string query,
        Dictionary<string, object> parameters)
    {
        // Use appropriate provider based on connection string
        // SQL Server: Microsoft.Data.SqlClient
        // PostgreSQL: Npgsql
        // SQLite: Microsoft.Data.Sqlite

        // Execute query with parameters
        // Return list of PlayerRecord
    }
}
```

**Trade-offs:**
- User needs to know SQL
- User responsible for query correctness
- No database validation in tool

### 6. Ollama for AI Operations

**Decision:** Use Ollama (local) instead of cloud APIs (OpenAI, Anthropic) for default.

**Rationale:**
- **Cost:** Local LLMs are free after one-time hardware cost
- **Privacy:** Data doesn't leave your machine
- **Speed:** No network latency (Ollama runs locally)
- **Flexibility:** Can swap models easily without changing code

**Trade-offs:**
- **Hardware:** Requires capable machine (RAM, GPU helps)
- **Setup:** User must install and configure Ollama
- **Models:** Must download models (few GB each)

**Models Selected:**
- **Name Matching:** qwen2.5:7b (default), qwen3:8b (better)
- **Face Detection:** qwen3-vl, llava:7b (as fallback)

### 7. OpenCV for Fast Face Detection

**Decision:** Use OpenCV (DNN/YOLOv8) for primary face detection, Ollama Vision as fallback.

**Rationale:**
- **Speed:** OpenCV is orders of magnitude faster than LLM vision models
- **Accuracy:** YOLOv8-Face matches state-of-the-art
- **Local:** Runs entirely on CPU, no GPU required for basic operations
- **Proven:** Battle-tested in production systems

**Model Files Required:**

OpenCV requires downloading model files (binary XML/caffemodel/ONNX files):

**Option A: OpenCV DNN (Caffe) - 2 files (~10 MB)**
- `res10_ssd_deploy.prototxt`
- `res10_300x300_ssd_iter_140000.caffemodel`
- Good speed/accuracy balance
- Face detection only (no eye detection)

**Option B: Haar Cascades - 4 files (~12 MB)**
- `haarcascade_frontalface_default.xml`
- `haarcascade_eye.xml`
- `haarcascade_lefteye_2splits.xml`
- `haarcascade_righteye_2splits.xml`
- Fastest speed, lower accuracy
- Supports both face and eye detection

**Option C: YOLOv8-Face - 1 file (~20 MB)**
- `yolov8-face.onnx`
- Best accuracy
- Face detection only

**Source:** OpenCV GitHub repository (see `docs/OPENCV_MODELS.md` for download commands)

**Trade-offs:**
- **Setup:** Requires model files (download from GitHub, not included in repo)
- **Configuration:** User must configure paths in `appsettings.json`
- **Accuracy:** May fail on extreme angles (handled by Ollama fallback)
- **Complexity:** Need to integrate OpenCvSharp4 library

**Storage Location:**
- Models stored in `./models/` directory (gitignored)
- Paths configured in `appsettings.json`
- Can also use local test data folder

### 8. BenchmarkDotNet for Performance Testing

**Decision:** Use BenchmarkDotNet for professional benchmarking and report generation.

**Rationale:**
- **Standard:** Industry standard for .NET benchmarking
- **Features:** Memory diagnosis, multiple iterations, statistical analysis
- **Reports:** Generate Markdown, HTML, CSV reports automatically
- **Integration:** Works with xUnit test framework

**Implementation:**
```csharp
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class NameMatchingBenchmark
{
    [Benchmark]
    [ArgumentsSource(nameof(GetServices))]
    public async Task BenchmarkModel(INameMatchingService service)
    {
        // Run test
        var result = await service.CompareNamesAsync(name1, name2);

        // Track metrics
        // Accuracy, precision, recall, F1 score, speed
    }
}
```

### 9. Test Data Structure

**Decision:** Create comprehensive test data structure with realistic scenarios.

**Structure (local-only, not in repository):**
```
Test Data (local):
├── DataExtraction/           # Database to CSV
├── NameMatch/                 # CSV + Photos → Mapping
├── FaceRecognition/          # Face/eye detection
└── PortraitExtraction/        # Portraits from detected faces
```

**Rationale:**
- Each step of workflow has dedicated test data
- Synthetic data ensures reproducibility
- No real player data (privacy concerns)
- Photo files can be placeholders for filename testing (0 bytes)
- Local-only to avoid committing sensitive or personal test data

### 10. Command-Line Parser

**Decision:** Use McMaster.CommandLineUtils instead of System.CommandLine.

**Rationale:**
- **Maturity:** You already use it, proven in production
- **Features:** Subcommands, help generation, parameter validation
- **Cross-platform:** Works on Windows, Linux, macOS
- **Active Development:** Still maintained

**Trade-offs:**
- **Not .NET Native:** Requires external package
- **Learning Curve:** Different from built-in System.CommandLine

### 11. Image Processing

**Decision:** Use ImageSharp instead of System.Drawing or OpenCV for image cropping.

**Rationale:**
- **Cross-platform:** Works on Windows, Linux, macOS without issues
- **Performance:** Fast enough for basic operations
- **API:** Modern, fluent, easy to use
- **.NET Native:** No native dependencies like OpenCV

**Trade-offs:**
- **Features:** Fewer features than OpenCV for advanced operations
- **Speed:** Slightly slower than native implementations (acceptable for our use case)

## Trade-offs Summary

| Decision | Benefit | Cost | Final Verdict |
|-----------|---------|-------|----------------|
| **Modular AI Services** | Easy testing, model swapping | More interfaces to implement | ✅ Good |
| **Multi-tier Face Detection** | High accuracy, 100% coverage | Complex, slower fallbacks | ✅ Good |
| **Filename Pattern Options** | Flexible, handles all cases | Complex logic | ✅ Good |
| **Ollama instead of Cloud APIs** | Free, private, fast | Requires hardware, setup | ✅ Good |
| **OpenCV for Face Detection** | Fast, accurate | Model files, external library | ✅ Good |
| **BenchmarkDotNet** | Professional, feature-rich | Learning curve | ✅ Good |
| **McMaster.CommandLineUtils** | Mature, features | External dependency | ✅ Good |
| **ImageSharp instead of System.Drawing** | Cross-platform | Slower, fewer features | ✅ Good |

## Anti-Patterns to Avoid

1. **Tight Coupling to Specific Database:**
   - ❌ Direct SQL Server or PostgreSQL connections
   - ✅ Use parameterized SQL queries with user connection strings

2. **Hardcoded AI Models:**
   - ❌ Direct calls to GPT-4 API in business logic
   - ✅ Interface-based design with model factory

3. **Single Approach for All Cases:**
   - ❌ Assume all filenames follow same pattern
   - ❌ Assume all photos are front-facing
   - ✅ Multiple options with fallbacks

4. **No Testing Strategy:**
   - ❌ Only manual testing
   - ❌ No benchmark data
   - ✅ Synthetic test data, automated benchmarks

5. **Cloud Dependencies:**
   - ❌ Require API keys for core functionality
   - ❌ Cloud-based image processing
   - ✅ Local-first approach (Ollama, OpenCV)

## Future Considerations

### Scalability

- **Large Databases:** Test with 10,000+ player records
- **Batch Processing:** Implement parallel processing for large photo sets
- **Memory Management:** Ensure tool doesn't crash with large datasets

### Extensibility

- **New Models:** Easy to add new Ollama models or cloud APIs
- **New Databases:** Add support for Oracle, MySQL, etc.
- **New Features:** Add support for video frames, live camera capture

### Performance

- **Caching:** Cache Ollama responses for common name comparisons
- **Parallel Processing:** Process multiple photos simultaneously
- **GPU Acceleration:** Use OpenCV with CUDA for faster face detection

### User Experience

- **Progress Indicators:** Show progress bars for long operations
- **Verbose Mode:** Detailed logging for debugging
- **Configuration Files:** Save settings between runs
- **Interactive Mode:** Select models interactively for testing

## Conclusion

The architectural decisions balance flexibility, performance, and maintainability. The modular AI service design allows easy experimentation with different models, the multi-tier fallback strategy ensures robustness, and the local-first approach minimizes costs and privacy concerns.

The test data structure and benchmarking capabilities will enable data-driven decisions about which models to use in production.
