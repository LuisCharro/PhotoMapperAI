# PhotoMapperAI - Face Detection Model Guide

## Overview

PhotoMapperAI supports multiple face detection models with automatic fallback capabilities. This guide helps you choose the right model for your use case.

## Available Face Detection Models

### 1. Center Crop (Fastest)
- **Model Name:** `center`
- **Speed:** Instant (< 1 second)
- **Accuracy:** Low (uses geometric center)
- **Use Case:** Quick testing, no face detection needed

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d center
```

### 2. OpenCV DNN (Fastest AI)
- **Model Name:** `opencv-dnn`
- **Speed:** Fast (1-2 seconds per image)
- **Accuracy:** Good
- **Requirements:** Model files in `./models/` directory
- **Status:** ⚠️ WIP - Missing prototxt file

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d opencv-dnn
```

**Model Files Needed:**
- `res10_300x300_ssd_iter_140000.caffemodel` (~10 MB) ✅ Available
- `res10_ssd_deploy.prototxt` (~2-3 KB) ❌ Missing

### 3. LLaVA 7B (Recommended for Production)
- **Model Name:** `llava:7b`
- **Speed:** Slow (5-10 seconds per image)
- **Accuracy:** Good
- **Requirements:** Ollama running with `llava:7b` model

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d llava:7b
```

### 4. Qwen3-VL (Best Accuracy)
- **Model Name:** `qwen3-vl`
- **Speed:** Very Slow (30-60 seconds per image)
- **Accuracy:** Best
- **Requirements:** Ollama running with `qwen3-vl` model

**Command:**
```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d qwen3-vl
```

## Fallback Mode (Recommended for Reliability)

Use comma-separated models to enable automatic fallback:

```bash
photomapperai generatephotos -i players.csv -p ./photos -o ./portraits -d llava:7b,qwen3-vl
```

**How it works:**
1. Tries first model (llava:7b)
2. If it fails or no face detected, tries next model (qwen3-vl)
3. If all models fail, uses center crop fallback

**Recommended fallback chains:**
- Fast + Reliable: `llava:7b,qwen3-vl`
- Testing: `center,llava:7b`
- Best Accuracy: `llava:7b,qwen3-vl,center`

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
