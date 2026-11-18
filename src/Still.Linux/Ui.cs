using Avalonia.Platform;
using Avalonia.Styling;

namespace Still.Linux;

/// <summary>The Settings/About colour palette currently in use, as Avalonia brushes and app resources.</summary>
internal static class Ui
{
    public static UiPalette Palette { get; private set; } = UiPalette.Dark;

    private static IBrush B(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
    public static IBrush Window => B(Palette.Window);
    public static IBrush Titlebar => B(Palette.Titlebar);
    public static IBrush Control => B(Palette.Control);
    public static IBrush Line => B(Palette.Line);
    public static IBrush Ink => B(Palette.Ink);
    public static IBrush Muted => B(Palette.Muted);
    public static IBrush Accent => B(Palette.Accent);
    public static IBrush OnAccent => B(Palette.OnAccent);
    public static IBrush Warning => B(Palette.Warning);
    public static IBrush SelectedTint => B(Palette.SelectedTint);
    public static Color AccentColor => Color.Parse(Palette.Accent);

    // The preview stage and preset cards stay dark in both modes.
    public static IBrush StageAccent => B(UiPalette.Dark.Accent);
    public static IBrush StageMuted => B(UiPalette.Dark.Muted);

    /// <summary>The desktop's light/dark preference (GNOME, KDE and others report it through Avalonia).</summary>
    public static bool SystemPrefersLight() =>
        Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Light;

    /// <summary>Resolves the preference and updates shared resources and Fluent's theme. Returns true when the palette changed.</summary>
    public static bool Apply(Application app, string? mode)
    {
        var next = UiPalette.For(mode, SystemPrefersLight());
        bool changed = !ReferenceEquals(next, Palette);
        Palette = next;
        app.RequestedThemeVariant = next.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;
        var r = app.Resources;
        r["UiWindow"] = Window; r["UiControl"] = Control; r["UiHover"] = B(Palette.Hover); r["UiBorder"] = B(Palette.Border);
        r["UiInk"] = Ink; r["UiMuted"] = Muted; r["UiAccent"] = Accent; r["UiOnAccent"] = OnAccent;
        r["UiExpander"] = next.IsLight ? B("#FFFFFF") : B("#080F1A");
        return changed;
    }
}
