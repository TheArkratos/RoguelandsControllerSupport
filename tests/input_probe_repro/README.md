# InputProbe frame and ownership regression

Compiles the real probe, mapped backend, canonical input state and binding sources
against minimal authored Unity/GadgetCore stubs. A fake `ISdlControllerApi` enters
through the real `SdlControllerBackend`, never native SDL or Linux device reads.
The Unity input stub supplies in-memory input only (including joystick names).

Checks game-caller polling before the runner's `InputProbe.Update`, same-frame
snapshot/edge preservation, next-frame clearing, keyboard/mouse handoff, mapped
press retake, quiet initial held snapshots, diagnostics-off behavior and disposal.
No game assemblies, generated references, native libraries or devices are needed.
This does not establish physical controls or Unity runtime acceptance.

Run one fixture process:

```sh
dotnet run --project tests/input_probe_repro/InputProbeRepro.csproj -p:UseSharedCompilation=false
```
