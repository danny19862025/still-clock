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
        Equal("Neon", defaults.Preset); True(defaults.Glow);
    });
    Test("older settings files keep their look: new effects default off", () =>
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(store.FilePath, "{\"Version\":1,\"Preset\":\"Glass\",\"Background\":\"Card\",\"TextColor\":\"#FF8080\"}");
        var loaded = store.Load();
        Equal("Glass", loaded.Preset); Equal("#FF8080", loaded.TextColor);
        True(!loaded.Glow && !loaded.Gradient && !loaded.Scanlines && !loaded.BlinkSeparator);
        Equal("None", loaded.Frame); Equal("None", loaded.SecondsIndicator); Equal(1d, loaded.Opacity);
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
        Equal("Neon", store.Load().Preset); True(store.Notice != null);
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
            Equal(name switch { "Glass" => "Card", "Typography" => "Solid", _ => "Transparent" }, preset.Background);
            Equal(name == "Typography" ? 1d : .64, preset.BackgroundOpacity);
        }
    });
    Test("every preset survives normalization unchanged", () =>
    {
        foreach (var name in Preferences.Presets)
        {
            var preset = new Preferences(); preset.ApplyPreset(name);
            string before = System.Text.Json.JsonSerializer.Serialize(preset);
            preset.Normalize();
            Equal(before, System.Text.Json.JsonSerializer.Serialize(preset));
        }
    });
    Test("invalid effect fields normalize safely", () =>
    {
        File.WriteAllText(store.FilePath, "{\"Frame\":\"Laser\",\"SecondsIndicator\":null,\"FontWeight\":\"Heavy\",\"SurfaceColor\":\"red\",\"Opacity\":0,\"GlowStrength\":9,\"CornerRadius\":-1,\"DateStyle\":\"x\",\"WeekdayStyle\":null,\"Caption\":\"" + new string('x', 90) + "\"}");
        var loaded = store.Load();
        Equal("None", loaded.Frame); Equal("None", loaded.SecondsIndicator); Equal("Light", loaded.FontWeight);
        True(Preferences.IsColor(loaded.SurfaceColor)); Equal(.2, loaded.Opacity); Equal(1d, loaded.GlowStrength);
        Equal(0d, loaded.CornerRadius); Equal("Long", loaded.DateStyle); Equal("Full", loaded.WeekdayStyle); Equal(40, loaded.Caption.Length);
    });
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

