public partial class GameScript
{
    public UnityEngine.GameObject menuCombat, skillBar;
    public int[] combatChips = new int[38];
    public bool holdingCombatChip, exitingcombatmode;
    public int ChipPickups, ChipPlaces, ChipSwaps;
    public void SelectCombatChip(int slot) { ChipPickups++; holdingItem=new Item{id=combatChips[slot]}; holdingCombatChip=true; combatChips[slot]=0; }
    public void PlaceCombatChip(int slot) { ChipPlaces++; combatChips[slot]=holdingItem.id; holdingItem=new Item(); holdingCombatChip=false; }
    public void SwapCombatChip(int slot) { ChipSwaps++; int old=combatChips[slot]; combatChips[slot]=holdingItem.id; holdingItem.id=old; }
}
