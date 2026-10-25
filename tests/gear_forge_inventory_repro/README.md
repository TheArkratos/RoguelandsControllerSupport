# Gear Forge shared inventory integration

Run from the repository root:

```bash
dotnet run --project Mods/ControllerSupport/tests/gear_forge_inventory_repro
```

The fixture compiles the actual shared crafting context, inventory navigator,
action adapter, bindings and joystick state. Its 51 checks cover Gear inputs and
output, native primary/secondary click routing, metadata preservation, both native
quick-transfer directions, ingredient navigation shortcuts, fresh Craft presses,
six-page wrapping and the native visual-page unlock lookup contract, remapping,
mouse ownership, modal freeze, idle validation silence and pending-drop quarantine.

Same-root Gear/Alchemy transitions are tested before the frame cache and through
public virtual-click reads: cached primary/secondary input and held repeat are
cleared, held directions require neutral, and transfer/drop/cancel/page actions
must rearm. These checks protect the shared pane even when its GameObject does not
change as craftType changes.

Native Gear secondary placement permits one item into an eligible empty input.
The native validator then rejects that same ingredient ID, so matching-stack
held repeat stops; a fresh attempt retains native rejection. This implementation
does not bypass that validator or invent a controller-only stacking repair.

Unity objects, lifecycle and native item operations are explicit test doubles.
Primary and place-one results are replayed between production Prepare and Update
to model native dispatch order. The unlock lookup double models native page-to-row
mapping; the production context calls the method instead of indexing visual pages.
The separate native IL fixture verifies real native method identities, and the
crafting-context fixture covers eligibility in isolation. This fixture does not
prove real forge progress, random output, sounds, rendering, persistence,
networking or save conservation. No installation, launch, profile or save is used.
