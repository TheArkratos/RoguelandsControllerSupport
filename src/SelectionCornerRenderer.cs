using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ControllerSupport
{
    // Visual only: the caller decides which object is selected and supplies its
    // world-space bounds. No collider, raycast, or input state is created here.
    internal sealed class SelectionCornerRenderer : IDisposable
    {
        private static readonly string[] ResourceNames =
        {
            "ControllerSupport.SelectionCorners.corner-top-left.png",
            "ControllerSupport.SelectionCorners.corner-top-right.png",
            "ControllerSupport.SelectionCorners.corner-bottom-right.png",
            "ControllerSupport.SelectionCorners.corner-bottom-left.png"
        };

        private static readonly string[] CornerNames =
        {
            "Top Left", "Top Right", "Bottom Right", "Bottom Left"
        };

        private static readonly Vector2[] Pivots =
        {
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 0f), new Vector2(0f, 0f)
        };

        private readonly GameObject root;
        private readonly SpriteRenderer[] renderers;
        private readonly Sprite[] sprites;
        private readonly Texture2D[] textures;
        private readonly Color[] normalCornerColors;
        private readonly Vector3[] normalCornerScales;
        private readonly SpriteRenderer heldIconRenderer;
        // The game's item material owns its texture. Only the Sprite wrapper is ours.
        private Sprite heldIconSprite;
        private int heldIconId;
        private float heldIconWidth;
        private float heldIconHeight;
        private bool disposed;

        // Unity destroys scene objects on a level transition while this
        // managed wrapper may still be held by the static menu controller.
        internal bool IsAlive { get { return !disposed && root != null; } }

        // Share the actual visible badge geometry with quantity presentation.
        // The renderer remains the single owner of its size and placement.
        internal Bounds? HeldItemBounds
        {
            get
            {
                if (!IsAlive || !root.activeInHierarchy || !heldIconRenderer.enabled ||
                    heldIconRenderer.sprite == null) return null;
                return heldIconRenderer.bounds;
            }
        }


        private SelectionCornerRenderer(GameObject root, SpriteRenderer[] renderers,
            Sprite[] sprites, Texture2D[] textures, SpriteRenderer heldIconRenderer)
        {
            this.root = root;
            this.renderers = renderers;
            this.sprites = sprites;
            this.textures = textures;
            this.heldIconRenderer = heldIconRenderer;
            normalCornerColors = new Color[renderers.Length];
            normalCornerScales = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                normalCornerColors[i] = renderers[i].color;
                normalCornerScales[i] = renderers[i].transform.localScale;
            }
        }

        // pixelsPerUnit sets the six-pixel corner's world size; the caller should
        // choose the same scale as its menu sprites. Returns null on asset failure.
        internal static SelectionCornerRenderer TryCreate(float pixelsPerUnit,
            int sortingOrder, Action<string> logWarning)
        {
            if (pixelsPerUnit <= 0f || float.IsNaN(pixelsPerUnit) ||
                float.IsInfinity(pixelsPerUnit))
            {
                Warn(logWarning, "Invalid selection-corner pixels-per-unit value.");
                return null;
            }

            GameObject root = null;
            SpriteRenderer[] renderers = new SpriteRenderer[4];
            Sprite[] sprites = new Sprite[4];
            Texture2D[] textures = new Texture2D[4];
            SpriteRenderer heldIconRenderer = null;
            try
            {
                root = new GameObject("Controller Support Selection Corners");
                for (int i = 0; i < 4; i++)
                {
                    byte[] bytes = ReadResource(ResourceNames[i]);
                    textures[i] = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!textures[i].LoadImage(bytes))
                        throw new InvalidDataException("Cannot decode " + ResourceNames[i]);
                    textures[i].filterMode = FilterMode.Point;
                    textures[i].wrapMode = TextureWrapMode.Clamp;
                    sprites[i] = Sprite.Create(textures[i],
                        new Rect(0f, 0f, textures[i].width, textures[i].height),
                        Pivots[i], pixelsPerUnit);
                    if (sprites[i] == null)
                        throw new InvalidDataException("Cannot create sprite from " + ResourceNames[i]);

                    GameObject corner = new GameObject("Selection Corner " + CornerNames[i]);
                    corner.transform.parent = root.transform;
                    renderers[i] = corner.AddComponent<SpriteRenderer>();
                    renderers[i].sprite = sprites[i];
                    renderers[i].sortingOrder = sortingOrder;
                }
                GameObject heldIcon = new GameObject("Held Item On Selection Corner");
                heldIcon.transform.parent = root.transform;
                heldIconRenderer = heldIcon.AddComponent<SpriteRenderer>();
                heldIconRenderer.sortingOrder = sortingOrder + 1;
                heldIconRenderer.enabled = false;
                root.SetActive(false);
                return new SelectionCornerRenderer(root, renderers, sprites, textures,
                    heldIconRenderer);
            }
            catch (Exception error)
            {
                Warn(logWarning, "Selection corners unavailable: " + error.Message);
                if (root != null) UnityEngine.Object.Destroy(root);
                DestroyAssets(sprites, textures);
                return null;
            }
        }

        // Scale only the four corner glyphs about their existing outer pivots.
        // The selection bounds, root and held-item badge keep their exact geometry.
        // Always derive from captured defaults so repeated frames cannot compound.
        internal void SetStyle(SelectionCornerStyle style)
        {
            if (disposed || root == null) return;
            bool active = style != SelectionCornerStyle.Normal;
            for (int i = 0; i < renderers.Length; i++)
            {
                Color normal = normalCornerColors[i];
                Vector3 scale = normalCornerScales[i];
                renderers[i].color = style == SelectionCornerStyle.Red ? new Color(1f, 0f, 0f, normal.a)
                    : style == SelectionCornerStyle.Blue ? new Color(0f, 0f, 1f, normal.a)
                    : style == SelectionCornerStyle.Purple ? new Color(0.65f, 0.15f, 1f, normal.a) : normal;
                renderers[i].transform.localScale = active
                    ? new Vector3(scale.x * 1.5f, scale.y * 1.5f, scale.z) : scale;
            }
        }

        internal void Show(Collider target)
        {
            if (target == null)
            {
                Hide();
                return;
            }
            // Native inventory cells move with their camera parent. Follow that
            // transform even if CameraScript.Update runs after selection Update.
            // Assigning parent retains the root's world position/rotation/scale;
            // the existing world-space badge geometry below stays unchanged.
            if (!disposed && root != null && root.transform.parent != target.transform)
                root.transform.parent = target.transform;
            Show(target.bounds);
        }

        internal void Show(Bounds bounds)
        {
            if (disposed || root == null) return;
            float z = bounds.center.z;
            renderers[0].transform.position = new Vector3(bounds.min.x, bounds.max.y, z);
            renderers[1].transform.position = new Vector3(bounds.max.x, bounds.max.y, z);
            renderers[2].transform.position = new Vector3(bounds.max.x, bounds.min.y, z);
            renderers[3].transform.position = new Vector3(bounds.min.x, bounds.min.y, z);
            if (heldIconRenderer.enabled)
            {
                // The native held icon uses the same item art as inventory.
                // Its rendered world size is supplied by the menu controller.
                // Fall back to a slot-relative estimate if Unity cannot report it.
                float width = heldIconWidth > 0f ? heldIconWidth * 0.75f : bounds.size.x * 0.55f;
                float height = heldIconHeight > 0f ? heldIconHeight * 0.75f : bounds.size.y * 0.55f;
                // Guard against implausible reference bounds without
                // shrinking a legitimate native icon to slot size.
                width = Mathf.Min(width, bounds.size.x * 1.25f);
                height = Mathf.Min(height, bounds.size.y * 1.25f);
                Vector3 spriteSize = heldIconRenderer.sprite.bounds.size;
                heldIconRenderer.transform.position = new Vector3(
                    bounds.min.x + width * 0.5f, bounds.max.y - height * 0.5f, z);
                heldIconRenderer.transform.localScale = new Vector3(
                    width / spriteSize.x, height / spriteSize.y, 1f);
            }
            if (!root.activeSelf) root.SetActive(true);
        }

        // Reuse the native item material's texture and update only when the held
        // item changes. The game already supplies this art for inventory icons.
        internal void SetHeldItemIcon(int itemId, float normalWorldWidth,
            float normalWorldHeight)
        {
            if (disposed || root == null) return;
            if (normalWorldWidth > 0f && !float.IsNaN(normalWorldWidth) &&
                !float.IsInfinity(normalWorldWidth)) heldIconWidth = normalWorldWidth;
            if (normalWorldHeight > 0f && !float.IsNaN(normalWorldHeight) &&
                !float.IsInfinity(normalWorldHeight)) heldIconHeight = normalWorldHeight;
            if (itemId == heldIconId) return;
            heldIconId = itemId;
            heldIconRenderer.enabled = false;
            heldIconRenderer.sprite = null;
            if (heldIconSprite != null)
            {
                UnityEngine.Object.Destroy(heldIconSprite);
                heldIconSprite = null;
            }
            if (itemId <= 0)
            {
                heldIconWidth = 0f;
                heldIconHeight = 0f;
                return;
            }
            Material material = Resources.Load("i/i" + itemId) as Material;
            Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
            if (texture == null || texture.width <= 0 || texture.height <= 0) return;
            heldIconSprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
            if (heldIconSprite == null) return;
            heldIconRenderer.sprite = heldIconSprite;
            heldIconRenderer.enabled = true;
        }

        internal void Hide()
        {
            SetStyle(SelectionCornerStyle.Normal);
            if (!disposed && root != null && root.activeSelf) root.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root);
            if (heldIconSprite != null) UnityEngine.Object.Destroy(heldIconSprite);
            DestroyAssets(sprites, textures);
        }

        private static byte[] ReadResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Missing embedded sprite", name);
                if (stream.Length <= 0 || stream.Length > 1024 * 1024)
                    throw new InvalidDataException("Invalid embedded sprite length: " + name);
                byte[] bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count <= 0) throw new EndOfStreamException("Truncated embedded sprite: " + name);
                    offset += count;
                }
                return bytes;
            }
        }

        private static void DestroyAssets(Sprite[] sprites, Texture2D[] textures)
        {
            foreach (Sprite sprite in sprites)
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (Texture2D texture in textures)
                if (texture != null) UnityEngine.Object.Destroy(texture);
        }

        private static void Warn(Action<string> logWarning, string message)
        {
            if (logWarning != null) logWarning(message);
            else Debug.LogWarning(message);
        }
    }
}
