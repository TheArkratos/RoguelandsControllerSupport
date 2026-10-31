#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
test -f "$project_dir/build/ControllerSupport.dll" || { echo "Run build.sh first." >&2; exit 1; }
test -f "$project_dir/LICENSE" || { echo "Missing LICENSE." >&2; exit 1; }

cd "$project_dir"
bsdtar -a -cf "build/ControllerSupport.zip" -C . Manifest.ini ModInfo.txt LICENSE -C build ControllerSupport.dll
echo "Created build/ControllerSupport.zip"
