using System.Globalization;

namespace Still.App;

// Shared by the desktop widget, live preview, and Windows screenshot exporter.
public sealed class ClockFace : FrameworkElement
{
    private Preferences p = new();
    private DateTime now;
    private Typeface typeface = new("Segoe UI");
    private double cell;
    private double bodyWidth;
    private readonly List<(string Text, double Size, Brush Brush, double Y)> lines = [];
    private string suffix = "";
    private double timeWidth;
    private double suffixFieldWidth;
    private double padding;
    private double mainSize;
    private Brush ink = Brushes.White;

    public void Update(Preferences preferences, DateTime time)
    {
        p = preferences; now = time;
        typeface = new Typeface(new FontFamily(p.Font), FontStyles.Normal, FontWeights.Light, FontStretches.Normal);
        mainSize = p.FontSize * p.Scale;
        ink = Brush(p.TextColor);
        var accent = p.UseAccent ? Brush(p.AccentColor) : ink;
        cell = "0123456789".Max(c => Measure(c.ToString(), mainSize));
        padding = (p.Background != "Transparent" ? 30 : 12) * p.Scale;
        lines.Clear(); suffix = "";
        double y = padding;
        var culture = CultureInfo.CurrentCulture;
        if (p.Advanced)
        {
            var text = TemplateFormatter.Format(p.Template, now, p);
            var parts = text.Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                double size = i == 0 ? mainSize : mainSize * .28;
                lines.Add((parts[i].TrimEnd('\r'), size, i == 0 ? ink : accent, y));
                y += size * 1.35;
            }
            // A fixed field, with additional room for longer custom lines.
            bodyWidth = Math.Max(mainSize * 5, lines.Max(l => StableWidth(l.Text, l.Size)));
        }
        else
        {
            string timeText = now.ToString(p.Hour24 ? "HH:mm" : "hh:mm", culture);
            suffix = (p.Seconds ? now.ToString(" ss", culture) : "") + (!p.Hour24 && p.AmPm ? now.ToString(" tt", culture) : "");
            timeWidth = StableWidth(timeText, mainSize);
            suffixFieldWidth = StableWidth((p.Seconds ? " 88" : "") + (!p.Hour24 && p.AmPm ? " " + Longest(culture.DateTimeFormat.AMDesignator, culture.DateTimeFormat.PMDesignator) : ""), mainSize * .28);
            bodyWidth = timeWidth + suffixFieldWidth;
            lines.Add((timeText, mainSize, ink, y));
            y += mainSize * 1.2;
            if (p.Preset == "Typography" && p.Weekday)
            {
                double size = mainSize * .43;
                bodyWidth = Math.Max(bodyWidth, culture.DateTimeFormat.DayNames.Max(d => StableWidth(d, size)));
                lines.Add((now.ToString("dddd", culture), size, accent, y));
                y += size * 1.4;
            }
            var dateParts = new List<string>();
            if (p.Weekday && p.Preset != "Typography") dateParts.Add(now.ToString(p.Preset == "Glass" ? "ddd" : "dddd", culture).ToUpper(culture));
            if (p.Date) dateParts.Add(now.ToString(p.Preset == "Glass" ? "d MMM" : "d MMMM yyyy", culture).ToUpper(culture));
            if (dateParts.Count > 0)
            {
                double size = mainSize * .205;
                var text = string.Join("  ·  ", dateParts);
                string longestDay = culture.DateTimeFormat.DayNames.MaxBy(d => StableWidth(d, size)) ?? "";
                string longestMonth = culture.DateTimeFormat.MonthNames.MaxBy(d => StableWidth(d, size)) ?? "";
                string widest = (p.Weekday && p.Preset != "Typography" ? longestDay + "  ·  " : "") + (p.Date ? "88 " + longestMonth + " 8888" : "");
                bodyWidth = Math.Max(bodyWidth, StableWidth(p.Preset == "Glass" ? text : widest, size));
                lines.Add((text, size, p.Preset == "Typography" ? ink : accent, y));
                y += size * 1.6;
            }
        }
        Width = Math.Ceiling(bodyWidth + padding * 2);
        Height = Math.Ceiling(y + padding);
        InvalidateVisual();
    }

    private string Longest(string a, string b) => StableWidth(a, mainSize * .28) > StableWidth(b, mainSize * .28) ? a : b;
    private static Brush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    private FormattedText Text(string s, double size, Brush brush) => new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private double Measure(string s, double size) => Text(s, size, ink).WidthIncludingTrailingWhitespace;

    private double StableWidth(string s, double size) => s.Sum(c => char.IsAsciiDigit(c) ? cell * size / mainSize : Measure(c.ToString(), size));
    private void DrawStable(DrawingContext dc, string text, double size, Brush brush, double x, double y)
    {
        foreach (char c in text)
        {
            var glyph = Text(c.ToString(), size, brush);
            double width = char.IsAsciiDigit(c) ? cell * size / mainSize : glyph.WidthIncludingTrailingWhitespace;
            dc.DrawText(glyph, new Point(x + (width - glyph.WidthIncludingTrailingWhitespace) / 2, y));
            x += width;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(0, 0, Width, Height);
        // Near-transparent hit area makes the transparent widget draggable throughout.
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), null, bounds);
        if (p.Background != "Transparent")
        {
            bool solid = p.Background == "Solid";
            byte alpha = solid ? (byte)255 : (byte)(255 * p.BackgroundOpacity);
            var top = solid ? Color.FromRgb(17, 28, 36) : Color.FromArgb(alpha, 39, 53, 58);
            var bottom = solid ? Color.FromRgb(17, 28, 36) : Color.FromArgb(alpha, 17, 27, 33);
            dc.DrawRoundedRectangle(new LinearGradientBrush(top, bottom, 90), new Pen(new SolidColorBrush(Color.FromArgb(44, 230, 245, 235)), 1), new Rect(.5, .5, Width - 1, Height - 1), 22 * p.Scale, 22 * p.Scale);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(32, 240, 255, 246)), 1), new Point(24 * p.Scale, 1.5), new Point(Width - 24 * p.Scale, 1.5));
        }
        foreach (var (text, size, brush, y) in lines)
        {
            bool first = y == padding;
            double width = StableWidth(text, size) + (first && !p.Advanced ? suffixFieldWidth : 0);
            double x = padding + (p.Alignment == "Center" ? (bodyWidth - width) / 2 : p.Alignment == "Right" ? bodyWidth - width : 0);
            DrawStable(dc, text, size, brush, x, y);
            if (first && !p.Advanced && suffix.Length > 0)
                DrawStable(dc, suffix, mainSize * .28, p.UseAccent ? Brush(p.AccentColor) : ink, x + timeWidth, y + mainSize * .61);
        }
    }
}
