# Model Benchmarks

Benchmark findings captured from `benchmark-results/*.json`.

Last updated: 2026-02-12

## Name Matching

Primary run used for conclusions:
- Source file: `benchmark-results/benchmark-20260211-112436.json`
- Model: `qwen2.5:7b-instruct-q4_K_M`
- Test set size: 10 name pairs

### Summary

| Metric | Value |
|---|---|
| Accuracy | 0.90 |
| Correct Matches | 9/10 |
| Avg Processing Time | 3216.9 ms |
| Avg Confidence | 0.9389 |
| Duration | 32182 ms |

### Key Observations

- Accuracy improved significantly versus earlier runs (0.20 to 0.25 in prior snapshots).
- One false positive remained (`Luis García` vs `Luis Martínez`) at confidence `0.85`.
- Accuracy is strong enough for assisted mapping workflows, but threshold tuning and extra validation logic are still recommended.

## Face Detection

Most recent face benchmark:
- Source file: `benchmark-results/benchmark-20260212-075152.json`
- Model: `opencv-dnn`

### Summary

| Metric | Value |
|---|---|
| Accuracy | 0.25 |
| Test Count | 4 |
| Avg Processing Time | 16 ms |
| Avg Confidence | 0.998 |

### Key Observations

- Face benchmark pipeline now executes on macOS after OpenCV runtime dependency fixes in build output layout.
- Current dataset is still very small (4 images), so this is only a smoke baseline.
- Benchmarks now support explicit expected-face labels via `tests/Data/FaceDetection/face_expected.csv`.
- Next pass should expand benchmark images and validate the same run on Windows 11.

## Historic Snapshot

Older benchmark files (for traceability) show:
- Early name-model runs often returned confidence `0` and low accuracy (0.20 to 0.25).
- OpenCV face benchmark repeatedly failed initialization in multiple timestamps.

These historical runs are useful for regression tracking but should not be used as current quality baselines.

## Next Benchmark Actions

1. Run face benchmark on Windows 11 and compare using:
   `photomapperai benchmark-compare --baseline benchmark-results/benchmark-20260212-075152.json --candidate benchmark-results/<windows-file>.json --faceModel opencv-dnn`
2. Expand benchmark dataset beyond 10 name pairs and include harder multilingual cases.
3. Add benchmark runs for additional models listed in the plan (`qwen3:8b`, `llava:7b`, and face fallbacks).
4. Version benchmark datasets to make results reproducible across machines.
