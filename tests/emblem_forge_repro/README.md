# Shared item conversion integration regression

Runs the actual `ItemConversionContext`, shared inventory navigator, binding parser,
raw joystick state, action adapter and hover/cursor helper. Unity scene behavior
and native/loader conversion are deterministic doubles. The source-backed native
replay covers Emblem Forge, Item Trasher and Prism Forge. It sets `slotID` from the selected collider before applying the installed
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

Item Trasher checks cover nested `bTrasher` discovery and right-stick navigation,
backpack-only conversion, protected IDs 51/52, exact in-place Credits output,
custom effective item values, no repeat on a held primary, mode-first B, held-item
recovery, disabled targets, remapping/freeze/quarantine and physical mouse priority.
One thousand idle frames assert no pricing queries or native conversions. Native
value calculation is a test-double provider; the controller never computes prices.
Actual Trash opener and cached-location fallback run in `inventory_lifecycle_repro`;
compiled patch discovery and native TrashItem target coverage run in
`phase51_native_il_repro`.

Prism checks cover nested `bPrism` discovery/right-stick navigation, native toggle
and purple indication, all three shard IDs at 9/10/25 quantities, exact output
and remainder with stale initial `slotID`, safe ten-input custom recipes and
rejection of unsafe counts/invalid outputs. Generated output keeps shared safe
return restrictions; native Prism ends conversion mode and does not rearm on
placement. Cancellation, held-item recovery, custom bindings, mouse ownership,
freeze/quarantine, idle frames and direct red/blue/purple transitions are covered.
The real renderer fixture checks each tint at 1.5x over repeated frames while
preserving anchors, held-item appearance and quantity geometry.
