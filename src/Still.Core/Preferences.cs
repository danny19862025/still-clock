using System.Text.Json;
using System.Text.RegularExpressions;

namespace Still.Core;

public sealed class Preferences
{
    public static readonly string[] Presets = ["Neon", "HUD", "Holo", "Terminal", "Minimal", "Typography", "Glass"];
    public static readonly string[] Weights = ["Thin", "Light", "Regular", "SemiBold", "Bold"];
    public static readonly string[] Frames = ["None", "Brackets", "Outline"];
    public static readonly string[] Indicators = ["None", "Bar", "Segments", "Dots"];
    public static readonly string[] DateStyles = ["Long", "Short", "ISO", "Numeric"];
    public static readonly string[] WeekdayStyles = ["Full", "Short", "Line"];

    /// <summary>Text/accent colour pairs offered as one-click palettes.</summary>
    public static readonly (string Name, string Text, string Accent)[] Palettes =
    [
        ("Cyber", "#E9FDFF", "#00E5FF"), ("Synth", "#FFE6FB", "#FF2BD6"), ("Matrix", "#B8FFC9", "#00FF66"),
        ("Solar", "#FFF1D6", "#FFB000"), ("Plasma", "#EDE4FF", "#9D6BFF"), ("Ice", "#FFFFFF", "#8FD8FF"),
        ("Ember", "#FFE3DC", "#FF4D3D"), ("Classic", "#F5F3ED", "#B6CDBD"),
    ];

    public int Version { get; set; } = 2;
    public string Preset { get; set; } = "Minimal";
    public string Font { get; set; } = "Segoe UI Variable Display";
    public string FontWeight { get; set; } = "Light";
    public double FontSize { get; set; } = 80;
    public double LetterSpacing { get; set; }
    public string TextColor { get; set; } = "#F5F3ED";
    public string AccentColor { get; set; } = "#B6CDBD";
    public bool UseAccent { get; set; } = true;
    public string Background { get; set; } = "Transparent";
    public string SurfaceColor { get; set; } = "#111C24";
    public double BackgroundOpacity { get; set; } = .64;
    public double CornerRadius { get; set; } = 22;
    public double Opacity { get; set; } = 1;
    public bool Hour24 { get; set; } = true;
    public bool Seconds { get; set; }
    public bool AmPm { get; set; } = true;
    public bool Weekday { get; set; } = true;
    public string WeekdayStyle { get; set; } = "Full";
    public bool Date { get; set; } = true;
    public string DateStyle { get; set; } = "Long";
    public bool Uppercase { get; set; } = true;
    public string Alignment { get; set; } = "Left";
    public double Scale { get; set; } = 1;

    // Futuristic effects. All default off so existing settings files keep their look.
    public bool Glow { get; set; }
    public double GlowStrength { get; set; } = .6;
    public bool Gradient { get; set; }
    public bool Scanlines { get; set; }
    public string Frame { get; set; } = "None";
    public string SecondsIndicator { get; set; } = "None";
    public bool BlinkSeparator { get; set; }
    public bool ShowZone { get; set; }
    public string Caption { get; set; } = "";

    public bool Locked { get; set; }
    public bool ClickThrough { get; set; }
    public bool Topmost { get; set; }
    public bool LaunchAtLogin { get; set; }
    public bool Advanced { get; set; }
    public string Template { get; set; } = "{time}\n{weekday} · {date}";
    public SavedPosition? Position { get; set; }

    /// <summary>Fresh-install appearance.</summary>
    public static Preferences CreateDefault()
    {
        var p = new Preferences();
        p.ApplyPreset("Neon");
        return p;
    }

