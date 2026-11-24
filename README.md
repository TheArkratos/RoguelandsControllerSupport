# Roguelands Controller Support

Controller controls for the native Linux version of Roguelands using GadgetCore.
The mod adds configurable input, inventory and station navigation, combat aiming,
aim assist, and handoff between controller and mouse/keyboard.

This is a development source release. No downloadable binary release is available
yet. The latest gameplay candidate has been used in an isolated test profile;
the latest menu-jump repair and Questbot integration still need physical acceptance.
Read [STATUS.md](STATUS.md) for supported features and remaining checks.

The tested device is a USB Xbox controller exposed as `/dev/input/js0`. The mod
assumes one controller and its tested axis layout. Windows, other layouts and
multiplayer are unverified. Controller text entry and Build Mode are not provided;
use a keyboard or on-screen keyboard to enter character names.

The user reports basic Steam Deck controls working with the dual-architecture
SDL2 test ZIP, using the native 32-bit game in Steam Desktop Mode after correcting
the active Steam controller layout. No custom launch options were needed. Steam
Input being enabled is not enough: select a gamepad-emitting layout rather than
a keyboard/mouse layout. Full gameplay, inventory and save acceptance remain
pending; Windows/Proton is not supported by this package. The standard backend
still reads a fixed `/dev/input/js0` with the tested Xbox mapping. See
[STATUS.md](STATUS.md) for evidence and remaining checks.

### Experimental mapped backend

Set `ROGUELANDS_CONTROLLER_BACKEND=sdl2` in the game's launch environment to
select SDL2 instead of the raw Linux reader. The default remains the existing
Linux backend. Selection is fixed for the session; failed SDL loading does not
silently mix in raw or Unity controller events. Keyboard/mouse remain available.
On non-Linux systems the raw backend is disabled rather than calling `libc`.

**The standard ZIP does not bundle SDL2.** The experiment requires a compatible native
SDL2 library matching the **game process** architecture: Linux loads
`libSDL2-2.0.so.0`; Windows/Proton loads a Windows `SDL2.dll`, not a Linux library.
Developers can set `ROGUELANDS_CONTROLLER_SDL2_LIBRARY` to its full path. Native
dependency and actual-game behavior remain separate from a system-library smoke.

The dedicated **Deck test ZIP** instead embeds pinned, licensed Linux x86 and
x86_64 SDL2 builds and automatically selects the process-matching library. Normal
Steam launch needs no mod-specific launch options or executable changes.
It extracts verified bytes only into
GadgetCore's temporary directory, never its managed `Libs/` directory. Follow
[Steam Deck test instructions](docs/STEAM-DECK-TEST.md), including current-save and
prior-package backups. Basic controls have user-reported Deck acceptance, but this
remains a test candidate, not a fully validated release or Windows/Proton package.

SDL uses its mapped GameController controls, choosing the first openable mapped
gamepad and retaining it until disconnect. Existing button tokens keep the tested
Xbox positions (A/B/X/Y, bumpers, View/Menu, L3/R3), not SDL enum numbers or arbitrary
device button numbers. Unmapped extra buttons are unavailable in this experiment.
Steam Input should supply a gamepad layout; keyboard-emitting bindings still act
as keyboard input. This is not direct Steam Input action integration. Device
filter hints and the host's Steam initialization are not overridden.

## Default controls

Controls change with the active screen. Menu actions consume their inputs before
those inputs can trigger gameplay actions.

| Gameplay action | Default |
| --- | --- |
| Move | Left stick |
| Aim / select an exploration target | Right stick |
| Jump | LT |
| Attack / dispatch to a selected resource | RT |
| Dash | LB |
| Switch Combat Mode | Y |
| Interact / reveal or advance dialogue | X |
| Inventory | View |
| Pause | Menu |
| Toggle aim assist | L3 |
| Hotkeys 1–4 | D-pad Up / Right / Down / Left |
| Hotkeys 5–6 | A / B |

Hotkeys use the game's combat chips or exploration item slots according to its
current mode. Aim assist starts enabled each gadget session. White aiming guides
and red staff crosshairs show functional targeting; these do not require diagnostic
logging. R3 has no default action.

While the raw controller is connected, Jump and Dash use your configured
controller bindings instead of the game's legacy A/LB/RB aliases. Keyboard
Space (Jump) and Q/E (left/right Dash) remain available independently. If raw
controller input is unavailable, these actions retain the game's native fallback.

| Menu action | Default |
| --- | --- |
| Navigate | Stick / D-pad, depending on the menu |
| Activate / primary item action | A |
| Secondary item action / repeat place-one | X |
| Quick transfer while empty-handed | Y |
| Drop the full held stack in inventory or storage | RT |
| Return a held item safely, then cancel / close | B |
| Previous / next storage page | LB / RB |
| Previous / next Alchemy recipe page | LB / RB |

Storage bumper shortcuts take priority over other actions bound to the same
bumpers. Storage pages wrap at either end. Release RT before another held-stack
drop. When dialogue is typing, X reveals it; a fresh press then advances it.
Story choices use their own selectable buttons.

Character deletion uses the native confirmation screen. Move outward from a
character card to its Delete button, then press A. Confirmation starts on No;
move to Yes and press A after releasing the opening press, or press B to cancel.

## Configuration

On first load the mod creates
`<Roguelands>/GadgetCore/Configs/ControllerSupportBindings.ini` if absent. Edit
with the game closed, then restart. [Phase1Bindings.ini](Phase1Bindings.ini)
records the new-file defaults; its historical filename does not limit the feature set.
Existing configuration files are preserved instead of rewritten automatically.

