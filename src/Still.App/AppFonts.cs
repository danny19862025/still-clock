namespace Still.App;

/// <summary>Turns a saved font name into a WPF FontFamily, using the fonts bundled in the app when needed.</summary>
internal static class AppFonts
{
    private static readonly Uri Base = new("pack://application:,,,/");
    private static HashSet<string>? installed;
    private static HashSet<string> Installed => installed ??= new(Fonts.SystemFontFamilies.Select(f => f.Source), StringComparer.OrdinalIgnoreCase);

    public static bool IsInstalled(string font) => Installed.Contains(font);

    /// <summary>True when Still can draw this font: installed, bundled, or with a bundled stand-in.</summary>
    public static bool IsAvailable(string font) =>
        IsInstalled(font) || FontCatalog.IsBundled(font) || FontCatalog.Fallbacks.ContainsKey(font);

    public static FontFamily Family(string font)
    {
        string name = FontCatalog.Resolve(font, IsInstalled);
        if (IsInstalled(name)) return new FontFamily(name);
        if (FontCatalog.IsBundled(name)) return new FontFamily(Base, "./Fonts/#" + name);
        return new FontFamily(name);
    }

    /// <summary>Font choices for Settings: bundled fonts first, then everything installed.</summary>
    public static List<string> Choices(string current)
    {
        var list = FontCatalog.Bundled.Concat(Installed.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!list.Contains(current, StringComparer.OrdinalIgnoreCase)) list.Insert(0, current);
        return list;
    }
}
