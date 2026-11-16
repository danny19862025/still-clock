using System.Globalization;
using System.Text;

namespace Still.Core;

// This is plain text substitution, never a markup or expression evaluator.
public static class TemplateFormatter
{
    public static readonly string[] Tokens = ["time", "hour", "minute", "second", "ampm", "weekday", "day", "month", "year", "date"];
    public static string? Validate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return "Add some text or a placeholder, such as {time}.";
        if (template.Length > 240) return "Use at most 240 characters.";
        if (template.Split('\n').Length > 3) return "Use at most three lines.";
        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] == '}') return "A closing brace needs an opening brace.";
            if (template[i] != '{') continue;
            int end = template.IndexOf('}', i + 1);
            if (end < 0) return "Close each placeholder with }.";
            var token = template[(i + 1)..end];
            if (!Tokens.Contains(token)) return $"Unknown placeholder {{{token}}}. Try {{time}} or {{weekday}}.";
            i = end;
        }
        return null;
    }

    public static string Format(string template, DateTime time, Preferences p, CultureInfo? culture = null)
    {
        var error = Validate(template);
        if (error != null) throw new FormatException(error);
        culture ??= CultureInfo.CurrentCulture;
        var output = new StringBuilder();
        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] != '{') { output.Append(template[i]); continue; }
            int end = template.IndexOf('}', i + 1);
            output.Append(Value(template[(i + 1)..end], time, p, culture));
            i = end;
        }
        return output.ToString();
    }

    private static string Value(string token, DateTime time, Preferences p, CultureInfo c) => token switch
    {
        "time" => time.ToString((p.Hour24 ? "HH:mm" : "hh:mm") + (p.Seconds ? ":ss" : ""), c),
        "hour" => time.ToString(p.Hour24 ? "HH" : "hh", c),
        "minute" => time.ToString("mm", c),
        "second" => time.ToString("ss", c),
        "ampm" => p.Hour24 ? "" : time.ToString("tt", c),
        "weekday" => time.ToString("dddd", c),
        "day" => time.ToString("dd", c),
        "month" => time.ToString("MMMM", c),
        "year" => time.ToString("yyyy", c),
        "date" => time.ToString("d MMMM yyyy", c),
        _ => ""
    };

    public static bool NeedsSeconds(Preferences p) =>
        p.SecondsIndicator != "None" || p.BlinkSeparator || (p.Advanced
            ? p.Template.Contains("{second}") || (p.Template.Contains("{time}") && p.Seconds)
            : p.Seconds);

    /// <summary>Weekday and date parts of the preset date line; either may be empty.</summary>
    public static (string Weekday, string Date) DateParts(DateTime time, Preferences p, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        string weekday = p.Weekday && p.WeekdayStyle != "Line" ? time.ToString(p.WeekdayStyle == "Short" ? "ddd" : "dddd", culture) : "";
        string date = !p.Date ? "" : p.DateStyle switch
        {
            "Short" => time.ToString("d MMM", culture),
            "ISO" => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "Numeric" => time.ToString(culture.DateTimeFormat.ShortDatePattern, culture),
            _ => time.ToString("d MMMM yyyy", culture),
        };
        return p.Uppercase ? (weekday.ToUpper(culture), date.ToUpper(culture)) : (weekday, date);
    }

    /// <summary>A compact UTC offset tag such as "UTC+11:00".</summary>
    public static string ZoneTag(DateTime time)
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(time);
        return "UTC" + (offset < TimeSpan.Zero ? "−" : "+") + offset.Duration().ToString(@"hh\:mm");
    }

    public static TimeSpan NextUpdate(DateTime now, Preferences p)
    {
        long unit = NeedsSeconds(p) ? TimeSpan.TicksPerSecond : TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(unit - now.Ticks % unit + TimeSpan.TicksPerMillisecond * 15);
    }
}
