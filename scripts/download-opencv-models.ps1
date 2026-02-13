# PowerShell script to download OpenCV Model Files for PhotoMapperAI

Write-Host "PhotoMapperAI - OpenCV Model Files Downloader" -ForegroundColor Green
Write-Host "==========================================="
Write-Host ""

# Default models directory
$ModelsDir = if ($args[0]) { $args[0] } else { "./models" }

# Create models directory if it doesn't exist
if (-not (Test-Path -Path $ModelsDir)) {
    Write-Host "Creating directory: $ModelsDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $ModelsDir | Out-Null
}

$OriginalDir = Get-Location
Set-Location -Path $ModelsDir

Write-Host "Downloading OpenCV models to: $((Get-Location).Path)" -ForegroundColor Green
Write-Host ""

# Ask user which models to download
Write-Host "Which models do you want to download?"
Write-Host "1) OpenCV DNN (Caffe) - Recommended (good speed/accuracy)"
Write-Host "2) Haar Cascades - Fastest, lower accuracy (includes eye detection)"
Write-Host "3) YOLOv8-Face - Best accuracy, slower"
Write-Host "4) All of the above"
Write-Host "5) Exit"
Write-Host ""

$Choice = Read-Host "Enter your choice (1-5)"

function Download-File {
    param(
        [string]$Url,
        [string]$OutFile
    )
    Write-Host "Downloading $OutFile..."
    Invoke-WebRequest -Uri $Url -OutFile $OutFile
}

switch ($Choice) {
    "1" {
        Write-Host ""
        Write-Host "Downloading OpenCV DNN models..." -ForegroundColor Green
        Write-Host ""

        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_ssd_deploy.prototxt" -OutFile "res10_ssd_deploy.prototxt"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel" -OutFile "res10_300x300_ssd_iter_140000.caffemodel"

        Write-Host ""
        Write-Host "✓ OpenCV DNN models downloaded successfully!" -ForegroundColor Green
        Write-Host ""
    }

    "2" {
        Write-Host ""
        Write-Host "Downloading Haar Cascade models..." -ForegroundColor Green
        Write-Host ""

        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml" -OutFile "haarcascade_frontalface_default.xml"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_eye.xml" -OutFile "haarcascade_eye.xml"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_lefteye_2splits.xml" -OutFile "haarcascade_lefteye_2splits.xml"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_righteye_2splits.xml" -OutFile "haarcascade_righteye_2splits.xml"

        Write-Host ""
        Write-Host "✓ Haar Cascade models downloaded successfully!" -ForegroundColor Green
        Write-Host ""
    }

    "3" {
        Write-Host ""
        Write-Host "YOLOv8-Face model" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "YOLOv8-Face requires manual download from GitHub."
        Write-Host "Please visit: https://github.com/akanametov/yolov8-face"
        Write-Host "Search for 'yolov8-face.onnx' or 'yolov8n-face.onnx'"
        Write-Host ""
        Write-Host "After downloading, place the .onnx file in: $ModelsDir"
    }

    "4" {
        Write-Host ""
        Write-Host "Downloading ALL models..." -ForegroundColor Green
        Write-Host ""

        # DNN
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_ssd_deploy.prototxt" -OutFile "res10_ssd_deploy.prototxt"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv_3rdparty/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000.caffemodel" -OutFile "res10_300x300_ssd_iter_140000.caffemodel"

        # Haar
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml" -OutFile "haarcascade_frontalface_default.xml"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_eye.xml" -OutFile "haarcascade_eye.xml"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_lefteye_2splits.xml" -OutFile "haarcascade_lefteye_2splits.xml"
        Download-File -Url "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_righteye_2splits.xml" -OutFile "haarcascade_righteye_2splits.xml"

        Write-Host ""
        Write-Host "✓ DNN and Haar Cascade models downloaded successfully!" -ForegroundColor Green
        Write-Host ""
    }

    "5" {
        Write-Host "Exiting..." -ForegroundColor Yellow
    }

    default {
        Write-Host "Invalid choice. Exiting." -ForegroundColor Red
    }
}

Set-Location -Path $OriginalDir
Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Green
Write-Host "1. Copy appsettings.template.json to appsettings.json"
Write-Host "2. Update the ModelsPath in appsettings.json if needed"
Write-Host "3. Verify model filenames match appsettings.json configuration"
Write-Host ""
Write-Host "For more information, see: docs/OPENCV_MODELS.md"
