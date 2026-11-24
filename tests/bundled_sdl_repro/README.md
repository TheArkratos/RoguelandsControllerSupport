# Bundled SDL2 extraction

Runs the production extraction module against authored bytes, without SDL or game
assemblies. Covers checksum/path rejection, byte preservation, content-addressed
reuse and refusing to overwrite an unexpected cached file. Covers automatic
32-/64-bit Linux resource selection and rejecting non-Linux/unknown targets.

```bash
dotnet run --project tests/bundled_sdl_repro/BundledSdlRepro.csproj -p:UseSharedCompilation=false
```

Actual native loading is a separate explicit SDL virtual-gamepad/legacy Mono smoke.
