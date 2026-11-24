using System;
using System.IO;
using System.Security.Cryptography;
using ControllerSupport;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException) { checks++; return; }
        catch (InvalidOperationException) { checks++; return; }
        throw new Exception(message);
    }
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "controller-bundle-" + Guid.NewGuid().ToString("N"));
        byte[] bytes = { 127, 69, 76, 70, 2, 1, 1, 0 };
        string hash;
        using (SHA256 sha = SHA256.Create())
            hash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        try
        {
            Check(BundledSdl2.ResourceFor(PlatformID.Unix, 4) == "ControllerSupport.Native.linux-x86.SDL2", "ordinary 32-bit native launch selects x86 resource");
            Check(BundledSdl2.ResourceFor(PlatformID.Unix, 8) == "ControllerSupport.Native.linux-x86_64.SDL2", "64-bit native launch selects x64 resource");
            try { BundledSdl2.ResourceFor(PlatformID.Win32NT, 4); throw new Exception("Windows must not select Linux bytes"); }
            catch (PlatformNotSupportedException) { checks++; }
            try { BundledSdl2.ResourceFor(PlatformID.Unix, 16); throw new Exception("unknown architecture must fail closed"); }
            catch (PlatformNotSupportedException) { checks++; }
            Check(!BundledSdl2.Available, "normal assembly does not select bundled backend");
            Reject(() => BundledSdl2.ExtractVerified(new MemoryStream(bytes), hash, "relative"), "reject relative cache path");
            Reject(() => BundledSdl2.ExtractVerified(new MemoryStream(bytes), "../" + new string('a', 61), root), "reject traversal in checksum");
            Reject(() => BundledSdl2.ExtractVerified(new MemoryStream(bytes), new string('a', 64), root), "reject corrupted embedded bytes");
            Check(!Directory.Exists(root), "validation failure creates no files");
            string path = BundledSdl2.ExtractVerified(new MemoryStream(bytes), hash, root);
            Check(Path.GetFileName(Path.GetDirectoryName(path)) == hash, "content-addressed private path");
            Check(Convert.ToHexString(File.ReadAllBytes(path)) == Convert.ToHexString(bytes), "byte-identical extraction");
            Check(BundledSdl2.ExtractVerified(new MemoryStream(bytes), hash, root) == path, "identical cache safely reused");
            File.WriteAllText(path, "preserve unexpected existing bytes");
            Reject(() => BundledSdl2.ExtractVerified(new MemoryStream(bytes), hash, root), "reject tampered cache");
            Check(File.ReadAllText(path) == "preserve unexpected existing bytes", "do not overwrite existing unexpected data");
            Check(Directory.GetFiles(Path.GetDirectoryName(path)).Length == 1, "no temporary files remain");
        }
        finally
        {
            // Only this fixture's GUID directory, never a user-selected location.
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        Console.WriteLine("Bundled SDL2 extraction: " + checks + " checks passed.");
    }
}
