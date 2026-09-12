using PixelCompanion.Models;

namespace PixelCompanion.Services;

public static class ReminderService
{
    public const int DefaultGraceMinutes = 5;

    /// <summary>
    /// Evaluates uncompleted tasks and returns those whose due time falls within the reminder window:
    /// from (due - minutesBefore) up to (due + 5 minutes grace period).
    /// Completed tasks and tasks without a valid DueTime are excluded.
    /// </summary>
    public static IReadOnlyList<ObsidianTask> DueSoon(IEnumerable<ObsidianTask> tasks, DateTime now, int minutesBefore = 10)
    {
        if (tasks == null)
        {
            return Array.Empty<ObsidianTask>();
        }

        var results = new List<ObsidianTask>();
        int clampedMinutesBefore = Math.Clamp(minutesBefore, 0, 120);

        foreach (var task in tasks)
        {
            if (task.IsCompleted || !task.DueTime.HasValue)
            {
                continue;
            }

            DateTime dueDateTime = now.Date + task.DueTime.Value;
            DateTime windowStart = dueDateTime.AddMinutes(-clampedMinutesBefore);
            DateTime windowEnd = dueDateTime.AddMinutes(DefaultGraceMinutes);

            if (now >= windowStart && now <= windowEnd)
            {
                results.Add(task);
            }
        }

        return results;
    }

    /// <summary>
    /// Generates a unique deduplication key for a reminder notification:
    /// yyyy-MM-dd|indent|marker|text|due
    /// </summary>
    public static string GetReminderKey(ObsidianTask task, DateTime date)
    {
        string dueString = task.DueTime.HasValue ? task.DueTime.Value.ToString(@"hh\:mm") : "none";
        return $"{date:yyyy-MM-dd}|{task.Indent}|{task.ListMarker}|{task.Text.Trim()}|{dueString}";
    }
}
