# Portrait Generation Improvements Plan

## Overview

This document outlines the required improvements to achieve consistent, high-quality portrait generation for sports player photos.

## Current Issues

1. **Inconsistent centering** - Portraits have varying eye positions across photos
2. **No Haar Cascade eye detection** - OpenCV DNN only detects face, not eyes
3. **Single output size** - Cannot generate multiple sizes in one run
4. **No portrait detection** - Crops photos that are already portraits
5. **No debug visualization** - Cannot see detected regions for troubleshooting

## Required Improvements

### 1. Haar Cascade Eye Detection (High Priority)

**Problem:** OpenCV DNN only detects face rectangle, not eyes. This leads to estimated eye positions which vary between photos.

**Solution:** Add Haar Cascade classifiers for eye detection within the face region.

**Implementation:**
- Add Haar Cascade XML files to `Resources/` directory:
  - `haarcascade_frontalface_default.xml` - Face detection
  - `haarcascade_eye.xml` - Eye detection within face
- Create `HaarCascadeFaceDetectionService` implementing `IFaceDetectionService`
- Detect face first, then detect eyes WITHIN the face region
- Return both face rectangle and eye positions

**Algorithm (Proven Approach):**
```
1. Detect face using Haar Cascade
2. Extract face region from image
3. Detect eyes within face region using Haar Cascade
4. If exactly 2 eyes found:
   - Calculate eye midpoint = average of both eye centers
   - Use for horizontal centering
5. If 1 or 0 eyes found:
   - Fall back to face center with Y adjustment (40% from top of face)
6. If >2 eyes found (false positives):
   - Merge overlapping eye regions
   - Retry with merged regions
```

**Files to Create/Modify:**
- `src/PhotoMapperAI/Resources/haarcascade_frontalface_default.xml`
- `src/PhotoMapperAI/Resources/haarcascade_eye.xml`
- `src/PhotoMapperAI/Services/AI/HaarCascadeFaceDetectionService.cs`
- `src/PhotoMapperAI/Services/Image/ImageProcessor.cs` - Update crop logic

### 2. Face-Based Crop Dimensions (High Priority)

**Problem:** Current crop size is based on image dimensions (35% of height), which varies between photos.

**Solution:** Calculate crop dimensions based on DETECTED FACE size, not image size.

**Implementation:**
```csharp
// Crop width = 2.0 × face width
var cropWidth = (int)(faceRect.Width * 2.0);

// Crop height = 3.0 × face height
var cropHeight = (int)(faceRect.Height * 3.0);
```

This ensures consistent portrait composition regardless of image resolution or distance from camera.

### 3. Multiple Output Sizes Configuration (Medium Priority)

**Problem:** Can only generate one output size per run.

**Solution:** Add configuration file support for multiple output sizes.

**Configuration Format (JSON):**
```json
{
  "outputSizes": [
    {
      "name": "small",
      "width": 34,
      "height": 50,
      "destinationPath": "Person/PP_NationalTeam/small"
    },
    {
      "name": "medium",
      "width": 67,
      "height": 100,
      "destinationPath": "Person/PP_NationalTeam/medium"
    },
    {
      "name": "large",
      "width": 100,
      "height": 150,
      "destinationPath": "Person/PP_NationalTeam/large"
    },
    {
      "name": "standard",
      "width": 200,
      "height": 300,
      "destinationPath": "Person/PP_NationalTeam/300"
    }
  ]
}
```

**CLI Usage:**
```bash
# Single size (current behavior)
dotnet run -- generatePhotos -i players.csv -o ./portraits -fw 200 -fh 300

# Multiple sizes from config
dotnet run -- generatePhotos -i players.csv -o ./portraits --sizeConfig sizes.json
```

### 4. Portrait Photo Detection (Medium Priority)

**Problem:** Photos that are already portraits get cropped again, degrading quality.

**Solution:** Detect if photo is already a portrait and skip cropping.

