using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace UnityEngine
{
    public static class Input
    {
        public static bool GetButton(string name) { return false; }
        public static bool GetButtonDown(string name) { return false; }
        public static bool GetButtonUp(string name) { return false; }
        public static float GetAxis(string name) { return 0; }
    }
    public static class Debug
    {
        public static void Log(string value) { }
        public static void LogWarning(string value) { }
    }
}

namespace GadgetCore.API
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyGadgetAttribute : Attribute
    {
        public HarmonyGadgetAttribute(string name) { }
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatchAttribute : Attribute
    {
        public HarmonyPatchAttribute(Type type, string method) { }
    }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyFinalizerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefixAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfixAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyTranspilerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPriorityAttribute : Attribute
    {
        public readonly int Value;
        public HarmonyPriorityAttribute(int value) { Value = value; }
    }
    public static class Priority { public const int First = 800; }
    public sealed class CodeInstruction
    {
        public OpCode opcode;
        public object operand;
        public List<object> labels = new List<object>();
        public List<object> blocks = new List<object>();
        public CodeInstruction(OpCode opcode, object operand = null)
        { this.opcode = opcode; this.operand = operand; }
        public CodeInstruction(CodeInstruction source)
        {
            opcode = source.opcode; operand = source.operand;
            labels.AddRange(source.labels); blocks.AddRange(source.blocks);
        }
    }
}

public sealed class Item { public int id; }
public sealed class GameScript
{
    public static bool inventoryOpen;
    private Item holdingItem = new Item();
    public bool StorageOpen;
    public int LeaveCalls, DropCalls, ReturnCalls, ChestDropCalls;
    public bool ReturnAllowed, DropAllowed = true;
    public void SetHeld(int id) { holdingItem.id = id; }
    public bool HasHeld { get { return holdingItem.id != 0; } }
    public void LeaveNPC()
    {
        LeaveCalls++;
        if (HasHeld) { DropCalls++; holdingItem.id = 0; }
        StorageOpen = false;
    }
    public void CloseAllMenus() { }
    public void PauseMenu() { }
}
public sealed class PlayerScript { public void Update() { } }
public sealed class ActionCursor { public void Update() { } }

namespace ControllerSupport
{
    internal static class ControllerMenuSelection
    {
        internal static void Reject(GameScript game, string reason) { Rejections++; }
        internal static int Rejections;
        internal static void NativeError(GameScript game, int code) { }
        internal static void Prepare(GameScript game) { }
        internal static void Update(GameScript game) { }
        internal static void EndNativeUpdate() { }
        internal static void PlaceHeldItemCursor(ActionCursor cursor) { }
        internal static bool ClosingStorageWouldDropHeldItem(GameScript game)
        { return game.StorageOpen && game.HasHeld; }
    }
    internal static class ControllerExploration
    {
        internal static void UpdateAndDispatch(GameScript game) { }
    }
    internal static class InventoryItemLifecycle
    {
        internal static int LeaveRangeCalls;
        internal static bool LeaveRange(GameScript game, out string reason)
        {
            LeaveRangeCalls++; reason=null;
            if (!game.StorageOpen || !game.HasHeld) return true;
            if (game.ReturnAllowed) { game.SetHeld(0); game.ReturnCalls++; return true; }
            if (game.DropAllowed) { game.SetHeld(0); game.ChestDropCalls++; return true; }
            reason="Drop failed"; return false;
        }
    }
    internal static class ControllerGameplayInput
    {
        internal static bool GamePausePressed() { return false; }
        internal static void SetGameUpdateContext(GameScript game) { }
        internal static bool PlayerButton(string name) { return false; }
        internal static bool PlayerButtonDown(string name) { return false; }
        internal static bool PlayerInteractionButtonDown(string name, PlayerScript player) { return false; }
        internal static bool PlayerButtonUp(string name) { return false; }
        internal static float PlayerAxis(string name) { return 0; }
        internal static bool GameButtonDown(string name) { return false; }
        internal static bool GameDroidButtonDown(string name, GameScript game) { return false; }
    }
}
