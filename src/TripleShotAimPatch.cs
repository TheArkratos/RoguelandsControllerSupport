using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // The first bullet is fired by Attack; the two delayed bullets are fired
    // by this separate coroutine and otherwise read the physical mouse.
    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class TripleShotAimPatch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            Type iterator = typeof(PlayerScript).GetNestedType(
                "<TripleShot>c__Iterator30", BindingFlags.NonPublic);
            MethodInfo method = iterator == null ? null : iterator.GetMethod("MoveNext",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                Debug.LogWarning("Controller Support: TripleShot iterator missing; leaving vanilla aim intact.");
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
            if (reads.Count != 2)
            {
                Debug.LogWarning("Controller Support: TripleShot iterator expected two mouse-position reads, found " +
                    reads.Count + "; leaving vanilla aim intact.");
                return original;
            }

            List<CodeInstruction> patched = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction instruction in original) patched.Add(new CodeInstruction(instruction));
            foreach (int index in reads) patched[index].operand = replacement;
            return patched;
        }
    }
}
