# X badge border repro

Run `dotnet run --project Mods/ControllerSupport/tests/badge_border_repro/BadgeBorderRepro.csproj` from the repo root.

This harness drives production `ControllerInteractPrompt.Update(true)` through its mesh-renderer W-prompt path. The texture stub begins opaque white, deliberately denying an assumption that a new texture is transparent. It asserts every pixel outside the centered 32×32 badge on its 96×96 canvas is transparent. It is a source-level repro, not a screenshot or proof of the native engine's initial texture contents.
