# Controller Support status

Development source release for native Linux Roguelands with GadgetCore. No binary
release is published yet. Earlier controller gameplay has user acceptance in an
isolated test profile; the latest Jump/Dash input-isolation fix and Questbot work remain
pending physical acceptance. Build success and offline fixtures are separate
from installation or gameplay verification.

## Steam Deck user-reported basic controls working — 2026-11-24

The user reports the dual-architecture candidate works after correcting an
unexpected active Steam controller layout. Test environment: **native Linux,
32-bit game, Steam Desktop Mode**, per-game Controller override **Use default
settings**, with Steam Input shown enabled. No custom launch options were needed.
The exact old/new layout names and individual action checklist were not supplied.

Two distinct blockers were established during this test sequence:

1. The initial x86_64-only candidate rejected the normal 32-bit launch before SDL
   initialization. Bundling x86 and x86_64 and selecting by process bitness removed
   that rejection without requiring an executable override.
2. With the replacement installed, the user's log showed SDL initialized but
   **0 joysticks / no mapped gamepad**. Steam Input being enabled did not establish
   a usable gamepad layout. After changing the unexpected active layout, the user
   reported: “it works now”. The mod needs a gamepad-emitting layout, not merely
   an enabled Steam Input setting. Desktop Mode itself was not the blocker.

**Acceptance:** user-reported basic controller operation on Deck; not an
agent-observed physical test or exhaustive menu/gameplay/inventory/save/reconnect
acceptance. Windows/Proton remains unverified and is not included in this ZIP.
This sequence does not establish the original raw-backend failure's root cause.
No runtime logs or private user data are included in the public repository.

## Deck user test: 32-bit launch blocked the first package — 2026-11-23

The supplied Deck log confirms Controller Support loaded, but Steam launched the
**32-bit native game**. The x86_64-only package selected SDL2 and then rejected the
process with `PlatformNotSupportedException`, before native initialization or
device detection. This establishes the immediate blocker in that test, not the
cause of the earlier raw-backend failure or proof that SDL controls will work.

The user requires normal Steam launch for a shareable mod: no executable override
or mod-specific launch configuration. **The earlier x86_64-only installation
recommendation is superseded** by the replacement below.

## Dual-architecture replacement ready for controlled installation — 2026-11-24

`build/ControllerSupport-Deck.zip` now embeds pinned SDL2 libraries for **both
Linux x86 and x86_64**, automatically selected by process pointer size. Leave
Steam Launch Options empty for this mod; no executable change, external library
installation or backend environment variable is required. Windows/Proton remains
a separate, unverified packaging task.

- `python3 -B tools/prepare_deck_sdl2.py --jobs 2`: built both stripped private
  libraries; each ELF passed architecture, exports, privacy and glibc 2.17 checks.
- `bash build.sh --deck` and `bash package.sh --deck`: succeeded. One managed DLL
  contains both native libraries and checksums; per-architecture licenses and
  provenance accompany the ZIP. The guide now names the correct mod-specific log.
- `python3 -B tools/check_deck_package.py`: **passed against the actual ZIP**.
  Its DLL selected/extracted/loaded matching SDL bytes in **both exact game Mono
  runtimes**, including initialization, update/count and disposal. Both actual
  embedded resources matched packaged provenance and passed ELF/privacy checks.
  These standalone harnesses do not start Unity/game code or test physical input.
- The x86 embedding harness needed an executable-stack declaration for the shipped
  old x86 Mono under modern host glibc. Only its newly generated, ignored harness
  executable is adjusted; no installed game/runtime or distributed mod/native
  binary is modified, and users need no such configuration.
- Extraction/selection fixture: **15 checks**. Full suite: **57 projects passed,
  0 failed, 0 skipped**. Standard source build also succeeded against legacy game
  references, with only existing Unity obsolete-member warnings.
- Python tooling: **61 tests passed**; public history audit: **0 issues**;
  `git diff --check`: clean.

