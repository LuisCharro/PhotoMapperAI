# PhotoMapperAI - Common Edge Cases and Solutions

This document describes common edge cases encountered when using PhotoMapperAI and their solutions.

## Name Matching Edge Cases

### Case 1: Name Transliteration Differences

**Problem:** Names with different transliterations (e.g., "José" vs "Jose", "Müller" vs "Mueller").

**Solution:** String matching normalizes accents before comparison.

**Example:**
- Photo name: "Rodriguez Martinez"
- Database name: "Rodríguez Martínez"
- Result: ✅ Matched (normalization removes accents)

### Case 2: Name Order Variations

**Problem:** Names in different orders (First Last vs Last First).

**Example:**
- Photo name: "Martinez Rodriguez"
- Database name: "Rodríguez Martínez"
- Result: ✅ Matched (word-set comparison handles order)

**Solution:** Use word-set comparison which ignores word order.

### Case 3: Missing Middle Names

**Problem:** Photo has middle name, database doesn't (or vice versa).

**Example:**
- Photo name: "Juan Carlos Rodriguez"
- Database name: "Juan Rodriguez"
- Result: ✅ Matched (AI handles partial matches with confidence)

**Solution:** AI name matching provides confidence scores to indicate partial matches.

### Case 4: Common Nicknames

**Problem:** Names use common nicknames (e.g., "Pep" for "Josep", "Pepe" for "José").

**Example:**
- Photo name: "Pepe"
- Database name: "José"
- Result: ⚠️ May not match (confidence depends on model knowledge)

**Solution:** Manually verify these cases, or update database with preferred names.

### Case 5: Compound Surnames

**Problem:** Spanish compound surnames (e.g., "García López" - two family names).

**Example:**
- Photo name: "Garcia Lopez"
- Database name: "García López"
- Result: ✅ Matched (word-set comparison)

**Solution:** String matching handles compound surnames well.

## Photo File Edge Cases

### Case 6: No Photo Found

**Problem:** Player exists in database but no photo file matches.

**Symptoms:**
- Console warning: `⚠ No photo found for player 123`
- CSV has `ExternalId` empty for that player
- Portrait generation skips that player

**Solutions:**

1. **Check filename patterns:**
   ```bash
   # List available photos
   ls -la photos/
   ```

2. **Verify photo naming convention:**
   - If photos are named `PlayerID_FirstName_LastName.jpg`
   - But you're expecting `PlayerID.jpg`
   - Update filename or use photo manifest

3. **Use photo manifest:**
   ```bash
   # Create manifest.json mapping photos to IDs
   photomapperai map -i players.csv -p ./photos -m manifest.json
   ```

### Case 7: Multiple Photos for Same Player

**Problem:** Multiple photo files with same ID (different angles, versions).

**Example:**
```
photos/
  123_main.jpg
  123_alt.jpg
  123_profile.jpg
```

**Solutions:**

1. **Use ID-based pattern:**
   - First match is used (alphabetical order)
   - Name convention: `123_*` matches all
   - Map command uses first match

2. **Manual selection:**
   - Rename desired photo to simple ID: `123.jpg`
   - Remove or move alternatives

3. **Photo manifest:**
   - Specify exact file to use:
     ```json
     {
       "photos": {
         "123.jpg": {
           "id": "123",
           "fullName": "Juan Rodriguez",
           "familyName": "Rodriguez",
           "surName": "Juan"
         }
       }
     }
     ```

### Case 8: Unsupported Image Formats

**Problem:** Photo files in unsupported format (e.g., TIFF, WEBP, HEIC).

**Supported formats:** `.png`, `.jpg`, `.jpeg`, `.bmp`

**Solutions:**

1. **Convert to supported format:**
   ```bash
   # Using ImageMagick
   convert photo.heic photo.jpg

   # Using ffmpeg
   ffmpeg -i photo.webp photo.jpg
   ```

2. **Batch convert directory:**
   ```bash
   for file in photos/*.heic; do
     convert "$file" "${file%.heic}.jpg"
   done
   ```

## Face Detection Edge Cases

### Case 9: No Face Detected

**Problem:** Face detection fails to find a face in the photo.

**Symptoms:**
- Console shows multiple spin cycles without success
- Portrait uses center crop (not face-aligned)
- Warning about "All face detection models failed"

**Solutions:**

1. **Check photo quality:**
   - Ensure photo is not blurry
   - Check face is visible (not occluded)
   - Verify good lighting

