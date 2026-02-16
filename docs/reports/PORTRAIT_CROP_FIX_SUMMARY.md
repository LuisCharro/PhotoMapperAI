# Portrait Crop Fix - Summary

## Issue (2026-02-11)

**Problem:** When using `--faceDetection center`, generated portraits showed too much body:
- Included chest + beginning of legs
- Expected: head + neck + bit of chest only
- Comparison: `/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Generated/Switzerland/1133408.jpg` (expected) vs. `Output_Portraits/9741690.jpg` (actual)

## Root Cause

The center crop fallback mode was using the **geometric center** of the image:
```csharp
// OLD CODE (wrong)
var cropY = (imageHeight - cropHeight) / 2;  // Center vertically
```

This works for centered faces but fails for full-body sports photos where the face is in the **upper portion** of the image.

## Fix

Updated `ImageProcessor.CalculatePortraitCrop()` to use **upper-body crop**:

```csharp
// NEW CODE (correct)
var cropY = (int)(imageHeight * 0.2) - (cropHeight / 2); // Start at 20% from top
cropY = Math.Max(0, cropY); // Ensure we don't go negative
```

**Behavior:**
- Crops from top 20% of image (not geometric center)
- Produces head + neck + bit of chest
- Optimized for full-body sports photos

## Files Changed

1. **`src/PhotoMapperAI/Services/Image/ImageProcessor.cs`**
   - Fixed `CalculatePortraitCrop()` Case 4 (no face detected fallback)

2. **`README.md`**
   - Added note about portrait crop behavior
   - Clarified output file naming (PlayerId, not ExternalId)

3. **`docs/FACE_DETECTION_GUIDE.md`**
   - Added detailed section explaining center vs. AI crop modes
   - Included visual diagram of upper-body crop

4. **`CHANGELOG.md`**
   - Added v1.0.1 entry documenting the fix

## Testing

### Before Fix (Center Mode)
```
Input: Full-body photo (800x1200)
Crop: Geometric center (400x600 from middle)
Result: Chest + legs included (too much body)
```

### After Fix (Center Mode)
```
Input: Full-body photo (800x1200)
Crop: Upper portion (400x600 from 20% from top)
Result: Head + neck + bit of chest (correct)
```

### Command to Test
```bash
cd /Users/luis/Repos/PhotoMapperAI/src/PhotoMapperAI
dotnet run -- generatePhotos \
  --inputCsvPath /Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/players_test.csv \
  --photosDir /Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Spain \
  --processedPhotosOutputPath /Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Output_Portraits \
  --format jpg \
  --faceDetection center \
  --noCache
```

## Output File Naming Clarification

**Important:** Portrait files are named by **PlayerId** (internal system ID), not ExternalId (FIFA photo ID).

Example:
- CSV: `PlayerId: 9741690, ExternalId: 250005992`
- Photo filename: `Esther_Gonzalez Rodriguez_250005992.jpg`
- Output portrait: `9741690.jpg` (PlayerId)

This matches the expected output in `Generated/Switzerland/` directory.

## Related Documentation

- `README.md` - Main documentation with portrait crop behavior notes
- `docs/FACE_DETECTION_GUIDE.md` - Detailed face detection and crop explanation
- `CHANGELOG.md` - Version history (v1.0.1 entry)

## Next Steps

1. Test the fix with your actual sports photos
2. Compare `Output_Portraits/` with `Generated/Switzerland/` for correctness
3. If crop position needs adjustment, modify the `0.2` value in `ImageProcessor.cs`:
   - `0.15` = Higher up (more headroom)
   - `0.25` = Lower down (more chest)
   - Default: `0.2` (recommended for full-body sports photos)
