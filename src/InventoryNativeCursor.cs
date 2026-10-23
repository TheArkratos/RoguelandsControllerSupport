using System;
using System.Collections.Generic;
using UnityEngine;

namespace ControllerSupport
{
    // Restore the native mouse path on handoff. Conversion cursor roots remain
    // active: their animations, particle simulation and native mode state belong
    // to the game. Controller corners replace only their rendered appearance.
    internal static class InventoryNativeCursor
    {
        private static readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        private static readonly HashSet<Renderer> desired = new HashSet<Renderer>();
        private static readonly List<Renderer> restore = new List<Renderer>();

        private static readonly Dictionary<Light, bool> hiddenLights = new Dictionary<Light, bool>();
        private static readonly HashSet<Light> desiredLights = new HashSet<Light>();
        private static readonly List<Light> restoreLights = new List<Light>();

        internal static void BeforeNativeUpdate(ActionCursor cursor)
        {
            if (cursor == null || cursor.gameObject == null || ControllerInputOwnership.UsingController) return;
            // A mouse event can arrive after this frame's controller UI update.
            // Restore captured visuals before the shared native cursor renders.
            Reset();
            string name = cursor.gameObject.name;
            if (string.Equals(name, "holdingItem", StringComparison.Ordinal) ||
                string.Equals(name, "hoverItem", StringComparison.Ordinal) ||
                string.Equals(name, "trasher", StringComparison.Ordinal) ||
                string.Equals(name, "emblem", StringComparison.Ordinal) ||
                string.Equals(name, "prism", StringComparison.Ordinal))
                Menuu.usingController = false;
        }

        internal static void Update(GameScript game)
        {
            bool controller = ControllerInputOwnership.UsingController;
            // Native movement can set this flag without our combat aim owning
            // it. UI handoff must work even while the combat cursor is inactive.
            if (!controller && game != null)
                Menuu.usingController = false;
            desired.Clear(); desiredLights.Clear();
            if (controller && game != null && GameScript.inventoryOpen)
            {
                Hide(game.cursorTrasher);
                Hide(game.cursorEmblemForge);
                Hide(game.cursorPrism);
            }
            restore.Clear();
            foreach (KeyValuePair<Renderer, bool> entry in hidden)
                if (!desired.Contains(entry.Key)) restore.Add(entry.Key);
            foreach (Renderer renderer in restore)
            {
                if (renderer) renderer.enabled = hidden[renderer];
                hidden.Remove(renderer);
            }
            restoreLights.Clear();
            foreach (KeyValuePair<Light, bool> entry in hiddenLights)
                if (!desiredLights.Contains(entry.Key)) restoreLights.Add(entry.Key);
            foreach (Light light in restoreLights)
            {
                if (light) light.enabled = hiddenLights[light];
                hiddenLights.Remove(light);
            }
        }

        private static void Hide(GameObject root)
        {
            if (!root || !root.activeInHierarchy) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer) continue;
                desired.Add(renderer);
                if (!hidden.ContainsKey(renderer)) hidden.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (!light) continue;
                desiredLights.Add(light);
                if (!hiddenLights.ContainsKey(light)) hiddenLights.Add(light, light.enabled);
                light.enabled = false;
            }
        }

        internal static void Reset()
        {
            foreach (KeyValuePair<Renderer, bool> entry in hidden)
                if (entry.Key) entry.Key.enabled = entry.Value;
            foreach (KeyValuePair<Light, bool> entry in hiddenLights)
                if (entry.Key) entry.Key.enabled = entry.Value;
            hidden.Clear(); desired.Clear(); restore.Clear();
            hiddenLights.Clear(); desiredLights.Clear(); restoreLights.Clear();
        }
    }
}
