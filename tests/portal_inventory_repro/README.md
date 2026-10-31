# Portal inventory routing regression

Runs the production inventory navigator, controller action adapter, binding parser,
raw joystick state and held-cursor helper against deterministic native/Unity doubles.
The portal transport double controls target eligibility and native versus GadgetCore
routing; the separate `portal_context_repro` exercises the real portal bridge.

```bash
dotnet run --project tests/portal_inventory_repro/PortalInventoryRepro.csproj -p:UseSharedCompilation=false
```

Covers shared inventory/portal selection, right-stick navigation and left-stick
independence, mouse priority, A-only portal actions, configurable activation,
held-item preservation, B lifecycle reuse, pane-transition rearming, page target
refresh and suppression of duplicate native actions for GadgetCore-routed clicks.
This does not establish physical controller, Unity scene, multiplayer or rendered
UI acceptance.

The tooltip regression replays native hover dispatch after production `Prepare`:
`GameScript.Update` selects the native hover branch, `HoverPlanet` is empty,
and `HoverItem` / `HoverDifficulty` activate the shared `hoverItem` object.
Verified against `GameReference/Generated/Assembly-CSharp/GameScript.cs`.
It covers controller selection changes and ownership takeover before native hover,
correct native item/difficulty repopulation, stable-target behavior and unchanged
physical mouse handling.
