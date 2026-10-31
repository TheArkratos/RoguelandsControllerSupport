using System;
using System.IO;
using System.Reflection;

// Build-time MSBuild paths keep runtime IL fixtures aligned with their references.
internal static class LocalReferences
{
    internal static string Get(string name)
    {
        foreach (AssemblyMetadataAttribute attribute in typeof(LocalReferences).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            if (attribute.Key == name)
            {
                if (!Directory.Exists(attribute.Value))
                    throw new DirectoryNotFoundException(name + " is unavailable: " + attribute.Value +
                        ". Configure ROGUELANDS_DIR or ROGUELANDS_REFERENCE_DIR before building this fixture.");
                return attribute.Value;
            }
        throw new InvalidOperationException("Missing fixture reference configuration: " + name);
    }
}
