#!/bin/bash

set -euo pipefail

BASELINE="${1:-benchmark-results/benchmark-20260212-080146.json}"
FACE_MODEL="${2:-opencv-dnn}"
TEST_DATA_PATH="${3:-tests/Data}"
OUTPUT_PATH="${4:-benchmark-results}"

echo "Running benchmark (face model: ${FACE_MODEL})..."
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- benchmark \
  --faceModels "${FACE_MODEL}" \
  --testDataPath "${TEST_DATA_PATH}" \
  --outputPath "${OUTPUT_PATH}"

LATEST_FILE="$(ls -1t "${OUTPUT_PATH}"/benchmark-*.json | head -n 1)"

echo ""
echo "Comparing latest run with baseline..."
echo "Baseline : ${BASELINE}"
echo "Candidate: ${LATEST_FILE}"

dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- benchmark-compare \
  --baseline "${BASELINE}" \
  --candidate "${LATEST_FILE}" \
  --faceModel "${FACE_MODEL}"
