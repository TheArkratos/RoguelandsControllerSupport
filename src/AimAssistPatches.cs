using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Retain the current attack when native code creates a delayed helper.
    [HarmonyPatch, HarmonyGadget("Controller Support")]
    public static class AimAssistFactoryPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(MethodInfo method in typeof(PlayerScript).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(method.ReturnType==typeof(IEnumerator)) yield return method;
        }
        [HarmonyPostfix]
        private static void Postfix(PlayerScript __instance,IEnumerator __result,MethodBase __originalMethod)
        {
            // A fresh Attack must earn its own token only after native guards;
            // it cannot inherit an outer attack if its verified IL hook is absent.
            ControllerAimAssist.Retain(__result,__originalMethod.Name!="Attack" && ControllerAimAssist.Local(__instance)
                ? ControllerAimAssist.Current : null);
        }
    }

    [HarmonyPatch, HarmonyGadget("Controller Support")]
    public static class AimAssistIteratorPatch
    {
        internal static IEnumerable<MethodBase> Targets()
        {
            foreach(Type type in typeof(PlayerScript).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))
                if(typeof(IEnumerator).IsAssignableFrom(type))
                {
                    MethodInfo move=type.GetMethod("MoveNext",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if(move!=null) yield return move;
                }
            foreach(string name in new[]{"<UpdatePos>c__Iterator3","<Boomerang>c__Iterator8"})
            {
                Type type=typeof(Projectile).GetNestedType(name,BindingFlags.NonPublic);
                MethodInfo move=type==null?null:type.GetMethod("MoveNext",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                if(move!=null) yield return move;
            }
        }
        private static IEnumerable<MethodBase> TargetMethods() { return Targets(); }
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Prefix(object __instance,out ControllerAimAssist.Attack __state)
        {
            ControllerAimAssist.Attack attack=ControllerAimAssist.Retained(__instance);
            Projectile projectile=ControllerAimAssist.IteratorProjectile(__instance);
            if(projectile)
            {
                FieldInfo local=typeof(Projectile).GetField("isController",BindingFlags.Instance|BindingFlags.NonPublic);
                attack=local!=null && (bool)local.GetValue(projectile)?ControllerAimAssist.Retained(projectile):null;
            }
            __state=ControllerAimAssist.Push(attack);
        }
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,ControllerAimAssist.Attack __state)
        { ControllerAimAssist.Restore(__state); return __exception; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { return AimAssistIL.Replace(instructions,__originalMethod); }
    }

    internal static class AimAssistIL
    {
        internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions,MethodBase method)
        {
            var code=new List<CodeInstruction>(instructions);
            bool attack=method.DeclaringType!=null && method.DeclaringType.Name=="<Attack>c__Iterator31";
            FieldInfo attacking=typeof(PlayerScript).GetField("attacking",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            int starts=0;
            for(int i=1;i<code.Count;i++)
                if(code[i].opcode==OpCodes.Stfld && Equals(code[i].operand,attacking) && code[i-1].opcode==OpCodes.Ldc_I4_1) starts++;
            if(attack && starts!=1)
            { Debug.LogWarning("Controller Support: aim assist Attack start pattern mismatch; leaving native IL unchanged."); return code; }
            FieldInfo x=typeof(ControllerCursor).GetField("staticX"), y=typeof(ControllerCursor).GetField("staticY");
            int xs=0,ys=0;
            foreach(CodeInstruction instruction in code)
            {
                if(instruction.opcode==OpCodes.Ldsfld && Equals(instruction.operand,x)) xs++;
                if(instruction.opcode==OpCodes.Ldsfld && Equals(instruction.operand,y)) ys++;
            }
            if(xs!=ys || xs>1)
            { Debug.LogWarning("Controller Support: aim assist cursor pattern mismatch in "+method+"; leaving native IL unchanged."); return code; }
            bool staff=method.DeclaringType!=null && method.DeclaringType.DeclaringType==typeof(Projectile);
            MethodInfo mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();
            int mouseReads=0;
            if(staff) foreach(CodeInstruction instruction in code) if(Equals(instruction.operand,mouse)) mouseReads++;
            if(staff && mouseReads!=1)
            { Debug.LogWarning("Controller Support: staff aim input pattern mismatch; leaving native IL unchanged."); return code; }
            var output=new List<CodeInstruction>();
            foreach(CodeInstruction instruction in code)
            {
                var copy=new CodeInstruction(instruction);
                if(copy.opcode==OpCodes.Ldsfld && Equals(copy.operand,x))
                { copy.opcode=OpCodes.Call; copy.operand=AccessTools.Method(typeof(ControllerAimAssist),"CursorX"); }
                if(copy.opcode==OpCodes.Ldsfld && Equals(copy.operand,y))
                { copy.opcode=OpCodes.Call; copy.operand=AccessTools.Method(typeof(ControllerAimAssist),"CursorY"); }
                if(staff && Equals(copy.operand,mouse)) copy.operand=AccessTools.Method(typeof(ControllerAimAssist),"StaffMousePosition");
                output.Add(copy);
                if(attack && instruction.opcode==OpCodes.Stfld && Equals(instruction.operand,attacking))
                {
                    int at=code.IndexOf(instruction);
                    if(at>0 && code[at-1].opcode==OpCodes.Ldc_I4_1)
                    {
                        output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                        output.Add(new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ControllerAimAssist),"BeginAttack")));
                    }
                }
            }
            return output;
        }
    }

    // Synchronous native helpers inherit the active attack/skill. A remote
    // PlayerScript must never inherit the local player's scope through an RPC.
    [HarmonyPatch, HarmonyGadget("Controller Support")]
    public static class AimAssistDirectPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(MethodBase method in MouseOnlyHelperAimPatch.TargetMethods())
                if(method.DeclaringType==typeof(PlayerScript)) yield return method;
            yield return AccessTools.Method(typeof(PlayerScript),"Staff");
        }
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Prefix(PlayerScript __instance,out ControllerAimAssist.Attack __state)
        { __state=ControllerAimAssist.Push(ControllerAimAssist.Local(__instance)?ControllerAimAssist.Current:null); }
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,ControllerAimAssist.Attack __state)
        { ControllerAimAssist.Restore(__state); return __exception; }
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { return AimAssistIL.Replace(instructions,__originalMethod); }
    }

    [HarmonyPatch(typeof(PlayerScript),"Staff"),HarmonyGadget("Controller Support")]
    public static class AimAssistStaffSpawnPatch
    {
        private static PlayerScript spawningPlayer;
        [HarmonyPrefix]
        private static void Prefix(PlayerScript __instance,NetworkPlayer pp,out PlayerScript __state)
        { __state=spawningPlayer; spawningPlayer=ControllerAimAssist.Local(__instance) && pp==Network.player ? __instance : null; }
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,PlayerScript __state)
        { spawningPlayer=__state; return __exception; }
        internal static UnityEngine.Object Capture(UnityEngine.Object spawned)
        {
            GameObject obj=spawned as GameObject;
            Projectile projectile=obj?obj.GetComponent<Projectile>():null;
            if(projectile) ControllerAimAssist.CaptureStaff(projectile,spawningPlayer);
            return spawned;
        }
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source=new List<CodeInstruction>(instructions);
            MethodInfo instantiate=AccessTools.Method(typeof(Network),"Instantiate",new[]{typeof(UnityEngine.Object),typeof(Vector3),typeof(Quaternion),typeof(int)});
            int count=0; foreach(CodeInstruction i in source) if(Equals(i.operand,instantiate)) count++;
            if(instantiate==null || count!=2)
            { Debug.LogWarning("Controller Support: staff spawn pattern mismatch; retained assistance disabled for unknown native layout."); return source; }
            var result=new List<CodeInstruction>();
            foreach(CodeInstruction i in source)
            {
                result.Add(new CodeInstruction(i));
                if(Equals(i.operand,instantiate)) result.Add(new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(AimAssistStaffSpawnPatch),"Capture")));
            }
            return result;
        }
    }

    // Valflame summons periodically while equipped, outside attack lifetimes.
    // Keep native spawn direction/RPCs; only choose each summon position live.
    [HarmonyPatch(typeof(PlayerScript),"ValflameProj"),HarmonyGadget("Controller Support")]
    public static class AimAssistValflamePatch
    {
        private sealed class Scope
        {
            internal PlayerScript Owner;
            internal ControllerAimAssist.Attack Placement;
            internal Vector3 Point;
            internal bool Ready;
        }
        private static Scope scope;
        [HarmonyPrefix]
        private static void Prefix(PlayerScript __instance,out object __state)
        { __state=scope; scope=new Scope { Owner=ControllerAimAssist.Local(__instance)?__instance:null }; }
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,object __state)
        { scope=__state as Scope; return __exception; }
        internal static Vector3 MousePosition()
        {
            if(scope==null) return Input.mousePosition;
            scope.Ready=ControllerAimAssist.TryValflamePoint(scope.Owner,out scope.Point,out scope.Placement);
            return scope.Ready?Camera.main.WorldToScreenPoint(scope.Point):Input.mousePosition;
        }
        internal static UnityEngine.Object Capture(UnityEngine.Object spawned)
        {
            GameObject obj=spawned as GameObject;
            Projectile projectile=obj?obj.GetComponent<Projectile>():null;
            if(projectile && scope!=null && scope.Ready)
                ControllerAimAssist.CaptureValflame(projectile,scope.Placement,scope.Point);
            return spawned;
        }
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source=new List<CodeInstruction>(instructions);
            MethodInfo instantiate=AccessTools.Method(typeof(UnityEngine.Object),"Instantiate",
                new[]{typeof(UnityEngine.Object),typeof(Vector3),typeof(Quaternion)});
            int count=0;foreach(CodeInstruction i in source) if(Equals(i.operand,instantiate)) count++;
            if(instantiate==null || count!=1)
            { Debug.LogWarning("Controller Support: Valflame spawn pattern mismatch; target marker correlation disabled."); return source; }
            var result=new List<CodeInstruction>();
            foreach(CodeInstruction i in source)
            {
                result.Add(new CodeInstruction(i));
                if(Equals(i.operand,instantiate)) result.Add(new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(AimAssistValflamePatch),"Capture")));
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(GameScript),"ActivateCombatChip"),HarmonyGadget("Controller Support")]
    public static class AimAssistSkillPatch
    {
        [HarmonyPrefix]
        private static void Prefix(int id,out ControllerAimAssist.Attack __state)
        {
            bool aimed=id==9||id==11||id==12||id==13||id==14||id==15||id==22||id==24;
            __state=ControllerAimAssist.Push(aimed?ControllerAimAssist.Acquire():null);
        }
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,ControllerAimAssist.Attack __state)
        { ControllerAimAssist.Restore(__state); return __exception; }
    }
    [HarmonyPatch(typeof(GameScript),"Atalanta"),HarmonyGadget("Controller Support")]
    public static class AimAssistAtalantaPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code=new List<CodeInstruction>(instructions);
            MethodInfo mouse=typeof(Input).GetProperty("mousePosition").GetGetMethod();
            int count=0; foreach(CodeInstruction i in code) if(Equals(i.operand,mouse)) count++;
            if(count!=2) return code;
            var result=new List<CodeInstruction>();
            foreach(CodeInstruction i in code)
            { var copy=new CodeInstruction(i); if(Equals(copy.operand,mouse)) copy.operand=AccessTools.Method(typeof(ControllerAtalantaPlacement),"MousePosition"); result.Add(copy); }
            return result;
        }
    }
}
