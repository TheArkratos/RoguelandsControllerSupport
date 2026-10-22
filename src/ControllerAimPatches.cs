using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    [HarmonyPatch(typeof(ActionCursor), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class ActionCursorAimPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ActionCursor __instance)
        {
            return ControllerVisibleCursor.Prefix(__instance);
        }
    }

    [HarmonyPatch(typeof(ControllerCursor), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class ControllerCursorAimPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ControllerCursor __instance)
        {
            Vector3 point;
            if (!ControllerAim.TryWorldPoint(out point)) return true;
            __instance.transform.position = new Vector3(point.x, point.y,
                __instance.transform.position.z);
            ControllerCursor.staticX = point.x;
            ControllerCursor.staticY = point.y;
            return false;
        }
    }

    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class PlayerAttackAimPatch
    {
        private const int ExpectedMouseReads = 19;

        [HarmonyPrefix]
        public static void Prefix()
        {
            // One vanilla Attack branch uses ControllerCursor.staticX/Y instead
            // of the mouse. Refresh it before each coroutine step as well.
            Vector3 point;
            if (!ControllerAim.TryWorldPoint(out point)) return;
            ControllerCursor.staticX = point.x;
            ControllerCursor.staticY = point.y;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // Exact compiled iterator in the verified native Assembly-CSharp.
            Type iterator = typeof(PlayerScript).GetNestedType(
                "<Attack>c__Iterator31", BindingFlags.NonPublic);
            MethodInfo method = iterator == null ? null : iterator.GetMethod("MoveNext",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                Debug.LogWarning("Controller Support: Attack iterator missing; leaving vanilla aim intact.");
            return method;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> original = new List<CodeInstruction>(instructions);
            MethodInfo getter = typeof(Input).GetProperty("mousePosition",
                BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            MethodInfo replacement = typeof(ControllerAim).GetMethod("AttackMousePosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (getter == null || replacement == null) return original;

            List<int> reads = new List<int>();
            for (int i = 0; i < original.Count; i++)
                if ((original[i].opcode == OpCodes.Call || original[i].opcode == OpCodes.Callvirt) &&
                    Equals(original[i].operand, getter)) reads.Add(i);
            if (reads.Count != ExpectedMouseReads)
            {
                Debug.LogWarning("Controller Support: Attack iterator expected " + ExpectedMouseReads +
                    " mouse-position reads, found " + reads.Count + "; leaving vanilla aim intact.");
                return original;
            }

            List<CodeInstruction> patched = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction instruction in original) patched.Add(new CodeInstruction(instruction));
            foreach (int index in reads) patched[index].operand = replacement;
            Debug.Log("Controller Support: patched all " + reads.Count +
                " mouse-position reads in PlayerScript.Attack iterator.");
            return patched;
        }
    }
}
