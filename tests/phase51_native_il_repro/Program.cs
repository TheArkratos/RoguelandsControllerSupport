using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
        foreach(string name in new[]{"AimAssistFactoryPatch","AimAssistIteratorPatch","AimAssistDirectPatch","AimAssistStaffSpawnPatch","AimAssistSkillPatch","AimAssistAtalantaPatch"})
        {
            Type patch=inspected.GetType("ControllerSupport."+name,true);
            Check(exported.Contains(patch),"GadgetCore loader discovers exported aim-assist patch "+name);
            CustomAttributeData gadget=patch.GetCustomAttributesData().SingleOrDefault(a=>a.AttributeType.FullName=="GadgetCore.API.HarmonyGadgetAttribute");
            Check(gadget!=null&&gadget.ConstructorArguments.Count>=1&&(string)gadget.ConstructorArguments[0].Value=="Controller Support","Exported patch belongs to exact Controller Support gadget: "+name);
            Check(patch.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"),"Exported patch retains Harmony patch declaration: "+name);
        }
        ((IDisposable)context).Dispose();
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
        Check(updateNative.Count(i=>Call(i,"GameScript","UseSkill"))==6&&updatePatched.Count(i=>Call(i,"GameScript","UseSkill"))==6,"six native chip actions retained");
        Check(updateNative.Count(i=>Call(i,"GameScript","UseItem"))==6&&updatePatched.Count(i=>Call(i,"GameScript","UseItem"))==6,"six native item actions retained");
        Console.WriteLine("Phase 5.1 installed native IL: "+checks+" checks passed.");
    }
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
