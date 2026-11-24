# Steam Deck native Linux test candidate

This is an **experimental test build**, not a general release. The user reports
basic controls working in Steam Desktop Mode with the native 32-bit game after
correcting the active controller layout; full acceptance remains pending.
Use the native Linux game with GadgetCore already installed. This ZIP includes
both **32-bit (x86) and 64-bit (x86_64)** SDL2 libraries and selects the matching
library automatically. It does **not** support Windows/Proton; do not enable
Proton for this test. No separate SDL install, executable change or backend
launch variable is necessary. Leave Steam Launch Options empty for this mod.

## Install safely in Desktop Mode

1. Quit Roguelands. In Steam → Roguelands → Properties → Compatibility, leave
   **Force the use of a specific Steam Play compatibility tool** unchecked.
   In Controller settings enable Steam Input and select the **Gamepad** template
   (not a keyboard/mouse community layout). Remove old controller-related launch
   overrides for this test; keep any unrelated required GadgetCore setup.
    Disconnect other gamepads for this first test: the backend keeps the first
    mapped gamepad it opens, rather than combining controllers.
    **Check the active Controller Layout even if Steam Input says enabled.** In
    the reported Deck test an unexpected layout coincided with SDL seeing zero
    joysticks; correcting the layout made controls work. Exact layout names were
    not recorded. A gamepad-emitting template is needed, not keyboard/mouse-only
    bindings. Desktop Mode worked; switching to Gaming Mode was not required.
2. Back up the **entire current native save directory** before the first launch:
   `~/.config/unity3d/DefaultCompany/Roguelands/`. Keep it outside the game/mod
   directories. Back up the currently installed Controller Support package and
   bindings file too. Do not substitute an older save or restore one to install.
   If you cannot locate the current save, stop before launching a valued profile.
3. Steam → Manage → Browse local files locates `<Roguelands>`. Verify GadgetCore
   exists there. Preserve the old Controller Support ZIP outside
   `GadgetCore/Mods/`, then copy `ControllerSupport-Deck.zip` there **as
   `ControllerSupport.zip`**. Keep exactly one Controller Support package or
   directory installed; do not leave the old ZIP alongside the new one.
4. Verify the transferred ZIP matches the supplied SHA-256. In Konsole:
   `sha256sum /path/to/ControllerSupport-Deck.zip`.
   Leave other mods and existing bindings unchanged. Start through Steam so its
   controller configuration applies, using a disposable or freshly backed-up
   profile. Do not first test using valuable inventory.

The ZIP is the mod; do not unpack its native library into `GadgetCore/Libs`.
GadgetCore reads the DLL from the ZIP. The mod verifies and extracts its embedded
SDL2 under `GadgetCore/Temp/ControllerSupport/` on demand. GadgetCore clears that
temporary directory at startup. The SDL license and provenance are in `SDL2/`
inside the ZIP, alongside this guide.

## First test: title screen, before loading a character

- Release all controls after startup. Move the left stick or D-pad, then press A.
  Check native title/menu navigation. Initial held controls deliberately do not
  count as new presses. B should cancel where supported.
- If nothing responds, **do not keep loading/saving characters to investigate**.
  Copy `GadgetCore/Logs/Controller Support.log` before the next launch for `Probe`
  entries and errors. Also include `GadgetCore.log` for loader/discovery failures.
- Expected startup messages include `experimental SDL2 backend selected`,
  `process=32-bit; bundled=True` or `process=64-bit; bundled=True`,
  `SDL2 mapped controller backend initialized`
  and `SDL2 mapped gamepad opened`. No mapped pad is a distinct logged result;
  it is not proof the mod failed to load. Either process bitness is expected.
  For a native-load error, stop and report it; do not install a random library.
- After a fresh stick/button action, `SDL2 mapped input activity observed` confirms
  state reached the mapped producer. It does not by itself prove menu dispatch.

Launch normally through Steam. If you previously added the experimental 64-bit
executable override, remove it: changing the executable is no longer required.

## Narrow gameplay acceptance (only after title navigation works)

- Left stick moves; right stick aims; LT jumps; RT attacks; LB dashes; Y switches
  combat mode; X interacts; View opens inventory; Menu pauses. Existing custom
  bindings take precedence over these defaults.
- A activates a menu item without causing a delayed jump on closing; RB must not
  trigger the game's legacy dash alias. Release controls between screen changes.
- Check separate triggers and D-pad directions, then keyboard/mouse or trackpad
  handoff and controller retake. Keep gyro disabled for this first gamepad-only
  test; validate mixed gyro/mouse use separately afterward.
- In a disposable inventory, pick up/cancel one item and verify it returns safely.
  Stop on duplication, loss or unexpected action; preserve current save/logs
  before another launch. Full station, persistence and multiplayer acceptance
  are **not** established by this short test.

## Failure and rollback

Quit; preserve the failed-run log and current save first. Replace only the mod ZIP
with the backed-up prior package and remove this test package if installed under
a different filename. Remove only test-specific Steam launch changes. Do not
restore a save automatically. Report SteamOS/game runtime (native Linux), selected
Steam Input template, startup Probe messages and the first failing action. Redact
personal paths/account details before sharing logs publicly.

Windows/Proton remains a separate build/verification task: it needs Windows SDL2,
not this Linux `.so`, even though Proton runs on a Linux host.
