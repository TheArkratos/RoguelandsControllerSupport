using System;

namespace ControllerSupport
{
    // One policy for controller effects. The provider reads InputProbe's existing
    // last-input flag; this module does not arbitrate devices or cache ownership.
    internal static class ControllerInputOwnership
    {
        private static Func<bool> source;
        private static Func<bool> gameplayModal;

        internal static bool UsingController
        {
            get { return source != null && source(); }
        }

        // A live modal query is independent of Harmony prefix order and frame caches.
        // Only controller adapters consult it; native mouse/keyboard input is unchanged.
        internal static bool GameplayBlocked
        { get { return UsingController && gameplayModal != null && gameplayModal(); } }

        internal static void Configure(Func<bool> provider, Func<bool> modalProvider = null)
        { source = provider; gameplayModal = modalProvider; }
        internal static void Reset() { source = null; gameplayModal = null; }
    }
}
