using System;
using UnityEngine;

namespace ControllerSupport
{
    // Swaps only the local player's existing W prompt artwork. PlayerScript
    // remains the sole owner of its visibility, animation, and interaction.
    internal static class ControllerInteractPrompt
    {
        private const string ButtonPrefix = "Joystick1Button";
        private const int BadgeScale = 3;
        private static string binding = "Unbound";
        private static Action<string> warning;
        private static GameObject prompt;
        private static Renderer renderer;
        private static Sprite originalSprite;
        private static Material originalMaterial;
        private static Sprite replacementSprite;
        private static Material replacementMaterial;
        private static Texture2D replacementTexture;
        private static bool controllerArtwork;
        private static bool warned;

        internal static void Configure(string interactBinding, Action<string> warningLogger)
        {
            Reset();
            binding = string.IsNullOrEmpty(interactBinding) ? "Unbound" : interactBinding;
            warning = warningLogger;
        }

        internal static void Update(bool usingController)
        {
            try
            {
                GameObject player = MenuScript.player;
                PlayerScript script = player ? player.GetComponent<PlayerScript>() : null;
                GameObject current = script ? script.w : null;
                if (current != prompt)
                {
                    Restore();
                    prompt = current;
                    renderer = current ? current.GetComponentInChildren<Renderer>(true) : null;
                    if (renderer)
                    {
                        originalMaterial = renderer.sharedMaterial;
                        SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                        if (spriteRenderer) originalSprite = spriteRenderer.sprite;
                    }
                }
                if (!renderer) return;
                if (!usingController)
                {
                    if (controllerArtwork) RestoreArtwork();
                    return;
                }
                if (controllerArtwork)
                {
                    SpriteRenderer animatedSprite = renderer as SpriteRenderer;
                    if (animatedSprite && animatedSprite.sprite != replacementSprite)
                        animatedSprite.sprite = replacementSprite;
                    return;
                }

                string label = LabelForBinding(binding);
                SpriteRenderer sprite = renderer as SpriteRenderer;
                // A sprite's pixels-per-unit controls its world size. A mesh
                // needs transparent UV padding instead, so the native quad and
                // its animation remain untouched while the badge shrinks.
                replacementTexture = DrawButton(label, !sprite);
                if (sprite)
                {
                    if (!originalSprite) throw new InvalidOperationException("W prompt has no source sprite");
                    replacementSprite = Sprite.Create(replacementTexture,
                        new Rect(0f, 0f, replacementTexture.width, replacementTexture.height),
                        new Vector2(originalSprite.pivot.x / originalSprite.rect.width,
                            originalSprite.pivot.y / originalSprite.rect.height),
                        BadgeScale * replacementTexture.width * originalSprite.pixelsPerUnit /
                            originalSprite.rect.width);
                    if (!replacementSprite) throw new InvalidOperationException("Cannot create button sprite");
                    sprite.sprite = replacementSprite;
                }
                else
                {
                    if (!originalMaterial) throw new InvalidOperationException("W prompt has no source material");
                    replacementMaterial = new Material(originalMaterial);
                    replacementMaterial.mainTexture = replacementTexture;
                    renderer.sharedMaterial = replacementMaterial;
                }
                controllerArtwork = true;
            }
            catch (Exception error)
            {
                RestoreArtwork();
                if (!warned && warning != null)
                    warning("Controller interaction prompt unavailable: " + error.Message);
                warned = true;
            }
        }

        // Known Xbox USB mapping is documented in ControllerSupportDoc.md.
        // Unknown physical buttons use their number, never a guessed Xbox name.
        internal static string LabelForBinding(string token)
        {
            if (token == "LeftTrigger") return "LT";
            if (token == "RightTrigger") return "RT";
            if (token == null || token == "Unbound") return "--";
            if (!token.StartsWith(ButtonPrefix, StringComparison.Ordinal)) return "?";
            int number;
            if (!int.TryParse(token.Substring(ButtonPrefix.Length), out number) ||
                number < 0 || number > 19) return "?";
            switch (number)
            {
                case 0: return "A";
                case 1: return "B";
                case 2: return "X";
                case 3: return "Y";
                case 4: return "LB";
                case 5: return "RB";
                case 6: return "VIEW";
                case 7: return "MENU";
                case 9: return "L3";
                case 10: return "R3";
                default: return "#" + number;
            }
        }

        internal static void Reset()
        {
            Restore();
            binding = "Unbound";
            warning = null;
            warned = false;
        }

        private static void Restore()
        {
            RestoreArtwork();
            prompt = null;
            renderer = null;
            originalSprite = null;
            originalMaterial = null;
        }

