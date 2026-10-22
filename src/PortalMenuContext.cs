using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GadgetCore.API;
using HarmonyLib;
using UnityEngine;

namespace ControllerSupport
{
    // Eligibility and click routing only. Native game/GadgetCore own every action.
    internal static class PortalMenuContext
    {
        internal const int FirstSlot = 100;
        internal const int TargetCount = 30;
        private static readonly FieldInfo Uses = typeof(GameScript).GetField("portalUses", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Teleing = typeof(GameScript).GetField("planetTeleing", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CustomPlanets = typeof(PlanetRegistry).GetField("selectorPlanets", BindingFlags.Static | BindingFlags.NonPublic);
        private static GameScript clickGame, scopedGame;
        private static GameObject clickRoot;
        private static Collider clickTarget;
        private static int clickFrame = -1, clickSlot = -1;
        private static bool gadgetRoute, bridgeReady;
        private static Vector3 clickPoint, physicalPoint;

        internal static bool IsOpen(GameScript game)
        {
            return game != null && GameScript.inventoryOpen && !GameScript.pausing &&
                !GameScript.dead && !GameScript.buildMode && game.inventoryMain &&
                game.inventoryMain.activeInHierarchy && game.menuPlanets && game.menuPlanets.activeInHierarchy;
        }

        internal static void CollectTargets(GameScript game, Collider[] absoluteSlots)
        {
            if (absoluteSlots == null || absoluteSlots.Length < FirstSlot + TargetCount) return;
            for (int i = FirstSlot; i < FirstSlot + TargetCount; i++) absoluteSlots[i] = null;
            if (!IsOpen(game)) return;
            foreach (Collider candidate in game.menuPlanets.GetComponentsInChildren<Collider>())
            {
                if (!ValidTarget(game, candidate)) continue;
                int index = SlotFor(candidate);
                string reason;
                // Native controls stay visible when their action is unavailable
                // (already selected portal, insufficient Credits, max challenge).
                // Keep them focusable so activation can explain the rejection.
                // Planet locks/uses and loader page availability still gate cells.
                if (index >= FirstSlot && (index == 126 || index == 127 ||
                    CanActivate(game, index, out reason))) absoluteSlots[index] = candidate;
            }
        }

        private static int SlotFor(Collider candidate)
        {
            int number;
            string name = candidate.gameObject.name;
            if (candidate.gameObject.layer == 16 && candidate.gameObject.tag == "planet" &&
                int.TryParse(name, out number) && number >= 0 && number < 26) return FirstSlot + number;
            switch (name)
            {
                case "bPlanetTele": return 126;
                case "bChallenge": return 127;
                case "bPlanetPageBack": return 128;
                case "bPlanetPageForward": return 129;
                default: return -1;
            }
        }

        private static bool ValidTarget(GameScript game, Collider candidate)
        {
            if (!candidate || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                candidate.transform.parent != game.menuPlanets.transform) return false;
            Renderer renderer = candidate.GetComponent<Renderer>();
            return renderer && renderer.enabled;
        }

        private static bool PlanetAvailable(GameScript game, int number)
        {
            if (number < 0 || number >= game.planetGuard.Length) return false;
            GameObject target = game.planetGuard[number];
            if (!target || target.transform.parent != game.menuPlanets.transform || !target.activeInHierarchy) return false;
            Collider collider = target.GetComponent<Collider>();
            ButtonMenu button = target.GetComponent<ButtonMenu>();
            if (!ValidTarget(game, collider) || !button || button.isPlanet) return false;
            if (PlanetRegistry.PlanetSelectorPage > 1)
            {
                Array entries = CustomPlanets == null ? null : CustomPlanets.GetValue(null) as Array;
                int index = (PlanetRegistry.PlanetSelectorPage - 2) * 14 + number;
                PlanetInfo info = entries != null && index >= 0 && index < entries.Length ? entries.GetValue(index) as PlanetInfo : null;
                return number < 14 && info != null && (info.PortalUses > 0 || info.PortalUses == -1);
            }
            int[] uses = Uses == null ? null : Uses.GetValue(game) as int[];
            return uses != null && number < uses.Length && (uses[number] > 0 || uses[number] == -1);
        }

        internal static bool CanActivate(GameScript game, int slot, out string reason)
        {
            reason = null;
            if (!IsOpen(game)) reason = "Planet selector is closed";
            else if (GadgetCoreAPI.IsInputFrozen()) reason = "Menu input is unavailable";
            else if (slot >= 100 && slot <= 125)
            {
                if (PlanetRegistry.PlanetSelectorPage > 1 && !bridgeReady) reason = "Planet page controller bridge is unavailable";
                else if (!PlanetAvailable(game, slot - FirstSlot)) reason = "Planet is locked or has no portal uses";
            }
            else if (slot == 126)
            {
                int index = (PlanetRegistry.PlanetSelectorPage - 1) * 14 + GameScript.curPlanet;
                if (Teleing == null || (bool)Teleing.GetValue(game)) reason = "Portal is changing";
                else if (!PlanetAvailable(game, GameScript.curPlanet)) reason = "Planet is locked or has no portal uses";
                else if (GameScript.curPlanetTrue == index) reason = "Portal already selected";
            }
            else if (slot == 127)
            {
                if (GameScript.challengeLevel >= 3) reason = "Challenge level is at maximum";
                else if (game.GetCredits() < game.GetChallengeCost(GameScript.challengeLevel)) reason = "Not enough Credits";
            }
            else if (slot == 128 || slot == 129)
            {
                if (!bridgeReady || PlanetRegistry.PlanetSelectorPages <= 1) reason = "No additional planet pages";
            }
            else reason = "No planet action for this target";
            return reason == null;
        }

        internal static string ActionLabel(int slot)
        {
            if (slot >= 100 && slot <= 125) return "Choose planet";
            switch (slot)
            {
                case 126: return "Set portal";
                case 127: return "Upgrade challenge";
                case 128: return "Previous page";
                case 129: return "Next page";
                default: return null;
            }
        }

        internal static void PrepareClick(GameScript game, int slot, Collider target)
        {
            ClearClick();
            string reason;
            if (!CanActivate(game, slot, out reason) || !ValidTarget(game, target) ||
                SlotFor(target) != slot || !Camera.main || PhysicalClick()) return;
            clickGame = game; clickRoot = game.menuPlanets; clickTarget = target;
            clickFrame = Time.frameCount; clickSlot = slot;
            physicalPoint = Input.mousePosition;
            clickPoint = Camera.main.WorldToScreenPoint(target.bounds.center);
            gadgetRoute = slot >= 128 || (slot <= 125 && PlanetRegistry.PlanetSelectorPage > 1);
        }

        internal static bool NativeClickAllowed(GameScript game)
        {
            // A queued loader action must never fall through into the native ray
            // when its target disappears. Physical clicks bypass this synthetic gate.
            return clickGame != game || clickFrame != Time.frameCount || !gadgetRoute;
        }

        private static bool PhysicalClick() { return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1); }
        private static bool CurrentClick(GameScript game)
        {
            return clickGame == game && clickFrame == Time.frameCount && IsOpen(game) &&
                game.menuPlanets == clickRoot && ValidTarget(game, clickTarget) &&
                !PhysicalClick() && (Input.mousePosition - physicalPoint).sqrMagnitude <= 0.25f;
        }

        internal static void EnterGadgetCore(GameScript game) { scopedGame = game; }
        internal static void LeaveGadgetCore() { scopedGame = null; }
        internal static Vector3 GadgetMousePosition()
        {
            return scopedGame != null && CurrentClick(scopedGame) && gadgetRoute ? clickPoint : Input.mousePosition;
        }
        internal static bool GadgetMouseButtonDown(int button)
        {
            if (Input.GetMouseButtonDown(button)) return true;
            string reason;
            return button == 0 && scopedGame != null && CurrentClick(scopedGame) && gadgetRoute &&
                CanActivate(scopedGame, clickSlot, out reason);
        }

        private static void ClearClick()
        {
            clickGame = null; clickRoot = null; clickTarget = null; clickFrame = -1; clickSlot = -1; gadgetRoute = false;
        }
        internal static void Reset() { ClearClick(); LeaveGadgetCore(); }
        internal static void SetBridgeReady(bool ready) { bridgeReady = ready; }
    }

