# Emblem Forge integration regression

Runs the actual `EmblemForgeContext`, shared inventory navigator, binding parser,
raw joystick state, action adapter and hover/cursor helper. Unity scene behavior
and native/loader conversion are deterministic doubles. The source-backed native
replay sets `slotID` from the selected collider before applying the installed
loader's conversion. It does not establish physical or multiplayer acceptance.

```bash
dotnet run --project Mods/ControllerSupport/tests/emblem_forge_repro/EmblemForgeRepro.csproj
```

Covers nested `bEmblem` discovery, configurable activation, native slot dispatch,
input remainder/output quantity, custom recipe safety (only ten-input recipes),
mode rearming, generated output without invented return provenance, B mode
cancellation, held-output primary cancellation, secondary/transfer restrictions,
freeze/quarantine, mouse priority and pane transitions. Equipped and hotkey
conversion targets fail closed; ordinary backpack slots 6–35 are supported.
The separate lifecycle regression exercises real provenance/range-exit code;
feedback UI checks exercise the shared `Cancel forging` label.
