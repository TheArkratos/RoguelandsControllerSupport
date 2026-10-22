using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Match the exact named input reads in the verified native game. On an
    // unknown IL layout, return every original instruction unchanged.
    internal static class GameplayInputPatches
    {
        private sealed class Read
        {
            internal readonly string InputMethod;
            internal readonly string Name;
            internal readonly string AdapterMethod;
            internal readonly int Count;

            internal Read(string inputMethod, string name, string adapterMethod, int count)
            {
                InputMethod = inputMethod;
                Name = name;
                AdapterMethod = adapterMethod;
                Count = count;
            }
        }

        private static readonly Read[] PlayerReads = {
            new Read("GetButton", "Left", "PlayerButton", 3),
            new Read("GetButton", "Right", "PlayerButton", 3),
            new Read("GetButton", "Up", "PlayerButton", 1),
            new Read("GetButton", "Down", "PlayerButton", 1),
            new Read("GetButtonDown", "Left", "PlayerButtonDown", 1),
            new Read("GetButtonDown", "Right", "PlayerButtonDown", 1),
            new Read("GetButtonDown", "Down", "PlayerButtonDown", 1),
            new Read("GetButtonDown", "Up", "PlayerInteractionButtonDown", 1),
            new Read("GetButtonDown", "Dash Left", "PlayerButtonDown", 1),
            new Read("GetButtonDown", "Dash Right", "PlayerButtonDown", 1),
            new Read("GetButtonDown", "Jump", "PlayerButtonDown", 1),
            new Read("GetButtonUp", "Left", "PlayerButtonUp", 1),
            new Read("GetButtonUp", "Right", "PlayerButtonUp", 1),
            new Read("GetButtonUp", "Up", "PlayerButtonUp", 1),
            new Read("GetButtonUp", "Down", "PlayerButtonUp", 1),
            new Read("GetAxis", "Horizontal", "PlayerAxis", 4),
            new Read("GetAxis", "Attack", "PlayerAxis", 1)
        };

        private static readonly Read[] GameReads = {
            new Read("GetButtonDown", "Cancel", "GameButtonDown", 1),
            new Read("GetButtonDown", "CombatMode", "GameButtonDown", 1),
            new Read("GetButtonDown", "Inventory", "GameButtonDown", 1),
            new Read("GetButtonDown", "Droid", "GameDroidButtonDown", 1),
            new Read("GetButtonDown", "UseItem1", "GameButtonDown", 1),
            new Read("GetButtonDown", "UseItem2", "GameButtonDown", 1),
            new Read("GetButtonDown", "UseItem3", "GameButtonDown", 1),
            new Read("GetButtonDown", "UseItem4", "GameButtonDown", 1),
            new Read("GetButtonDown", "UseItem5", "GameButtonDown", 1),
            new Read("GetButtonDown", "UseItem6", "GameButtonDown", 1)
        };

        internal static IEnumerable<CodeInstruction> Player(IEnumerable<CodeInstruction> instructions)
        {
            return Replace(instructions, PlayerReads, "PlayerScript.Update");
        }

        internal static IEnumerable<CodeInstruction> Game(IEnumerable<CodeInstruction> instructions)
        {
            return Replace(instructions, GameReads, "GameScript.Update");
        }

        private static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions,
            Read[] reads, string target)
        {
            List<CodeInstruction> original = new List<CodeInstruction>(instructions);
            List<int>[] matches = new List<int>[reads.Length];
            MethodInfo[] inputMethods = new MethodInfo[reads.Length];
            MethodInfo[] adapterMethods = new MethodInfo[reads.Length];
            for (int i = 0; i < reads.Length; i++)
            {
                matches[i] = new List<int>();
                inputMethods[i] = typeof(Input).GetMethod(reads[i].InputMethod, new[] { typeof(string) });
                adapterMethods[i] = typeof(ControllerGameplayInput).GetMethod(reads[i].AdapterMethod,
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (inputMethods[i] == null || adapterMethods[i] == null)
                {
                    Debug.LogWarning("Controller Support: " + target + " input method lookup failed; leaving vanilla IL intact.");
                    return original;
                }
            }

            for (int position = 1; position < original.Count; position++)
            {
                CodeInstruction call = original[position];
                CodeInstruction argument = original[position - 1];
                if (argument.opcode != OpCodes.Ldstr || call.opcode != OpCodes.Call) continue;
                for (int i = 0; i < reads.Length; i++)
                {
                    if (Equals(argument.operand, reads[i].Name) && Equals(call.operand, inputMethods[i]))
                        matches[i].Add(position);
                }
            }

            int total = 0;
            for (int i = 0; i < reads.Length; i++)
            {
                if (matches[i].Count != reads[i].Count)
                {
                    Debug.LogWarning("Controller Support: " + target + " expected " + reads[i].Count +
                        " " + reads[i].InputMethod + "(" + reads[i].Name + ") reads, found " +
                        matches[i].Count + "; leaving vanilla IL intact.");
                    return original;
                }
                total += matches[i].Count;
            }

            // Clone only after all counts pass. Preserve any labels and blocks
            // attached to the existing instruction at each call site.
            List<CodeInstruction> patched = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction instruction in original) patched.Add(new CodeInstruction(instruction));
            for (int i = 0; i < reads.Length; i++)
                foreach (int position in matches[i])
                    patched[position].operand = adapterMethods[i];
            // The Up edge needs the actual PlayerScript instance to avoid
            // responding on remote players and to inspect canInteract.
            int upIndex = Array.FindIndex(reads, r => r.Name == "Up" &&
                r.InputMethod == "GetButtonDown");
            if (target == "PlayerScript.Update" && upIndex >= 0)
                InsertInstanceArgument(patched, matches[upIndex][0]);
            if (target == "GameScript.Update")
            {
                int droidIndex = Array.FindIndex(reads, r => r.Name == "Droid" &&
                    r.InputMethod == "GetButtonDown");
                if (droidIndex >= 0)
                    InsertInstanceArgument(patched, matches[droidIndex][0]);
            }
            Debug.Log("Controller Support: patched " + total + " input reads in " + target + ".");
            return patched;
        }

        private static void InsertInstanceArgument(List<CodeInstruction> patched, int callIndex)
        {
            CodeInstruction call = patched[callIndex];
            CodeInstruction instance = new CodeInstruction(OpCodes.Ldarg_0);
            instance.labels.AddRange(call.labels);
            call.labels.Clear();
            instance.blocks.AddRange(call.blocks);
            call.blocks.Clear();
            patched.Insert(callIndex, instance);
        }
    }

    [HarmonyPatch(typeof(PlayerScript), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class PlayerGameplayInputPatch
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return GameplayInputPatches.Player(instructions);
        }
    }

    [HarmonyPatch(typeof(GameScript), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class GameGameplayInputPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(GameScript __instance)
        {
            ControllerDialogueInput.Prepare(__instance);
            ControllerGameplayInput.SetGameUpdateContext(__instance);
            ControllerMenuSelection.Prepare(__instance);
            ControllerGameplayActions.Prepare(__instance);
            ControllerExploration.UpdateAndDispatch(__instance);
            if (!ControllerGameplayInput.GamePausePressed()) return;
            if (GameScript.inventoryOpen) __instance.CloseAllMenus();
            __instance.PauseMenu();
        }

        [HarmonyPostfix]
        internal static void Postfix(GameScript __instance)
        {
            ControllerMenuSelection.Update(__instance);
            ControllerMenuSelection.EndNativeUpdate();
        }

        [HarmonyFinalizer]
        internal static Exception Finalizer(Exception __exception)
        {
            ControllerMenuSelection.EndNativeUpdate();
            return __exception;
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return GameplayInputPatches.Game(instructions);
        }
    }

    [HarmonyPatch(typeof(GameScript), "Error")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryNativeErrorPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(GameScript __instance, int __0)
        {
            ControllerMenuSelection.NativeError(__instance, __0);
        }
    }

    // Walking out of an NPC trigger closes its inventory UI independently of
    // controller buttons. Resolve the held item before native close handlers:
    // safely return it, otherwise drop at the remembered chest/console position.
    [HarmonyPatch(typeof(GameScript), "LeaveNPC")]
    [HarmonyGadget("Controller Support")]
    public static class StorageHeldItemLeaveNpcPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(GameScript __instance)
        {
            string reason;
            if (InventoryItemLifecycle.LeaveRange(__instance, out reason)) return true;
            ControllerMenuSelection.Reject(__instance, reason);
            return false;
        }
    }

    [HarmonyPatch(typeof(ActionCursor), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class StorageHeldItemCursorPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ActionCursor __instance)
        {
            ControllerMenuSelection.PlaceHeldItemCursor(__instance);
        }
    }
}
