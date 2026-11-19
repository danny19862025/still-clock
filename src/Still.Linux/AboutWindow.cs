using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using VectorPath = Avalonia.Controls.Shapes.Path;

namespace Still.Linux;

/// <summary>About Still: who made it, the GitHub repository, Buy Me a Coffee, and an on-demand update check.</summary>
public sealed class AboutWindow : Window
{
    public static Version CurrentVersion => typeof(AboutWindow).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);
    private static readonly FontFamily Mono = LinuxDesktop.MonoFamily;

    private readonly TextBlock updateStatus = new();
    private readonly Button check = new();
    private readonly Button download = new();
    private ReleaseInfo? latest;
    private CancellationTokenSource? busy;

    public AboutWindow(bool checkNow = false)
    {
        Title = "About Still";
        Icon = ClockApplication.AppIcon;
        Width = 760; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Window;

        var body = new StackPanel { Margin = new Thickness(30, 26, 30, 24) };
        Content = body;

        // Hero: icon, name, version, author.
        var hero = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var badge = ClockBadge(72); badge.Margin = new Thickness(0, 0, 20, 0);
        DockPanel.SetDock(badge, Dock.Left); hero.Children.Add(badge);
        var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new TextBlock { Text = "Still", FontSize = 30, FontWeight = FontWeight.SemiBold, Foreground = Ui.Ink });
        identity.Children.Add(new TextBlock { Text = $"Version {CurrentVersion.ToString(3)}", FontFamily = Mono, FontSize = 12.5, Foreground = Ui.Muted, Margin = new Thickness(0, 2, 0, 4) });
        identity.Children.Add(new TextBlock { Text = $"Created by {Links.Author}", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Ui.Accent });
        hero.Children.Add(identity);
        body.Children.Add(hero);

        var overview = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 22) };
        overview.Children.Add(new TextBlock
        {
            Text = "A futuristic, deeply customizable desktop clock. Pick from presets, poster and script layouts, glow, frames and a calendar, then make it yours.\n\n" +
                   "No account, telemetry or background network use. Still only contacts GitHub when you press Check for updates.",
            TextWrapping = TextWrapping.Wrap, Foreground = Ui.Ink, FontSize = 13.5, LineHeight = 21, Margin = new Thickness(0, 0, 24, 0)
        });
        var links = new StackPanel { Spacing = 10, MinWidth = 250 };
        Grid.SetColumn(links, 1); overview.Children.Add(links);
        links.Children.Add(LinkButton(GitHubIcon(20), "still-clock · GitHub", Links.Repository, "Open Still's source code on GitHub"));
        var coffee = LinkButton(CoffeeIcon(22), "Buy Me a Coffee", Links.Coffee, "Support Danny on Buy Me a Coffee");
        coffee.Classes.Add("coffee");
        AutomationProperties.SetName(coffee, "Buy Me a Coffee — support Danny");
        links.Children.Add(coffee);
        links.Children.Add(LinkButton(null, "Report a problem ↗", Links.Issues, "Open the issue tracker on GitHub"));
        body.Children.Add(overview);

        var updates = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(18, 14), Background = Ui.Control };
        var updatesPanel = new StackPanel { Spacing = 8 }; updates.Child = updatesPanel;
        updatesPanel.Children.Add(new TextBlock { Text = "// UPDATES", FontFamily = Mono, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Foreground = Ui.Accent });
        var row = new WrapPanel();
        check.Content = "Check for _updates"; check.Margin = new Thickness(0, 0, 8, 0);
        check.Click += async (_, _) => await CheckAsync();
        row.Children.Add(check);
        download.IsVisible = false; download.Margin = new Thickness(0, 0, 8, 0); download.Classes.Add("primary");
        download.Click += (_, _) => { if (latest != null) Open(latest.PageUrl); };
        row.Children.Add(download);
        row.Children.Add(LinkButton(null, "Releases ↗", Links.Releases, "Open the latest release on GitHub"));
        updatesPanel.Children.Add(row);
        updateStatus.Text = "Check GitHub for a newer Still release."; updateStatus.Foreground = Ui.Muted; updateStatus.TextWrapping = TextWrapping.Wrap; updateStatus.FontSize = 12.5;
        AutomationProperties.SetLiveSetting(updateStatus, AutomationLiveSetting.Polite);
        updatesPanel.Children.Add(updateStatus);
        body.Children.Add(updates);

        body.Children.Add(new TextBlock { Text = "Inspired by HTML Clock for Plasma by Marcin Orlowski (MIT). Bundled fonts under the SIL Open Font License. MIT licence.", Foreground = Ui.Muted, FontSize = 11.5, Margin = new Thickness(0, 16, 0, 0), TextWrapping = TextWrapping.Wrap });
        var close = new Button { Content = "_Close", MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
        close.Click += (_, _) => Close();
        body.Children.Add(close);

        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => busy?.Cancel();
        Opened += async (_, _) => { close.Focus(); if (checkNow) await CheckAsync(); };
    }

    private Button LinkButton(Control? icon, string text, string url, string tooltip)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (icon != null) content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 8) };
        ToolTip.SetTip(button, tooltip + "\n" + url);
        AutomationProperties.SetName(button, text.Replace(" ↗", "") + " (opens in your browser)");
        button.Click += (_, _) => Open(url);
        return button;
    }

    /// <summary>Opens a fixed https link with the desktop's default browser.</summary>
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", url) { UseShellExecute = false }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    internal async Task CheckAsync()
    {
        if (busy != null && !check.IsEnabled) return;
        busy?.Cancel(); busy = new CancellationTokenSource();
        check.IsEnabled = false; download.IsVisible = false; latest = null;
        SetStatus("Checking GitHub for the latest release…", false);
        try
        {
            using var client = UpdateChecker.CreateClient();
            var release = await UpdateChecker.CheckAsync(client, busy.Token);
            if (UpdateChecker.IsNewer(release.Version, CurrentVersion))
            {
                latest = release;
                download.Content = $"_Get Still {release.Version.ToString(3)}";
                download.IsVisible = true;
                SetStatus(UpdateChecker.Summary(release, CurrentVersion) + $" Download {UpdateChecker.LinuxArchiveName}, extract it and run ./install.sh to update.", false);
            }
            else SetStatus(UpdateChecker.Summary(release, CurrentVersion), false);
        }
        catch (OperationCanceledException) when (busy.IsCancellationRequested) { }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
        {
            SetStatus("Couldn't reach GitHub. " + (e is HttpRequestException h && h.Message.StartsWith("GitHub") ? h.Message : "Check your connection and try again, or open Releases."), true);
        }
        finally { check.IsEnabled = true; }
    }

    private void SetStatus(string text, bool problem) { updateStatus.Text = text; updateStatus.Foreground = problem ? Ui.Warning : Ui.Ink; }

    public static Control ClockBadge(double size)
    {
        var accent = Color.FromRgb(0x00, 0xE5, 0xFF);
        var canvas = new Canvas { Width = 64, Height = 64 };
        canvas.Children.Add(new Rectangle { Width = 64, Height = 64, RadiusX = 15, RadiusY = 15, Fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromRgb(0x0B, 0x18, 0x2A), 0), new GradientStop(Color.FromRgb(0x16, 0x0B, 0x2E), 1) } } });
        var ring = new Ellipse { Width = 40, Height = 40, Stroke = new ImmutableSolidColorBrush(accent), StrokeThickness = 3, Effect = new DropShadowEffect { Color = accent, OffsetX = 0, OffsetY = 0, BlurRadius = 10, Opacity = .8 } };
        Canvas.SetLeft(ring, 12); Canvas.SetTop(ring, 12); canvas.Children.Add(ring);
        canvas.Children.Add(new VectorPath { Data = Geometry.Parse("M32 21v11l7 5"), Stroke = Brushes.White, StrokeThickness = 3.2, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round });
        var dot = new Ellipse { Width = 5, Height = 5, Fill = new ImmutableSolidColorBrush(accent) };
        Canvas.SetLeft(dot, 29.5); Canvas.SetTop(dot, 29.5); canvas.Children.Add(dot);
        return new Viewbox { Child = canvas, Width = size, Height = size };
    }

    public static Control CoffeeIcon(double size)
    {
        var canvas = new Canvas { Width = 32, Height = 32 };
        var brown = new ImmutableSolidColorBrush(Color.FromRgb(0x9B, 0x69, 0x37));
        canvas.Children.Add(new VectorPath { Data = Geometry.Parse("M7 11h17v11a7 7 0 0 1-7 7h-3a7 7 0 0 1-7-7z"), Fill = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00)), Stroke = new ImmutableSolidColorBrush(Color.FromRgb(0x30, 0x27, 0x19)), StrokeThickness = 2 });
        canvas.Children.Add(new VectorPath { Data = Geometry.Parse("M24 13h2a4 4 0 0 1 0 8h-2M11 7c-3-3 3-3 0-6M17 7c-3-3 3-3 0-6"), Stroke = brown, StrokeThickness = 2, StrokeLineCap = PenLineCap.Round });
        canvas.Children.Add(new VectorPath { Data = Geometry.Parse("M5 30h22"), Stroke = brown, StrokeThickness = 2, StrokeLineCap = PenLineCap.Round });
        return new Viewbox { Child = canvas, Width = size, Height = size };
    }

    public static Control GitHubIcon(double size)
    {
        var mark = Geometry.Parse("M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z");
        var canvas = new Canvas { Width = 16, Height = 16 };
        canvas.Children.Add(new VectorPath { Data = mark, Fill = Ui.Ink });
        return new Viewbox { Child = canvas, Width = size, Height = size };
    }
}
