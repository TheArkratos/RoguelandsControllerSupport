using System;
using ControllerSupport;
using UnityEngine;
internal static class Program
{
 static int checks; static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static int Main(){
  var g=new GameScript{menuCombat=new GameObject("chips"),inventoryBar=new GameObject("bar"),inventoryMain=new GameObject("inventory")};
  GameScript.inventoryOpen=true;GameScript.pausing=GameScript.dead=GameScript.buildMode=false;ControllerInputOwnership.Configure(()=>true);
  Check(CombatChipMenuContext.IsOpen(g),"native chip root opens independently of inventory root");
  Check(CombatChipMenuContext.Root(g)==g.menuCombat,"chip root identity");
  foreach(int id in new[]{152,157,158,189})Check(CombatChipMenuContext.IsTarget(id),"chip target range includes "+id);
  foreach(int id in new[]{-1,0,151,190})Check(!CombatChipMenuContext.IsTarget(id),"chip target range excludes "+id);
  string reason;Check(!CombatChipMenuContext.CanActivate(g,152,out reason),"empty chip slot cannot pick up");
  g.combatChips[0]=7;Check(CombatChipMenuContext.CanActivate(g,152,out reason),"native existing chip may pick up");
  Check(CombatChipMenuContext.ActionLabel(g,152)=="Pick up chip","native pickup label");
  g.holdingItem=new Item{id=8};g.holdingCombatChip=true;Check(CombatChipMenuContext.HasHeldItem(g)&&CombatChipMenuContext.IsHeldChip(g),"native zero quantity chip is held");
  Check(CombatChipMenuContext.ActionLabel(g,152)=="Swap chip","occupied chip target labels swap");
  Check(CombatChipMenuContext.ActionLabel(g,153)=="Place chip","empty chip target labels place");
  g.holdingItem.id=7;Check(!CombatChipMenuContext.CanActivate(g,152,out reason),"native same-chip-ID slot is no-op");
  g.holdingCombatChip=false;Check(!CombatChipMenuContext.HasHeldItem(g),"ordinary zero quantity item is not held");
  g.holdingItem.q=2;Check(CombatChipMenuContext.HasHeldItem(g)&&!CombatChipMenuContext.IsHeldChip(g),"ordinary held item remains distinct");
  Check(!CombatChipMenuContext.CanActivate(g,152,out reason),"ordinary held item cannot become chip");
  g.holdingItem=new Item();g.exitingcombatmode=true;Check(!CombatChipMenuContext.CanActivate(g,152,out reason),"native exit cooldown prevents pickup");g.exitingcombatmode=false;
  ControllerInputOwnership.Configure(()=>false);Check(!CombatChipMenuContext.CanActivate(g,152,out reason),"mouse ownership cannot invoke controller action");ControllerInputOwnership.Configure(()=>true);
  GameScript.pausing=true;Check(!CombatChipMenuContext.IsOpen(g),"paused chip menu unavailable");GameScript.pausing=false;
  GameScript.dead=true;Check(!CombatChipMenuContext.IsOpen(g),"dead chip menu unavailable");GameScript.dead=false;
  GameScript.buildMode=true;Check(!CombatChipMenuContext.IsOpen(g),"build mode excludes chip menu");GameScript.buildMode=false;
  g.menuCombat.SetActive(false);Check(!CombatChipMenuContext.IsOpen(g),"hidden chip root unavailable");
  Check(CombatChipMenuContext.ReadChip(g,151)==0&&CombatChipMenuContext.ReadChip(g,190)==0,"out-of-range reads fail safely");
  Console.WriteLine("Combat Chip context: "+checks+" checks passed.");return 0;
 }
}
