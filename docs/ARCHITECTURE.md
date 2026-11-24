# Architecture

Controller Support adapts native game input and actions through GadgetCore/Harmony.
The installed game remains authoritative for items, recipes, combat state and save
persistence. The mod supplies controller intent, selection, presentation and safety
checks around those existing paths.

## Responsibilities

| Area | Main files | Responsibility |
| --- | --- | --- |
| Lifecycle | `src/ControllerSupportGadget.cs`, `src/ProbeRunner.cs` | Configure shared services, maintain the input probe and reset native presentation on unload |
| Device and bindings | `src/RawLinuxJoystickProbe.cs`, `src/SdlControllerBackend.cs`, `src/Sdl2NativeControllerApi.cs`, `src/MappedControllerState.cs`, `src/JoystickInputState.cs`, `src/InputProbe.cs`, `src/BindingConfig.cs` | Select one backend, normalize state/edges and interpret configured actions |
| Ownership and gameplay | `src/LastInputSource.cs`, `src/ControllerInputOwnership.cs`, `src/ControllerGameplayInput.cs` | Decide live controller ownership, consume UI actions and adapt native gameplay input reads |
| Selection and actions | `src/InventorySelectionNavigator.cs`, `src/ControllerMenuSelection.cs` | Discover targets, navigate, prepare native A/X/Y actions, handle repeat and request safe closure |
| Item conservation | `src/InventoryItemLifecycle.cs`, `src/InventoryLifecyclePatches.cs` | Track exact origins, return held items, handle explicit drops/rollback and quarantine pending mutations |
| Station contexts | `src/ItemConversionContext.cs`, `src/CraftingMenuContext.cs`, `src/CombatChipMenuContext.cs`, `src/ShipDroidMenuContext.cs`, `src/GearModificationMenuContext.cs`, `src/QuestMenuContext.cs`, `src/PortalMenuContext.cs` | Identify native UI state, expose valid targets and route screen-specific native actions |
| Presentation | `src/InventoryFeedback.cs`, `src/InventoryCursorPosition.cs`, `src/InventoryNativeCursor.cs`, `src/SelectionCornerRenderer.cs` | Render hints/quantities/selection and preserve native cursor/tooltip behavior on handoff |
| Front end and dialogue | `src/ControllerFrontEndMenus.cs`, `src/ControllerDialogueInput.cs` | Native highlights, safe confirmation, story choices and fresh-press dialogue advancement |
| Aim and exploration | `src/ControllerAim.cs`, `src/ControllerFacing.cs`, `src/ControllerLookDirection.cs`, `src/ControllerExploration.cs` | Share aim/facing arbitration, remembered walking direction and exploration targeting |
| Aim assist | `src/ControllerAimAssist.cs`, `src/AimAssistPatches.cs`, `src/MouseOnlyHelperAimPatch.cs` | Reuse shared targeting for eligible attacks, staff projectiles and helper effects |

## Input and ownership

`InputProbe` polls the device and tracks the latest meaningful input source.
`ControllerInputOwnership` exposes the live ownership decision to gameplay,
menus, aiming and presentation. Consult it at dispatch time: a selection or input
snapshot prepared earlier in the frame can become stale after mouse takeover.

Menu actions consume logical bindings before gameplay reads them. Held actions
need explicit release/rearm rules when a menu opens or closes. Native Unity input
edges can arrive later than raw device edges; the menu-jump regression covers
this timing. Do not assume consuming one raw button event also consumes every
native action associated with it.

Unity's named Jump/Dash input merges keyboard and legacy joystick sources.
Do not identify that source by checking whether a raw pad button is still held:
Unity and Linux joystick edges can arrive in different frames. For the supported
native build, `ControllerGameplayInput` reads physical Space/Q/E for the keyboard
contribution while a raw pad is connected, even during keyboard/mouse ownership.
Configured controller actions retain the live menu and release gates. If raw
input is disconnected or unavailable, the original named-input fallback remains.

The persistent probe starts at gadget initialization, including title screens.
Verbose diagnostics are controlled by `ROGUELANDS_CONTROLLER_DIAGNOSTICS=1`;
ordinary gameplay must not depend on diagnostic output.

