using System;
using System.Runtime.InteropServices;

namespace ControllerSupport
{
    // Dynamic binding works on the game's old Mono without NativeLibrary APIs
    // or per-runtime dllmap files. The Deck variant embeds a private SDL2 build.
    internal sealed class Sdl2NativeControllerApi : ISdlControllerApi
    {
        private const uint GameController = 0x00002000;
        private readonly bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        private readonly string nativeCacheDirectory;
        private IntPtr library;
        private bool initialized;
        private InitDelegate init;
        private QuitDelegate quit;
        private UpdateDelegate update;
        private CountDelegate count;
        private IndexDelegate isController;
        private OpenDelegate open;
        private AttachedDelegate attached;
        private AxisDelegate axis;
        private ButtonDelegate button;
        private CloseDelegate close;
        private ErrorDelegate error;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitDelegate(uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void QuitDelegate(uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void UpdateDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CountDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IndexDelegate(int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr OpenDelegate(int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int AttachedDelegate(IntPtr pad);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate short AxisDelegate(IntPtr pad, int axis);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte ButtonDelegate(IntPtr pad, int button);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void CloseDelegate(IntPtr pad);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ErrorDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EventStateDelegate(int state);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint WasInitDelegate(uint flags);

        internal Sdl2NativeControllerApi(string nativeCacheDirectory = null)
        {
            this.nativeCacheDirectory = nativeCacheDirectory;
        }

        [DllImport("libdl.so.2", EntryPoint = "dlopen")] private static extern IntPtr Dlopen(string path, int flags);
        [DllImport("libdl.so.2", EntryPoint = "dlsym")] private static extern IntPtr Dlsym(IntPtr handle, string name);
        [DllImport("libdl.so.2", EntryPoint = "dlclose")] private static extern int Dlclose(IntPtr handle);
        [DllImport("libdl.so.2", EntryPoint = "dlerror")] private static extern IntPtr Dlerror();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr handle, string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr handle);

        public void Initialize()
        {
            if (initialized) return;
            string path = Environment.GetEnvironmentVariable("ROGUELANDS_CONTROLLER_SDL2_LIBRARY");
            bool bundled = string.IsNullOrEmpty(path) && BundledSdl2.Available;
            if (bundled) path = BundledSdl2.Extract(nativeCacheDirectory);
            if (string.IsNullOrEmpty(path)) path = windows ? "SDL2.dll" : "libSDL2-2.0.so.0";
            // A private SDL must not bind its internal calls to a host SDL copy.
            // DEEPBIND is Linux-only; bundled variants are Linux x86/x86_64.
            library = windows ? LoadLibraryW(path) : Dlopen(path, bundled ? 10 : 2);
            if (library == IntPtr.Zero)
                throw new DllNotFoundException("Cannot load SDL2 for this process architecture: " +
                    (windows ? "Windows loader error " + Marshal.GetLastWin32Error() : Marshal.PtrToStringAnsi(Dlerror())) +
                    ". Verify the native library and process architecture; no controller fallback was selected.");
            try
            {
                init = (InitDelegate)Bind("SDL_InitSubSystem", typeof(InitDelegate));
                quit = (QuitDelegate)Bind("SDL_QuitSubSystem", typeof(QuitDelegate));
                update = (UpdateDelegate)Bind("SDL_GameControllerUpdate", typeof(UpdateDelegate));
                count = (CountDelegate)Bind("SDL_NumJoysticks", typeof(CountDelegate));
                isController = (IndexDelegate)Bind("SDL_IsGameController", typeof(IndexDelegate));
                open = (OpenDelegate)Bind("SDL_GameControllerOpen", typeof(OpenDelegate));
                attached = (AttachedDelegate)Bind("SDL_GameControllerGetAttached", typeof(AttachedDelegate));
                axis = (AxisDelegate)Bind("SDL_GameControllerGetAxis", typeof(AxisDelegate));
                button = (ButtonDelegate)Bind("SDL_GameControllerGetButton", typeof(ButtonDelegate));
                close = (CloseDelegate)Bind("SDL_GameControllerClose", typeof(CloseDelegate));
                error = (ErrorDelegate)Bind("SDL_GetError", typeof(ErrorDelegate));
                if (bundled)
                {
                    WasInitDelegate wasInit = (WasInitDelegate)Bind("SDL_WasInit", typeof(WasInitDelegate));
                    if (wasInit(0) != 0)
                        throw new InvalidOperationException("Bundled SDL2 unexpectedly shares initialized host state.");
                }
                if (init(GameController) != 0)
                    throw new InvalidOperationException("SDL_InitSubSystem: " + Marshal.PtrToStringAnsi(error()));
                initialized = true;
                if (bundled)
                {
                    // Only our isolated, windowless SDL copy: avoid an unconsumed
                    // event queue without touching the host's event settings.
                    EventStateDelegate joystickEvents = (EventStateDelegate)Bind("SDL_JoystickEventState", typeof(EventStateDelegate));
                    EventStateDelegate controllerEvents = (EventStateDelegate)Bind("SDL_GameControllerEventState", typeof(EventStateDelegate));
                    joystickEvents(0);
                    controllerEvents(0);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private Delegate Bind(string name, Type type)
        {
            IntPtr address = windows ? GetProcAddress(library, name) : Dlsym(library, name);
            if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
            return Marshal.GetDelegateForFunctionPointer(address, type);
        }

        public void Update() { update(); }
        public int Count { get { return count(); } }
        public bool IsController(int index) { return isController(index) != 0; }
        public IntPtr Open(int index) { return open(index); }
        public bool Attached(IntPtr controller) { return attached(controller) != 0; }
        public short Axis(IntPtr controller, int index) { return axis(controller, index); }
        public bool Button(IntPtr controller, int index) { return button(controller, index) != 0; }
        public void Close(IntPtr controller) { close(controller); }

        public void Dispose()
        {
            // Balance only our subsystem reference; never SDL_Quit or Steam shutdown.
            if (initialized) quit(GameController);
            initialized = false;
            if (library == IntPtr.Zero) return;
            if (windows) FreeLibrary(library); else Dlclose(library);
            library = IntPtr.Zero;
        }
    }
}
