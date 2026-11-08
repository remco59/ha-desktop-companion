using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopCompanion.Windows.Tray;

/// <summary>
/// Owns the native notification-area icon and its hidden callback window.
/// All callbacks are raised on the thread that created this host.
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private const string WindowClassName = "DesktopCompanion.TrayCallbackWindow";
    private const uint CallbackMessage = 0x8000 + 0x41;

    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;

    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifGuid = 0x00000020;

    private const uint NotifyIconVersion4 = 4;
    private const uint WmDestroy = 0x0002;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint NinSelect = 0x0400;
    private const uint NinKeySelect = 0x0401;

    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const int ErrorClassAlreadyExists = 1410;
    private const int IdiApplication = 32512;

    private static readonly Guid IconGuid = new("6D10E8F8-52CB-4C87-91D4-4B1728CE5A33");
    private static readonly ConcurrentDictionary<IntPtr, TrayIconHost> Hosts = new();
    private static readonly WindowProcedure WindowProcDelegate = WindowProcedureHandler;
    private static bool _windowClassRegistered;
    private static readonly object WindowClassLock = new();

    private readonly IntPtr _windowHandle;
    private readonly uint _taskbarCreatedMessage;
    private readonly IntPtr _iconHandle;
    private bool _disposed;
    private bool _iconAdded;

    public event EventHandler? Activated;
    public event EventHandler? ContextRequested;

    public TrayIconHost(string tooltip = "Desktop Companion")
    {
        EnsureWindowClassRegistered();

        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        if (_taskbarCreatedMessage == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the TaskbarCreated message.");
        }

        _iconHandle = LoadIconW(IntPtr.Zero, new IntPtr(IdiApplication));
        if (_iconHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load the application tray icon.");
        }

        var instanceHandle = GetModuleHandleW(null);
        _windowHandle = CreateWindowExW(
            WsExToolWindow | WsExNoActivate,
            WindowClassName,
            "Desktop Companion Tray Host",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            instanceHandle,
            IntPtr.Zero);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray callback window.");
        }

        if (!Hosts.TryAdd(_windowHandle, this))
        {
            DestroyWindow(_windowHandle);
            throw new InvalidOperationException("A tray callback window with this handle already exists.");
        }

        try
        {
            AddIcon(tooltip);
        }
        catch
        {
            Hosts.TryRemove(_windowHandle, out _);
            _ = DestroyWindow(_windowHandle);
            throw;
        }
    }

    private void AddIcon(string tooltip = "Desktop Companion")
    {
        var data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip | NifGuid,
            uCallbackMessage = CallbackMessage,
            hIcon = _iconHandle,
            szTip = tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
            guidItem = IconGuid
        };

        if (!Shell_NotifyIconW(NimAdd, ref data))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not add the Desktop Companion tray icon.");
        }

        _iconAdded = true;
        data.uVersion = NotifyIconVersion4;
        _ = Shell_NotifyIconW(NimSetVersion, ref data);
    }

    private void HandleCallback(IntPtr eventValue)
    {
        // With NOTIFYICON_VERSION_4, the notification code is in LOWORD(lParam).
        var eventCode = unchecked((uint)eventValue.ToInt64()) & 0xFFFF;

        switch (eventCode)
        {
            case WmLButtonUp:
            case WmLButtonDoubleClick:
            case NinSelect:
            case NinKeySelect:
                Activated?.Invoke(this, EventArgs.Empty);
                break;

            case WmRButtonUp:
            case WmContextMenu:
                ContextRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private static void EnsureWindowClassRegistered()
    {
        lock (WindowClassLock)
        {
            if (_windowClassRegistered)
            {
                return;
            }

            var windowClass = new WindowClassEx
            {
                cbSize = (uint)Marshal.SizeOf<WindowClassEx>(),
                lpfnWndProc = WindowProcDelegate,
                hInstance = GetModuleHandleW(null),
                lpszClassName = WindowClassName
            };

            if (RegisterClassExW(ref windowClass) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorClassAlreadyExists)
                {
                    throw new Win32Exception(error, "Could not register the tray callback window class.");
                }
            }

            _windowClassRegistered = true;
            GC.KeepAlive(WindowProcDelegate);
        }
    }

    private static IntPtr WindowProcedureHandler(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (Hosts.TryGetValue(hWnd, out var host))
        {
            try
            {
                if (message == host._taskbarCreatedMessage)
                {
                    host._iconAdded = false;
                    host.AddIcon();
                    return IntPtr.Zero;
                }

                if (message == CallbackMessage)
                {
                    host.HandleCallback(lParam);
                    return IntPtr.Zero;
                }

                if (message == WmDestroy)
                {
                    Hosts.TryRemove(hWnd, out _);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Desktop Companion tray callback failed: {exception}");
            }
        }

        return DefWindowProcW(hWnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_iconAdded)
        {
            var data = new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = NifGuid,
                szTip = string.Empty,
                szInfo = string.Empty,
                szInfoTitle = string.Empty,
                guidItem = IconGuid
            };

            _ = Shell_NotifyIconW(NimDelete, ref data);
            _iconAdded = false;
        }

        Hosts.TryRemove(_windowHandle, out _);
        if (_windowHandle != IntPtr.Zero)
        {
            _ = DestroyWindow(_windowHandle);
        }

        GC.KeepAlive(WindowProcDelegate);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint cbSize;
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WindowProcedure lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;

        public uint uVersion
        {
            readonly get => uTimeoutOrVersion;
            set => uTimeoutOrVersion = value;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WindowClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string message);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);
}
