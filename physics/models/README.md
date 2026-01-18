# Model Files

This folder should contain the ONNX segmentation model for person detection.

## Required Model

**File:** `person-segmentation.onnx`

**Source:** FCN ResNet-50 from ONNX Model Zoo

**Download URL:** 
https://github.com/onnx/models/raw/main/validated/vision/object_detection_segmentation/fcn/model/fcn-resnet50-12.onnx

**Size:** ~134 MB

## Setup Instructions

1. Download the model from the URL above
2. Rename it to `person-segmentation.onnx`
3. Place it in this `models` folder

The final path should be:
```
physics/bin/Debug/net9.0/models/person-segmentation.onnx
```

Or if running from the project directory:
```
models/person-segmentation.onnx
```

## Alternative: Automatic Setup

You can download the model using PowerShell:

```powershell
# Create the models directory
New-Item -ItemType Directory -Force -Path "models"

# Download the model
Invoke-WebRequest -Uri "https://github.com/onnx/models/raw/main/validated/vision/object_detection_segmentation/fcn/model/fcn-resnet50-12.onnx" -OutFile "models/person-segmentation.onnx"
```

Or using curl:
```bash
mkdir -p models
curl -L -o models/person-segmentation.onnx "https://github.com/onnx/models/raw/main/validated/vision/object_detection_segmentation/fcn/model/fcn-resnet50-12.onnx"
```
