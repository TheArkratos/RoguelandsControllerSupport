using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // These delayed attack steps run outside Attack's iterator. Keep their
    // mouse reads and the vanilla controller-cursor branch on one aim owner.
    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class HelperAttackAimPatch
    {
        private static readonly string[] IteratorNames = {
            "<Repeater>c__Iterator13",
            "<Wrath>c__Iterator19",
            "<Cerberus>c__Iterator1F",
            "<SiriusS>c__Iterator35"
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in IteratorNames)
            {
                Type iterator = typeof(PlayerScript).GetNestedType(name, BindingFlags.NonPublic);
                MethodInfo method = iterator == null ? null : iterator.GetMethod("MoveNext",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogWarning("Controller Support: " + name +
                        " iterator missing; leaving vanilla aim intact.");
                    continue;
                }
                yield return method;
            }
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            Vector3 point;
            if (!ControllerAim.TryWorldPoint(out point)) return;
            ControllerCursor.staticX = point.x;
            ControllerCursor.staticY = point.y;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> source = new List<CodeInstruction>(instructions);
            MethodInfo getter = typeof(Input).GetProperty("mousePosition",
                BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            MethodInfo replacement = typeof(ControllerAim).GetMethod("AttackMousePosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (getter == null || replacement == null)
            {
                Debug.LogWarning("Controller Support: helper aim methods missing for " + original +
                    "; leaving vanilla aim intact.");
                return source;
            }

            int read = -1;
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if ((source[i].opcode == OpCodes.Call || source[i].opcode == OpCodes.Callvirt) &&
                    Equals(source[i].operand, getter))
                {
                    read = i;
                    count++;
                }
            }
            if (count != 1)
            {
                Debug.LogWarning("Controller Support: " + original +
                    " expected one mouse-position read, found " + count +
                    "; leaving vanilla aim intact.");
                return source;
            }

            List<CodeInstruction> patched = new List<CodeInstruction>(source.Count);
            foreach (CodeInstruction instruction in source)
                patched.Add(new CodeInstruction(instruction));
            patched[read].operand = replacement;
            return patched;
        }
    }
}
