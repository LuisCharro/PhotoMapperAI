# Testing Strategy - PhotoMapperAI

## Overview

This document outlines the testing and benchmarking approach for PhotoMapperAI's AI components. The goal is to:
- Compare different AI models for name matching
- Compare different face detection approaches
- Document performance metrics and success rates
- Identify the best models for production use

## Testing Philosophy

1. **Modular Design:** Every AI component implements an interface → easy to swap models
2. **Benchmark-First:** Build testing tools alongside implementation
3. **Data Privacy:** Never commit test data with real player information
4. **Reproducible:** Document exact model versions, parameters, and datasets
5. **Iterative:** Update benchmarks as models improve or new ones emerge

## Test Suite Structure

```
tests/
├── PhotoMapperAI.Tests/
│   ├── Models/
│   │   ├── PlayerRecordTests.cs
│   │   ├── PhotoMetadataTests.cs
│   │   └── MappingResultTests.cs
│   ├── Services/
│   │   ├── DatabaseExtractorTests.cs
│   │   ├── PhotoMatcherTests.cs
│   │   └── PortraitCropperTests.cs
│   ├── AI/
│   │   ├── NameMatchingTests.cs       # Unit tests for each implementation
│   │   └── FaceDetectionTests.cs      # Unit tests for each implementation
│   ├── Benchmarks/
│   │   ├── NameMatchingBenchmark.cs    # Model comparison
│   │   └── FaceDetectionBenchmark.cs   # Approach comparison
│   ├── Data/
│   │   ├── sample-players.csv         # Synthetic player data
│   │   ├── name-pairs.json            # Pre-evaluated name matches
│   │   └── test-photos/               # Placeholder for test images
│   └── Helpers/
│       ├── TestDataGenerator.cs
│       └── BenchmarkRunner.cs
└── benchmark-results/
    ├── name-matching-2025-02-11.md
    └── face-detection-2025-02-11.md
```

## Name Matching Testing

### Test Dataset

**Synthetic Data Structure:**
```json
{
  "namePairs": [
    {
      "id": 1,
      "nameA": "Rodríguez Sánchez, Francisco Román Alarcón",
      "nameB": "Rodríguez Sánchez, Isco",
      "expectedMatch": true,
      "category": "same_person_nickname"
    },
    {
      "id": 2,
      "nameA": "Messi Lionel",
      "nameB": "Ramos Sergio",
      "expectedMatch": false,
      "category": "different_players"
    },
    {
      "id": 3,
      "nameA": "Silva David",
      "nameB": "Silva David dos Santos",
      "expectedMatch": true,
      "category": "same_player_fuller_name"
    }
  ]
}
```

**Name Variation Categories:**
1. **Exact match:** Names are identical
2. **Nickname:** One name uses nickname (e.g., "Isco" vs "Francisco Román")
3. **Full name vs short:** "David Silva dos Santos" vs "David Silva"
4. **Ordering difference:** "Rodríguez Sánchez, Isco" vs "Isco Rodríguez Sánchez"
5. **Accent handling:** "José" vs "Jose", "Müller" vs "Muller"
6. **Different players:** Clearly different people

**Test Data Generation:**
```csharp
public class TestDataGenerator
{
    public List<NamePair> GenerateNamePairs(int count)
    {
        var pairs = new List<NamePair>();

        // Generate known matches (different variations)
        pairs.AddRange(GenerateNicknamePairs(20));
        pairs.AddRange(GenerateFullVsShortPairs(20));
        pairs.AddRange(GenerateOrderingPairs(20));

        // Generate known non-matches
        pairs.AddRange(GenerateDifferentPlayers(40));

        return pairs;
    }

    private List<NamePair> GenerateNicknamePairs(int count)
    {
        // Real-world examples:
        // "Isco" vs "Francisco Román Alarcón Suárez"
        // "Neymar" vs "Neymar da Silva Santos Júnior"
        // "Ronaldo" vs "Cristiano Ronaldo dos Santos Aveiro"
    }
}
```

### Benchmark Execution

