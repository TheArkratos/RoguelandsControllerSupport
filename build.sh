#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${ROGUELANDS_DIR:-$HOME/.local/share/Steam/steamapps/common/Roguelands}"
managed_dir="$game_dir/Roguelands_Data/Managed"
csc_dll="/usr/share/dotnet/sdk/10.0.112/Roslyn/bincore/csc.dll"
output_dir="$project_dir/build"

for reference in mscorlib.dll System.dll System.Core.dll UnityEngine.dll GadgetCore.dll; do
    test -f "$managed_dir/$reference" || { echo "Missing reference: $managed_dir/$reference" >&2; exit 1; }
done
test -f "$csc_dll" || { echo "Roslyn compiler not found: $csc_dll" >&2; exit 1; }
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
    "$project_dir/Properties/AssemblyInfo.cs" \
    "$project_dir/src/BindingConfig.cs" \
    "$project_dir/src/RawJoystickEvent.cs" \
    "$project_dir/src/JoystickInputState.cs" \
    "$project_dir/src/LastInputSource.cs" \
    "$project_dir/src/RawLinuxJoystickProbe.cs" \
    "$project_dir/src/InputProbe.cs" \
    "$project_dir/src/ControllerGameplayInput.cs" \
    "$project_dir/src/ControllerGameplayActions.cs" \
    "$project_dir/src/GameplayInputPatches.cs" \
    "$project_dir/src/ControllerAim.cs" \
    "$project_dir/src/AimAssistGeometry.cs" \
    "$project_dir/src/ControllerAimAssist.cs" \
    "$project_dir/src/AimAssistPatches.cs" \
    "$project_dir/src/ExplorationTargetSelector.cs" \
    "$project_dir/src/ControllerExploration.cs" \
    "$project_dir/src/ControllerVisibleCursor.cs" \
    "$project_dir/src/SelectionCornerRenderer.cs" \
    "$project_dir/src/InventorySelectionNavigator.cs" \
    "$project_dir/src/PortalMenuContext.cs" \
    "$project_dir/src/EmblemForgeContext.cs" \
    "$project_dir/src/ControllerMenuSelection.cs" \
    "$project_dir/src/InventoryItemLifecycle.cs" \
    "$project_dir/src/InventoryLifecyclePatches.cs" \
    "$project_dir/src/InventoryFeedback.cs" \
    "$project_dir/src/InventoryCursorPosition.cs" \
    "$project_dir/src/ControllerFrontEndMenus.cs" \
    "$project_dir/src/ControllerInteractPrompt.cs" \
    "$project_dir/src/ControllerAimPatches.cs" \
    "$project_dir/src/TripleShotAimPatch.cs" \
    "$project_dir/src/HelperAttackAimPatch.cs" \
    "$project_dir/src/MouseOnlyHelperAimPatch.cs" \
    "$project_dir/src/CameraAimPatches.cs" \
    "$project_dir/src/ProbeRunner.cs" \
    "$project_dir/src/ControllerSupportGadget.cs"

echo "Built $output_dir/ControllerSupport.dll"
