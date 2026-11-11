using System.Globalization;
using Still.Core;

int count = 0;
void Test(string name, Action body)
{
    try { body(); count++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { Console.Error.WriteLine("FAIL " + name + ": " + e.Message); Environment.ExitCode = 1; }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}."); }
void True(bool value) { if (!value) throw new Exception("Expected true."); }
var en = CultureInfo.GetCultureInfo("en-AU");
var p = new Preferences();
var midnight = new DateTime(2026, 11, 12, 0, 4, 9);
Test("24-hour midnight and exact system sample", () => Equal("00:04", TemplateFormatter.Format("{time}", midnight, p, en)));
Test("12-hour midnight / noon", () =>
{
    var twelve = new Preferences { Hour24 = false };
    Equal("12:04 am", TemplateFormatter.Format("{time} {ampm}", midnight, twelve, en));
    Equal("12:04 pm", TemplateFormatter.Format("{time} {ampm}", midnight.AddHours(12), twelve, en));
});
Test("seconds and date rollover", () =>
{
    var seconds = new Preferences { Seconds = true };
    Equal("23:59:59", TemplateFormatter.Format("{time}", new DateTime(2026, 12, 31, 23, 59, 59), seconds, en));
    Equal("01 January 2027", TemplateFormatter.Format("{day} {month} {year}", new DateTime(2027, 1, 1), seconds, en));
});
Test("all tokens / regional names", () =>
{
    Equal("Thursday · 12 November 2026 · 00:04:09 · ", TemplateFormatter.Format("{weekday} · {date} · {hour}:{minute}:{second} · {ampm}", midnight, p, en));
    Equal("Donnerstag", TemplateFormatter.Format("{weekday}", midnight, p, CultureInfo.GetCultureInfo("de-DE")));
});
Test("unknown / nested / malformed / oversized templates rejected", () =>
{
    foreach (string bad in new[] { "", "{Time}", "{time", "time}", "{{time}}", "{random|a|b}", "a\nb\nc\nd", new string('x', 241) }) True(TemplateFormatter.Validate(bad) != null);
});
Test("markup and script remain inert literal text", () => Equal("<script>alert(1)</script> 00:04", TemplateFormatter.Format("<script>alert(1)</script> {time}", midnight, p, en)));
Test("minute / second phase scheduling", () =>
{
    Equal(TimeSpan.FromSeconds(51.015), TemplateFormatter.NextUpdate(midnight, p));
    var seconds = new Preferences { Advanced = true, Template = "{second}" };
    True(TemplateFormatter.NeedsSeconds(seconds));
    Equal(TimeSpan.FromMilliseconds(1015), TemplateFormatter.NextUpdate(midnight, seconds));
    seconds.Template = "{date}"; seconds.Seconds = true;
    True(!TemplateFormatter.NeedsSeconds(seconds));
});
Test("clock moving backward recomputes next boundary", () => Equal(TimeSpan.FromMilliseconds(265), TemplateFormatter.NextUpdate(midnight.AddMilliseconds(-250), new Preferences { Seconds = true })));

var primary = new DisplayArea("primary", 0, 0, 1920, 1040, 1, true);
var left = new DisplayArea("left", -2560, -200, 2560, 1400, 1.5);
Test("fresh install defaults to visible primary position", () => Equal(new PixelPoint(64, 64), Placement.Restore(null, [left, primary], 400, 200)));
Test("negative monitor origins and 150% DPI round-trip", () =>
{
    var saved = Placement.Capture(left, -2400, -80);
    Equal(new PixelPoint(-2400, -80), Placement.Restore(saved, [primary, left], 400, 200));
});
Test("disconnected monitor falls back and clamps", () => Equal(new PixelPoint(1520, 840), Placement.Restore(new SavedPosition("removed", 99999, 99999), [primary], 400, 200)));
Test("oversized widget anchors at work area origin", () => Equal(new PixelPoint(0, 0), Placement.Restore(new SavedPosition("primary", 800, 800), [primary], 4000, 3000)));
Test("DPI change retains DIP offsets", () => Equal(new PixelPoint(100, 120), Placement.Restore(new SavedPosition("primary", 50, 60), [primary with { DpiScale = 2 }], 400, 200)));
Test("corrupt offsets cannot restore off-screen", () => Equal(new PixelPoint(64, 64), Placement.Restore(new SavedPosition("primary", double.NaN, double.PositiveInfinity), [primary], 400, 200)));

string directory = Path.Combine(Path.GetTempPath(), "still-tests-" + Guid.NewGuid());
try
{
    var store = new SettingsStore(directory);
    Test("missing preferences use interaction-safe defaults", () =>
    {
        var defaults = store.Load();
        True(!defaults.Locked && !defaults.ClickThrough && !defaults.LaunchAtLogin && !defaults.Topmost);
        Equal("Minimal", defaults.Preset);
    });
    Test("preferences persist appearance and physical display identity", () =>
    {
        var source = new Preferences { Font = "Consolas", Scale = 1.3, Background = "Card", Position = new("left", 54, 72), Locked = true, ClickThrough = true, Advanced = true, Template = "{time}\n{weekday}" };
        True(store.Save(source)); var loaded = store.Load();
        Equal(source.Font, loaded.Font); Equal(source.Scale, loaded.Scale); Equal(source.Position, loaded.Position);
        True(loaded.ClickThrough && loaded.Advanced); Equal(source.Template, loaded.Template);
        True(!File.Exists(store.FilePath + ".tmp"));
    });
    Test("damaged JSON is preserved and defaults recover", () =>
    {
        File.WriteAllText(store.FilePath, "{ broken");
        Equal("Minimal", store.Load().Preset); True(store.Notice != null);
        Equal("{ broken", File.ReadAllText(store.FilePath + ".damaged"));
    });
    Test("null and invalid fields normalize safely", () =>
    {
        File.WriteAllText(store.FilePath, "{\"Font\":null,\"TextColor\":null,\"Template\":null,\"Scale\":-5,\"FontSize\":99999,\"Preset\":\"nope\",\"ClickThrough\":true}");
        var loaded = store.Load(); Equal(.5, loaded.Scale); Equal(160d, loaded.FontSize); Equal("Minimal", loaded.Preset);
        True(Preferences.IsColor(loaded.TextColor)); True(!loaded.ClickThrough); True(TemplateFormatter.Validate(loaded.Template) == null);
    });
    Test("save failure stays nonfatal and explains session-only changes", () =>
    {
        var blocked = new SettingsStore(store.FilePath); True(!blocked.Save(p)); True(blocked.Notice != null);
    });
    Test("all three presets remain valid and preserve desktop preferences", () =>
    {
        foreach (var name in new[] { "Minimal", "Typography", "Glass" })
        {
            var preset = new Preferences { Locked = true, Topmost = true }; preset.ApplyPreset(name); preset.Normalize();
            Equal(name, preset.Preset); True(preset.Locked && preset.Topmost);
            Equal(name == "Glass" ? "Card" : "Transparent", preset.Background);
        }
    });
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
Console.WriteLine($"{count} tests passed.");
