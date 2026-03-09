# Face Detection on macOS - Research Findings

## Problem Statement

OpenCV (via OpenCvSharp4) does not support macOS ARM64 (Apple Silicon). When trying to use face detection models like `opencv-dnn`, `opencv-yunet`, or `haar-cascade` on a Mac, the application throws `DllNotFoundException` because there's no native ARM64 build available.

This forces macOS users to fall back to `center` crop (no actual face detection) which produces lower quality portraits compared to Windows users who get accurate face detection with OpenCV.

## Current Workaround

The current solution uses Ollama vision models (`qwen3-vl` or `llava:7b`) for face detection on macOS. This works but is significantly slower than OpenCV.

### Performance Comparison

| Platform | Model | Time per Image |
|----------|-------|---------------|
| Windows (OpenCV) | opencv-dnn | ~50ms |
| macOS (qwen3-vl) | AI vision | ~5000ms |

The AI-based approach is ~100x slower.

## Available Alternatives

### 1. OpenCvSharp4 (Official)
- **macOS Support**: ❌ No ARM64 support
- **x64 Support**: ✅ Works via Rosetta (slow)
- **Status**: Not viable for Apple Silicon

### 2. Sdcb.OpenCvSharp4.mini (Third-party)
- **Package**: `Sdcb.OpenCvSharp4.mini.runtime.osx-arm64`
- **macOS Support**: ⚠️ Partial/Third-party
- **Status**: Unmaintained, may work but risky

### 3. UltraFaceDotNet
- **Package**: `UltraFaceDotNet`
- **macOS Support**: ❌ x64 only
- **Status**: No ARM64 support

### 4. DlibDotNet
- **macOS Support**: ✅ Works
- **Speed**: Medium
- **Notes**: Requires CUDA for GPU acceleration

### 5. Apple Vision Framework (Best Native Option)
- **macOS Support**: ✅ Native, very fast
- **Speed**: Similar to OpenCV
- **Implementation**: Requires Swift interop or P/Invoke
- **Status**: Not yet implemented

### 6. Ollama Vision Models (Current Solution)
- **Models**: `qwen3-vl`, `llava:7b`
- **macOS Support**: ✅ Works
- **Speed**: Slow (~5s per image)
- **Accuracy**: Good

## Recommendations

### Short-term
- Keep using `qwen3-vl` as default for macOS
- Consider adding fallback chain: `qwen3-vl,center` for speed

### Long-term
- Implement Apple Vision Framework via P/Invoke
- This would provide native macOS speed comparable to OpenCV

## References

- [OpenCvSharp4 GitHub](https://github.com/shimat/opencvsharp)
- [Apple Vision Framework](https://developer.apple.com/documentation/vision)
- [UltraFaceDotNet](https://github.com/takuya-takeuchi/UltraFaceDotNet)
- [DlibDotNet](https://github.com/takuya-takeuchi/DlibDotNet)
