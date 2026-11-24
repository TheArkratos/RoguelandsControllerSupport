# Changes

## Unreleased

- User reports basic controls working on native 32-bit Steam Deck in Steam Desktop
  Mode after correcting the active Steam controller layout. The dual-architecture
  ZIP removes the initial 32-bit loading blocker without custom launch options.
  Steam Input enabled alone did not ensure a usable gamepad layout. Full physical
  acceptance and Windows/Proton support remain outstanding.

- Added a separate self-contained Linux x86/x86_64 Deck test build with automatic SDL2
  selection, licensed/pinned native dependency, checksum-verified temporary
  extraction and a backup-first test guide. Standard build defaults remain
  unchanged; Windows/Proton is not included in this package. The library matches
  process bitness automatically; no Steam launch override is required.
- Isolated bundled SDL input-event settings from host SDL and added extraction,
  private virtual-controller/host-coexistence and exact embedded-Mono smoke checks.
  This prepares a test candidate, not a confirmed repair of the reported failure.

- Added an opt-in experimental SDL2 mapped-input backend, stable existing binding
  positions, disconnect/reconnect handling and missing-library containment. The
  standard package does not bundle SDL2; Deck and Windows/Proton remain unverified.
- Documented the reported native Linux Steam Deck input failure. Its root cause
  is not yet reproduced or confirmed.

- Fixed delayed legacy A-to-Jump and RB-to-Dash input after raw button release
  or input handoff. Keyboard Space/Q/E are separated from legacy pad aliases
  while the raw controller is connected; configured controller remaps remain active.

- Removed duplicate native-cursor updates and cached reflection metadata while
  retaining live input ownership, health, credit and item-state reads.
- Named shared inventory/storage slot ranges and menu action indices.
- Disabled verbose input and aim traces by default; enable them with
  `ROGUELANDS_CONTROLLER_DIAGNOSTICS=1`.
- Replaced asset-report dependencies in combat-guide checks with synthetic
  geometry and camera rotations, keeping historical audits local.
- Added an independent publication audit for excluded paths, recognizable
  credentials, private machine paths and history reachable from the current branch.

- Standalone native Linux controller mod with configurable bindings, shared menu
  navigation/actions, combat aiming and mouse/keyboard handoff.
- Station, inventory, dialogue, character-selection and Questbot support, with
  physical acceptance limits recorded in STATUS.md.
- Regression for delayed native A-to-Jump input after menu actions; real-device
  acceptance remains pending.
- Installed-game reference configuration, locally generated native test inputs,
  opt-in controller diagnostics and a bounded-concurrency regression runner.
- Public source and developer documentation separated from private profiles,
  historical research evidence and personal save-edit records.

No binary release has been published. Offline coverage does not establish
persistence or multiplayer correctness.
