using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Data;
using VectorPath = System.Windows.Shapes.Path;

namespace Still.App;

/// <summary>About Still: who made it, the GitHub repository, Buy Me a Coffee, and an on-demand update check.</summary>
public sealed class AboutWindow : Window
{
    public static Version CurrentVersion => typeof(AboutWindow).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas");

    private readonly ClockApplication app;
    private readonly TextBlock updateStatus = new();
    private readonly Button check = new();
    private readonly Button install = new();
    private readonly ProgressBar progress = new();
    private ReleaseInfo? latest;
    private CancellationTokenSource? busy;

    public AboutWindow(ClockApplication application, bool checkNow = false)
    {
        app = application;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "About Still";
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/still.ico"));
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height; Width = 760;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        Background = Ui.Window;
        BorderBrush = Ui.Border; BorderThickness = new Thickness(1);

        var root = new DockPanel { Background = Ui.Window };
        Content = root;

        // Header bar: title and a close button.
        var header = new Border { Background = Ui.Titlebar, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Height = 48 };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var headerGrid = new Grid(); header.Child = headerGrid;
        headerGrid.Children.Add(new TextBlock { Text = "About Still", FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var closeX = new Button { Content = "×", Width = 46, FontSize = 20, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "Close About" };
        AutomationProperties.SetName(closeX, "Close About");
        closeX.Click += (_, _) => Close();
        headerGrid.Children.Add(closeX);
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        var body = new StackPanel { Margin = new Thickness(30, 24, 30, 24) };
        root.Children.Add(body);

        // Hero: icon, name, version, author.
        var hero = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var icon = ClockBadge(72);
        ((FrameworkElement)icon).Margin = new Thickness(0, 0, 20, 0);
        DockPanel.SetDock(icon, Dock.Left); hero.Children.Add(icon);
        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new TextBlock { Text = "Still", FontSize = 30, FontWeight = FontWeights.SemiBold });
        identity.Children.Add(new TextBlock { Text = $"Version {CurrentVersion.ToString(3)}", FontFamily = Mono, FontSize = 12.5, Foreground = Ui.Muted, Margin = new Thickness(0, 2, 0, 4) });
        identity.Children.Add(new TextBlock { Text = $"Created by {Links.Author}", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Ui.Accent });
        hero.Children.Add(identity);
        body.Children.Add(hero);

        // Overview: description beside the links.
        var overview = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        overview.ColumnDefinitions.Add(new ColumnDefinition()); overview.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        overview.Children.Add(new TextBlock
        {
            Text = "A futuristic, deeply customizable desktop clock. Pick from presets, poster and script layouts, glow, frames and a calendar, then make it yours.\n\n" +
                   "No account, telemetry or background network use. Still only contacts GitHub when you press Check for updates.",
            TextWrapping = TextWrapping.Wrap, Foreground = Ui.Ink, FontSize = 13.5, LineHeight = 21, Margin = new Thickness(0, 0, 24, 0)
        });
        var links = new StackPanel { VerticalAlignment = VerticalAlignment.Top, MinWidth = 250 };
        Grid.SetColumn(links, 1); overview.Children.Add(links);
        var repo = LinkButton(GitHubIcon(20), "still-clock · GitHub", Links.Repository, "Open Still's source code on GitHub");
        links.Children.Add(repo);
        var coffee = LinkButton(CoffeeIcon(22), "Buy Me a Coffee", Links.Coffee, "Support Danny on Buy Me a Coffee");
        coffee.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00)); coffee.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x0A));
        coffee.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xC2, 0x00)); coffee.FontWeight = FontWeights.SemiBold;
        coffee.Margin = new Thickness(0, 10, 0, 0);
        AutomationProperties.SetName(coffee, "Buy Me a Coffee — support Danny");
        links.Children.Add(coffee);
        var issues = LinkButton(null, "Report a problem ↗", Links.Issues, "Open the issue tracker on GitHub");
        issues.Margin = new Thickness(0, 10, 0, 0);
        links.Children.Add(issues);
        body.Children.Add(overview);

        // Updates.
        var updates = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(18, 14, 18, 14), Background = Ui.Control };
        var updatesPanel = new StackPanel(); updates.Child = updatesPanel;
        updatesPanel.Children.Add(new TextBlock { Text = "// UPDATES", FontFamily = Mono, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.Accent, Margin = new Thickness(0, 0, 0, 10) });
        var row = new WrapPanel();
        check.Content = "Check for _updates"; check.Margin = new Thickness(0, 0, 8, 6);
        check.Click += async (_, _) => await CheckAsync();
        row.Children.Add(check);
        install.Visibility = Visibility.Collapsed; install.Margin = new Thickness(0, 0, 8, 6);
        install.Background = Ui.Accent; install.Foreground = Ui.OnAccent; install.BorderBrush = Ui.Accent; install.FontWeight = FontWeights.SemiBold;
        install.Click += async (_, _) => await InstallAsync();
        row.Children.Add(install);
        var releases = LinkButton(null, "Releases ↗", Links.Releases, "Open the latest release on GitHub");
        releases.Margin = new Thickness(0, 0, 8, 6);
        row.Children.Add(releases);
        updatesPanel.Children.Add(row);
        progress.Height = 4; progress.Minimum = 0; progress.Maximum = 1; progress.Visibility = Visibility.Collapsed; progress.Margin = new Thickness(0, 4, 0, 6);
        progress.Foreground = Ui.Accent; progress.Background = Ui.Line; progress.BorderThickness = new Thickness(0);
        updatesPanel.Children.Add(progress);
        updateStatus.Text = "Check GitHub for a newer Still release.";
        updateStatus.Foreground = Ui.Muted; updateStatus.TextWrapping = TextWrapping.Wrap; updateStatus.FontSize = 12.5;
        AutomationProperties.SetLiveSetting(updateStatus, AutomationLiveSetting.Polite);
        updatesPanel.Children.Add(updateStatus);
        body.Children.Add(updates);

        body.Children.Add(new TextBlock { Text = "Inspired by HTML Clock for Plasma by Marcin Orlowski (MIT). MIT licence.", Foreground = Ui.Muted, FontSize = 11.5, Margin = new Thickness(0, 16, 0, 0), TextWrapping = TextWrapping.Wrap });

        var close = new Button { Content = "_Close", Width = 120, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => busy?.Cancel();
        Loaded += async (_, _) => { close.Focus(); if (checkNow) await CheckAsync(); };
    }

    private Button LinkButton(UIElement? icon, string text, string url, string tooltip)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (icon != null) { content.Children.Add(icon); }
        content.Children.Add(new TextBlock { Text = text, Margin = new Thickness(icon != null ? 10 : 0, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        // Follow the button's own text colour (the yellow coffee button uses dark text).
        ((TextBlock)content.Children[^1]).SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
        var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = tooltip + "\n" + url, Padding = new Thickness(14, 8, 14, 8) };
        AutomationProperties.SetName(button, text.Replace(" ↗", "") + " (opens in your browser)");
        AutomationProperties.SetHelpText(button, url);
        button.Click += (_, _) => Open(url);
        return button;
    }

    /// <summary>Opens a link in the default browser. Only fixed https links from <see cref="Links"/> are passed here.</summary>
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private async Task CheckAsync()
    {
        busy?.Cancel(); busy = new CancellationTokenSource();
        check.IsEnabled = false; install.Visibility = Visibility.Collapsed; latest = null;
        SetStatus("Checking GitHub for the latest release…", false);
        try
        {
            using var client = UpdateChecker.CreateClient();
            var release = await UpdateChecker.CheckAsync(client, busy.Token);
            SetStatus(UpdateChecker.Summary(release, CurrentVersion), false);
            if (UpdateChecker.IsNewer(release.Version, CurrentVersion))
            {
                latest = release;
                bool canInstall = release.InstallerUrl != null && release.ChecksumsUrl != null && ClockApplication.IsInstalledCopy;
                install.Content = canInstall ? $"_Download and install {release.Version.ToString(3)}" : "_Open download page";
                install.Visibility = Visibility.Visible;
                if (!ClockApplication.IsInstalledCopy) SetStatus(UpdateChecker.Summary(release, CurrentVersion) + " This is a portable copy, so download the new ZIP from the release page.", false);
            }
        }
        catch (OperationCanceledException) when (busy.IsCancellationRequested) { }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
        {
            SetStatus("Couldn't reach GitHub. " + (e is HttpRequestException { Message.Length: > 0 } h && h.Message.StartsWith("GitHub") ? h.Message : "Check your connection and try again, or open Releases."), true);
        }
        finally { check.IsEnabled = true; }
    }

    private async Task InstallAsync()
    {
        if (latest == null) return;
        if (latest.InstallerUrl == null || latest.ChecksumsUrl == null || !ClockApplication.IsInstalledCopy) { Open(latest.PageUrl); return; }
        busy?.Cancel(); busy = new CancellationTokenSource();
        check.IsEnabled = install.IsEnabled = false;
        progress.Value = 0; progress.Visibility = Visibility.Visible;
        SetStatus($"Downloading Still {latest.Version.ToString(3)}…", false);
        try
        {
            using var client = UpdateChecker.CreateClient();
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Still-update");
            string path = await UpdateChecker.DownloadVerifiedInstallerAsync(client, latest, folder, new Progress<double>(v => progress.Value = v), busy.Token);
            SetStatus("Checksum verified. Still will close, update and reopen in a few seconds.", false);
            await Task.Delay(900);
            app.InstallUpdate(path);
        }
        catch (OperationCanceledException) when (busy.IsCancellationRequested) { }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            SetStatus("The update didn't finish: " + e.Message + " You can download it from Releases instead.", true);
            check.IsEnabled = install.IsEnabled = true; progress.Visibility = Visibility.Collapsed;
        }
    }

    private void SetStatus(string text, bool problem)
    {
        updateStatus.Text = text;
        updateStatus.Foreground = problem ? Ui.Warning : Ui.Ink;
    }

    /// <summary>A crisp vector version of the app icon: a glowing clock ring on a dark tile.</summary>
    public static UIElement ClockBadge(double size)
    {
        var accent = Color.FromRgb(0x00, 0xE5, 0xFF);
        var canvas = new Canvas { Width = 64, Height = 64 };
        canvas.Children.Add(new System.Windows.Shapes.Rectangle { Width = 64, Height = 64, RadiusX = 15, RadiusY = 15, Fill = new LinearGradientBrush(Color.FromRgb(0x0B, 0x18, 0x2A), Color.FromRgb(0x16, 0x0B, 0x2E), 45) });
        var ring = new System.Windows.Shapes.Ellipse { Width = 40, Height = 40, Stroke = new SolidColorBrush(accent), StrokeThickness = 3, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accent, ShadowDepth = 0, BlurRadius = 10, Opacity = .8 } };
        Canvas.SetLeft(ring, 12); Canvas.SetTop(ring, 12); canvas.Children.Add(ring);
        canvas.Children.Add(new VectorPath { Data = Geometry.Parse("M32 21v11l7 5"), Stroke = Brushes.White, StrokeThickness = 3.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
        var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(accent) };
        Canvas.SetLeft(dot, 29.5); Canvas.SetTop(dot, 29.5); canvas.Children.Add(dot);
        return new Viewbox { Child = canvas, Width = size, Height = size };
    }

    // Icons drawn as vectors, matching ArchEasy's cup and the GitHub mark.
    public static UIElement CoffeeIcon(double size)
    {
        var canvas = new Canvas { Width = 32, Height = 32 };
        var cup = Geometry.Parse("M7 11h17v11a7 7 0 0 1-7 7h-3a7 7 0 0 1-7-7z");
        var steam = Geometry.Parse("M24 13h2a4 4 0 0 1 0 8h-2M11 7c-3-3 3-3 0-6M17 7c-3-3 3-3 0-6");
        var saucer = Geometry.Parse("M5 30h22");
        var brown = new SolidColorBrush(Color.FromRgb(0x9B, 0x69, 0x37));
        canvas.Children.Add(new VectorPath { Data = cup, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00)), Stroke = new SolidColorBrush(Color.FromRgb(0x30, 0x27, 0x19)), StrokeThickness = 2 });
        canvas.Children.Add(new VectorPath { Data = steam, Stroke = brown, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        canvas.Children.Add(new VectorPath { Data = saucer, Stroke = brown, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        return new Viewbox { Child = canvas, Width = size, Height = size };
    }

    public static UIElement GitHubIcon(double size)
    {
        var mark = Geometry.Parse("M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z");
        var path = new VectorPath { Data = mark, Fill = Ui.Ink };
        var canvas = new Canvas { Width = 16, Height = 16 }; canvas.Children.Add(path);
        return new Viewbox { Child = canvas, Width = size, Height = size };
    }
}
