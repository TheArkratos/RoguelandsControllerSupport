public class ButtonMenu : UnityEngine.Object { }
public partial class GameScript
{
    public UnityEngine.GameObject menuGearMods;
    public Item[] modSlot = { new Item() };
    public int SelectModCalls, PlaceModCalls, SwapModCalls, AddModCalls;
    public bool CanAddMod(int a)
    { for(int i=0;i<3;i++) if(i!=a && modSlot[0].aspect[i]==holdingItem.id) return false; return true; }
    public void SelectItemMod(int slot) { SelectModCalls++;holdingItem=modSlot[slot];modSlot[slot]=new Item(); }
    public void PlaceItemMod(int slot) { PlaceModCalls++;modSlot[slot]=holdingItem;holdingItem=new Item(); }
    public void SwapItemMod(int slot) { SwapModCalls++;var previous=modSlot[slot];modSlot[slot]=holdingItem;holdingItem=previous; }
    // Native mutation replay; built-DLL/native IL checks independently verify
    // the actual click branches and source patch declarations.
    public void AddMod(int a)
    {
        AddModCalls++;
        if(holdingItem.id>200&&holdingItem.id<230&&modSlot[0].id!=0&&modSlot[0].aspectLvl[a]<5&&CanAddMod(a)&&
            (modSlot[0].aspect[a]==holdingItem.id||modSlot[0].aspect[a]==0||modSlot[0].aspectLvl[a]==0))
        { modSlot[0].aspectLvl[a]++;modSlot[0].aspect[a]=holdingItem.id;holdingItem.q--; }
    }
}
