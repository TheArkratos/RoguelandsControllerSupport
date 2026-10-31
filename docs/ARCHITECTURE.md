# Architecture

Controller Support adapts native game input and actions through GadgetCore/Harmony.
The installed game remains authoritative for items, recipes, combat state and save
persistence. The mod supplies controller intent, selection, presentation and safety
checks around those existing paths.

## Responsibilities

| Area | Main files | Responsibility |
| --- | --- | --- |
| Lifecycle | `src/ControllerSupportGadget.cs`, `src/ProbeRunner.cs` | Configure shared services, maintain the input probe and reset native presentation on unload |
| Device and bindings | `src/RawLinuxJoystickProbe.cs`, `src/JoystickInputState.cs`, `src/InputProbe.cs`, `src/BindingConfig.cs` | Read raw Linux events, track state/edges and interpret configured actions |
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

The persistent probe starts at gadget initialization, including title screens.
Verbose diagnostics are controlled by `ROGUELANDS_CONTROLLER_DIAGNOSTICS=1`;
ordinary gameplay must not depend on diagnostic output.

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
