using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media.Effects;
using Forms = System.Windows.Forms;

namespace Still.App;

public sealed class SettingsWindow : Window
{
    private static readonly string[] Pages = ["Theme", "Style", "Effects", "Advanced"];
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
    private CheckBox? locked, clickThrough, topmost;
    private Slider? scale;
    private bool syncing;
    private Preferences P => app.Preferences;

    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(127, 155, 176)));
    private static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0, 229, 255)));
    private static readonly Brush Magenta = Freeze(new SolidColorBrush(Color.FromRgb(255, 43, 214)));
    private static readonly Brush Line = Freeze(new SolidColorBrush(Color.FromRgb(22, 40, 56)));
    private static readonly Brush Panel = Freeze(new SolidColorBrush(Color.FromRgb(8, 15, 26)));
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    private static Brush Freeze(Brush b) { b.Freeze(); return b; }

    public SettingsWindow(ClockApplication application)
    {
        app = application;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Still — Settings";
        Width = Math.Min(1120, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(860, SystemParameters.WorkArea.Height - 32);
        MinWidth = Math.Min(760, SystemParameters.WorkArea.Width - 32);
        MinHeight = Math.Min(540, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/still.ico"));
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(5, 8, 15)) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(74) });
        Content = root;

        // ---- Title bar ----
        var titlebar = new Border { Background = new SolidColorBrush(Color.FromRgb(7, 12, 22)), BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) };
        root.Children.Add(titlebar);
        var titleGrid = new Grid(); titlebar.Child = titleGrid;
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(26, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border { Width = 10, Height = 10, Background = Accent, Margin = new Thickness(0, 0, 14, 0), RenderTransform = new RotateTransform(45), RenderTransformOrigin = new Point(.5, .5), Effect = new DropShadowEffect { Color = Color.FromRgb(0, 229, 255), ShadowDepth = 0, BlurRadius = 12 } });
        brand.Children.Add(new TextBlock { Text = "STILL", FontSize = 19, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        brand.Children.Add(new TextBlock { Text = "  //  CHRONO INTERFACE", FontFamily = Mono, FontSize = 12, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
        titleGrid.Children.Add(brand);
        titlebar.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else DragMove(); };
        var chrome = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        titleGrid.Children.Add(chrome);
        clockReadout.FontFamily = Mono; clockReadout.FontSize = 12; clockReadout.Foreground = Accent; clockReadout.VerticalAlignment = VerticalAlignment.Center; clockReadout.Margin = new Thickness(0, 0, 18, 0);
        chrome.Children.Add(clockReadout);
        foreach (var (text, name, action) in new (string, string, Action)[] { ("−", "Minimize", () => WindowState = WindowState.Minimized), ("□", "Maximize", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized), ("×", "Close", Close) })
        {
            var button = new Button { Content = text, Width = 48, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 20, Padding = new Thickness(0) };
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => action(); chrome.Children.Add(button);
        }

        // ---- Body: preview stage + controls ----
        var body = new Grid { Margin = new Thickness(26, 22, 26, 20) }; Grid.SetRow(body, 1); root.Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new Border { Margin = new Thickness(0, 0, 26, 0), CornerRadius = new CornerRadius(6), BorderBrush = Line, BorderThickness = new Thickness(1), Background = new LinearGradientBrush(Color.FromRgb(6, 12, 24), Color.FromRgb(14, 8, 30), 45), ClipToBounds = true };
        body.Children.Add(left);
        var stage = new Grid { Background = StageBrush() }; left.Child = stage;
        previewGlow.Width = 420; previewGlow.Height = 260; previewGlow.HorizontalAlignment = HorizontalAlignment.Center; previewGlow.VerticalAlignment = VerticalAlignment.Center;
        stage.Children.Add(previewGlow);
        stage.Children.Add(new Viewbox { Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Center });
        stage.Children.Add(new TextBlock { Text = "LIVE PREVIEW", FontFamily = Mono, FontSize = 10, Foreground = Accent, Margin = new Thickness(16, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
        previewCaption.Foreground = Muted; previewCaption.FontFamily = Mono; previewCaption.FontSize = 10; previewCaption.Margin = new Thickness(16, 0, 16, 14);
        previewCaption.VerticalAlignment = VerticalAlignment.Bottom; previewCaption.HorizontalAlignment = HorizontalAlignment.Left; previewCaption.TextTrimming = TextTrimming.CharacterEllipsis;
        stage.Children.Add(previewCaption);

        var right = new Grid(); Grid.SetColumn(right, 1); body.Children.Add(right);
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition());
        var nav = new UniformGridCompat(Pages.Length) { Margin = new Thickness(0, 0, 0, 18) }; right.Children.Add(nav);
        for (int i = 0; i < Pages.Length; i++)
        {
            string name = Pages[i];
            var button = new Button { Content = $"0{i + 1}  {name.ToUpperInvariant()}", FontFamily = Mono, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(4, 11, 4, 11), FontSize = 11.5, BorderThickness = new Thickness(0, 0, 0, 2) };
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => ShowPage(name); nav.Children.Add(button); navigation[name] = button;
        }
        scroll.Content = panel; scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.Padding = new Thickness(0, 0, 8, 0); Grid.SetRow(scroll, 1); right.Children.Add(scroll);

        // ---- Footer ----
        var footer = new Border { BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(26, 16, 26, 16) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
        var actions = new Grid(); footer.Child = actions;
        var restore = new Button { Content = "Show clock", Width = 140, HorizontalAlignment = HorizontalAlignment.Left }; restore.Click += (_, _) => app.Unlock(); actions.Children.Add(restore);
        status.FontSize = 11; status.FontFamily = Mono; status.Foreground = Muted; status.VerticalAlignment = VerticalAlignment.Center; status.HorizontalAlignment = HorizontalAlignment.Center; status.MaxWidth = 460; status.TextWrapping = TextWrapping.Wrap; actions.Children.Add(status);
        var done = new Button { Content = "_Done", Width = 128, HorizontalAlignment = HorizontalAlignment.Right, Background = Accent, Foreground = Brushes.Black, BorderBrush = Accent, FontWeight = FontWeights.SemiBold }; done.Click += (_, _) => Close(); actions.Children.Add(done);

        SizeChanged += (_, _) =>
        {
            bool compact = ActualWidth < 880;
            left.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            body.ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(1.05, GridUnitType.Star);
            status.MaxWidth = compact ? 220 : 460;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.D1 && e.Key < Key.D1 + Pages.Length)
            { ShowPage(Pages[e.Key - Key.D1]); e.Handled = true; }
        };
        app.Updated += OnUpdated;
        Closed += (_, _) => app.Updated -= OnUpdated;
        ShowPage(page); RefreshPreview(DateTime.Now);
    }

    // A faint blueprint grid behind the preview.
    private static Brush StageBrush()
    {
        var lines = new GeometryGroup();
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(32, 0)));
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 32)));
        var tile = new DrawingBrush(new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromArgb(26, 0, 229, 255)), 1), lines))
        { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 32, 32), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 32, 32), ViewboxUnits = BrushMappingMode.Absolute };
        return tile;
    }

    private void OnUpdated() { RefreshPreview(DateTime.Now); UpdateStatus(); }
    public void RefreshPreview(DateTime time)
    {
        preview.Update(P, time);
        clockReadout.Text = time.ToString("HH:mm:ss") + "  ·  " + TemplateFormatter.ZoneTag(time);
        var accent = (Color)ColorConverter.ConvertFromString(P.UseAccent ? P.AccentColor : P.TextColor);
        previewGlow.Background = new RadialGradientBrush(Color.FromArgb(48, accent.R, accent.G, accent.B), Colors.Transparent);
        string surface = P.Background == "Card" ? "TRANSLUCENT CARD" : P.Background == "Solid" ? "SOLID PANEL" : "TRANSPARENT";
        previewCaption.Text = $"{P.Preset.ToUpperInvariant()}  /  {P.Scale:0.00}×  /  {surface}  /  FX {(P.Glow ? "GLOW " : "")}{(P.Gradient ? "GRADIENT " : "")}{(P.Scanlines ? "SCANLINES " : "")}{(P.Frame != "None" ? P.Frame.ToUpperInvariant() : "")}".TrimEnd();
        if (scale != null && Math.Abs(scale.Value - P.Scale) > .001) { syncing = true; scale.Value = P.Scale; syncing = false; }
    }
    public void UpdateStatus() => status.Text = app.StartupNotice ?? app.Store.Notice ?? "●  SAVED AUTOMATICALLY";
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
        double offset = name == page ? scroll.VerticalOffset : 0;
        page = name; panel.Children.Clear(); locked = clickThrough = topmost = null; scale = null;
        foreach (var (key, button) in navigation)
        {
            button.Background = key == name ? new SolidColorBrush(Color.FromArgb(28, 0, 229, 255)) : Brushes.Transparent;
            button.Foreground = key == name ? Accent : Muted;
            button.BorderBrush = key == name ? Accent : Line;
        }
        switch (name)
        {
            case "Theme": Theme(); break;
            case "Style": StylePage(); break;
            case "Effects": Effects(); break;
            default: Advanced(); break;
        }
        UpdateStatus();
        // Rebuilding the page after a click keeps the reader's place.
        scroll.UpdateLayout(); scroll.ScrollToVerticalOffset(offset);
    }

    // ---- Control helpers ----
    private static TextBlock Copy(string text, double size = 12) => new() { Text = text, FontSize = size, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5 };
    private void Section(string title, string? subtitle = null)
    {
        var header = new Grid { Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 26, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new ColumnDefinition());
        header.Children.Add(new TextBlock { Text = "// " + title.ToUpperInvariant(), FontFamily = Mono, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, 0, 12, 0) });
        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center, Background = new LinearGradientBrush(Color.FromArgb(90, 0, 229, 255), Colors.Transparent, 0) };
        Grid.SetColumn(rule, 1); header.Children.Add(rule);
        panel.Children.Add(header);
        if (subtitle != null) { var t = Copy(subtitle); t.Margin = new Thickness(0, 0, 0, 10); panel.Children.Add(t); }
    }
    private void Field(string name, Control control)
    {
        panel.Children.Add(new Label { Content = name, Target = control, Margin = new Thickness(0, 9, 0, 0) });
        AutomationProperties.SetName(control, name.Replace("_", "")); panel.Children.Add(control);
    }
    private ComboBox Choice(string name, IEnumerable<string> values, string selected, Action<string> change)
    {
        var combo = new ComboBox { ItemsSource = values.ToArray(), SelectedItem = selected };
        combo.SelectionChanged += (_, _) => { if (syncing || combo.SelectedItem is not string value) return; change(value); app.Apply(); };
        Field(name, combo); return combo;
    }
    private CheckBox Check(string name, bool value, Action<bool> change, bool rebuild = false)
    {
        var box = new CheckBox { Content = name, IsChecked = value };
        box.Click += (_, _) => { if (syncing) return; change(box.IsChecked == true); app.Apply(); if (rebuild) ShowPage(page); };
        panel.Children.Add(box); return box;
    }
    private Slider Range(string name, double min, double max, double value, double step, Action<double> change, string format = "0")
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, SmallChange = step, LargeChange = step * 5 };
        var label = new Label { Content = $"{name}  ·  {value.ToString(format)}", Target = slider, Margin = new Thickness(0, 9, 0, 0) };
        AutomationProperties.SetName(slider, name.Replace("_", "")); panel.Children.Add(label); panel.Children.Add(slider);
        slider.ValueChanged += (_, _) => { label.Content = $"{name}  ·  {slider.Value.ToString(format)}"; if (syncing) return; change(slider.Value); app.Apply(); };
        return slider;
    }
    private void ColorPicker(string name, string selected, Action<string> change)
    {
        var chip = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 12, 0), BorderBrush = Line, BorderThickness = new Thickness(1), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected)) };
        var label = new TextBlock { Text = selected.ToUpperInvariant(), FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal }; content.Children.Add(chip); content.Children.Add(label);
        var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Left };
        Field(name, button);
        button.Click += (_, _) =>
        {
            using var dialog = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(selected), CustomColors = [0xFFE500, 0xD62BFF, 0x66FF00, 0x00B0FF, 0xFF6B9D, 0x3D4DFF, 0xEDF3F5, 0x1A0A04] };
            if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
            selected = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            chip.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected)); label.Text = selected;
            change(selected); app.Apply();
        };
    }
    private void Note(string text) { var t = Copy(text, 11.5); t.Margin = new Thickness(0, 2, 0, 4); panel.Children.Add(t); }

    // ---- Pages ----
    private void Theme()
    {
        Section("Presets", "Each preset is a starting point. Change anything afterwards on the Style and Effects pages.");
        var gallery = new UniformGridCompat(2);
        var sample = DateTime.Now;
        foreach (var name in Preferences.Presets)
        {
            var look = new Preferences(); look.ApplyPreset(name); look.Hour24 = P.Hour24;
            var face = new ClockFace(); face.Update(look, sample);
            bool active = P.Preset == name;
            var card = new Grid();
            card.RowDefinitions.Add(new RowDefinition { Height = new GridLength(86) }); card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.Children.Add(new Viewbox { Child = face, Stretch = Stretch.Uniform, Margin = new Thickness(6, 8, 6, 4) });
            var title = new TextBlock { Text = (name == "Typography" ? "TYPE" : name.ToUpperInvariant()), FontFamily = Mono, FontSize = 11, Foreground = active ? Accent : Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 2) };
            Grid.SetRow(title, 1); card.Children.Add(title);
            var button = new Button { Content = card, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(6), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BorderBrush = active ? Accent : Line, BorderThickness = new Thickness(active ? 1.5 : 1), Background = active ? new SolidColorBrush(Color.FromRgb(8, 30, 44)) : new LinearGradientBrush(Color.FromRgb(8, 16, 30), Color.FromRgb(14, 10, 30), 45) };
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
                chips.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 4, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) });
            chips.Children.Add(new TextBlock { Text = " " + name.ToUpperInvariant(), FontFamily = Mono, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = active ? Accent : Brushes.White });
            var button = new Button { Content = chips, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 7, 12, 7), BorderBrush = active ? Accent : Line };
            AutomationProperties.SetName(button, name + " palette");
            button.Click += (_, _) => { P.TextColor = text; P.AccentColor = accent; P.UseAccent = true; app.Apply(); ShowPage(page); };
            palettes.Children.Add(button);
        }
        panel.Children.Add(palettes);

        Section("Share a theme", "Copy your look as text to keep a backup or send to a friend. Position and desktop behaviour stay on this PC.");
        var share = new UniformGridCompat(2);
        var feedback = Copy(""); feedback.Margin = new Thickness(0, 4, 0, 0);
        var copy = new Button { Content = "Copy theme", Margin = new Thickness(0, 0, 8, 0) };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(P.ExportTheme()); feedback.Text = "Theme copied to the clipboard."; feedback.Foreground = Accent; }
            catch (System.Runtime.InteropServices.COMException) { feedback.Text = "The clipboard is busy. Try again."; feedback.Foreground = Magenta; }
        };
        var paste = new Button { Content = "Paste theme", Margin = new Thickness(0, 0, 8, 0) };
        paste.Click += (_, _) =>
        {
            string? text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch (System.Runtime.InteropServices.COMException) { }
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
        Note("Poster: a huge day number with the month across it and the weekday up the side. Script: a tall day number, the month up the side, a handwritten weekday and a bold time. Alignment, date format and weekday style apply to Classic.");
        Check("Fade the big day number", P.Fade, v => P.Fade = v);
        var allFonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        string[] scripts = ["Segoe Script", "Ink Free", "Segoe Print", "Gabriola", "Lucida Handwriting", "Brush Script MT", "Mistral"];
        var scriptChoices = scripts.Where(allFonts.Contains).Concat(allFonts.Where(f => !scripts.Contains(f))).ToList();
        if (!scriptChoices.Contains(P.ScriptFont)) scriptChoices.Insert(0, P.ScriptFont);
        Choice("Script _handwriting font", scriptChoices, P.ScriptFont, v => P.ScriptFont = v);
        Range("Script day-number _width", .35, 1, P.Condense, .05, v => P.Condense = v, "0%");

        Section("Typeface");
        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        if (!fonts.Contains(P.Font)) fonts.Insert(0, P.Font);
        Choice("_Font", fonts, P.Font, v => P.Font = v);
        Choice("_Weight", Preferences.Weights, P.FontWeight, v => P.FontWeight = v);
        Range("_Size", 32, 160, P.FontSize, 1, v => P.FontSize = v);
        Range("Letter _spacing", -4, 24, P.LetterSpacing, .5, v => P.LetterSpacing = v, "0.0");

        Section("Colour");
        ColorPicker("_Text colour", P.TextColor, v => P.TextColor = v);
        ColorPicker("_Accent colour", P.AccentColor, v => P.AccentColor = v);
        Check("Use accent for secondary text, glow and indicators", P.UseAccent, v => P.UseAccent = v);

        Section("Time", P.Advanced ? "A custom template controls the text. Turn it off in Advanced to use the time and date options." : null);
        Choice("Time _format", ["24-hour", "12-hour"], P.Hour24 ? "24-hour" : "12-hour", v => P.Hour24 = v == "24-hour");
        Check("Show s_econds", P.Seconds, v => P.Seconds = v);
        Check("Show A_M/PM in 12-hour time", P.AmPm, v => P.AmPm = v);

        Section("Date");
        Check("Show _weekday", P.Weekday, v => P.Weekday = v);
        Choice("Weekday st_yle", Preferences.WeekdayStyles, P.WeekdayStyle, v => P.WeekdayStyle = v);
        Note("Line shows the weekday as a large line of its own.");
        Check("Show _date", P.Date, v => P.Date = v);
        Choice("Date f_ormat", Preferences.DateStyles, P.DateStyle, v => P.DateStyle = v);
        Note("Long: 12 November 2026 · Short: 12 Nov · ISO: 2026-11-12 · Numeric: your regional short date.");
        Check("_Uppercase date line", P.Uppercase, v => P.Uppercase = v);

        Section("Layout");
        Choice("A_lignment", ["Left", "Center", "Right"], P.Alignment, v => P.Alignment = v);
        scale = Range("Overall sca_le", .5, 2, P.Scale, .05, v => P.Scale = v, "0.00'×'");
        Note("You can also drag the clock’s lower-right corner while it is unlocked.");
        Range("Widget _opacity", .2, 1, P.Opacity, .05, v => P.Opacity = v, "0%");
    }

    private void Effects()
    {
        Section("Glow");
        Check("_Neon glow", P.Glow, v => P.Glow = v);
        Range("Glow _strength", .1, 1, P.GlowStrength, .05, v => P.GlowStrength = v, "0%");
        Check("_Gradient text (text colour to accent)", P.Gradient, v => P.Gradient = v);

        Section("Motion");
        Choice("Seconds _indicator", Preferences.Indicators, P.SecondsIndicator, v => P.SecondsIndicator = v);
        Note("Bar sweeps across each minute. Segments light in 2-second steps. Dots marks every second.");
        Check("_Blink the time separator", P.BlinkSeparator, v => P.BlinkSeparator = v);

        Section("Frame & header");
        Choice("_Frame", Preferences.Frames, P.Frame, v => P.Frame = v);
        var caption = new TextBox { Text = P.Caption, MaxLength = 40 };
        Field("Header _caption", caption);
        caption.TextChanged += (_, _) => { if (syncing) return; P.Caption = caption.Text; app.Apply(); };
        Note("A short label above the time, such as SYS.CHRONO or HOME OFFICE. Leave empty to hide it.");
        Check("Show time _zone tag", P.ShowZone, v => P.ShowZone = v);

        Section("Surface");
        Choice("_Background", ["Transparent", "Card", "Solid"], P.Background, v => P.Background = v);
        ColorPicker("Surface _tint", P.SurfaceColor, v => P.SurfaceColor = v);
        Range("Surface o_pacity", .1, 1, P.BackgroundOpacity, .01, v => P.BackgroundOpacity = v, "0%");
        Range("Corner _radius", 0, 48, P.CornerRadius, 1, v => P.CornerRadius = v);
        Check("CRT _scanlines", P.Scanlines, v => P.Scanlines = v);
        Note("Scanlines appear on Card and Solid surfaces. Surface opacity affects the panel only; text stays crisp. There is no wallpaper blur.");
    }

    private void Desktop()
    {
        Section("Position & interaction");
        locked = Check("_Lock position", P.Locked, v => { P.Locked = v; if (!v) P.ClickThrough = false; SyncBehavior(); });
        clickThrough = Check("Enable _click-through while locked", P.ClickThrough, v => P.ClickThrough = v);
        clickThrough.IsEnabled = P.Locked;
        panel.Children.Add(Copy("Click-through sends mouse input to the windows behind the clock. Use the tray menu’s “Unlock and restore interaction” action to regain control."));
        topmost = Check("_Always on top", P.Topmost, v => P.Topmost = v);
        panel.Children.Add(Copy("Off by default. Still normally follows standard window stacking. It is not attached to the wallpaper: Windows + D may hide it. Use Show clock in the tray to restore it."));
        Section("Startup");
        var login = Check("_Launch Still when I sign in", P.LaunchAtLogin, v => app.SetLogin(v));
        login.Click += (_, _) => { syncing = true; login.IsChecked = P.LaunchAtLogin; syncing = false; UpdateStatus(); };
        panel.Children.Add(Copy("Optional and off initially. Keep the app in a permanent folder before enabling this. No administrator access is needed."));
        Section("Your displays");
        foreach (var d in NativeDesktop.Displays())
        {
            var button = new Button { Content = $"Move to {(d.Primary ? "primary display" : d.Name)}  ·  {d.DpiScale:0.##}×", Margin = new Thickness(0, 4, 0, 4) };
            button.Click += (_, _) => { P.Position = new SavedPosition(d.Name, 64, 64); app.Clock.RestorePosition(); app.Apply(); };
            panel.Children.Add(button);
        }
        panel.Children.Add(Copy("Position is remembered per saved display. If that display disconnects, Still moves to the primary display and stays within its working area."));
        Section("About Still", "Still 2.0 · A futuristic desktop clock for Windows 11\nOriginal WPF design inspired by HTML Clock for Plasma by Marcin Orlowski (MIT). See THIRD-PARTY-NOTICES.md for attribution.");
    }

    private void Advanced()
    {
        int behaviorStart = panel.Children.Count;
        Desktop();
        var behavior = new StackPanel();
        while (panel.Children.Count > behaviorStart) { var child = panel.Children[behaviorStart]; panel.Children.RemoveAt(behaviorStart); behavior.Children.Add(child); }
        panel.Children.Add(new Expander { Header = "Position & startup", Content = behavior, Margin = new Thickness(0, 0, 0, 0) });
        Section("Optional custom text", "Up to three lines of plain text with a small set of placeholders. Fonts, colours, effects, frame and indicator still apply.");
        Check("Use custom _template", P.Advanced, v => P.Advanced = v);
        var editor = new TextBox { Text = P.Template, AcceptsReturn = true, Height = 126, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = Mono, MaxLength = 240 };
        Field("_Layout text", editor);
        var validation = Copy("Template ready. Changes preview and save immediately."); validation.Margin = new Thickness(0, 8, 0, 14); panel.Children.Add(validation);
        editor.TextChanged += (_, _) =>
        {
            string? error = TemplateFormatter.Validate(editor.Text);
            validation.Text = error ?? "Template ready. Changes preview and save immediately.";
            validation.Foreground = error == null ? Accent : Magenta;
            if (error != null) return; // Last valid template remains active and saved.
            P.Template = editor.Text; app.Apply();
        };
        panel.Children.Add(Copy("Insert a placeholder at the cursor:"));
        var tokens = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        foreach (string token in TemplateFormatter.Tokens)
        {
            var button = new Button { Content = "{" + token + "}", FontFamily = Mono, FontSize = 11, Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) =>
            {
                string insert = "{" + token + "}";
                if (editor.Text.Length - editor.SelectionLength + insert.Length > editor.MaxLength)
                { validation.Text = "The template is limited to 240 characters."; return; }
                int start = editor.SelectionStart;
                editor.SelectedText = insert; editor.Select(start + insert.Length, 0); editor.Focus();
            };
            tokens.Children.Add(button);
        }
        panel.Children.Add(tokens);
        Section("Placeholder guide");
        panel.Children.Add(Copy("{time}  Hours and minutes; follows 12/24-hour and seconds settings.\n{hour}  Two-digit hour in the selected time format.\n{minute} / {second}  Two-digit minute / second.\n{ampm}  AM or PM in 12-hour mode; empty in 24-hour mode.\n{weekday}  Full weekday name.\n{date}  Day, full month and year.\n{day} / {month} / {year}  Two-digit day, full month, four-digit year.\n\nNames follow your Windows regional settings. The first line is large; the following lines use smaller secondary text."));
        Section("Simple by design");
        panel.Children.Add(Copy("Placeholders are case-sensitive. Unknown names, unmatched braces and more than three lines show a helpful error; the last valid template stays active. Limits: 240 characters and three lines.\n\nEverything else is literal text. There is no HTML, CSS, JavaScript, remote loading, command execution or expression evaluation."));
    }

    private sealed class UniformGridCompat : System.Windows.Controls.Primitives.UniformGrid
    { public UniformGridCompat(int columns) { Columns = columns; } }
}
