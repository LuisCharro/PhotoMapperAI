# OpenCV Model Files Setup

## Overview

PhotoMapperAI uses OpenCV for face detection, which requires downloading model files. These models are binary files (XML, caffemodel, ONNX) and should NOT be committed to the repository.

**Files are gitignored:** `.gitignore` includes `models/`, `*.xml`, `*.caffemodel`, `*.onnx`

## Model Files Needed

### 1. OpenCV DNN (Caffe) - Face Detection

**Files:**
- `res10_ssd_deploy.prototxt` (~28 KB)
- `res10_300x300_ssd_iter_140000.caffemodel` (~10 MB)

**Source:** [OpenCV DNN Face Detector (Caffe Model)](https://github.com/opencv/opencv_3rdparty/tree/dnn_samples_face_detector_20170830)

**Download commands:**
```bash
cd models
curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_ssd_deploy.prototxt
curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel
```

### 2. Haar Cascades - Face & Eye Detection

**Files:**
- `haarcascade_frontalface_default.xml` (~9 MB)
- `haarcascade_eye.xml` (~1 MB)
- `haarcascade_lefteye_2splits.xml` (~800 KB)
- `haarcascade_righteye_2splits.xml` (~800 KB)

**Source:** [OpenCV Haar Cascades Repository](https://github.com/opencv/opencv/tree/master/data/haarcascades)

**Download commands:**
```bash
cd models

# Face cascade
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml

# Eye cascades
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_eye.xml
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_lefteye_2splits.xml
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_righteye_2splits.xml
```

### 3. YOLOv8-Face (Optional, Best Accuracy)

**File:**
- `yolov8-face.onnx` (~20 MB)

**Source:** [YOLOv8-Face Models on GitHub](https://github.com/akanametov/yolov8-face)

**Search for:** `yolov8-face.onnx` or `yolov8n-face.onnx`

**Alternative:** Convert from PyTorch to ONNX if needed.

## Setup Instructions

### Step 1: Create Models Directory

```bash
cd /Users/luis/Repos/PhotoMapperAI
mkdir -p models
```

### Step 2: Download Models

Choose the models you want to use:

#### Option A: OpenCV DNN (Recommended - Good Speed/Accuracy)
```bash
cd models
curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_ssd_deploy.prototxt
curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel
```

#### Option B: Haar Cascades (Fastest, Lower Accuracy)
```bash
cd models
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_eye.xml
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_lefteye_2splits.xml
curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_righteye_2splits.xml
```

#### Option C: YOLOv8-Face (Best Accuracy, Slower)
```bash
cd models
# Download from YOLOv8-Face repository
# See https://github.com/akanametov/yolov8-face for download links
```

### Step 3: Configure appsettings.json

1. Copy the template:
```bash
cp appsettings.template.json appsettings.json
```

2. Update paths in `appsettings.json`:
```json
{
  "OpenCV": {
    "ModelsPath": "./models",
    "FaceDetection": {
      "Model": "res10_ssd_deploy.prototxt",
      "Weights": "res10_300x300_ssd_iter_140000.caffemodel"
    }
  }
}
```

### Step 4: Add to .gitignore

The `.gitignore` already includes:
```
models/
*.xml
*.caffemodel
*.onnx
appsettings.json
```

## Model Comparison

| Model | Files Needed | Speed | Accuracy | Face Detection | Eye Detection |
|--------|-------------|---------|------------|---------------|
| **OpenCV DNN** | 2 files (prototxt + caffemodel) | Fast | High | ❌ No |
| **Haar Cascades** | 4 files (XML) | Very Fast | Medium | ✅ Yes |
| **YOLOv8-Face** | 1 file (ONNX) | Medium | Very High | ❌ No |
| **Ollama Vision** | 0 files (uses Ollama) | Slow | High | ✅ Yes |

**Recommended:** Start with **OpenCV DNN** (best balance). Add **Haar Cascades** for eye detection. Use **Ollama Vision** as fallback.

## Alternative: Put Models in Test Data

Instead of committing to repository, you can put models in your local test data folder:

```bash
# In test data folder
mkdir -p ~/test-data/PhotoMapperAI/models

# Download models there
cd ~/test-data/PhotoMapperAI/models
# ... download commands ...

# Update appsettings.json
{
  "OpenCV": {
    "ModelsPath": "/Users/luis/test-data/PhotoMapperAI/models"
  }
}
```

## Troubleshooting

### "Cannot find model file"
- Check `appsettings.json` paths
- Verify files exist in `ModelsPath` directory
- Check file permissions

### "Model file is corrupted"
- Re-download the model file
- Verify file size matches expected size
- Use `curl -L` to follow redirects

### "Face detection not working"
- Check confidence threshold in appsettings.json
- Try different model (DNN vs Haar vs YOLOv8)
- Test with Ollama Vision as fallback

## References

- OpenCV DNN Face Detector: https://github.com/opencv/opencv_3rdparty/tree/dnn_samples_face_detector_20170830
- OpenCV Haar Cascades: https://github.com/opencv/opencv/tree/master/data/haarcascades
- YOLOv8-Face: https://github.com/akanametov/yolov8-face
- OpenCvSharp4: https://github.com/shimat/opencvsharp
