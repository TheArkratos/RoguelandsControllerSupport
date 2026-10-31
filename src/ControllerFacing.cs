using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Exploration facing retains gameplay left-stick direction through menus.
    // Combat facing keeps the white-crosshair arbitration and sword-swing lock.
    internal static class ControllerFacing
    {
        internal static float CursorX()
        {
            float x;
            return ControllerLookDirection.TryExplorationX(out x) || ControllerAim.TryWalkingFacingX(out x)
                ? x : ControllerCursor.staticX;
        }

        internal static Vector3 MousePosition()
        {
            float x;
            Camera camera = Camera.main;
            if (camera != null && (ControllerLookDirection.TryExplorationX(out x) || ControllerAim.TryWalkingFacingX(out x)))
                return camera.WorldToScreenPoint(new Vector3(x, MenuScript.player.transform.position.y, 0f));
            return Input.mousePosition;
        }
    }

    [HarmonyPatch(typeof(PlayerScript), "Update")]
    [HarmonyGadget("Controller Support")]
    public static class ControllerFacingPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(PlayerScript __instance)
        {
            // Native facing reads are skipped while dead/paused; still observe
            // local lifecycle changes so an old body cannot retain stale intent.
            if (MenuScript.player != null && MenuScript.player.GetComponent<PlayerScript>() == __instance)
                ControllerLookDirection.Observe();
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            MethodInfo mouse = typeof(Input).GetProperty("mousePosition").GetGetMethod();
            FieldInfo cursor = typeof(ControllerCursor).GetField("staticX");
            int mice = 0, xs = 0;
            foreach (CodeInstruction code in original)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && Equals(code.operand, mouse)) mice++;
                if (code.opcode == OpCodes.Ldsfld && Equals(code.operand, cursor)) xs++;
            }
            // The verified Update has exactly one read of each, both confined
            // to native facing. Refuse unknown layouts rather than affect aim.
            if (mice != 1 || xs != 1)
            {
                Debug.LogWarning("Controller Support: player facing input pattern mismatch; leaving native IL unchanged.");
                return original;
            }
            var result = new List<CodeInstruction>(original.Count);
            foreach (CodeInstruction code in original)
            {
                var copy = new CodeInstruction(code);
                if ((copy.opcode == OpCodes.Call || copy.opcode == OpCodes.Callvirt) && Equals(copy.operand, mouse))
                {
                    copy.opcode = OpCodes.Call;
                    copy.operand = AccessTools.Method(typeof(ControllerFacing), "MousePosition");
                }
                else if (copy.opcode == OpCodes.Ldsfld && Equals(copy.operand, cursor))
                {
                    copy.opcode = OpCodes.Call;
                    copy.operand = AccessTools.Method(typeof(ControllerFacing), "CursorX");
                }
                result.Add(copy);
            }
            return result;
        }
    }
}
