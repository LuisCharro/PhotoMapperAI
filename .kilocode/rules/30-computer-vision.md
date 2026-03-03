# Computer Vision & Image Processing

## Face Detection Models

### Available Models

| Model | Description | Pros | Cons |
|-------|-------------|------|------|
| `opencv-dnn` | OpenCV DNN module | Fast, no external deps | Less accurate |
| `yolov8-face` | YOLOv8 face detection | High accuracy | Requires model file |
| `llava` | Ollama Vision | Best accuracy | Slow, needs Ollama |
| `center` | Center crop | Instant | Assumes full-body photos |

### Using Face Detection
```csharp
// Via service
var processor = new OpenCvImageProcessor();
var faceResult = await processor.DetectFaceAsync(imagePath);

// Check result
if (faceResult.FaceDetected)
{
    var eyes = faceResult.EyePositions;
    var cropArea = CalculatePortraitCrop(eyes, imageHeight);
}
```

## Image Processing Pipeline

### Portrait Generation Flow
```
Input Image → Face Detection → Eye Detection → Calculate Crop → Resize → Output
```

### Crop Calculation
```csharp
public static Rectangle CalculatePortraitCrop(EyePositions eyes, int imageHeight)
{
    // Eyes are typically at ~40% from top in portrait
    var eyeCenter = (eyes.Left.Y + eyes.Right.Y) / 2;
    var cropTop = Math.Max(0, eyeCenter - (imageHeight * 0.30));
    var cropHeight = (int)(imageHeight * 0.45); // Head + neck + chest
    
    return new Rectangle(0, (int)cropTop, imageWidth, cropHeight);
}
```

## OpenCV Best Practices

### Loading Images
```csharp
using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
if (image.Empty())
{
    throw new InvalidOperationException($"Cannot load image: {imagePath}");
}
```

### Face Detection (OpenCV DNN)
```csharp
using var faceNet = CvDnn.ReadNetFromCaffe(prototxt, weights);
using var blob = CvDnn.BlobFromImage(image, 1.0, size, mean);
faceNet.SetInput(blob);
var detections = faceNet.Forward("detection_out");
```

### Saving Images
```csharp
Cv2.ImWrite(outputPath, processedImage, 
    new[] { (ImwriteFlags)95, 90 }); // JPEG quality
```

## YOLOv8 Face Detection

### Setup
- Download YOLOv8 face model (`yolov8n-face.pt`)
- Place in `models/` directory

### Usage
```csharp
var detector = new YoloFaceDetector("models/yolov8n-face.pt");
var faces = detector.Detect(imagePath);
```

## Ollama Vision Models

### Configuration
```json
{
  "FaceDetection": {
    "Models": ["llava:7b", "qwen2.5vl:7b"],
    "Fallback": "opencv-dnn"
  }
}
```

### API Call
```csharp
var visionResult = await ollamaClient.AnalyzeImageAsync(
    model: "llava:7b",
    imagePath: imagePath,
    prompt: "Detect face and eye positions. Return as JSON."
);
```

## Error Handling

### Common Issues

1. **Image too dark/small**
   - Apply preprocessing: histogram equalization, resize
   
2. **No face detected**
   - Try alternative model
   - Log warning, skip image
   
3. **Multiple faces**
   - Use largest/center face
   - Log ambiguity warning

### Graceful Degradation
```csharp
try
{
    var result = await DetectFaceWithFallbackAsync(imagePath);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Face detection failed, using center crop");
    return CalculateCenterCrop(imagePath);
}
```

## Performance

### Caching
- Cache loaded models in singleton
- Reuse `Mat` objects when possible

### Batch Processing
```csharp
// Process in batches for memory efficiency
foreach (var batch in images.Chunk(10))
{
    var results = await Task.WhenAll(
        batch.Select(DetectFaceAsync)
    );
}
```

### Image Formats
- Input: PNG, JPG, BMP supported
- Output: Configurable (default: JPG at 90% quality)
