#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${ROGUELANDS_DIR:-$HOME/.local/share/Steam/steamapps/common/Roguelands}"
managed_dir="$game_dir/Roguelands_Data/Managed"
sdk_version="$(dotnet --version)"
sdk_root="$(dotnet --list-sdks | awk -v version="$sdk_version" '$1 == version {sub(/^.*\[/, ""); sub(/\].*$/, ""); print; exit}')"
test -n "$sdk_root" || { echo "Cannot locate active .NET SDK $sdk_version." >&2; exit 1; }
csc_dll="$sdk_root/$sdk_version/Roslyn/bincore/csc.dll"
output_dir="$project_dir/build"
variant="${1:-}"
extra_resources=()
case "$variant" in
    "") ;;
    --deck)
        for architecture in linux-x86 linux-x86_64; do
            native_dir="$output_dir/deck-native/$architecture"
            for native_file in libControllerSupport-SDL2.so libControllerSupport-SDL2.so.sha256 LICENSE.txt provenance.json; do
                test -f "$native_dir/$native_file" || { echo "Run python3 -B tools/prepare_deck_sdl2.py first (missing $architecture/$native_file)." >&2; exit 1; }
            done
            python3 - "$native_dir" <<'PY'
import hashlib, pathlib, sys
root = pathlib.Path(sys.argv[1])
binary = root / 'libControllerSupport-SDL2.so'
assert hashlib.sha256(binary.read_bytes()).hexdigest() == (root / (binary.name + '.sha256')).read_text().strip(), 'SDL2 checksum mismatch'
PY
            extra_resources+=(
                "/resource:$native_dir/libControllerSupport-SDL2.so,ControllerSupport.Native.$architecture.SDL2"
                "/resource:$native_dir/libControllerSupport-SDL2.so.sha256,ControllerSupport.Native.$architecture.SDL2.sha256"
            )
        done
        output_dir="$output_dir/deck"
        ;;
    *) echo "Usage: bash build.sh [--deck]" >&2; exit 1 ;;
esac

for reference in mscorlib.dll System.dll System.Core.dll UnityEngine.dll Assembly-CSharp.dll GadgetCore.dll 0Harmony.dll; do
    test -f "$managed_dir/$reference" || { echo "Missing reference: $managed_dir/$reference" >&2; exit 1; }
done
test -f "$csc_dll" || { echo "Roslyn compiler not found: $csc_dll" >&2; exit 1; }
# Only production files directly under src/ participate; tests stay separate.
shopt -s nullglob
source_files=("$project_dir"/src/*.cs)
((${#source_files[@]})) || { echo "No production sources found." >&2; exit 1; }
mapfile -d '' -t source_files < <(printf '%s\0' "${source_files[@]}" | LC_ALL=C sort -z)
mkdir -p "$output_dir"

dotnet "$csc_dll" \
    /nologo /target:library /langversion:7.3 /nostdlib \
    /out:"$output_dir/ControllerSupport.dll" \
    /reference:"$managed_dir/mscorlib.dll" \
    /reference:"$managed_dir/System.dll" \
    /reference:"$managed_dir/System.Core.dll" \
    /reference:"$managed_dir/UnityEngine.dll" \
    /reference:"$managed_dir/GadgetCore.dll" \
    /reference:"$managed_dir/Assembly-CSharp.dll" \
    /reference:"$managed_dir/0Harmony.dll" \
    /resource:"$project_dir/Assets/SelectionCorners/corner-top-left.png",ControllerSupport.SelectionCorners.corner-top-left.png \
    /resource:"$project_dir/Assets/SelectionCorners/corner-top-right.png",ControllerSupport.SelectionCorners.corner-top-right.png \
    /resource:"$project_dir/Assets/SelectionCorners/corner-bottom-right.png",ControllerSupport.SelectionCorners.corner-bottom-right.png \
    /resource:"$project_dir/Assets/SelectionCorners/corner-bottom-left.png",ControllerSupport.SelectionCorners.corner-bottom-left.png \
    "${extra_resources[@]}" \
    "$project_dir/Properties/AssemblyInfo.cs" \
    "${source_files[@]}"

echo "Built $output_dir/ControllerSupport.dll"
