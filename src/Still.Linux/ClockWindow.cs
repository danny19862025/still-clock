using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using CorePoint = Still.Core.PixelPoint;

namespace Still.Linux;

public sealed class ClockWindow : Window
{
    public ClockFace Face { get; } = new();
    private readonly ClockApplication app;
    private readonly DispatcherTimer moveSave;
    private readonly Border resizeGrip;
    private readonly Grid root = new();
    private readonly Viewbox faceHost;
    private bool restoring, ready, scaling, clickThroughApplied;
    private PixelPoint scaleStart;
    private double initialScale;

    public ClockWindow(ClockApplication application)
    {
        app = application;
        Title = "Still — desktop clock";
        SystemDecorations = SystemDecorations.None; CanResize = false;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Icon = ClockApplication.AppIcon;

        faceHost = new Viewbox { Child = Face, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        root.Children.Add(faceHost);
        resizeGrip = new Border
        {
            Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner), Background = Brushes.Transparent, Opacity = 0,
            Child = new TextBlock { Text = "◢", FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255)), HorizontalAlignment = HorizontalAlignment.Center }
        };
        root.Children.Add(resizeGrip); Content = root;

        resizeGrip.PointerPressed += BeginScale;
        resizeGrip.PointerMoved += ScaleMove;
        resizeGrip.PointerReleased += (_, e) => { if (!scaling) return; e.Pointer.Capture(null); scaling = false; app.SaveSoon(); RestorePosition(); e.Handled = true; };
        resizeGrip.PointerCaptureLost += (_, _) => scaling = false;
        PointerEntered += (_, _) => { if (!app.Preferences.Locked) resizeGrip.Opacity = .8; };
        PointerExited += (_, _) => { if (!scaling) resizeGrip.Opacity = 0; };
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Handled) return;
            if (e.ClickCount == 2) { app.OpenSettings(); return; }
            if (app.Preferences.Locked) return;
            BeginMoveDrag(e);
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
        PositionChanged += (_, _) => { if (!restoring && ready && !scaling) { moveSave.Stop(); moveSave.Start(); } };
        Opened += (_, _) =>
        {
            clickThroughApplied = false; ApplyHintsSoon();
            if (!ready) { ready = true; RestorePosition(); }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) app.Unlock();
            if (e.Key == Key.F2 || (e.Key == Key.OemComma && e.KeyModifiers == KeyModifiers.Control)) app.OpenSettings();
        };
    }

    /// <summary>Shows the widget and reapplies hints the window manager drops when a window is unmapped.</summary>
    public void ShowClock()
    {
        // Re-showing a visible window would make the window manager flag it as demanding attention.
        if (IsVisible && WindowState == WindowState.Normal) return;
        bool wasVisible = IsVisible;
        if (!wasVisible) LinuxDesktop.PrepareWidget(this);
        Show(); WindowState = WindowState.Normal;
        if (!wasVisible) { clickThroughApplied = false; ApplyHintsSoon(); }
    }

    // Window managers ignore requests for windows they are still managing, so send the hints twice.
    private void ApplyHintsSoon()
    {
        Dispatcher.UIThread.Post(() => { LinuxDesktop.ApplyWidgetHints(this); ApplyInteraction(); }, DispatcherPriority.Background);
        DispatcherTimer.RunOnce(() => { if (IsVisible) LinuxDesktop.ApplyWidgetHints(this); }, TimeSpan.FromMilliseconds(600));
    }

    private void BeginScale(object? sender, PointerPressedEventArgs e)
    {
        if (app.Preferences.Locked || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        scaling = true; scaleStart = this.PointToScreen(e.GetPosition(this)); initialScale = app.Preferences.Scale;
        e.Pointer.Capture(resizeGrip); e.Handled = true;
    }
    private void ScaleMove(object? sender, PointerEventArgs e)
    {
        if (!scaling) return;
        var point = this.PointToScreen(e.GetPosition(this));
        app.Preferences.Scale = Math.Clamp(initialScale + (point.X - scaleStart.X) / (380 * RenderScaling), .5, 2);
        app.Apply(); e.Handled = true;
    }

    private bool ClickThroughWanted => app.Preferences.Locked && app.Preferences.ClickThrough;
    private void ApplyInteraction()
    {
        if (!IsVisible) return;
        // A freshly mapped window takes input everywhere, so only changes need an X11 call.
        bool wanted = ClickThroughWanted;
        if (wanted == clickThroughApplied) return;
        bool ok = LinuxDesktop.SetClickThrough(this, wanted);
        app.InteractionNotice = ok ? null : "Click-through is not available in this desktop session.";
        clickThroughApplied = ok && wanted;
    }

    public void Apply(DateTime now)
    {
        Topmost = app.Preferences.Topmost;
        Face.Update(app.Preferences, now);
        resizeGrip.IsVisible = !app.Preferences.Locked;
        ApplyInteraction();
        if (ready && !scaling) Dispatcher.UIThread.Post(RestorePosition, DispatcherPriority.Background);
    }

    public void RestorePosition()
    {
        if (!ready) return;
        var displays = LinuxDesktop.Displays(this);
        if (displays.Count == 0) return;
        restoring = true;
        var target = displays.FirstOrDefault(d => d.Name == app.Preferences.Position?.Display) ?? displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        faceHost.MaxWidth = target.Width / target.DpiScale;
        faceHost.MaxHeight = target.Height / target.DpiScale;
        root.Measure(Size.Infinity);
        var size = root.DesiredSize;
        CorePoint point = Placement.Restore(app.Preferences.Position, displays, size.Width, size.Height);
        Position = new PixelPoint((int)Math.Round(point.X), (int)Math.Round(point.Y));
        Dispatcher.UIThread.Post(() => { restoring = false; CapturePosition(); }, DispatcherPriority.Background);
    }

    public void CapturePosition()
    {
        if (!ready || restoring || !IsVisible) return;
        var displays = LinuxDesktop.Displays(this);
        double scaling = RenderScaling;
        double left = Position.X, top = Position.Y, right = left + Bounds.Width * scaling, bottom = top + Bounds.Height * scaling;
        // Largest intersection, rather than nearest origin, works across negative-coordinate displays.
        var display = displays.MaxBy(d => Math.Max(0, Math.Min(right, d.X + d.Width) - Math.Max(left, d.X)) * Math.Max(0, Math.Min(bottom, d.Y + d.Height) - Math.Max(top, d.Y)));
        if (display == null) return;
        app.Preferences.Position = Placement.Capture(display, left, top);
        app.SaveSoon();
    }
}
