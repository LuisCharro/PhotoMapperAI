# Face Detection and Portrait Generation Guide

## Overview

`generatephotos` reads a mapped CSV, finds the source image for each row, resolves a face-detection strategy, and writes portraits named by internal `PlayerId`.

Current generate-related options in code include:

- `--faceDetection`
- `--portraitOnly`
- `--faceWidth`
- `--faceHeight`
- `--cropFrameWidth`
- `--cropFrameHeight`
- `--sizeProfile`
- `--allSizes`
- `--outputProfile`
- `--downloadOpenCvModels`
- `--faceDetectionTrace`
- `--onlyPlayer`
- `--placeholderImage`
- `--noProfilePlaceholders`
- `--cropOffsetX`
- `--cropOffsetY`

## Supported Models

### Native or classic CV

- `apple-vision`
- `opencv-yunet`
- `opencv-dnn`
- `yolov8-face`
- `haar-cascade`
- `center`

### Ollama vision examples

- `llava:7b`
- `qwen3-vl`

You can also pass a comma-separated fallback chain:

```bash
dotnet run --project src/PhotoMapperAI -- generatephotos \
  --inputCsvPath team.csv \
  --photosDir ./photos \
  --processedPhotosOutputPath ./portraits \
  --faceDetection opencv-yunet,llava:7b,center
```

## Recommended Defaults

- macOS UI default: `apple-vision`
- Windows/Linux UI default: `opencv-yunet`
- general fast local choice: `opencv-yunet`
- reliable non-AI fallback: `center`
- harder vision cases: add `llava:7b` or `qwen3-vl` later in the fallback chain

## Portrait Sizing

There are two main modes:

### Manual single-size mode

Use `--faceWidth` and `--faceHeight`.

Default output size is `200x300`.

### Size-profile mode

Use `--sizeProfile` with a JSON file like [`../../size_profiles.json`](../../size_profiles.json).

- without `--allSizes`, the command uses the first variant
- with `--allSizes`, it generates every variant into subfolders

Per-variant placeholder files can be defined through `placeholderPath`. Use `--noProfilePlaceholders` to ignore them.

## Crop Frame vs Output Size

`--cropFrameWidth` and `--cropFrameHeight` let you change framing while keeping the final output size fixed.

That is useful when:

- you need to preview or tune a looser/tighter crop
- you want legacy framing with a fixed output size
- you are comparing parity against an older workflow

## Placeholder Handling

Two placeholder strategies exist:

- `--placeholderImage` for a single placeholder file used during the run
- per-variant `placeholderPath` entries inside a size profile

## Single-Player Verification

Use `--onlyPlayer` to debug one player at a time.

Example:

```bash
dotnet run --project src/PhotoMapperAI -- generatephotos \
  --inputCsvPath team.csv \
  --photosDir ./photos \
  --processedPhotosOutputPath ./portraits \
  --faceDetection opencv-yunet \
  --onlyPlayer 58877
```

The filter accepts either internal `PlayerId` or `External_Player_ID`.

## Preview and Trace

- `--faceDetectionTrace` logs model-specific detection details
- the UI preview uses the same shared generation logic for better parity with final output
- preview crop-frame presets are persisted in UI settings

## OpenCV Models

OpenCV-backed modes depend on model files configured through `appsettings.json`.

Relevant assets:

- DNN:
  - `deploy.prototxt`
  - `res10_300x300_ssd_iter_140000.caffemodel`
- YuNet:
  - `face_detection_yunet_2023mar.onnx`

See [`OPENCV_MODELS.md`](OPENCV_MODELS.md).

## Practical Recommendations

- Start with `opencv-yunet` on Windows/Linux and `apple-vision` on macOS.
- Add `center` at the end of a fallback chain when you want deterministic completion.
- Use `--onlyPlayer` when tuning crop offsets or comparing parity against legacy output.
- Use a size profile when downstream consumers expect multiple portrait sizes.
