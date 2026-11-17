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

        poster = p.Layout == "Poster" && !p.Advanced;
        scriptLayout = p.Layout == "Script" && !p.Advanced;
        if (poster)
        {
            y = LayoutPoster(y, culture);
        }
        else if (scriptLayout)
        {
            y = LayoutScript(y, culture);
        }
        else if (p.Advanced)
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
    private double Advance(char c, double size, double extra = 0)
    {
        if (char.IsAsciiDigit(c)) return cell * size / mainSize + Tracking(size) + extra;
        if (!widths.TryGetValue((c, size), out double w))
            widths[(c, size)] = w = Glyph(c.ToString(), size, Brushes.White).WidthIncludingTrailingWhitespace;
        return w + Tracking(size) + extra;
    }
    private double StableWidth(string s, double size, double extra = 0) => s.Length == 0 ? 0 : s.Sum(c => Advance(c, size, extra)) - Tracking(size) - extra;

    private void DrawStable(DrawingContext dc, string text, double size, Brush brush, double x, double y, Brush? dimColon = null, double extra = 0)
    {
        foreach (char c in text)
        {
            var glyph = Glyph(c.ToString(), size, c == ':' && dimColon != null ? dimColon : brush);
            double advance = Advance(c, size, extra);
            dc.DrawText(glyph, new Point(x + (advance - Tracking(size) - extra - glyph.WidthIncludingTrailingWhitespace) / 2, y));
            x += advance;
        }
    }

    // Ink (not line-box) top offset and height of a sample glyph, so poster parts align to visible edges.
    private (double Top, double Height) Ink(string sample, double size)
    {
        string k = $"ink|{sample}|{size}";
        if (!reserves.TryGetValue(k, out double top))
        {
            var b = Glyph(sample, size, Brushes.White).BuildGeometry(new Point(0, 0)).Bounds;
            reserves[k] = top = b.IsEmpty ? 0 : b.Top;
            reserves[k + "|h"] = b.IsEmpty ? size * .7 : b.Height;
        }
        return (top, reserves[k + "|h"]);
    }

    // ---- Poster layout ----
    private bool poster;
    private string pDay = "", pMonth = "", pTime = "", pWeekday = "";
    private double pY, pNumSize, pNumTop, pNumH, pNumX, pNumExtra, pMonthSize, pMonthX, pMonthY, pTimeSize, pTimeY, pLineY, pLineW, pWkSize, pWkExtra, pWkX, pWkBottom;

    // ---- Script layout: tall condensed day number, vertical month, handwritten weekday, bold time ----
    private bool scriptLayout;
    private Typeface scriptFace = new("Segoe Script"), boldFace = new("Segoe UI");
    private FormattedText? sWeekday, sTime;
    private string sDay = "", sMonth = "";
    private double sY, sNumSize, sNumTop, sNumH, sNumX, sNumW, sNumExtra, sMonthSize, sMonthExtra, sMonthX, sMonthBottom,
        sScriptX, sScriptBase, sTimeCenter, sTimeY, sTimeW, sIndicatorY;

    private FormattedText Text(string s, double size, Brush brush, Typeface face) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double LayoutScript(double y, CultureInfo culture)
    {
        double s = p.Scale, cond = p.Condense;
        scriptFace = new Typeface(new FontFamily(p.ScriptFont), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        boldFace = new Typeface(new FontFamily(p.Font), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        sY = y;
        sNumSize = mainSize * 2.7; sNumExtra = -sNumSize * .02;
        (sNumTop, sNumH) = Ink("8", sNumSize);
        sDay = now.ToString("dd", CultureInfo.InvariantCulture);
        sNumW = StableWidth("88", sNumSize, sNumExtra) * cond;

        // Month runs up the left edge, its top level with the top of the number.
        sMonthSize = mainSize * .21; sMonthExtra = sMonthSize * .1;
        sMonth = p.Date ? Case(now.ToString("MMMM", culture), culture) : "";
        double monthLength = StableWidth(sMonth, sMonthSize, sMonthExtra) * cond;
        double monthReserve = p.Date ? culture.DateTimeFormat.MonthNames.Where(m => m.Length > 0).Max(m => StableWidth(Case(m, culture), sMonthSize, sMonthExtra)) * cond : 0;
        double column = p.Date ? Glyph("M", sMonthSize, Brushes.White).Height + mainSize * .1 : 0;
        sMonthX = padding;
        sMonthBottom = y + monthLength;
        sNumX = padding + column;

        // Handwritten weekday sweeps across the lower part of the number.
        double scriptSize = mainSize * .64;
        sWeekday = p.Weekday ? Text(now.ToString("dddd", culture), scriptSize, Brushes.White, scriptFace) : null;
        double scriptReserve = p.Weekday ? culture.DateTimeFormat.DayNames.Max(d => Text(d, scriptSize, Brushes.White, scriptFace).WidthIncludingTrailingWhitespace) : 0;
        // Starts just inside the number and sweeps out to the right, like a signature.
        sScriptX = sNumX + sNumW * .12;
        sScriptBase = y + sNumH * .9;
        double scriptBottom = p.Weekday ? sScriptBase + scriptSize * .3 : y + sNumH;

        // Bold time centred beneath the number.
        double timeSize = mainSize * .42;
        string time = TemplateFormatter.CompactTime(now, p, culture).Replace(" ", "");
        sTime = Text(time, timeSize, Brushes.White, boldFace);
        sTimeW = Text(TemplateFormatter.CompactTime(new DateTime(2026, 1, 1, 20, 58, 58), p, culture).Replace(" ", "").Replace('2', '8'), timeSize, Brushes.White, boldFace).WidthIncludingTrailingWhitespace;
        var timeInk = Text("8", timeSize, Brushes.White, boldFace).BuildGeometry(new Point(0, 0)).Bounds;
        double timeCap = Math.Max(y + sNumH, scriptBottom) + mainSize * .16;
        sTimeY = timeCap - timeInk.Top;
        double timeBottom = timeCap + timeInk.Height;
        sTimeCenter = sNumX + sNumW / 2;
        sIndicatorY = timeBottom + mainSize * .16;

        // Keep everything inside the padding even with a long weekday or time.
        double left = Math.Min(Math.Min(sScriptX, sTimeCenter - sTimeW / 2), sMonthX);
        if (left < padding) { double shift = padding - left; sMonthX += shift; sNumX += shift; sScriptX += shift; sTimeCenter += shift; }
        double right = Math.Max(Math.Max(sNumX + sNumW, sScriptX + scriptReserve), sTimeCenter + sTimeW / 2);
        bodyWidth = Math.Max(bodyWidth, right - padding);
        double bottom = p.SecondsIndicator != "None" ? sIndicatorY + 8 * s : timeBottom;
        return Math.Max(bottom, y + monthReserve);
    }

    private void RenderScript(DrawingContext dc, Color text, Color accent, Brush inkBrush)
    {
        double cond = p.Condense;
        Brush numBrush = p.Gradient
            ? Frozen(new LinearGradientBrush(text, accent, 90) { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, sY), EndPoint = new Point(0, sY + sNumH) })
            : Frozen(new SolidColorBrush(text));
        if (p.Fade)
            dc.PushOpacityMask(new LinearGradientBrush(new GradientStopCollection { new(Colors.Black, 0), new(Color.FromArgb(225, 0, 0, 0), .45), new(Color.FromArgb(28, 0, 0, 0), 1) }, 90)
            { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, sY), EndPoint = new Point(0, sY + sNumH) });
        dc.PushTransform(new ScaleTransform(cond, 1, sNumX, 0));
        DrawStable(dc, sDay, sNumSize, numBrush, sNumX, sY - sNumTop, extra: sNumExtra);
        dc.Pop();
        if (p.Fade) dc.Pop();

        if (sMonth.Length > 0)
        {
            dc.PushTransform(new RotateTransform(-90, sMonthX, sMonthBottom));
            dc.PushTransform(new ScaleTransform(cond, 1, sMonthX, sMonthBottom));
            DrawStable(dc, sMonth, sMonthSize, inkBrush, sMonthX, sMonthBottom, extra: sMonthExtra);
            dc.Pop(); dc.Pop();
        }
        if (sWeekday != null)
        {
            sWeekday.SetForegroundBrush(Frozen(new SolidColorBrush(accent)));
            dc.PushTransform(new RotateTransform(-8, sScriptX, sScriptBase));
            dc.DrawText(sWeekday, new Point(sScriptX, sScriptBase - sWeekday.Baseline));
            dc.Pop();
        }
        if (sTime != null)
        {
            sTime.SetForegroundBrush(Frozen(new SolidColorBrush(text)));
            dc.DrawText(sTime, new Point(sTimeCenter - sTime.WidthIncludingTrailingWhitespace / 2, sTimeY));
        }
        if (p.SecondsIndicator != "None") DrawIndicator(dc, accent, sTimeCenter - sTimeW / 2, sTimeW, sIndicatorY);
    }

    private double LayoutPoster(double y, CultureInfo culture)
    {
        double s = p.Scale;
        pY = y;
        pNumSize = mainSize * 3.2; pNumExtra = -pNumSize * .03;
        (pNumTop, pNumH) = Ink("8", pNumSize);
        pDay = now.Day.ToString(CultureInfo.InvariantCulture);
        double numW = StableWidth("88", pNumSize, pNumExtra);

        pWkSize = mainSize * .16; pWkExtra = pWkSize * .85;
        pWeekday = p.Weekday ? Case(now.ToString("dddd", culture), culture) : "";
        double wkLength = p.Weekday ? culture.DateTimeFormat.DayNames.Max(d => StableWidth(Case(d, culture), pWkSize, pWkExtra)) : 0;
        double wkColumn = p.Weekday ? Glyph("D", pWkSize, Brushes.White).Height + mainSize * .22 : 0;
        pWkX = padding;
        // Digits sit centred in a fixed cell, so a narrow "1" leaves room: tuck the number towards the weekday.
        pNumX = padding + wkColumn - (p.Weekday ? numW * .06 : 0);

        pMonthSize = mainSize * .72;
        pMonth = Case(now.ToString("MMMM", culture), culture);
        double monthW = culture.DateTimeFormat.MonthNames.Where(m => m.Length > 0).Max(m => StableWidth(Case(m, culture), pMonthSize));
        var (monthTop, monthH) = Ink("M", pMonthSize);
        pMonthX = pNumX + numW * .24;
        double monthCap = y + pNumH * .4;
        pMonthY = monthCap - monthTop;

        pTimeSize = mainSize * .21;
        pTime = TemplateFormatter.CompactTime(now, p, culture);
        var (timeTop, timeH) = Ink("8", pTimeSize);
        double timeCap = monthCap + monthH + mainSize * .3;
        pTimeY = timeCap - timeTop;
        double timeW = StableWidth(TemplateFormatter.CompactTime(new DateTime(2026, 1, 1, 20, 58, 58), p, culture), pTimeSize);
        pLineY = timeCap + timeH + mainSize * .2;
        pLineW = Math.Max(mainSize * 1.1, timeW * .9);
        if (p.SecondsIndicator != "None") pLineW = Math.Max(pLineW, monthW * .6);

        pWkBottom = y + Math.Max(pNumH, wkLength);
        bodyWidth = Math.Max(bodyWidth + padding, pNumX + numW);
        bodyWidth = Math.Max(bodyWidth, pMonthX + Math.Max(monthW, Math.Max(timeW, pLineW))) - padding;
        return Math.Max(Math.Max(y + pNumH, pWkBottom), pLineY + 8 * s);
    }

    private string Case(string text, CultureInfo culture) => p.Uppercase ? text.ToUpper(culture) : text;

    private void RenderPoster(DrawingContext dc, Color text, Color accent, Brush inkBrush, Brush? dim)
    {
        double s = p.Scale;
        // Big day number: accent colour (or a text-to-accent sweep), optionally fading towards its base.
        Brush numBrush = p.Gradient
            ? Frozen(new LinearGradientBrush(accent, text, 0) { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(pNumX, 0), EndPoint = new Point(pNumX + StableWidth("88", pNumSize, pNumExtra), 0) })
            : Frozen(new SolidColorBrush(accent));
        if (p.Fade)
            dc.PushOpacityMask(new LinearGradientBrush(new GradientStopCollection { new(Colors.Black, 0), new(Color.FromArgb(225, 0, 0, 0), .45), new(Color.FromArgb(28, 0, 0, 0), 1) }, 90)
            { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, pY), EndPoint = new Point(0, pY + pNumH) });
        DrawStable(dc, pDay, pNumSize, numBrush, pNumX, pY - pNumTop, extra: pNumExtra);
        if (p.Fade) dc.Pop();

        // Weekday reads bottom-to-top beside the number.
        if (pWeekday.Length > 0)
        {
            dc.PushTransform(new RotateTransform(-90, pWkX, pWkBottom));
            DrawStable(dc, pWeekday, pWkSize, Frozen(new SolidColorBrush(Alpha(text, .9))), pWkX, pWkBottom, extra: pWkExtra);
            dc.Pop();
        }
        DrawStable(dc, pMonth, pMonthSize, inkBrush, pMonthX, pMonthY);
        DrawStable(dc, pTime, pTimeSize, Frozen(new SolidColorBrush(text)), pMonthX, pTimeY, dim);
        if (p.SecondsIndicator == "None")
            dc.DrawRectangle(Frozen(new SolidColorBrush(Alpha(text, .85))), null, new Rect(pMonthX, pLineY, pLineW, Math.Max(1, 1.6 * s)));
        else
            DrawIndicator(dc, accent, pMonthX, pLineW, pLineY);
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
            if (indicatorY >= 0) DrawIndicator(dc, accent, padding, bodyWidth, indicatorY);
            if (poster) RenderPoster(dc, text, accent, inkBrush, dim);
            if (scriptLayout) RenderScript(dc, text, accent, inkBrush);
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

    private void DrawIndicator(DrawingContext dc, Color accent, double x, double w, double y)
    {
        double s = p.Scale;
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
