# Held-item storage close regression

Run `dotnet run --project tests/storage_close_repro/StorageCloseRepro.csproj -p:UseSharedCompilation=false` from the repository root.

This fixture compiles production `GameplayInputPatches.cs`. Its lifecycle double models preparation owning controller pause/close: an unresolved held item keeps the UI open; a safe return permits close/pause exactly once. Assertions verify the patch prepares lifecycle before remaining world pause processing and does not independently double-close or drop an item. The native Cancel read must still use the controller adapter. Actual return movement is covered by the production-linked lifecycle fixture; Harmony behavior and save persistence require in-game validation.
