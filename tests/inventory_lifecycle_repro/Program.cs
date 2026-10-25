using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using ControllerSupport;
using GadgetCore.API;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string text) { checks++; if (!value) throw new Exception(text); }
    private static GameScript Fresh()
    {
        InventoryItemLifecycle.Reset();
        InstallCapture(true);
        Network.Live.Clear(); Network.FailDestroy = false;
        GadgetCoreAPI.LastIsChip = false; GadgetCoreAPI.Drops = 0; GadgetCoreAPI.FailSpawn = GadgetCoreAPI.ThrowSpawn = false;
        MenuScript.player = new GameObject(); MenuScript.player.transform.position = new Vector3(5, 8, 0);
        PlayerScript.curInteractObj = new GameObject(); PlayerScript.curInteractObj.transform.position = new Vector3(40, 20, 0);
        GameScript game = new GameScript(); InventoryItemLifecycle.ObserveContext(game); return game;
    }
    // Drive the actual production prefix/postfix against native-method test doubles.
    private static void Action(GameScript game, string name, int slot)
    {
        MethodInfo prefix = typeof(InventorySourcePatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo postfix = typeof(InventorySourcePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
        object[] args = { game, null }; prefix.Invoke(null, args);
        MethodInfo method = typeof(GameScript).GetMethod(name); method.Invoke(game, new object[] { slot });
        postfix.Invoke(null, new object[] { game, slot, method, args[1] });
    }
    private static List<HarmonyLib.CodeInstruction> InstallCapture(bool correct)
    {
        MethodInfo instantiate = typeof(Network).GetMethod("Instantiate");
        var input = new List<HarmonyLib.CodeInstruction> {
            new HarmonyLib.CodeInstruction(OpCodes.Call, instantiate),
            new HarmonyLib.CodeInstruction(OpCodes.Ret)
        };
        input[0].labels.Add("branch"); input[0].blocks.Add("exception");
        if (correct) input.Insert(1, new HarmonyLib.CodeInstruction(OpCodes.Call, instantiate));
        var method = typeof(InventoryDropCapturePatch).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static);
        var output = ((IEnumerable<HarmonyLib.CodeInstruction>)method.Invoke(null, new object[] { input })).ToList();
        Check(output[0] != input[0] && output[0].labels.Contains("branch") && output[0].blocks.Contains("exception"),
            "transpiler clones instructions preserving labels/exception blocks");
        Check(output.Count == input.Count + (correct ? 2 : 0), "capture injected after exactly two calls or rejected");
        if (correct)
        {
            Check(output[1].opcode == OpCodes.Call && ((MethodInfo)output[1].operand).Name == "TrackSpawn"
                && ((MethodInfo)output[3].operand).Name == "TrackSpawn", "both branches capture immediately after spawn");
        }
        return output;
    }

    private static bool Guard(GameScript game)
    {
        return (bool)typeof(InventoryMutationGuardPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { game });
    }

    private static void OpenForge(GameScript game, Vector3 position)
    {
        game.EmblemForge();
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = position;
        typeof(InventoryEmblemContextPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { game });
    }

    private static void OpenPrism(GameScript game, Vector3 position)
    {
        game.Prism();
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = position;
        typeof(InventoryPrismContextPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { game });
    }

    private static void GearForgeLifecycle()
    {
        string reason;
        GameScript game = Fresh(); game.GearForge();
        game.craft[0] = new Item(101, 5) { extra = "Gear Forge emblem metadata" };
        Action(game, "SelectItemCraft", 0);
        Check(InventoryItemLifecycle.TryReturn(game, out reason) && game.craft[0].q == 5
            && game.craft[0].extra == "Gear Forge emblem metadata" && game.inventory[0].id == 0,
            "Gear Forge emblem returns to original crafting input, not inventory hotkey zero");
        Action(game, "SplitItemCraft", 0);
        Check(InventoryItemLifecycle.TryReturn(game, out reason) && game.craft[0].q == 5,
            "Gear Forge split emblem rejoins its exact compatible source stack");
        Item output = new Item(300, 1) { extra = "generated gear metadata" };
        game.craft[3] = output; Action(game, "SelectItemCraft", 3);
        Check(InventoryItemLifecycle.TryReturn(game, out reason) && ReferenceEquals(game.craft[3], output)
            && game.craft[3].extra == "generated gear metadata" && game.inventory[3].id == 0,
            "Gear Forge safe return preserves exact native output instance and metadata");
        Action(game, "SelectItemCraft", 0); game.Alchemy();
        Item held = game.holdingItem;
        Check(!InventoryItemLifecycle.TryReturn(game, out reason) && ReferenceEquals(game.holdingItem, held)
            && game.craft[0].id == 0, "Same-root station change cannot return Gear emblems into an Alchemy context");
        game = Fresh(); game.Alchemy(); game.craft[1] = new Item(11, 2);
        Action(game, "SelectItemCraft", 1); game.GearForge();
        Check(!InventoryItemLifecycle.TryReturn(game, out reason) && game.holdingItem.id == 11
            && game.craft[1].id == 0, "Alchemy source cannot be reinterpreted after changing the same pane to Gear Forge");
        game = Fresh(); game.GearForge();
        PlayerScript.curInteractObj.transform.position = new Vector3(300, 120, 0);
        Type opener = typeof(InventoryItemLifecycle).Assembly.GetType("ControllerSupport.InventoryGearForgeContextPatch");
        Check(opener != null && opener.IsPublic, "Gear Forge opener patch is exported for GadgetCore");
        opener.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { game });
        game.menuCraft.activeSelf = false; game.menuRecipe.activeSelf = true;
        game.holdingItem = new Item(301, 1) { extra = "unknown generated gear" }; PlayerScript.curInteractObj = null;
        Check(InventoryItemLifecycle.LeaveRange(game, out reason) && GadgetCoreAPI.LastPosition.x == 300
            && GadgetCoreAPI.LastPosition.y == 121.5f && GadgetCoreAPI.LastDrop.extra == "unknown generated gear",
            "Gear Forge recipe pane keeps original station position for held-output range fallback");
        game = Fresh(); game.Alchemy(); InventoryItemLifecycle.ObserveContext(game);
        game.GearForge(); PlayerScript.curInteractObj.transform.position = new Vector3(310, 125, 0);
        opener.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { game });
        game.holdingItem = new Item(302, 1); PlayerScript.curInteractObj = null;
        Check(InventoryItemLifecycle.LeaveRange(game, out reason) && GadgetCoreAPI.LastPosition.x == 310
            && GadgetCoreAPI.LastPosition.y == 126.5f, "Same pane changing station type refreshes its opener position");
        game = Fresh(); game.GearForge(); game.holdingItem = new Item(303, 1);
        GadgetCoreAPI.ThrowSpawn = true; Network.FailDestroy = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(game, out reason) && !Guard(game),
            "Gear Forge preserves unresolved drop quarantine");
    }

    private static void AlchemyLifecycle()
    {
        string reason;
        GameScript game = Fresh(); game.Alchemy();
        game.craft[1] = new Item(11, 5) { extra = "alchemy ingredient metadata" };
        Action(game, "SelectItemCraft", 1);
        Check(InventoryItemLifecycle.TryReturn(game, out reason) && game.craft[1].q == 5
            && game.craft[1].extra == "alchemy ingredient metadata" && game.inventory[1].id == 0,
            "Alchemy ingredient returns to exact craft slot, never a hotkey inventory slot");
        Action(game, "SplitItemCraft", 1);
        Check(InventoryItemLifecycle.TryReturn(game, out reason) && game.craft[1].q == 5,
            "Alchemy split returns whole held portion to compatible original craft stack");
        game.craft[3] = new Item(53, 2) { extra = "native output" };
        Action(game, "SelectItemCraft", 3);
        Check(InventoryItemLifecycle.TryReturn(game, out reason) && game.craft[3].q == 2
            && game.craft[3].extra == "native output" && game.inventory[3].id == 0,
            "Alchemy output returns to exact output slot without ingredient eligibility validation");
        Action(game, "SelectItemCraft", 1); game.craft[1] = new Item(12, 1);
        Item held = game.holdingItem;
        Check(!InventoryItemLifecycle.TryReturn(game, out reason) && ReferenceEquals(held, game.holdingItem)
            && game.craft[1].id == 12 && game.craft[1].q == 1,
            "Alchemy return rejects incompatible occupied source without touching items");
        game = Fresh(); game.Alchemy(); game.craft[0] = new Item(11, 2);
        Action(game, "SelectItemCraft", 0); game.menuCraft.activeSelf = false;
        Check(!InventoryItemLifecycle.TryReturn(game, out reason) && game.holdingItem.q == 2
            && game.inventory[0].id == 0, "Closed Alchemy context cannot redirect a craft source into inventory");
        game = Fresh(); game.Alchemy();
        PlayerScript.curInteractObj.transform.position = new Vector3(250, 110, 0);
        Type opener = typeof(InventoryItemLifecycle).Assembly.GetType("ControllerSupport.InventoryAlchemyContextPatch");
        Check(opener != null && opener.IsPublic, "Alchemy opener patch is exported for GadgetCore");
        opener.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { game });
        game.holdingItem = new Item(53, 1);
        game.menuCraft.activeSelf = false; game.menuRecipe.activeSelf = true;
        PlayerScript.curInteractObj = null;
        Check(InventoryItemLifecycle.LeaveRange(game, out reason) && GadgetCoreAPI.LastPosition.x == 250
            && GadgetCoreAPI.LastPosition.y == 111.5f,
            "Alchemy recipe transition retains remembered opener for unknown held-output range drop");
        MethodInfo targets = typeof(InventoryMutationGuardPatch).GetMethod("TargetMethods", BindingFlags.Static | BindingFlags.NonPublic);
        var names = ((IEnumerable<MethodBase>)targets.Invoke(null, null)).Select(method => method.Name).ToArray();
        Check(names.Contains("Craft") && names.Contains("DropCraftItems"), "Drop quarantine covers crafting progress and staged ingredient drops");
        game = Fresh(); game.Alchemy(); game.holdingItem = new Item(53, 1);
        GadgetCoreAPI.ThrowSpawn = true; Network.FailDestroy = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(game, out reason) && !Guard(game),
            "Alchemy inherits unresolved drop quarantine without resetting transaction protection");
    }

    private static void ChipAction(GameScript game, string name, int slot)
    {
        Type patch=typeof(InventoryItemLifecycle).Assembly.GetType("ControllerSupport.InventoryChipSourcePatch");
        MethodInfo prefix=patch.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
        MethodInfo postfix=patch.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
        object[] args={game,null};prefix.Invoke(null,args);
        game.slotID=slot; typeof(GameScript).GetMethod(name).Invoke(game,new object[]{slot});
        postfix.Invoke(null,new object[]{game,slot,args[1]});
    }
    private static void ChipLifecycle()
    {
        string reason; var g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);
        g.holdingItem=new Item(17,0);g.holdingCombatChip=true;
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&g.holdingItem.id==17,
            "q0 chip with no source rejects return instead of silently claiming empty hand");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[7]=17;
        ChipAction(g,"SelectCombatChip",7);
        Check(g.holdingItem.id==17&&g.holdingItem.q==0&&g.holdingCombatChip&&g.combatChips[7]==0,
            "native chip pickup preserves q0 special held type");
        InventoryItemLifecycle.ClearIfEmpty(g);InventoryItemLifecycle.ObserveContext(g);
        Check(InventoryItemLifecycle.TryReturn(g,out reason)&&g.combatChips[7]==17&&g.holdingItem.id==0&&!g.holdingCombatChip,
            "q0 chip returns to exact original chip slot through native placement");
        Check(g.ChipEquipCalls==1&&g.ChipDequipCalls==1&&g.inventory[7].id==0,
            "chip return keeps native equip hooks and never reinterprets source as inventory");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[7]=17;g.combatChips[12]=18;
        ChipAction(g,"SelectCombatChip",7);Item same=g.holdingItem;ChipAction(g,"SwapCombatChip",12);
        Check(ReferenceEquals(same,g.holdingItem)&&g.holdingItem.id==18&&g.combatChips[12]==17,
            "native chip swap changes held ID on the same Item object");
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&g.holdingItem.id==18&&g.combatChips[12]==17,
            "occupied swap source does not undo completed swap");
        g.combatChips[12]=0;
        Check(InventoryItemLifecycle.TryReturn(g,out reason)&&g.combatChips[12]==18&&g.combatChips[7]==0,
            "same-object chip swap records latest source slot");
        g=Fresh();g.inventory[5]=new Item(17,1);Action(g,"SelectItem",5);g.CombatChipMenu();
        g.holdingItem=new Item(17,0);g.holdingCombatChip=true;
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&g.inventory[5].id==0,
            "chip without native pickup provenance cannot use stale ordinary inventory source");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[4]=22;
        ChipAction(g,"SelectCombatChip",4);g.menuCombat.activeSelf=false;
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&g.holdingItem.id==22&&g.combatChips[4]==0,
            "closed chip menu cannot receive return through stale source");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[4]=22;
        ChipAction(g,"SelectCombatChip",4);g.holdingCombatChip=false;g.holdingItem.q=1;
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&g.inventory[4].id==0,
            "ordinary item cannot reinterpret an old chip source");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.holdingItem=new Item(19,0);g.holdingCombatChip=true;
        Check(InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&GadgetCoreAPI.LastIsChip&&GadgetCoreAPI.LastDrop.id==19&&GadgetCoreAPI.LastDrop.q==0,
            "above-head q0 chip drop uses special native chip branch without inventing quantity");
        Check(GadgetCoreAPI.LastPosition.x==5&&GadgetCoreAPI.LastPosition.y==11&&g.holdingItem.id==0&&!g.holdingCombatChip,
            "successful chip drop clears both native held fields only after confirmed spawn");
        Check(InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&GadgetCoreAPI.Drops==1,
            "repeated chip drop cannot duplicate a pickup");
        g=Fresh();PlayerScript.curInteractObj.transform.position=new Vector3(71,29,0);g.CombatChipMenu();
        var opener=typeof(InventoryItemLifecycle).Assembly.GetType("ControllerSupport.InventoryCombatChipContextPatch");
        opener.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g});
        g.holdingItem=new Item(20,0);g.holdingCombatChip=true;PlayerScript.curInteractObj.transform.position=new Vector3(600,600,0);
        Check(InventoryItemLifecycle.LeaveRange(g,out reason)&&GadgetCoreAPI.LastIsChip&&GadgetCoreAPI.LastPosition.x==71&&GadgetCoreAPI.LastPosition.y==30.5f,
            "unknown q0 chip forced exit uses captured station rather than current interaction target");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.holdingItem=new Item(21,0);g.holdingCombatChip=true;GadgetCoreAPI.ThrowSpawn=true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&Network.Live.Count==0&&g.holdingCombatChip&&g.holdingItem.id==21&&!InventoryItemLifecycle.MutationsBlocked(g),
            "failed chip RPC rolls back real pickup and retains q0 chip plus flag");
        GadgetCoreAPI.ThrowSpawn=false;
        Check(InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&GadgetCoreAPI.LastIsChip&&Network.Live.Count==1,
            "rolled-back chip drop retries as a chip exactly once");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.holdingItem=new Item(21,0);g.holdingCombatChip=true;GadgetCoreAPI.ThrowSpawn=true;Network.FailDestroy=true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&!Guard(g)&&g.holdingCombatChip&&g.holdingItem.id==21,
            "uncertain chip rollback quarantines native actions and retains special held type");
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&!InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&GadgetCoreAPI.Drops==1,
            "quarantined q0 chip cannot be returned or spawned again");
        var targets=((IEnumerable<MethodBase>)typeof(InventoryMutationGuardPatch).GetMethod("TargetMethods",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null)).Select(x=>x.Name).ToList();
        foreach(string name in new[]{"SelectCombatChip","SwapCombatChip","PlaceCombatChip"})
            Check(targets.Contains(name),"native quarantine covers "+name);
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[3]=23;
        ChipAction(g,"SelectCombatChip",3);g.ThrowRefresh=true;
        Check(InventoryItemLifecycle.TryReturn(g,out reason)&&g.combatChips[3]==23&&g.holdingItem.id==0&&!g.holdingCombatChip,
            "cosmetic exception after committed chip placement cannot refund or duplicate chip");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[3]=23;
        ChipAction(g,"SelectCombatChip",3);g.RejectPlace=true;
        Check(!InventoryItemLifecycle.TryReturn(g,out reason)&&g.combatChips[3]==0&&g.holdingItem.id==23&&g.holdingCombatChip,
            "rejected native chip return preserves q0 held chip and source");
        g.RejectPlace=false;Check(InventoryItemLifecycle.TryReturn(g,out reason)&&g.combatChips[3]==23,
            "rejected chip return can retry through native placement without a copy");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.holdingItem=new Item(24,0);g.holdingCombatChip=true;GadgetCoreAPI.FailSpawn=true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g,out reason)&&g.holdingItem.id==24&&g.holdingItem.q==0&&g.holdingCombatChip&&Network.Live.Count==0,
            "failed chip spawn preserves exact held native type");
        g=Fresh();g.CombatChipMenu();InventoryItemLifecycle.ObserveContext(g);g.combatChips[1]=5;g.exitingcombatmode=true;
        ChipAction(g,"SelectCombatChip",1);
        Check(g.combatChips[1]==5&&g.holdingItem==null&&InventoryItemLifecycle.TryReturn(g,out reason),
            "native exiting-combat rejection does not invent chip provenance");
    }

    public static void Main()
    {
        ChipLifecycle();
        GearForgeLifecycle();
        AlchemyLifecycle();
        string reason;
        GameScript g = Fresh();
        OpenPrism(g, new Vector3(210, 85, 0));
        g.inventory[6] = new Item(86, 25);
        g.PrismItem(6); g.holdingItem.extra = "generated prism";
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && reason.Contains("source")
            && g.inventory[6].id == 86 && g.inventory[6].q == 5 && g.holdingItem.id == 89 && g.holdingItem.q == 2,
            "Prism generated output has no invented source and keeps native consumed input unchanged on Cancel");
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = new Vector3(700, 900, 0);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.LastPosition.x == 210
            && GadgetCoreAPI.LastPosition.y == 86.5f, "Prism context remembers station position for generated held-output range exit");
        Check(g.holdingItem.id == 0 && GadgetCoreAPI.LastDrop.id == 89 && GadgetCoreAPI.LastDrop.q == 2
            && GadgetCoreAPI.LastDrop.extra == "generated prism", "Prism range exit drops exact generated output and clears held item after success");
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1,
            "Repeated Prism exit does not duplicate generated output");
        g.menuPrism.activeSelf = false; InventoryItemLifecycle.ObserveContext(g); g.holdingItem = new Item(90, 1);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1,
            "Closing Prism invalidates its remembered station position");
        g = Fresh();
        g.inventory[3] = new Item(10, 5) { extra = "metadata" };
        Action(g, "SelectItem", 3);
        Check(InventoryItemLifecycle.TryReturn(g, out reason), "pickup cancellation succeeds");
        Check(g.inventory[3].q == 5 && g.inventory[3].extra == "metadata", "whole item metadata preserved");

        // Harmony still runs postfixes when the earlier mutation guard skips
        // the original and the later side-effecting out-state prefix.
        g = Fresh(); g.curStoragePage = 2;
        g.inventory[3] = new Item(10, 5) { extra = "guarded source" };
        Action(g, "SelectItem", 3);
        Item guardedHeld = g.holdingItem;
        GadgetCoreAPI.ThrowSpawn = true; Network.FailDestroy = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && !Guard(g),
            "quarantine skips native action and later source-capture prefix");
        typeof(InventorySourcePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { g, 7, typeof(GameScript).GetMethod("SwapItemStorage"), null });
        Check(ReferenceEquals(g.holdingItem, guardedHeld) && guardedHeld.id == 10 && guardedHeld.q == 5
            && guardedHeld.extra == "guarded source" && g.inventory[3].id == 0 && !Guard(g),
            "postfix with skipped capture preserves quarantined held stack and does not throw");
        BindingFlags sourceFields = BindingFlags.Static | BindingFlags.NonPublic;
        Check((int)typeof(InventoryItemLifecycle).GetField("sourceSlot", sourceFields).GetValue(null) == 3
            && (int)typeof(InventoryItemLifecycle).GetField("sourcePage", sourceFields).GetValue(null) == 2
            && !(bool)typeof(InventoryItemLifecycle).GetField("sourceStorage", sourceFields).GetValue(null),
            "postfix with null state preserves exact original pickup provenance");

        g = Fresh(); g.inventory[3] = new Item(10, 5); Action(g, "SelectItem", 3);
        g.inventory[4] = new Item(10, 3); g.holdingItem.q = 2;
        Check(InventoryItemLifecycle.TryReturn(g, out reason) && g.inventory[3].q == 2 && g.inventory[4].q == 3,
            "partial placements remain completed; only remainder returns");

        g = Fresh(); g.inventory[0] = new Item(10, 5); g.inventory[2] = new Item(11, 1);
        Action(g, "SelectItem", 0); g.RejectPlace = true; Action(g, "SwapItem", 2); g.RejectPlace = false;
        Check(InventoryItemLifecycle.TryReturn(g, out reason) && g.inventory[0].q == 5 && g.inventory[2].id == 11,
            "rejected swap cannot overwrite original provenance");

        g = Fresh(); g.inventory[5] = new Item(10, 9); Action(g, "SplitItem", 5);
        Check(InventoryItemLifecycle.TryReturn(g, out reason) && g.inventory[5].q == 9, "split recombines exact original stack");

        g = Fresh(); g.inventory[0] = new Item(10, 5); Action(g, "SelectItem", 0);
        g.inventory[0] = new Item(10, 9997);
        Check(!InventoryItemLifecycle.TryReturn(g, out reason), "insufficient capacity rejected atomically");
        Check(g.inventory[0].q == 9997 && g.holdingItem.q == 5, "no partial return");
        g.inventory[0] = new Item(10, 1) { extra = "different" };
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && g.holdingItem.q == 5, "native metadata compatibility honored");

        g = Fresh(); g.inventory[1] = new Item(10, 5); g.inventory[2] = new Item(11, 2);
        Action(g, "SelectItem", 1); Action(g, "SwapItem", 2);
        Check(!InventoryItemLifecycle.TryReturn(g, out reason), "swap source occupied; no chain undo");
        Check(g.inventory[1].id == 0 && g.inventory[2].id == 10 && g.holdingItem.id == 11, "completed swap retained");
        g.inventory[2] = new Item(0, 0);
        Check(InventoryItemLifecycle.TryReturn(g, out reason) && g.inventory[2].id == 11, "newly held swap item returns to swap slot");

        g = Fresh(); g.curStoragePage = 11; g.storage[431] = new Item(12, 8) { extra = "sixth row" };
        Action(g, "SelectItemStorage", 35); g.curStoragePage = 2;
        Check(InventoryItemLifecycle.TryReturn(g, out reason) && g.storage[431].q == 8, "sixth row final page returns canonical slot");
        Check(g.curStoragePage == 2 && g.storage[65].id == 0, "current page and adjacent canonical slots preserved");
        Check(InventoryItemLifecycle.MapStorageSlot(11, 35, 432) == 431 && InventoryItemLifecycle.MapStorageSlot(0, 30, 360) == -1,
            "expanded and native storage bounds distinguished");

        g = Fresh(); g.inventory[36] = new Item(300, 1); Action(g, "SelectItem", 36); g.RejectPlace = true;
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && g.holdingItem.id == 300, "native equipment rejection preserves item");

        g = Fresh(); g.holdingItem = new Item(20, 8) { extra = "drop" };
        Check(InventoryItemLifecycle.TryDropAbovePlayer(g, out reason), "explicit drop succeeds");
        Check(GadgetCoreAPI.LastPosition.x == 5 && GadgetCoreAPI.LastPosition.y == 11 && GadgetCoreAPI.LastDrop.extra == "drop", "drop above player with metadata");
        InventoryItemLifecycle.TryDropAbovePlayer(g, out reason);
        Check(GadgetCoreAPI.Drops == 1 && g.holdingItem.id == 0, "repeated drop cannot duplicate");

        g = Fresh(); g.holdingItem = new Item(20, 8);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason), "walk-away falls back to drop with missing origin");
        Check(GadgetCoreAPI.LastPosition.x == 40 && GadgetCoreAPI.LastPosition.y == 21.5f, "walk-away uses remembered chest");

        g = Fresh(); g.holdingItem = new Item(20, 8); GadgetCoreAPI.FailSpawn = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && g.holdingItem.q == 8, "failed spawn retains held item");
        GadgetCoreAPI.FailSpawn = false; GadgetCoreAPI.ThrowSpawn = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason), "RPC exception is reported");
        Check(Network.Live.Count == 0 && g.holdingItem.q == 8 && !InventoryItemLifecycle.MutationsBlocked(g),
            "exception after spawn removes real pickup before releasing held item");
        GadgetCoreAPI.ThrowSpawn = false;
        Check(InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && GadgetCoreAPI.Drops == 3
            && Network.Live.Count == 1 && g.holdingItem.id == 0 && Guard(g), "rolled back drop safely retries with one real pickup");

        g = Fresh(); g.holdingItem = new Item(20, 8); GadgetCoreAPI.ThrowSpawn = true; Network.FailDestroy = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && InventoryItemLifecycle.MutationsBlocked(g)
            && !Guard(g) && Network.Live.Count == 1 && g.holdingItem.q == 8, "rollback failure quarantines both controller and native mutations");
        GadgetCoreAPI.ThrowSpawn = false; Network.FailDestroy = false;
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && !InventoryItemLifecycle.TryDropAbovePlayer(g, out reason)
            && GadgetCoreAPI.Drops == 1 && !Guard(g), "quarantined held copy cannot return or respawn");
        Application.loadedLevel++; InventoryItemLifecycle.ObserveContext(g);
        Check(InventoryItemLifecycle.MutationsBlocked(g) && !Guard(g), "scene transition cannot silently release an uncertain held copy");
        var targets = ((IEnumerable<MethodBase>)typeof(InventoryMutationGuardPatch)
            .GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null)).Select(x => x.Name).ToList();
        foreach (string name in new[] { "SelectItem", "SelectItemStorage", "SplitItem", "SwapItem", "PlaceItem", "PlaceOneItem", "CombineItem", "SelectItemCraft", "ForgeEmblem", "TrashItem", "PrismItem", "DropItem", "CloseAllMenus" })
            Check(targets.Contains(name), "native guard covers " + name);

        g = Fresh(); g.holdingItem = new Item(20, 8); InstallCapture(false);
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && GadgetCoreAPI.Drops == 0
            && g.holdingItem.q == 8 && Guard(g), "capture mismatch fails closed before API but normal mouse functions remain available");
        InstallCapture(true);
        InventoryItemLifecycle.Reset(); InventoryItemLifecycle.ObserveContext(g);
        var unrelated = Network.Instantiate(null, new Vector3(), new Quaternion(), 0);
        Check(ReferenceEquals(InventoryItemLifecycle.TrackSpawn(unrelated), unrelated), "unscoped spawn unchanged");
        Check(InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && Network.Live.Contains(unrelated)
            && !InventoryItemLifecycle.MutationsBlocked(g), "native unrelated pickup is untouched and success releases scoped state");

        g = Fresh(); g.holdingItem = new Item(20, 8); Application.loadedLevel++;
        PlayerScript.curInteractObj = null;
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0, "scene transition cannot use stale chest position");

        g = Fresh(); g.inventory[0] = new Item(10, 1); Action(g, "SelectItem", 0); g.ThrowRefresh = true;
        Check(InventoryItemLifecycle.TryReturn(g, out reason) && g.inventory[0].q == 1 && g.holdingItem.id == 0,
            "cosmetic exception after committed native placement cannot duplicate item");
        // Portal opening must capture the actual console before selection or
        // native range exit can switch curInteractObj to something else.
        g = Fresh();
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = new Vector3(90, 30, 0);
        g.Planets();
        typeof(InventoryPortalContextPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { g });
        g.inventory[1] = new Item(10, 5); g.inventory[2] = new Item(11, 2) { extra = "portal swap" };
        Action(g, "SelectItem", 1); Action(g, "SwapItem", 2);
        Check(!InventoryItemLifecycle.TryReturn(g, out reason), "portal incompatible swap correctly rejects return");
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = new Vector3(700, 900, 0);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1
            && GadgetCoreAPI.LastPosition.x == 90 && GadgetCoreAPI.LastPosition.y == 31.5f,
            "portal walk-away drops at remembered console, not changed interaction target or old chest");
        Check(g.holdingItem.id == 0 && g.inventory[2].id == 10 && GadgetCoreAPI.LastDrop.id == 11
            && GadgetCoreAPI.LastDrop.q == 2 && GadgetCoreAPI.LastDrop.extra == "portal swap",
            "portal fallback preserves completed swap and drops only exact held item");

        g = Fresh(); g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        g.inventory[3] = new Item(10, 5); Action(g, "SelectItem", 3);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && g.inventory[3].q == 5
            && GadgetCoreAPI.Drops == 0, "portal safe source return closes without a drop");

        g = Fresh(); g.menuStorage.activeSelf = false; InventoryItemLifecycle.ObserveContext(g);
        g.holdingItem = new Item(20, 8);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "closed root invalidates cached chest position");
        PlayerScript.curInteractObj = null; g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "portal opened without interaction cannot reuse prior closed chest position");

        g = Fresh(); g.holdingItem = new Item(20, 8); PlayerScript.curInteractObj = null;
        g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "direct panel replacement without opener invalidates prior chest position");

        g = Fresh(); g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        g.holdingItem = new Item(20, 8); GameScript.inventoryOpen = false;
        InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "inventory closure invalidates even active auxiliary panel position");
        GameScript.inventoryOpen = true;

        g = Fresh(); g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        g.holdingItem = new Item(20, 8); Application.loadedLevel++; PlayerScript.curInteractObj = null;
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "portal cached position cannot cross scene transition");
        g = Fresh(); OpenForge(g, new Vector3(130, 45, 0));
        // Forge-generated output has no inventory/storage pickup provenance.
        // Cancel must not invent a slot merely because this UI uses inventory.
        g.holdingItem = new Item(80, 1) { extra = "generated emblem" };
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && g.holdingItem.id == 80
            && GadgetCoreAPI.Drops == 0 && reason.Contains("source"),
            "forge output with no source rejects Cancel without assigning an inventory slot");
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = new Vector3(700, 900, 0);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1
            && GadgetCoreAPI.LastPosition.x == 130 && GadgetCoreAPI.LastPosition.y == 46.5f,
            "forge forced range exit drops generated output at remembered forge despite changed interaction target");
        Check(g.holdingItem.id == 0 && GadgetCoreAPI.LastDrop.id == 80
            && GadgetCoreAPI.LastDrop.q == 1 && GadgetCoreAPI.LastDrop.extra == "generated emblem",
            "forge output fallback preserves exact metadata and clears held item only after native drop");
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1,
            "repeated forge exit does not duplicate generated output");
        g.menuEmblemForge.activeSelf = false; InventoryItemLifecycle.ObserveContext(g);
        g.holdingItem = new Item(81, 1);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1,
            "closed forge invalidates remembered opener");

        g = Fresh(); OpenForge(g, new Vector3(130, 45, 0));
        g.inventory[3] = new Item(10, 5); Action(g, "SelectItem", 3);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && g.inventory[3].q == 5
            && GadgetCoreAPI.Drops == 0, "forge known inventory source returns safely before any fallback drop");

        g = Fresh(); OpenForge(g, new Vector3(130, 45, 0));
        g.holdingItem = new Item(80, 1); PlayerScript.curInteractObj = null;
        g.menuEmblemForge = new GameObject { activeSelf = true }; InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "replacement forge panel without opener cannot reuse previous forge position");

        g = Fresh(); OpenForge(g, new Vector3(130, 45, 0));
        g.holdingItem = new Item(80, 1); PlayerScript.curInteractObj = null;
        g.menuEmblemForge.activeSelf = false; g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "switching forge to portal without opener invalidates forge position");

        g = Fresh(); OpenForge(g, new Vector3(130, 45, 0));
        g.holdingItem = new Item(80, 1); Application.loadedLevel++; PlayerScript.curInteractObj = null;
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "forge cached position cannot cross scene transition");
        g = Fresh(); g.inventory[3] = new Item(10, 1);
        Action(g, "SelectItem", 3); g.PlaceItem(4);
        // Native placement cleared the held item; opening the forge observes
        // that empty hand before generating output, invalidating old provenance.
        OpenForge(g, new Vector3(150, 60, 0)); g.ForgeEmblem(0);
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && reason.Contains("source")
            && g.inventory[3].id == 0 && g.inventory[4].id == 10 && g.holdingItem.id == 80,
            "production forge opener clears stale pickup provenance before generated output");
        PlayerScript.curInteractObj = null;
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.LastPosition.x == 150
            && GadgetCoreAPI.LastPosition.y == 61.5f && GadgetCoreAPI.LastDrop.id == 80,
            "production forge opener postfix captures fallback position before interaction disappears");
        g = Fresh();
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = new Vector3(180, 70, 0);
        g.Trash();
        typeof(InventoryTrashContextPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { g });
        g.inventory[6] = new Item(10, 5); g.inventory[7] = new Item(11, 2) { extra = "trasher swap" };
        Action(g, "SelectItem", 6); Action(g, "SwapItem", 7);
        PlayerScript.curInteractObj = new GameObject();
        PlayerScript.curInteractObj.transform.position = new Vector3(700, 900, 0);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1
            && GadgetCoreAPI.LastPosition.x == 180 && GadgetCoreAPI.LastPosition.y == 71.5f,
            "Trasher opener captures station location for unreturnable held-item exit");
        Check(g.holdingItem.id == 0 && g.inventory[7].id == 10 && GadgetCoreAPI.LastDrop.id == 11
            && GadgetCoreAPI.LastDrop.q == 2 && GadgetCoreAPI.LastDrop.extra == "trasher swap",
            "Trasher range exit preserves completed swap and exact held-item metadata");
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1,
            "Repeated Trasher exit cannot duplicate fallback item");
        g.menuTrasher.activeSelf = false; InventoryItemLifecycle.ObserveContext(g); g.holdingItem = new Item(12, 2);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 1,
            "Closing Trasher invalidates its remembered position");

        g = Fresh(); g.Trash();
        typeof(InventoryTrashContextPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { g });
        g.inventory[6] = new Item(21, 5); Action(g, "SelectItem", 6);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && g.inventory[6].q == 5 && GadgetCoreAPI.Drops == 0,
            "Trasher known source returns safely without dropping");
        g.holdingItem = new Item(20, 8); GadgetCoreAPI.ThrowSpawn = true; Network.FailDestroy = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && InventoryItemLifecycle.MutationsBlocked(g),
            "Uncertain drop quarantines inventory while Trasher is open");
        Item beforeTrash = g.inventory[6];
        if(Guard(g)) g.TrashItem(6);
        Check(ReferenceEquals(g.inventory[6], beforeTrash) && g.inventory[6].id == 21 && g.inventory[6].q == 5,
            "Production quarantine guard blocks native TrashItem without mutating the ingredient");
        g = Fresh(); OpenPrism(g, new Vector3(210, 85, 0));
        g.inventory[6] = new Item(21, 5) { extra = "Prism pickup" }; Action(g, "SelectItem", 6);
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && g.inventory[6].q == 5
            && g.inventory[6].extra == "Prism pickup" && GadgetCoreAPI.Drops == 0,
            "Prism known pickup source returns safely without fallback drop");
        g.inventory[7] = new Item(86, 25); g.holdingItem = new Item(90, 2) { extra = "uncertain Prism output" };
        GadgetCoreAPI.ThrowSpawn = true; Network.FailDestroy = true;
        Check(!InventoryItemLifecycle.TryDropAbovePlayer(g, out reason) && !Guard(g),
            "Uncertain Prism output drop quarantines native conversion");
        Item beforePrism = g.inventory[7], beforePrismHeld = g.holdingItem;
        if (Guard(g)) g.PrismItem(7);
        Check(ReferenceEquals(g.inventory[7], beforePrism) && g.inventory[7].q == 25
            && ReferenceEquals(g.holdingItem, beforePrismHeld) && g.holdingItem.id == 90 && g.holdingItem.q == 2
            && g.holdingItem.extra == "uncertain Prism output", "Prism quarantine preserves ingredient and held output without native conversion");

        g = Fresh(); g.inventory[3] = new Item(10, 1);
        Action(g, "SelectItem", 3); g.PlaceItem(4);
        OpenPrism(g, new Vector3(220, 90, 0)); g.inventory[6] = new Item(87, 10); g.PrismItem(6);
        Check(!InventoryItemLifecycle.TryReturn(g, out reason) && reason.Contains("source")
            && g.inventory[3].id == 0 && g.inventory[4].id == 10 && g.holdingItem.id == 90,
            "Production Prism opener clears stale pickup provenance before generating output");
        PlayerScript.curInteractObj = null;
        Check(InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.LastPosition.x == 220
            && GadgetCoreAPI.LastPosition.y == 91.5f && GadgetCoreAPI.LastDrop.id == 90,
            "Production Prism opener captures fallback location before interaction disappears");

        g = Fresh(); OpenPrism(g, new Vector3(210, 85, 0)); g.holdingItem = new Item(91, 1);
        PlayerScript.curInteractObj = null; g.menuPrism = new GameObject { activeSelf = true };
        InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "Replacement Prism panel without opener cannot reuse previous station position");
        g = Fresh(); OpenPrism(g, new Vector3(210, 85, 0)); g.holdingItem = new Item(91, 1);
        PlayerScript.curInteractObj = null; g.menuPrism.activeSelf = false; g.Planets(); InventoryItemLifecycle.ObserveContext(g);
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "Switching Prism to portal without opener invalidates previous Prism position");
        g = Fresh(); OpenPrism(g, new Vector3(210, 85, 0)); g.holdingItem = new Item(91, 1);
        PlayerScript.curInteractObj = null; Application.loadedLevel++;
        Check(!InventoryItemLifecycle.LeaveRange(g, out reason) && GadgetCoreAPI.Drops == 0,
            "Prism cached position cannot cross scene transition");
        Console.WriteLine("Inventory lifecycle: " + checks + " checks passed.");
    }
}
