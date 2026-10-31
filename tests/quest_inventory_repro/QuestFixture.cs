using UnityEngine;
public sealed class Quest
{
    public int type=1, numberOf=5, numberOf2, thingID=10, tier, rewardID=59, rewardQ=2;
}
public partial class GameScript
{
    public GameObject menuQuest;
    public Item[] inventoryQuest = new Item[3];
    public static Quest[] quest = new Quest[3];
    public int QuestClaims, QuestXp, ShuffleCalls, DeliveryCalls;
    public void SelectItemQuest(int slot)
    {
        QuestClaims++;QuestXp++;
        holdingItem=inventoryQuest[slot];inventoryQuest[slot]=new Item();
        quest[slot]=new Quest();
    }
    public void Shuffle()
    {
        if(Credits<1500)return; Credits-=1500;ShuffleCalls++;
        for(int i=0;i<3;i++){quest[i]=new Quest();inventoryQuest[i]=new Item();}
    }
    public void QuestClick(int slot)
    {
        var q=quest[slot];
        if(q.type==1&&holdingItem.id==q.thingID&&q.numberOf2<q.numberOf)
        {
            int count=System.Math.Min(holdingItem.q,q.numberOf-q.numberOf2);holdingItem.q-=count;q.numberOf2+=count;DeliveryCalls++;
            if(holdingItem.q==0)holdingItem=new Item();
            if(q.numberOf2==q.numberOf)inventoryQuest[slot]=new Item{id=q.rewardID,q=q.rewardQ,extraData="quest reward"};
        }
        else if((q.type==1||q.type==2)&&holdingItem.id==0&&q.numberOf2==q.numberOf)SelectItemQuest(slot);
    }
}
public class ButtonMenu : UnityEngine.Object { }
