using System.Globalization;

namespace Still.Core;

/// <summary>Dates shown by the optional calendar. Pure date arithmetic, so it is shared and tested.</summary>
public static class CalendarModel
{
    public static DayOfWeek FirstDay(Preferences p, CultureInfo? culture = null) => p.WeekStart switch
    {
        "Monday" => DayOfWeek.Monday,
        "Sunday" => DayOfWeek.Sunday,
        "Saturday" => DayOfWeek.Saturday,
        _ => (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.FirstDayOfWeek,
    };

    private static DateTime StartOfWeek(DateTime day, DayOfWeek first) => day.Date.AddDays(-(((int)day.DayOfWeek - (int)first + 7) % 7));

    /// <summary>Six full weeks (42 days) covering the month, so the calendar never changes height.</summary>
    public static DateTime[] MonthGrid(DateTime today, DayOfWeek first)
    {
        var start = StartOfWeek(new DateTime(today.Year, today.Month, 1), first);
        return Enumerable.Range(0, 42).Select(i => start.AddDays(i)).ToArray();
    }

    public static DateTime[] Week(DateTime today, DayOfWeek first)
    {
        var start = StartOfWeek(today, first);
        return Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToArray();
    }

    public static DateTime[] MonthDays(DateTime today) =>
        Enumerable.Range(1, DateTime.DaysInMonth(today.Year, today.Month)).Select(d => new DateTime(today.Year, today.Month, d)).ToArray();

    public static int WeekNumber(DateTime day) => ISOWeek.GetWeekOfYear(day);

    /// <summary>Short weekday names ("Mo", "Tu"…) starting on <paramref name="first"/>.</summary>
    public static string[] DayInitials(DayOfWeek first, CultureInfo? culture = null)
    {
        var names = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.AbbreviatedDayNames;
        return Enumerable.Range(0, 7).Select(i => Initial(names[((int)first + i) % 7], 2)).ToArray();
    }

    /// <summary>The first letters of a day name ("Mon" → "Mo"). Windows and Linux ship different "shortest" names, so derive them.</summary>
    public static string Initial(string name, int letters)
    {
        name = name.TrimEnd('.');
        return name.Length <= letters ? name : name[..letters];
    }

    public static string Initial(DayOfWeek day, int letters, CultureInfo? culture = null) =>
        Initial((culture ?? CultureInfo.CurrentCulture).DateTimeFormat.AbbreviatedDayNames[(int)day], letters);

    public static bool IsWeekend(DateTime day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
