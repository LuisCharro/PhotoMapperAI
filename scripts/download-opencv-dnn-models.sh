#!/bin/bash

# Script to download OpenCV DNN face detection model files
# This script downloads the ResNet-10 SSD face detection model from OpenCV's repository

set -e

MODELS_DIR="./models"
PROTOTXT_URL="https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.prototxt"
CAFFEMODEL_URL="https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel"

echo "============================================"
echo "OpenCV DNN Face Detection Model Download"
echo "============================================"
echo ""

# Create models directory if it doesn't exist
if [ ! -d "$MODELS_DIR" ]; then
    echo "Creating models directory..."
    mkdir -p "$MODELS_DIR"
fi

# Download prototxt file
echo "Downloading prototxt file..."
echo "URL: $PROTOTXT_URL"
curl -L -o "$MODELS_DIR/res10_ssd_deploy.prototxt" "$PROTOTXT_URL"

if [ $? -eq 0 ]; then
    echo "✓ Downloaded res10_ssd_deploy.prototxt"
else
    echo "✗ Failed to download prototxt file"
    exit 1
fi

# Download caffemodel file
echo ""
echo "Downloading caffemodel file..."
echo "URL: $CAFFEMODEL_URL"
curl -L -o "$MODELS_DIR/res10_300x300_ssd_iter_140000.caffemodel" "$CAFFEMODEL_URL"

if [ $? -eq 0 ]; then
    echo "✓ Downloaded res10_300x300_ssd_iter_140000.caffemodel"
else
    echo "✗ Failed to download caffemodel file"
    exit 1
fi

echo ""
echo "============================================"
echo "Download complete!"
echo "============================================"
echo ""
echo "Files downloaded to: $MODELS_DIR/"
echo "  - res10_ssd_deploy.prototxt ($(wc -c < "$MODELS_DIR/res10_ssd_deploy.prototxt") bytes)"
echo "  - res10_300x300_ssd_iter_140000.caffemodel ($(wc -c < "$MODELS_DIR/res10_300x300_ssd_iter_140000.caffemodel") bytes)"
echo ""
echo "Expected file sizes:"
echo "  - res10_ssd_deploy.prototxt: ~2-3 KB"
echo "  - res10_300x300_ssd_iter_140000.caffemodel: ~10 MB"
echo ""
