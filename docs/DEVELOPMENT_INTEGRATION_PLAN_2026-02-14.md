# Development Integration Plan (2026-02-14)

Base commit for current work split:
- `adcb9c47a106e1f0582ac8c8152ed888973a71b3` (merge PR #1)

## Current Strategy

- Keep `main` stable.
- Integrate ongoing work into `development` first.
- Use PRs from feature branches into `development`.
- Test each branch after merge (or before merge when needed).
- Merge `development` to `main` only when validated.

## Branches to Integrate into `development`

1. `feature/generate-quality-fix`
2. `feature/validation-config-parity`
3. `feature/cli-size-profiles`
4. `feature/ui-size-profile-integration`
5. `planning/ppm-parity-roadmap` (docs/ops support)

## Suggested Integration/Test Order

1. **generate-quality-fix**
   - Validate image quality improvements
   - Compare explicit `opencv-dnn` vs default `llava:7b`
   - Check output format behavior and memory/perf stability

2. **validation-config-parity**
   - Validate per-team CSV config support
   - Validate command timeout handling
   - Validate continue-on-error behavior and summary JSON output

3. **cli-size-profiles**
   - Validate `--sizeProfile`
   - Validate `--allSizes`
   - Validate output profile alias and env override behavior

4. **ui-size-profile-integration**
   - Validate UI profile controls
   - Confirm manual size controls disable/enable correctly
   - Confirm command generation alignment

5. **planning/ppm-parity-roadmap**
   - Keep docs/ops tooling aligned with actual process

## Notes

- `development` is currently unprotected by branch rules (acceptable for now per user decision).
- Existing open PRs targeting `main` should be mirrored/re-targeted to `development` for this integration cycle.
