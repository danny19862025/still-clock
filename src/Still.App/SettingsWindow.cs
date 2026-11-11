using System.Windows.Automation;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace Still.App;

public sealed class SettingsWindow : Window
{
    private readonly ClockApplication app;
    private readonly ClockFace preview = new();
    private readonly StackPanel panel = new();
    private readonly TextBlock status = new();
    private readonly TextBlock previewCaption = new();
    private readonly TextBlock pageTitle = new();
    private readonly Dictionary<string, Button> navigation = [];
    private string page = "Appearance";
    private CheckBox? locked, clickThrough, topmost;
    private Slider? scale;
    private bool syncing;
    private Preferences P => app.Preferences;
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(174, 184, 179));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(182, 205, 189));

    public SettingsWindow(ClockApplication application)
    {
        app = application;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Still — Settings";
        Width = Math.Min(1060, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(830, SystemParameters.WorkArea.Height - 32);
        MinWidth = Math.Min(760, SystemParameters.WorkArea.Width - 32);
        MinHeight = Math.Min(540, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/still.ico"));
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(20, 28, 32)) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        Content = root;

        var left = new Grid { Margin = new Thickness(32, 28, 32, 24) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(left);
        var brand = new StackPanel();
        brand.Children.Add(new TextBlock { Text = "◷  STILL", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Accent });
        brand.Children.Add(new TextBlock { Text = "Make time\nyours.", FontSize = 38, FontWeight = FontWeights.Light, Margin = new Thickness(0, 30, 0, 12), LineHeight = 44 });
        brand.Children.Add(Copy("A little clarity. A quieter desktop.", 14));
        left.Children.Add(brand);

        var stage = new Grid { Margin = new Thickness(0, 26, 0, 26) };
        Grid.SetRow(stage, 1); left.Children.Add(stage);
        var stageBackground = new Border
        {
            CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(62, 83, 78)),
            Background = new LinearGradientBrush(Color.FromRgb(33, 59, 65), Color.FromRgb(54, 73, 61), 35), Padding = new Thickness(20)
        };
        stage.Children.Add(stageBackground);
        var stageGrid = new Grid(); stageBackground.Child = stageGrid;
        stageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stageGrid.RowDefinitions.Add(new RowDefinition());
        stageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var badge = Copy("LIVE PREVIEW", 10); badge.Foreground = Accent; stageGrid.Children.Add(badge);
        var viewbox = new Viewbox { Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(0, 20, 0, 20) };
        Grid.SetRow(viewbox, 1); stageGrid.Children.Add(viewbox);
        previewCaption.TextWrapping = TextWrapping.Wrap; previewCaption.Foreground = Accent; previewCaption.FontSize = 11;
        Grid.SetRow(previewCaption, 2); stageGrid.Children.Add(previewCaption);
        var footer = new StackPanel(); Grid.SetRow(footer, 2); left.Children.Add(footer);
        footer.Children.Add(Copy("Drag the clock to move it.\nDouble-click it to return here.", 12));
        var restore = new Button { Content = "_Unlock & bring clock into view", Margin = new Thickness(0, 16, 0, 12) };
        restore.Click += (_, _) => app.Unlock(); footer.Children.Add(restore);
        footer.Children.Add(Copy("Find Still in the notification area,\neven when the clock is hidden or click-through.", 11));

        var right = new Border { Background = new SolidColorBrush(Color.FromRgb(26, 36, 41)), Padding = new Thickness(30, 28, 30, 18) };
        Grid.SetColumn(right, 1); root.Children.Add(right);
        var rightGrid = new Grid(); right.Child = rightGrid;
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition());
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel(); rightGrid.Children.Add(header);
        var nav = new UniformGridCompat(3);
        foreach (var name in new[] { "Appearance", "Desktop", "Advanced" })
        {
            var button = new Button { Content = name, Margin = new Thickness(0, 0, 6, 18), Padding = new Thickness(6, 9, 6, 9), FontSize = 12 };
            button.Click += (_, _) => ShowPage(name); nav.Children.Add(button); navigation[name] = button;
        }
        header.Children.Add(nav);
        var compactPreview = new Border
        {
            Height = 130, CornerRadius = new CornerRadius(14), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 14),
            Background = stageBackground.Background, Visibility = Visibility.Collapsed
        };
        header.Children.Add(compactPreview);
        // Preserve a usable settings form and live preview on smaller/scaled displays.
        bool compact = false;
        SizeChanged += (_, _) =>
        {
            bool next = root.ActualWidth < 850;
            if (next == compact) return;
            compact = next;
            if (compact)
            {
                stageGrid.Children.Remove(viewbox); compactPreview.Child = viewbox;
                viewbox.Margin = new Thickness(0); compactPreview.Visibility = Visibility.Visible;
                left.Visibility = Visibility.Collapsed; root.ColumnDefinitions[0].Width = new GridLength(0);
            }
            else
            {
                compactPreview.Child = null; stageGrid.Children.Add(viewbox);
                viewbox.Margin = new Thickness(0, 20, 0, 20); compactPreview.Visibility = Visibility.Collapsed;
                left.Visibility = Visibility.Visible; root.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            }
        };
        pageTitle.FontSize = 25; pageTitle.FontWeight = FontWeights.Light; pageTitle.Margin = new Thickness(0, 0, 0, 18); header.Children.Add(pageTitle);
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 12, 0) };
        Grid.SetRow(scroll, 1); rightGrid.Children.Add(scroll);
        var bottom = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; Grid.SetRow(bottom, 2); rightGrid.Children.Add(bottom);
        status.FontSize = 11; status.Foreground = Accent; status.TextWrapping = TextWrapping.Wrap; bottom.Children.Add(status);
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0) }; bottom.Children.Add(actions);
        var reset = new Button { Content = "_Reset defaults", Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) => { app.Reset(); ShowPage(page); }; actions.Children.Add(reset);
        var done = new Button { Content = "_Done", MinWidth = 92, HorizontalAlignment = HorizontalAlignment.Right, Background = new SolidColorBrush(Color.FromRgb(63, 88, 74)) };
        done.Click += (_, _) => Close(); actions.Children.Add(done);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is >= Key.D1 and <= Key.D3)
            { ShowPage(new[] { "Appearance", "Desktop", "Advanced" }[(int)e.Key - (int)Key.D1]); e.Handled = true; }
        };
        app.Updated += OnUpdated;
        Closed += (_, _) => app.Updated -= OnUpdated;
        ShowPage(page); RefreshPreview(DateTime.Now);
    }

    private void OnUpdated() { RefreshPreview(DateTime.Now); UpdateStatus(); }
    public void RefreshPreview(DateTime time)
    {
        preview.Update(P, time);
        previewCaption.Text = $"{P.Preset}  /  {P.Scale:0.00}×  /  {(P.Background == "Card" ? "Translucent card" : "Transparent on your wallpaper")}";
        if (scale != null && Math.Abs(scale.Value - P.Scale) > .001) { syncing = true; scale.Value = P.Scale; syncing = false; }
    }
    public void UpdateStatus() => status.Text = app.StartupNotice ?? app.Store.Notice ?? "Changes save automatically · Works entirely offline";
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
        page = name; panel.Children.Clear(); locked = clickThrough = topmost = null; scale = null;
        pageTitle.Text = name == "Appearance" ? "Your clock, your way" : name == "Desktop" ? "At home on your desktop" : "A few words of your own";
        foreach (var (key, button) in navigation) button.Background = new SolidColorBrush(key == name ? Color.FromRgb(63, 85, 76) : Color.FromRgb(35, 49, 54));
        if (name == "Appearance") Appearance(); else if (name == "Desktop") Desktop(); else Advanced();
        UpdateStatus();
    }
    private static TextBlock Copy(string text, double size = 12) => new() { Text = text, FontSize = size, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.5 };
    private void Section(string title, string? subtitle = null)
    {
        panel.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 24, 0, 9) });
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
    private CheckBox Check(string name, bool value, Action<bool> change)
    {
        var box = new CheckBox { Content = name, IsChecked = value };
        box.Click += (_, _) => { if (syncing) return; change(box.IsChecked == true); app.Apply(); };
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
        var button = new Button { Content = "Choose colour…   " + selected, HorizontalContentAlignment = HorizontalAlignment.Left };
        Field(name, button);
        button.Click += (_, _) =>
        {
            using var dialog = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(selected), CustomColors = new[] { 0xEDF3F5, 0xBDCDB6, 0xB8D8E5, 0xC7B5C9, 0x28231E } };
            if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
            selected = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            button.Content = "Choose colour…   " + selected; change(selected); app.Apply();
        };
    }
    private void Appearance()
    {
        Section("START WITH A PRESET", "Three considered starting points. Make any of them your own.");
        var presets = new UniformGridCompat(3);
        foreach (var name in new[] { "Minimal", "Typography", "Glass" })
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = "09:41", FontSize = name == "Typography" ? 27 : 24, FontWeight = FontWeights.Light, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = name == "Typography" ? "Thursday" : "THU · 12 NOV", FontSize = name == "Typography" ? 12 : 8, Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 13) });
            content.Children.Add(new TextBlock { Text = name, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            var button = new Button { Content = content, Padding = new Thickness(8, 14, 8, 10), Margin = new Thickness(0, 0, 7, 0), BorderBrush = P.Preset == name ? Accent : new SolidColorBrush(Color.FromRgb(65, 81, 77)), Background = new SolidColorBrush(name == "Glass" ? Color.FromRgb(49, 67, 70) : Color.FromRgb(31, 43, 48)) };
            AutomationProperties.SetName(button, name + " preset");
            button.Click += (_, _) => { P.ApplyPreset(name); app.Apply(); ShowPage("Appearance"); app.Clock.RestorePosition(); };
            presets.Children.Add(button);
        }
        panel.Children.Add(presets);
        Section("TYPE & COLOUR");
        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        if (!fonts.Contains(P.Font)) fonts.Insert(0, P.Font);
        Choice("_Font", fonts, P.Font, v => P.Font = v);
        Range("Font _size", 32, 160, P.FontSize, 1, v => P.FontSize = v);
        ColorPicker("_Text colour", P.TextColor, v => P.TextColor = v);
        Check("Use _accent colour for secondary text", P.UseAccent, v => P.UseAccent = v);
        ColorPicker("Accent _colour", P.AccentColor, v => P.AccentColor = v);
        Section("TIME & DATE", P.Advanced ? "Your advanced template currently controls the text. Turn it off in Advanced to use these visibility options." : null);
        Choice("Time _format", new[] { "24-hour", "12-hour" }, P.Hour24 ? "24-hour" : "12-hour", v => P.Hour24 = v == "24-hour");
        Check("Show _seconds", P.Seconds, v => P.Seconds = v);
        Check("Show A_M/PM in 12-hour time", P.AmPm, v => P.AmPm = v);
        Check("Show _weekday", P.Weekday, v => P.Weekday = v);
        Check("Show _date", P.Date, v => P.Date = v);
        Section("SURFACE & LAYOUT");
        Choice("_Background style", new[] { "Transparent", "Card" }, P.Background, v => P.Background = v);
        Range("Card _opacity", .1, 1, P.BackgroundOpacity, .01, v => P.BackgroundOpacity = v, "0%");
        panel.Children.Add(Copy("Card opacity affects the surface only. Text stays crisp. Glass is a translucent tint, without wallpaper blur."));
        Choice("_Alignment", new[] { "Left", "Center", "Right" }, P.Alignment, v => P.Alignment = v);
        scale = Range("Overall sca_le", .5, 2, P.Scale, .05, v => P.Scale = v, "0.00'×'");
        panel.Children.Add(Copy("You can also drag the clock’s lower-right corner while unlocked."));
    }

    private void Desktop()
    {
        Section("POSITION & INTERACTION");
        locked = Check("_Lock position", P.Locked, v => { P.Locked = v; if (!v) P.ClickThrough = false; SyncBehavior(); });
        clickThrough = Check("Enable _click-through while locked", P.ClickThrough, v => P.ClickThrough = v);
        clickThrough.IsEnabled = P.Locked;
        panel.Children.Add(Copy("Click-through sends mouse input to the windows behind the clock. Use the tray menu’s “Unlock and restore interaction” action to regain control."));
        topmost = Check("_Always on top", P.Topmost, v => P.Topmost = v);
        panel.Children.Add(Copy("Off by default. Still normally follows standard window stacking. It is not attached to the wallpaper: Windows + D may hide it. Use Show clock in the tray to restore it."));
        Section("STARTUP");
        var login = Check("_Launch Still when I sign in", P.LaunchAtLogin, v => app.SetLogin(v));
        login.Click += (_, _) => { syncing = true; login.IsChecked = P.LaunchAtLogin; syncing = false; UpdateStatus(); };
        panel.Children.Add(Copy("Optional and off initially. Keep the app in a permanent folder before enabling this. No administrator access is needed."));
        Section("YOUR DISPLAYS");
        foreach (var d in NativeDesktop.Displays())
        {
            var button = new Button { Content = $"Move to {(d.Primary ? "primary display" : d.Name)}  ·  {d.DpiScale:0.##}×", Margin = new Thickness(0, 4, 0, 4) };
            button.Click += (_, _) => { P.Position = new SavedPosition(d.Name, 64, 64); app.Clock.RestorePosition(); app.Apply(); };
            panel.Children.Add(button);
        }
        panel.Children.Add(Copy("Position is remembered per saved display. If that display disconnects, Still moves to the primary display and stays within its working area."));
        Section("ABOUT STILL", "Still 1.0 · A quiet clock for Windows 11\nOriginal WPF design inspired by HTML Clock for Plasma by Marcin Orlowski (MIT). See THIRD-PARTY-NOTICES.md for attribution.");
    }

    private void Advanced()
    {
        Section("OPTIONAL CUSTOM TEXT", "Basic customization never needs a template. This editor gives you up to three lines of plain text with a small set of placeholders.");
        Check("Use custom _template", P.Advanced, v => P.Advanced = v);
        var editor = new TextBox { Text = P.Template, AcceptsReturn = true, Height = 126, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Cascadia Mono, Consolas"), MaxLength = 240 };
        Field("_Layout text", editor);
        var validation = Copy("Template ready. Changes preview and save immediately."); validation.Margin = new Thickness(0, 8, 0, 14); panel.Children.Add(validation);
        editor.TextChanged += (_, _) =>
        {
            string? error = TemplateFormatter.Validate(editor.Text);
            validation.Text = error ?? "Template ready. Changes preview and save immediately.";
            validation.Foreground = error == null ? Accent : new SolidColorBrush(Color.FromRgb(255, 192, 169));
            if (error != null) return; // Last valid template remains active and saved.
            P.Template = editor.Text; app.Apply();
        };
        panel.Children.Add(Copy("Insert a placeholder at the cursor:"));
        var tokens = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        foreach (string token in TemplateFormatter.Tokens)
        {
            var button = new Button { Content = "{" + token + "}", FontSize = 11, Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) => { editor.SelectedText = "{" + token + "}"; editor.CaretIndex = editor.SelectionStart + editor.SelectionLength; editor.Focus(); };
            tokens.Children.Add(button);
        }
        panel.Children.Add(tokens);
        Section("PLACEHOLDER GUIDE");
        panel.Children.Add(Copy("{time}  Hours and minutes; follows 12/24-hour and seconds settings.\n{hour}  Two-digit hour in the selected time format.\n{minute} / {second}  Two-digit minute / second.\n{ampm}  AM or PM in 12-hour mode; empty in 24-hour mode.\n{weekday}  Full weekday name.\n{date}  Day, full month and year.\n{day} / {month} / {year}  Two-digit day, full month, four-digit year.\n\nNames follow your Windows regional settings. The first line is large; the following lines use smaller secondary text. Font, colours, alignment, card and scale still apply."));
        Section("SIMPLE BY DESIGN");
        panel.Children.Add(Copy("Placeholders are case-sensitive. Unknown names, unmatched braces and more than three lines show a helpful error; the last valid template stays active. Limits: 240 characters and three lines.\n\nEverything else is literal text. There is no HTML, CSS, JavaScript, remote loading, command execution or expression evaluation."));
    }

    private sealed class UniformGridCompat : System.Windows.Controls.Primitives.UniformGrid
    { public UniformGridCompat(int columns) { Columns = columns; } }
}
