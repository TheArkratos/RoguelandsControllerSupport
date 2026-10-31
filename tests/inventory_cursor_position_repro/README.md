# Inventory mouse-cursor handoff regression

Run `dotnet run --project tests/inventory_cursor_position_repro/InventoryCursorPositionRepro.csproj -p:UseSharedCompilation=false` from the repository root.

Compiles the unmodified native `ActionCursor` with minimal Unity stubs and the production `InventoryCursorPosition` helper. Native controller mode writes a stale controller world point; physical mouse ownership must restore held-item and hover-item positions, respecting native z. Controller hover follows selection, unrelated combat cursors remain untouched, and missing camera/game references are safe.

Observed RED before helper implementation: `mouse takeover must restore held icon to mouse: actual=999,20,0` (expected 25,15,-3). Observed GREEN after the narrow postfix correction. This reproduces the stale-controller-mode source path; it does not prove the physical game's reported right-edge symptom has this cause or establish visual appearance.
