#!/bin/bash

# Pack translation files into zip
# Usage: ./build_release.sh DeluxeGrabberFix

set -e

if [ -z "$1" ]; then
  echo "Usage: $0 <mod-name>"
  echo "Example: $0 DeluxeGrabberFix"
  exit 1
fi

MOD_NAME="$1"
ZH_SOURCE="mods/zh/${MOD_NAME}"
DIST_DIR="mods/release"
OUTPUT_ZIP="${DIST_DIR}/${MOD_NAME}.zip"

if [ ! -d "$ZH_SOURCE" ]; then
  echo "Error: Source directory '${ZH_SOURCE}' not found"
  exit 1
fi

mkdir -p "$DIST_DIR"

echo "Packing ${MOD_NAME}..."
(cd "mods/zh" && zip -r "../../${OUTPUT_ZIP}" "$MOD_NAME" -x "*/README.md")

echo "Created: ${OUTPUT_ZIP}"
