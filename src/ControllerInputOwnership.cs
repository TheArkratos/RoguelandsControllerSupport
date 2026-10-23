using System;

namespace ControllerSupport
{
    // One policy for controller effects. The provider reads InputProbe's existing
    // last-input flag; this module does not arbitrate devices or cache ownership.
    internal static class ControllerInputOwnership
    {
        private static Func<bool> source;

        internal static bool UsingController
        {
            get { return source != null && source(); }
        }

        internal static void Configure(Func<bool> provider) { source = provider; }
        internal static void Reset() { source = null; }
    }
}
