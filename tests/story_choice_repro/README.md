# Story choice controller reproduction

Runs production ControllerFrontEndMenus, configurable bindings/raw input and native ButtonMenu hover code against Unity doubles. Does not launch the game or touch profiles.

```bash
dotnet run --project Mods/ControllerSupport/tests/story_choice_repro/StoryChoiceRepro.csproj -p:UseSharedCompilation=false
```

Covers first visible choice despite scene/pointer order, native highlight restoration, both sticks and D-pad, virtual raycast/click validation, release guards, remapped/unbound activation/cancel, hidden/absent options, Back via the native close seam, contradictory station roots, death/pause/root replacement/disconnection, controller/mouse ownership and modal cleanup. Related gameplay_actions, dialogue_input, aim_assist_integration and exploration fixtures test live modal suppression and same-frame state changes in their production adapters.

This model does not execute a real story branch, real GadgetCore callbacks, save persistence or the game's rendering/physics. Native IL inspection and physical branch/cancel/reopen testing remain separate.
