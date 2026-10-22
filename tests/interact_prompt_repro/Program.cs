using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }

    private static bool Close(float actual, float expected)
    { return Math.Abs(actual - expected) < 0.001f; }

    private static void CheckBadgeBounds(Texture2D texture)
    {
        int minX = texture.width, maxX = -1, minY = texture.height, maxY = -1;
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
            if (texture.GetPixel(x, y).a > 0f)
            {
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        Check(texture.width == 96 && texture.height == 96, "mesh texture preserves native quad with threefold canvas");
        Check(maxX - minX + 1 <= 32 && maxY - minY + 1 <= 32, "mesh badge occupies at most one third per axis");
        Check(Math.Abs((minX + maxX) / 2f - (texture.width - 1) / 2f) <= 0.5f &&
            Math.Abs((minY + maxY) / 2f - (texture.height - 1) / 2f) <= 0.5f, "mesh badge centered within one pixel");
    }

    private static void Main()
    {
        string[] expected = { "A", "B", "X", "Y", "LB", "RB", "VIEW", "MENU", "#8", "L3", "R3" };
        for (int i = 0; i < 20; i++)
        {
            string result = ControllerInteractPrompt.LabelForBinding("Joystick1Button" + i);
            Check(result == (i < expected.Length ? expected[i] : "#" + i), "button label " + i);
        }
        Check(ControllerInteractPrompt.LabelForBinding("LeftTrigger") == "LT", "LT label");
        Check(ControllerInteractPrompt.LabelForBinding("RightTrigger") == "RT", "RT label");
        Check(ControllerInteractPrompt.LabelForBinding("Unbound") == "--", "unbound label");
        Check(ControllerInteractPrompt.LabelForBinding("Joystick1Button20") == "?", "invalid label");

        var original = new Sprite { rect = new Rect(0, 0, 32, 32), pivot = new Vector2(16, 16), pixelsPerUnit = 32 };
        var player = new GameObject();
        var prompt = new GameObject { activeSelf = false };
        var renderer = new SpriteRenderer { sprite = original, sharedMaterial = new Material() };
        prompt.Add(renderer);
        player.Add(new PlayerScript { w = prompt });
        MenuScript.player = player;
        ControllerInteractPrompt.Configure("Joystick1Button2", null);
        ControllerInteractPrompt.Update(false);
        Check(renderer.sprite == original, "keyboard keeps original sprite");
        ControllerInteractPrompt.Update(true);
        Check(renderer.sprite != original, "controller swaps sprite");
        Check(!prompt.activeSelf, "controller does not show hidden native prompt");
        var controllerSprite = renderer.sprite;
        Check(Close(controllerSprite.rect.width / controllerSprite.pixelsPerUnit,
            original.rect.width / original.pixelsPerUnit / 3f), "sprite badge one third width");
        Check(Close(controllerSprite.rect.height / controllerSprite.pixelsPerUnit,
            original.rect.height / original.pixelsPerUnit / 3f), "sprite badge one third height");
        Check(Close(controllerSprite.pivot.x / controllerSprite.rect.width,
            original.pivot.x / original.rect.width) &&
            Close(controllerSprite.pivot.y / controllerSprite.rect.height,
            original.pivot.y / original.rect.height), "sprite badge preserves center pivot");
        renderer.sprite = original;
        ControllerInteractPrompt.Update(true);
        Check(renderer.sprite == controllerSprite, "animation cannot overwrite controller artwork");
        ControllerInteractPrompt.Update(false);
        Check(renderer.sprite == original, "keyboard restores exact sprite");
        ControllerInteractPrompt.Update(true);
        ControllerInteractPrompt.Reset();
        Check(renderer.sprite == original, "reset restores exact sprite");

        var originalMaterial = new Material();
        prompt = new GameObject { activeSelf = true };
        var meshRenderer = new Renderer { sharedMaterial = originalMaterial };
        prompt.Add(meshRenderer);
        player.Add(new PlayerScript { w = prompt });
        ControllerInteractPrompt.Configure("RightTrigger", null);
        ControllerInteractPrompt.Update(true);
        Check(meshRenderer.sharedMaterial != originalMaterial, "mesh renderer receives cloned material");
        CheckBadgeBounds(meshRenderer.sharedMaterial.mainTexture);
        var nextPlayer = new GameObject();
        var nextPrompt = new GameObject();
        var nextSprite = new SpriteRenderer { sprite = original, sharedMaterial = new Material() };
        nextPrompt.Add(nextSprite);
        nextPlayer.Add(new PlayerScript { w = nextPrompt });
        MenuScript.player = nextPlayer;
        ControllerInteractPrompt.Update(true);
        Check(meshRenderer.sharedMaterial == originalMaterial, "old player material restored on local-player change");
        Check(nextSprite.sprite != original, "new local player receives controller prompt");
        ControllerInteractPrompt.Update(false);
        Check(nextSprite.sprite == original, "new player prompt restored on keyboard");
        Check(prompt.activeSelf, "native active state preserved");
        Console.WriteLine("Interaction prompt: " + checks + " checks passed.");
    }
}
