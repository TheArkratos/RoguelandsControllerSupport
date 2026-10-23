using System.Runtime.CompilerServices;
using ControllerSupport;

// Legacy fixtures exercise controller-positive behavior without constructing a
// Gadget/InputProbe. This initializer is included ONLY by tests/Directory.Build.targets;
// production keeps the policy's unconfigured default false. Fixtures testing
// ownership transitions override Configure or Reset explicitly.
internal static class ControllerOwnershipTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ControllerInputOwnership.Configure(() => true);
    }
}