```csharp
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class NameMatchingBenchmark
{
    private List<INameMatchingService> _services;
    private List<NamePair> _testData;

    [GlobalSetup]
    public void Setup()
    {
        _services = new List<INameMatchingService>
        {
            new OllamaNameMatchingService("qwen2.5:7b"),
            new OllamaNameMatchingService("qwen3:8b"),
            new OllamaNameMatchingService("llava:7b"),
            new OllamaNameMatchingService("llama3.2:3b"),
            new OllamaNameMatchingService("gemma2:9b")
        };

        _testData = TestDataGenerator.GenerateNamePairs(100);
    }

    [Benchmark]
    [ArgumentsSource(nameof(GetServices))]
    public async Task BenchmarkModel(INameMatchingService service)
    {
        var results = new List<MatchResult>();

        foreach (var pair in _testData)
        {
            var result = await service.CompareNamesAsync(pair.NameA, pair.NameB);
            results.Add(result);
        }

        // Calculate metrics
        var accuracy = CalculateAccuracy(results, _testData);
        var avgConfidence = results.Average(r => r.Confidence);
        var speed = CalculateSpeed(results);

        Console.WriteLine($"{service.ModelName}: {accuracy:P} accuracy, {avgConfidence:F2} avg confidence");
    }

    public IEnumerable<INameMatchingService> GetServices() => _services;
}
```

### Output Format

Run benchmark and generate Markdown report:

```bash
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark NameMatching
```

Generates: `benchmark-results/name-matching-2025-02-11.md`

```markdown
# Name Matching Model Benchmarks

**Date:** 2025-02-11
**Dataset:** 100 name pairs (60 matches, 40 non-matches)
**Test Machine:** MacBook Air M3, 24GB RAM

## Results

| Model | Accuracy | Precision | Recall | F1 Score | Avg Confidence | False Positives | False Negatives | Avg Speed (ms) | Notes |
|-------|----------|-----------|--------|----------|----------------|-----------------|-----------------|-----------------|-------|
| **qwen2.5:7b** | 92.0% | 94.9% | 88.3% | 91.5% | 0.94 | 2 | 6 | 850 | Good balance |
| **qwen3:8b** | 95.0% | 96.7% | 93.3% | 95.0% | 0.96 | 1 | 4 | 920 | **Best accuracy** |
| **llava:7b** | 88.0% | 91.8% | 83.3% | 87.4% | 0.91 | 5 | 5 | 1100 | Slower, less accurate |
| **llama3.2:3b** | 85.0% | 89.5% | 80.0% | 84.5% | 0.89 | 6 | 6 | 520 | Fastest, lowest accuracy |
| **gemma2:9b** | 90.0% | 93.2% | 86.7% | 89.9% | 0.93 | 4 | 4 | 780 | Solid alternative |

## Analysis

### Top Performer: qwen3:8b
- **Accuracy:** 95% (highest)
- **Speed:** 920ms per comparison (acceptable)
- **Recommendation:** Use as default model

### Fastest: llama3.2:3b
- **Speed:** 520ms per comparison (1.8x faster than qwen3)
- **Accuracy:** 85% (10% drop)
- **Use case:** When speed is critical and some errors acceptable

### Overall Recommendation
1. **Production:** qwen3:8b (best accuracy)
2. **Quick testing:** qwen2.5:7b (already installed, good accuracy)
3. **Speed-critical:** llama3.2:3b (fastest)

## False Positive Examples

| Model | Name A | Name B | Confidence | Expected | Correct? |
|-------|--------|--------|------------|----------|---------|
| qwen2.5:7b | "Silva David" | "Silva David dos Santos" | 0.98 | Match | ✅ |
| llama3.2:3b | "Rodríguez Sergio" | "Rodríguez Alberto" | 0.75 | No Match | ❌ |

## False Negative Examples

| Model | Name A | Name B | Confidence | Expected | Correct? |
|-------|--------|--------|------------|----------|---------|
| qwen3:8b | "Neymar" | "Neymar da Silva Santos Júnior" | 0.94 | Match | ✅ |
| llava:7b | "Isco" | "Francisco Román Alarcón" | 0.82 | Match | ❌ |
```

## Face Detection Testing

### Test Dataset

**Synthetic Data Structure:**
```json
{
  "testPhotos": [
    {
      "id": 1,
      "path": "test-data/photo-001.png",
      "category": "frontal_full_body",
      "expectedFaceDetected": true,
      "expectedBothEyes": true,
      "expectedFaceRect": [250, 180, 120, 140]
    },
    {
      "id": 2,
      "path": "test-data/photo-002.png",
      "category": "side_angle",
      "expectedFaceDetected": true,
      "expectedBothEyes": false,
      "expectedFaceRect": [300, 200, 100, 130]
    },
    {
      "id": 3,
      "path": "test-data/photo-003.png",
      "category": "extreme_angle",
      "expectedFaceDetected": true,
      "expectedBothEyes": false,
      "expectedFaceRect": null // Hard to predict
    }
  ]
}
```