The experimental `sdl2` backend is selected explicitly through
`ROGUELANDS_CONTROLLER_BACKEND`; the legacy Linux backend remains the standard-build
default. A separate `--deck` build embeds private Linux x86 and x86_64 SDL2 resources and
selects SDL2 by default; an explicit backend environment setting still takes priority.
`InputProbe` polls the selected producer once per Unity frame, including reads
before `ProbeRunner.Update`. `SdlControllerBackend` owns selection/reconnect and
failure containment; `Sdl2NativeControllerApi` owns native binding and balances
only its own SDL GameController subsystem reference. It does not initialize video,
run a second event loop, overwrite Steam filter hints, or call Steam initialization.
`MappedControllerState` translates SDL snapshots to the existing canonical Xbox
binding positions, preserving the gameplay/menu action interface. Initial held
snapshots are not fresh edges or ownership activity. No raw fallback is merged
after an SDL failure. Missing libraries disable that producer for the session.

`BundledSdl2` validates the embedded checksum before extracting content-addressed
bytes to `GadgetPaths.TempPath`, and refuses to overwrite unexpected cached bytes.
The native resource is selected from the current process pointer size, not the
operating system's bitness. Both ordinary 32-bit Steam launch and 64-bit native
launch use the same ZIP without executable overrides.
Native code is never placed in GadgetCore's managed-assembly `Libs/` scan. The
private SDL build is loaded locally with Linux deep binding and isolated internal
symbols. Only this private, windowless copy has joystick/controller events disabled:
polling and hotplug still update state without filling an unconsumed event queue.
External/system SDL overrides retain host event settings untouched.

The explicit native virtual-controller smoke covers disabled-event polling and
coexistence with a separately initialized system SDL. The standalone legacy Mono
smoke loads the production DLL in the game's exact embedded Mono/mscorlib without
starting Unity or game code. Neither is physical platform acceptance: Steam runtime,
Deck visibility, actual-game focus/handoff and Windows patch-target compatibility
still require testing on their respective runtimes.

## Menus and held items

`InventorySelectionNavigator` owns navigation and target discovery. The shared
`InventorySelectionSlots` helper in that file centralizes slot classification.
`ControllerMenuSelection` uses the selected context to route native actions and
render feedback. Shared contexts describe eligibility and native operations;
keep hints and hover paths read-only. Recipe eligibility must not roll outputs
or invoke random-producing recipe getters.

The native `holdingItem` remains the authoritative held value.
`InventoryItemLifecycle` records where an item came from and distinguishes actual
inventory/storage origins from generated outputs or claimed rewards. A quest
reward must not return into a quest output and trigger another reward/XP action.
A close request may need to return an item first; failed returns or uncertain
pending drops must preserve the held item rather than silently clear it.

Conservation guards can outlive controller presentation. A pending native drop
or queued mutation still needs safe resolution after mouse takeover. Keep this
narrow safety policy separate from controller-only visual and action ownership.

Expanded storage is an optional companion integration. Controller Support has no
build-time dependency on its DLL; verify bridge absence and native slot behavior
as well as paired operation. Keep common slot geometry/discovery consistent across
selection, feedback and lifecycle consumers.

## Aim and native patching

`ControllerAim` supplies a common world target. Explicit stick aim takes priority;
neutral movement can supply horizontal direction, while sword attack state defers
movement-derived turning. `ControllerLookDirection` shares remembered walking
facing with exploration and explicit drop placement. Avoid independent helper
homing policies that disagree with the main aim state.

Harmony adapters target verified native methods and iterator `MoveNext` bodies.
Preserve existing guards, loader behavior and random-call ordering. Where a patch
requires a specific IL shape, reject a mismatch instead of partially rewriting it.
The native IL fixture checks compiled targets and patch discovery, complementing
small fixture tests rather than replacing a real game test.

## Development boundaries

- Extend the existing context/action/lifecycle path when adding a station.
- Centralize repeated slot ranges and discovery rules; keep unrelated UI contexts
  out of gameplay input and core item conservation code.
- Extract cohesive operations from large coordinators only after their current
  input consumption, rollback and handoff contracts have focused coverage.
- Keep authored stubs and generators public. Generate native fixture source from
  the developer's local references into ignored `obj/` directories.
- Use the repository runner for the full suite, at most two fixtures concurrently.
  Keep results under ignored build output. Physical acceptance is tracked separately
  in [STATUS](../STATUS.md).

The public repository contains runtime source, authored assets, packaging, fixtures
and concise developer documentation. Optional local research archives are neither
dependencies of normal builds nor source material to redistribute with releases.
