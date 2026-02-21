#!/usr/bin/env bash
set -euo pipefail

# Regression check for external real data (not committed).
# Verifies deterministic output for one known player using --expectedOutput.

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXTERNAL_ROOT="${1:-/Users/luis/Repos/PhotoMapperAI_ExternalData_Test}"

MAPPED_CSV="$EXTERNAL_ROOT/Competition2024/Csvs/mapped_players_7548_Spanien.csv"
PHOTOS_DIR="$EXTERNAL_ROOT/Competition2024/Images/Spain"
EXPECTED_IMG="$EXTERNAL_ROOT/TestData_Euro2024/TestData/Euro2024/PortraitPicturesByTeam/Spain/200x300/1079659.jpg"
OUT_DIR="${2:-/tmp/photomapperai-regression-200x300}"

if [[ ! -f "$MAPPED_CSV" ]]; then
  echo "Missing mapped CSV: $MAPPED_CSV" >&2
  exit 1
fi
if [[ ! -d "$PHOTOS_DIR" ]]; then
  echo "Missing photos dir: $PHOTOS_DIR" >&2
  exit 1
fi
if [[ ! -f "$EXPECTED_IMG" ]]; then
  echo "Missing expected image: $EXPECTED_IMG" >&2
  exit 1
fi

mkdir -p "$OUT_DIR"

echo "Running one-player regression check..."
dotnet run --project "$REPO_ROOT/src/PhotoMapperAI" -- generatephotos \
  --inputCsvPath "$MAPPED_CSV" \
  --photosDir "$PHOTOS_DIR" \
  --processedPhotosOutputPath "$OUT_DIR" \
  --format jpg \
  --faceDetection opencv-dnn \
  --noCache \
  --onlyPlayer 1079659 \
  --expectedOutput "$EXPECTED_IMG"

echo "Done. Output dir: $OUT_DIR"
