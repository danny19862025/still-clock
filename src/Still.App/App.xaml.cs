using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Still.App;

public partial class ClockApplication : Application
{
    public Preferences Preferences { get; private set; } = new();
    public SettingsStore Store { get; } = new(Environment.GetCommandLineArgs().Contains("--verify") ? Path.Combine(AppContext.BaseDirectory, "screenshots", "profile") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Still"));
    public ClockWindow Clock { get; private set; } = null!;
    private SettingsWindow? settings;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private Mutex? instance;
    private DispatcherTimer tick = null!, save = null!;
    private bool exiting;
    private bool started;
    public bool IsExiting => exiting;
    public event Action? Updated;
    public string? StartupNotice { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, e.Args.Contains("--verify") ? "Local\\Still.Clock.Verify" : "Local\\Still.Clock.SingleInstance", out bool first);
        if (!first) { System.Windows.MessageBox.Show("Still is already running. Use its clock icon in the notification area to open Settings or unlock it.", "Still"); Shutdown(); return; }
        started = true;
        Preferences = Store.Load();
        if (e.Args.Contains("--verify")) Preferences.ApplyPreset("Glass");
        if (!Fonts.SystemFontFamilies.Any(f => f.Source == Preferences.Font)) Preferences.Font = "Segoe UI";
        // Read our per-user Run entry rather than trusting a stale JSON preference.
        Preferences.LaunchAtLogin = LoginEnabled();
        tick = new DispatcherTimer(DispatcherPriority.Background);
        tick.Tick += (_, _) => RefreshTime();
        save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        save.Tick += (_, _) => { save.Stop(); Store.Save(Preferences); settings?.UpdateStatus(); };
        Clock = new ClockWindow(this); MainWindow = Clock;
        CreateTray();
        Apply(); Clock.Show();
        SystemEvents.TimeChanged += SystemTimeChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        if (e.Args.Contains("--capture") || e.Args.Contains("--verify")) Dispatcher.BeginInvoke(() => CaptureScreenshots(), DispatcherPriority.ApplicationIdle);
        else if (e.Args.Contains("--settings") || !File.Exists(Store.FilePath) || Store.Notice != null) OpenSettings();
    }

    public void RefreshTime()
    {
        if (exiting || Clock == null) return;
        tick.Stop();
        var now = DateTime.Now;
        if (Clock.IsVisible) Clock.Face.Update(Preferences, now);
        settings?.RefreshPreview(now);
        if (Clock.IsVisible || settings?.IsVisible == true)
        {
            tick.Interval = TemplateFormatter.NextUpdate(now, Preferences);
            tick.Start();
        }
    }

    public void Apply()
    {
        Preferences.Normalize();
        Clock.Apply(DateTime.Now);
        RefreshTime(); SaveSoon(); Updated?.Invoke();
    }
    public void SaveSoon() { if (save == null) return; save.Stop(); save.Start(); }
    public void OpenSettings()
    {
        if (settings == null)
        {
            settings = new SettingsWindow(this);
            settings.Closed += (_, _) => { settings = null; RefreshTime(); };
        }
        settings.Show(); settings.WindowState = WindowState.Normal; settings.Activate(); RefreshTime();
    }
    public void Unlock()
    {
        Preferences.ClickThrough = false; Preferences.Locked = false;
        if (!Clock.IsVisible) Clock.Show();
        Clock.WindowState = WindowState.Normal;
        Apply(); Clock.RestorePosition(); settings?.SyncBehavior();
    }
    public void ToggleVisibility()
    {
        if (Clock.IsVisible && Clock.WindowState != WindowState.Minimized) Clock.Hide(); else { Clock.Show(); Clock.WindowState = WindowState.Normal; Clock.RestorePosition(); }
        RefreshTime();
    }
    public void Reset()
    {
        var position = Preferences.Position;
        SetLogin(false);
        Preferences = new Preferences { Position = position, LaunchAtLogin = LoginEnabled() };
        Apply(); Clock.RestorePosition();
    }

    private void CreateTray()
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/still.ico")).Stream;
        trayIcon = new System.Drawing.Icon(stream);
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "Still • desktop clock", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        var show = menu.Items.Add("Hide clock", null, (_, _) => Dispatcher.Invoke(ToggleVisibility));
        var locked = menu.Items.Add("Lock position", null, (_, _) => Dispatcher.Invoke(() =>
        {
            if (Preferences.Locked) Unlock(); else { Preferences.Locked = true; Apply(); settings?.SyncBehavior(); }
        }));
        menu.Items.Add("Unlock and restore interaction", null, (_, _) => Dispatcher.Invoke(Unlock));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit Still", null, (_, _) => Dispatcher.Invoke(ExitApp));
        menu.Opening += (_, _) => { show.Text = Clock.IsVisible && Clock.WindowState != WindowState.Minimized ? "Hide clock" : "Show clock"; locked.Text = Preferences.Locked ? "Unlock position" : "Lock position"; };
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
    }
    public void RestoreTray() { if (tray != null) { tray.Visible = false; tray.Visible = true; } }
    private void SystemTimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { TimeZoneInfo.ClearCachedData(); RefreshTime(); });
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(() => { TimeZoneInfo.ClearCachedData(); RefreshTime(); });
    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { Clock.RestorePosition(); RefreshTime(); });

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static bool LoginEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("Still") is string value && value == LoginCommand(); }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return false; }
    }
    private static string LoginCommand() => "\"" + Environment.ProcessPath + "\"";
    public bool SetLogin(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue("Still", LoginCommand()); else key.DeleteValue("Still", false);
            Preferences.LaunchAtLogin = enabled; StartupNotice = null; SaveSoon(); return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { StartupNotice = "Windows did not allow the startup change. Your previous startup choice is still active."; return false; }
    }

    public void ExitApp()
    {
        if (exiting) return;
        Clock.CapturePosition(); Store.Save(Preferences); exiting = true;
        tick.Stop(); save.Stop(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (started) Store.Save(Preferences);
        SystemEvents.TimeChanged -= SystemTimeChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        tray?.Dispose(); trayIcon?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        exiting = true; Clock.CapturePosition(); Store.Save(Preferences); base.OnSessionEnding(e);
    }

    private void CaptureScreenshots()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "screenshots"); Directory.CreateDirectory(dir);
        foreach (string preset in new[] { "Minimal", "Typography", "Glass" })
        {
            var p = new Preferences(); p.ApplyPreset(preset);
            var face = new ClockFace(); face.Update(p, new DateTime(2026, 11, 12, 9, 41, 28));
            var background = new Border { Background = new LinearGradientBrush(Color.FromRgb(26, 51, 59), Color.FromRgb(58, 78, 68), 35), Padding = new Thickness(48), Child = face };
            background.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); background.Arrange(new Rect(background.DesiredSize)); background.UpdateLayout();
            SaveVisual(background, Path.Combine(dir, preset.ToLowerInvariant() + ".png"));
        }
        OpenSettings(); settings!.UpdateLayout();
        SaveVisual(settings.Content as FrameworkElement ?? settings, Path.Combine(dir, "settings.png"));
        if (Environment.GetCommandLineArgs().Contains("--verify")) { ExitApp(); return; }
        System.Windows.MessageBox.Show("Screenshots saved to " + dir, "Still");
    }
    private static void SaveVisual(FrameworkElement element, string path)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * 1.5), (int)Math.Ceiling(element.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
