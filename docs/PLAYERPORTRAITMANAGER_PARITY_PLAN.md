# PlayerPortraitManager → PhotoMapperAI Parity Plan

**Date:** 2026-02-13  
**Branch:** `planning/ppm-parity-roadmap`  
**Goal:** Reach feature parity with legacy CLI first, then expose parity cleanly in UI.

---

## 1) Current comparison (old vs new)

### Legacy tool (`PlayerPortraitManager`)
Main flow:
- `GetCsvWithCesimPlayers`
- `MapPlayers`
- `GeneratePlayersPhotos`

Key generate capabilities found:
- Multi-size generation from config (`--Sizes` XML)
- Named environments for output (`--OutputPhotosFolder TEST|PROD|path`)
- Fallback to default sizes config (`ConfigurationData/SizePhotosConfig.xml`)
- Single-size direct override (`--Width`, `--Height`)
- Optional `--SaveAs` format

### New tool (`PhotoMapperAI`)
Main flow:
- `extract`
- `map`
- `generatephotos`

Generate currently supports:
- Single size per run (`--faceWidth`, `--faceHeight`)
- Model/crop/parallel/cache options
- UI wizard wrapping command logic

Gap to close:
- **No legacy-style multi-size profile generation in one run**
- **No profile names (like TEST/PROD or named size sets)**

---

## 2) Architecture decision

## Decision: **CLI contract first, UI second**

Why:
1. UI already depends on command logic; stable CLI contract reduces rework.
2. Legacy parity requirement is mostly generation behavior (domain logic), not UI widgets.
3. Easier testability and CI coverage in CLI/service layer first.

Rule:
- Add parity features to command/service layer first.
- UI only consumes already-tested command/service options.

---

## 3) Proposed target design for size handling

Use **both** approaches:

### A) Explicit dimensions (keep current)
- `--faceWidth`, `--faceHeight`

### B) Named size profiles (new)
- `--sizeProfile <name|path>`
  - `default` profile supported out-of-box
  - custom JSON config path supported

### C) Generate all sizes in one run (new)
- `--allSizes` flag
  - runs generation for every configured size entry
  - writes to per-size output subfolders or configured destinations

### D) Optional profile env alias (new)
- `--outputProfile <test|prod>` (optional)
  - maps to output roots from appsettings config
  - still allow explicit `--processedPhotosOutputPath`

---

## 4) Recommended execution order (phased)

### Phase 1 — CLI parity foundation
1. Add size profile model (`SizeProfile`, `SizeVariant`)
2. Add config loader (JSON in new tool; keep simple and typed)
3. Extend `generatephotos` options:
   - `--sizeProfile`
   - `--allSizes`
   - `--outputProfile` (optional)
4. Refactor `GeneratePhotosCommandLogic` to iterate variants when `--allSizes` is set
5. Keep current single-size behavior backward-compatible

**Deliverable:** `photomapperai generatephotos` can reproduce old multi-size workflow.

### Phase 2 — Tests and parity validation
1. Unit tests: config parsing + variant resolution
2. Integration tests: one-run multi-size output generation
3. Golden sample comparison with legacy dimensions/naming behavior
4. Update README and GUIDE with migration examples

**Deliverable:** confidence that parity is real, not just implemented.

### Phase 3 — UI integration
1. Add "Size mode" in Step 3:
   - Single size
   - Profile (named/path)
   - All sizes
2. Add profile selector and preview of variants
3. Wire UI to same command logic options
4. Keep defaults simple for non-advanced users

**Deliverable:** GUI can drive parity features without custom duplicated logic.

---

## 5) Branch strategy

### Trunk
- `main` stable

### Planning branch
- `planning/ppm-parity-roadmap` (this branch)

### Implementation branches (suggested)
1. `feature/cli-size-profiles`
   - config model + parser + CLI options
2. `feature/generate-multisize-logic`
   - command logic + output structure
3. `feature/parity-tests-and-docs`
   - tests + README/GUIDE migration section
4. `feature/ui-size-profile-integration`
   - Step 3 controls + bindings

Merge policy:
- Small PRs per branch
- Each PR must keep existing single-size flow working

---

## 6) Acceptance criteria (final)

1. A single command can generate multiple portrait sizes from profile config.
2. Legacy equivalent sizes (e.g., 34x50, 67x100, 100x150, 200x300) can be produced in one run.
3. UI supports selecting single size vs profile/all sizes.
4. Existing scripts and current CLI args remain backward-compatible.
5. Docs clearly explain migration from old tool to new tool.

---

## 7) Immediate next step

Start with **Phase 1 / branch `feature/cli-size-profiles`**.

First implementation slice:
- add `SizeProfile` config file schema
- add `--sizeProfile` option and load/validate it
- keep generation single-size for this first slice (no `--allSizes` yet)

Then incremental PR for `--allSizes`.
