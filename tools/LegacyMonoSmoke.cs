using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// Only the adapter type is resolved. Never enumerate mod types or invoke its gadget.
internal static class LegacyMonoSmoke
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo
    {
        public IntPtr FileName;
        public IntPtr BaseAddress;
        public IntPtr SymbolName;
        public IntPtr SymbolAddress;
    }

    [DllImport("libdl.so.2", EntryPoint = "dlsym")]
    private static extern IntPtr Dlsym(IntPtr handle, string name);
    [DllImport("libdl.so.2", EntryPoint = "dladdr")]
    private static extern int Dladdr(IntPtr symbol, out DlInfo info);

    private static string Hash(Stream stream)
    {
        using (SHA256 hash = new SHA256Managed())
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public static int Main(string[] args)
    {
        object api = null;
        Type type = null;
        int result = 0;
        try
        {
            if (args.Length != 3) throw new ArgumentException("Expected mod DLL, cache and resource-export directories.");
            Console.WriteLine("MANAGED_RUNTIME " + Environment.Version);
            Console.WriteLine("PROCESS_ARCH " + (IntPtr.Size == 4 ? "x86" : "x86_64"));
            Console.WriteLine("MSCORLIB " + typeof(object).Assembly.Location);
            Assembly mod = Assembly.LoadFrom(args[0]);
            Directory.CreateDirectory(args[2]);
            foreach (string arch in new string[] { "linux-x86", "linux-x86_64" })
            {
                string name = "ControllerSupport.Native." + arch + ".SDL2";
                using (Stream stream = mod.GetManifestResourceStream(name))
                using (Stream checksum = mod.GetManifestResourceStream(name + ".sha256"))
                {
                    if (stream == null && checksum == null) continue;
                    if (stream == null || checksum == null)
                        throw new InvalidOperationException("Missing native resource/checksum: " + name);
                    string digest = Hash(stream);
                    string expected;
                    using (StreamReader reader = new StreamReader(checksum)) expected = reader.ReadToEnd().Trim();
                    if (digest != expected) throw new InvalidOperationException("Native resource checksum mismatch: " + name);
                    stream.Position = 0;
                    using (Stream destination = File.Create(Path.Combine(args[2], arch + ".so")))
                    {
                        byte[] buffer = new byte[65536];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0) destination.Write(buffer, 0, bytesRead);
                    }
                    Console.WriteLine("VERIFIED_RESOURCE " + arch + " " + digest);
                }
            }
            foreach (string resource in mod.GetManifestResourceNames())
            {
                using (Stream stream = mod.GetManifestResourceStream(resource))
                {
                    // Resource naming is not part of the loader contract. Identify ELF bytes.
                    if (stream.ReadByte() != 0x7f || stream.ReadByte() != 'E' ||
                        stream.ReadByte() != 'L' || stream.ReadByte() != 'F') continue;
                    stream.Position = 0;
                    Console.WriteLine("EMBEDDED_SHA256 " + Hash(stream) + " " + resource);
                }
            }
            type = mod.GetType("ControllerSupport.Sdl2NativeControllerApi", true);
            ConstructorInfo constructor = type.GetConstructor(Members, null, new Type[] { typeof(string) }, null);
            if (constructor != null)
            {
                Console.WriteLine("ADAPTER_CONSTRUCTOR cache-directory");
                api = constructor.Invoke(new object[] { args[1] });
            }
            else
            {
                Console.WriteLine("ADAPTER_CONSTRUCTOR legacy-parameterless");
                api = Activator.CreateInstance(type, true);
            }
            type.GetMethod("Initialize", Members).Invoke(api, null);
            type.GetMethod("Update", Members).Invoke(api, null);
            int count = (int)type.GetProperty("Count", Members).GetValue(api, null);
            if (count < 0) throw new InvalidOperationException("SDL_NumJoysticks returned " + count);
            Console.WriteLine("CONTROLLER_COUNT " + count);
            FieldInfo field = type.GetField("library", Members);
            if (field == null || field.FieldType != typeof(IntPtr))
                throw new InvalidOperationException("Cannot identify native library: expected adapter IntPtr library field.");
            IntPtr symbol = Dlsym((IntPtr)field.GetValue(api), "SDL_InitSubSystem");
            DlInfo info;
            if (symbol == IntPtr.Zero || Dladdr(symbol, out info) == 0)
                throw new InvalidOperationException("Cannot identify loaded SDL with dladdr.");
            string native = Path.GetFullPath(Marshal.PtrToStringAnsi(info.FileName));
            Console.WriteLine("LOADED_NATIVE " + native);
            using (Stream stream = File.OpenRead(native))
                Console.WriteLine("LOADED_SHA256 " + Hash(stream));
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL managed native smoke: " + error);
            result = 1;
        }
        finally
        {
            if (api != null)
            {
                try
                {
                    type.GetMethod("Dispose", Members).Invoke(api, null);
                    Console.WriteLine("ADAPTER_DISPOSED");
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("FAIL adapter disposal: " + error);
                    result = 1;
                }
            }
        }
        if (result == 0) Console.WriteLine("PASS native adapter initialization/update/count/disposal");
        return result;
    }
}
