# SDL backend fixture

Run `dotnet run --project tests/sdl_backend_repro/SdlBackendRepro.csproj -p:UseSharedCompilation=false`.

The default fixture checks device selection, unmapped-device skipping, frame
edges, reconnects, disposal, initialization/poll failure and real native-loader
missing-library containment. It requires no installed SDL library or game.

An explicit `-- --native-smoke` additionally loads the local SDL2 library and
initializes the GameController subsystem in the **test process**, without a game
or video/event loop. On Linux it creates a process-local SDL virtual gamepad,
selects only that test device and exercises A, held-edge suppression, LT
normalization and hot-unplug through the real native adapter. This requires SDL2
with virtual-joystick support (2.0.14 or newer). Physical controllers are not read
by the virtual-device assertions. This optional smoke is not part of the default
runner and does not establish legacy Mono, Unity, Deck or Windows acceptance.

To exercise the bundled private library, first prepare it, then run from the mod
repository root:

```bash
dotnet run --project tests/sdl_backend_repro/SdlBackendRepro.csproj -p:UseSharedCompilation=false -p:DeckNativeDir="$PWD/build/deck-native/linux-x86_64" -- --native-smoke
```

This variant checks polling with input events disabled, an empty private event
queue, and independent host SDL initialization/event settings/subsystem lifetime
in both initialization orders. It embeds the native checksum explicitly as a
non-culture resource (`.so` must not be mistaken for the Somali culture suffix).
Use `tools/check_deck_package.py` separately to check the actual packaged DLL in
the game's embedded Mono without starting Unity.
