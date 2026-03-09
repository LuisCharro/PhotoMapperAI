# Face Detection on macOS - Research Findings

## Current Repo State

The repo does not currently support OpenCV-based face detection on macOS as configured.

The main reasons are:

1. `src/PhotoMapperAI/PhotoMapperAI.csproj` references `OpenCvSharp4` plus `OpenCvSharp4.runtime.win`, but no macOS runtime package.
2. `src/PhotoMapperAI/Services/AI/FaceDetectionServiceFactory.cs` explicitly throws on macOS for `opencv-yunet`, `opencv-dnn`, `yolov8-face`, and `haar-cascade`.
3. The UI view models only offer Ollama or `center` on macOS.

So the present limitation is partly upstream runtime support, and partly a project-level policy that hard-disables OpenCV on macOS.

## What "New OpenCV on Mac" Actually Means

For this project, "new OpenCV on macOS" does not mean upgrading the managed `OpenCvSharp4` package alone.

The real requirement is a working Apple Silicon native runtime that is compatible with the OpenCvSharp wrapper used by this codebase.

## Upstream Status

### 1. OpenCvSharp4 (official)
- `OpenCvSharp4` itself is only the managed wrapper.
- The official repo currently documents native runtime packages for Windows, Linux, Linux ARM, and WASM.
- The official package list does not present a current stable `osx-arm64` runtime alongside those platforms.
- There is an older prerelease package `OpenCvSharp4.runtime.osx_arm64`, but it is stale and not aligned with the version used in this repo.
- Practical conclusion: official OpenCvSharp support for Apple Silicon is not a clean drop-in path here.

### 2. Third-party runtime option
- A third-party package exists: `Sdcb.OpenCvSharp4.mini.runtime.osx-arm64`.
- This is the most realistic way to try OpenCV on Apple Silicon without writing native code from scratch.
- It is still a runtime strategy change, not a simple upgrade.
- We would need to validate compatibility with:
  - `CvDnn.ReadNetFromCaffe`
  - `CvDnn.ReadNetFromOnnx`
  - `Net.Forward`
  - the current YuNet and DNN code paths

### 3. Apple Vision Framework
- This is the best native macOS option long term.
- It avoids OpenCvSharp runtime fragility entirely.
- It likely gives better reliability on Apple Silicon than trying to force desktop OpenCV bindings into the app.

## Performance / Product Impact

The current macOS path uses Ollama vision models such as `qwen3-vl` and `llava:7b`.

That is functional, but much slower than local CV inference. The project documentation and test notes already reflect a large speed gap between OpenCV-style inference and LLM vision inference.

## .vscode Findings

The `.vscode` folder does not currently solve or influence the macOS OpenCV problem.

What it does today:

- `tasks.json` builds and publishes `osx-arm64`
- `launch.json` debugs CLI and GUI
- `settings.json` only auto-approves `dotnet run`

What it does not do:

- add a macOS OpenCV runtime package
- copy Homebrew OpenCV libraries
- set `DYLD_LIBRARY_PATH`
- configure native probing paths

## Decision

The recommended solution for this project is:

- Use **Apple Vision** for face detection on macOS.
- Keep **OpenCV** as-is on Windows.
- Keep **Ollama** only as a fallback, not the default macOS path.

This is the best fit for the stated goal:

- fast face detection on Mac
- no broad NuGet/runtime churn
- minimal risk to the rest of the app

## Conclusion

Yes, fast native face detection on macOS is achievable, but the safest path is **not** "upgrade OpenCV on Mac."

The safest interpretation is:

- **No**, there is not a clean official OpenCvSharp Apple Silicon upgrade path already wired into this repo.
- **Yes**, we can add fast macOS face detection by using Apple's native Vision framework behind the existing `IFaceDetectionService` interface.

## Recommended Path

### Option A: Experimental OpenCV on macOS

Try this if the goal is to keep the existing OpenCV YuNet / DNN implementation:

1. Add a macOS ARM64 runtime package compatible with OpenCvSharp.
2. Remove the hard macOS block in the face-detection factory.
3. Re-enable OpenCV options in the macOS UI model lists.
4. Add a macOS smoke test that verifies:
   - `Cv2.ImRead`
   - YuNet ONNX load
   - DNN Caffe load
   - one real face-detection pass

This is the fastest path to learn whether the runtime is good enough, but it is **not** the lowest-risk product path.

### Option B: Native Apple Vision implementation (Recommended)

Try this if the goal is a stable product-quality macOS solution:

1. Implement a dedicated macOS face-detection service using Apple Vision.
2. Keep OpenCV on Windows/Linux.
3. Keep Ollama as a slower fallback.
4. Route macOS to Apple Vision in the face-detection factory.
5. Update the macOS UI defaults to prefer Apple Vision over Ollama.

This is a cleaner architecture than forcing OpenCV runtimes on Apple Silicon.

## Why Apple Vision Is The Best Fit Here

Apple Vision matches the product constraints better than a macOS OpenCV retrofit:

- It is a native Apple framework, so it is the most likely to perform well on Apple Silicon.
- It can be introduced as a **macOS-only implementation** behind `IFaceDetectionService`.
- It does not require changing the existing Windows OpenCV package setup.
- It avoids betting the app on unofficial or stale macOS OpenCvSharp runtime packages.
- It limits blast radius: Windows and Linux behavior can remain unchanged.

## Expected App Impact

If Apple Vision is implemented correctly, the impact should stay local to the face-detection layer.

Expected touch points:

- new macOS-specific face detection service
- small factory update
- small macOS UI default update
- tests for macOS detection behavior

Areas that should **not** need to change:

- existing Windows OpenCV flow
- existing OpenCvSharp NuGet setup for Windows
- portrait generation logic outside the face-detection service contract
- general CLI/UI architecture

## References

- OpenCvSharp repo: https://github.com/shimat/opencvsharp
- Official OpenCvSharp runtime package list: https://github.com/shimat/opencvsharp
- Stale Apple Silicon prerelease runtime: https://www.nuget.org/packages/OpenCvSharp4.runtime.osx_arm64
- Third-party Apple Silicon runtime: https://www.nuget.org/packages/Sdcb.OpenCvSharp4.mini.runtime.osx-arm64
- Apple Vision Framework: https://developer.apple.com/documentation/vision
