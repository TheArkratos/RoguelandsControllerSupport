#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
variant="${1:-}"
if [[ "$variant" == "--deck" ]]; then
    test -f "$project_dir/build/deck/ControllerSupport.dll" || { echo "Run bash build.sh --deck first." >&2; exit 1; }
    metadata_dir="$(mktemp -d "$project_dir/build/deck/package-metadata-XXXXXX")"
    trap 'rm -rf "$metadata_dir"' EXIT
    for arch in linux-x86 linux-x86_64; do
        native_dir="$project_dir/build/deck-native/$arch"
        mkdir -p "$metadata_dir/SDL2/$arch"
        cp "$native_dir/LICENSE.txt" "$metadata_dir/SDL2/$arch/LICENSE.txt"
        cp "$native_dir/provenance.json" "$metadata_dir/SDL2/$arch/provenance.json"
    done
    cd "$project_dir"
    bsdtar -a -cf "build/ControllerSupport-Deck.zip" -C . Manifest.ini ModInfo.txt LICENSE -C build/deck ControllerSupport.dll -C "$metadata_dir" SDL2 -C "$project_dir/docs" STEAM-DECK-TEST.md
    (cd build && sha256sum ControllerSupport-Deck.zip > ControllerSupport-Deck.zip.sha256)
    echo "Created build/ControllerSupport-Deck.zip (native Linux x86/x86_64 test candidate)"
    exit 0
elif [[ -n "$variant" ]]; then
    echo "Usage: bash package.sh [--deck]" >&2; exit 1
fi
test -f "$project_dir/build/ControllerSupport.dll" || { echo "Run build.sh first." >&2; exit 1; }
test -f "$project_dir/LICENSE" || { echo "Missing LICENSE." >&2; exit 1; }

cd "$project_dir"
bsdtar -a -cf "build/ControllerSupport.zip" -C . Manifest.ini ModInfo.txt LICENSE -C build ControllerSupport.dll
echo "Created build/ControllerSupport.zip"