Native hashes: x86 `37a046d494357ad5861eeb68e34e9fd8fc354381bf6146925ab6788ddb3a0e20`;
x86_64 `f625cbd5bfd9f7b25389d020f80bd083515b75e40fa0ac4341067ce04d25f98a`.
ZIP SHA-256: `0e167198421ea1093f1a746bdaef9db86040678a5cc7b432adb8cc0e8139f434`;
sidecar and machine-readable validation receipt remain under ignored `build/`.

**Installed by agent:** no. **Physical acceptance at this preparation checkpoint:** pending;
see the later user-reported basic-controls result above.
No saves or profiles touched. Preserve a fresh current save and prior installed
package before replacement; follow [the title-screen-first test guide](docs/STEAM-DECK-TEST.md).
The known 32-bit load rejection is addressed; actual Deck controller visibility
and dispatch still require the user's test before distribution as a working release.

## Earlier x86_64-only Deck test package preparation — 2026-11-23

A separate `--deck` build now packages a private Linux x86_64 SDL2 dependency and
selects SDL2 automatically. This targets a **controlled Steam Deck installation
test**, not a confirmed repair or a supported-platform release. The standard
package still defaults to the existing raw Linux backend. No Windows/Proton DLL
or 32-bit SDL library is included; see [the test guide](docs/STEAM-DECK-TEST.md).

**Ready for user installation as a controlled test:**
`build/ControllerSupport-Deck.zip` (with its SHA-256 sidecar). Preserve the current
Deck save and prior installed mod package before replacing anything.

- The native preparation tool verifies pinned official SDL 2.32.10, Zig 0.13.0
  and libudev-header downloads before extraction/build. It targets glibc 2.17
  and baseline x86_64 without video/audio/X/Wayland dependencies. Upstream Steam
  filtering is unchanged. License notices and build provenance accompany the ZIP.
- Native code is embedded in the mod DLL, checksum-verified, and extracted only
  into GadgetCore's temporary directory. It is never put in the managed `Libs/`
  scan. Existing unexpected cached bytes are preserved and rejected.
- Private SONAME/local symbol binding and Linux deep binding isolate this copy
  from host SDL. Only the private copy disables joystick/controller event delivery;
  snapshot polling/hotplug continue without accumulating an unread event queue.
- Startup logs distinguish backend selection, process bitness, initialization,
  mapped device opening and first input activity. Load failures include native
  loader details and leave keyboard/mouse available.
- The test guide covers current-save/prior-package backups, native Steam Input
  Gamepad setup, title-screen-first acceptance, log collection and mod-only rollback.
  Its optional 64-bit launch override preserves Steam wrappers and arguments.

Validation: **57 regression projects passed, 0 failed,
0 skipped**; extraction fixture **11 checks**; bundled SDL native/virtual-controller
and host-coexistence smoke **37 checks**. The production DLL loaded its embedded
SDL in the game's exact Mono/mscorlib (`3.0.40818.0`) in a standalone process,
including checksum-verified extraction, initialization, update/count and disposal.
No Unity or game code was started by that smoke. These results do not reproduce
the reported Deck session.

The final `build/ControllerSupport-Deck.zip` passed `tools/check_deck_package.py`:
the DLL taken from the actual ZIP loaded the matching stripped native resource
in exact game Mono, and package entries/manifest, license checksums, provenance,
binary privacy and the glibc 2.17 dependency ceiling all passed. Native debug data
was stripped with the pinned toolchain before hashing; final native SHA-256 is
`f625cbd5bfd9f7b25389d020f80bd083515b75e40fa0ac4341067ce04d25f98a`.
The ZIP checksum sidecar and machine-readable validation receipt are under
ignored `build/`. Python tooling: **44 tests passed**; public history audit:
**0 issues**. Test guide and license/provenance are included inside the ZIP.

**Installed:** no. **Physical Deck/game-runtime tested:** no. Existing installation
and saves remain untouched. Actual Deck device visibility, focus/gyro handoff,
title/gameplay behavior and inventory/persistence acceptance remain user tests.

## First experimental mapped backend implementation — 2026-11-23

An opt-in SDL2 GameController backend now feeds the existing controller action
state. This is a development checkpoint, **not a confirmed fix** for the reported
native Linux Deck failure and not a verified Windows/Proton port.

