# Parity Execution Tracker (Legacy → New)

Use this file as the practical checklist while implementing `PLAYERPORTRAITMANAGER_PARITY_PLAN.md`.

## Current active branches

- `feature/generate-quality-fix` ✅ active
  - quality resize/crop improvements
  - default face detection stabilized to `llava:7b`
  - image disposal + regression tests
- `feature/validation-config-parity` ✅ active
  - external validation resilience
  - team-specific source CSV support
  - timeout + continue-on-error + richer report metrics

---

## Phase 1 — CLI parity foundation

### 1.1 Size profile model + loader
- [ ] Add models: `SizeProfile`, `SizeVariant`
- [ ] Add JSON schema/template in `samples/`
- [ ] Validate profile (non-empty, positive dimensions)
- Branch: `feature/cli-size-profiles`

### 1.2 CLI options (non-breaking)
- [ ] Add `--sizeProfile <name|path>`
- [ ] Add `--allSizes`
- [ ] Add optional `--outputProfile <test|prod>`
- [ ] Keep current single-size path unchanged
- Branch: `feature/cli-size-profiles`

### 1.3 Generate command logic
- [ ] Iterate variants when `--allSizes` is set
- [ ] Keep output naming by `PlayerId`
- [ ] Ensure no stretch regressions in all variants
- Branch: `feature/generate-multisize-logic`

---

## Phase 2 — validation and confidence

### 2.1 Tests
- [ ] Unit tests for profile parsing and resolution
- [ ] Integration test for multi-size generation in one run
- [ ] Regression: output dimensions + format correctness
- Branch: `feature/parity-tests-and-docs`

### 2.2 Real-data parity checks (external dataset)
- [ ] Run Spain + Switzerland with team-specific source CSVs
- [ ] Compare against expected portraits using helper scripts
- [ ] Store report summaries (no external raw data committed)
- Branch: `feature/validation-config-parity`

---

## Phase 3 — UI integration

### 3.1 Generate step controls
- [ ] Add size mode toggle: single/profile/all
- [ ] Add profile selector / path input
- [ ] Preview resolved size variants in UI
- Branch: `feature/ui-size-profile-integration`

### 3.2 UI command wiring
- [ ] Pass profile options to same command logic
- [ ] Keep existing defaults for non-advanced users
- Branch: `feature/ui-size-profile-integration`

---

## Merge sequence recommendation

1. `feature/generate-quality-fix`
2. `feature/validation-config-parity`
3. `feature/cli-size-profiles`
4. `feature/generate-multisize-logic`
5. `feature/parity-tests-and-docs`
6. `feature/ui-size-profile-integration`

---

## Done definition (release-ready parity)

- [ ] Multi-size generation in one run implemented and tested
- [ ] Legacy-equivalent sizes generated correctly
- [ ] GUI supports size profile workflow
- [ ] External parity validation reports are reproducible
- [ ] README/GUIDE updated with migration examples