2. **Try different model:**
   ```bash
   # qwen3-vl is slower but more accurate
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d qwen3-vl
   ```

3. **Use center crop:**
   ```bash
   # Skip face detection entirely
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d center
   ```

4. **Fallback chain:**
   ```bash
   # Try multiple models automatically
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d llava:7b,qwen3-vl,center
   ```

### Case 10: Multiple Faces Detected

**Problem:** Photo contains group photo with multiple faces.

**Current Behavior:**
- Uses first detected face
- Other faces are ignored

**Solutions:**

1. **Use individual player photos:**
   - Best practice: one photo per player
   - Avoid group photos for mapping

2. **Manual cropping:**
   - Manually crop individual faces
   - Save as separate files
   - Use cropped versions

3. **Photo manifest:**
   - Specify which photo file to use

### Case 11: Profile/Extreme Angle Photos

**Problem:** Face is not frontal (profile, extreme angles).

**Current Behavior:**
- qwen3-vl handles these better than OpenCV DNN
- May still fail for extreme angles
- Falls back to center crop

**Solutions:**

1. **Use qwen3-vl:**
   - More robust for non-frontal faces
   ```bash
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d qwen3-vl
   ```

2. **Manually retake photos:**
   - Best practice: frontal portrait photos
   - Neutral expression, good lighting

3. **Use center crop:**
   ```bash
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d center
   ```

## Portrait Generation Edge Cases

### Case 12: Portrait Size Mismatch

**Problem:** Generated portrait size doesn't match specifications.

**Symptoms:**
- Portraits are larger or smaller than expected
- Different aspect ratio

**Solutions:**

1. **Specify portrait dimensions:**
   ```bash
   # Custom size (default: 800x1000)
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits \
     -fw 1024 -fh 1280
   ```

2. **Check aspect ratio:**
   - Default: 800x1000 (4:5 vertical ratio)
   - Adjust for your requirements

3. **Crop method:**
   - `generic`: Standard center-on-face cropping
   - `ai`: (future) Advanced AI-aware cropping

### Case 13: Low Quality Portraits

**Problem:** Generated portraits are low quality or pixelated.

**Solutions:**

1. **Check source photo quality:**
   - If source is low resolution, output will be too
   - Use higher resolution source photos

2. **Format selection:**
   ```bash
   # PNG is lossless (larger files)
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -f png

   # JPG is lossy (smaller files, good compression)
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -f jpg
   ```

3. **Don't upscale unnecessarily:**
   - If source is 640x480, don't request 1024x1280
   - Match source resolution or smaller for thumbnails

## Performance Edge Cases

### Case 14: Slow Face Detection

**Problem:** Face detection takes 30-60 seconds per photo with qwen3-vl.

**Impact:**
- Processing 100 players takes 50-100 minutes

**Solutions:**

1. **Use faster model:**
   ```bash
   # llava:7b is 3-6x faster
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d llava:7b
   ```

2. **Enable parallel processing:**
   ```bash
   # Process 4 photos concurrently
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -par -pd 4
   ```

3. **Use caching:**
   ```bash
   # First run: slow (detects faces)
   # Second run: fast (uses cache)
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits
   ```

4. **Use center crop:**
   ```bash
   # Instant processing (no face detection)
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d center
   ```

### Case 15: Memory Issues with Large Datasets

**Problem:** Out of memory when processing hundreds of photos.

**Symptoms:**
- Application crashes
- System becomes unresponsive

**Solutions:**

1. **Reduce parallel degree:**
   ```bash
   # Default is 4 concurrent tasks
   # Reduce to 2 for memory-constrained systems
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -par -pd 2
   ```

2. **Process in batches:**
   ```bash
   # Split CSV into smaller files
   # Process each batch separately
   photomapperai generatephotos -i batch1.csv -p ./photos -o ./portraits1
   photomapperai generatephotos -i batch2.csv -p ./photos -o ./portraits2
   ```

3. **Disable caching for large datasets:**
   ```bash
   # Cache uses memory
   photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -nc
   ```

## Database Edge Cases

### Case 16: Connection String Issues

**Problem:** Cannot connect to database.

**Solutions:**

