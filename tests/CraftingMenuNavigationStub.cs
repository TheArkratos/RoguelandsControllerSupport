// These older fixtures exclude Alchemy. Dedicated Alchemy fixtures link the real context.
namespace ControllerSupport
{
    internal static class CraftingMenuContext
    {
        internal const int FirstSlot = 131, TargetCount = 21;
        internal const int CraftButton = 135, RecipeToggle = 136, RecipeBack = 137, PageUp = 138, PageDown = 139, FirstRecipe = 140;
        internal static bool IsOpen(GameScript game) { return false; }
        internal static int ModeIdentity(GameScript game) { return -1; }
        internal static bool IsGear(GameScript game) { return false; }
        internal static bool IsRecipeView(GameScript game) { return false; }
        internal static bool TryChangeRecipePage(GameScript game, int direction, out string reason) { reason = "Alchemy unavailable in this fixture"; return false; }
        internal static UnityEngine.GameObject Root(GameScript game) { return null; }
        internal static void CollectTargets(GameScript game, UnityEngine.Collider[] slots) { }
        internal static bool IsTarget(int slot) { return slot >= FirstSlot && slot < FirstSlot + TargetCount; }
        internal static bool IsItemSlot(int slot) { return slot >= FirstSlot && slot < FirstSlot + 4; }
        internal static int NativeSlot(int slot) { return slot - FirstSlot; }
    }
}
