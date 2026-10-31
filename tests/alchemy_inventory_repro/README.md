# Alchemy shared inventory integration

Run from the repository root:

```bash
dotnet run --project tests/alchemy_inventory_repro -p:UseSharedCompilation=false
```

This fixture compiles the actual Alchemy `CraftingMenuContext`, shared inventory
navigator, action adapter, config and joystick state. The initial implementation
loop failed at selecting Alchemy input slot 131 before context collection existed.

Checks cover right-stick navigation across panels, configured primary and
secondary native-click projection, native Alchemy's held-secondary no-op, output
and backpack quick-transfer routing, held-output protection, fresh presses for
crafting, recipe/back transitions, same-frame mouse ownership, native mouse clicks,
controller reclaim, idle validation silence, pending-drop quarantine, modal freeze,
remapping and native error/feedback capture without replay.

Unity components, lifecycle and native item methods are explicit test doubles.
The primary pickup/placement result is replayed to test the adapter's post-action
and held-input behavior; this does not execute the game's complete native Update
or prove recipe costs, random results, craft-progress timing, rendering, persistence
or multiplayer. Native/loader method wiring is checked separately by
`phase51_native_il_repro`; helper eligibility is checked by `alchemy_context_repro`.
No game launch, installation, profile or save is used.

Recipe navigation uses the native layout inspected read-only with UnityPy: the
pane's 180-degree X rotation puts `bRecipe2` above the page arrows; `bRecDown` is
physically left and `bRecUp` right. Cells are two columns of six, numbered 0–5 and
6–11. Recipe cells retain normal spatial navigation; a breadth-first traversal
of the actual navigator checks all twelve are reachable from the page controls
without leaving the book. Up on the top return button holds selection. The two page arrows select each other with inward
horizontal movement, without searching unrelated targets when the peer is absent.

In the ingredient pane, only the three ingredient inputs have shortcuts: Up
selects the visible `bRecipe` book button; Down selects the actual Craft button.
Neither shortcut activates a button or changes panes. The output keeps normal
spatial navigation.

Configured LB/RB recipe paging checks cover native method routing and wrapping,
held-action guards, opposing presses, default Drop priority, remapping, modal
freeze, mouse ownership and controller reclaim. These calls use explicit native
RecipeUp/RecipeDown test doubles; actual native method identities are checked by
the separate native IL fixture.
