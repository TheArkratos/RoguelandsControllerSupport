using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Additional weapon/effect helpers that sample the physical mouse rather
    // than the main Attack iterator's aim point. Counts are from the verified
    // native Linux assembly; a changed method is left wholly unmodified.
    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class MouseOnlyHelperAimPatch
    {
        private sealed class Target
        {
            internal readonly string Name;
            internal readonly string Iterator;
            internal readonly int MouseReads;

            internal Target(string name, string iterator, int mouseReads)
            {
                Name = name;
                Iterator = iterator;
                MouseReads = mouseReads;
            }
        }

        private static readonly Target[] Targets = {
            new Target("SwordEffects", "<SwordEffects>c__Iterator2A", 2),
            new Target("LanceEffects", "<LanceEffects>c__Iterator2F", 4),
            new Target("SchegI", "<SchegI>c__Iterator32", 3),
            new Target("Devilsbane", "<Devilsbane>c__Iterator2B", 2),
            new Target("LostVoice", "<LostVoice>c__Iterator2C", 2),
            new Target("CasterSword", "<CasterSword>c__Iterator33", 2),
            new Target("AirSplit", "<AirSplit>c__Iterator34", 3),
            new Target("Savage2", "<Savage2>c__Iterator23", 1),
            new Target("Trigoddess", null, 1),
            new Target("Fractured", null, 1),
            new Target("MageMash", null, 1),
            new Target("Excalibur", null, 1),
            new Target("ColdsnapP", null, 1),
            new Target("ValflameProj", null, 1),
            new Target("Doom", null, 1),
            new Target("KingLance", null, 1),
            new Target("Ringabolt", null, 2),
            new Target("Atma", null, 1),
            new Target("Stormbringer", null, 1),
            new Target("Shock", null, 1),
            new Target("Messenger", null, 2),
            new Target("Inferno", null, 1),
            new Target("Blaze", null, 1),
            new Target("HyperBeam", null, 1),
            new Target("TurretS", null, 2),
            new Target("TurretF", null, 2),
            new Target("Turret", null, 2),
            new Target("Copter", null, 2),
            new Target("Plasma", null, 1)
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Target target in Targets)
            {
                Type owner = target.Iterator == null ? typeof(PlayerScript) :
                    typeof(PlayerScript).GetNestedType(target.Iterator, BindingFlags.NonPublic);
                MethodInfo method = owner == null ? null : owner.GetMethod(
                    target.Iterator == null ? target.Name : "MoveNext",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogWarning("Controller Support: mouse-only helper " + target.Name +
                        " missing; leaving vanilla aim intact.");
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
            Target target = FindTarget(original);
            MethodInfo getter = typeof(Input).GetProperty("mousePosition",
                BindingFlags.Public | BindingFlags.Static).GetGetMethod();
            MethodInfo replacement = original != null && original.DeclaringType == typeof(PlayerScript) && original.Name == "ValflameProj"
                ? typeof(AimAssistValflamePatch).GetMethod("MousePosition", BindingFlags.Static | BindingFlags.NonPublic)
                : typeof(ControllerAim).GetMethod("AttackMousePosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (target == null || getter == null || replacement == null)
            {
                Debug.LogWarning("Controller Support: mouse-only helper aim target/methods missing for " +
                    original + "; leaving vanilla aim intact.");
                return source;
            }

            List<int> reads = new List<int>();
            for (int i = 0; i < source.Count; i++)
                if ((source[i].opcode == OpCodes.Call || source[i].opcode == OpCodes.Callvirt) &&
                    Equals(source[i].operand, getter)) reads.Add(i);
            if (reads.Count != target.MouseReads)
            {
                Debug.LogWarning("Controller Support: " + original + " expected " +
                    target.MouseReads + " mouse-position reads, found " + reads.Count +
                    "; leaving vanilla aim intact.");
                return source;
            }

            List<CodeInstruction> patched = new List<CodeInstruction>(source.Count);
            foreach (CodeInstruction instruction in source)
                patched.Add(new CodeInstruction(instruction));
            foreach (int index in reads) patched[index].operand = replacement;
            return patched;
        }

        private static Target FindTarget(MethodBase method)
        {
            if (method == null || method.DeclaringType == null) return null;
            foreach (Target target in Targets)
            {
                if (target.Iterator == null)
                {
                    if (method.DeclaringType == typeof(PlayerScript) && method.Name == target.Name)
                        return target;
                }
                else if (method.Name == "MoveNext" && method.DeclaringType.DeclaringType == typeof(PlayerScript) &&
                    method.DeclaringType.Name == target.Iterator)
                    return target;
            }
            return null;
        }
    }
}
