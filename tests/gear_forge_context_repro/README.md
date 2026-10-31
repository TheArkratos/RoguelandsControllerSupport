# Gear Forge context fixture

This research fixture checks the controller context against your local native
recipe-unlock method. Requires Python 3, .NET 10 and locally generated references.
The public repository contains the generator and test scaffolding; native game
source stays local.

From the Controller Support root:

```bash
export ROGUELANDS_REFERENCE_DIR="/path/to/GameReference/Generated"
dotnet run --project tests/gear_forge_context_repro/GearForgeContextRepro.csproj -p:UseSharedCompilation=false
```

The original companion workspace reference location remains the default when
the environment variable is unset. MSBuild runs `generate_native_fixture.py`
before compiling. It reads `Assembly-CSharp/GameScript.cs` and writes only
ignored `obj/NativeGearRecipe.g.cs`, adding call recording to the extracted method.
Missing references or changed method syntax stop generation with an error.

The native IL audit also invokes this generator, so it runs independently of
the Gear fixture and verifies the generated method against the local reference.
Both fixtures may run concurrently, with at most two dotnet fixtures at once.
An older local `NativeGearRecipe.cs` may remain as preserved evidence; it is
excluded from compilation and version control. Never publish generated inputs.
