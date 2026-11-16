using System.Globalization;
using System.Windows.Media.Effects;

namespace Still.App;

// Shared by the desktop widget, live preview, preset cards and the screenshot exporter.
// Three layers: the surface (card, hit area), the ink (text, frame, seconds indicator; glows)
// and an overlay (scanlines) clipped to the surface.
public sealed class ClockFace : FrameworkElement
{
    private enum Role { Caption, Time, WeekdayLine, DateLine, Template, TemplateSecondary }
    private readonly record struct Line(string Text, double Size, bool Accent, double Y, Role Role, string Trailing = "");

    private readonly DrawingVisual surface = new(), ink = new(), overlay = new();
    private readonly VisualCollection layers;
    private readonly List<Line> lines = [];
    private Preferences p = new();
    private DateTime now;
    private Typeface typeface = new("Segoe UI");
    private double cell, bodyWidth, padding, mainSize, suffixFieldWidth, indicatorY = -1;
    private string suffix = "", zone = "";
    // Glyph widths and reserved date widths depend only on font, DPI and size, so cache them per font.
    private readonly Dictionary<(char, double), double> widths = [];
    private readonly Dictionary<string, double> reserves = [];
    private string fontKey = "";

    public ClockFace() { layers = new VisualCollection(this) { surface, ink, overlay }; }
    protected override int VisualChildrenCount => layers.Count;
    protected override Visual GetVisualChild(int index) => layers[index];