**Detection Logic:**
```csharp
// If face width × 2.5 > image width, photo is already a portrait
var isAlreadyPortrait = faceRect.Width * 2.5 > imageWidth;

// If face height × 3 > image height, photo is already a portrait
var isAlreadyPortraitHeight = faceRect.Height * 3 > imageHeight;

if (isAlreadyPortrait || isAlreadyPortraitHeight)
{
    // Skip crop, just resize to target dimensions
    return ResizeOnly(image, targetWidth, targetHeight);
}
```

### 5. Debug Visualization Mode (Low Priority)

**Problem:** Cannot see what regions are being detected for troubleshooting.

**Solution:** Add `--debug` flag that saves intermediate images with detected regions highlighted.

**Output:**
- `debug_face.jpg` - Image with face rectangle highlighted
- `debug_eyes.jpg` Image with eye rectangles highlighted
- `debug_crop.jpg` - Image with final crop region highlighted

**CLI Usage:**
```bash
dotnet run -- generatePhotos -i players.csv -o ./portraits --debug
```

## Implementation Priority

| Priority | Feature | Effort | Impact |
|----------|---------|--------|--------|
| 1 | Haar Cascade Eye Detection | Medium | High |
| 2 | Face-Based Crop Dimensions | Low | High |
| 3 | Multiple Output Sizes | Medium | Medium |
| 4 | Portrait Detection | Low | Medium |
| 5 | Debug Visualization | Low | Low |

## Success Criteria

1. **Consistent eye position** - All portraits have eyes at 35-40% from top
2. **Consistent horizontal centering** - All portraits have face centered using eye midpoint
3. **Multiple sizes in one run** - Generate 4+ sizes from single command
4. **No over-cropping** - Portrait photos are detected and not cropped
5. **Debug capability** - Can visualize detection for troubleshooting

## Technical Notes

### Haar Cascade vs DNN

| Aspect | Haar Cascade | DNN |
|--------|--------------|-----|
| Speed | Fast (10-50ms) | Medium (50-100ms) |
| Face Detection | Good | Better |
| Eye Detection | Good (with face region) | Not available |
| Model Files | XML (~1MB total) | Caffe model (~10MB) |
| CPU Only | Yes | Yes |

**Recommendation:** Use Haar Cascade for eye detection within face region. This is the proven approach that produces consistent results.

### Crop Dimension Math

For a portrait showing head + neck + upper chest:
- Face should occupy ~33% of portrait height
- Crop height = 3 × face height
- Crop width = 2 × face width (maintains 2:3 aspect ratio)

### Eye Positioning Math

For standard portrait composition:
- Eyes should be at ~35% from top of portrait
- Horizontal center = midpoint between eyes
- Vertical offset from eye midpoint = cropHeight × 0.35

## Files to Create/Modify

### New Files
- `src/PhotoMapperAI/Resources/haarcascade_frontalface_default.xml`
- `src/PhotoMapperAI/Resources/haarcascade_eye.xml`
- `src/PhotoMapperAI/Services/AI/HaarCascadeFaceDetectionService.cs`
- `src/PhotoMapperAI/Models/OutputSizeConfig.cs`
- `src/PhotoMapperAI/Configuration/SizeConfigReader.cs`
- `sizes.template.json` (configuration template)

### Modified Files
- `src/PhotoMapperAI/Services/Image/ImageProcessor.cs` - Update crop logic
- `src/PhotoMapperAI/Commands/GeneratePhotosCommand.cs` - Add size config support
- `src/PhotoMapperAI/Commands/Program.cs` - Add --sizeConfig and --debug options
- `docs/FACE_DETECTION_GUIDE.md` - Document new features

## Next Steps

1. Download Haar Cascade XML files from OpenCV repository
2. Implement `HaarCascadeFaceDetectionService`
3. Update `ImageProcessor.CalculatePortraitCrop()` for face-based dimensions
4. Add multiple output size configuration
5. Add portrait detection logic
6. Add debug visualization mode
7. Update documentation