- Select with `ROGUELANDS_CONTROLLER_BACKEND=sdl2`. Existing native Linux users
  retain the raw backend by default. Unsupported/unknown selections fail closed;
  the raw reader is not constructed on Windows.
- One selected producer is polled once per Unity frame. SDL snapshots map to
  existing binding positions, including separate triggers and latched D-pad
  semantics. Held initialization/reconnect snapshots do not claim ownership or
  emit fresh action edges. Keyboard/mouse handoff remains in `InputProbe`.
- SDL selects the first openable mapped gamepad and retains it until disconnect.
  Disconnect and polling/load failures clear action state. Native-load failure is
  logged once, disables that producer for the session and leaves keyboard/mouse
  available, rather than merging raw/Unity fallback events.
- This experiment uses SDL2's small stable native interface to evaluate the old
  Mono integration. It does not settle the final redistribution/version choice.
  SDL is loaded dynamically; **no native SDL binaries are packaged**. Optional
  `ROGUELANDS_CONTROLLER_SDL2_LIBRARY` overrides the platform library path. The
  library must match the game process architecture; Proton needs a Windows DLL.
- No Steam Input direct actions, custom publisher configuration, global filter
  overrides, video initialization, Steam initialization or second event loop
  were added. Steam Input emulation must expose a mapped gamepad; emitted
  keyboard/mouse input remains keyboard/mouse input.

New offline fixtures cover mapped controls/default bindings, edge suppression,
D-pad snapshot transitions, initial-held/reconnect behavior, ownership,
device selection/disposal and contained native-loader failures. An optional Linux
SDL2 virtual-gamepad smoke exercises the actual native adapter through button,
trigger and disconnect state changes in the test process. Neither kind of test
reproduces the user's Deck session or establishes physical platform acceptance.

Validation for this implementation checkpoint:

- `bash build.sh` and `bash package.sh`: succeeded against the configured game
  reference assemblies; existing Unity obsolete-member warnings remain.
- `python3 -B tools/run_tests.py --jobs 2`: **56 passed, 0 failed, 0 skipped**.
- Mapped snapshot fixture: **1,522 checks**; shared probe fixture: **25 checks**;
  SDL backend fixture: **15 checks**. The explicit local native/virtual-gamepad
  smoke passed **28 checks** (including the backend checks).
- Python tooling: **17 tests passed**; public history audit: **0 issues**;
  `git diff --check`: clean. Reference verification passed before implementation.
- **Installed:** no. **Game/runtime or physical Deck/Windows tested:** no.

**Remaining before default/portable support:**

- Capture actual Deck gadget loading, selected backend logs, device visibility
  and Steam Input layout; the reported root cause is still unknown.
- Verify native loading in the game's legacy Mono, process architecture and
  Steam runtime. Plan licensed native-library distribution, including Windows
  binaries. Verify Windows game/Harmony patch compatibility separately.
- Validate coexistence with any host SDL initialization, focus behavior and
  event-queue ownership; this snapshot experiment does not consume the host's
  SDL events. Complete those checks before promoting it to the default backend.
- Physically test native Linux/Deck and Windows/Proton: title/menu/gameplay,
  reconnect, triggers/D-pad, A/RB alias isolation, mouse/keyboard/gyro handoff,
  menu release gates and the existing inventory/save acceptance checklist.

No game launch, installation, profile modification or save access occurred for
this implementation checkpoint. The installed package remains the earlier lab
checkpoint below. Build, package and offline checks are recorded separately from
runtime acceptance.

## Earlier planning checkpoint — 2026-11-23

- **Reported failure:** no controller controls work on Steam Deck with the native
  Linux game. The initial Proton description was corrected; this is not a
  confirmed Proton failure. Loader state, device enumeration and Steam Input
  layout have not yet been captured, and the cause remains unconfirmed.
- **Current implementation:** raw Linux joystick reads from `/dev/input/js0`
  through `libc`, using the tested Xbox layout. Neither Deck support nor
  Windows/Proton compatibility has been established.
- **Desired scope:** native Linux and Windows/Proton controller support,
  compatible with Steam Input. This is a goal, not a supported-platform claim.
