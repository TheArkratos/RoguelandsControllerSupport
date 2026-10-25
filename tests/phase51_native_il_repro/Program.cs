using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HarmonyLib;

// Calls compiled production transpilers against the installed assembly's real IL.
// Does not instantiate Unity objects, invoke gameplay, launch a profile or write saves.
internal static class Program
{
    private static readonly BindingFlags All = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static readonly Dictionary<short,OpCode> Opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)
        .Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
    private static int checks;
    private static Assembly mod,game;
    private static void Check(bool value,string description) { checks++; if(!value) throw new Exception(description); }
    private static bool Call(CodeInstruction instruction,string type,string method)
    { return (instruction.opcode==OpCodes.Call || instruction.opcode==OpCodes.Callvirt) && instruction.operand is MethodBase target && target.DeclaringType.FullName==type && target.Name==method; }
    private static List<CodeInstruction> Invoke(string type,string method,params object[] arguments)
    { return ((IEnumerable<CodeInstruction>)mod.GetType("ControllerSupport."+type,true).GetMethod(method,All).Invoke(null,arguments)).ToList(); }
    private static MethodInfo Iterator(Type owner,string name)
    { return owner.GetNestedTypes(All).Single(t=>t.Name.StartsWith("<"+name+">",StringComparison.Ordinal)).GetMethod("MoveNext",All); }
    private static string ModDirectory([System.Runtime.CompilerServices.CallerFilePath]string source="")
    { return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source),"..","..")); }
    private static void Main()
    {
        string managed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local/share/Steam/steamapps/common/Roguelands/Roguelands_Data/Managed");
        AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=> { string path=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll"); return File.Exists(path)?Assembly.LoadFrom(path):null; };
        game=Assembly.LoadFrom(Path.Combine(managed,"Assembly-CSharp.dll"));
        mod=Assembly.LoadFrom(Path.Combine(ModDirectory(),"build","ControllerSupport.dll"));
        // GadgetLoader.PatchGadget enumerates GetExportedTypes, not GetTypes.
        // A working transpiler on an internal class is never installed in-game.
        // The installed GadgetCore is x86-only PE; inspect actual DLL metadata
        // instead of executing it in this x64 .NET test host. MetadataLoadContext
        // returns Assembly/Type objects with the same exported-type contract.
        string sdkDirectory=Directory.GetDirectories("/usr/share/dotnet/sdk").OrderByDescending(p=>p).First(p=>File.Exists(Path.Combine(p,"System.Reflection.MetadataLoadContext.dll")));
        Assembly metadataApi=Assembly.LoadFrom(Path.Combine(sdkDirectory,"System.Reflection.MetadataLoadContext.dll"));
        string[] metadataPaths=Directory.GetFiles(managed,"*.dll").Concat(new[]{mod.Location}).ToArray();
        object resolver=Activator.CreateInstance(metadataApi.GetType("System.Reflection.PathAssemblyResolver",true),new object[]{metadataPaths});
        object context=Activator.CreateInstance(metadataApi.GetType("System.Reflection.MetadataLoadContext",true),new object[]{resolver,"mscorlib"});
        Assembly inspected=(Assembly)context.GetType().GetMethod("LoadFromAssemblyPath").Invoke(context,new object[]{mod.Location});
        Type[] exported=inspected.GetExportedTypes();
        foreach(string name in new[]{"AimAssistFactoryPatch","AimAssistIteratorPatch","AimAssistDirectPatch","AimAssistStaffSpawnPatch","AimAssistSkillPatch","AimAssistAtalantaPatch",
            "InventoryDropCapturePatch","InventoryMutationGuardPatch","InventorySourcePatch",
            "InventoryChestContextPatch","InventoryPortalContextPatch","InventoryHeldEmptyPatch",
            "InventoryEmblemContextPatch","InventoryTrashContextPatch","InventoryPrismContextPatch","InventoryAlchemyContextPatch","InventoryGearForgeContextPatch","InventoryChipSourcePatch","InventoryCombatChipContextPatch","FrontEndNativeHoverPatch"})
        {
            Type patch=inspected.GetType("ControllerSupport."+name,true);
            Check(exported.Contains(patch),"GadgetCore loader discovers exported patch "+name);
            CustomAttributeData gadget=patch.GetCustomAttributesData().SingleOrDefault(a=>a.AttributeType.FullName=="GadgetCore.API.HarmonyGadgetAttribute");
            Check(gadget!=null&&gadget.ConstructorArguments.Count>=1&&(string)gadget.ConstructorArguments[0].Value=="Controller Support","Exported patch belongs to exact Controller Support gadget: "+name);
            Check(patch.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"),"Exported patch retains Harmony patch declaration: "+name);
        }
        foreach(Type patch in inspected.GetTypes())
        {
            CustomAttributeData gadget=patch.GetCustomAttributesData().SingleOrDefault(a=>a.AttributeType.FullName=="GadgetCore.API.HarmonyGadgetAttribute");
            if(gadget==null || gadget.ConstructorArguments.Count<1 || (string)gadget.ConstructorArguments[0].Value!="Controller Support") continue;
            Check(exported.Contains(patch),"Every Controller Support GadgetCore patch is exported: "+patch.FullName);
        }
        foreach (var declaration in new[] {
            new[]{"InventoryDropCapturePatch","GadgetCore.API.GadgetCoreAPI","DropItem"},
            new[]{"InventoryChestContextPatch","GameScript","Storage"},
            new[]{"InventoryPortalContextPatch","GameScript","Planets"},
            new[]{"InventoryHeldEmptyPatch","GameScript","RefreshHoldingSlot"},
            new[]{"InventoryEmblemContextPatch","GameScript","EmblemForge"},
            new[]{"InventoryTrashContextPatch","GameScript","Trash"},
            new[]{"InventoryPrismContextPatch","GameScript","Prism"},
            new[]{"InventoryAlchemyContextPatch","GameScript","Alchemy"},
            new[]{"InventoryGearForgeContextPatch","GameScript","GearForge"},
            new[]{"InventoryCombatChipContextPatch","GameScript","CombatChipMenu"}})
        {
            Type patch = inspected.GetType("ControllerSupport." + declaration[0], true);
            CustomAttributeData target = patch.GetCustomAttributesData().Single(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch");
            Check(target.ConstructorArguments.Count>=2 && ((Type)target.ConstructorArguments[0].Value).FullName==declaration[1]
                && (string)target.ConstructorArguments[1].Value==declaration[2], "Actual lifecycle patch target declaration: "+declaration[0]);
        }
        MethodBase[] hoverTargets = Targets("FrontEndNativeHoverPatch");
        Check(hoverTargets.Length == 2 && hoverTargets.All(t => t.DeclaringType.FullName == "ButtonMenu")
            && hoverTargets.Select(t => t.Name).OrderBy(n => n).SequenceEqual(new[] { "OnMouseEnter", "OnMouseExit" }),
            "Native front-end hover patch targets exactly ButtonMenu enter and exit callbacks");
        MethodInfo hoverPrefix = mod.GetType("ControllerSupport.FrontEndNativeHoverPatch", true).GetMethod("Prefix", All);
        Check(hoverPrefix.ReturnType == typeof(bool) && hoverPrefix.GetParameters().Length == 1
            && hoverPrefix.GetParameters()[0].ParameterType.FullName == "ButtonMenu",
            "Native hover guard uses the actual button instance and boolean Harmony prefix contract");
        MethodInfo hoverDispatch = mod.GetType("ControllerSupport.ControllerFrontEndMenus", true).GetMethod("DispatchNativeHover", All);
        List<CodeInstruction> hoverCode = Read(hoverDispatch);
        Check(hoverCode.Count(i => Call(i, "ButtonMenu", "OnMouseEnter")) == 1
            && hoverCode.Count(i => Call(i, "ButtonMenu", "OnMouseExit")) == 1,
            "Compiled front-end focus reuses native hover enter/exit handlers");
        Assembly loader=(Assembly)context.GetType().GetMethod("LoadFromAssemblyPath").Invoke(context,new object[]{Path.Combine(managed,"GadgetCore.dll")});
        Type prismRecipePatch=loader.GetType("GadgetCore.Patches.Patch_GameScript_PrismItem",true);
        MethodInfo prismRecipePrefix=prismRecipePatch.GetMethod("Prefix",All | BindingFlags.DeclaredOnly);
        Check(prismRecipePrefix!=null && prismRecipePrefix.GetParameters().Any(p=>p.Name=="___slotID" && p.ParameterType.IsByRef && p.ParameterType.GetElementType().FullName=="System.Int32"),
            "Actual installed GadgetCore Prism prefix consumes private slotID by reference");
        // GadgetCore references a legacy mscorlib 2.0 System.Type in its
        // attribute constructors. Inspect declaration presence here without
        // decoding that unavailable dependency; verify the exact target below
        // against the hash-verified loader reference source.
        Check(prismRecipePatch.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"),
            "Actual installed GadgetCore Prism recipe class retains Harmony patch declaration");
        ((IDisposable)context).Dispose();
        MethodBase[] mutationTargets = Targets("InventoryMutationGuardPatch");
        Type inventoryGame = game.GetType("GameScript", true);
        foreach (string name in new[]{"SelectItem","SplitItem","SwapItem","SelectItemStorage","SplitItemStorage","SwapItemStorage","ForgeEmblem","TrashItem","PrismItem",
            "SelectItemCraft","SplitItemCraft","SwapItemCraft","PlaceItemCraft","PlaceOneItemCraft","CombineItemCraft",
            "SelectCombatChip","PlaceCombatChip","SwapCombatChip"})
            Check(mutationTargets.Contains(inventoryGame.GetMethod(name,All,null,new[]{typeof(int)},null)), "Quarantine guards native mutation "+name+"(int)");
        foreach (string name in new[]{"CloseAllMenus","DropItem","Craft","DropCraftItems"})
            Check(mutationTargets.Contains(inventoryGame.GetMethod(name,All,null,Type.EmptyTypes,null)), "Quarantine guards native "+name+"()");
        MethodInfo[] queued = inventoryGame.GetNestedTypes(All).Where(t=>(t.Name.Contains("ShiftClick") || t.Name.Contains("QuickCraft")) && typeof(System.Collections.IEnumerator).IsAssignableFrom(t))
            .Select(t=>t.GetMethod("MoveNext",All)).Where(m=>m!=null&&m.ReturnType==typeof(bool)).ToArray();
        Check(queued.Length>0,"Real native ShiftClick iterators exist");
        foreach(MethodInfo move in queued) Check(mutationTargets.Contains(move),"Quarantine guards previously queued "+move.DeclaringType.Name);
        MethodInfo trash = inventoryGame.GetMethod("TrashItem",All,null,new[]{typeof(int)},null);
        Check(trash.ReturnType==typeof(void),"Native TrashItem is synchronous void, not a queued conversion coroutine");
        Check(!inventoryGame.GetNestedTypes(All).Any(t=>t.Name.StartsWith("<TrashItem>",StringComparison.Ordinal)),"Native TrashItem has no iterator requiring an additional queued guard");
        Check(Read(trash).Count(i=>Call(i,"GameScript","GetItemWorth"))==1,"Trash conversion retains native worth calculation");
        Check(Read(trash).Count(i=>Call(i,"GameScript","Trasher"))==1,"Trash conversion retains native mode re-entry");
        MethodInfo prism = inventoryGame.GetMethod("PrismItem",All,null,new[]{typeof(int)},null);
        var prismNative=Read(prism);
        Check(prism.ReturnType==typeof(void),"Native PrismItem is synchronous void, not a queued conversion coroutine");
        Check(!inventoryGame.GetNestedTypes(All).Any(t=>t.Name.StartsWith("<PrismItem>",StringComparison.Ordinal)),"Native PrismItem has no iterator requiring an additional queued guard");
        Check(prismNative.Count(i=>Call(i,"GameScript","Prismer"))==1,"Prism conversion retains native mode exit");
        Check(prismNative.Where((i,index)=>i.opcode==OpCodes.Div && index>0 && prismNative[index-1].opcode==OpCodes.Ldc_I4_S
                && Convert.ToInt32(prismNative[index-1].operand)==10).Count()==1 && prismNative.Count(i=>i.opcode==OpCodes.Mul)==1
            && prismNative.Count(i=>i.opcode==OpCodes.Sub)==1 && prismNative.Count(i=>i.opcode==OpCodes.Ldc_I4_S && Convert.ToInt32(i.operand)==10)==2,
            "Native Prism conversion divides quantity by ten and subtracts converted batches times ten");
        Check(prismNative.Any(i=>i.opcode==OpCodes.Stfld && i.operand is FieldInfo f && f.Name=="holdingItem"),
            "Native Prism conversion produces held output rather than replacing the input slot");
        FieldInfo slotID=inventoryGame.GetField("slotID",All);
        Check(slotID!=null && slotID.IsPrivate && slotID.FieldType==typeof(int),"Native private slotID exists for loader recipe lookup");
        string patches=Path.Combine(ModDirectory(),"..","..","GameReference","Generated","GadgetCore","GadgetCore.Patches");
        string prismRecipeSource=File.ReadAllText(Path.Combine(patches,"Patch_GameScript_PrismItem.cs"));
        Check(prismRecipeSource.Contains("[HarmonyPatch(\"PrismItem\")]") && prismRecipeSource.Contains("prismForgeRecipes.ContainsKey(___inventory[___slotID].id)")
            && prismRecipeSource.Contains("___inventory[___slotID].q >= num"),"Verified loader recipe source validates selected private slotID and required quantity");
        Check(prismRecipeSource.Contains("___inventory[slot].q / num") && prismRecipeSource.Contains("___inventory[slot].q -= num2 * 10"),
            "Verified loader recipe source uses configured divisor but fixed ten-item consumption");
        Check(prismRecipeSource.Contains("___prismAgain = false;") && prismRecipeSource.Contains("___holdingItem = new Item("),
            "Verified loader recipe source exits conversion mode and generates held output");
        string loaderUpdate=File.ReadAllText(Path.Combine(patches,"Patch_GameScript_Update.cs"));
        Check(loaderUpdate.Contains("new CodeInstruction(OpCodes.Call, \"Void PrismItem(Int32)\")")
            && loaderUpdate.Contains("cILProcessor.RemoveInsns(iLRef2.GetRefByOffset(-20), 17)"),
            "Verified loader Update transpiler removes native vanilla-only Prism recipe eligibility block");
        MethodBase[] sourceTargets=Targets("InventorySourcePatch");
        foreach(string name in new[]{"SelectItem","SplitItem","SwapItem","SelectItemStorage","SplitItemStorage","SwapItemStorage",
            "SelectItemCraft","SplitItemCraft","SwapItemCraft"})
            Check(sourceTargets.Contains(inventoryGame.GetMethod(name,All,null,new[]{typeof(int)},null)),"Exact source tracking targets "+name);
        Check(sourceTargets.Length==9,"Exact source tracking covers nine inventory/storage/crafting pickup actions");
        Type craftingContext = mod.GetType("ControllerSupport.CraftingMenuContext", true);
        Check((int)craftingContext.GetField("MaximumQuickCraftQuantity", All).GetRawConstantValue() == 7,
            "Controller quick-craft capacity reserve matches audited maximum seven");
        List<CodeInstruction> recipeCode = Read(inventoryGame.GetMethod("GetRecipeItem", All));
        int recipeRolls = 0, maximumQuantity = 0;
        for (int i = 2; i < recipeCode.Count; i++)
        {
            if (!Call(recipeCode[i], "UnityEngine.Random", "Range")) continue;
            int? minimum = IntegerConstant(recipeCode[i - 2]), upper = IntegerConstant(recipeCode[i - 1]);
            Check(minimum.HasValue && upper.HasValue && minimum.Value >= 0 && upper.Value <= 8,
                "Native Alchemy recipe quantity roll has a proven bounded integer range");
            maximumQuantity = Math.Max(maximumQuantity, upper.Value - 1); recipeRolls++;
        }
        Check(recipeRolls == 24 && maximumQuantity == 7,
            "All 24 native Alchemy recipes produce at most seven items per quick craft");
        foreach (string method in new[] { "ActionLabel", "SecondaryLabel", "CanTransfer", "CanTransferInventory", "CanActivate" })
            Check(!CallsRecipeGetter(craftingContext.GetMethod(method, All)),
                "Controller Alchemy eligibility/hints do not reroll native recipes: " + method);
        foreach (string method in new[] { "ActionLabel", "SecondaryLabel", "CanTransfer", "CanTransferInventory", "CanActivate", "CanPlaceOne" })
            Check(!CallsRecipeGetter(craftingContext.GetMethod(method, All), "GetGearCrafted") &&
                !CallsRecipeGetter(craftingContext.GetMethod(method, All), "GetRandomTier"),
                "Shared crafting eligibility/hints do not generate or reroll gear: " + method);
        MethodInfo nativeGearUnlock = inventoryGame.GetMethod("GetRecipeUnlocked0", All);
        List<CodeInstruction> nativeUnlockCode = Read(nativeGearUnlock);
        Check(nativeUnlockCode.Any(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo field && field.Name == "recipeUnlocked0"),
            "Native Gear unlock getter reads the actual stored recipe matrix");
        Check(!CallsRecipeGetter(nativeGearUnlock) && !CallsRecipeGetter(nativeGearUnlock, "GetRandomTier") &&
            !CallsRecipeGetter(nativeGearUnlock, "Craft"), "Native Gear unlock lookup is independent of output rolls and crafting");
        string nativeSource = File.ReadAllText(Path.Combine(ModDirectory(), "..", "..", "GameReference", "Generated", "Assembly-CSharp", "GameScript.cs"));
        string fixtureGetter = File.ReadAllText(Path.Combine(ModDirectory(), "tests", "gear_forge_context_repro", "NativeGearRecipe.cs"));
        string referenceGetter = ExtractMethod(nativeSource, "public int GetRecipeUnlocked0(int p, int n)");
        fixtureGetter = ExtractMethod(fixtureGetter, "public int GetRecipeUnlocked0(int p, int n)")
            .Replace("UnlockCalls++; LastUnlockPage = p; LastUnlockCell = n;", "");
        Check(System.Text.RegularExpressions.Regex.Replace(referenceGetter, @"\s+", "") ==
            System.Text.RegularExpressions.Regex.Replace(fixtureGetter, @"\s+", ""),
            "Gear context fixture uses the unchanged native unlock getter body apart from call recording");
        Check(CallsRecipeGetter(inventoryGame.GetMethod("GetGearCrafted", All), "GetRandomTier"),
            "Native gear generation retains ownership of random-tier selection");
        Check(queued.Any(method => method.DeclaringType.Name.Contains("QuickCraft")),
            "Pending-drop quarantine includes native quick-craft continuation");
        // Old Unity's logger is managed until its enabled sink reaches a native
        // ECall. Disable that sink so successful production IL audits can finish.
        Assembly unity=Assembly.LoadFrom(Path.Combine(managed,"UnityEngine.dll"));
        Type debug=unity.GetType("UnityEngine.Debug",true);
        PropertyInfo logger=debug.GetProperty("unityLogger",All)??debug.GetProperty("logger",All);
        object managedLogger=logger.GetValue(null,null);
        managedLogger.GetType().GetProperty("logEnabled").SetValue(managedLogger,false,null);
        Type player=game.GetType("PlayerScript",true),projectile=game.GetType("Projectile",true);
        MethodInfo attack=Iterator(player,"Attack");
        var native=Read(attack); var patched=Invoke("AimAssistIL","Replace",native,attack);
        FieldInfo attacking=player.GetField("attacking",All);
        int trueStore=native.FindIndex(i=>i.opcode==OpCodes.Stfld&&Equals(i.operand,attacking)&&native[native.IndexOf(i)-1].opcode==OpCodes.Ldc_I4_1);
        Check(trueStore>=0,"native actual attacking=true transition found");
        Check(native.Take(trueStore).Count(i=>i.opcode.FlowControl==FlowControl.Cond_Branch)>=3,"actual transition follows native conditional guards");
        foreach(string guard in new[]{"attacking","canAttack","combatSwitching","beaming"})
        {
            int read=native.FindIndex(i=>(i.opcode==OpCodes.Ldfld||i.opcode==OpCodes.Ldsfld)&&i.operand is FieldInfo f&&f.Name==guard);
            Check(read>=0&&read<trueStore,"actual acquisition follows guard read "+guard);
            Check(native.Skip(read+1).Take(trueStore-read-1).Any(i=>i.opcode.FlowControl==FlowControl.Cond_Branch),"guard branches before acquisition "+guard);
        }
        Check(patched.Count(i=>Call(i,"ControllerSupport.ControllerAimAssist","BeginAttack"))==1,"one acquisition injected per native activation");
        int begin=patched.FindIndex(i=>Call(i,"ControllerSupport.ControllerAimAssist","BeginAttack"));
        Check(patched[begin-1].opcode==OpCodes.Ldarg_0&&patched[begin-2].opcode==OpCodes.Stfld&&Equals(patched[begin-2].operand,attacking),"acquisition consumes iterator after actual state transition");
        Check(patched.Count==native.Count+2,"only acquisition adds two instructions");
        foreach(string field in new[]{"staticX","staticY"})
        {
            Check(native.Count(i=>i.opcode==OpCodes.Ldsfld&&i.operand is FieldInfo f&&f.DeclaringType.Name=="ControllerCursor"&&f.Name==field)==1,"native attack has expected "+field+" branch");
            Check(!patched.Any(i=>i.opcode==OpCodes.Ldsfld&&i.operand is FieldInfo f&&f.DeclaringType.Name=="ControllerCursor"&&f.Name==field),"attack no longer reads visual "+field);
            Check(patched.Count(i=>Call(i,"ControllerSupport.ControllerAimAssist",field=="staticX"?"CursorX":"CursorY"))==1,"attack uses separate attack-point getter for "+field);
        }
        Check(!patched.Any(i=>i.opcode==OpCodes.Stsfld&&i.operand is FieldInfo f&&f.DeclaringType.Name=="ControllerCursor"),"assistance does not write global visual cursor");
        foreach(string name in new[]{"UpdatePos","Boomerang"})
        {
            MethodInfo target=Iterator(projectile,name); var original=Read(target); var changed=Invoke("AimAssistIL","Replace",original,target);
            Check(original.Count(i=>Call(i,"UnityEngine.Input","get_mousePosition"))==1,name+" exact native mouse pattern");
            Check(!changed.Any(i=>Call(i,"UnityEngine.Input","get_mousePosition")),name+" native mouse is adapted");
            Check(changed.Count(i=>Call(i,"ControllerSupport.ControllerAimAssist","StaffMousePosition"))==1,name+" projectile-owned tracking adapter is sampled");
            Check(changed.Count==original.Count,name+" preserves native lifetime and timing instructions");
        }
        MethodInfo staff=player.GetMethod("Staff",All); var staffNative=Read(staff); var staffPatched=Invoke("AimAssistStaffSpawnPatch","Transpiler",staffNative);
        Check(staffNative.Count(i=>Call(i,"UnityEngine.Network","Instantiate"))==2,"both native staff creation branches verified");
        Check(staffPatched.Count(i=>Call(i,"ControllerSupport.AimAssistStaffSpawnPatch","Capture"))==2,"both staff spawns retain activation");
        for(int i=0;i<staffPatched.Count;i++)if(Call(staffPatched[i],"UnityEngine.Network","Instantiate"))
            Check(Call(staffPatched[i+1],"ControllerSupport.AimAssistStaffSpawnPatch","Capture"),"capture immediately follows creation, before SetStaff RPC");
        Check(staffPatched.Count==staffNative.Count+2,"staff capture leaves other instructions intact");
        MethodInfo update=game.GetType("GameScript",true).GetMethod("Update",All);var updateNative=Read(update);var updatePatched=Invoke("GameplayInputPatches","Game",updateNative);
        foreach(string name in new[]{"Cancel","CombatMode","Inventory","Droid","UseItem1","UseItem2","UseItem3","UseItem4","UseItem5","UseItem6"})
        {
            int source=Named(updateNative,name,"UnityEngine.Input","GetButtonDown");
            Check(source==1,"native exact named input "+name);
            Check(Named(updatePatched,name,"UnityEngine.Input","GetButtonDown")==0,"no unadapted named input "+name);
            if(name!="Droid") Check(Named(updatePatched,name,"ControllerSupport.ControllerGameplayInput","GameButtonDown")==1,"logical input routed once "+name);
        }
        Check(updatePatched.Count(i=>Call(i,"ControllerSupport.ControllerGameplayInput","GameDroidButtonDown"))==1,"Droid instance adapter remains targeted");
        Check(updatePatched.Count==updateNative.Count+1,"only Droid receiver argument changes native update length");
        Check(updateNative.Count(i=>Call(i,"GameScript","PrismItem"))==1 && updatePatched.Count(i=>Call(i,"GameScript","PrismItem"))==1,
            "Controller Update input adapter preserves native Prism conversion dispatch");
        Check(updateNative.Count(i=>Call(i,"GameScript","Craft")) > 0 &&
            updatePatched.Count(i=>Call(i,"GameScript","Craft")) == updateNative.Count(i=>Call(i,"GameScript","Craft")),
            "Native crafting action calls remain intact after controller input adaptation");
        Check(updateNative.Count(i=>Call(i,"GameScript","UseSkill"))==6&&updatePatched.Count(i=>Call(i,"GameScript","UseSkill"))==6,"six native chip actions retained");
        Check(updateNative.Count(i=>Call(i,"GameScript","UseItem"))==6&&updatePatched.Count(i=>Call(i,"GameScript","UseItem"))==6,"six native item actions retained");
        MethodBase[] chipSources = Targets("InventoryChipSourcePatch");
        Check(chipSources.Length == 2 && chipSources.Select(t => t.Name).OrderBy(n => n)
            .SequenceEqual(new[] { "SelectCombatChip", "SwapCombatChip" }),
            "Chip provenance observes exactly native pickup and swap methods");
        Type chipContext = mod.GetType("ControllerSupport.CombatChipMenuContext", true);
        Check(inventoryGame.GetField("combatChips", All).FieldType == typeof(int[]) &&
            inventoryGame.GetField("holdingCombatChip", All).FieldType == typeof(bool),
            "Actual native chips use integer slots and a separate held-type flag");
        foreach (string name in new[] { "SelectCombatChip", "PlaceCombatChip", "SwapCombatChip" })
        {
            Check(updateNative.Count(i => Call(i, "GameScript", name)) == 1 &&
                updatePatched.Count(i => Call(i, "GameScript", name)) == 1,
                "Controller input adaptation preserves native chip action dispatch: " + name);
            List<CodeInstruction> nativeChipAction = Read(inventoryGame.GetMethod(name, All));
            Check(nativeChipAction.Count(i => Call(i, "GameScript", "UpdateHP")) == 1 &&
                nativeChipAction.Count(i => Call(i, "GameScript", "UpdateMana")) == 1,
                "Native chip stat updates remain authoritative: " + name);
        }
        List<CodeInstruction> pickupChipCode = Read(inventoryGame.GetMethod("SelectCombatChip", All));
        Check(!pickupChipCode.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f &&
            f.DeclaringType.FullName == "Item" && f.Name == "q"),
            "Native chip pickup does not assign an ordinary stack quantity");
        foreach (string name in new[] { "SaveGame", "LoadGame" })
            Check(Read(inventoryGame.GetMethod(name, All)).Any(i => i.opcode == OpCodes.Ldfld &&
                i.operand is FieldInfo f && f.Name == "combatChips"),
                "Native chip persistence remains present: " + name);
        Type guide = mod.GetType("ControllerSupport.ControllerCombatGuide", true);
        Check(Read(mod.GetType("ControllerSupport.StorageHeldItemCursorPatch", true).GetMethod("Postfix", All))
            .Count(i => Call(i, "ControllerSupport.ControllerCombatGuide", "Observe")) == 1,
            "Native ActionCursor postfix observes the combat guide once");
        Check(Read(mod.GetType("ControllerSupport.ProbeRunner", true).GetMethod("LateUpdate", All))
            .Count(i => Call(i, "ControllerSupport.ControllerCombatGuide", "UpdateVisual")) == 1,
            "Combat guide refresh runs after native update/camera motion");
        Check(CountCompiledCalls("ControllerSupport.ControllerSupportGadget", "Unload",
            "ControllerSupport.ControllerCombatGuide", "Reset") == 1,
            "Gadget unload restores native bars and removes owned guide resources");
        Check(Read(guide.GetMethod("UpdateCone", All))
            .Count(i => Call(i, "ControllerSupport.AimAssistGeometry", "VisibleConePolygon")) == 1,
            "Staff cone rendering reuses the shared viewport-clipped aim-assist cone geometry");
        Console.WriteLine("Controller Support installed native IL and patch discovery: "+checks+" checks passed.");
    }
    // Inspect call tokens without runtime-loading GadgetCore's x86-only DLL.
    // This preserves the real compiled-helper check on a 64-bit test host.
    private static bool CallsRecipeGetter(MethodInfo method, string targetName = "GetRecipeItem")
    {
        using var stream = File.OpenRead(method.Module.Assembly.Location);
        using var pe = new PEReader(stream);
        return CountCalls(pe.GetMetadataReader(), method.GetMethodBody().GetILAsByteArray(), targetName) > 0;
    }
    private static int CountCompiledCalls(string typeName, string methodName, string targetType, string targetName)
    {
        using var stream = File.OpenRead(mod.Location);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        TypeDefinition type = metadata.TypeDefinitions.Select(h => metadata.GetTypeDefinition(h))
            .Single(t => metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name) == typeName);
        MethodDefinition method = type.GetMethods().Select(h => metadata.GetMethodDefinition(h))
            .Single(m => metadata.GetString(m.Name) == methodName);
        return CountCalls(metadata, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes(), targetName, targetType);
    }
    private static int CountCalls(MetadataReader metadata, byte[] bytes, string targetName, string targetType = null)
    {
        int matches = 0;
        for (int position = 0; position < bytes.Length;)
        {
            byte first = bytes[position++];
            short key = first == 0xFE ? (short)(0xFE00 | bytes[position++]) : first;
            OpCode op = Opcodes[key];
            if ((op == OpCodes.Call || op == OpCodes.Callvirt) && op.OperandType == OperandType.InlineMethod)
            {
                EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(bytes, position));
                string name = null, owner = null;
                if (handle.Kind == HandleKind.MethodDefinition)
                {
                    MethodDefinition method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                    name = metadata.GetString(method.Name);
                    TypeDefinition type = metadata.GetTypeDefinition(method.GetDeclaringType());
                    owner = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
                }
                else if (handle.Kind == HandleKind.MemberReference)
                {
                    MemberReference member = metadata.GetMemberReference((MemberReferenceHandle)handle);
                    name = metadata.GetString(member.Name);
                    if (member.Parent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                        owner = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
                    }
                }
                if (name == targetName && (targetType == null || owner == targetType)) matches++;
            }
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: position++; break;
                case OperandType.InlineVar: position += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: position += 8; break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(bytes, position); position += 4 + count * 4; break;
                default: position += 4; break;
            }
        }
        return matches;
    }
    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) throw new Exception("Missing native method declaration: " + signature);
        int open = source.IndexOf('{', start), depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
        }
        throw new Exception("Unterminated native method: " + signature);
    }
    private static int? IntegerConstant(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldc_I4 || instruction.opcode == OpCodes.Ldc_I4_S)
            return Convert.ToInt32(instruction.operand);
        OpCode[] small = { OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2,
            OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6,
            OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8 };
        for (int i = 0; i < small.Length; i++) if (instruction.opcode == small[i]) return i;
        return null;
    }
    private static MethodBase[] Targets(string patch)
    { return ((IEnumerable<MethodBase>)mod.GetType("ControllerSupport."+patch,true).GetMethod("TargetMethods",All).Invoke(null,null)).ToArray(); }
    private static int Named(List<CodeInstruction> code,string name,string type,string method)
    { int count=0;for(int i=1;i<code.Count;i++)if(code[i-1].opcode==OpCodes.Ldstr&&Equals(code[i-1].operand,name)&&Call(code[i],type,method))count++;return count; }
    private static List<CodeInstruction> Read(MethodInfo method)
    {
        byte[] bytes=method.GetMethodBody().GetILAsByteArray();var result=new List<CodeInstruction>();
        for(int p=0;p<bytes.Length;)
        {
            byte first=bytes[p++]; short key=first==0xFE?(short)(0xFE00|bytes[p++]):first; OpCode op=Opcodes[key];object value=null;
            switch(op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: value=p+1+(sbyte)bytes[p];p++;break;
                case OperandType.ShortInlineI: value=(sbyte)bytes[p++];break;
                case OperandType.ShortInlineVar: value=bytes[p++];break;
                case OperandType.InlineVar:value=BitConverter.ToUInt16(bytes,p);p+=2;break;
                case OperandType.InlineBrTarget:value=p+4+BitConverter.ToInt32(bytes,p);p+=4;break;
                case OperandType.InlineI:value=BitConverter.ToInt32(bytes,p);p+=4;break;
                case OperandType.ShortInlineR:value=BitConverter.ToSingle(bytes,p);p+=4;break;
                case OperandType.InlineField:value=method.Module.ResolveField(BitConverter.ToInt32(bytes,p),method.DeclaringType.GetGenericArguments(),method.GetGenericArguments());p+=4;break;
                case OperandType.InlineMethod:value=method.Module.ResolveMethod(BitConverter.ToInt32(bytes,p),method.DeclaringType.GetGenericArguments(),method.GetGenericArguments());p+=4;break;
                case OperandType.InlineString:value=method.Module.ResolveString(BitConverter.ToInt32(bytes,p));p+=4;break;
                case OperandType.InlineType:case OperandType.InlineTok:case OperandType.InlineSig:value=BitConverter.ToInt32(bytes,p);p+=4;break;
                case OperandType.InlineI8:value=BitConverter.ToInt64(bytes,p);p+=8;break;
                case OperandType.InlineR:value=BitConverter.ToDouble(bytes,p);p+=8;break;
                case OperandType.InlineSwitch:int n=BitConverter.ToInt32(bytes,p);p+=4+n*4;break;
                default:throw new Exception("Unsupported native operand "+op.OperandType);
            }
            result.Add(new CodeInstruction(op,value));
        }
        return result;
    }
}
