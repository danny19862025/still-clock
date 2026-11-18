using Microsoft.Win32;

namespace Still.App;

/// <summary>The Settings/About colour palette currently in use, exposed as WPF brushes and app resources.</summary>
internal static class Ui
{
    public static UiPalette Palette { get; private set; } = UiPalette.Dark;

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Brush B(string hex) { var b = new SolidColorBrush(C(hex)); b.Freeze(); return b; }

    public static Brush Window => B(Palette.Window);
    public static Brush Titlebar => B(Palette.Titlebar);
    public static Brush Control => B(Palette.Control);
    public static Brush Hover => B(Palette.Hover);
    public static Brush Border => B(Palette.Border);
    public static Brush Line => B(Palette.Line);
    public static Brush Ink => B(Palette.Ink);
    public static Brush Muted => B(Palette.Muted);
    public static Brush Accent => B(Palette.Accent);
    public static Brush OnAccent => B(Palette.OnAccent);
    public static Brush Warning => B(Palette.Warning);
    public static Brush SelectedTint => B(Palette.SelectedTint);
    public static Color AccentColor => C(Palette.Accent);

    // The preview stage and preset cards stay dark in both modes, so text drawn on them uses the dark palette.
    public static Brush StageAccent => B(UiPalette.Dark.Accent);
    public static Brush StageMuted => B(UiPalette.Dark.Muted);
    public static Brush StageInk => B(UiPalette.Dark.Ink);

    /// <summary>Windows' "app mode" setting (Settings > Personalisation > Colours).</summary>
    public static bool SystemPrefersLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    /// <summary>Resolves the preference and updates the shared style resources. Returns true when the palette changed.</summary>
    public static bool Apply(ResourceDictionary resources, string? mode)
    {
        var next = UiPalette.For(mode, SystemPrefersLight());
        bool changed = !ReferenceEquals(next, Palette);
        Palette = next;
        resources["UiWindow"] = Window; resources["UiControl"] = Control; resources["UiHover"] = Hover;
        resources["UiBorder"] = Border; resources["UiInk"] = Ink; resources["UiMuted"] = Muted; resources["UiAccent"] = Accent;
        return changed;
    }
}
