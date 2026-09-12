using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class ReminderServiceTests
{
    [TestMethod]
    public void Test_DueSoon_WindowAndGracePeriodBoundaries()
    {
        var baseDate = new DateTime(2026, 9, 10, 14, 0, 0); // 14:00

        var taskInWindow = new ObsidianTask
        {
            Text = "Task due in 10 mins ⏰ 14:10",
            DueTime = new TimeSpan(14, 10, 0),
            IsCompleted = false
        };

        var taskExactlyAtWindowStart = new ObsidianTask
        {
            Text = "Task due in 15 mins ⏰ 14:15",
            DueTime = new TimeSpan(14, 15, 0),
            IsCompleted = false
        };

        var taskOutsideWindowFuture = new ObsidianTask
        {
            Text = "Task due in 30 mins ⏰ 14:30",
            DueTime = new TimeSpan(14, 30, 0),
            IsCompleted = false
        };

        var taskInGracePeriod = new ObsidianTask
        {
            Text = "Task due 3 mins ago ⏰ 13:57",
            DueTime = new TimeSpan(13, 57, 0),
            IsCompleted = false
        };

        var taskExactlyAtGraceEnd = new ObsidianTask
        {
            Text = "Task due 5 mins ago ⏰ 13:55",
            DueTime = new TimeSpan(13, 55, 0),
            IsCompleted = false
        };

        var taskPastGracePeriod = new ObsidianTask
        {
            Text = "Task due 6 mins ago ⏰ 13:54",
            DueTime = new TimeSpan(13, 54, 0),
            IsCompleted = false
        };

        var completedTask = new ObsidianTask
        {
            Text = "Completed task due in 5 mins ⏰ 14:05",
            DueTime = new TimeSpan(14, 5, 0),
            IsCompleted = true
        };

        var taskWithoutDueTime = new ObsidianTask
        {
            Text = "Task without time",
            DueTime = null,
            IsCompleted = false
        };

        var allTasks = new[]
        {
            taskInWindow,
            taskExactlyAtWindowStart,
            taskOutsideWindowFuture,
            taskInGracePeriod,
            taskExactlyAtGraceEnd,
            taskPastGracePeriod,
            completedTask,
            taskWithoutDueTime
        };

        var due = ReminderService.DueSoon(allTasks, baseDate, minutesBefore: 15);

        Assert.HasCount(4, due);
        Assert.IsTrue(due.Contains(taskInWindow));
        Assert.IsTrue(due.Contains(taskExactlyAtWindowStart));
        Assert.IsTrue(due.Contains(taskInGracePeriod));
        Assert.IsTrue(due.Contains(taskExactlyAtGraceEnd));
        Assert.IsFalse(due.Contains(taskOutsideWindowFuture));
        Assert.IsFalse(due.Contains(taskPastGracePeriod));
        Assert.IsFalse(due.Contains(completedTask));
        Assert.IsFalse(due.Contains(taskWithoutDueTime));
    }

    [TestMethod]
    public void Test_DueSoon_MinutesBeforeClamping()
    {
        var baseDate = new DateTime(2026, 9, 10, 14, 0, 0);

        var task = new ObsidianTask
        {
            Text = "Task due ⏰ 14:05",
            DueTime = new TimeSpan(14, 5, 0),
            IsCompleted = false
        };

        // When minutesBefore < 0 (e.g. -10), clamped to 0 -> only due now to +5 grace
        var dueNegative = ReminderService.DueSoon(new[] { task }, baseDate, minutesBefore: -10);
        Assert.IsEmpty(dueNegative); // 14:05 is 5 mins in the future, with 0 mins lead time it's not due yet

        // When now reaches 14:05, it is in window
        var dueAtTime = ReminderService.DueSoon(new[] { task }, new DateTime(2026, 9, 10, 14, 5, 0), minutesBefore: 0);
        Assert.HasCount(1, dueAtTime);
    }

    [TestMethod]
    public void Test_GetReminderKey_Format()
    {
        var task = new ObsidianTask
        {
            Indent = "  ",
            ListMarker = "-",
            Text = "Complete assignment ⏰ 16:30",
            DueTime = new TimeSpan(16, 30, 0)
        };

        string key = ReminderService.GetReminderKey(task, new DateTime(2026, 9, 10));
        Assert.AreEqual("2026-09-10|  |-|Complete assignment ⏰ 16:30|16:30", key);

        var taskNoTime = new ObsidianTask
        {
            Indent = "",
            ListMarker = "*",
            Text = "General task",
            DueTime = null
        };

        string key2 = ReminderService.GetReminderKey(taskNoTime, new DateTime(2026, 9, 10));
        Assert.AreEqual("2026-09-10||*|General task|none", key2);
    }
}
