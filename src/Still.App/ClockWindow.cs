using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Still.App;

public sealed class ClockWindow : Window
{
    public ClockFace Face { get; } = new();
    private readonly ClockApplication app;
    private readonly DispatcherTimer moveSave;
    private bool restoring;
    private bool ready;
    private readonly Border resizeGrip;
    private readonly Grid root = new();
    private readonly Viewbox faceHost;
    private System.Windows.Threading.DispatcherOperation? pendingRestore;

    public ClockWindow(ClockApplication application)
    {
        app = application;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Still — desktop clock";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; SizeToContent = SizeToContent.WidthAndHeight;
        faceHost = new Viewbox { Child = Face, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        root.Children.Add(faceHost);
        resizeGrip = new Border
        {
            Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE,
            Background = Brushes.Transparent, Opacity = 0,
            Child = new TextBlock { Text = "◢", FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(182, 205, 189)), HorizontalAlignment = HorizontalAlignment.Center }
        };
        root.Children.Add(resizeGrip); Content = root;
        resizeGrip.MouseLeftButtonDown += BeginScale;
        resizeGrip.MouseMove += ScaleMove;
        resizeGrip.MouseLeftButtonUp += (_, e) => { resizeGrip.ReleaseMouseCapture(); scaling = false; app.SaveSoon(); RestorePosition(); e.Handled = true; };
        resizeGrip.LostMouseCapture += (_, _) => { scaling = false; };
        MouseEnter += (_, _) => { if (!app.Preferences.Locked) resizeGrip.Opacity = .8; };
        MouseLeave += (_, _) => { if (!scaling) resizeGrip.Opacity = 0; };
        MouseLeftButtonDown += (_, e) =>
        {
            if (app.Preferences.Locked) return;
            if (e.ClickCount == 2) { app.OpenSettings(); return; }
            try { DragMove(); CapturePosition(); RestorePosition(); } catch (InvalidOperationException) { }
        };
        var context = new ContextMenu();
        void Item(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); context.Items.Add(item); }
        Item("_Settings…", app.OpenSettings);
        Item("_Unlock and restore interaction", app.Unlock);
        Item("_Lock position", () => { app.Preferences.Locked = true; app.Apply(); });
        Item("_Hide clock", app.ToggleVisibility);
        Item("E_xit Still", app.ExitApp);
        ContextMenu = context;
        Closing += (_, e) =>
        {
            if (app.IsExiting) return;
            e.Cancel = true; Hide(); app.RefreshTime();
        };
        moveSave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        moveSave.Tick += (_, _) => { moveSave.Stop(); if (!restoring && ready) CapturePosition(); };
        LocationChanged += (_, _) => { if (!restoring && ready) { moveSave.Stop(); moveSave.Start(); } };
        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
            NativeDesktop.SetInteraction(this, app.Preferences.Locked && app.Preferences.ClickThrough);
        };
        ContentRendered += (_, _) => { if (!ready) { ready = true; RestorePosition(); } };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) app.Unlock();
            if (e.Key == Key.F2 || (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)) app.OpenSettings();
        };
    }

    private bool scaling;
    private Point scaleStart;
    private double initialScale;
    private void BeginScale(object sender, MouseButtonEventArgs e)
    {
        if (app.Preferences.Locked) return;
        scaling = true; scaleStart = PointToScreen(e.GetPosition(this)); initialScale = app.Preferences.Scale;
        resizeGrip.CaptureMouse(); e.Handled = true;
    }
    private void ScaleMove(object sender, MouseEventArgs e)
    {
        if (!scaling || e.LeftButton != MouseButtonState.Pressed) return;
        Point point = PointToScreen(e.GetPosition(this));
        app.Preferences.Scale = Math.Clamp(initialScale + (point.X - scaleStart.X) / (380 * VisualTreeHelper.GetDpi(this).DpiScaleX), .5, 2);
        app.Apply(); e.Handled = true;
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)message == NativeDesktop.TaskbarCreated) app.RestoreTray();
        if (message is 0x001E or 0x0218 or 0x001A) { TimeZoneInfo.ClearCachedData(); app.RefreshTime(); } // time, power, settings/timezone.
        if (message == 0x007E) Dispatcher.BeginInvoke(() => RestorePosition(), DispatcherPriority.ContextIdle);
        if (message == 0x02E0) Dispatcher.BeginInvoke(() => { Face.Update(app.Preferences, DateTime.Now); app.RefreshTime(); }, DispatcherPriority.ContextIdle);
        return IntPtr.Zero;
    }

    public void Apply(DateTime now)
    {
        Topmost = app.Preferences.Topmost;
        Face.Update(app.Preferences, now);
        resizeGrip.Visibility = app.Preferences.Locked ? Visibility.Collapsed : Visibility.Visible;
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero) NativeDesktop.SetInteraction(this, app.Preferences.Locked && app.Preferences.ClickThrough);
        if (ready && !scaling)
        {
            pendingRestore?.Abort();
            pendingRestore = Dispatcher.BeginInvoke(() => RestorePosition(), DispatcherPriority.ContextIdle);
        }
    }

    public void RestorePosition()
    {
        if (!ready) return;
        var displays = NativeDesktop.Displays();
        if (displays.Count == 0) return;
        restoring = true;
        var target = displays.FirstOrDefault(d => d.Name == app.Preferences.Position?.Display) ?? displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        faceHost.MaxWidth = target.Width / target.DpiScale;
        faceHost.MaxHeight = target.Height / target.DpiScale;
        UpdateLayout();
        var point = Placement.Restore(app.Preferences.Position, displays, ActualWidth, ActualHeight);
        NativeDesktop.Move(this, point);
        // WPF applies target-monitor DPI during the move. Clamp again once layout settles.
        Dispatcher.BeginInvoke(() =>
        {
            UpdateLayout();
            var currentDisplays = NativeDesktop.Displays();
            if (currentDisplays.Count > 0) NativeDesktop.Move(this, Placement.Restore(app.Preferences.Position, currentDisplays, ActualWidth, ActualHeight));
            restoring = false; CapturePosition();
        }, DispatcherPriority.ContextIdle);
    }

    public void CapturePosition()
    {
        if (!ready || restoring) return;
        if (!NativeDesktop.GetWindowRect(new WindowInteropHelper(this).Handle, out var rect)) return;
        var displays = NativeDesktop.Displays();
        // Largest intersection, rather than nearest origin, works across negative-coordinate displays.
        var display = displays.MaxBy(d => Math.Max(0, Math.Min(rect.Right, d.X + d.Width) - Math.Max(rect.Left, d.X)) * Math.Max(0, Math.Min(rect.Bottom, d.Y + d.Height) - Math.Max(rect.Top, d.Y)));
        if (display == null) return;
        app.Preferences.Position = Placement.Capture(display, rect.Left, rect.Top);
        app.SaveSoon();
    }
}
