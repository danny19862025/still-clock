using System.Windows.Automation;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace Still.App;

public sealed class SettingsWindow : Window
{
    private readonly ClockApplication app;
    private readonly ClockFace preview = new();
    private readonly StackPanel panel = new();
    private readonly ScrollViewer scroll = new();
    private readonly TextBlock status = new();
    private readonly TextBlock previewCaption = new();
    private readonly TextBlock pageTitle = new();
    private readonly Dictionary<string, Button> navigation = [];
    private string page = "Appearance";
    private CheckBox? locked, clickThrough, topmost;
    private Slider? scale;
    private bool syncing;
    private Preferences P => app.Preferences;
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(160, 177, 189));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(41, 225, 239));

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
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(10, 17, 23)) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
        Content = root;
        var titlebar = new Border { Background = new SolidColorBrush(Color.FromRgb(16, 25, 32)), BorderBrush = new SolidColorBrush(Color.FromRgb(44, 60, 69)), BorderThickness = new Thickness(0,0,0,1) };
        root.Children.Add(titlebar);
        var titleGrid = new Grid(); titlebar.Child = titleGrid;
        titleGrid.Children.Add(new TextBlock { Text = "◷   STILL", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(28,0,0,0), VerticalAlignment = VerticalAlignment.Center });
        titlebar.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else DragMove(); };
        var chrome = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        titleGrid.Children.Add(chrome);
        foreach (var (text, action) in new (string, Action)[] { ("−", () => WindowState = WindowState.Minimized), ("□", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized), ("×", Close) })
        {
            var button = new Button { Content = text, Width = 48, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 21, Padding = new Thickness(0) };
            button.Click += (_, _) => action(); chrome.Children.Add(button);
        }
        var body = new Grid { Margin = new Thickness(30,26,30,24) }; Grid.SetRow(body,1); root.Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
        var left = new Grid { Margin = new Thickness(0,0,36,0) }; body.Children.Add(left);
        var glow = new Border { Height = 230, Background = new RadialGradientBrush(Color.FromArgb(45,30,219,238), Colors.Transparent), CornerRadius = new CornerRadius(100) }; left.Children.Add(glow);
        var viewbox = new Viewbox { Child = preview, Stretch = Stretch.Uniform, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }; left.Children.Add(viewbox);
        previewCaption.Foreground = Muted; previewCaption.FontSize = 11; previewCaption.VerticalAlignment = VerticalAlignment.Bottom; previewCaption.HorizontalAlignment = HorizontalAlignment.Center;
        left.Children.Add(previewCaption);
        var right = new Grid { Margin = new Thickness(30,0,0,0) }; Grid.SetColumn(right,1); body.Children.Add(right);
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition());
        var header = new StackPanel(); right.Children.Add(header);
        pageTitle.Text = "Settings"; pageTitle.FontSize = 30; pageTitle.FontWeight = FontWeights.Light; pageTitle.Margin = new Thickness(0,0,0,18); header.Children.Add(pageTitle);
        var nav = new UniformGridCompat(2); header.Children.Add(nav);
        foreach (var name in new[] { "Appearance", "Advanced" })
        {
            var button = new Button { Content = name, Margin = new Thickness(0,0,6,20), Padding = new Thickness(5,10,5,10), FontSize = 13, BorderThickness = new Thickness(0,0,0,2) };
            button.Click += (_, _) => ShowPage(name); nav.Children.Add(button); navigation[name] = button;
        }
        scroll.Content = panel; scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.Padding = new Thickness(0,0,6,0); Grid.SetRow(scroll,1); right.Children.Add(scroll);
        var footer = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(44,60,69)), BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(30,16,30,16) }; Grid.SetRow(footer,2); root.Children.Add(footer);
        var actions = new Grid(); footer.Child = actions;
        var restore = new Button { Content = "Show clock", Width = 150, HorizontalAlignment = HorizontalAlignment.Left }; restore.Click += (_, _) => app.Unlock(); actions.Children.Add(restore);
        status.FontSize = 11; status.Foreground = Muted; status.VerticalAlignment = VerticalAlignment.Center; status.HorizontalAlignment = HorizontalAlignment.Center; status.MaxWidth = 460; status.TextWrapping = TextWrapping.Wrap; actions.Children.Add(status);
        var done = new Button { Content = "_Done", Width = 128, HorizontalAlignment = HorizontalAlignment.Right, Background = Accent, Foreground = Brushes.Black, BorderBrush = Accent }; done.Click += (_, _) => Close(); actions.Children.Add(done);
        SizeChanged += (_, _) => {
            bool compact = ActualWidth < 850;
            left.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            body.ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(1,GridUnitType.Star);
            right.Margin = compact ? new Thickness(0) : new Thickness(30,0,0,0);
            status.MaxWidth = compact ? 220 : 460;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is >= Key.D1 and <= Key.D2)
            { ShowPage(new[] { "Appearance", "Advanced" }[(int)e.Key - (int)Key.D1]); e.Handled = true; }
        };
        app.Updated += OnUpdated;
        Closed += (_, _) => app.Updated -= OnUpdated;
        ShowPage(page); RefreshPreview(DateTime.Now);
    }

    private void OnUpdated() { RefreshPreview(DateTime.Now); UpdateStatus(); }
    public void RefreshPreview(DateTime time)
    {
        preview.Update(P, time);
        previewCaption.Text = $"{P.Preset}  /  {P.Scale:0.00}×  /  {(P.Background == "Card" ? "Translucent card" : P.Background == "Solid" ? "Solid dark card" : "Transparent on your wallpaper")}";
        if (scale != null && Math.Abs(scale.Value - P.Scale) > .001) { syncing = true; scale.Value = P.Scale; syncing = false; }
    }
    public void UpdateStatus() => status.Text = app.StartupNotice ?? app.Store.Notice ?? "●  Saved automatically";
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
        scroll.ScrollToTop();
        foreach (var (key, button) in navigation)
        {
            button.Background = Brushes.Transparent;
            button.Foreground = key == name ? Brushes.White : Muted;
            button.BorderBrush = key == name ? Accent : new SolidColorBrush(Color.FromRgb(44,60,69));
        }
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
        Section("Preset");
        var presets = new UniformGridCompat(3);
        foreach (var name in new[] { "Minimal", "Typography", "Glass" })
        {
            var button = new Button { Content = name == "Typography" ? "Type" : name, Margin = new Thickness(0,0,8,16), Padding = new Thickness(8,10,8,10), BorderBrush = P.Preset == name ? Accent : new SolidColorBrush(Color.FromRgb(56,75,86)), Background = P.Preset == name ? new SolidColorBrush(Color.FromRgb(19,55,65)) : new SolidColorBrush(Color.FromRgb(17,28,36)) };
            AutomationProperties.SetName(button, name + " preset");
            button.Click += (_, _) => { P.ApplyPreset(name); app.Apply(); ShowPage("Appearance"); app.Clock.RestorePosition(); }; presets.Children.Add(button);
        }
        panel.Children.Add(presets);
        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        if (!fonts.Contains(P.Font)) fonts.Insert(0,P.Font);
        Choice("_Font", fonts, P.Font, v => P.Font = v);
        Range("_Size",32,160,P.FontSize,1,v => P.FontSize=v);
        Section("Colour");
        var colours = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
        foreach (string hex in new[] { "#F5F3ED", "#29E1EF", "#B9A0F5", "#FF8080" })
        {
            var swatch = new Button { Width = 34, Height = 34, Margin = new Thickness(0,0,16,0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), BorderBrush = P.TextColor.Equals(hex,StringComparison.OrdinalIgnoreCase) ? Accent : Brushes.Transparent, BorderThickness = new Thickness(3), ToolTip = hex };
            swatch.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border CornerRadius='17' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'/></ControlTemplate>");
            AutomationProperties.SetName(swatch,"Text colour " + hex);
            swatch.Click += (_, _) => { P.TextColor = hex; app.Apply(); ShowPage("Appearance"); }; colours.Children.Add(swatch);
        }
        panel.Children.Add(colours);
        Check("Show _date",P.Date,v => P.Date=v);
        Check("_Glass effect",P.Background == "Card",v => P.Background = v ? "Card" : "Transparent");
        int start = panel.Children.Count;
        Section("TIME & DATE", P.Advanced ? "Your advanced template currently controls the text. Turn it off in Advanced to use these visibility options." : null);
        Choice("Time _format", new[] { "24-hour", "12-hour" }, P.Hour24 ? "24-hour" : "12-hour", v => P.Hour24 = v == "24-hour");
        Check("Show _seconds", P.Seconds, v => P.Seconds = v);
        Check("Show A_M/PM in 12-hour time", P.AmPm, v => P.AmPm = v);
        Check("Show _weekday", P.Weekday, v => P.Weekday = v);

        ColorPicker("Custom text colour", P.TextColor, v => P.TextColor = v);
        Check("Use accent colour for secondary text",P.UseAccent,v => P.UseAccent=v);
        ColorPicker("Accent colour",P.AccentColor,v => P.AccentColor=v);
        Section("SURFACE & LAYOUT");
        Choice("_Background style", new[] { "Transparent", "Solid", "Card" }, P.Background, v => P.Background = v);
        Range("Card _opacity", .1, 1, P.BackgroundOpacity, .01, v => P.BackgroundOpacity = v, "0%");
        panel.Children.Add(Copy("Card opacity affects the surface only. Text stays crisp. Glass is a translucent tint, without wallpaper blur."));
        Choice("_Alignment", new[] { "Left", "Center", "Right" }, P.Alignment, v => P.Alignment = v);
        scale = Range("Overall sca_le", .5, 2, P.Scale, .05, v => P.Scale = v, "0.00'×'");
        panel.Children.Add(Copy("You can also drag the clock’s lower-right corner while unlocked."));
        var more = new StackPanel();
        while (panel.Children.Count > start) { var child = panel.Children[start]; panel.Children.RemoveAt(start); more.Children.Add(child); }
        panel.Children.Add(new Expander { Header = "More options", Content = more });
        var reset = new Button { Content = "Reset defaults", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,16,0,0) };
        reset.Click += (_, _) => { app.Reset(); ShowPage(page); }; more.Children.Add(reset);
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
        Section("ABOUT STILL", "Still 1.1 · A quiet clock for Windows 11\nOriginal WPF design inspired by HTML Clock for Plasma by Marcin Orlowski (MIT). See THIRD-PARTY-NOTICES.md for attribution.");
    }

    private void Advanced()
    {
        int behaviorStart = panel.Children.Count;
        Desktop();
        var behavior = new StackPanel();
        while (panel.Children.Count > behaviorStart) { var child = panel.Children[behaviorStart]; panel.Children.RemoveAt(behaviorStart); behavior.Children.Add(child); }
        panel.Children.Add(new Expander { Header = "Position & startup", Content = behavior });
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
        Section("PLACEHOLDER GUIDE");
        panel.Children.Add(Copy("{time}  Hours and minutes; follows 12/24-hour and seconds settings.\n{hour}  Two-digit hour in the selected time format.\n{minute} / {second}  Two-digit minute / second.\n{ampm}  AM or PM in 12-hour mode; empty in 24-hour mode.\n{weekday}  Full weekday name.\n{date}  Day, full month and year.\n{day} / {month} / {year}  Two-digit day, full month, four-digit year.\n\nNames follow your Windows regional settings. The first line is large; the following lines use smaller secondary text. Font, colours, alignment, card and scale still apply."));
        Section("SIMPLE BY DESIGN");
        panel.Children.Add(Copy("Placeholders are case-sensitive. Unknown names, unmatched braces and more than three lines show a helpful error; the last valid template stays active. Limits: 240 characters and three lines.\n\nEverything else is literal text. There is no HTML, CSS, JavaScript, remote loading, command execution or expression evaluation."));
    }

    private sealed class UniformGridCompat : System.Windows.Controls.Primitives.UniformGrid
    { public UniformGridCompat(int columns) { Columns = columns; } }
}
