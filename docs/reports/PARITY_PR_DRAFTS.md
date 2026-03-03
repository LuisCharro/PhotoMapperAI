# Parity PR Drafts (Ready to Paste)

## PR 1 — `feature/generate-quality-fix`

**Title**
`fix(image): improve portrait output quality and format correctness`

**Summary**
- Improve portrait resizing quality (high-quality sampler + safer crop behavior)
- Ensure requested output format is honored (`png`/`jpg`)
- Reduce memory pressure during generation by disposing image buffers deterministically
- Add regression tests for image sizing and PNG output signature

**Validation**
- `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj`
- `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~ImageProcessorTests"`

**Notes**
- Backward-compatible CLI behavior maintained
- No external/private dataset added to repo

---

## PR 2 — `feature/validation-config-parity`

**Title**
`feat(validation): harden external parity validation workflows and reporting`

**Summary**
- Add team-specific source CSV support for legacy-ID parity runs
- Add per-command timeouts for map/generate
- Add continue-on-error mode and per-team error status in report
- Add richer report metrics (coverage %, common/missing/unexpected IDs, avg file size)
- Add optional machine-readable summary JSON output
- Add helper script `scripts/compare_portrait_sets.py` for quick parity comparisons

**Validation**
- `python3 -m py_compile scripts/run_external_validation.py`
- `python3 -m py_compile scripts/compare_portrait_sets.py`
- `python3 scripts/run_external_validation.py --config samples/external_validation.realdata_parity.template.json --dry-run`

**Notes**
- External data remains out of repo
- Improves reproducibility and failure isolation

---

## PR 3 — `feature/cli-size-profiles`

**Title**
`feat(cli): add size-profile parity support for multi-size portrait generation`

**Summary**
- Add size profile model + JSON loader (`SizeProfile`, `SizeVariant`)
- Add sample profile (`samples/size_profiles.default.json`)
- Add `--sizeProfile` and `--allSizes` for `generatephotos`
- Add `--outputProfile test|prod` alias with env overrides
- Keep single-size mode backward-compatible
- Add tests for size profile loader and output profile resolver (+ env override behavior)

**Validation**
- `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj`
- `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj --filter "FullyQualifiedName~SizeProfileLoaderTests|FullyQualifiedName~OutputProfileResolverTests"`

**Notes**
- Designed as CLI-first contract for later UI parity

---

## PR 4 — `feature/ui-size-profile-integration`

**Title**
`feat(ui): integrate size profile/all-sizes/output-profile in Generate step`

**Summary**
- Add Generate UI controls for:
  - Size profile path
  - Generate all profile sizes
  - Output profile selector (`none/test/prod`)
- Wire generation logic to run single/profile/all-variant flows
- Disable manual width/height when profile mode is active
- Align session model with new generate options
- Add UI manual validation checklist doc
- Update GUIDE.md for new defaults and flow

**Validation**
- `dotnet build src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj`
- Run checklist: `docs/UI_GENERATE_SIZE_PROFILE_CHECKLIST.md`

**Notes**
- Behavior remains compatible for users not using size profiles

---

## Recommended merge order
1. `feature/generate-quality-fix`
2. `feature/validation-config-parity`
3. `feature/cli-size-profiles`
4. `feature/ui-size-profile-integration`
