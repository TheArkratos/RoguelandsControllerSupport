using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using System.Reflection;
using GadgetCore.API;
using HarmonyLib;

namespace ControllerSupport
{
    // GadgetCore remains responsible for serialization, host IDs and RPCs. This
    // only observes each real network object within our synchronous drop scope.
    [HarmonyPatch(typeof(GadgetCoreAPI), "DropItem", new[] { typeof(Vector3), typeof(Item), typeof(bool) })]
    [HarmonyGadget("Controller Support")]
    public static class InventoryDropCapturePatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            InventoryItemLifecycle.SetDropCaptureReady(false);
            var code = new List<CodeInstruction>();
            foreach (CodeInstruction instruction in instructions) code.Add(new CodeInstruction(instruction));
            MethodInfo instantiate = AccessTools.Method(typeof(Network), "Instantiate",
                new[] { typeof(UnityEngine.Object), typeof(Vector3), typeof(Quaternion), typeof(int) });
            int matches = 0;
            foreach (CodeInstruction instruction in code)
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, instantiate)) matches++;
            // The installed overload has exactly two branches, one for items and
            // one for chips. Fail closed if an updated API differs from that shape.
            if (instantiate == null || matches != 2) return code;
            var output = new List<CodeInstruction>();
            MethodInfo track = AccessTools.Method(typeof(InventoryItemLifecycle), "TrackSpawn",
                new[] { typeof(UnityEngine.Object) });
            foreach (CodeInstruction instruction in code)
            {
                output.Add(instruction);
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, instantiate))
                    output.Add(new CodeInstruction(OpCodes.Call, track));
            }
            InventoryItemLifecycle.SetDropCaptureReady(true);
            return output;
        }
    }

    // Exceptional quarantine covers physical mouse/keyboard paths as well as
    // controller routing. Native inventory behavior is unchanged outside it.
    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class InventoryMutationGuardPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(GameScript).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string name = method.Name;
                bool mutation = name.StartsWith("SelectItem", StringComparison.Ordinal)
                    || name.StartsWith("SplitItem", StringComparison.Ordinal)
                    || name.StartsWith("SwapItem", StringComparison.Ordinal)
                    || name.StartsWith("PlaceItem", StringComparison.Ordinal)
                    || name.StartsWith("PlaceOneItem", StringComparison.Ordinal)
                    || name.StartsWith("CombineItem", StringComparison.Ordinal)
                    || name.StartsWith("ShiftClick", StringComparison.Ordinal)
                    || name == "ForgeEmblem"
                    || name == "TrashItem"
                    || name == "PrismItem";
                ParameterInfo[] parameters = method.GetParameters();
                if (mutation && parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                    yield return method;
                else if ((name == "DropItem" || name == "CloseAllMenus") && parameters.Length == 0)
                    yield return method;
            }
            // Older Unity/Mono iterators do not carry IteratorStateMachineAttribute.
            // Guard their MoveNext too, including a coroutine queued before a drop.
            foreach (Type nested in typeof(GameScript).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (nested.Name.IndexOf("ShiftClick", StringComparison.Ordinal) < 0
                    || !typeof(IEnumerator).IsAssignableFrom(nested)) continue;
                MethodInfo move = nested.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (move != null && move.ReturnType == typeof(bool)) yield return move;
            }
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First + 1000)]
        private static bool Prefix(object __instance)
        {
            GameScript game = __instance as GameScript;
            if (game == null && __instance != null)
                foreach (FieldInfo field in __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(GameScript)) { game = field.GetValue(__instance) as GameScript; break; }
            return !InventoryItemLifecycle.MutationsBlocked(game);
        }
    }

    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class InventorySourcePatch
    {
        internal sealed class Capture
        {
            internal Item Before;
            internal int Quantity;
            internal int Page;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "SelectItem", "SplitItem", "SwapItem",
                "SelectItemStorage", "SplitItemStorage", "SwapItemStorage" })
                yield return AccessTools.Method(typeof(GameScript), name, new[] { typeof(int) });
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First + 100)]
        private static void Prefix(GameScript __instance, out Capture __state)
        {
            InventoryItemLifecycle.ObserveContext(__instance);
            Item held = InventoryItemLifecycle.Held(__instance);
            __state = new Capture { Before = held, Quantity = held == null ? 0 : held.q,
                Page = InventoryItemLifecycle.CurrentPage(__instance) };
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last - 100)]
        private static void Postfix(GameScript __instance, int __0, MethodBase __originalMethod, Capture __state)
        {
            // An earlier quarantine prefix can skip both the action and our
            // out-state prefix; Harmony still invokes this postfix afterward.
            if (__state == null) return;
            Item held = InventoryItemLifecycle.Held(__instance);
            if (held == null || held.id == 0 || held.q <= 0) return;
            if (!ReferenceEquals(held, __state.Before) || held.q != __state.Quantity)
                InventoryItemLifecycle.RecordSource(__instance, __0, __state.Page,
                    __originalMethod.Name.EndsWith("Storage"));
        }

        [HarmonyFinalizer]
        private static System.Exception Finalizer(GameScript __instance, int __0,
            MethodBase __originalMethod, Capture __state, System.Exception __exception)
        {
            // A native action can change the held item before a cosmetic callback
            // throws. Preserve that item's provenance without swallowing the error.
            if (__exception != null && __state != null)
                Postfix(__instance, __0, __originalMethod, __state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GameScript), "Storage")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryChestContextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameScript __instance) { InventoryItemLifecycle.ObserveContext(__instance); }
    }

    [HarmonyPatch(typeof(GameScript), "Planets")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryPortalContextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameScript __instance) { InventoryItemLifecycle.ObserveContext(__instance); }
    }

    [HarmonyPatch(typeof(GameScript), "RefreshHoldingSlot")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryHeldEmptyPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameScript __instance) { InventoryItemLifecycle.ClearIfEmpty(__instance); }
    }
}

namespace ControllerSupport
{
    [HarmonyPatch(typeof(GameScript), "EmblemForge")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryEmblemContextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameScript __instance)
        { InventoryItemLifecycle.ObserveContext(__instance); }
    }
}

namespace ControllerSupport
{
    [HarmonyPatch(typeof(GameScript), "Trash")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryTrashContextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameScript __instance)
        { InventoryItemLifecycle.ObserveContext(__instance); }
    }
}

namespace ControllerSupport
{
    [HarmonyPatch(typeof(GameScript), "Prism")]
    [HarmonyGadget("Controller Support")]
    public static class InventoryPrismContextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameScript __instance)
        { InventoryItemLifecycle.ObserveContext(__instance); }
    }
}
