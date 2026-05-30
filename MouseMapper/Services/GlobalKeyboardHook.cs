using System.Runtime.InteropServices;

namespace MouseMapper.Services;

public class GlobalKeyboardHook : IDisposable
{
    public event EventHandler<int>? KeyDown;
    public event EventHandler<int>? KeyUp;

    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _hookProc;
    private Thread? _hookThread;
    private uint _threadId;
    private bool _disposed;
    private readonly ManualResetEvent _hookReady = new(false);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

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

    public void Start()
    {
        if (_hookThread != null)
            return;

        _hookReady.Reset();
        _hookThread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "GlobalKeyboardHook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        _hookReady.WaitOne(5000);
    }

    public void Stop()
    {
        if (_hookThread == null)
            return;

        if (_threadId != 0)
            PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);

        if (!_hookThread.Join(5000)) { }

        _hookThread = null;
        _threadId = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _hookReady.Dispose();
        _disposed = true;
    }

    private void HookThreadProc()
    {
        _hookProc = HookCallback;

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        _threadId = GetCurrentThreadId();
        _hookReady.Set();

        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

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
            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool wasDown = (kb.flags & 0x40) != 0;
                if (!wasDown)
                    KeyDown?.Invoke(this, (int)kb.vkCode);
            }
            else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                KeyUp?.Invoke(this, (int)kb.vkCode);
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
}
