# Phase 3 geometry/identity replay

Run from the repository root:

```sh
dotnet run --project tests/phase3_geometry_repro/Phase3GeometryRepro.csproj -p:UseSharedCompilation=false
```

This compiles the **production** `InventorySelectionNavigator.cs` against a
minimal, deterministic Unity scene/input stub. It replays the identity problem
in the preserved 2026-10-19 `[DEBUG-P3GEOM]` trace without copying any save or
unrelated log lines. The two direct `inventoryMain/6` children are distinct:
one is a stat collider (`tag=stat`, Faith), the other an inventory-grid collider
(`tag=Untagged`). A name-only `Transform.Find("6")` caches just one; Faith is hit
by the mouse ray but selection remains the prior stat in the old implementation.
The test selects both objects independently, checks right/down grid steps from
grid slot 6 to 7/12, and retains equipped-droid and hotkey hover. It also
checks mouse hover on every one of the 45 main-section slots.

Minimal captured facts (screen coordinates, pixels):

| Object | Hit collider | Visible renderer/text | Selected after hover |
| --- | --- | --- | --- |
| Faith `inventoryMain/6` | `(742.4,731.3)–(862.4,779.3)` | `txtFTH (809.9,734.7)–(849.0,783.7)` | Previous stat, not Faith |
| Droid `inventoryMain/42` | `(262.0,868.0)–(382.0,972.0)` | Icon `(250.0,840.0)–(410.0,1000.0)` | Droid 42 |
| Hotkey `inventoryBar/0` | `(381.0,983.2)–(469.0,1071.2)` | Renderer `(345.0,947.2)–(505.0,1107.2)` | Hotkey 0 |

The other hotkeys `inventoryBar/1..5` were also hit and selected in the trace.
The trace establishes collider-vs-renderer differences, but **not** the desired
corner rectangle. In particular, the droid's collider left edge is 12 px to
the *right* of its icon's left edge, whereas the user described the drawn left
corners as too far left. Without a screenshot or logged corner transforms, a
geometry assertion would invent a target and could validate the wrong fix.
The same limitation applies to the stat corners described as too far inward.

Initial result: `FAIL Faith hover selects its own stat collider, not prior stat`
and four passing control checks. After direct-child, role-aware discovery, all
53 checks pass. This does not test sprite placement or in-game rendering.

## Equipped-item row navigation regression (2026-10-19)

The same command now includes a minimal four-collider right-column fixture
compiled against the real navigator. The archived trace shows inventory slot
11 centered at `(850.3,555.0)`, equipped-item slot 41 at `(850.3,655.1)`,
and Faith at `(802.4,755.3)`. Thus the equipped-item row is **above** the
ordinary inventory grid on screen despite its slot numbers following that
grid. Slot 35 is included only to expose the erroneous numbered-grid step;
its `y=155` follows the observed 100-pixel grid spacing but was not directly
captured in the trace. Its precise position is not needed for that assertion.

The red repro is deterministic across two consecutive runs, takes roughly
three seconds, and produces these focused results:

```text
FAIL up from inventory 11 reaches equipped 41: selected=6/stat, expected=41/Untagged
PASS down from Faith reaches equipped 41
FAIL down from equipped 41 enters inventory 11: selected=41/Untagged, expected=11/Untagged
FAIL up from equipped 41 does not jump to inventory 35: selected=35/Untagged, expected=6/stat
RESULT 3 failures
```

The pre-existing identity/hover checks remain green. This is an unattended
regression test; no production source, installed mod, or save was changed.
