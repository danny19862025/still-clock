using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace Still.Linux;

/// <summary>Linux desktop integration: displays, fonts, XDG autostart and a few X11 window hints.</summary>
internal static class LinuxDesktop
{
    // ---- Displays ----
    internal static List<DisplayArea> Displays(Window window)
    {
        var screens = window.Screens.All;
        return screens.Select((s, i) =>
        {
            var w = s.WorkingArea.Width > 0 ? s.WorkingArea : s.Bounds;
            string name = string.IsNullOrWhiteSpace(s.DisplayName) ? $"display-{i + 1}" : s.DisplayName!;
            return new DisplayArea(name, w.X, w.Y, w.Width, w.Height, s.Scaling > 0 ? s.Scaling : 1, s.IsPrimary);
        }).ToList();
    }

    // ---- Fonts ----
    // Presets name Windows fonts. Saved preferences keep those names (so themes stay portable)
    // and Linux draws them with bundled open-licence equivalents.
    internal const string Sans = "Inter", Din = "Barlow", Script = "Kaushan Script", Mono = "Share Tech Mono", Round = "Quicksand", Wide = "Michroma";
    internal static readonly string[] Bundled = [Sans, Din, Script, Mono, Round, Wide];
    private static readonly Dictionary<string, string> Substitutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Segoe UI Variable Display"] = Sans, ["Segoe UI Variable Text"] = Sans, ["Segoe UI"] = Sans, ["Segoe UI Light"] = Sans,
        ["Bahnschrift"] = Din, ["Anurati"] = Wide,
        ["Segoe Script"] = Script, ["Ink Free"] = Script, ["Segoe Print"] = Script, ["Gabriola"] = Script,
        ["Lucida Handwriting"] = Script, ["Brush Script MT"] = Script, ["Mistral"] = Script,
        ["Cascadia Mono"] = Mono, ["Cascadia Code"] = Mono, ["Consolas"] = Mono,
    };
    private static HashSet<string>? system;
    internal static IReadOnlyCollection<string> SystemFonts =>
        system ??= new HashSet<string>(FontManager.Current.SystemFonts.Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);

    /// <summary>The name shown in Settings for a saved font, e.g. Bahnschrift → Barlow.</summary>
    internal static string DisplayName(string font) =>
        Substitutes.TryGetValue(font, out var s) && !SystemFonts.Contains(font) ? s : font;

    internal static FontFamily Family(string font)
    {
        string name = DisplayName(font);
        if (name == Sans) return new FontFamily("fonts:Inter#Inter");
        if (Bundled.Contains(name)) return new FontFamily("fonts:Still#" + name);
        return SystemFonts.Contains(name) ? new FontFamily(name) : new FontFamily("fonts:Inter#Inter");
    }
    internal static readonly FontFamily MonoFamily = new("fonts:Still#Share Tech Mono");

    internal static List<string> FontChoices(params string[] first)
    {
        var list = first.Concat(Bundled).Concat(SystemFonts.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list;
    }

    // ---- XDG autostart ----
    private static string AutostartFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart", "still.desktop");
    private static string Executable => Environment.ProcessPath ?? "still";
    private static string ExecLine => "Exec=\"" + Executable.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$") + "\"";

    internal static bool LoginEnabled()
    {
        try { return File.Exists(AutostartFile) && File.ReadLines(AutostartFile).Contains(ExecLine); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    internal static void SetLogin(bool enabled)
    {
        if (!enabled) { if (File.Exists(AutostartFile)) File.Delete(AutostartFile); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(AutostartFile)!);
        File.WriteAllText(AutostartFile, string.Join('\n',
            "[Desktop Entry]", "Type=Application", "Name=Still", "Comment=Futuristic desktop clock", ExecLine,
            "Icon=still", "Terminal=false", "X-GNOME-Autostart-enabled=true", "X-KDE-autostart-after=panel", ""));
    }

    // ---- X11 window hints (Avalonia runs through X11/XWayland) ----
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] private static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
    [DllImport("libX11.so.6")] private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate, IntPtr mask, ref ClientMessage message);
    [DllImport("libX11.so.6")] private static extern int XFlush(IntPtr display);
    [DllImport("libXext.so.6")] private static extern void XShapeCombineRectangles(IntPtr display, IntPtr window, int kind, int x, int y, IntPtr rectangles, int count, int operation, int ordering);
    [DllImport("libXext.so.6")] private static extern void XShapeCombineMask(IntPtr display, IntPtr window, int kind, int x, int y, IntPtr pixmap, int operation);

    [StructLayout(LayoutKind.Sequential, Size = 192)]
    private struct ClientMessage
    {
        public int Type; public IntPtr Serial; public int SendEvent; public IntPtr Display; public IntPtr Window;
        public IntPtr MessageType; public int Format; public IntPtr L0, L1, L2, L3, L4;
    }

    private static IntPtr display;
    private static bool x11Failed;
    private static IntPtr Connection()
    {
        if (display != IntPtr.Zero || x11Failed) return display;
        try { display = XOpenDisplay(IntPtr.Zero); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        x11Failed = display == IntPtr.Zero;
        return display;
    }

    private static IntPtr Xid(Window window) =>
        window.TryGetPlatformHandle() is { HandleDescriptor: "XID" } handle ? handle.Handle : IntPtr.Zero;

    /// <summary>Keeps the widget out of the taskbar, pager and Alt+Tab, and on every virtual desktop.</summary>
    internal static void ApplyWidgetHints(Window window)
    {
        IntPtr d = Connection(), w = Xid(window);
        if (d == IntPtr.Zero || w == IntPtr.Zero) return;
        try
        {
            State(d, w, 1, "_NET_WM_STATE_SKIP_TASKBAR", "_NET_WM_STATE_SKIP_PAGER");
            State(d, w, 1, "_KDE_NET_WM_STATE_SKIP_SWITCHER", "_NET_WM_STATE_STICKY");
            // The widget is never activated on purpose, so it does not need the WM's attention flag.
            State(d, w, 0, "_NET_WM_STATE_DEMANDS_ATTENTION", null);
            Send(d, w, "_NET_WM_DESKTOP", new IntPtr(0xFFFFFFFF), new IntPtr(1));
            XFlush(d);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
    }

    /// <summary>
    /// KDE Plasma does not apply the X11 "all desktops" request to XWayland windows when it has
    /// several virtual desktops, so on KDE ask KWin itself through its scripting interface.
    /// Runs in the background and does nothing elsewhere or if busctl is missing.
    /// </summary>
    internal static void PinToAllDesktopsOnKde()
    {
        string desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
        if (!desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase)) return;
        int pid = Environment.ProcessId;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } d ? d : Path.GetTempPath();
                string script = Path.Combine(dir, $"still-kwin-{pid}.js"), name = $"still-pin-{pid}";
                File.WriteAllText(script,
                    "for (const w of workspace.windowList()) {\n" +
                    $"  if (w.pid === {pid} && String(w.caption).indexOf(\"desktop clock\") >= 0) {{ w.onAllDesktops = true; w.skipTaskbar = true; w.skipPager = true; w.skipSwitcher = true; }}\n" +
                    "}\n");
                Busctl("org.kde.kwin.Scripting", "/Scripting", "unloadScript", "s", name);
                string? loaded = Busctl("org.kde.kwin.Scripting", "/Scripting", "loadScript", "ss", script, name);
                if (loaded != null && loaded.Trim().Split(' ') is [_, var id] && int.TryParse(id, out int number) && number >= 0)
                    Busctl("org.kde.kwin.Script", "/Scripting/Script" + number, "run", "");
                System.Threading.Thread.Sleep(500);
                Busctl("org.kde.kwin.Scripting", "/Scripting", "unloadScript", "s", name);
                File.Delete(script);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
        });
    }

    private static string? Busctl(string @interface, string path, string method, string signature, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo("busctl") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "--user", "call", "org.kde.KWin", path, @interface, method }) start.ArgumentList.Add(a);
        if (signature.Length > 0) { start.ArgumentList.Add(signature); foreach (var a in args) start.ArgumentList.Add(a); }
        using var process = System.Diagnostics.Process.Start(start);
        if (process == null) return null;
        string output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(3000)) { try { process.Kill(); } catch (InvalidOperationException) { } return null; }
        return process.ExitCode == 0 ? output : null;
    }

    /// <summary>
    /// Brings a window to the front. Requests marked as coming from a pager/taskbar (source 2) are
    /// honoured by window managers' focus-stealing prevention, unlike an app activating itself.
    /// </summary>
    internal static void BringToFront(Window window, bool activate)
    {
        IntPtr d = Connection(), w = Xid(window);
        if (d == IntPtr.Zero || w == IntPtr.Zero) return;
        try
        {
            if (activate) Send(d, w, "_NET_ACTIVE_WINDOW", new IntPtr(2), IntPtr.Zero, IntPtr.Zero);
            else Send(d, w, "_NET_RESTACK_WINDOW", new IntPtr(2), IntPtr.Zero, IntPtr.Zero /* Above */);
            XFlush(d);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
    }

    /// <summary>Before the widget is mapped: ask to be placed on all virtual desktops (EWMH honours this at map time).</summary>
    internal static void PrepareWidget(Window window)
    {
        IntPtr d = Connection(), w = Xid(window);
        if (d == IntPtr.Zero || w == IntPtr.Zero) return;
        try
        {
            long all = 0xFFFFFFFF;
            XChangeProperty(d, w, XInternAtom(d, "_NET_WM_DESKTOP", false), new IntPtr(6) /* CARDINAL */, 32, 0, ref all, 1);
            XFlush(d);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
    }
    [DllImport("libX11.so.6")] private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, ref long data, int count);

    private static void State(IntPtr d, IntPtr w, int action, string first, string? second) =>
        Send(d, w, "_NET_WM_STATE", new IntPtr(action), XInternAtom(d, first, false), second == null ? IntPtr.Zero : XInternAtom(d, second, false), new IntPtr(1));

    private static void Send(IntPtr d, IntPtr w, string type, params IntPtr[] data)
    {
        var message = new ClientMessage { Type = 33, SendEvent = 1, Display = d, Window = w, MessageType = XInternAtom(d, type, false), Format = 32 };
        if (data.Length > 0) message.L0 = data[0];
        if (data.Length > 1) message.L1 = data[1];
        if (data.Length > 2) message.L2 = data[2];
        if (data.Length > 3) message.L3 = data[3];
        XSendEvent(d, XDefaultRootWindow(d), false, new IntPtr((1 << 20) | (1 << 19)), ref message);
    }

    /// <summary>Click-through: an empty X11 input shape passes the pointer to windows beneath. Returns false if unavailable.</summary>
    internal static bool SetClickThrough(Window window, bool clickThrough)
    {
        IntPtr d = Connection(), w = Xid(window);
        if (d == IntPtr.Zero || w == IntPtr.Zero) return !clickThrough;
        try
        {
            const int ShapeInput = 2, ShapeSet = 0;
            if (clickThrough) XShapeCombineRectangles(d, w, ShapeInput, 0, 0, IntPtr.Zero, 0, ShapeSet, 0);
            else XShapeCombineMask(d, w, ShapeInput, 0, 0, IntPtr.Zero, ShapeSet);
            XFlush(d);
            return true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return !clickThrough; }
    }
}
