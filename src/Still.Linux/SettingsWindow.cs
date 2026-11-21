using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Still.Linux;

public sealed class SettingsWindow : Window
{
    private static readonly string[] Pages = ["Theme", "Style", "Effects", "Calendar", "Advanced"];
    private readonly ClockApplication app;
    private readonly ClockFace preview = new();
    private readonly StackPanel panel = new();
    private readonly ScrollViewer scroll = new();
    private readonly TextBlock status = new();
    private readonly TextBlock previewCaption = new();
    private readonly TextBlock clockReadout = new();
    private readonly Border previewGlow = new();
    private readonly Dictionary<string, Button> navigation = [];
    private string page = "Theme";
    private ToggleSwitch? locked, clickThrough, topmost;
    private Slider? scale;
    private bool syncing;
    private Preferences P => app.Preferences;

    // Chrome colours follow the Dark/Light/System choice; the window is rebuilt when it changes.
    private static IBrush Muted => Ui.Muted;
    private static IBrush Accent => Ui.Accent;
    private static IBrush Magenta => Ui.Warning;
    private static IBrush Line => Ui.Line;
    private static readonly FontFamily Mono = LinuxDesktop.MonoFamily;

    /// <summary>A faint blueprint grid behind the preview.</summary>
    private sealed class Blueprint : Control
    {
        public override void Render(DrawingContext dc)
        {
            var pen = new Pen(new ImmutableSolidColorBrush(Color.FromArgb(26, 0, 229, 255)), 1);
            for (double x = .5; x < Bounds.Width; x += 32) dc.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
            for (double y = .5; y < Bounds.Height; y += 32) dc.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
        }
    }

