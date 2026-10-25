namespace ControllerSupport
{
    internal static class CombatChipMenuContext
    {
        internal static bool IsOpen(GameScript game) { return game!=null && GameScript.inventoryOpen && !GameScript.pausing && game.menuCombat.activeInHierarchy; }
        internal static bool HasHeldItem(GameScript game)
        {
            var field=typeof(GameScript).GetField("holdingItem",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            var held=game!=null && field!=null ? field.GetValue(game) as Item:null;
            return held!=null && held.id>0 && (held.q>0 || IsHeldChip(game));
        }
        internal static bool IsHeldChip(GameScript game) { return game!=null && game.holdingCombatChip; }
    }
}
