# Gameplay action routing regression

Links production gameplay input/actions, config parsing and raw pad state. Uses
Unity/input eligibility doubles; does not execute real item/chip effects.

Checks hotkeys in both gameplay modes, A/native-jump and B/native-Cancel isolation,
first-direction D-pad latch, hotkey consumption, remapping, L3 toggling, UI/modal/
scene/mode/local-player guards and fresh-release requirements. Same-frame raw
press/release taps are covered. Native effect/cost routing and ten input reads
are separately verified against installed assembly IL by phase51_native_il_repro.

Run `dotnet run --project tests/gameplay_actions_repro/GameplayActionsRepro.csproj -p:UseSharedCompilation=false`.
