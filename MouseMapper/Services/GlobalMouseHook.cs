using System.Runtime.InteropServices;

namespace MouseMapper.Services;

public class GlobalMouseHook : IDisposable
{
    public event EventHandler<MouseEventArgs>? MouseEvent;

    public class MouseEventArgs : EventArgs
    {
        public int DeltaX { get; init; }
        public int DeltaY { get; init; }
        public int WheelDelta { get; init; }
        public bool MiddleButtonPressed { get; init; }
        public bool XButton1Pressed { get; init; }
        public bool XButton2Pressed { get; init; }
    }

    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _hookProc;
    private Thread? _hookThread;
    private uint _threadId;
    private bool _disposed;
    private int _lastCursorX;
    private int _lastCursorY;
    private bool _cursorInitialized;
    private readonly object _cursorLock = new();
    private readonly ManualResetEvent _hookReady = new(false);

    #region Private structs

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    #endregion

    #region P/Invoke for message pump

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    #endregion

    public void Start()
    {
        if (_hookThread != null)
            return;

        _hookReady.Reset();
        _hookThread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "GlobalMouseHook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        _hookReady.WaitOne(5000);
    }

    public void Stop()
    {
        if (_hookThread == null)
            return;

        _hookReady.WaitOne(5000);

        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        }

        if (!_hookThread.Join(5000))
        {
            // Thread did not exit cleanly; hook will be released on process exit
        }

        _hookThread = null;
        _threadId = 0;
    }

    public (int dx, int dy) GetCursorDelta()
    {
        NativeMethods.GetCursorPos(out NativeMethods.POINT pt);

        int dx = 0;
        int dy = 0;

        lock (_cursorLock)
        {
            if (_cursorInitialized)
            {
                dx = pt.x - _lastCursorX;
                dy = pt.y - _lastCursorY;
            }

            _lastCursorX = pt.x;
            _lastCursorY = pt.y;
            _cursorInitialized = true;
        }

        return (dx, dy);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _hookReady.Dispose();
        _disposed = true;
    }

    private void HookThreadProc()
    {
        _hookProc = HookCallback;

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        _threadId = GetCurrentThreadId();
        _hookReady.Set();

        // Message pump
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        // Cleanup hook when message pump exits
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_MOUSEWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int wheelDelta = (short)(hookStruct.mouseData >> 16);
                bool middleButton = (NativeMethods.GetAsyncKeyState(0x04) & 0x8000) != 0;

                MouseEvent?.Invoke(this, new MouseEventArgs
                {
                    WheelDelta = wheelDelta,
                    MiddleButtonPressed = middleButton
                });
            }
            else if (msg == NativeMethods.WM_MBUTTONDOWN)
            {
                MouseEvent?.Invoke(this, new MouseEventArgs
                {
                    MiddleButtonPressed = true
                });
            }
            else if (msg == NativeMethods.WM_XBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int xButton = (short)(hookStruct.mouseData >> 16);
                MouseEvent?.Invoke(this, new MouseEventArgs
                {
                    XButton1Pressed = xButton == 1,
                    XButton2Pressed = xButton == 2
                });
            }
            else if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                MouseEvent?.Invoke(this, new MouseEventArgs());
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
