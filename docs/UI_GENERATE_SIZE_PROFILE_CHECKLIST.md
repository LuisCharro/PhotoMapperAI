# UI Generate Step — Size Profile Checklist

Use this checklist to validate the new Generate Step behavior in the desktop UI.

## Preconditions
- Build UI successfully:
  - `dotnet build src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj`
- Have valid inputs ready:
  - mapped CSV
  - source photos folder
  - output folder
- Optional size profile file:
  - `samples/size_profiles.default.json`

## Test 1 — Manual single size (no profile)
- [ ] Leave size profile empty
- [ ] Verify Width/Height controls are enabled
- [ ] Run generation
- [ ] Verify output in selected output directory

Expected:
- Generation uses manual Width/Height values.

## Test 2 — Profile first variant mode
- [ ] Select a size profile JSON
- [ ] Verify Width/Height controls become disabled
- [ ] Verify "Generate all profile sizes" becomes enabled
- [ ] Keep "Generate all" unchecked
- [ ] Run generation

Expected:
- Generation runs one variant (first profile variant)
- Output goes to base output folder

## Test 3 — Profile all sizes mode
- [ ] Keep size profile selected
- [ ] Enable "Generate all profile sizes"
- [ ] Run generation

Expected:
- Generation runs each variant
- Output is created in per-variant subfolders under base output folder

## Test 4 — Output profile alias
- [ ] Set Output Profile to `test` (or `prod`)
- [ ] Run generation

Expected:
- Output path resolves to profile path logic (env override if present, else `<base>/test` or `<base>/prod`).

## Test 5 — Cancel behavior
- [ ] Start generation on non-trivial dataset
- [ ] Click Cancel

Expected:
- UI shows cancellation status
- No crash, app remains responsive

## Quick pass criteria
- [ ] No runtime exceptions in UI log
- [ ] Correct output path behavior for manual/profile/all-sizes/profile-alias modes
- [ ] Portrait count summary displayed at completion