**Photo Categories:**
1. **Frontal full body:** Straight-on photo, clear face
2. **Slight angle:** 15-30 degrees, profile partially visible
3. **Side view:** 45-90 degrees, one eye visible
4. **Extreme angle:** Over 90 degrees, challenging
5. **Partial occlusion:** Ball, other players in way
6. **Lighting issues:** Shadows, backlight
7. **Multiple players:** Small face in background

**Test Data Generation:**
```csharp
public class TestDataGenerator
{
    // Generate test cases for face detection
    public List<FaceDetectionTestCase> GenerateFaceDetectionTests(int count)
    {
        // Use synthetic data with expected outcomes
        // Photos would be added manually or generated programmatically
    }
}
```

### Benchmark Execution

```csharp
[MemoryDiagnoser]
public class FaceDetectionBenchmark
{
    private List<IFaceDetectionService> _services;
    private List<FaceDetectionTestCase> _testData;

    [GlobalSetup]
    public void Setup()
    {
        _services = new List<IFaceDetectionService>
        {
            new OpenCVFaceDetectionService("opencv-dnn"),
            new OpenCVFaceDetectionService("yolov8-face"),
            new OpenCVFaceDetectionService("haar-cascade"),
            new OllamaFaceDetectionService("llava:7b"),
            new OllamaFaceDetectionService("qwen3-vl")
        };

        _testData = TestDataGenerator.GenerateFaceDetectionTests(100);
    }

    [Benchmark]
    [ArgumentsSource(nameof(GetServices))]
    public async Task BenchmarkModel(IFaceDetectionService service)
    {
        var results = new List<FaceDetectionResult>();

        foreach (var testCase in _testData)
        {
            var result = await service.DetectFaceLandmarksAsync(testCase.Path);
            results.Add(result);
        }

        // Calculate metrics
        var faceDetectionRate = CalculateFaceDetectionRate(results, _testData);
        var bothEyesRate = CalculateBothEyesRate(results, _testData);
        var avgSpeed = CalculateAverageSpeed(results);

        Console.WriteLine($"{service.ModelName}: {faceDetectionRate:P} faces, {bothEyesRate:P} both eyes, {avgSpeed:F0}ms avg");
    }
}
```

### Output Format

Generates: `benchmark-results/face-detection-2025-02-11.md`

