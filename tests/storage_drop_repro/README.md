# Storage drop regression replay

Run `dotnet run --project tests/storage_drop_repro/StorageDropRepro.csproj -p:UseSharedCompilation=false` from the repository root. The fixture compiles production `ControllerMenuSelection`, `InventorySelectionNavigator`, and `ControllerFrontEndMenus`, then feeds their mouse wrappers into the source-backed `GameScript.Update` inventory-click dispatch.

It verifies that a controller-only A pickup and subsequent A placement use the selected inventory slot even when the physical cursor remains over a layer-17 world drop target. A physical mouse click takes device ownership and retains native behavior, including when pressed simultaneously with A. External controller-to-mouse mapping therefore needs separate physical validation. The simulated branch verifies routing rather than native item mutations or save persistence.