Test("poster layout: presets, normalization and older files", () =>
{
    var poster = new Preferences(); poster.ApplyPreset("Poster");
    Equal("Poster", poster.Layout); True(poster.Fade);
    poster.ApplyPreset("Neon"); Equal("Classic", poster.Layout);
    var bad = new Preferences { Layout = "Spiral" }; bad.Normalize(); Equal("Classic", bad.Layout);
    var legacy = System.Text.Json.JsonSerializer.Deserialize<Preferences>("{\"Preset\":\"Glass\"}")!; legacy.Normalize();
    Equal("Classic", legacy.Layout);
});
Test("script layout: preset, clamped width and safe font fallback", () =>
{
    var sig = new Preferences(); sig.ApplyPreset("Signature");
    Equal("Script", sig.Layout); Equal("Segoe Script", sig.ScriptFont); True(!sig.Fade);
    var bad = new Preferences { Layout = "Script", Condense = 9, ScriptFont = "" }; bad.Normalize();
    Equal(1d, bad.Condense); Equal("Segoe Script", bad.ScriptFont);
    bad.Condense = double.NaN; bad.Normalize(); Equal(.6, bad.Condense);
});
Test("overlay and arc presets select their layouts", () =>
{
    var look = new Preferences(); look.ApplyPreset("Midnight"); Equal("Overlay", look.Layout);
    look.ApplyPreset("Arc"); Equal("Arc", look.Layout); True(look.Fade);
});
Test("ordinal dates in English, plain dates elsewhere", () =>
{
    Equal("27th February 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 2, 27), en));
    Equal("1st March 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 3, 1), en));
    Equal("2nd March 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 3, 2), en));
    Equal("3rd March 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 3, 3), en));
    Equal("11th March 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 3, 11), en));
    Equal("22nd March 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 3, 22), en));
    Equal("27 Februar 2026", TemplateFormatter.OrdinalDate(new DateTime(2026, 2, 27), CultureInfo.GetCultureInfo("de-DE")));
});
Test("poster compact time follows 12/24-hour, seconds and AM/PM", () =>
{
    var evening = new DateTime(2026, 11, 17, 20, 30, 5);
    Equal("8:30 PM", TemplateFormatter.CompactTime(evening, new Preferences { Hour24 = false }, en));
    Equal("8:30", TemplateFormatter.CompactTime(evening, new Preferences { Hour24 = false, AmPm = false }, en));
    Equal("20:30:05", TemplateFormatter.CompactTime(evening, new Preferences { Seconds = true }, en));
});

Test("seconds indicator and blinking separator tick every second", () =>
{
    True(TemplateFormatter.NeedsSeconds(new Preferences { SecondsIndicator = "Bar" }));
    True(TemplateFormatter.NeedsSeconds(new Preferences { BlinkSeparator = true }));
    Equal(TimeSpan.FromMilliseconds(1015), TemplateFormatter.NextUpdate(midnight, new Preferences { SecondsIndicator = "Dots" }));
});
Test("typed hex colours: with or without #, short form, any case", () =>
{
    Equal("#FF2BD6", Preferences.ParseHexColor("#ff2bd6"));
    Equal("#FF2BD6", Preferences.ParseHexColor("  FF2BD6 "));
    Equal("#FF22DD", Preferences.ParseHexColor("#f2d"));
    Equal("#00E5FF", Preferences.ParseHexColor("00e5ff"));
    foreach (string bad in new[] { "", "#", "#12345", "#1234567", "#GG0000", "red", "#12 34 56", null! }) Equal<string?>(null, Preferences.ParseHexColor(bad));
    True(Preferences.IsColor(Preferences.ParseHexColor("abc")));
});
Test("split preset: bold accent hour layout on a flat panel", () =>
{
    var split = new Preferences(); split.ApplyPreset("Split"); split.Normalize();
    Equal("Split", split.Layout); Equal("Solid", split.Background); Equal("#FF006E", split.AccentColor);
    True(split.Seconds && split.Date && !split.Weekday);
    split.ApplyPreset("Neon"); Equal("Classic", split.Layout);
    var odd = new Preferences { Layout = "Split" }; odd.Normalize(); Equal("Split", odd.Layout);
});
Test("calendar: month grid, weeks, strip, week start and week numbers", () =>
{
    var day = new DateTime(2026, 11, 18); // a Wednesday
    var grid = CalendarModel.MonthGrid(day, DayOfWeek.Monday);
    Equal(42, grid.Length); Equal(new DateTime(2026, 10, 26), grid[0]); Equal(DayOfWeek.Monday, grid[0].DayOfWeek);
    True(grid.Contains(new DateTime(2026, 11, 1)) && grid.Contains(new DateTime(2026, 11, 30)));
    Equal(new DateTime(2026, 11, 1), CalendarModel.MonthGrid(day, DayOfWeek.Sunday)[0]); // the 1st is a Sunday
    var week = CalendarModel.Week(day, DayOfWeek.Monday);
    Equal(new DateTime(2026, 11, 16), week[0]); Equal(new DateTime(2026, 11, 22), week[6]);
    Equal(new DateTime(2026, 11, 14), CalendarModel.Week(day, DayOfWeek.Saturday)[0]);
    Equal(30, CalendarModel.MonthDays(day).Length); Equal(29, CalendarModel.MonthDays(new DateTime(2028, 2, 3)).Length);
    Equal(47, CalendarModel.WeekNumber(day)); Equal(53, CalendarModel.WeekNumber(new DateTime(2026, 12, 31)));
    Equal("Mo", CalendarModel.DayInitials(DayOfWeek.Monday, en)[0]); Equal("Su", CalendarModel.DayInitials(DayOfWeek.Monday, en)[6]);
    Equal("Di", CalendarModel.DayInitials(DayOfWeek.Monday, CultureInfo.GetCultureInfo("de-DE"))[1]); Equal("W", CalendarModel.Initial(DayOfWeek.Wednesday, 1, en));
    Equal(DayOfWeek.Sunday, CalendarModel.FirstDay(new Preferences { WeekStart = "Sunday" }, en));
});
Test("calendar settings: off by default, kept by presets, normalized", () =>
{
    var p2 = new Preferences(); True(!p2.ShowCalendar); Equal("Month", p2.CalendarStyle);
    p2.ShowCalendar = true; p2.CalendarStyle = "Week"; p2.ApplyPreset("Split"); True(p2.ShowCalendar); Equal("Week", p2.CalendarStyle);
    var bad = new Preferences { CalendarStyle = "Year", CalendarPlacement = "Above", WeekStart = "Friday" }; bad.Normalize();
    Equal("Month", bad.CalendarStyle); Equal("Below", bad.CalendarPlacement); Equal("Regional", bad.WeekStart);
});
Test("date styles", () =>
{
    var d = new Preferences { WeekdayStyle = "Short", DateStyle = "ISO" };
    Equal(("THU", "2026-11-12"), TemplateFormatter.DateParts(midnight, d, en));
    d.Uppercase = false; d.DateStyle = "Short";
    Equal(("Thu", "12 Nov"), TemplateFormatter.DateParts(midnight, d, en));
    d.WeekdayStyle = "Line"; d.DateStyle = "Long";
    Equal(("", "12 November 2026"), TemplateFormatter.DateParts(midnight, d, en));
    d.Weekday = false; d.Date = false;
    Equal(("", ""), TemplateFormatter.DateParts(midnight, d, en));
});
Test("themes round-trip appearance but never desktop placement or behaviour", () =>
{
    var source = new Preferences(); source.ApplyPreset("HUD"); source.AccentColor = "#FF2BD6"; source.Caption = "BRIDGE";
    source.Position = new("left", 1, 2); source.Locked = true; source.Topmost = true;
    string theme = source.ExportTheme();
    True(!theme.Contains("left"));
    var target = new Preferences { Position = new("primary", 9, 9), Locked = false, Topmost = false };
    Equal<string?>(null, target.ImportTheme(theme));
    Equal("HUD", target.Preset); Equal("#FF2BD6", target.AccentColor); Equal("BRIDGE", target.Caption); Equal("Brackets", target.Frame);
    Equal(new SavedPosition("primary", 9, 9), target.Position); True(!target.Locked && !target.Topmost);
});
Test("malformed themes are rejected without changes", () =>
{
    var target = new Preferences();
    foreach (string? bad in new[] { null, "", "hello", "still-theme:{oops", "still-theme:" + new string(' ', 17000) })
        True(target.ImportTheme(bad) != null);
    Equal("Minimal", target.Preset);
    Equal<string?>(null, target.ImportTheme("still-theme:{\"Frame\":\"Laser\",\"Preset\":\"Holo\"}"));
    Equal("Holo", target.Preset); Equal("None", target.Frame);
});
Test("UI theme: normalized, resolved, and never carried by shared themes", () =>
{
    var prefs = new Preferences { UiTheme = "Neon" }; prefs.Normalize(); Equal("Dark", prefs.UiTheme);
    True(!UiPalette.For("Dark", true).IsLight); True(UiPalette.For("Light", false).IsLight);
    True(UiPalette.For("System", true).IsLight); True(!UiPalette.For("System", false).IsLight);
    var source = new Preferences { UiTheme = "Light" };
    var target = new Preferences { UiTheme = "Dark" };
    Equal<string?>(null, target.ImportTheme(source.ExportTheme()));
    Equal("Dark", target.UiTheme);
});
Test("update versions compare numerically and reject odd tags", () =>
{
    Equal(new Version(2, 10, 0), UpdateChecker.ParseVersion("v2.10.0"));
    Equal(new Version(3, 1, 0), UpdateChecker.ParseVersion("3.1"));
    foreach (var bad in new[] { null, "", "latest", "v2.4.0-rc1", "v2", "v1.2.3.4", "v-1.0.0" }) True(UpdateChecker.ParseVersion(bad) == null);
    True(UpdateChecker.IsNewer(new Version(2, 10, 0), new Version(2, 9, 0)));
    True(!UpdateChecker.IsNewer(new Version(2, 4, 0), new Version(2, 4, 0)));
    True(!UpdateChecker.IsNewer(new Version(2, 3, 0), new Version(2, 4, 0)));
    True(UpdateChecker.IsNewer(new Version(2, 4, 1), new Version(2, 4)));
});
Test("latest release parsing trusts only this repository's downloads", () =>
{
    const string json = """
    {"tag_name":"v2.5.0","draft":false,"prerelease":false,"html_url":"https://github.com/danny19862025/still-clock/releases/tag/v2.5.0",
     "assets":[{"name":"Still-Setup-win-x64.exe","browser_download_url":"https://github.com/danny19862025/still-clock/releases/download/v2.5.0/Still-Setup-win-x64.exe"},
               {"name":"SHA256SUMS.txt","browser_download_url":"https://evil.example/SHA256SUMS.txt"}]}
    """;
    var release = UpdateChecker.ParseLatest(json);
    Equal(new Version(2, 5, 0), release.Version);
    True(release.InstallerUrl != null); True(release.ChecksumsUrl == null);
    True(UpdateChecker.Summary(release, new Version(2, 4, 0)).Contains("2.5.0 is available"));
    True(UpdateChecker.Summary(release, new Version(2, 5, 0)).Contains("up to date"));
    foreach (var bad in new[] { "{\"tag_name\":\"v3.0.0\",\"prerelease\":true}", "{\"message\":\"rate limit\"}", "[]" })
    {
        bool threw = false;
        try { UpdateChecker.ParseLatest(bad); } catch (FormatException) { threw = true; }
        True(threw);
    }
});
Test("checksum listing lookup", () =>
{
    string hash = new string('a', 64), other = new string('b', 64);
    string sums = $"{other}  Still-win-x64.zip\n{hash}  Still-Setup-win-x64.exe\n";
    Equal<string?>(hash, UpdateChecker.ExpectedHash(sums, "Still-Setup-win-x64.exe"));
    Equal<string?>(hash, UpdateChecker.ExpectedHash($"{hash} *Still-Setup-win-x64.exe", "Still-Setup-win-x64.exe"));
    Equal<string?>(null, UpdateChecker.ExpectedHash(sums, "missing.exe"));
    Equal<string?>(null, UpdateChecker.ExpectedHash("nothex  Still-Setup-win-x64.exe", "Still-Setup-win-x64.exe"));
});
Test("overlay and arc time includes enabled seconds and AM/PM", () =>
{
    var time = new DateTime(2026, 11, 19, 20, 30, 45);
    var options = new Preferences { Hour24 = false, Seconds = true, AmPm = true };
    Equal("8:30:45 PM", TemplateFormatter.CompactTime(time, options, en));
    Equal(("08 PM", "30:45"), TemplateFormatter.StackedTime(time, options, en));
    options.Hour24 = true;
    Equal(("20", "30:45"), TemplateFormatter.StackedTime(time, options, en));
    options.Seconds = false;
    Equal(("20", "30"), TemplateFormatter.StackedTime(time, options, en));
    options.Hour24 = false; options.AmPm = false;
    Equal(("08", "30"), TemplateFormatter.StackedTime(time, options, en));
});
Test("spaced layout: preset and regional full date", () =>
{
    var look = new Preferences(); look.ApplyPreset("Spaced");
    Equal("Spaced", look.Layout); True(look.Seconds); look.Normalize(); Equal("Spaced", look.Layout);
    var day = new DateTime(2026, 9, 28);
    Equal("September 28, 2026", TemplateFormatter.FullDate(day, CultureInfo.GetCultureInfo("en-US")));
    Equal("28 September 2026", TemplateFormatter.FullDate(day, en));
    Equal("28 September 2026", TemplateFormatter.FullDate(day, CultureInfo.GetCultureInfo("de-DE")));
});
Test("bundled fonts: Anurati falls back to Michroma unless installed; small-text font normalizes", () =>
{
    var look = new Preferences(); look.ApplyPreset("Spaced");
    Equal("Anurati", look.Font); Equal("Quicksand", look.DetailFont);
    Equal("Michroma", FontCatalog.Resolve("Anurati", _ => false));
    Equal("Anurati", FontCatalog.Resolve("Anurati", name => name == "Anurati"));
    Equal("Bahnschrift", FontCatalog.Resolve("Bahnschrift", _ => false));
    True(FontCatalog.IsBundled("quicksand") && FontCatalog.IsBundled("Michroma") && !FontCatalog.IsBundled("Anurati"));
    var other = new Preferences(); other.ApplyPreset("Neon"); Equal("", other.DetailFont);
    var bad = new Preferences { DetailFont = new string('x', 200) }; bad.Normalize(); Equal("", bad.DetailFont);
    var spaced = new Preferences { DetailFont = "  Quicksand " }; spaced.Normalize(); Equal("Quicksand", spaced.DetailFont);
});
Test("flip presets: dark and light panels on the Flip layout", () =>
{
    var dark = new Preferences(); dark.ApplyPreset("Flip");
    var light = new Preferences(); light.ApplyPreset("Flip Light");
    foreach (var look in new[] { dark, light })
    {
        Equal("Flip", look.Layout); Equal("Solid", look.Background); Equal(1d, look.BackgroundOpacity);
        look.Normalize(); Equal("Flip", look.Layout);
    }
    True(dark.SurfaceColor != light.SurfaceColor && dark.TextColor != light.TextColor);
    var keep = new Preferences { Topmost = true, Locked = true }; keep.ApplyPreset("Flip"); True(keep.Topmost && keep.Locked);
});
Console.WriteLine($"{count} tests passed.");
