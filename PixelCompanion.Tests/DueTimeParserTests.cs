using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class DueTimeParserTests
{
    private readonly ObsidianTaskParser _parser = new();

    [TestMethod]
    public void Test_TryParseDueTime_Variants()
    {
        // ⏰ with space
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("Meeting with team ⏰ 18:00", out var dt1));
        Assert.AreEqual(new TimeSpan(18, 0, 0), dt1);

        // ⏰ without space
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("Doctor appointment ⏰18:00", out var dt2));
        Assert.AreEqual(new TimeSpan(18, 0, 0), dt2);

        // @ variant
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("Standup call @09:30", out var dt3));
        Assert.AreEqual(new TimeSpan(9, 30, 0), dt3);

        // Single-digit hour
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("Morning coffee ⏰ 8:15", out var dt4));
        Assert.AreEqual(new TimeSpan(8, 15, 0), dt4);

        // Midnight and late night
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("Midnight cleanup @00:00", out var dt5));
        Assert.AreEqual(new TimeSpan(0, 0, 0), dt5);
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("Late review ⏰ 23:59", out var dt6));
        Assert.AreEqual(new TimeSpan(23, 59, 0), dt6);
    }

    [TestMethod]
    public void Test_TryParseDueTime_PersianAndArabicDigits()
    {
        // Persian digits (۱۸:۰۰ -> 18:00)
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("جلسه با تیم ⏰ ۱۸:۰۰", out var dtPersian));
        Assert.AreEqual(new TimeSpan(18, 0, 0), dtPersian);

        // Persian digits with @ (۰۹:۳۰ -> 09:30)
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("بررسی کدها @۰۹:۳۰", out var dtPersian2));
        Assert.AreEqual(new TimeSpan(9, 30, 0), dtPersian2);

        // Arabic-Indic digits (١٤:٤٥ -> 14:45)
        Assert.IsTrue(ObsidianTaskParser.TryParseDueTime("لقاء ⏰ ١٤:٤٥", out var dtArabic));
        Assert.AreEqual(new TimeSpan(14, 45, 0), dtArabic);
    }

    [TestMethod]
    public void Test_TryParseDueTime_InvalidTimesIgnored()
    {
        // Invalid hour (25:00)
        Assert.IsFalse(ObsidianTaskParser.TryParseDueTime("Invalid hour ⏰ 25:00", out _));

        // Invalid minute (12:99)
        Assert.IsFalse(ObsidianTaskParser.TryParseDueTime("Invalid minute ⏰ 12:99", out _));

        // Completely invalid (25:99)
        Assert.IsFalse(ObsidianTaskParser.TryParseDueTime("Impossible time ⏰ 25:99", out _));

        // Malformed
        Assert.IsFalse(ObsidianTaskParser.TryParseDueTime("No digits ⏰ ab:cd", out _));
        Assert.IsFalse(ObsidianTaskParser.TryParseDueTime("Empty string", out _));
        Assert.IsFalse(ObsidianTaskParser.TryParseDueTime(string.Empty, out _));
    }

    [TestMethod]
    public void Test_ParseTasks_AssignsDueTime_PreservesFullText()
    {
        string[] lines =
        [
            "- [ ] Prepare slides ⏰ 14:30 for conference",
            "- [x] Completed task without time",
            "- [ ] Invalid time task ⏰ 99:99",
            "  * [ ] Indented task @10:00"
        ];

        var tasks = _parser.ParseTasks(lines);

        Assert.HasCount(4, tasks);

        // Task 1: DueTime parsed, full text preserved
        Assert.AreEqual(new TimeSpan(14, 30, 0), tasks[0].DueTime);
        Assert.AreEqual("Prepare slides ⏰ 14:30 for conference", tasks[0].Text);

        // Task 2: No time -> null
        Assert.IsNull(tasks[1].DueTime);
        Assert.AreEqual("Completed task without time", tasks[1].Text);

        // Task 3: Invalid time -> null, full text preserved
        Assert.IsNull(tasks[2].DueTime);
        Assert.AreEqual("Invalid time task ⏰ 99:99", tasks[2].Text);

        // Task 4: @ variant with indentation
        Assert.AreEqual(new TimeSpan(10, 0, 0), tasks[3].DueTime);
        Assert.AreEqual("Indented task @10:00", tasks[3].Text);
    }

    [TestMethod]
    public void Test_BuildToggledLine_PreservesTokenByteIdentical()
    {
        string originalLine = "- [ ] Submit taxes ⏰ ۱۸:۰۰ by end of day";
        string[] lines = [originalLine];

        var tasks = _parser.ParseTasks(lines);
        Assert.HasCount(1, tasks);

        // Toggle to completed
        string toggledToTrue = _parser.BuildToggledLine(tasks[0], true);
        Assert.AreEqual("- [x] Submit taxes ⏰ ۱۸:۰۰ by end of day", toggledToTrue);

        // Toggle back to uncompleted
        tasks[0].RawLine = toggledToTrue;
        string toggledToFalse = _parser.BuildToggledLine(tasks[0], false);
        Assert.AreEqual(originalLine, toggledToFalse);
    }
}
