#!/bin/bash

# Pack translation files into zip
# Usage: ./pack_translation.sh DeluxeGrabberFix

set -e

if [ -z "$1" ]; then
  echo "Usage: $0 <mod-name>"
  echo "Example: $0 DeluxeGrabberFix"
  exit 1
fi

MOD_NAME="$1"
ZH_SOURCE="ChineseTranslation/zh/${MOD_NAME}"
DIST_DIR="dist"
OUTPUT_ZIP="${DIST_DIR}/${MOD_NAME}.zip"

if [ ! -d "$ZH_SOURCE" ]; then
  echo "Error: Source directory '${ZH_SOURCE}' not found"
  exit 1
fi

mkdir -p "$DIST_DIR"

echo "Packing ${MOD_NAME}..."
(cd "ChineseTranslation/zh" && zip -r "../../${OUTPUT_ZIP}" "$MOD_NAME" -x "*/README.md")

echo "Created: ${OUTPUT_ZIP}"