    private static LinearGradientBrush Diagonal(Color a, Color b) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(a, 0), new GradientStop(b, 1) }
    };

    public SettingsWindow(ClockApplication application)
    {
        app = application;
        Title = "Still — Settings";
        Icon = ClockApplication.AppIcon;
        var work = app.Clock.Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1280, 900);
        double dip = app.Clock.Screens.Primary?.Scaling ?? 1;
        Width = Math.Min(1120, work.Width / dip - 32);
        Height = Math.Min(860, work.Height / dip - 48);
        MinWidth = Math.Min(640, work.Width / dip - 32);
        MinHeight = Math.Min(520, work.Height / dip - 48);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Window;

        var root = new Grid { RowDefinitions = new RowDefinitions("56,*,Auto") };
        Content = root;

        // ---- Header ----
        var titlebar = new Border { Background = Ui.Titlebar, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) };
        root.Children.Add(titlebar);
        var titleGrid = new Grid(); titlebar.Child = titleGrid;
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(26, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border { Width = 10, Height = 10, Background = Accent, Margin = new Thickness(0, 0, 14, 0), RenderTransform = new RotateTransform(45), Effect = new DropShadowEffect { Color = Ui.AccentColor, OffsetX = 0, OffsetY = 0, BlurRadius = 12 }, VerticalAlignment = VerticalAlignment.Center });
        brand.Children.Add(new TextBlock { Text = "STILL", FontSize = 19, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        brand.Children.Add(new TextBlock { Text = "  //  CHRONO INTERFACE", FontFamily = Mono, FontSize = 12, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
        titleGrid.Children.Add(brand);
        // About and Buy Me a Coffee sit at the top, beside the live time.
        var chrome = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Spacing = 8, Margin = new Thickness(0, 0, 26, 0) };
        var about = new Button { Content = "_About", Padding = new Thickness(14, 5), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(about, "About Still, updates and support (F1)");
        AutomationProperties.SetName(about, "About Still");
        about.Click += (_, _) => app.OpenAbout();
        var coffee = new Button { Content = AboutWindow.CoffeeIcon(20), Padding = new Thickness(7, 4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        ToolTip.SetTip(coffee, "Support Danny on Buy Me a Coffee");
        AutomationProperties.SetName(coffee, "Buy Me a Coffee — support Danny");
        coffee.Click += (_, _) => AboutWindow.Open(Links.Coffee);
        clockReadout.FontFamily = Mono; clockReadout.FontSize = 12; clockReadout.Foreground = Accent; clockReadout.VerticalAlignment = VerticalAlignment.Center;
        chrome.Children.Add(about); chrome.Children.Add(coffee); chrome.Children.Add(clockReadout);
        titleGrid.Children.Add(chrome);

        // ---- Body: preview stage + controls ----
        var body = new Grid { Margin = new Thickness(26, 22, 26, 20), ColumnDefinitions = new ColumnDefinitions("1.05*,*") }; Grid.SetRow(body, 1); root.Children.Add(body);
        var left = new Border { Margin = new Thickness(0, 0, 26, 0), CornerRadius = new CornerRadius(6), BorderBrush = Line, BorderThickness = new Thickness(1), Background = Diagonal(Color.FromRgb(6, 12, 24), Color.FromRgb(14, 8, 30)), ClipToBounds = true };
        body.Children.Add(left);
        var stage = new Grid(); left.Child = stage;
        stage.Children.Add(new Blueprint());
        previewGlow.Width = 420; previewGlow.Height = 260; previewGlow.HorizontalAlignment = HorizontalAlignment.Center; previewGlow.VerticalAlignment = VerticalAlignment.Center;
        stage.Children.Add(previewGlow);
        stage.Children.Add(new Viewbox { Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Center });
        stage.Children.Add(new TextBlock { Text = "LIVE PREVIEW", FontFamily = Mono, FontSize = 10, Foreground = Ui.StageAccent, Margin = new Thickness(16, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
        previewCaption.Foreground = Ui.StageMuted; previewCaption.FontFamily = Mono; previewCaption.FontSize = 10; previewCaption.Margin = new Thickness(16, 0, 16, 14);
        previewCaption.VerticalAlignment = VerticalAlignment.Bottom; previewCaption.HorizontalAlignment = HorizontalAlignment.Left; previewCaption.TextTrimming = TextTrimming.CharacterEllipsis;
        stage.Children.Add(previewCaption);

        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*") }; Grid.SetColumn(right, 1); body.Children.Add(right);
        var nav = new UniformGrid { Columns = Pages.Length, Margin = new Thickness(0, 0, 0, 18) }; right.Children.Add(nav);
        for (int i = 0; i < Pages.Length; i++)
        {
            string name = Pages[i];
            var button = new Button { Content = $"0{i + 1}  {name.ToUpperInvariant()}", FontFamily = Mono, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(4, 11), FontSize = 12, BorderThickness = new Thickness(0, 0, 0, 2), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, CornerRadius = new CornerRadius(0) };
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, $"Ctrl+{i + 1}");
            button.Click += (_, _) => ShowPage(name); nav.Children.Add(button); navigation[name] = button;
        }
        scroll.Content = panel; scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        panel.Margin = new Thickness(0, 0, 16, 0);
        Grid.SetRow(scroll, 1); right.Children.Add(scroll);

        // ---- Footer ----
        var footer = new Border { BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(26, 16) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") }; footer.Child = actions;
        var restore = new Button { Content = "Show clock", MinWidth = 140, HorizontalContentAlignment = HorizontalAlignment.Center }; restore.Click += (_, _) => app.Unlock(); actions.Children.Add(restore);
        status.FontSize = 11; status.FontFamily = Mono; status.Foreground = Muted; status.VerticalAlignment = VerticalAlignment.Center; status.HorizontalAlignment = HorizontalAlignment.Center;
        status.TextWrapping = TextWrapping.Wrap; status.TextAlignment = TextAlignment.Center; status.Margin = new Thickness(16, 0); Grid.SetColumn(status, 1); actions.Children.Add(status);
        var done = new Button { Content = "_Done", MinWidth = 128, HorizontalContentAlignment = HorizontalAlignment.Center, Classes = { "primary" } }; done.Click += (_, _) => Close();
        Grid.SetColumn(done, 2); actions.Children.Add(done);

        SizeChanged += (_, e) =>
        {
            bool compact = e.NewSize.Width < 880;
            left.IsVisible = !compact;
            body.ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(1.05, GridUnitType.Star);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            if (e.Key == Key.F1) { app.OpenAbout(); e.Handled = true; }
            if (e.KeyModifiers == KeyModifiers.Control && e.Key >= Key.D1 && e.Key < Key.D1 + Pages.Length)
            { ShowPage(Pages[e.Key - Key.D1]); e.Handled = true; }
        };
        app.Updated += OnUpdated;
        Closed += (_, _) => app.Updated -= OnUpdated;
        ShowPage(page); RefreshPreview(DateTime.Now);
    }

    private void OnUpdated() { RefreshPreview(DateTime.Now); UpdateStatus(); }
    public void RefreshPreview(DateTime time)
    {
        preview.Update(P, time);
        clockReadout.Text = time.ToString("HH:mm:ss") + "  ·  " + TemplateFormatter.ZoneTag(time);
        var accent = Color.Parse(P.UseAccent ? P.AccentColor : P.TextColor);
        previewGlow.Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb(48, accent.R, accent.G, accent.B), 0), new GradientStop(Colors.Transparent, 1) } };
        string surface = P.Background == "Card" ? "TRANSLUCENT CARD" : P.Background == "Solid" ? "SOLID PANEL" : "TRANSPARENT";
        previewCaption.Text = $"{P.Preset.ToUpperInvariant()}  /  {P.Scale:0.00}×  /  {surface}  /  FX {(P.Glow ? "GLOW " : "")}{(P.Gradient ? "GRADIENT " : "")}{(P.Scanlines ? "SCANLINES " : "")}{(P.Frame != "None" ? P.Frame.ToUpperInvariant() : "")}".TrimEnd();
        if (scale != null && Math.Abs(scale.Value - P.Scale) > .001) { syncing = true; scale.Value = P.Scale; syncing = false; }
    }
    public void UpdateStatus() => status.Text = app.StartupNotice ?? app.InteractionNotice ?? app.Store.Notice ?? "●  SAVED AUTOMATICALLY";
    public void SyncBehavior()
    {
        syncing = true;
        if (locked != null) locked.IsChecked = P.Locked;
        if (clickThrough != null) { clickThrough.IsEnabled = P.Locked; clickThrough.IsChecked = P.ClickThrough; }
        if (topmost != null) topmost.IsChecked = P.Topmost;
        syncing = false;
    }

    private void ShowPage(string name)
    {
        double offset = name == page ? scroll.Offset.Y : 0;
        page = name; panel.Children.Clear(); locked = clickThrough = topmost = null; scale = null;
        foreach (var (key, button) in navigation)
        {
            button.Background = key == name ? Ui.SelectedTint : Brushes.Transparent;
            button.Foreground = key == name ? Accent : Muted;
            button.BorderBrush = key == name ? Accent : Line;
        }
        switch (name)
        {
            case "Theme": ThemePage(); break;
            case "Style": StylePage(); break;
            case "Effects": Effects(); break;
            case "Calendar": CalendarPage(); break;
            default: Advanced(); break;
        }
        UpdateStatus();
        // Rebuilding the page after a click keeps the reader's place.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => scroll.Offset = new Vector(0, offset), Avalonia.Threading.DispatcherPriority.Background);
    }

    // ---- Control helpers ----
    private static TextBlock Copy(string text, double size = 12) => new() { Text = text, FontSize = size, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5 };
    private void Section(string title, string? subtitle = null)
    {
        var header = new Grid { Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 26, 0, 10), ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        header.Children.Add(new TextBlock { Text = "// " + title.ToUpperInvariant(), FontFamily = Mono, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Accent, Margin = new Thickness(0, 0, 12, 0) });
        var rule = new Border
        {
            Height = 1, VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromArgb(90, Ui.AccentColor.R, Ui.AccentColor.G, Ui.AccentColor.B), 0), new GradientStop(Colors.Transparent, 1) } }
        };
        Grid.SetColumn(rule, 1); header.Children.Add(rule);
        panel.Children.Add(header);
        if (subtitle != null) { var t = Copy(subtitle); t.Margin = new Thickness(0, 0, 0, 10); panel.Children.Add(t); }
    }
    private void Field(string name, Control control, Panel? target = null)
    {
        target ??= panel;
        target.Children.Add(new Label { Content = name, Target = control, Margin = new Thickness(0, 10, 0, 0) });
        AutomationProperties.SetName(control, name.Replace("_", "")); target.Children.Add(control);
    }
    private ComboBox Choice(string name, IEnumerable<string> values, string selected, Action<string> change)
    {
        var combo = new ComboBox { ItemsSource = values.ToArray(), SelectedItem = selected };
        combo.SelectionChanged += (_, _) => { if (syncing || combo.SelectedItem is not string value || value == selected) return; selected = value; change(value); app.Apply(); };
        Field(name, combo); return combo;
    }
    private ToggleSwitch Check(string name, bool value, Action<bool> change, bool rebuild = false)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4) };
        var label = new TextBlock { Text = name.Replace("_", ""), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0), Cursor = new Cursor(StandardCursorType.Hand) };
        var toggle = new ToggleSwitch { IsChecked = value, OnContent = null, OffContent = null, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(toggle, name.Replace("_", ""));
        Grid.SetColumn(toggle, 1);
        row.Children.Add(label); row.Children.Add(toggle);
        label.PointerPressed += (_, _) => { if (toggle.IsEnabled) toggle.IsChecked = !(toggle.IsChecked ?? false); };
        toggle.PropertyChanged += (_, e) => { if (e.Property == IsEnabledProperty) label.Opacity = toggle.IsEnabled ? 1 : .45; };
        toggle.IsCheckedChanged += (_, _) => { if (syncing) return; change(toggle.IsChecked == true); app.Apply(); if (rebuild) ShowPage(page); };
        panel.Children.Add(row); return toggle;
    }
    private Slider Range(string name, double min, double max, double value, double step, Action<double> change, string format = "0")
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = step * 5 };
        var label = new Label { Content = $"{name}  ·  {value.ToString(format)}", Target = slider, Margin = new Thickness(0, 10, 0, 0) };
        AutomationProperties.SetName(slider, name.Replace("_", "")); panel.Children.Add(label); panel.Children.Add(slider);
        slider.ValueChanged += (_, _) => { label.Content = $"{name}  ·  {slider.Value.ToString(format)}"; if (syncing) return; change(slider.Value); app.Apply(); };
        return slider;
    }
    private void ColorChoice(string name, string selected, Action<string> change)
    {
        var picker = new ColorPicker { Color = Color.Parse(selected), IsAlphaEnabled = false, IsAlphaVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
        // Type or paste a hex code (#FF2BD6, FF2BD6 or #F2D); a valid code applies as you type.
        var hex = new TextBox { Text = selected.ToUpperInvariant(), FontFamily = Mono, Width = 120, MaxLength = 9, Watermark = "#RRGGBB", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        ToolTip.SetTip(hex, "Type a hex colour such as #FF2BD6 or #F2D");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(picker); row.Children.Add(hex);
        panel.Children.Add(new Label { Content = name, Target = hex, Margin = new Thickness(0, 10, 0, 0) });
        AutomationProperties.SetName(picker, name.Replace("_", ""));
        AutomationProperties.SetName(hex, name.Replace("_", "") + " hex code");
        panel.Children.Add(row);
        bool updating = false;
        void Set(string value, bool fromText)
        {
            if (value.Equals(selected, StringComparison.OrdinalIgnoreCase)) return;
            selected = value;
            updating = true;
            if (fromText) picker.Color = Color.Parse(value); else hex.Text = value;
            updating = false;
            change(value); app.Apply();
        }
        picker.ColorChanged += (_, e) =>
        {
            if (updating) return;
            var c = e.NewColor;
            Set($"#{c.R:X2}{c.G:X2}{c.B:X2}", false);
        };
        hex.TextChanged += (_, _) =>
        {
            if (updating) return;
            string? value = Preferences.ParseHexColor(hex.Text);
            if (value == null && !string.IsNullOrWhiteSpace(hex.Text)) hex.BorderBrush = Magenta; else hex.ClearValue(TextBox.BorderBrushProperty);
            if (value != null) Set(value, true);
        };
        // Leaving the box (or pressing Enter) tidies it to the colour in use, e.g. "f2d" → "#FF22DD".
        void Tidy() { updating = true; hex.Text = selected; hex.ClearValue(TextBox.BorderBrushProperty); updating = false; }
        hex.LostFocus += (_, _) => Tidy();
        hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Tidy(); e.Handled = true; } };
    }
    private void Note(string text) { var t = Copy(text, 11.5); t.Margin = new Thickness(0, 2, 0, 4); panel.Children.Add(t); }

    // ---- Pages ----
    private void ThemePage()
    {
        Section("Settings appearance", "Colours for this window and About. The clock itself keeps its own look.");
        var modes = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var (mode, label) in new[] { ("Dark", "Dark"), ("Light", "Light"), ("System", "Match system") })
        {
            bool active = P.UiTheme == mode;
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, BorderBrush = active ? Accent : Line, BorderThickness = new Thickness(active ? 1.5 : 1) };
            if (active) { button.Background = Ui.SelectedTint; button.Foreground = Accent; }
            AutomationProperties.SetName(button, label + " settings appearance");
            button.Click += (_, _) =>
            {
                if (P.UiTheme == mode) return;
                P.UiTheme = mode; app.Apply();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => app.ApplyUiTheme(force: true, page: page));
            };
            modes.Children.Add(button);
        }
        panel.Children.Add(modes);

        Section("Presets", "Each preset is a starting point. Change anything afterwards on the Style and Effects pages.");
        var gallery = new UniformGrid { Columns = 2 };
        var sample = DateTime.Now;
        foreach (var name in Preferences.Presets)
        {
            var look = new Preferences(); look.ApplyPreset(name); look.Hour24 = P.Hour24;
            var face = new ClockFace(); face.Update(look, sample);
            bool active = P.Preset == name;
            var card = new Grid { RowDefinitions = new RowDefinitions("86,Auto") };
            card.Children.Add(new Viewbox { Child = face, Stretch = Stretch.Uniform, Margin = new Thickness(6, 8, 6, 4) });
            var title = new TextBlock { Text = name == "Typography" ? "TYPE" : name.ToUpperInvariant(), FontFamily = Mono, FontSize = 11, Foreground = active ? Ui.StageAccent : Ui.StageMuted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 2) };
            Grid.SetRow(title, 1); card.Children.Add(title);
            var button = new Button
            {
                Content = card, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BorderBrush = active ? Accent : Line, BorderThickness = new Thickness(active ? 1.5 : 1),
                Background = active ? new ImmutableSolidColorBrush(Color.FromRgb(8, 30, 44)) : Diagonal(Color.FromRgb(8, 16, 30), Color.FromRgb(14, 10, 30))
            };
            AutomationProperties.SetName(button, (name == "Typography" ? "Type" : name) + " preset");
            button.Click += (_, _) => { P.ApplyPreset(name); app.Apply(); ShowPage(page); app.Clock.RestorePosition(); };
            gallery.Children.Add(button);
        }
        panel.Children.Add(gallery);

        Section("Colour palettes", "Sets the text and accent colours together. Fine-tune both on the Style page.");
        var palettes = new WrapPanel();
        foreach (var (name, text, accent) in Preferences.Palettes)
        {
            bool active = P.TextColor.Equals(text, StringComparison.OrdinalIgnoreCase) && P.AccentColor.Equals(accent, StringComparison.OrdinalIgnoreCase);
            var chips = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var hex in new[] { text, accent })
                chips.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 4, 0), Background = new ImmutableSolidColorBrush(Color.Parse(hex)), VerticalAlignment = VerticalAlignment.Center });
            chips.Children.Add(new TextBlock { Text = " " + name.ToUpperInvariant(), FontFamily = Mono, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = active ? Accent : Ui.Ink });
            var button = new Button { Content = chips, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 7, 12, 7), BorderBrush = active ? Accent : Line };
            AutomationProperties.SetName(button, name + " palette");
            button.Click += (_, _) => { P.TextColor = text; P.AccentColor = accent; P.UseAccent = true; app.Apply(); ShowPage(page); };
            palettes.Children.Add(button);
        }
        panel.Children.Add(palettes);

        Section("Share a theme", "Copy your look as text to keep a backup or send to a friend. Themes work between the Windows and Linux versions. Position and desktop behaviour stay on this computer.");
        var share = new UniformGrid { Columns = 2 };
        var feedback = Copy(""); feedback.Margin = new Thickness(0, 4, 0, 0);
        var copy = new Button { Content = "Copy theme", Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        copy.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard == null) throw new InvalidOperationException();
                await Clipboard.SetTextAsync(P.ExportTheme());
                feedback.Text = "Theme copied to the clipboard."; feedback.Foreground = Accent;
            }
            catch (Exception) { feedback.Text = "The clipboard is not available. Try again."; feedback.Foreground = Magenta; }
        };
        var paste = new Button { Content = "Paste theme", Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        paste.Click += async (_, _) =>
        {
            string? text = null;
            try { if (Clipboard != null) text = await Clipboard.TryGetTextAsync(); } catch (Exception) { }
            string? error = P.ImportTheme(text);
            if (error != null) { feedback.Text = error; feedback.Foreground = Magenta; return; }
            app.Apply(); app.Clock.RestorePosition(); ShowPage(page);
        };
        share.Children.Add(copy); share.Children.Add(paste);
        panel.Children.Add(share); panel.Children.Add(feedback);
        var reset = new Button { Content = "Reset to defaults", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 18, 0, 0) };
        reset.Click += (_, _) => { app.Reset(); ShowPage(page); }; panel.Children.Add(reset);
    }

    private void StylePage()
    {
        Section("Layout");
        Choice("La_yout", Preferences.Layouts, P.Layout, v => P.Layout = v);
        Note("Spaced: a wide, letter-spaced weekday with the full date and a dashed time line beneath. Poster: a huge day number with the month across it and the weekday up the side. Script: a tall day number, the month up the side, a handwritten weekday and a bold time. Overlay: a big see-through time with a handwritten weekday across it and the date below. Arc: hours over minutes with a glowing arc and a small date; a seconds indicator turns the arc into a seconds sweep. Split: a bold accent hour, minutes with small raised seconds, and a compact date, on a flat panel. Alignment, date format and weekday style apply to Classic.");
        Check("Fade the big day number", P.Fade, v => P.Fade = v);
        string script = LinuxDesktop.DisplayName(P.ScriptFont);
        Choice("Script _handwriting font", LinuxDesktop.FontChoices(script, LinuxDesktop.Script), script, v => P.ScriptFont = v);
        Range("Script / Overlay number _width", .35, 1, P.Condense, .05, v => P.Condense = v, "0%");

        Section("Typeface");
        string font = LinuxDesktop.DisplayName(P.Font);
        Choice("_Font", LinuxDesktop.FontChoices(font), font, v => P.Font = v);
        Note("Inter, Barlow, Kaushan Script, Share Tech Mono, Quicksand and Michroma are built in. Presets made for Windows fonts use them automatically, and Anurati is drawn with Michroma unless you install it.");
        const string sameFont = "Same as main font";
        string detailFont = P.DetailFont.Length > 0 ? LinuxDesktop.DisplayName(P.DetailFont) : sameFont;
        var details = LinuxDesktop.FontChoices(LinuxDesktop.Round); details.Insert(0, sameFont);
        if (!details.Contains(detailFont)) details.Insert(1, detailFont);
        Choice("Small te_xt font", details, detailFont, v => P.DetailFont = v == sameFont ? "" : v);
        Note("Used for the date and time lines of the Spaced layout.");
        Choice("_Weight", Preferences.Weights, P.FontWeight, v => P.FontWeight = v);
        Range("_Size", 32, 160, P.FontSize, 1, v => P.FontSize = v);
        Range("Letter _spacing", -4, 24, P.LetterSpacing, .5, v => P.LetterSpacing = v, "0.0");

        Section("Colour");
        ColorChoice("_Text colour", P.TextColor, v => P.TextColor = v);
        ColorChoice("_Accent colour", P.AccentColor, v => P.AccentColor = v);
        Check("Use accent for secondary text, glow and indicators", P.UseAccent, v => P.UseAccent = v);

        Section("Time", P.Advanced ? "A custom template controls the text. Turn it off in Advanced to use the time and date options." : null);
        Choice("Time _format", ["24-hour", "12-hour"], P.Hour24 ? "24-hour" : "12-hour", v => P.Hour24 = v == "24-hour");
        Check("Show seconds", P.Seconds, v => P.Seconds = v);
        Check("Show AM/PM in 12-hour time", P.AmPm, v => P.AmPm = v);

        Section("Date");
        Check("Show weekday", P.Weekday, v => P.Weekday = v);
        Choice("Weekday st_yle", Preferences.WeekdayStyles, P.WeekdayStyle, v => P.WeekdayStyle = v);
        Note("Line shows the weekday as a large line of its own.");
        Check("Show date", P.Date, v => P.Date = v);
        Choice("Date f_ormat", Preferences.DateStyles, P.DateStyle, v => P.DateStyle = v);
        Note("Long: 12 November 2026 · Short: 12 Nov · ISO: 2026-11-12 · Numeric: your regional short date.");
        Check("Uppercase date line", P.Uppercase, v => P.Uppercase = v);

        Section("Layout");
        Choice("A_lignment", ["Left", "Center", "Right"], P.Alignment, v => P.Alignment = v);
        scale = Range("Overall sca_le", .5, 2, P.Scale, .05, v => P.Scale = v, "0.00'×'");
        Note("You can also drag the clock’s lower-right corner while it is unlocked.");
        Range("Widget _opacity", .2, 1, P.Opacity, .05, v => P.Opacity = v, "0%");
    }

    private void CalendarPage()
    {
        Section("Calendar", "An optional calendar that sits with your clock.");
        Check("Show calendar", P.ShowCalendar, v => P.ShowCalendar = v, rebuild: true);
        Section("Mode", "Pick a mode. Each card is a live preview of your clock with that calendar.");
        var gallery = new UniformGrid { Columns = 3 };
        foreach (var style in Preferences.CalendarStyles)
        {
            var look = new Preferences(); look.ImportTheme(P.ExportTheme());
            look.ShowCalendar = true; look.CalendarStyle = style; look.CalendarPlacement = "Below";
            var face = new ClockFace(); face.Update(look, DateTime.Now);
            bool active = P.ShowCalendar && P.CalendarStyle == style;
            var card = new Grid { RowDefinitions = new RowDefinitions("130,Auto,Auto") };
            card.Children.Add(new Viewbox { Child = face, Stretch = Stretch.Uniform, Margin = new Thickness(4, 6, 4, 4) });
            var title = new TextBlock { Text = style.ToUpperInvariant(), FontFamily = Mono, FontSize = 11, Foreground = active ? Ui.StageAccent : Ui.StageMuted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(title, 1); card.Children.Add(title);
            var hint = new TextBlock { Text = style switch { "Week" => "This week", "Strip" => "Every day in one row", _ => "The whole month" }, FontSize = 10.5, Foreground = Ui.StageMuted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(hint, 2); card.Children.Add(hint);
            var button = new Button
            {
                Content = card, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BorderBrush = active ? Accent : Line, BorderThickness = new Thickness(active ? 1.5 : 1),
                Background = active ? new ImmutableSolidColorBrush(Color.FromRgb(8, 30, 44)) : Diagonal(Color.FromRgb(8, 16, 30), Color.FromRgb(14, 10, 30))
            };
            AutomationProperties.SetName(button, style + " calendar");
            button.Click += (_, _) => { P.CalendarStyle = style; P.ShowCalendar = true; app.Apply(); ShowPage(page); app.Clock.RestorePosition(); };
            gallery.Children.Add(button);
        }
        panel.Children.Add(gallery);
        Section("Layout");
        Choice("_Placement", Preferences.CalendarPlacements, P.CalendarPlacement, v => P.CalendarPlacement = v);
        Choice("Week _starts on", Preferences.WeekStarts, P.WeekStart, v => P.WeekStart = v);
        Note("Regional follows your language and region settings.");
        Check("Show week numbers", P.WeekNumbers, v => P.WeekNumbers = v);
        Check("Dim weekends", P.DimWeekends, v => P.DimWeekends = v);
        Check("Show days from other months", P.OtherMonthDays, v => P.OtherMonthDays = v);
        Note("Today is marked in the accent colour, and the calendar turns over at midnight. Month and day names follow your system language. The calendar uses the clock's font, colours, glow and surface, and stays when you switch presets.");
    }

    private void Effects()
    {
        Section("Glow");
        Check("Neon glow", P.Glow, v => P.Glow = v);
        Range("Glow _strength", .1, 1, P.GlowStrength, .05, v => P.GlowStrength = v, "0%");
        Check("Gradient text (text colour to accent)", P.Gradient, v => P.Gradient = v);

        Section("Motion");
        Choice("Seconds _indicator", Preferences.Indicators, P.SecondsIndicator, v => P.SecondsIndicator = v);
        Note("Bar sweeps across each minute. Segments light in 2-second steps. Dots marks every second.");
        Check("Blink the time separator", P.BlinkSeparator, v => P.BlinkSeparator = v);

        Section("Frame & header");
        Choice("_Frame", Preferences.Frames, P.Frame, v => P.Frame = v);
        var caption = new TextBox { Text = P.Caption, MaxLength = 40 };
        Field("Header _caption", caption);
        caption.TextChanged += (_, _) => { if (syncing) return; P.Caption = caption.Text ?? ""; app.Apply(); };
        Note("A short label above the time, such as SYS.CHRONO or HOME OFFICE. Leave empty to hide it.");
        Check("Show time zone tag", P.ShowZone, v => P.ShowZone = v);

        Section("Surface");
        Choice("_Background", ["Transparent", "Card", "Solid"], P.Background, v => P.Background = v);
        ColorChoice("Surface _tint", P.SurfaceColor, v => P.SurfaceColor = v);
        Range("Surface o_pacity", .1, 1, P.BackgroundOpacity, .01, v => P.BackgroundOpacity = v, "0%");
        Range("Corner _radius", 0, 48, P.CornerRadius, 1, v => P.CornerRadius = v);
        Check("CRT scanlines", P.Scanlines, v => P.Scanlines = v);
        Note("Scanlines appear on Card and Solid surfaces. Surface opacity affects the panel only; text stays crisp. Transparency needs a compositing desktop (KDE Plasma, GNOME and most others). There is no wallpaper blur.");
    }

    private void Desktop()
    {
        Section("Position & interaction");
        locked = Check("Lock position", P.Locked, v => { P.Locked = v; if (!v) P.ClickThrough = false; SyncBehavior(); });
        clickThrough = Check("Enable click-through while locked", P.ClickThrough, v => P.ClickThrough = v);
        clickThrough.IsEnabled = P.Locked;
        panel.Children.Add(Copy("Click-through sends mouse input to the windows behind the clock. Use the tray menu’s “Unlock and restore interaction”, or launch Still again, to regain control."));
        topmost = Check("Always on top", P.Topmost, v => P.Topmost = v);
        panel.Children.Add(Copy("Off by default. Still normally follows standard window stacking and appears on every virtual desktop. It is not attached to the wallpaper: Show Desktop may hide it. Use Show clock in the tray to restore it."));
        Section("Startup");
        var login = Check("Launch Still when I log in", P.LaunchAtLogin, v => app.SetLogin(v));
        login.IsCheckedChanged += (_, _) => { if (syncing) return; syncing = true; login.IsChecked = P.LaunchAtLogin; syncing = false; UpdateStatus(); };
        panel.Children.Add(Copy("Optional and off initially. Adds an autostart entry in ~/.config/autostart that points at this copy of Still. No root access is needed."));
        Section("Your displays");
        foreach (var d in LinuxDesktop.Displays(app.Clock))
        {
            var button = new Button { Content = $"Move to {(d.Primary ? "primary display" : d.Name)}  ·  {d.DpiScale:0.##}×", Margin = new Thickness(0, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => { P.Position = new SavedPosition(d.Name, 64, 64); app.Clock.RestorePosition(); app.Apply(); };
            panel.Children.Add(button);
        }
        panel.Children.Add(Copy("Position is remembered per saved display. If that display disconnects, Still moves to the primary display and stays within its working area."));
        Section("About Still", $"Still {AboutWindow.CurrentVersion.ToString(3)} · A futuristic desktop clock for Linux by {Links.Author}\nAvalonia port of the Windows app, inspired by HTML Clock for Plasma by Marcin Orlowski (MIT). Bundled fonts are under the SIL Open Font License. See THIRD-PARTY-NOTICES.md for attribution.");
        var aboutButton = new Button { Content = "About, updates and support…" };
        aboutButton.Click += (_, _) => app.OpenAbout();
        panel.Children.Add(aboutButton);
    }

    public string CurrentPage => page;
    public void Go(string name) { if (Pages.Contains(name)) ShowPage(name); }

    private void Advanced()
    {
        int behaviorStart = panel.Children.Count;
        Desktop();
        var behavior = new StackPanel();
        while (panel.Children.Count > behaviorStart) { var child = panel.Children[behaviorStart]; panel.Children.RemoveAt(behaviorStart); behavior.Children.Add(child); }
        panel.Children.Add(new Expander { Header = "Position & startup", Content = behavior, HorizontalAlignment = HorizontalAlignment.Stretch });
        Section("Optional custom text", "Up to three lines of plain text with a small set of placeholders. Fonts, colours, effects, frame and indicator still apply.");
        Check("Use custom template", P.Advanced, v => P.Advanced = v);
        var editor = new TextBox { Text = P.Template, AcceptsReturn = true, Height = 126, TextWrapping = TextWrapping.Wrap, FontFamily = Mono, MaxLength = 240 };
        Field("_Layout text", editor);
        var validation = Copy("Template ready. Changes preview and save immediately."); validation.Margin = new Thickness(0, 8, 0, 14); panel.Children.Add(validation);
        editor.TextChanged += (_, _) =>
        {
            string text = (editor.Text ?? "").Replace("\r\n", "\n");
            string? error = TemplateFormatter.Validate(text);
            validation.Text = error ?? "Template ready. Changes preview and save immediately.";
            validation.Foreground = error == null ? Accent : Magenta;
            if (error != null) return; // Last valid template remains active and saved.
            P.Template = text; app.Apply();
        };
        panel.Children.Add(Copy("Insert a placeholder at the cursor:"));
        var tokens = new WrapPanel { Margin = new Thickness(0, 8) };
        foreach (string token in TemplateFormatter.Tokens)
        {
            var button = new Button { Content = "{" + token + "}", FontFamily = Mono, FontSize = 11, Padding = new Thickness(9, 6), Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) =>
            {
                string insert = "{" + token + "}", current = editor.Text ?? "";
                int start = Math.Min(editor.SelectionStart, editor.SelectionEnd), end = Math.Max(editor.SelectionStart, editor.SelectionEnd);
                start = Math.Clamp(start, 0, current.Length); end = Math.Clamp(end, start, current.Length);
                if (current.Length - (end - start) + insert.Length > editor.MaxLength)
                { validation.Text = "The template is limited to 240 characters."; return; }
                editor.Text = current[..start] + insert + current[end..];
                editor.CaretIndex = start + insert.Length; editor.Focus();
            };
            tokens.Children.Add(button);
        }
        panel.Children.Add(tokens);
        Section("Placeholder guide");
        panel.Children.Add(Copy("{time}  Hours and minutes; follows 12/24-hour and seconds settings.\n{hour}  Two-digit hour in the selected time format.\n{minute} / {second}  Two-digit minute / second.\n{ampm}  AM or PM in 12-hour mode; empty in 24-hour mode.\n{weekday}  Full weekday name.\n{date}  Day, full month and year.\n{day} / {month} / {year}  Two-digit day, full month, four-digit year.\n\nNames follow your system language and region (LANG / LC_ALL). The first line is large; the following lines use smaller secondary text."));
        Section("Simple by design");
        panel.Children.Add(Copy("Placeholders are case-sensitive. Unknown names, unmatched braces and more than three lines show a helpful error; the last valid template stays active. Limits: 240 characters and three lines.\n\nEverything else is literal text. There is no HTML, CSS, JavaScript, remote loading, command execution or expression evaluation."));
    }
}
