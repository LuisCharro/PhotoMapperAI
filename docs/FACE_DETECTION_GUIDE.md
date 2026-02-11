# PhotoMapperAI - Face Detection Model Guide

## Overview

PhotoMapperAI supports multiple face detection models with automatic fallback capabilities. This guide helps you choose the right model for your use case.

## How Portrait Cropping Works

The goal is to generate consistent portraits with:
- **Eyes at ~35% from top** of the output image (standard portrait composition)
- **Face centered horizontally**
- **Head + neck + upper chest** visible (not full body)

### Detection Hierarchy

The system uses a tiered approach to find the best centering point:

```
1. Both eyes detected? → Use eye midpoint (BEST)
   ↓ No
2. One eye detected? → Use eye position + estimate other eye
   ↓ No
3. Face detected? → Estimate eye position from face rectangle
   ↓ No
4. No detection? → Use upper-body crop (top 35% of image)
```

### Why Eye Detection Matters

For consistent portraits, **eye detection is critical**:
- Eyes provide precise **horizontal centering** (midpoint between eyes)
- Eyes provide precise **vertical positioning** (eyes at 35% from top)
- Without eyes, the system estimates positions which may vary between photos

## Available Face Detection Models

### 1. Center Crop (Fastest - No AI)
- **Model Name:** `center`
- **Speed:** Instant (< 1 second)
- **Face Detection:** ❌ No
- **Eye Detection:** ❌ No
- **Use Case:** Quick testing, no face detection needed

**How it works:**
- Assumes full-body sports photo
- Crops top 35% of image (head + neck + chest area)
- Centers horizontally on image center
- **Limitation:** No face/eye detection, so positioning may vary

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d center
```

### 2. OpenCV DNN (Fast AI - Face Only)
- **Model Name:** `opencv-dnn`
- **Speed:** Fast (1-2 seconds per image)
- **Face Detection:** ✅ Yes
- **Eye Detection:** ❌ No (estimates from face rect)
- **Requirements:** Model files in `./models/` directory

**How it works:**
- Detects face rectangle using neural network
- **Estimates eye position** at 35% from top of face rectangle
- Uses face center for horizontal positioning
- **Limitation:** No actual eye detection, so horizontal centering is estimated

**Model Files Needed:**
- `res10_300x300_ssd_iter_140000.caffemodel` (~10 MB)
- `res10_ssd_deploy.prototxt` (~2-3 KB)

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d opencv-dnn
```

### 3. LLaVA 7B (Recommended for Production)
- **Model Name:** `llava:7b`
- **Speed:** Slow (5-10 seconds per image)
- **Face Detection:** ✅ Yes
- **Eye Detection:** ✅ Yes (both eyes)
- **Requirements:** Ollama running with `llava:7b` model

**How it works:**
- Uses AI vision model to analyze image
- Detects face rectangle AND both eye positions
- **Precise horizontal centering** using eye midpoint
- **Precise vertical positioning** using actual eye coordinates
- **Best for consistent portraits**

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d llava:7b
```

### 4. Qwen3-VL (Best Accuracy)
- **Model Name:** `qwen3-vl`
- **Speed:** Very Slow (30-60 seconds per image)
- **Face Detection:** ✅ Yes
- **Eye Detection:** ✅ Yes (both eyes)
- **Requirements:** Ollama running with `qwen3-vl` model

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d qwen3-vl
```

## Model Comparison Table

| Model | Face | Eyes | Speed | Horizontal Center | Vertical Center |
|-------|------|------|-------|-------------------|-----------------|
| center | ❌ | ❌ | Instant | Image center | Estimated (15% from top) |
| opencv-dnn | ✅ | ❌ | 1-2s | Face center | Estimated (35% of face) |
| llava:7b | ✅ | ✅ | 5-10s | **Eye midpoint** | **Actual eye Y** |
| qwen3-vl | ✅ | ✅ | 30-60s | **Eye midpoint** | **Actual eye Y** |

## Fallback Mode (Recommended for Reliability)

Use comma-separated models to enable automatic fallback:

```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d llava:7b,qwen3-vl
```

**How it works:**
1. Tries first model (llava:7b) - detects face + eyes
2. If it fails or no face detected, tries next model (qwen3-vl)
3. If all models fail, uses center crop fallback

**Recommended fallback chains:**
- **Best quality:** `llava:7b,qwen3-vl` (both detect eyes)
- **Fast + quality:** `opencv-dnn,llava:7b` (opencv for speed, llava for eyes)
- **Testing:** `center,llava:7b` (instant fallback to AI)

## Performance Benchmarks

