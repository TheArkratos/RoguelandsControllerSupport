namespace ControllerSupport
{
    internal static class ControllerDialogueInput
    {
        internal static void Prepare(GameScript game) { }
    }

    internal static class ControllerGameplayActions
    {
        internal static void ConsumeForFrame() { }
        internal static void Prepare(GameScript game) { }
        internal static bool HotkeyPressed(int slot) { return false; }
    }
}