1. **Verify connection string format:**
   ```text
   # SQL Server example
   Server=localhost;Database=PhotoDB;User Id=sa;Password=secret;

   # MySQL example
   Server=localhost;Database=PhotoDB;Uid=root;Pwd=secret;

   # PostgreSQL example
   Host=localhost;Port=5432;Database=PhotoDB;Username=postgres;Password=secret;

   # SQLite example
   Data Source=./photodb.db;
   ```

2. **Use connection string file:**
   ```bash
   # Store in separate file (not committed to git)
   echo "Server=localhost;Database=PhotoDB;..." > connection.txt

   # Use in command
   photomapperai extract -i get_players.sql -c connection.txt -teamId 10 -o team.csv
   ```

3. **Test connection separately:**
   ```bash
   # Use database tool to verify connection
   # e.g., SSMS, MySQL Workbench, psql
   ```

### Case 17: SQL Parameter Issues

**Problem:** Database doesn't recognize parameter syntax.

**Solutions:**

1. **Check parameter syntax for your database:**
   - SQL Server: `@parameter`
   - MySQL: `{parameter}`
   - PostgreSQL: `$1`, `$2` (positional)
   - SQLite: `@parameter` or `?`

2. **See examples:**
   - Check `samples/sql-examples/` for your database type

## CSV Edge Cases

### Case 18: CSV Encoding Issues

**Problem:** Special characters (accents, emojis) display incorrectly.

**Solutions:**

1. **PhotoMapperAI uses UTF-8 by default**
   - Should handle most special characters
   - Check your terminal/editor UTF-8 support

2. **If issues persist:**
   - Check source database encoding
   - May need to convert encoding

### Case 19: Empty or Invalid CSV

**Problem:** Map command produces empty or invalid CSV.

**Solutions:**

1. **Verify input CSV:**
   ```bash
   # Check file has content
   cat input.csv | head -5

   # Check for BOM (byte order mark)
   hexdump -C input.csv | head -5
   ```

2. **Check column headers:**
   - Must match expected format exactly
   - Case-sensitive column names

3. **Test with small dataset:**
   ```bash
   # Test with 1-2 records first
   head -n 2 input.csv > test.csv
   photomapperai map -i test.csv -p ./photos
   ```

## Getting Help

### Debug Mode

Add verbose logging for troubleshooting:

```bash
# Add console logging
# (TODO: Add --verbose flag in future release)
```

### Check Logs

Look for error messages in output:
- Yellow warnings: Non-critical issues
- Red errors: Fatal issues that stopped processing

### Report Issues

If you encounter an edge case not documented here:
1. Check GitHub issues: https://github.com/LuisCharro/PhotoMapperAI/issues
2. Search existing issues first
3. Create new issue with:
   - PhotoMapperAI version
   - Command used
   - Error message
   - Expected vs actual behavior
   - Sample data (sanitized)

## Best Practices

### 1. Start with Test Data

Always test with small dataset first:
- 2-5 player records
- 2-5 photo files
- Verifies workflow end-to-end

### 2. Use Cache in Development

Enable caching for faster iteration:
```bash
# First run: creates cache
photomapperai generatephotos -i test.csv -p ./photos -o ./portraits

# Subsequent runs: use cache (instant)
photomapperai generatephotos -i test.csv -p ./photos -o ./portraits
```

### 3. Verify Each Step

Check outputs after each command:
```bash
# Step 1: Extract
photomapperai extract -i players.sql -teamId 10 -o team.csv
cat team.csv  # Verify output

# Step 2: Map
photomapperai map -i team.csv -p ./photos
cat mapped_team.csv  # Verify matches

# Step 3: Generate Photos
photomapperai generatephotos -i mapped_team.csv -p ./photos -o ./portraits
ls -la portraits/  # Verify portraits
```

### 4. Use Appropriate Models

Choose model based on your use case:

| Scenario | Recommended Model |
|-----------|-------------------|
| Development/testing | `center` |
| Small datasets (<10) | Any model |
| Medium datasets (10-50) | `llava:7b,qwen3-vl` |
| Large datasets (50+) | `llava:7b` with `--par -pd 4` |
| Edge case photos | `qwen3-vl` |

### 5. Backup Data

Before running on production data:
- Backup source photos
- Backup database or export CSV
- Test on copy first

## Related Documentation

- Main README: `../README.md`
- Face Detection Guide: `../docs/FACE_DETECTION_GUIDE.md`
- SQL Query Examples: `./README.md`
- Architecture Decisions: `../docs/ARCHITECTURE_DECISIONS.md`
