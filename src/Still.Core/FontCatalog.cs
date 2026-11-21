namespace Still.Core;

/// <summary>Fonts shipped with Still and the fallbacks used when a preset's font is not installed.</summary>
public static class FontCatalog
{
    /// <summary>Open-licence (SIL OFL 1.1) fonts bundled with both the Windows and Linux apps.</summary>
    public static readonly string[] Bundled = ["Quicksand", "Michroma"];

    /// <summary>
    /// Fonts a preset may ask for that Still cannot redistribute. Anurati (the wide display face of the
    /// popular "Mond" desktop look) is free for personal use, so it is used only when the user has installed it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Fallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Anurati"] = "Michroma",
    };

    /// <summary>The font to draw with: the requested one if installed, else its bundled stand-in, else the request unchanged.</summary>
    public static string Resolve(string font, Func<string, bool> isInstalled)
    {
        if (string.IsNullOrWhiteSpace(font) || isInstalled(font)) return font;
        return Fallbacks.TryGetValue(font, out var fallback) ? fallback : font;
    }

    public static bool IsBundled(string font) => Bundled.Contains(font, StringComparer.OrdinalIgnoreCase);
}
