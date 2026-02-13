#!/bin/bash

# Download OpenCV Model Files for PhotoMapperAI
# This script downloads the required model files from OpenCV GitHub repository

set -e  # Exit on error

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}PhotoMapperAI - OpenCV Model Files Downloader${NC}"
echo "==========================================="
echo ""

# Default models directory (can be overridden with first argument)
MODELS_DIR="${1:-./models}"

# Create models directory if it doesn't exist
if [ ! -d "$MODELS_DIR" ]; then
    echo -e "${YELLOW}Creating directory: $MODELS_DIR${NC}"
    mkdir -p "$MODELS_DIR"
fi

cd "$MODELS_DIR"

echo -e "${GREEN}Downloading OpenCV models to: $MODELS_DIR${NC}"
echo ""

# Ask user which models to download
echo "Which models do you want to download?"
echo "1) OpenCV DNN (Caffe) - Recommended (good speed/accuracy)"
echo "2) Haar Cascades - Fastest, lower accuracy (includes eye detection)"
echo "3) YOLOv8-Face - Best accuracy, slower"
echo "4) All of the above"
echo "5) Exit"
echo ""
read -p "Enter your choice (1-5): " choice

case $choice in
    1)
        echo ""
        echo -e "${GREEN}Downloading OpenCV DNN models...${NC}"
        echo ""

        # Download DNN face detection model
        echo "Downloading res10_ssd_deploy.prototxt..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_ssd_deploy.prototxt

        echo "Downloading res10_300x300_ssd_iter_140000.caffemodel..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel

        echo ""
        echo -e "${GREEN}✓ OpenCV DNN models downloaded successfully!${NC}"
        echo ""
        echo "Files downloaded:"
        ls -lh res10_ssd_deploy.prototxt res10_300x300_ssd_iter_140000.caffemodel
        ;;

    2)
        echo ""
        echo -e "${GREEN}Downloading Haar Cascade models...${NC}"
        echo ""

        # Download face cascade
        echo "Downloading haarcascade_frontalface_default.xml..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml

        # Download eye cascades
        echo "Downloading haarcascade_eye.xml..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_eye.xml

        echo "Downloading haarcascade_lefteye_2splits.xml..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_lefteye_2splits.xml

        echo "Downloading haarcascade_righteye_2splits.xml..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_righteye_2splits.xml

        echo ""
        echo -e "${GREEN}✓ Haar Cascade models downloaded successfully!${NC}"
        echo ""
        echo "Files downloaded:"
        ls -lh haarcascade_*.xml
        ;;

    3)
        echo ""
        echo -e "${YELLOW}YOLOv8-Face model${NC}"
        echo ""
        echo "YOLOv8-Face requires manual download from GitHub."
        echo "Please visit: https://github.com/akanametov/yolov8-face"
        echo "Search for 'yolov8-face.onnx' or 'yolov8n-face.onnx'"
        echo ""
        echo "After downloading, place the .onnx file in: $MODELS_DIR"
        ;;

    4)
        echo ""
        echo -e "${GREEN}Downloading ALL models...${NC}"
        echo ""

        # Download DNN models
        echo "Downloading OpenCV DNN models..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_ssd_deploy.prototxt
        curl -L -O https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel

        echo ""
        echo "Downloading Haar Cascade models..."
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_eye.xml
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_lefteye_2splits.xml
        curl -L -O https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_righteye_2splits.xml

        echo ""
        echo -e "${GREEN}✓ DNN and Haar Cascade models downloaded successfully!${NC}"
        echo ""
        echo "Note: YOLOv8-Face requires manual download."
        echo "Visit: https://github.com/akanametov/yolov8-face"
        echo ""
        echo "Files downloaded:"
        ls -lh
        ;;

    5)
        echo -e "${YELLOW}Exiting...${NC}"
        exit 0
        ;;

    *)
        echo -e "${RED}Invalid choice. Exiting.${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}==========================================${NC}"
echo -e "${GREEN}Next steps:${NC}"
echo "1. Copy appsettings.template.json to appsettings.json"
echo "2. Update the ModelsPath in appsettings.json if needed"
echo "3. Verify model filenames match appsettings.json configuration"
echo ""
echo "For more information, see: docs/OPENCV_MODELS.md"
