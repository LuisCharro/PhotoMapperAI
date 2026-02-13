# OpenCV DNN Face Detection Status

## Current Status: ❌ **WORK IN PROGRESS**

The OpenCV DNN face detection is currently not functional due to model file issues.

## Known Issues

1. **Prototxt File Mismatch**: The manually created prototxt file is incomplete and doesn't match the pre-trained caffemodel weights.
2. **Download Limitations**: The official GitHub repository for the prototxt file returns 404 errors, making automatic download difficult.
3. **Layer Configuration**: The prototxt file requires exact layer definitions that match the pre-trained weights in the caffemodel file.

## Alternative Solutions

### Option 1: Use Ollama Vision Models (Recommended)

Ollama vision models (qwen3-vl, llava:7b) provide robust face detection and work out-of-the-box:

```bash
# Use qwen3-vl for best accuracy
photomapperai generatephotos -i mapped_players.csv -p ./photos -o ./portraits -d qwen3-vl

# Use llava:7b as fallback
photomapperai generatephotos -i mapped_players.csv -p ./photos -o ./portraits -d llava:7b
```

### Option 2: Manual OpenCV DNN Setup

To use OpenCV DNN, you need to manually obtain the correct prototxt file:

1. Find the correct `res10_300x300_ssd_iter_140000.prototxt` file from OpenCV releases
2. Ensure it matches the caffemodel version
3. Place both files in the `./models/` directory
4. Run: `photomapperai generatephotos ... -faceDetection opencv-dnn`

## Current Working Face Detection Methods

| Method       | Status      | Notes                              |
|--------------|-------------|------------------------------------|
| qwen3-vl     | ✅ Working  | Best accuracy, handles edge cases  |
| llava:7b     | ✅ Working  | Good fallback option              |
| center crop  | ✅ Working  | Ultimate fallback, no AI required |
| opencv-dnn   | ❌ WIP      | Requires correct model files      |

## Next Steps for OpenCV DNN

1. Find working prototxt file source
2. Update download script with correct URLs
3. Verify model compatibility
4. Add comprehensive error handling
5. Test with real images

## Related Files

- `/scripts/download-opencv-models.sh` - Download script (needs URL fixes)
- `/models/` - Directory for model files
- `/docs/OPENCV_MODELS.md` - General OpenCV models documentation