    public void ApplyPreset(string preset)
    {
        Preset = preset;
        // Shared baseline: each preset below only states what differs.
        Font = "Segoe UI Variable Display"; FontWeight = "Light"; FontSize = 80; LetterSpacing = 0;
        TextColor = "#F5F3ED"; AccentColor = "#A0B1BD"; UseAccent = true;
        Background = "Transparent"; SurfaceColor = "#111C24"; BackgroundOpacity = .64; CornerRadius = 22; Opacity = 1;
        Alignment = "Center"; Scale = 1; Weekday = Date = true; WeekdayStyle = "Full"; DateStyle = "Long"; Uppercase = true;
        Seconds = false; Advanced = false;
        Glow = Gradient = Scanlines = BlinkSeparator = ShowZone = false; GlowStrength = .6;
        Frame = "None"; SecondsIndicator = "None"; Caption = "";
        switch (preset)
        {
            case "Typography":
                FontSize = 96; Background = "Solid"; BackgroundOpacity = 1; WeekdayStyle = "Line";
                break;
            case "Glass":
                Background = "Card"; TextColor = "#FF8080"; WeekdayStyle = "Short"; DateStyle = "Short";
                break;
            case "Neon":
                TextColor = "#E9FDFF"; AccentColor = "#00E5FF"; FontWeight = "Thin"; LetterSpacing = 2;
                Glow = true; GlowStrength = .75; Seconds = true; SecondsIndicator = "Bar"; BlinkSeparator = true;
                WeekdayStyle = "Short"; DateStyle = "ISO";
                break;
            case "HUD":
                Font = "Bahnschrift"; TextColor = "#E9FDFF"; AccentColor = "#00E5FF";
                Background = "Card"; SurfaceColor = "#04121A"; BackgroundOpacity = .55; CornerRadius = 4;
                Frame = "Brackets"; Scanlines = true; Glow = true; GlowStrength = .45; Seconds = true;
                SecondsIndicator = "Segments"; ShowZone = true; Caption = "SYS.CHRONO"; WeekdayStyle = "Short"; DateStyle = "ISO";
                Alignment = "Left";
                break;
            case "Holo":
                TextColor = "#FF2BD6"; AccentColor = "#00E5FF"; FontWeight = "Regular"; Gradient = true; Glow = true;
                GlowStrength = .55; Background = "Card"; SurfaceColor = "#120A24"; BackgroundOpacity = .45; CornerRadius = 28;
                Frame = "Outline"; SecondsIndicator = "Dots"; WeekdayStyle = "Short"; DateStyle = "Short";
                break;
            case "Terminal":
                Font = "Cascadia Mono"; FontWeight = "Regular"; FontSize = 72; TextColor = "#B8FFC9"; AccentColor = "#00FF66";
                Background = "Solid"; SurfaceColor = "#020A04"; BackgroundOpacity = .9; CornerRadius = 6; Scanlines = true;
                Glow = true; GlowStrength = .4; Seconds = true; BlinkSeparator = true; Caption = "> now";
                WeekdayStyle = "Short"; DateStyle = "ISO"; Alignment = "Left";
                break;
        }
    }

    public void Normalize()
    {
        Version = 2;
        if (!Presets.Contains(Preset)) Preset = "Minimal";
        if (string.IsNullOrWhiteSpace(Font) || Font.Length > 150) Font = "Segoe UI Variable Display";
        if (!Weights.Contains(FontWeight)) FontWeight = "Light";
        FontSize = Limit(FontSize, 32, 160, 80);
        LetterSpacing = Limit(LetterSpacing, -4, 24, 0);
        Scale = Limit(Scale, .5, 2, 1);
        BackgroundOpacity = Limit(BackgroundOpacity, .1, 1, .64);
        CornerRadius = Limit(CornerRadius, 0, 48, 22);
        Opacity = Limit(Opacity, .2, 1, 1);
        GlowStrength = Limit(GlowStrength, .1, 1, .6);
        if (!IsColor(TextColor)) TextColor = "#F5F3ED";
        if (!IsColor(AccentColor)) AccentColor = "#A0B1BD";
        if (!IsColor(SurfaceColor)) SurfaceColor = "#111C24";
        if (Background is not ("Transparent" or "Card" or "Solid")) Background = "Transparent";
        if (Alignment is not ("Left" or "Center" or "Right")) Alignment = "Left";
        if (!Frames.Contains(Frame)) Frame = "None";
        if (!Indicators.Contains(SecondsIndicator)) SecondsIndicator = "None";
        if (!DateStyles.Contains(DateStyle)) DateStyle = "Long";
        if (!WeekdayStyles.Contains(WeekdayStyle)) WeekdayStyle = "Full";
        Caption = (Caption ?? "").Replace("\r", "").Replace("\n", " ");
        if (Caption.Length > 40) Caption = Caption[..40];
        if (TemplateFormatter.Validate(Template) != null) Template = "{time}\n{weekday} · {date}";
        if (Position is { } p && (!double.IsFinite(p.OffsetX) || !double.IsFinite(p.OffsetY))) Position = null;
        if (!Locked) ClickThrough = false;
    }

    // ---- Theme sharing: appearance only, never position or desktop behaviour. ----
    private const string ThemePrefix = "still-theme:";

    public string ExportTheme()
    {
        var copy = (Preferences)MemberwiseClone();
        copy.Position = null; copy.Locked = copy.ClickThrough = copy.Topmost = copy.LaunchAtLogin = false;
        return ThemePrefix + JsonSerializer.Serialize(copy);
    }

    /// <summary>Applies a theme exported by <see cref="ExportTheme"/>. Returns an error message, or null on success.</summary>
    public string? ImportTheme(string? text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith(ThemePrefix, StringComparison.Ordinal)) return "The clipboard does not contain a Still theme.";
        if (text.Length > 16 * 1024) return "That theme is too large.";
        Preferences? theme;
        try { theme = JsonSerializer.Deserialize<Preferences>(text[ThemePrefix.Length..]); }
        catch (JsonException) { return "That theme could not be read."; }
        if (theme == null) return "That theme could not be read.";
        // Desktop behaviour and placement always stay with this computer.
        theme.Position = Position; theme.Locked = Locked; theme.ClickThrough = ClickThrough;
        theme.Topmost = Topmost; theme.LaunchAtLogin = LaunchAtLogin;
        theme.Normalize();
        foreach (var property in typeof(Preferences).GetProperties())
            if (property.CanWrite) property.SetValue(this, property.GetValue(theme));
        return null;
    }

    public static bool IsColor(string? value) => value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
    private static double Limit(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
