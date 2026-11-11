using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Still.App;

internal static class NativeDesktop
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    internal static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");

    internal static List<DisplayArea> Displays()
    {
        var displays = new List<DisplayArea>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref info))
            {
                GetDpiForMonitor(monitor, 0, out uint dpi, out _);
                var r = info.Work;
                displays.Add(new(info.Device, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, dpi > 0 ? dpi / 96d : 1, (info.Flags & 1) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return displays;
    }

    internal static void SetInteraction(Window window, bool clickThrough)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        int style = GetWindowLong(hwnd, -20);
        style |= 0x80; // WS_EX_TOOLWINDOW, excluded from Alt+Tab.
        style = clickThrough ? style | 0x20 : style & ~0x20; // layered-window mouse transparency.
        SetWindowLong(hwnd, -20, style);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x37); // no move/size/z-order/activate; refresh frame.
    }

    internal static void Move(Window window, PixelPoint point) => SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, (int)Math.Round(point.X), (int)Math.Round(point.Y), 0, 0, 0x15);
}