| Model          | Time per Image | Accuracy | Recommended For |
|----------------|----------------|-----------|----------------|
| center         | < 1 sec        | Low       | Testing, quick iteration |
| opencv-dnn     | 1-2 sec        | Good      | Production (when working) |
| llava:7b       | 5-10 sec       | Good      | Production default |
| qwen3-vl       | 30-60 sec      | Best      | Edge cases, difficult images |

## Testing Workflow

For fast iteration during development:

```bash
# 1. Test with center crop (instant)
photomapperai generatephotos -i test.csv -p ./photos -o ./test -d center

# 2. Test with portrait-only (skip face detection)
photomapperai generatephotos -i test.csv -p ./photos -o ./test -po

# 3. Test with fallback (when ready)
photomapperai generatephotos -i test.csv -p ./photos -o ./test -d llava:7b,qwen3-vl
```

## Portrait Crop Behavior

### Center Crop Mode (`-d center`)

When using center crop mode (no face detection), the tool applies an **upper-body crop** optimized for full-body sports photos:

- **Crop Position:** Top of image (starts at 2% from top with small margin)
- **Crop Size:** 35% of source image height, maintaining target aspect ratio (2:3)
- **Expected Content:** Head, neck, and upper chest/shoulders (proper portrait composition)
- **Use Case:** Fast testing when face detection is unavailable or not needed

**Why upper-body crop?**
Sports photos are typically full-body shots with players standing. Cropping from the geometric center would include too much lower body (chest + legs). The upper-body crop captures the top 35% of the image, which contains the head, neck, and upper chest - exactly what a proper portrait should show.

**Example:**
```
Input: Full-body photo (e.g., 427x640 pixels)
       ┌─────────────────┐
       │   ┌─────┐       │  ← Head/face area
       │   │Face │       │
       │   └─────┘       │
       │                 │  ← Upper 35% = Portrait crop
       │─ ─ ─ ─ ─ ─ ─ ─ │     (head + neck + chest)
       │                 │
       │                 │  ← Lower body (not included)
       │                 │
       │                 │
       └─────────────────┘

Output: Portrait (200x300 pixels)
       Head + neck + upper chest/shoulders
       Face occupies ~45-50% of vertical space
```

### AI Face Detection Mode

When using face detection models (OpenCV, LLaVA, Qwen3-VL):

- **Crop Position:** Centered on detected eyes (optimal for portraits)
- **Crop Size:** 1.2-1.8x target dimensions (varies by detection quality)
- **Expected Content:** Face centered, with proper headroom
- **Use Case:** Production when quality matters

## Portrait-Only Mode

Skip face detection entirely and use existing photo mappings:

```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -po
```

**Use Case:** When you've already verified photo-to-player mappings and just need to generate portraits.

## Ollama Setup

### Install Models:

```bash
# Install LLaVA 7B (recommended default)
ollama pull llava:7b

# Install Qwen3-VL (best accuracy)
ollama pull qwen3-vl

# Verify installation
ollama list
```

### Verify Ollama is Running:

```bash
# Check Ollama service
curl http://localhost:11434/api/tags

# Should return list of available models
```

## Troubleshooting

### Face detection takes too long:
- Use `llava:7b` instead of `qwen3-vl` (3-6x faster)
- Use `-po` flag to skip face detection entirely
- Use `center` model for instant center crop

### All models fail:
- Check Ollama is running: `curl http://localhost:11434/api/tags`
- Check models are installed: `ollama list`
- Fallback to center crop: `-d center`

### "Unknown face detection model" error:
- Check model name is spelled correctly
- For Ollama models, ensure they're installed
- Use fallback mode: `-d llava:7b,qwen3-vl`

## Best Practices

### Development/Testing:
- Use `-d center` for instant feedback
- Use `-po` to skip face detection when mapping is verified
- Test with small datasets first (1-5 players)

### Production:
- Use `-d llava:7b,qwen3-vl` for reliability
- Set appropriate portrait size: `-fw 800 -fh 1000`
- Choose format based on use case: `-f jpg` (smaller) or `-f png` (lossless)

### Performance Optimization:
- Batch processing: Use fallback mode to avoid manual retries
- Parallel processing: Not yet implemented (TODO)
- Caching: Not yet implemented (TODO)

## Default Configuration

The default face detection model is now set to `llava:7b,qwen3-vl` for optimal balance of speed and reliability with automatic fallback.

## Future Improvements

- [ ] OpenCV DNN model file fixes
- [ ] Parallel face detection for batch operations
- [ ] Face detection result caching
- [ ] Performance monitoring and logging
- [ ] Adaptive model selection based on image characteristics
