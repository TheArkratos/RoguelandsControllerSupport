using System.Collections;
using UnityEngine;
public partial class GameScript
{
    public GameObject menuGather;
    public static int[] shipDroid=new int[6];
    public Item[] gatherStorage=new Item[9];
    public int GatherTransferCalls, GatherTakeCalls, UpgradeCalls;
    public int GetDroidCost(int level) { return level==1?100:level==2?500:level==3?1000:level==4?5000:level==5?9999:0; }
    public IEnumerator ShiftClickGather(int slot)
    {
        GatherTransferCalls++;LastNativeSlot=slot;
        // The production router invokes this named native/loader coroutine.
        // Mutation implementation is modeled; native IL is checked separately.
        for(int i=0;i<36;i++)
            if(inventory[i].id==0) {inventory[i]=gatherStorage[slot];gatherStorage[slot]=new Item();break;}
        yield return null;
    }
    public void ReplayUpgrade(int slot)
    { if(shipDroid[slot]<5 && Credits>=GetDroidCost(shipDroid[slot]+1)){Credits-=GetDroidCost(shipDroid[slot]+1);shipDroid[slot]++;UpgradeCalls++;} }
    public void ReplayTake(int slot)
    { holdingItem=gatherStorage[slot];gatherStorage[slot]=new Item();GatherTakeCalls++; }
}
