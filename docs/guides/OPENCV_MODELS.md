# OpenCV Model Files Setup

## Overview

PhotoMapperAI can use OpenCV-backed face detection models. The project ships config templates and download helpers, but the model binaries themselves should stay out of git.

Ignored by default:

- `models/`
- `*.xml`
- `*.caffemodel`
- `*.onnx`

## Models Commonly Used

### YuNet

Recommended local default on Windows/Linux.

File:

- `face_detection_yunet_2023mar.onnx`

### OpenCV DNN

Files:

- `deploy.prototxt`
- `res10_300x300_ssd_iter_140000.caffemodel`

### Haar cascades

Files:

- `haarcascade_frontalface_default.xml`
- `haarcascade_eye.xml`
- `haarcascade_lefteye_2splits.xml`
- `haarcascade_righteye_2splits.xml`

### YOLOv8-Face

Optional ONNX model when you want that path specifically.

## Recommended Setup

From the repo root:

```bash
mkdir -p models
```

Then either:

- use the included helper scripts:
  - `scripts/download-opencv-models.ps1`
  - `scripts/download-opencv-models.sh`
- or download the files manually into `./models`

Example manual downloads:

```bash
curl -L -o models/face_detection_yunet_2023mar.onnx https://raw.githubusercontent.com/opencv/opencv_zoo/main/models/face_detection_yunet/face_detection_yunet_2023mar.onnx
curl -L -o models/deploy.prototxt https://raw.githubusercontent.com/opencv/opencv/master/samples/dnn/face_detector/deploy.prototxt
curl -L -o models/res10_300x300_ssd_iter_140000.caffemodel https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel
```

## Config

The default config shape is defined in [`../../appsettings.template.json`](../../appsettings.template.json).

Typical local setup:

1. Copy `appsettings.template.json` to `appsettings.json`.
2. Keep `OpenCV.ModelsPath` pointed at `./models` unless you have a reason to move it.
3. Verify the filenames in config match the files you downloaded.

## Practical Recommendation

- Start with YuNet.
- Add DNN if you need that fallback.
- Use Ollama or Apple Vision paths where appropriate for harder cases.
- Let `center` be the final fallback when deterministic completion matters more than face alignment.

## Related Docs

- [`FACE_DETECTION_GUIDE.md`](FACE_DETECTION_GUIDE.md)
- [`../../README.md`](../../README.md)
