using System.IO;
using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class RolloverTests
{
    private string _tempVault = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempVault = Path.Combine(Path.GetTempPath(), $"CC_TestVault_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempVault);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempVault))
        {
            try
            {
                Directory.Delete(_tempVault, true);
            }
            catch
            {
            }
        }
    }

    private (ObsidianService service, AppConfig config, ConfigService configService) CreateService(bool rolloverEnabled = true)
    {
        var config = new AppConfig
        {
            ObsidianVaultPath = _tempVault,
            DailyNotesFolder = "Daily",
            DailyNoteDateFormat = "yyyy-MM-dd",
            RolloverEnabled = rolloverEnabled
        };

        var configService = new ConfigService();
        var service = new ObsidianService(configService, config);
        return (service, config, configService);
    }

    [TestMethod]
    public void Test_Rollover_CreatesDailyNoteWithUnfinishedTasksFromYesterday()
    {
        var (service, _, _) = CreateService(rolloverEnabled: true);

        // Seed yesterday's daily note
        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime yesterday = DateTime.Today.AddDays(-1);
        string yesterdayFile = Path.Combine(dailyDir, $"{yesterday:yyyy-MM-dd}.md");
        File.WriteAllLines(yesterdayFile, new[]
        {
            $"# {yesterday:yyyy-MM-dd}",
            "- [ ] Unfinished task ⏰ 17:00",
            "- [x] Finished task",
            "- [ ] Another pending item"
        });

        // Ensure today's note
        string? todayPath = service.EnsureDailyNoteFileExists(DateTime.Today);
        Assert.IsNotNull(todayPath);
        Assert.IsTrue(File.Exists(todayPath));

        string todayContent = File.ReadAllText(todayPath);
        StringAssert.Contains(todayContent, "## From yesterday");
        StringAssert.Contains(todayContent, "- [ ] Unfinished task ⏰ 17:00");
        StringAssert.Contains(todayContent, "- [ ] Another pending item");
        Assert.DoesNotContain("Finished task", todayContent);

        // Verify tasks parsed from today's note
        var tasks = service.GetTodayTasks();
        Assert.HasCount(2, tasks);
        Assert.AreEqual("Unfinished task ⏰ 17:00", tasks[0].Text);
        Assert.AreEqual(new TimeSpan(17, 0, 0), tasks[0].DueTime);
    }

    [TestMethod]
    public void Test_Rollover_Disabled_DoesNotRollover()
    {
        var (service, _, _) = CreateService(rolloverEnabled: false);

        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime yesterday = DateTime.Today.AddDays(-1);
        string yesterdayFile = Path.Combine(dailyDir, $"{yesterday:yyyy-MM-dd}.md");
        File.WriteAllLines(yesterdayFile, new[]
        {
            $"# {yesterday:yyyy-MM-dd}",
            "- [ ] Unfinished task"
        });

        string? todayPath = service.EnsureDailyNoteFileExists(DateTime.Today);
        Assert.IsNotNull(todayPath);
        Assert.IsTrue(File.Exists(todayPath));

        string todayContent = File.ReadAllText(todayPath);
        Assert.DoesNotContain("## From yesterday", todayContent);
        Assert.DoesNotContain("Unfinished task", todayContent);

        var tasks = service.GetTodayTasks();
        Assert.IsEmpty(tasks);
    }

    [TestMethod]
    public async Task Test_RolloverNowAsync_ManualTrigger_AndDeduplication()
    {
        var (service, _, _) = CreateService(rolloverEnabled: true);

        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime yesterday = DateTime.Today.AddDays(-1);
        string yesterdayFile = Path.Combine(dailyDir, $"{yesterday:yyyy-MM-dd}.md");
        File.WriteAllLines(yesterdayFile, new[]
        {
            $"# {yesterday:yyyy-MM-dd}",
            "- [ ] Yesterday open task",
            "- [x] Done item"
        });

        // Pre-create today's note WITHOUT rollover tasks
        string todayFile = Path.Combine(dailyDir, $"{DateTime.Today:yyyy-MM-dd}.md");
        File.WriteAllLines(todayFile, new[]
        {
            $"# {DateTime.Today:yyyy-MM-dd}",
            "- [ ] Today existing task"
        });

        Assert.IsTrue(service.HasPendingRolloverTasks());

        // Manual rollover
        bool rolled = await service.RolloverNowAsync();
        Assert.IsTrue(rolled);

        var tasks = service.GetTodayTasks();
        Assert.HasCount(2, tasks);
        Assert.IsTrue(tasks.Any(t => t.Text == "Today existing task"));
        Assert.IsTrue(tasks.Any(t => t.Text == "Yesterday open task"));

        // Second manual rollover should be a no-op since it's already rolled over
        Assert.IsFalse(service.HasPendingRolloverTasks());
        bool rolledAgain = await service.RolloverNowAsync();
        Assert.IsFalse(rolledAgain);
    }

    [TestMethod]
    public void Test_Rollover_PicksMostRecentWithin7Days_AndIgnoresBeyond7Days()
    {
        var (service, _, _) = CreateService(rolloverEnabled: true);

        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        // Seed a note 4 days ago
        DateTime fourDaysAgo = DateTime.Today.AddDays(-4);
        string file4 = Path.Combine(dailyDir, $"{fourDaysAgo:yyyy-MM-dd}.md");
        File.WriteAllLines(file4, new[]
        {
            $"# {fourDaysAgo:yyyy-MM-dd}",
            "- [ ] Task from 4 days ago"
        });

        var candidates = service.GetRolloverCandidates(DateTime.Today);
        Assert.HasCount(1, candidates);
        Assert.AreEqual("Task from 4 days ago", candidates[0].Text);

        // Delete that and seed only 8 days ago
        File.Delete(file4);
        DateTime eightDaysAgo = DateTime.Today.AddDays(-8);
        string file8 = Path.Combine(dailyDir, $"{eightDaysAgo:yyyy-MM-dd}.md");
        File.WriteAllLines(file8, new[]
        {
            $"# {eightDaysAgo:yyyy-MM-dd}",
            "- [ ] Task from 8 days ago"
        });

        var candidatesBeyond = service.GetRolloverCandidates(DateTime.Today);
        Assert.IsEmpty(candidatesBeyond);
    }

    [TestMethod]
    public void Test_Rollover_ScansPastAllDoneDays_ToFindOpenTask()
    {
        var (service, _, _) = CreateService(rolloverEnabled: true);

        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime yesterday = DateTime.Today.AddDays(-1);
        DateTime dayBeforeYesterday = DateTime.Today.AddDays(-2);

        string yesterdayFile = Path.Combine(dailyDir, $"{yesterday:yyyy-MM-dd}.md");
        string dayBeforeFile = Path.Combine(dailyDir, $"{dayBeforeYesterday:yyyy-MM-dd}.md");

        // Yesterday has only completed tasks
        File.WriteAllLines(yesterdayFile, new[]
        {
            $"# {yesterday:yyyy-MM-dd}",
            "- [x] All done yesterday",
            "- [X] Another completed yesterday"
        });

        // Day before yesterday has an open task
        File.WriteAllLines(dayBeforeFile, new[]
        {
            $"# {dayBeforeYesterday:yyyy-MM-dd}",
            "- [ ] Day-before open task",
            "- [x] Day-before completed task"
        });

        // Rollover candidates must skip yesterday (all-done) and pick day-before's open task
        var candidates = service.GetRolloverCandidates(DateTime.Today);
        Assert.HasCount(1, candidates);
        Assert.AreEqual("Day-before open task", candidates[0].Text);

        // Ensuring today's note rolls day-before's task into today
        string? todayPath = service.EnsureDailyNoteFileExists(DateTime.Today);
        Assert.IsNotNull(todayPath);
        string todayContent = File.ReadAllText(todayPath);
        StringAssert.Contains(todayContent, "## From yesterday");
        StringAssert.Contains(todayContent, "- [ ] Day-before open task");
        Assert.DoesNotContain("All done yesterday", todayContent);
    }

    [TestMethod]
    public void Test_Rollover_BothYesterdayAndDayBeforeEmpty_YieldsNothing()
    {
        var (service, _, _) = CreateService(rolloverEnabled: true);

        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime yesterday = DateTime.Today.AddDays(-1);
        DateTime dayBeforeYesterday = DateTime.Today.AddDays(-2);

        string yesterdayFile = Path.Combine(dailyDir, $"{yesterday:yyyy-MM-dd}.md");
        string dayBeforeFile = Path.Combine(dailyDir, $"{dayBeforeYesterday:yyyy-MM-dd}.md");

        // Both days have only completed tasks
        File.WriteAllLines(yesterdayFile, new[]
        {
            $"# {yesterday:yyyy-MM-dd}",
            "- [x] Completed task 1"
        });

        File.WriteAllLines(dayBeforeFile, new[]
        {
            $"# {dayBeforeYesterday:yyyy-MM-dd}",
            "- [x] Completed task 2"
        });

        var candidates = service.GetRolloverCandidates(DateTime.Today);
        Assert.IsEmpty(candidates);

        string? todayPath = service.EnsureDailyNoteFileExists(DateTime.Today);
        Assert.IsNotNull(todayPath);
        string todayContent = File.ReadAllText(todayPath);
        Assert.DoesNotContain("## From yesterday", todayContent);
    }
}
