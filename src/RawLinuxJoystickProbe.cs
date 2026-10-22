using System;
using System.IO;
using System.Runtime.InteropServices;
using GadgetCore;
using UnityEngine;

namespace ControllerSupport
{
    // One read-only Linux event stream shared by diagnostics and game adapters.
    internal sealed class RawLinuxJoystickProbe : IDisposable
    {
        private const string DevicePath = "/dev/input/js0";
        private const int OpenReadOnlyNonBlocking = 0x800; // O_RDONLY (0) | O_NONBLOCK on Linux
        private const int Again = 11;
        private const int Interrupted = 4;
        private readonly GadgetLogger logger;
        private readonly byte[] eventBytes = new byte[8];
        private readonly int[] lastLoggedAxis = new int[256];
        private readonly JoystickInputState state = new JoystickInputState();
        private int fileDescriptor = -1;
        private int lastPollFrame = -1;
        private float nextOpenAttempt;
        private string lastStatus;

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int Open(string path, int flags);

        [DllImport("libc", EntryPoint = "read", SetLastError = true)]
        private static extern int Read(int fd, byte[] buffer, UIntPtr count);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        private static extern int Close(int fd);

        internal RawLinuxJoystickProbe(GadgetLogger logger)
        {
            this.logger = logger;
            for (int i = 0; i < lastLoggedAxis.Length; i++) lastLoggedAxis[i] = int.MinValue;
        }

        internal JoystickInputState State { get { return state; } }

        internal void Update()
        {
            // A game input wrapper may poll before ProbeRunner.Update. Preserve
            // edge flags for every reader in that Unity frame, and read js0 once.
            if (lastPollFrame == Time.frameCount) return;
            lastPollFrame = Time.frameCount;
            state.BeginFrame();
            if (fileDescriptor < 0)
            {
                if (Time.unscaledTime < nextOpenAttempt) return;
                nextOpenAttempt = Time.unscaledTime + 5f;
                if (!File.Exists(DevicePath))
                {
                    Status("Raw Linux joystick " + DevicePath + " absent; retrying. Unity probe remains active.");
                    return;
                }
                fileDescriptor = Open(DevicePath, OpenReadOnlyNonBlocking);
                if (fileDescriptor < 0)
                {
                    Status("Raw Linux joystick " + DevicePath + " open failed (errno " + Marshal.GetLastWin32Error() + "); retrying.");
                    return;
                }
                state.Connect();
                for (int i = 0; i < lastLoggedAxis.Length; i++) lastLoggedAxis[i] = int.MinValue;
                Status("Raw Linux joystick " + DevicePath + " opened read-only; axis and button numbers are Linux js numbers, not Unity mappings.");
            }

            // Nonblocking reads keep the Unity frame responsive, even if the pad is idle.
            for (int i = 0; i < 64; i++)
            {
                int count = Read(fileDescriptor, eventBytes, (UIntPtr)eventBytes.Length);
                if (count < 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == Again || error == Interrupted) return;
                    Disconnect("read failed (errno " + error + ")");
                    return;
                }
                if (count == 0) { Disconnect("device closed"); return; }
                if (count != eventBytes.Length) { Disconnect("short event read (" + count + " bytes)"); return; }
                RawJoystickEvent joystickEvent;
                if (!RawJoystickEvent.TryParse(eventBytes, out joystickEvent)) continue;
                state.Apply(joystickEvent);
                string initial = joystickEvent.Initial ? " initial" : "";
                if (joystickEvent.Type == 1)
                {
                    logger.Log("Raw js0 button " + joystickEvent.Number + " = " + joystickEvent.Value + initial);
                }
                else if (RawJoystickEvent.ShouldLogAxis(lastLoggedAxis[joystickEvent.Number], joystickEvent.Value))
                {
                    lastLoggedAxis[joystickEvent.Number] = joystickEvent.Value;
                    logger.Log("Raw js0 axis " + joystickEvent.Number + " = " + joystickEvent.Value + initial);
                }
            }
        }

        private void Status(string status)
        {
            if (status == lastStatus) return;
            lastStatus = status;
            logger.Log("Probe " + status);
        }

        private void Disconnect(string reason)
        {
            Dispose();
            nextOpenAttempt = Time.unscaledTime + 5f;
            Status("Raw Linux joystick " + DevicePath + " " + reason + "; retrying.");
        }

        public void Dispose()
        {
            if (fileDescriptor >= 0) Close(fileDescriptor);
            fileDescriptor = -1;
            state.Clear();
        }
    }
}
