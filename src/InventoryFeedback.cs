using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ControllerSupport
{
    // Presentation only. Callers decide action eligibility using native rules;
    // this class never consumes input or modifies an item.
    internal static class InventoryFeedback
    {
        private static readonly FieldInfo HeldField = typeof(GameScript).GetField(
            "holdingItem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static GameObject root;
        private static GameScript game;
        private static Collider selected;
        private static Bounds? heldBadgeBounds;
        private static Func<Bounds?> heldBadgeProvider;
        private static bool controller;
        private static string hints;
        private static string rejection;
        private static float rejectionUntil;

        internal static void SetContext(GameScript current, Collider target,
            bool controllerActive, BindingConfig bindings, string primary,
            string secondary, bool canTransfer, bool canDrop, Bounds? badgeBounds = null,
            string cancelLabel = null, Func<Bounds?> badgeProvider = null)
        {
            game = current;
            selected = target;
            heldBadgeBounds = badgeBounds;
            heldBadgeProvider = badgeProvider;
            controller = controllerActive;
            hints = null;
            if (!IsOpen() || bindings == null) return;
            if (controllerActive)
            {
                StringBuilder text = new StringBuilder();
                Append(text, bindings, "MenuActivate", primary);
                Append(text, bindings, "MenuSecondary", secondary);
                if (canTransfer) Append(text, bindings, "MenuTransfer", "Transfer");
                if (canDrop) Append(text, bindings, "MenuDrop", "Drop");
                Item held = HeldItem();
                Append(text, bindings, "MenuCancel", cancelLabel ?? (held != null && held.id > 0 && held.q > 0 ? "Return" : "Close"));
                hints = text.ToString();
            }
            EnsureRunner();
        }

        internal static void Reject(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return;
            rejection = reason;
            rejectionUntil = Time.realtimeSinceStartup + 2f;
            EnsureRunner();
        }

        internal static void Reset()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            game = null;
            selected = null;
            heldBadgeBounds = null;
            heldBadgeProvider = null;
            hints = rejection = null;
            rejectionUntil = 0f;
            controller = false;
        }

        private static void Append(StringBuilder text, BindingConfig bindings,
            string action, string label)
        {
            if (string.IsNullOrEmpty(label) || bindings[action] == "Unbound") return;
            if (text.Length > 0) text.Append("   ·   ");
            text.Append(bindings.Label(action)).Append(" ").Append(label);
        }

        private static bool IsOpen()
        {
            return game != null && !GameScript.pausing && GameScript.inventoryOpen &&
                game.inventoryMain != null && game.inventoryMain.activeInHierarchy;
        }

        private static Item HeldItem()
        {
            return game != null && HeldField != null ? HeldField.GetValue(game) as Item : null;
        }

        private static void EnsureRunner()
        {
            if (root != null) return;
            root = new GameObject("Controller Support Inventory Feedback");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<InventoryFeedbackRunner>();
        }

        internal sealed class InventoryFeedbackRunner : MonoBehaviour
        {
            private GUIStyle hintStyle;
            private GUIStyle quantityStyle;

            private void OnGUI()
            {
                if (!IsOpen()) return;
                float scale = Mathf.Clamp(Mathf.Min(Screen.height / 720f,
                    Screen.width / 1280f), 0.75f, 2f);
                if (hintStyle == null)
                {
                    hintStyle = new GUIStyle(GUI.skin.label);
                    hintStyle.alignment = TextAnchor.MiddleCenter;
                    hintStyle.wordWrap = true;
                    hintStyle.normal.textColor = Color.white;
                    quantityStyle = new GUIStyle(GUI.skin.label);
                    quantityStyle.alignment = TextAnchor.UpperLeft;
                    quantityStyle.normal.textColor = Color.white;
                    // Position the glyphs themselves beside the badge; native
                    // label padding otherwise adds an invisible extra gap.
                    quantityStyle.padding = new RectOffset(0, 0, 0, 0);
                }
                hintStyle.fontSize = Mathf.RoundToInt(16f * scale);
                quantityStyle.fontSize = Mathf.RoundToInt(16f * scale);
                float margin = 12f * scale;
                float width = Mathf.Max(1f, Mathf.Min(Screen.width - margin * 2f, 760f * scale));
                float bottom = Screen.height - margin;
                if (controller && !string.IsNullOrEmpty(hints))
                    bottom = DrawBar(hints, width, bottom, scale);
                if (!string.IsNullOrEmpty(rejection) && Time.realtimeSinceStartup < rejectionUntil)
                    DrawBar(rejection, width, bottom - 4f * scale, scale);
                if (!controller || selected == null || Camera.main == null) return;
                // Scene/camera transforms may have advanced since GameScript.Update.
                // Read visible badge and cell geometry together at GUI render time.
                Bounds? currentBadge = heldBadgeProvider != null ? heldBadgeProvider() : heldBadgeBounds;
                if (!currentBadge.HasValue) return;
                Item held = HeldItem();
                if (held == null || held.id <= 0 || held.q <= 1) return;
                Bounds bounds = currentBadge.Value;
                Vector3 corner = Camera.main.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z));
                Vector3 right = Camera.main.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                if (corner.z <= 0f || right.z <= 0f) return;
                // Use the badge that was actually rendered, including its native
                // art dimensions and slot-specific cap, rather than estimating it.
                Bounds cell = selected.bounds;
                Vector3 cellLeft = Camera.main.WorldToScreenPoint(new Vector3(cell.min.x, cell.max.y, cell.center.z));
                Vector3 cellRight = Camera.main.WorldToScreenPoint(new Vector3(cell.max.x, cell.max.y, cell.center.z));
                if (cellLeft.z <= 0f || cellRight.z <= 0f) return;
                const float quantityGap = 1f;
                string quantity = held.q.ToString();
                GUIContent quantityContent = new GUIContent(quantity);
                float quantityWidth = quantityStyle.CalcSize(quantityContent).x;
                float availableQuantityWidth = Mathf.Max(1f, cellRight.x - cellLeft.x - quantityGap * 2f);
                // Narrow cells still show the authoritative full stack count.
                // Reduce only its text size; never alter the held icon geometry.
                while (quantityWidth > availableQuantityWidth && quantityStyle.fontSize > 1)
                {
                    quantityStyle.fontSize--;
                    quantityWidth = quantityStyle.CalcSize(quantityContent).x;
                }
                float quantityHeight = quantityStyle.CalcHeight(quantityContent, quantityWidth);
                // A valid held badge can extend beyond its selected cell. Keep
                // its counter inside that cell rather than labeling the next one.
                float x = Mathf.Max(cellLeft.x + quantityGap,
                    Mathf.Min(right.x + quantityGap, cellRight.x - quantityWidth - quantityGap));
                x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - quantityWidth - scale));
                float y = Mathf.Clamp(Screen.height - Mathf.Min(corner.y, cellLeft.y), 0f,
                    Mathf.Max(0f, Screen.height - quantityHeight - scale));
                Rect quantityRect = new Rect(x, y, quantityWidth, quantityHeight);
                Color previous = GUI.color;
                GUI.color = Color.black;
                GUI.Label(new Rect(x + scale, y + scale, quantityRect.width, quantityRect.height), quantity, quantityStyle);
                GUI.color = Color.white;
                GUI.Label(quantityRect, quantity, quantityStyle);
                GUI.color = previous;
            }

            private float DrawBar(string text, float availableWidth, float bottom, float scale)
            {
                GUIContent content = new GUIContent(text);
                float width = Mathf.Min(availableWidth, hintStyle.CalcSize(content).x + 16f * scale);
                float height = hintStyle.CalcHeight(content, Mathf.Max(1f, width - 16f * scale)) + 12f * scale;
                float top = bottom - height;
                float left = (Screen.width - width) * 0.5f;
                Color previous = GUI.color;
                Color previousBackground = GUI.backgroundColor;
                try
                {
                    GUI.color = Color.white;
                    GUI.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.94f);
                    GUI.Box(new Rect(left, top, width, height), "");
                    GUI.Label(new Rect(left + 8f * scale, top + 6f * scale,
                        Mathf.Max(1f, width - 16f * scale), height - 12f * scale), text, hintStyle);
                }
                finally
                {
                    GUI.color = previous;
                    GUI.backgroundColor = previousBackground;
                }
                return top;
            }
        }
    }
}
