# Preview/Crop Cross-Platform Reliability Roadmap

## Context

Current issue observed by Luis:

- **Step 5 Preview** in GUI does not consistently match expected portrait framing (e.g. `1079659.jpg` expected output) on macOS.
- Historical demos on Windows 11 looked correct with the same `opencv-dnn` selection.
- CLI-generated output can match expected, while GUI preview can diverge when runtime/model loading differs.

This suggests a **cross-platform runtime consistency issue**, not only crop math.

---

## Known Symptoms

1. Preview appears too close/too low (chin cut).
2. In some runs, fallback-like behavior appears (face detection unavailable or degraded).
3. Behavior differs by OS/runtime environment, despite same nominal detector (`opencv-dnn`).

---

## Working Assumption

Core crop code is mostly correct when input landmarks are correct.
The unstable part is likely one of:

- native runtime loading for OpenCV dependencies,
- model file resolution at runtime,
- preview path not being perfectly identical to final generation path,
- silent fallback behavior hiding detector failures.

---

## Roadmap (phased)

## Phase 1 — Diagnostics First (must-have)

### 1.1 Add explicit detector diagnostics in UI preview flow
For each preview run, log:

- face detection model requested (`opencv-dnn`),
- actual detector initialized (success/failure),
- resolved model paths (`prototxt`, `caffemodel`),
- native OpenCV runtime load status,
- detected face rectangle + confidence,
- computed crop rectangle before/after offsets.

### 1.2 Add strict-mode toggle for debugging
When enabled:

- fail preview with clear error if detector is unavailable,
- **do not silently fallback** to center crop.

Goal: eliminate “looks wrong but no visible error”.

---

## Phase 2 — Deterministic Preview = Final Output Pipeline

### 2.1 Unify implementation path
Refactor preview generation so it uses the exact same processing path as final generate output for one player:

- same detector creation,
- same crop function,
- same image encode step,
- same offset preset handling.

### 2.2 Add one-player parity check command
Add internal/debug command to generate one player from GUI settings and compare:

- preview image hash (or pixel diff tolerance),
- generated output hash.

Goal: preview and final output cannot drift silently.

---

## Phase 3 — Cross-Platform Runtime Hardening

### 3.1 Native dependencies packaging validation
At startup (or diagnostics command), verify expected native dependencies are resolvable on current OS.

### 3.2 Deterministic model discovery
Ensure model resolution always picks a directory containing both required files:

- `res10_ssd_deploy.prototxt`
- `res10_300x300_ssd_iter_140000.caffemodel`

If invalid: fail early with actionable message.

### 3.3 Environment report
Add `--diagnose` output (CLI + optional GUI export) with:

- OS + architecture + .NET runtime,
- detector model and resolved paths,
- runtime native load checks,
- preview/final parity snapshot for one known sample.

---

## Phase 4 — Regression Safety Net

### 4.1 Golden dataset
Maintain a small fixed dataset (including Spain sample and known expected portraits).

### 4.2 CI matrix
Run golden generation on:

- Windows,
- macOS.

Compare outputs (hash or thresholded image diff). Fail build on unacceptable drift.

### 4.3 Track known acceptable deltas
If exact binary equality is not always realistic due to codec/platform nuances, define tolerance thresholds and document them.

---

## Immediate Next Actions

1. Implement Phase 1.1 diagnostics in `GenerateStepViewModel.GeneratePreview()` and log panel.
2. Add strict-mode switch in Step 5 for debugging sessions.
3. Implement Phase 2.1 preview/final pipeline unification.
4. Validate with player `1079659` against expected `200x300` sample on macOS.

---

## Notes

- This roadmap assumes `opencv-dnn` remains the canonical detector across OS.
- If parity still fails after Phase 2, inspect detector output differences first (face rect/confidence), not only crop constants.
