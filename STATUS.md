# Controller Support status

Development source release for native Linux Roguelands with GadgetCore. No binary
release is published yet. Earlier controller gameplay has user acceptance in an
isolated test profile; the latest Jump/Dash input-isolation fix and Questbot work remain
pending physical acceptance. Build success and offline fixtures are separate
from installation or gameplay verification.

## Feature acceptance

| Feature | Current evidence | Remaining checks |
| --- | --- | --- |
| Movement, configurable input, combat aiming and input handoff | Earlier controller/combat play accepted; staff testing and sword movement-facing lock accepted; automated coverage | Neutral combat entry and individual special weapon/augment effects |
| Inventory, equipment, hotkeys, storage and portals | Earlier general menu/controller use accepted; offline fixtures | Detailed conservation, rollback, every close route and save/reload |
| Emblem/Prism Forge, Item Trasher, Alchemy/Gear Forge, combat chips | Shared controller actions implemented; offline fixtures | Individual effects and persistence, custom-recipe compatibility |
| Ship Droid upgrades and collection | General UI operation accepted; offline fixtures | Cap/full-bag cases, individual side effects and persistence |
| Ultimate Forge, Gear Modification and explicit drops | General preceding package accepted; offline fixtures | Individual station effects, near-wall drops and exceptional handling |
| Pause highlights | General preceding package accepted; offline fixtures | Detailed handoff/options/cancel cases |
| Character creation | Accepted using keyboard/on-screen keyboard name entry | Controller text entry is outside current scope |
| Character deletion | Native deletion accepted; confirmation defaults No; offline fixtures | Detailed cancellation, handoff and reload cases |
| Story choices | Implemented with offline coverage | Physical choice/cancel/handoff and saved consequences |
| Questbot | Delivery, claim, Shuffle, safe reward stow and drop implemented; offline fixtures | Physical delivery/reward/XP, Shuffle, handoff and save/reload |
| Legacy A-to-Jump and bumper Dash aliases | Delayed legacy edges reproduced; keyboard Space/Q/E separated from raw controller input; fix installed in the isolated profile | Repeated A/RB presses in gameplay, menus and after menu closure; configured Jump/Dash and keyboard handoff on a real controller |
| Standalone and expanded storage | Independent DLL and optional companion bridge; paired lab/offline evidence | Clean controller-only native storage, paired sixth row, absent bridge/load order and persistence |
| Build Mode | No controller implementation | Future work |
| Multiplayer | Unverified | Separate acceptance work |

## Current input-isolation fix

The previously installed candidate still allowed legacy A-to-Jump and RB-to-Dash
events after raw button release. The new regression failed with both actions
before repair. Unity's named input combines keyboard and joystick sources;
checking the current raw held state cannot identify a delayed Unity event.

Jump/Dash now read physical Space/Q/E for their keyboard contribution while the
raw pad is connected, including keyboard/mouse ownership. Configured controller
bindings retain live menu and release gates. Native fallback remains when raw
input is disconnected or unavailable. Other named-input adapters are unchanged.

The production build and all 53 regression projects pass with no failures or
skips, including 355 menu/input ownership checks and 383 compiled native IL
checks. All 17 Python tooling checks pass. Coverage includes delayed edges,
ownership handoff, simultaneous keyboard input, menu update order and closure,
disconnection, A-to-Jump remapping and directional RB-to-Dash remapping.

This candidate is installed only in the existing isolated local test profile.
A fresh complete profile backup was verified before replacement; only the mod
ZIP changed. Save, bindings and other packages are unchanged. Receipts and hashes
remain private. The normal game installation was not updated. No game was
launched during repair or installation; physical acceptance is pending.

## Earlier cleanup checkpoint

The production build and all 53 regression projects pass after the cleanup,
with zero failures or skips. This includes 235 aim-assist integration checks,
373 native IL checks, and 282 synthetic combat-guide checks. All 17 Python
tooling checks also pass, including eight publication-audit checks. The guide
fixture now uses authored geometry and camera rolls instead of exported asset
reports; it does not verify native asset
values or dialogue transitions. Earlier asset audits remain local research.

The cleanup caches reflection metadata while reading live values, removes a
duplicate cursor update, centralizes slot/action identifiers, and makes verbose
diagnostics opt-in. The build retains the installed game's reference assemblies.
The cleanup candidate from source commit `833173e` has been rebuilt, packaged
and installed only in the existing isolated local test profile. The fresh build
also passes the binding/export and native IL fixtures. The current profile and
prior package were backed up and verified first; only the mod ZIP changed,
with save, bindings and other packages unchanged. Installation receipts remain
private. The normal game installation was not updated. No game was launched;
physical acceptance is pending. Tests use fixtures and compiled/native IL,
not a game profile.

## Known limits and next acceptance work

- Native Linux, one USB Xbox controller and the tested `/dev/input/js0` mapping
  are the verified input scope. Other layouts, Steam Input configurations and
  platforms require calibration and testing.
- Finish physical Jump/Dash isolation and Questbot acceptance before treating the latest
  candidate as accepted. Verify mouse/keyboard handoff alongside controller actions.
- Complete native storage and optional companion storage checks separately,
  using disposable items and exact quantities across save/quit/reload.
- Keep special weapon/augment behavior, saved story consequences, station side
  effects and multiplayer as explicit tests rather than inferring success from
  general gameplay acceptance.
- Verbose controller diagnostics are disabled unless
  `ROGUELANDS_CONTROLLER_DIAGNOSTICS=1` is set when launching the game.

Build, configuration and test commands are in [README.md](README.md). Current
source responsibilities and extension rules are in [architecture](docs/ARCHITECTURE.md).
