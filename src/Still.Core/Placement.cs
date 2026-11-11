namespace Still.Core;

public sealed record SavedPosition(string Display, double OffsetX, double OffsetY);
public sealed record DisplayArea(string Name, double X, double Y, double Width, double Height, double DpiScale, bool Primary = false);
public sealed record PixelPoint(double X, double Y);

public static class Placement
{
    // Work-area origins are physical pixels. Saved offsets and window sizes are DIPs.
    public static PixelPoint Restore(SavedPosition? saved, IReadOnlyList<DisplayArea> displays, double widthDip, double heightDip)
    {
        if (displays.Count == 0) throw new ArgumentException("At least one display is required.");
        var d = displays.FirstOrDefault(d => d.Name == saved?.Display)
            ?? displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        double x = d.X + (saved?.OffsetX ?? 64) * d.DpiScale;
        double y = d.Y + (saved?.OffsetY ?? 64) * d.DpiScale;
        if (!double.IsFinite(x)) x = d.X + 64;
        if (!double.IsFinite(y)) y = d.Y + 64;
        return new(Math.Clamp(x, d.X, Math.Max(d.X, d.X + d.Width - widthDip * d.DpiScale)),
            Math.Clamp(y, d.Y, Math.Max(d.Y, d.Y + d.Height - heightDip * d.DpiScale)));
    }

    public static SavedPosition Capture(DisplayArea display, double x, double y) =>
        new(display.Name, (x - display.X) / display.DpiScale, (y - display.Y) / display.DpiScale);
}