    public void Update(Preferences preferences, DateTime time)
    {
        p = preferences; now = time;
        string key = $"{p.Font}|{p.FontWeight}|{VisualTreeHelper.GetDpi(this).PixelsPerDip}|{CultureInfo.CurrentCulture.Name}";
        if (key != fontKey)
        {
            fontKey = key; widths.Clear(); reserves.Clear();
            typeface = new Typeface(new FontFamily(p.Font), FontStyles.Normal, Weight(p.FontWeight), FontStretches.Normal);
        }
        mainSize = p.FontSize * p.Scale;
        cell = "0123456789".Max(c => { if (!widths.TryGetValue((c, mainSize), out double w)) widths[(c, mainSize)] = w = Glyph(c.ToString(), mainSize, Brushes.White).WidthIncludingTrailingWhitespace; return w; });
        if (widths.Count > 4000) widths.Clear();
        bool boxed = p.Background != "Transparent" || p.Frame != "None";
        padding = Math.Max((boxed ? 30 : 12) * p.Scale, p.Glow ? GlowRadius() * .7 : 0);
        lines.Clear(); suffix = ""; suffixFieldWidth = 0; indicatorY = -1;
        zone = p.ShowZone ? TemplateFormatter.ZoneTag(now) : "";
        var culture = CultureInfo.CurrentCulture;
        double y = padding;
        bodyWidth = 0;

        if (p.Caption.Length > 0 || zone.Length > 0)
        {
            double size = mainSize * .15;
            string caption = p.Caption.ToUpper(culture);
            lines.Add(new(caption, size, true, y, Role.Caption, zone));
            bodyWidth = StableWidth(caption, size) + StableWidth(zone, size) + (caption.Length > 0 && zone.Length > 0 ? size * 3 : 0);
            y += size * 2;
        }

        if (p.Advanced)
        {
            var parts = TemplateFormatter.Format(p.Template, now, p).Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                double size = i == 0 ? mainSize : mainSize * .28;
                lines.Add(new(parts[i].TrimEnd('\r'), size, i > 0, y, i == 0 ? Role.Template : Role.TemplateSecondary));
                y += size * 1.35;
                if (i == 0) y = AddIndicator(y - size * .15);
            }
            // A fixed field, with additional room for longer custom lines.
            bodyWidth = Math.Max(bodyWidth, Math.Max(mainSize * 5, lines.Max(l => StableWidth(l.Text, l.Size))));
        }
        else
        {
            string timeText = now.ToString(p.Hour24 ? "HH:mm" : "hh:mm", culture);
            suffix = (p.Seconds ? now.ToString(" ss", culture) : "") + (!p.Hour24 && p.AmPm ? now.ToString(" tt", culture) : "");
            string am = culture.DateTimeFormat.AMDesignator, pm = culture.DateTimeFormat.PMDesignator;
            suffixFieldWidth = StableWidth((p.Seconds ? " 88" : "") + (!p.Hour24 && p.AmPm ? " " + (StableWidth(am, mainSize * .28) > StableWidth(pm, mainSize * .28) ? am : pm) : ""), mainSize * .28);
            bodyWidth = Math.Max(bodyWidth, StableWidth(timeText, mainSize) + suffixFieldWidth);
            lines.Add(new(timeText, mainSize, false, y, Role.Time, suffix));
            y = AddIndicator(y + mainSize * 1.12);
            if (p.Weekday && p.WeekdayStyle == "Line")
            {
                double size = mainSize * .43;
                string text = now.ToString("dddd", culture);
                bodyWidth = Math.Max(bodyWidth, culture.DateTimeFormat.DayNames.Max(d => StableWidth(d, size)));
                lines.Add(new(text, size, true, y, Role.WeekdayLine));
                y += size * 1.4;
            }
            var (weekday, date) = TemplateFormatter.DateParts(now, p, culture);
            if (weekday.Length > 0 || date.Length > 0)
            {
                double size = mainSize * .205;
                string text = string.Join("  ·  ", new[] { weekday, date }.Where(s => s.Length > 0));
                // Reserve the widest possible date so the widget does not resize day to day.
                string reserveKey = $"{size}|{p.Weekday}|{p.WeekdayStyle}|{p.Date}|{p.DateStyle}|{p.Uppercase}|{p.LetterSpacing}";
                if (!reserves.TryGetValue(reserveKey, out double reserve))
                {
                    // Every month, each weekday, and two-digit days.
                    reserves[reserveKey] = reserve = Enumerable.Range(1, 12).SelectMany(m => Enumerable.Range(22, 7).Select(d => new DateTime(2026, m, d)))
                        .Max(d => { var (w, dt) = TemplateFormatter.DateParts(d, p, culture); return StableWidth(string.Join("  ·  ", new[] { w, dt }.Where(s => s.Length > 0)), size); });
                }
                bodyWidth = Math.Max(bodyWidth, reserve);
                lines.Add(new(text, size, p.WeekdayStyle != "Line", y, Role.DateLine));
                y += size * 1.6;
            }
        }
        Width = Math.Ceiling(bodyWidth + padding * 2);
        Height = Math.Ceiling(y + padding);
        Opacity = p.Opacity;
        Render();
    }

    private double AddIndicator(double y)
    {
        if (p.SecondsIndicator == "None") return y;
        indicatorY = y;
        return y + 14 * p.Scale;
    }

    private double GlowRadius() => (6 + 34 * p.GlowStrength) * p.Scale;
    private static FontWeight Weight(string name) => name switch
    {
        "Thin" => FontWeights.Thin, "Regular" => FontWeights.Normal, "SemiBold" => FontWeights.SemiBold,
        "Bold" => FontWeights.Bold, _ => FontWeights.Light,
    };
    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Color Alpha(Color c, double a) => Color.FromArgb((byte)Math.Clamp(a * 255, 0, 255), c.R, c.G, c.B);
    private static Color Mix(Color a, Color b, double t) => Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    private static Brush Frozen(Brush b) { b.Freeze(); return b; }
    private FormattedText Glyph(string s, double size, Brush brush) => new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    // Small uppercase lines get extra tracking for an instrument-panel feel.
    private double Tracking(double size) => p.LetterSpacing * size / 80 + (size < mainSize * .5 && p.Uppercase ? size * .12 : 0);
    private double Advance(char c, double size)
    {
        if (char.IsAsciiDigit(c)) return cell * size / mainSize + Tracking(size);
        if (!widths.TryGetValue((c, size), out double w))
            widths[(c, size)] = w = Glyph(c.ToString(), size, Brushes.White).WidthIncludingTrailingWhitespace;
        return w + Tracking(size);
    }
    private double StableWidth(string s, double size) => s.Length == 0 ? 0 : s.Sum(c => Advance(c, size)) - Tracking(size);

    private void DrawStable(DrawingContext dc, string text, double size, Brush brush, double x, double y, Brush? dimColon = null)
    {
        foreach (char c in text)
        {
            var glyph = Glyph(c.ToString(), size, c == ':' && dimColon != null ? dimColon : brush);
            double advance = Advance(c, size);
            dc.DrawText(glyph, new Point(x + (advance - Tracking(size) - glyph.WidthIncludingTrailingWhitespace) / 2, y));
            x += advance;
        }
    }

    private double LineX(double width) => padding + (p.Alignment == "Center" ? (bodyWidth - width) / 2 : p.Alignment == "Right" ? bodyWidth - width : 0);

    private void Render()
    {
        var bounds = new Rect(0, 0, Width, Height);
        var text = Hex(p.TextColor);
        var accent = p.UseAccent ? Hex(p.AccentColor) : text;
        Brush inkBrush = p.Gradient
            ? Frozen(new LinearGradientBrush(new GradientStopCollection { new(text, 0), new(Mix(text, accent, .5), .55), new(accent, 1) }) { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(padding, 0), EndPoint = new Point(padding + bodyWidth, mainSize) })
            : Frozen(new SolidColorBrush(text));
        Brush accentBrush = Frozen(new SolidColorBrush(accent));
        double radius = Math.Min(p.CornerRadius * p.Scale, Math.Min(Width, Height) / 2);
        var card = new Rect(.5, .5, Math.Max(0, Width - 1), Math.Max(0, Height - 1));

        using (var dc = surface.RenderOpen())
        {
            // Near-transparent hit area makes the transparent widget draggable throughout.
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), null, bounds);
            if (p.Background != "Transparent")
            {
                var baseColor = Hex(p.SurfaceColor);
                double alpha = p.BackgroundOpacity;
                Brush fill = p.Background == "Solid"
                    ? new SolidColorBrush(Alpha(baseColor, alpha))
                    : new LinearGradientBrush(Alpha(Mix(baseColor, Colors.White, .1), alpha), Alpha(Mix(baseColor, Colors.Black, .15), alpha), 90);
                var edge = p.Frame == "None" ? new Pen(new SolidColorBrush(Color.FromArgb(44, 230, 245, 235)), 1) : null;
                dc.DrawRoundedRectangle(fill, edge, card, radius, radius);
                if (p.Background == "Card")
                    dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(32, 240, 255, 246)), 1), new Point(Math.Max(radius, 8), 1.5), new Point(Width - Math.Max(radius, 8), 1.5));
            }
        }

        using (var dc = ink.RenderOpen())
        {
            DrawFrame(dc, accent, card, radius);
            Brush? dim = p.BlinkSeparator && now.Second % 2 == 1 ? Frozen(new SolidColorBrush(Alpha(text, .22))) : null;
            foreach (var line in lines)
            {
                var brush = line.Accent ? accentBrush : inkBrush;
                switch (line.Role)
                {
                    case Role.Caption:
                        double cx = padding;
                        if (line.Text.Length > 0) DrawStable(dc, line.Text, line.Size, brush, cx, line.Y);
                        if (line.Trailing.Length > 0) DrawStable(dc, line.Trailing, line.Size, Frozen(new SolidColorBrush(Alpha(accent, .7))), padding + bodyWidth - StableWidth(line.Trailing, line.Size), line.Y);
                        break;
                    case Role.Time:
                        double x = LineX(StableWidth(line.Text, line.Size) + suffixFieldWidth);
                        DrawStable(dc, line.Text, line.Size, brush, x, line.Y, dim);
                        if (line.Trailing.Length > 0)
                            DrawStable(dc, line.Trailing, mainSize * .28, accentBrush, x + StableWidth(line.Text, line.Size), line.Y + mainSize * .61);
                        break;
                    case Role.Template:
                        DrawStable(dc, line.Text, line.Size, brush, LineX(StableWidth(line.Text, line.Size)), line.Y, dim);
                        break;
                    default:
                        DrawStable(dc, line.Text, line.Size, brush, LineX(StableWidth(line.Text, line.Size)), line.Y);
                        break;
                }
            }
            if (indicatorY >= 0) DrawIndicator(dc, accent);
        }
        ink.Effect = p.Glow ? new DropShadowEffect { Color = accent, ShadowDepth = 0, BlurRadius = GlowRadius(), Opacity = .45 + .55 * p.GlowStrength, RenderingBias = RenderingBias.Quality } : null;

        using (var dc = overlay.RenderOpen())
        {
            if (p.Scanlines && p.Background != "Transparent")
            {
                dc.PushClip(new RectangleGeometry(card, radius, radius));
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), Math.Max(1, p.Scale));
                pen.Freeze();
                for (double sy = 1; sy < Height; sy += 3 * Math.Max(1, p.Scale)) dc.DrawLine(pen, new Point(0, sy), new Point(Width, sy));
                dc.Pop();
            }
        }
        InvalidateMeasure();
    }

    private void DrawFrame(DrawingContext dc, Color accent, Rect card, double radius)
    {
        if (p.Frame == "Outline")
        {
            var pen = new Pen(new SolidColorBrush(Alpha(accent, .75)), 1.2 * p.Scale);
            var inset = Rect.Inflate(card, -.6 * p.Scale, -.6 * p.Scale);
            dc.DrawRoundedRectangle(null, pen, inset, radius, radius);
        }
        else if (p.Frame == "Brackets")
        {
            double len = Math.Min(22 * p.Scale, Math.Min(Width, Height) / 3), t = 1.6 * p.Scale, i = t / 2 + .5;
            var pen = new Pen(new SolidColorBrush(accent), t) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
            foreach (var (cx, cy, dx, dy) in new[] { (i, i, 1, 1), (Width - i, i, -1, 1), (i, Height - i, 1, -1), (Width - i, Height - i, -1, -1) })
            {
                dc.DrawLine(pen, new Point(cx, cy), new Point(cx + dx * len, cy));
                dc.DrawLine(pen, new Point(cx, cy), new Point(cx, cy + dy * len));
            }
            // Fine tick marks along the top and bottom edges.
            var tick = new Pen(new SolidColorBrush(Alpha(accent, .4)), Math.Max(.75, .8 * p.Scale));
            for (int k = 1; k < 8; k++)
            {
                double tx = len * 1.6 + (Width - len * 3.2) * k / 8;
                dc.DrawLine(tick, new Point(tx, i), new Point(tx, i + 4 * p.Scale));
                dc.DrawLine(tick, new Point(tx, Height - i), new Point(tx, Height - i - 4 * p.Scale));
            }
        }
    }

    private void DrawIndicator(DrawingContext dc, Color accent)
    {
        double x = padding, w = bodyWidth, y = indicatorY, s = p.Scale;
        double progress = (now.Second + 1) / 60.0;
        var lit = new SolidColorBrush(accent); var dark = new SolidColorBrush(Alpha(accent, .18));
        switch (p.SecondsIndicator)
        {
            case "Bar":
                double h = 2.5 * s;
                dc.DrawRoundedRectangle(dark, null, new Rect(x, y, w, h), h / 2, h / 2);
                dc.DrawRoundedRectangle(lit, null, new Rect(x, y, w * progress, h), h / 2, h / 2);
                dc.DrawEllipse(lit, null, new Point(x + w * progress, y + h / 2), 3.2 * s, 3.2 * s);
                break;
            case "Segments":
                const int count = 30;
                double gap = 3 * s, seg = (w - gap * (count - 1)) / count;
                for (int k = 0; k < count; k++)
                    dc.DrawRectangle(k < Math.Ceiling(progress * count) ? lit : dark, null, new Rect(x + k * (seg + gap), y, Math.Max(1, seg), 5 * s));
                break;
            case "Dots":
                for (int k = 0; k < 60; k++)
                {
                    double cx = x + w * (k + .5) / 60;
                    bool current = k == now.Second;
                    dc.DrawEllipse(k <= now.Second ? lit : dark, null, new Point(cx, y + 3 * s), (current ? 2.6 : 1.3) * s, (current ? 2.6 : 1.3) * s);
                }
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Width, Height);
}
