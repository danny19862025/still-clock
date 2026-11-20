using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Still.Linux;

public partial class ClockApplication : Application
{
    internal static WindowIcon AppIcon => icon ??= new WindowIcon(AssetLoader.Open(new Uri("avares://still/Assets/still.png")));
    private static WindowIcon? icon;

    public Preferences Preferences { get; private set; } = new();
    // XDG_CONFIG_HOME/still (normally ~/.config/still). --verify uses a throwaway profile beside the binary.
    public SettingsStore Store { get; } = new(Program.Verify
        ? Path.Combine(AppContext.BaseDirectory, "screenshots", "profile")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "still"));
    public ClockWindow Clock { get; private set; } = null!;
    private SettingsWindow? settings;
    private TrayIcon? tray;
    private NativeMenuItem? showItem, lockItem;
    private DispatcherTimer tick = null!, save = null!;
    private FileSystemWatcher? zoneWatcher;
    private IClassicDesktopStyleApplicationLifetime? desktop;
    private readonly List<PosixSignalRegistration> signals = [];
    private bool exiting, started;
    public bool IsExiting => exiting;
    public event Action? Updated;
    public string? StartupNotice { get; private set; }
    public string? InteractionNotice { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime) return;
        desktop = lifetime;
        var args = lifetime.Args ?? [];
        started = true;
        Preferences = Store.Load();
        if (Program.Verify) Preferences.ApplyPreset("Neon");
        // Read the autostart entry rather than trusting a stale JSON preference.
        Preferences.LaunchAtLogin = LinuxDesktop.LoginEnabled();
        Ui.Apply(this, args.Contains("--light") ? "Light" : Preferences.UiTheme);
        if (PlatformSettings != null)
            PlatformSettings.ColorValuesChanged += (_, _) => Dispatcher.UIThread.Post(() => { if (Preferences.UiTheme == "System") ApplyUiTheme(); });
        tick = new DispatcherTimer(DispatcherPriority.Background);
        tick.Tick += (_, _) => RefreshTime();
        save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        save.Tick += (_, _) => { save.Stop(); Store.Save(Preferences); settings?.UpdateStatus(); };
        Clock = new ClockWindow(this);
        CreateTray();
        Apply(); Clock.ShowClock();
        RefreshTime(); // Apply() ran before the window was visible, so start the tick now.
        Clock.Screens.Changed += (_, _) => Dispatcher.UIThread.Post(() => { Clock.RestorePosition(); RefreshTime(); });
        WatchTimeZone();
        lifetime.ShutdownRequested += (_, _) => { if (!exiting) { exiting = true; Clock.CapturePosition(); Store.Save(Preferences); } };
        lifetime.Exit += (_, _) => { if (started) Store.Save(Preferences); zoneWatcher?.Dispose(); tray?.Dispose(); };
        Program.Listen(message => { if (message == "show") { Unlock(); OpenSettings(); } });
        // Logout, `kill` and the installer send SIGTERM: save and close like Exit Still.
        foreach (var signal in new[] { PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP })
            signals.Add(PosixSignalRegistration.Create(signal, context => { context.Cancel = true; Dispatcher.UIThread.Post(ExitApp); }));

        if (args.Contains("--capture") || args.Contains("--verify")) Dispatcher.UIThread.Post(() => _ = CaptureScreenshots(), DispatcherPriority.ApplicationIdle);
        else if (args.Contains("--settings") || !File.Exists(Store.FilePath) || Store.Notice != null) OpenSettings();
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
            // The timer runs on a monotonic clock that pauses during suspend, so never wait more
            // than a few seconds: the clock resamples DateTime.Now promptly after resume or a clock change.
            var next = TemplateFormatter.NextUpdate(now, Preferences);
            tick.Interval = next < TimeSpan.FromSeconds(10) ? next : TimeSpan.FromSeconds(10);
            tick.Start();
        }
    }

    public void Apply()
    {
        Preferences.Normalize();
        Clock.Apply(DateTime.Now);
        RefreshTime(); SaveSoon(); UpdateTrayMenu(); Updated?.Invoke();
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
        FrontSoon(settings, true);
    }

    // A new window is still being managed when it first appears, so ask twice.
    private static void FrontSoon(Window window, bool activate)
    {
        Dispatcher.UIThread.Post(() => LinuxDesktop.BringToFront(window, activate), DispatcherPriority.Background);
        DispatcherTimer.RunOnce(() => { if (window.IsVisible) LinuxDesktop.BringToFront(window, activate); }, TimeSpan.FromMilliseconds(400));
    }
    public void Unlock()
    {
        Preferences.ClickThrough = false; Preferences.Locked = false;
        Clock.ShowClock();
        Apply(); Clock.RestorePosition(); settings?.SyncBehavior();
        FrontSoon(Clock, false);
    }
    public void ToggleVisibility()
    {
        if (Clock.IsVisible && Clock.WindowState != WindowState.Minimized) { Clock.CapturePosition(); Clock.Hide(); }
        else { Clock.ShowClock(); Clock.RestorePosition(); FrontSoon(Clock, false); }
        RefreshTime(); UpdateTrayMenu();
    }
    public void Reset()
    {
        var position = Preferences.Position; var uiTheme = Preferences.UiTheme;
        SetLogin(false);
        Preferences = Preferences.CreateDefault(); Preferences.Position = position; Preferences.LaunchAtLogin = LinuxDesktop.LoginEnabled(); Preferences.UiTheme = uiTheme;
        Apply(); Clock.RestorePosition();
    }

    private AboutWindow? about;
    public void OpenAbout() => OpenAbout(false);
    public void OpenAbout(bool checkForUpdates)
    {
        if (about == null)
        {
            about = new AboutWindow(checkForUpdates);
            about.Closed += (_, _) => about = null;
        }
        else if (checkForUpdates) _ = about.CheckAsync();
        about.Show(); about.Activate();
        FrontSoon(about, true);
    }

    /// <summary>Re-reads the Dark/Light/System choice; open windows are rebuilt when the palette changes.</summary>
    public void ApplyUiTheme(bool force = false, string? page = null)
    {
        bool changed = Ui.Apply(this, Preferences.UiTheme);
        if (!changed && !force) return;
        if (settings != null)
        {
            string current = page ?? settings.CurrentPage;
            var position = settings.Position; var size = new Size(settings.Width, settings.Height);
            settings.Close();
            OpenSettings();
            settings!.Position = position; settings.Width = size.Width; settings.Height = size.Height;
            settings.Go(current);
        }
        if (about != null) { about.Close(); OpenAbout(); }
    }

    private void CreateTray()
    {
        var menu = new NativeMenu();
        NativeMenuItem Item(string header, Action action) { var item = new NativeMenuItem(header); item.Click += (_, _) => action(); menu.Items.Add(item); return item; }
        Item("Settings…", OpenSettings);
        showItem = Item("Hide clock", ToggleVisibility);
        lockItem = Item("Lock position", () => { if (Preferences.Locked) Unlock(); else { Preferences.Locked = true; Apply(); settings?.SyncBehavior(); } });
        Item("Unlock and restore interaction", Unlock);
        menu.Items.Add(new NativeMenuItemSeparator());
        Item("About Still…", () => OpenAbout(false));
        Item("Check for updates…", () => OpenAbout(true));
        Item("Buy Me a Coffee…", () => AboutWindow.Open(Links.Coffee));
        menu.Items.Add(new NativeMenuItemSeparator());
        Item("Exit Still", ExitApp);
        tray = new TrayIcon { Icon = AppIcon, ToolTipText = "Still • futuristic desktop clock", Menu = menu, IsVisible = true };
        tray.Clicked += (_, _) => OpenSettings();
        TrayIcon.SetIcons(this, [tray]);
    }
    private void UpdateTrayMenu()
    {
        if (showItem != null) showItem.Header = Clock.IsVisible ? "Hide clock" : "Show clock";
        if (lockItem != null) lockItem.Header = Preferences.Locked ? "Unlock position" : "Lock position";
    }

    // Timezone changes on Linux replace the /etc/localtime link; .NET caches the old zone until told.
    private void WatchTimeZone()
    {
        try
        {
            zoneWatcher = new FileSystemWatcher("/etc", "localtime") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime };
            void Changed(object? s, EventArgs e) => Dispatcher.UIThread.Post(() => { TimeZoneInfo.ClearCachedData(); RefreshTime(); });
            zoneWatcher.Changed += Changed; zoneWatcher.Created += Changed; zoneWatcher.Renamed += Changed; zoneWatcher.Deleted += Changed;
            zoneWatcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException) { zoneWatcher = null; }
    }

    public bool SetLogin(bool enabled)
    {
        try
        {
            LinuxDesktop.SetLogin(enabled);
            Preferences.LaunchAtLogin = enabled; StartupNotice = null; SaveSoon(); return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            StartupNotice = "The autostart entry could not be changed. Your previous startup choice is still active.";
            Preferences.LaunchAtLogin = LinuxDesktop.LoginEnabled();
            return false;
        }
    }

    public void ExitApp()
    {
        if (exiting) return;
        Clock.CapturePosition(); Store.Save(Preferences); exiting = true;
        tick.Stop(); save.Stop();
        desktop?.Shutdown();
    }

    // ---- Renders of every preset and Settings, for documentation and checking ----
    private async System.Threading.Tasks.Task CaptureScreenshots()
    {
        string dir = Path.Combine(Program.Verify ? AppContext.BaseDirectory : Environment.CurrentDirectory, "screenshots"); Directory.CreateDirectory(dir);
        foreach (string preset in Preferences.Presets)
        {
            var p = new Preferences(); p.ApplyPreset(preset);
            var face = new ClockFace(); face.Update(p, new DateTime(2026, 11, 12, 9, 41, 28));
            var background = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, .7, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromRgb(8, 14, 30), 0), new GradientStop(Color.FromRgb(40, 18, 60), 1) }
                },
                Padding = new Thickness(48), Child = face, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
            };
            SaveVisual(background, Path.Combine(dir, preset.ToLowerInvariant().Replace(' ', '-') + ".png"));
        }
        OpenSettings();
        await System.Threading.Tasks.Task.Delay(1500);
        if (settings != null) SaveVisual(settings, Path.Combine(dir, "settings.png"));
        OpenAbout();
        await System.Threading.Tasks.Task.Delay(800);
        if (about != null) SaveVisual(about, Path.Combine(dir, "about.png"));
        Console.WriteLine("Screenshots saved to " + dir);
        if (Program.Verify) ExitApp();
    }
    private static void SaveVisual(Control element, string path)
    {
        if (element is not TopLevel)
        {
            element.Measure(Size.Infinity);
            element.Arrange(new Rect(element.DesiredSize));
        }
        var size = element.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width * 1.5), (int)Math.Ceiling(size.Height * 1.5)), new Vector(144, 144));
        bitmap.Render(element);
        bitmap.Save(path);
    }
}
