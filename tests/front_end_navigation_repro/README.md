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
