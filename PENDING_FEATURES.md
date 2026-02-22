# Pending Features for PhotoMapperAI

This document tracks features that are planned or under consideration for future implementation.

**Last Updated:** 2026-02-22

---

## High Priority

### 1. Batch Automation View ✅ IMPLEMENTED

**Description:** New GUI view for processing multiple teams in one run with combined Extract→Map→Generate workflow.

**Implementation:**
- New `BatchAutomationView.axaml` and `BatchAutomationViewModel.cs`
- Team list management with CSV/Database import
- Progress tracking across all teams
- Session state persistence for batch runs
- Select/deselect teams for processing
- Photo directory validation

**Status:** ✅ Implemented (2026-02-22)

---

### 2. Filename Pattern Presets ✅ IMPLEMENTED

**Description:** Save and reuse filename patterns with user-friendly placeholders.

**Implementation:**
- New placeholders: `{first}`, `{last}`, `{id}` (clearer naming)
- Backward compatible with legacy `{sur}`, `{family}` placeholders
- Presets stored in `appsettings.local.json`
- GUI: Preset dropdown with Save/New buttons
- Default presets: Auto-detect, FirstName_LastName_ID, etc.

**Status:** ✅ Implemented (2026-02-22)

---

## Medium Priority

### 3. Placeholder Image Support

**Description:** When a player has no photo available, copy a placeholder image to the output directory instead of skipping the player entirely.

**Use Cases:**
- Maintaining complete output sets
- Visual indication of missing photos
- Consistent output structure

**Implementation:**
- Added `placeholderPath` to SizeVariant (per-variant placeholders)
- Added `--placeholderImage` CLI option for single-size mode
- GUI checkbox "Use placeholder images" when using size profile
- Placeholders are pre-sized images that get copied directly (no resizing)
- Size profile defines placeholderPath per variant:
  ```json
  {
    "variants": [
      { "key": "small", "width": 34, "height": 50, "placeholderPath": "./placeholder-34x50.jpg" },
      { "key": "medium", "width": 67, "height": 100, "placeholderPath": "./placeholder-67x100.jpg" }
    ]
  }
  ```

**Status:** ✅ Implemented (2026-02-16)

---

## Low Priority

### 4. Output Profiles Configuration

**Description:** Currently output profiles (`test` and `prod`) are configured via environment variables (`PHOTOMAPPER_OUTPUT_TEST` and `PHOTOMAPPER_OUTPUT_PROD`). This feature would add support for configuring output profiles in `appsettings.json`, similar to how the legacy tool used XML config files (`TestOutputFolderConfig.xml` and `ProdOutputFolderConfig.xml`).

**Legacy Approach (PlayerPortraitManager):**
- `ConfigurationData/TestOutputFolderConfig.xml` → `C:\PlayerPortraitSportApiImages`
- `ConfigurationData/ProdOutputFolderConfig.xml` → `\\stxts00011.media.int\SportApiImages`

**Current Approach (PhotoMapperAI):**
- Environment variables `PHOTOMAPPER_OUTPUT_TEST` and `PHOTOMAPPER_OUTPUT_PROD`

**Proposed Enhancement:**
Add `OutputProfiles` section to `appsettings.json`:
```json
{
  "OutputProfiles": {
    "test": "C:\\PlayerPortraitSportApiImages",
    "prod": "\\\\stxts00011.media.int\\SportApiImages"
  }
}
```

This would allow users to configure all output profiles in one place, matching the legacy tool's approach.

**Status:** ⚠️ Under consideration - Environment variables currently work, but config file approach would be more user-friendly

---

## Implemented Features

The following features are already implemented and working:

### ✅ Single Player Processing (2026-02-15)
Generate portraits for a specific player by ID:
```bash
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits --onlyPlayer 12345
```
Or use the GUI filter field in the Generate step.

### ✅ Placeholder Image Support (2026-02-16)
When a player has no photo available, use a placeholder image. Placeholders are pre-sized images that get copied directly (no resizing):

```json
// Size profile with per-variant placeholders
{
  "variants": [
    { "key": "small", "width": 34, "height": 50, "placeholderPath": "./placeholder-34x50.jpg" },
    { "key": "medium", "width": 67, "height": 100, "placeholderPath": "./placeholder-67x100.jpg" },
    { "key": "large", "width": 100, "height": 150, "placeholderPath": "./placeholder-100x150.jpg" },
    { "key": "x200x300", "width": 200, "height": 300, "placeholderPath": "./placeholder-200x300.jpg" }
  ]
}
```

```bash
# Via size profile (recommended - uses placeholderPath from each variant)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -sizeProfile samples/size_profiles.default.json -allSizes

# Via CLI option (single size mode)
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -placeholderImage ./placeholder-200x300.jpg
```

In GUI: Check "Use placeholder images" when using a size profile to enable placeholder copying.

### ✅ PNG to JPG Transparency Handling (2026-02-16)
When converting transparent PNG images to JPG format, transparent areas are now filled with white background instead of becoming black:
```bash
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg
```
The system automatically detects transparency in PNG images and applies white background fill before saving as JPG, ensuring better visual appearance.

### ✅ Intelligent Face/Eye Centering
The portrait cropping automatically uses the best available centering method:
- Both eyes detected → Center on eye midpoint
- One eye detected → Estimate center from detected eye
- Only face detected → Estimate eye position (upper 40% of face)
- No face detected → Use upper portion of image (appropriate for full-body photos)

No manual `--centerOn` option needed - the system intelligently selects the best method.

### ✅ Size Profiles with Output Subfolders
Generate multiple portrait sizes in one run with automatic subfolder organization:
```bash
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -sizeProfile samples/size_profiles.default.json -allSizes
```

### ✅ Output Profile Shortcuts
Quick switching between test and production output locations:
```bash
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -outputProfile test
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -outputProfile prod
```
Configure via `PHOTOMAPPER_OUTPUT_TEST` and `PHOTOMAPPER_OUTPUT_PROD` environment variables.

### ✅ Debug Artifacts
Write debug information for troubleshooting:
```bash
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits --writeDebugArtifacts
```

---

## Feature Summary Table

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| Batch Automation View | High | ✅ Implemented | Process multiple teams in one run |
| Filename Pattern Presets | High | ✅ Implemented | `{first}`, `{last}`, `{id}` placeholders |
| AI Model Selection Tiers | High | ✅ Implemented | Free Tier, Local, Paid tabs |
| Single Player Processing | High | ✅ Implemented | CLI `--onlyPlayer` + GUI filter field |
| PNG→JPG Transparency | High | ✅ Implemented | White background fill for transparent PNGs |
| Placeholder Images | Medium | ✅ Implemented | CLI `--placeholderImage` + size profile |
| Output Profiles Config | Low | ⚠️ Consideration | Add to appsettings.json (like old XML files) |
