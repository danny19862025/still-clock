namespace Still.Core;

/// <summary>
/// Colours for the Settings and About windows (not the clock itself). Values are #RRGGBB or #AARRGGBB.
/// The live preview stage and preset cards stay dark in both modes: they stand in for a wallpaper.
/// </summary>
public sealed record UiPalette(
    bool IsLight,
    string Window, string Titlebar, string Control, string Hover, string Border, string Line,
    string Ink, string Muted, string Accent, string OnAccent, string Warning, string SelectedTint, string Scrollbar)
{
    public static readonly UiPalette Dark = new(false,
        Window: "#05080F", Titlebar: "#070C16", Control: "#0A1220", Hover: "#0D2B3D", Border: "#1D3448", Line: "#162838",
        Ink: "#E6F7FF", Muted: "#7F9BB0", Accent: "#00E5FF", OnAccent: "#000000", Warning: "#FF2BD6",
        SelectedTint: "#1C00E5FF", Scrollbar: "#1D3448");

    // Accent is a deeper cyan so text and focus rings keep at least 4.5:1 contrast on white.
    public static readonly UiPalette Light = new(true,
        Window: "#F3F6FA", Titlebar: "#FFFFFF", Control: "#FFFFFF", Hover: "#E2F4F8", Border: "#C3D1DD", Line: "#D5DFE8",
        Ink: "#0B1A26", Muted: "#4E6577", Accent: "#00778A", OnAccent: "#FFFFFF", Warning: "#B0127F",
        SelectedTint: "#1A00778A", Scrollbar: "#B5C4D1");

    /// <summary>Resolves a <see cref="Preferences.UiTheme"/> value; System follows the platform's light/dark setting.</summary>
    public static UiPalette For(string? mode, bool systemPrefersLight) => mode switch
    {
        "Light" => Light,
        "System" => systemPrefersLight ? Light : Dark,
        _ => Dark,
    };
}