- **Proposed direction:** evaluate SDL's mapped GameController/Gamepad interface
  with Steam Input gamepad emulation before considering direct action-based
  Steam Input integration. SDL2 versus SDL3 is undecided pending legacy Unity/
  Mono, native-library loading, process architecture and Steam runtime checks.
- **Safety requirements:** one authoritative controller-state producer; preserve
  Steam's device filtering, menu consumption/release gates, keyboard/mouse
  handoff and held-item conservation. Do not independently merge raw, Unity and
  SDL events from the same controller.
- **Next evidence:** confirm gadget loading and device/input visibility on Deck;
  establish a reproducible failure before attributing it to a particular backend.
  Physical Linux and Windows/Proton acceptance remains separate from offline
  fixture results.

Valve documents [gamepad emulation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices)
as an option alongside the direct Steam Input API. Its
[developer guide](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs)
describes full action-based integration and SDL coexistence guidance.
[SDL's mapped gamepad interface](https://wiki.libsdl.org/SDL3/CategoryGamepad)
is the candidate for evaluation; this choice is a mod-specific recommendation,
not a Valve requirement. Publisher-only Steamworks configuration is distinct
from a community controller layout.

This is a documentation checkpoint only. No input-backend code, build,
installation or runtime acceptance changed during this planning session.
Historical test counts below describe earlier checkpoints, not new test runs.

## Earlier session handoff — 2026-11-02

The installed source checkpoint is `3484166` (Jump/Dash input isolation), also
pushed to GitHub. It was built and installed only in ControllerSupportLab.
All 53 regression projects and 17 Python tooling checks passed. The current
profile and previous ZIP were backed up before installation; only the mod ZIP
changed at that time. Private receipts remain local. No binary release exists.

The user will perform manual testing before starting the next session. No
physical acceptance results for this candidate have been reported yet. Start
the next session by collecting actual pass/fail observations and updating the
checklist below; do not infer acceptance from the automated results. Preserve
earlier accepted behavior unless the user reports a regression.

### First: Jump/Dash isolation

- [ ] **Gameplay:** repeatedly tap A and RB while stationary and moving. They
  must not produce the legacy Jump/Dash actions; A's configured hotkey should
  remain usable. Verify configured Jump/Dash (defaults LT/LB) still work.
- [ ] **Menus and closure:** repeat in inventory, storage and Questbot, using
  RB for paging where supported. Close while holding A or RB, then release.
  No delayed jump/dash should follow; fresh configured actions should resume.
- [ ] **Input handoff:** switch between controller, mouse and keyboard. Verify
  Space Jump and Q/E Dash, including while holding controller buttons.
- [ ] **Reconnect:** unplug/reconnect the controller and repeat the checks.
  Keyboard fallback must work while raw input is unavailable, and configured
  controller behavior must return after reconnection.

For a failure, record the screen, movement direction, button press/release order,
whether it occurs during a menu or after closure, and Steam Input configuration
if known. The fix separates legacy joystick aliases from keyboard events; actual
keyboard events emitted by external controller-mapping software need separate
investigation if the problem persists.

### Remaining manual acceptance

- [ ] **Questbot:** delivery, rewards/XP exactly once, Shuffle, full-bag handling,
  controller/mouse handoff and save/quit/reload.
- [ ] **Item safety:** transfers, held-item return, full inventories, drops near
  walls, close routes, rollback and persistence using disposable items.
- [ ] **Storage compatibility:** native controller-only storage and the optional
  SixBySixChest bridge, including absent bridge/load order and paired persistence.
- [ ] **Combat edge cases:** neutral combat-entry aim, Valflame target changes,
  Atalanta's Eye placement/bonus and remaining special weapon/augment effects.
- [ ] **Story/station persistence:** saved choices and equipment, droid,
  crafting and modification side effects; detailed cancellation/handoff cases.
- [ ] **Multiplayer:** separate future validation; currently unverified.

Build Mode controller support and controller text entry are unimplemented,
not merely awaiting acceptance. No launch, reinstall or restore is part of this
handoff. User testing may change the profile after the recorded installation;
preserve its latest state before future runtime changes and never substitute
the historical installation backup for current progress.

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
