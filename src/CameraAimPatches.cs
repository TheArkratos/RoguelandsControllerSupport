using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Keep the vanilla camera averaging and clamping; only substitute its
    // cursor source when controller aim owns the cursor.
    [HarmonyPatch(typeof(CameraScript), "GetAvgX")]
    [HarmonyGadget("Controller Support")]
    public static class CameraAvgXPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return CameraAimPatchCore.ReplaceMouseRead(instructions, "GetAvgX");
        }
    }

    [HarmonyPatch(typeof(CameraScript), "GetAvgy")]
    [HarmonyGadget("Controller Support")]
    public static class CameraAvgYPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return CameraAimPatchCore.ReplaceMouseRead(instructions, "GetAvgy");
        }
    }

    internal static class CameraAimPatchCore
    {
        internal static IEnumerable<CodeInstruction> ReplaceMouseRead(
            IEnumerable<CodeInstruction> instructions, string methodName)
        {
            List<CodeInstruction> original = new List<CodeInstruction>(instructions);
            MethodInfo getter = typeof(Input).GetProperty("mousePosition",
                BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            MethodInfo replacement = typeof(ControllerAim).GetMethod("AttackMousePosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (getter == null || replacement == null) return original;

            int readIndex = -1;
            int reads = 0;
            for (int i = 0; i < original.Count; i++)
            {
                if ((original[i].opcode == OpCodes.Call || original[i].opcode == OpCodes.Callvirt) &&
                    Equals(original[i].operand, getter))
                {
                    readIndex = i;
                    reads++;
                }
            }
            if (reads != 1)
            {
                Debug.LogWarning("Controller Support: CameraScript." + methodName +
                    " expected one mouse-position read, found " + reads +
                    "; leaving vanilla camera intact.");
                return original;
            }

            List<CodeInstruction> patched = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction instruction in original)
                patched.Add(new CodeInstruction(instruction));
            patched[readIndex].operand = replacement;
            Debug.Log("Controller Support: patched camera cursor read in CameraScript." + methodName + ".");
            return patched;
        }
    }
}
