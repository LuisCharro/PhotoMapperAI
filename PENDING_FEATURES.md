# Pending Features for PhotoMapperAI

This document tracks features that are planned or under consideration for future implementation.

**Last Updated:** 2026-02-15

---

## High Priority

### 1. Single Player Processing Option

**Description:** Add the ability to generate portraits for a specific player by ID, rather than processing all players in the CSV.

**Use Cases:**
- Testing and debugging individual players
- Re-processing failed portraits
- Selective updates without full regeneration

**Implementation:**
- CLI: `--onlyPlayer <PlayerId>` parameter in `GeneratePhotosCommand.cs`
- GUI: Filter field in `GenerateStepViewModel.cs` / `GenerateStepView.axaml`
- Supports both internal PlayerId and ExternalId for filtering

**Status:** ✅ Implemented (2026-02-15)

---

### 2. PNG to JPG Transparency Handling

**Description:** When converting transparent PNG images to JPG format, transparent areas become black by default. The expected behavior is to replace transparent areas with a white background for better appearance.

**Implementation:**
- Modified `ImageProcessor.cs` to detect transparent PNG sources
- Added `HasTransparency()` method to check for alpha channel
- Added `FillTransparentAreasWithWhite()` method to apply white background fill before saving as JPG

**Status:** ✅ Implemented (2026-02-16)

---

## Medium Priority

### 3. Placeholder Image Support

**Description:** When a player has no photo available, copy a placeholder image to the output directory instead of skipping the player entirely.

**Use Cases:**
- Maintaining complete output sets
- Visual indication of missing photos
- Consistent output structure

**Implementation Needed:**
- Add placeholder path configuration to size profiles
- Copy placeholder when no source photo exists

**Status:** ❌ Not implemented

---

## Low Priority

### 4. Absolute Destination Paths in Size Profiles

**Description:** Currently size profiles use relative subfolder paths. Adding support for absolute destination paths could enable direct output to network locations.

**Current Format:**
```json
{
  "variants": [
    { "key": "small", "width": 34, "height": 50, "outputSubfolder": "small" }
  ]
}
```

**Potential Enhancement:**
```json
{
  "variants": [
    { "key": "small", "width": 34, "height": 50, "outputSubfolder": "small", "destinationPath": "\\\\server\\images\\small" }
  ]
}
```

**Status:** ⚠️ Under consideration - Current relative paths sufficient for most use cases

---

## Implemented Features

The following features are already implemented and working:

### ✅ Single Player Processing (2026-02-15)
Generate portraits for a specific player by ID:
```bash
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits --onlyPlayer 12345
```
Or use the GUI filter field in the Generate step.

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
| Single Player Processing | High | ✅ Implemented | CLI `--onlyPlayer` + GUI filter field |
| PNG→JPG Transparency | High | ✅ Implemented | White background fill for transparent PNGs |
| Placeholder Images | Medium | ❌ Pending | Size profile enhancement |
| Absolute Destination Paths | Low | ⚠️ Consideration | Relative paths sufficient for now |