Button actions accept `Joystick1Button0` through `Joystick1Button19`,
`LeftTrigger`, `RightTrigger`, `DPadUp`, `DPadRight`, `DPadDown`, `DPadLeft`, or
`Unbound`. The fixed stick roles are `Move = LeftStick` and
`AimTargetOrMenuSelection = RightStick`. Bindings may be reused across contexts.
Explicit collisions within one context, duplicate keys and unknown inputs are
invalid; the mod logs a warning and leaves gameplay buttons unbound for that
session. Added actions receive compatible in-memory defaults when possible.
An added default becomes `Unbound` if it conflicts with an explicit mapping.

## Build and package

Requires Bash, the .NET 10 SDK, `bsdtar`, and an installed native Linux game with
GadgetCore. The build reads the installed game's managed assemblies. Game DLLs
and decompiled game source are not distributed with this project.

From this repository root:

```bash
# Optional if the game is installed elsewhere:
export ROGUELANDS_DIR="/path/to/Roguelands"
bash build.sh
bash package.sh
```

Without `ROGUELANDS_DIR`, the default is
`$HOME/.local/share/Steam/steamapps/common/Roguelands`. The build discovers the
active SDK through `dotnet` and writes `build/ControllerSupport.dll`. Packaging
creates `build/ControllerSupport.zip` containing `Manifest.ini`, `ModInfo.txt`,
`LICENSE` and the DLL. The four menu-corner PNGs are embedded in the DLL.
Neither script installs the mod or launches the game.

To prepare the separate self-contained native Deck test package:

```bash
python3 -B tools/prepare_deck_sdl2.py
bash build.sh --deck
bash package.sh --deck
python3 -B tools/verify_legacy_mono.py --assembly build/deck/ControllerSupport.dll --require-embedded
python3 -B tools/check_deck_package.py
```

Native preparation also requires Python 3.12+, CMake, Ninja and `readelf` on an
x86_64 Linux build host; the Deck only needs the resulting ZIP. The preparation
tool downloads checksum-pinned upstream inputs and builds only
the controller-focused SDL2 dependency under ignored `build/`. Its license and
portable provenance accompany `build/ControllerSupport-Deck.zip`. The ordinary
build/package commands remain unchanged and do not enable SDL2 by default.
The package checker validates the actual ZIP, runs its DLL in standalone game
Mono, and checks the extracted native bytes, ABI and redistribution metadata.

For manual testing, close the game and preserve a fresh copy of the current
target save and any installed package before replacing the ZIP in
`<Roguelands>/GadgetCore/Mods/`. Use a disposable or freshly backed-up test profile.
Check GadgetCore discovery and errors, then exercise the intended behavior.
Keep built, installed and physically tested results separate. Do not restore an
older save merely to install or update the mod.

Controller Support builds independently of SixBySixChest. The companion's sixth
storage row uses an optional runtime bridge; controller-only storage and paired
persistence still have separate acceptance checks.

## Tests

The baseline suite contains 53 regression projects covering input ownership,
bindings, menus, inventory lifecycles, aiming and native IL. From this root:

```bash
bash build.sh
python3 tools/run_tests.py
```

The runner limits fixture concurrency to two and disables shared compilation.
It writes logs and `summary.json` under ignored `build/test-results/`; failures
and missing-prerequisite skips produce a nonzero exit. Use `--list` to inspect
fixture requirements, `--filter TEXT` to select fixtures, or `--output-dir PATH`
to choose a results directory.
It writes logs and a JSON summary under ignored `build/test-results/`. Use
`--list` to inspect prerequisites or `--filter <text>` to select fixture paths.
Missing prerequisites are reported as skips and produce a nonzero exit code;
a partial run is not a full-suite pass. To run one fixture directly:

```bash
dotnet run --project tests/BindingConfigTests.csproj -p:UseSharedCompilation=false
```

Some research fixtures additionally require locally generated game/GadgetCore
source. Point them at your own reference directory:

```bash
export ROGUELANDS_REFERENCE_DIR="/path/to/GameReference/Generated"
```

That directory contains `Assembly-CSharp/` and `GadgetCore/`. The original optional
companion workspace location remains the default. The Gear Forge fixture and
native IL audit require Python 3 and generate their native test input under an
ignored `obj/` directory. See the [Gear fixture instructions](tests/gear_forge_context_repro/README.md).
Normal mod builds require no decompilation. Offline checks do not establish
Unity rendering, real-device behavior, item persistence or multiplayer correctness.

## Diagnostics

Verbose controller diagnostics are opt-in. Set
`ROGUELANDS_CONTROLLER_DIAGNOSTICS=1` in the environment used to launch the game
to enable verbose input-probe and aim-assist diagnostics. Omit it for ordinary play. Diagnostics do not
change the binding file. Check GadgetCore's logs for loader and device errors;
Linux permissions must allow reading `/dev/input/js0`. Diagnostic logs stay local.

## Development and license

See [architecture](docs/ARCHITECTURE.md), [development instructions](AGENTS.md),
[status](STATUS.md) and [changes](CHANGELOG.md).

Before publishing, check candidate files, the index and history reachable from
the current branch for excluded material. The audit reports paths and categories
without printing secret values; it is a targeted check, not an exhaustive secret
scanner. Run the Python tooling tests separately from the .NET fixtures:

```bash
python3 -B tools/check_public_files.py --history
python3 -B -m unittest discover -s tools -p 'test_*.py'
```

Source: [TheArkratos/RoguelandsControllerSupport](https://github.com/TheArkratos/RoguelandsControllerSupport).
Original mod material is under the [MIT license](LICENSE). Roguelands, Unity,
GadgetCore and Harmony remain under their respective owners' terms. Private
profiles, saves, local research evidence and generated references are excluded
from this source repository.
