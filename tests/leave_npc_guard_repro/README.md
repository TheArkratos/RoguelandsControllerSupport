# LeaveNPC held-storage resolution repro

Run `dotnet run --project Mods/ControllerSupport/tests/leave_npc_guard_repro/LeaveNpcGuardRepro.csproj` from the repository root.

This compiles production `GameplayInputPatches.cs` and models Harmony's prefix decision with an explicit lifecycle double. Safe return or a chest-position fallback resolves the held item before native closure; neither reaches native cursor-position dropping. Failed resolution preserves the item and reports rejection. Empty-hand and non-storage paths retain native behavior. Actual item return/drop placement is tested by the production-linked lifecycle fixture; Harmony patch ordering needs in-game validation.
