using System;

/// <summary>
/// Calendar rules for Riley's 30 days in segregation (Riley Playthrough doc, "The 30-day calendar").
/// Day 1 is a Sunday, matching <see cref="GameController.GetDayOfWeek"/>; every Monday from Day 2 through
/// Day 23 is a commissary RFI day (weeks 1-4).
/// </summary>
public static class SegCalendar
{
    public const int TotalDays = 30;
    public const int RfiWeeks = 4;
    private const int FirstRfiDay = 2;

    public static DayOfWeek DayOfWeekOf(int day)
    {
        return (DayOfWeek)((day - 1) % 7);
    }

    /// <summary>RFI week a day falls in (1 for Days 2-8, 2 for Days 9-15, ...); 0 before the first RFI.</summary>
    public static int WeekOf(int day)
    {
        return day < FirstRfiDay ? 0 : (day - FirstRfiDay) / 7 + 1;
    }

    public static bool IsRfiDay(int day)
    {
        return day >= FirstRfiDay && (day - FirstRfiDay) % 7 == 0 && WeekOf(day) <= RfiWeeks;
    }

    /// <summary>"Day 5 (Thursday)"</summary>
    public static string Describe(int day)
    {
        return "Day " + day + " (" + DayOfWeekOf(day) + ")";
    }
}
