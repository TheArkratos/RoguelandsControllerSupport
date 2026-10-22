using System;
using ControllerSupport;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        var player = new GameObject();
        var prompt = new GameObject();
        var native = new Material();
        var renderer = new Renderer { sharedMaterial = native };
        prompt.Add(renderer);
        player.Add(new PlayerScript { w = prompt });
        MenuScript.player = player;
        ControllerInteractPrompt.Configure("Joystick1Button2", null);
        ControllerInteractPrompt.Update(true);

        if (renderer.sharedMaterial == native)
            throw new Exception("mesh W prompt was not replaced");
        Texture2D texture = renderer.sharedMaterial.mainTexture;
        if (texture.width != 96 || texture.height != 96)
            throw new Exception("expected 96×96 canvas for centered 32×32 badge");
        int outsideOpaque = 0;
        int badgeOpaque = 0;
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            bool insideBadge = x >= 32 && x < 64 && y >= 32 && y < 64;
            bool opaque = texture.GetPixel(x, y).a > 0f;
            if (!insideBadge && opaque) outsideOpaque++;
            if (insideBadge && opaque) badgeOpaque++;
        }
        Console.WriteLine("Badge opaque pixels: " + badgeOpaque + "; outside 1/3 badge: " + outsideOpaque);
        if (badgeOpaque == 0 || outsideOpaque != 0)
        {
            Console.Error.WriteLine("FAIL: white/opaque border remains around reduced X badge");
            return 1;
        }
        Console.WriteLine("PASS: mesh canvas is transparent beyond X badge");
        return 0;
    }
}
