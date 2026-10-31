# Interaction prompt repro

Run `dotnet run --project tests/interact_prompt_repro/InteractPromptRepro.csproj -p:UseSharedCompilation=false`
from the repository root. The test compiles production prompt code with
minimal Unity stubs and checks binding labels, artwork replacement/restoration
for sprite and mesh renderers, and preservation of the native active state.
It does not prove the installed game's material/UV scale looks right.
