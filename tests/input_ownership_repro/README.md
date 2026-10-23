# Input ownership regression fixture

Run from the repository root:

```bash
dotnet run --project Mods/ControllerSupport/tests/input_ownership_repro/InputOwnershipRepro.csproj -p:UseSharedCompilation=false
```

This fixture compiles the real shared policy, last-input arbitration, and Linux
joystick frame-state classes. It verifies that controller effects default off,
track ownership immediately, ignore held controls and input noise after a mouse
handoff, favor keyboard/mouse on simultaneous activity, and require fresh intent
to switch back. Reset/reconfiguration checks cover stale delegates on unload.

`tests/Directory.Build.targets` links the policy into all fixture projects. Legacy
fixtures primarily exercise positive controller behavior and use the explicitly
test-only `ControllerOwnershipTestBootstrap.cs` initializer to grant ownership.
That initializer is never linked by the production build. This fixture opts out
with `ControllerOwnershipTestBootstrap=false` so its first assertion also checks
the production default. Other fixture handoff tests override `Configure` or call
`Reset` explicitly to exercise the actual gated feature code.

This does not construct Unity's Gadget/InputProbe or poll a physical device;
the fixture uses the same live `() => source.UsingController` delegation and
feeds the actual raw-event classifier. Feature fixtures cover native fallbacks,
visual cleanup, and aim behavior when the shared policy changes.
