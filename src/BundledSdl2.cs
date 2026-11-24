using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace ControllerSupport
{
    // Only the Deck test build contains these resources. Never put native code in
    // GadgetCore/Libs: that directory is scanned as managed assemblies.
    internal static class BundledSdl2
    {
        private const string ResourcePrefix = "ControllerSupport.Native.";
        internal static bool Available
        {
            get
            {
                Assembly assembly = typeof(BundledSdl2).Assembly;
                using (Stream x86 = assembly.GetManifestResourceStream(ResourcePrefix + "linux-x86.SDL2"))
                using (Stream x64 = assembly.GetManifestResourceStream(ResourcePrefix + "linux-x86_64.SDL2"))
                    return x86 != null || x64 != null;
            }
        }

        internal static string ResourceFor(PlatformID platform, int pointerSize)
        {
            if (platform != PlatformID.Unix || (pointerSize != 4 && pointerSize != 8))
                throw new PlatformNotSupportedException("This package requires the native Linux x86 or x86_64 game, not Windows/Proton.");
            return ResourcePrefix + (pointerSize == 4 ? "linux-x86" : "linux-x86_64") + ".SDL2";
        }

        internal static string Extract(string cacheDirectory)
        {
            string resource = ResourceFor(Environment.OSVersion.Platform, IntPtr.Size);
            Assembly assembly = typeof(BundledSdl2).Assembly;
            using (Stream data = assembly.GetManifestResourceStream(resource))
            using (Stream hash = assembly.GetManifestResourceStream(resource + ".sha256"))
            {
                if (data == null || hash == null) throw new InvalidOperationException("Bundled SDL2 resources are missing.");
                using (StreamReader reader = new StreamReader(hash))
                    return ExtractVerified(data, reader.ReadToEnd().Trim(), cacheDirectory);
            }
        }

        internal static string ExtractVerified(Stream data, string expectedHash, string cacheDirectory)
        {
            if (string.IsNullOrEmpty(cacheDirectory) || !Path.IsPathRooted(cacheDirectory))
                throw new InvalidOperationException("Bundled SDL2 requires an absolute GadgetCore temporary directory.");
            if (expectedHash == null || expectedHash.Length != 64 ||
                expectedHash.Trim("0123456789abcdef".ToCharArray()).Length != 0)
                throw new InvalidDataException("Invalid bundled SDL2 checksum.");
            byte[] bytes;
            using (MemoryStream buffer = new MemoryStream())
            {
                byte[] chunk = new byte[8192];
                int count;
                while ((count = data.Read(chunk, 0, chunk.Length)) > 0) buffer.Write(chunk, 0, count);
                bytes = buffer.ToArray();
            }
            if (Hash(bytes) != expectedHash) throw new InvalidDataException("Bundled SDL2 checksum mismatch.");
            string directory = Path.Combine(Path.Combine(cacheDirectory, "ControllerSupport"), expectedHash);
            string path = Path.Combine(directory, "libControllerSupport-SDL2.so");
            Directory.CreateDirectory(directory);
            if (!File.Exists(path))
            {
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                        output.Write(bytes, 0, bytes.Length);
                    try { File.Move(temporary, path); }
                    catch (IOException) { if (!File.Exists(path)) throw; }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            // Refuse to overwrite unexpected bytes, even in the ephemeral cache.
            if (Hash(File.ReadAllBytes(path)) != expectedHash)
                throw new InvalidDataException("Extracted SDL2 checksum mismatch; cached file preserved.");
            return path;
        }

        private static string Hash(byte[] data)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
