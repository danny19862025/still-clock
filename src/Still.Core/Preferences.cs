using System.Text.RegularExpressions;

namespace Still.Core;

public sealed class Preferences
{
    public int Version { get; set; } = 1;
    public string Preset { get; set; } = "Minimal";
    public string Font { get; set; } = "Segoe UI Variable Display";
    public double FontSize { get; set; } = 80;
    public string TextColor { get; set; } = "#F5F3ED";
    public string AccentColor { get; set; } = "#B6CDBD";
    public bool UseAccent { get; set; } = true;
    public string Background { get; set; } = "Transparent";
    public double BackgroundOpacity { get; set; } = .64;
    public bool Hour24 { get; set; } = true;
    public bool Seconds { get; set; }
    public bool AmPm { get; set; } = true;
    public bool Weekday { get; set; } = true;
    public bool Date { get; set; } = true;
    public string Alignment { get; set; } = "Left";
    public double Scale { get; set; } = 1;
    public bool Locked { get; set; }
    public bool ClickThrough { get; set; }
    public bool Topmost { get; set; }
    public bool LaunchAtLogin { get; set; }
    public bool Advanced { get; set; }
    public string Template { get; set; } = "{time}\n{weekday} · {date}";
    public SavedPosition? Position { get; set; }

    public void ApplyPreset(string preset)
    {
        Preset = preset;
        Font = "Segoe UI Variable Display";
        FontSize = preset == "Typography" ? 96 : 80;
        TextColor = "#F5F3ED"; AccentColor = "#A0B1BD"; UseAccent = true;
        Background = preset switch { "Glass" => "Card", "Typography" => "Solid", _ => "Transparent" };
        BackgroundOpacity = preset == "Typography" ? 1 : .64; Alignment = "Center"; Scale = 1;
        if (preset == "Glass") TextColor = "#FF8080";
        Weekday = Date = true; Seconds = false; Advanced = false;
    }

    public void Normalize()
    {
        Version = 1;
        if (Preset is not ("Minimal" or "Typography" or "Glass")) Preset = "Minimal";
        if (string.IsNullOrWhiteSpace(Font) || Font.Length > 150) Font = "Segoe UI Variable Display";
        FontSize = Limit(FontSize, 32, 160, 80);
        Scale = Limit(Scale, .5, 2, 1);
        BackgroundOpacity = Limit(BackgroundOpacity, .1, 1, .64);
        if (!IsColor(TextColor)) TextColor = "#F5F3ED";
        if (!IsColor(AccentColor)) AccentColor = "#A0B1BD";
        if (Background is not ("Transparent" or "Card" or "Solid")) Background = "Transparent";
        if (Alignment is not ("Left" or "Center" or "Right")) Alignment = "Left";
        if (TemplateFormatter.Validate(Template) != null) Template = "{time}\n{weekday} · {date}";
        if (Position is { } p && (!double.IsFinite(p.OffsetX) || !double.IsFinite(p.OffsetY))) Position = null;
        if (!Locked) ClickThrough = false;
    }

    public static bool IsColor(string? value) => value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
    private static double Limit(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
