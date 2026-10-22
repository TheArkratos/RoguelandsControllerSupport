# Front-end navigation repro

Run `dotnet run --project Mods/ControllerSupport/tests/front_end_navigation_repro/FrontEndNavigationRepro.csproj`
from the repository root. This compiles the production `ControllerFrontEndMenus.cs`
against narrow Unity/GadgetCore stubs and invokes its actual menu collection and
direction-step logic. No game or save is touched.

The title checks encode the requested Data→Right→Options, Quit→Up→Data,
Play→Right→Options, and Options→Up→Play transitions. Character checks encode
two columns of three slots, Back beneath them, and all vertical neighbors.
The process exits nonzero while any expected transition fails. Synthetic
collider coordinates reproduce the observed spatial-routing skips;
they are **not** a claim about exact in-game pixel coordinates. A passing fix
should make navigation follow menu semantics regardless of collider spacing.

The focused input checks also exercise both stick axes through the production
`Begin` path on title, character select, single/multiplayer, and pause menus.
They verify left-only and right-only navigation, a single step when both sticks
point the same way, and deterministic right-stick priority when they disagree.

The pregame navigation regression also exercises latched D-pad directions through
main, options, character selection, character creation, the choice grid,
multiplayer/host controls, and server/connect controls. It checks repeat timing,
release/reversal, simultaneous and rolling diagonals, and navigation without
synthetic activation. A direction held across a menu-root change must return
to neutral before navigating the new menu.

Character entry checks prefer native `bs0` regardless of collider collection
order or stationary mouse hover, reset on re-entry, and fall back to the first
eligible row-major character slot. Physical movement/clicks can take selection
back. Disabled/hidden slots cannot become the controller default.

Server-entry primary activation goes through the existing native click adapter.
Deleting saved servers, character deletion confirmation, and destructive data
controls retain their existing mouse-only protections. IP/port/name fields may
be selected; their text is still entered through the native keyboard path.
No network connection, server preference write, game process or save is used by
these tests.
