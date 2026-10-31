# Changes

## Unreleased

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