```markdown
# Face Detection Model Benchmarks

**Date:** 2025-02-11
**Dataset:** 100 test photos (various angles and conditions)
**Test Machine:** MacBook Air M3, 24GB RAM

## Results

| Model | Face Detection | Both Eyes | One Eye | No Face | Avg Speed (ms) | Notes |
|-------|----------------|-----------|---------|---------|----------------|-------|
| **OpenCV DNN** | 95.0% | 82.0% | 13.0% | 5.0% | 45 | **Best overall** |
| **YOLOv8-Face** | 97.0% | 88.0% | 9.0% | 3.0% | 65 | **Highest accuracy** |
| **LLaVA:7b** | 92.0% | 75.0% | 17.0% | 8.0% | 3500 | Slowest |
| **Qwen3-VL:7b** | 94.0% | 80.0% | 14.0% | 6.0% | 3200 | Slow, good accuracy |
| **Haar Cascade** | 88.0% | 70.0% | 18.0% | 12.0% | 25 | Fastest, lowest accuracy |

## Analysis

### Top Performer: YOLOv8-Face
- **Face Detection:** 97% (highest)
- **Both Eyes:** 88% (highest)
- **Speed:** 65ms per photo (fast enough)
- **Recommendation:** Use as default when available

### Speed Champion: Haar Cascade
- **Speed:** 25ms per photo (2.6x faster than YOLOv8)
- **Accuracy:** 88% (9% drop from YOLOv8)
- **Use case:** Quick previews, batch processing with manual review

### Fallback Strategy
1. **Try YOLOv8-Face** → Success? Use it
2. **Fail? Try OpenCV DNN** → Success? Use it
3. **Fail? Try Haar Cascade** → Success? Use it
4. **Fail? Try Ollama Vision** → Success? Use it (slow but handles edge cases)
5. **All fail? Use center crop** (last resort)

## Detection Failure Analysis

### By Photo Category

| Category | Photos | YOLOv8 | OpenCV DNN | Haar | LLaVA |
|----------|--------|---------|-------------|------|-------|
| Frontal full body | 30 | 100% | 98% | 95% | 97% |
| Slight angle | 25 | 96% | 94% | 88% | 92% |
| Side view | 20 | 95% | 93% | 82% | 90% |
| Extreme angle | 15 | 93% | 90% | 80% | 88% |
| Partial occlusion | 8 | 88% | 85% | 75% | 87% |
| Lighting issues | 2 | 100% | 95% | 85% | 95% |

**Findings:**
- YOLOv8 performs best across all categories
- Haar cascade struggles with extreme angles
- Ollama Vision handles partial occlusion better than expected

### Eye Detection Success Rate

| Model | Frontal | Side | Extreme | Occlusion |
|-------|---------|------|---------|-----------|
| YOLOv8-Face | 98% | 85% | 65% | 50% |
| OpenCV DNN | 92% | 82% | 60% | 48% |
| Haar Cascade | 80% | 65% | 40% | 35% |
| LLaVA:7b | 88% | 75% | 55% | 55% |

**Findings:**
- Both eyes hard to detect in extreme angles
- Portrait cropping should handle one-eye or no-eye scenarios

## Recommendations

### Production Setup

```csharp
public FaceDetectionServiceFactory()
{
    // Primary: YOLOv8-Face (best accuracy)
    var primary = new YOLOv8FaceDetectionService();

    // Fallbacks in order
    var fallbacks = new List<IFaceDetectionService>
    {
        new OpenCVDNNFaceDetectionService(),
        new HaarCascadeFaceDetectionService(),
        new OllamaFaceDetectionService("llava:7b")
    };

    return new CascadedFaceDetectionService(primary, fallbacks);
}
```

### Model Selection by Use Case

| Use Case | Primary Model | Reason |
|----------|---------------|---------|
| **Production** | YOLOv8-Face | Best accuracy, good speed |
| **Quick preview** | Haar Cascade | Fastest, acceptable accuracy |
| **Challenging photos** | Ollama Vision | Best for edge cases |
| **Offline without model files** | Haar Cascade | Built into OpenCV |

## Portrait Quality Assessment

**Human Evaluation (Manual Review):**

| Model | Quality Score | Notes |
|-------|---------------|-------|
| YOLOv8-Face | 4.7/5 | Best centering, consistent |
| OpenCV DNN | 4.5/5 | Good, occasional misalignment |
| LLaVA:7b | 4.2/5 | Good centering, sometimes too zoomed |
| Haar Cascade | 4.0/5 | Variable quality, depends on detection |

**Quality Criteria:**
1. Face centered in portrait
2. Appropriate headroom (not too close, not too far)
3. Eyes at ~40% of portrait height (rule of thirds)
4. Shoulders visible (context)
5. No cropping issues
```

## Running Benchmarks

### Name Matching

```bash
# Run all name matching benchmarks
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark NameMatching --all

# Run specific model
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark NameMatching --model qwen3:8b

# Generate report
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark NameMatching --report
```

### Face Detection

```bash
# Run all face detection benchmarks
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark FaceDetection --all

# Run specific approach
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark FaceDetection --model yolov8-face

# Generate report
dotnet run --project tests/PhotoMapperAI.Tests -- --benchmark FaceDetection --report
```

## Documentation Updates

After each benchmark run:
1. Update `MODEL_BENCHMARKS.md` with latest results
2. Add date to filename: `name-matching-YYYY-MM-DD.md`
3. Update production recommendations if top model changes
4. Document any new models or approaches tested

## Continuous Testing

Add benchmarks to CI/CD pipeline (GitHub Actions):

```yaml
name: Benchmarks

on:
  schedule:
    - cron: '0 0 * * 0'  # Weekly
  workflow_dispatch:

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Install Ollama
        run: |
          curl -fsSL https://ollama.com/install.sh | sh
          ollama serve &
          ollama pull qwen2.5:7b
          ollama pull qwen3:8b
          ollama pull llava:7b
      - name: Run Benchmarks
        run: dotnet test --project tests/PhotoMapperAI.Tests --filter="Benchmark"
      - name: Upload Results
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-results
          path: benchmark-results/
```

## Summary

**Key Goals:**
1. Test multiple models/approaches systematically
2. Document results with clear metrics
3. Make informed decisions about production models
4. Update documentation as models improve
5. Continuous testing to catch regressions

**Success Metrics:**
- Name matching accuracy > 90%
- Face detection rate > 95%
- Both eyes detection rate > 80%
- Processing speed < 1000ms per operation
- Clear documentation of trade-offs