    // The loader's own postfix owns modded planet selection and page arrows.
    // Restrict virtualization to this exact method and our captured portal event.
    [HarmonyPatch]
    [HarmonyGadget("Controller Support")]
    public static class PortalGadgetCoreInputPatch
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = typeof(GadgetCoreAPI).Assembly.GetType("GadgetCore.Patches.Patch_GameScript_Update");
            MethodInfo method = type == null ? null : type.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
            if (method != null) yield return method;
            else Debug.LogWarning("Controller Support: GadgetCore planet input bridge target is missing.");
        }
        [HarmonyPrefix]
        internal static void Prefix(GameScript __0) { PortalMenuContext.EnterGadgetCore(__0); }
        [HarmonyFinalizer]
        internal static Exception Finalizer(Exception __exception)
        { PortalMenuContext.LeaveGadgetCore(); return __exception; }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> original = new List<CodeInstruction>(instructions);
            MethodInfo mouse = typeof(Input).GetProperty("mousePosition").GetGetMethod();
            MethodInfo click = typeof(Input).GetMethod("GetMouseButtonDown", new[] { typeof(int) });
            int positions = 0, clicks = 0;
            foreach (CodeInstruction instruction in original)
            {
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, mouse)) positions++;
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, click)) clicks++;
            }
            PortalMenuContext.SetBridgeReady(positions == 2 && clicks == 3);
            if (positions != 2 || clicks != 3)
            {
                Debug.LogWarning("Controller Support: GadgetCore planet input pattern changed; leaving original IL unchanged.");
                return original;
            }
            MethodInfo virtualMouse = typeof(PortalMenuContext).GetMethod("GadgetMousePosition", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo virtualClick = typeof(PortalMenuContext).GetMethod("GadgetMouseButtonDown", BindingFlags.Static | BindingFlags.NonPublic);
            List<CodeInstruction> patched = new List<CodeInstruction>();
            foreach (CodeInstruction instruction in original)
            {
                CodeInstruction copy = new CodeInstruction(instruction);
                if (copy.opcode == OpCodes.Call && Equals(copy.operand, mouse)) copy.operand = virtualMouse;
                else if (copy.opcode == OpCodes.Call && Equals(copy.operand, click)) copy.operand = virtualClick;
                patched.Add(copy);
            }
            return patched;
        }
    }
}
