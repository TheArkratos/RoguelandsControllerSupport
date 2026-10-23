# Native inventory cursor regression

Exercises actual ControllerVisibleCursor, InventoryNativeCursor and the unchanged
generated native ActionCursor.Update, using modeled Unity renderers/lights.

```bash
dotnet run --project Mods/ControllerSupport/tests/native_inventory_cursor_repro/NativeInventoryCursorRepro.csproj -p:UseSharedCompilation=false
```

Covers stale native controller flags without a mod aim owner, physical mouse
position/z, conversion mesh/particle/light suppression, originally-disabled state,
same-frame handoff, all three station roots, close/switch/unload restoration and
preserved root activation. Shared ownership is explicitly controlled in this
fixture; the production policy source is linked by the common test targets.

Before the correction, the native-sequence reproduction used (999,20,0) instead
of mouse world position (25,15,-3), and conversion effects remained visible while
controller-owned. Final result: 22 checks pass. Renderer state checks cannot prove
Unity appearance; perform the physical checklist after test installation.
[Evidence](../../Reports/native-inventory-cursor-followup-2026-10-23.md).
