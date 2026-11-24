using System;
using System.Runtime.InteropServices;

// Hooks create only a process-local virtual controller in the SAME SDL instance
// as the adapter under test, never a physical device or the host's event loop.
internal sealed class NativeVirtualApi : IDisposable
{
    private readonly IntPtr library;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int AttachDelegate(int type, int axes, int buttons, int hats);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int IndexDelegate(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr OpenDelegate(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void CloseDelegate(IntPtr joystick);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int AxisDelegate(IntPtr joystick, int axis, short value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ButtonDelegate(IntPtr joystick, int button, byte value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int EventStateDelegate(int state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int PollDelegate(IntPtr buffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate uint WasInitDelegate(uint flags);
    internal readonly AttachDelegate Attach;
    internal readonly IndexDelegate Detach;
    internal readonly OpenDelegate Open;
    internal readonly CloseDelegate Close;
    internal readonly AxisDelegate Axis;
    internal readonly ButtonDelegate Button;
    internal readonly EventStateDelegate ControllerEvents, JoystickEvents;
    internal readonly PollDelegate Poll;
    internal readonly WasInitDelegate WasInit;

    [DllImport("libdl.so.2")] private static extern IntPtr dlopen(string path, int flags);
    [DllImport("libdl.so.2")] private static extern IntPtr dlsym(IntPtr handle, string name);
    [DllImport("libdl.so.2")] private static extern int dlclose(IntPtr handle);

    internal NativeVirtualApi(string path)
    {
        library = dlopen(path, 2);
        if (library == IntPtr.Zero) throw new Exception("Cannot open native virtual test hooks: " + path);
        Attach = (AttachDelegate)Bind("SDL_JoystickAttachVirtual", typeof(AttachDelegate));
        Detach = (IndexDelegate)Bind("SDL_JoystickDetachVirtual", typeof(IndexDelegate));
        Open = (OpenDelegate)Bind("SDL_JoystickOpen", typeof(OpenDelegate));
        Close = (CloseDelegate)Bind("SDL_JoystickClose", typeof(CloseDelegate));
        Axis = (AxisDelegate)Bind("SDL_JoystickSetVirtualAxis", typeof(AxisDelegate));
        Button = (ButtonDelegate)Bind("SDL_JoystickSetVirtualButton", typeof(ButtonDelegate));
        ControllerEvents = (EventStateDelegate)Bind("SDL_GameControllerEventState", typeof(EventStateDelegate));
        JoystickEvents = (EventStateDelegate)Bind("SDL_JoystickEventState", typeof(EventStateDelegate));
        Poll = (PollDelegate)Bind("SDL_PollEvent", typeof(PollDelegate));
        WasInit = (WasInitDelegate)Bind("SDL_WasInit", typeof(WasInitDelegate));
    }

    private Delegate Bind(string name, Type type)
    {
        IntPtr address = dlsym(library, name);
        if (address == IntPtr.Zero) throw new Exception("Missing native test hook " + name);
        return Marshal.GetDelegateForFunctionPointer(address, type);
    }

    public void Dispose() { dlclose(library); }
}