        private static void RestoreArtwork()
        {
            if (renderer)
            {
                SpriteRenderer sprite = renderer as SpriteRenderer;
                if (sprite)
                {
                    if (controllerArtwork) sprite.sprite = originalSprite;
                }
                else if (controllerArtwork) renderer.sharedMaterial = originalMaterial;
            }
            controllerArtwork = false;
            if (replacementSprite) UnityEngine.Object.Destroy(replacementSprite);
            if (replacementMaterial) UnityEngine.Object.Destroy(replacementMaterial);
            if (replacementTexture) UnityEngine.Object.Destroy(replacementTexture);
            replacementSprite = null;
            replacementMaterial = null;
            replacementTexture = null;
        }

        private static Texture2D DrawButton(string label, bool padded)
        {
            const int size = 32;
            int offset = padded ? size : 0;
            int canvasSize = padded ? size * BadgeScale : size;
            Texture2D texture = new Texture2D(canvasSize, canvasSize, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color dark = new Color(0.10f, 0.13f, 0.20f, 1f);
            Color rim = new Color(0.80f, 0.73f, 0.52f, 1f);
            Color face = new Color(0.22f, 0.38f, 0.56f, 1f);
            Color ink = new Color(1f, 0.96f, 0.78f, 1f);
            // Texture2D's initial pixel contents are not guaranteed to be
            // transparent. The padded mesh badge needs a clear outer area.
            for (int y = 0; y < canvasSize; y++)
            for (int x = 0; x < canvasSize; x++)
                texture.SetPixel(x, y, clear);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = x - 15, dy = y - 15;
                int radius2 = dx * dx + dy * dy;
                texture.SetPixel(x + offset, y + offset, radius2 > 225 ? clear :
                    radius2 > 192 ? dark : radius2 > 156 ? rim : face);
            }
            string text = label.Length > 4 ? label.Substring(0, 4) : label;
            int scale = text.Length == 1 ? 3 : text.Length == 2 ? 2 : 1;
            int width = text.Length * 6 * scale - scale;
            int left = (size - width) / 2;
            int bottom = (size - 7 * scale) / 2;
            for (int i = 0; i < text.Length; i++)
            {
                byte[] rows = Glyph(text[i]);
                for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                {
                    if ((rows[row] & (1 << (4 - col))) == 0) continue;
                    for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        texture.SetPixel(offset + left + (i * 6 + col) * scale + sx,
                            offset + bottom + (6 - row) * scale + sy, ink);
                }
            }
            texture.Apply();
            return texture;
        }

        private static byte[] Glyph(char letter)
        {
            switch (letter)
            {
                case 'A': return new byte[] { 14, 17, 17, 31, 17, 17, 17 };
                case 'B': return new byte[] { 30, 17, 17, 30, 17, 17, 30 };
                case 'E': return new byte[] { 31, 16, 16, 30, 16, 16, 31 };
                case 'L': return new byte[] { 16, 16, 16, 16, 16, 16, 31 };
                case 'M': return new byte[] { 17, 27, 21, 21, 17, 17, 17 };
                case 'N': return new byte[] { 17, 25, 21, 19, 17, 17, 17 };
                case 'R': return new byte[] { 30, 17, 17, 30, 20, 18, 17 };
                case 'T': return new byte[] { 31, 4, 4, 4, 4, 4, 4 };
                case 'U': return new byte[] { 17, 17, 17, 17, 17, 17, 14 };
                case 'V': return new byte[] { 17, 17, 17, 17, 17, 10, 4 };
                case 'W': return new byte[] { 17, 17, 17, 21, 21, 21, 10 };
                case 'X': return new byte[] { 17, 17, 10, 4, 10, 17, 17 };
                case 'Y': return new byte[] { 17, 17, 10, 4, 4, 4, 4 };
                case '0': return new byte[] { 14, 17, 19, 21, 25, 17, 14 };
                case '1': return new byte[] { 4, 12, 4, 4, 4, 4, 14 };
                case '2': return new byte[] { 14, 17, 1, 2, 4, 8, 31 };
                case '3': return new byte[] { 30, 1, 1, 14, 1, 1, 30 };
                case '4': return new byte[] { 2, 6, 10, 18, 31, 2, 2 };
                case '5': return new byte[] { 31, 16, 16, 30, 1, 1, 30 };
                case '6': return new byte[] { 14, 16, 16, 30, 17, 17, 14 };
                case '7': return new byte[] { 31, 1, 2, 4, 8, 8, 8 };
                case '8': return new byte[] { 14, 17, 17, 14, 17, 17, 14 };
                case '9': return new byte[] { 14, 17, 17, 15, 1, 1, 14 };
                case '#': return new byte[] { 10, 31, 10, 10, 31, 10, 0 };
                case '-': return new byte[] { 0, 0, 0, 31, 0, 0, 0 };
                default: return new byte[] { 14, 17, 1, 2, 4, 0, 4 };
            }
        }
    }
}
