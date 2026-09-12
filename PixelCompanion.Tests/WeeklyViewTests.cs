using System.IO;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

namespace PixelCompanion.Tests;

[TestClass]
public class WeeklyViewTests
{
    private string _tempVault = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempVault = Path.Combine(Path.GetTempPath(), $"CC_WeeklyTest_{Guid.NewGuid():N}");
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

    private (ObsidianService service, AppConfig config, ConfigService configService) CreateEnvironment()
    {
        var config = new AppConfig
        {
            ObsidianVaultPath = _tempVault,
            DailyNotesFolder = "Daily",
            DailyNoteDateFormat = "yyyy-MM-dd",
            RolloverEnabled = false
        };

        var configService = new ConfigService();
        var service = new ObsidianService(configService, config);
        return (service, config, configService);
    }

    [TestMethod]
    public void Test_GetTasksForDate_ReturnsDateSpecificTasks()
    {
        var (service, _, _) = CreateEnvironment();
        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime day1 = DateTime.Today;
        DateTime day2 = DateTime.Today.AddDays(-2);
        DateTime day3 = DateTime.Today.AddDays(-5);

        File.WriteAllLines(Path.Combine(dailyDir, $"{day1:yyyy-MM-dd}.md"), new[] { $"# {day1:yyyy-MM-dd}", "- [ ] Today task" });
        File.WriteAllLines(Path.Combine(dailyDir, $"{day2:yyyy-MM-dd}.md"), new[] { $"# {day2:yyyy-MM-dd}", "- [ ] Day 2 task ⏰ 14:00" });
        File.WriteAllLines(Path.Combine(dailyDir, $"{day3:yyyy-MM-dd}.md"), new[] { $"# {day3:yyyy-MM-dd}", "- [x] Day 3 task" });

        var tasks1 = service.GetTasksForDate(day1);
        var tasks2 = service.GetTasksForDate(day2);
        var tasks3 = service.GetTasksForDate(day3);

        Assert.HasCount(1, tasks1);
        Assert.AreEqual("Today task", tasks1[0].Text);

        Assert.HasCount(1, tasks2);
        Assert.AreEqual("Day 2 task ⏰ 14:00", tasks2[0].Text);
        Assert.AreEqual(new TimeSpan(14, 0, 0), tasks2[0].DueTime);

        Assert.HasCount(1, tasks3);
        Assert.AreEqual("Day 3 task", tasks3[0].Text);
        Assert.IsTrue(tasks3[0].IsCompleted);
    }

    [TestMethod]
    public async Task Test_TaskMutations_OnPastDate_DoNotAffectToday()
    {
        var (service, _, _) = CreateEnvironment();
        string dailyDir = Path.Combine(_tempVault, "Daily");
        Directory.CreateDirectory(dailyDir);

        DateTime pastDate = DateTime.Today.AddDays(-3);
        string pastFile = Path.Combine(dailyDir, $"{pastDate:yyyy-MM-dd}.md");
        string todayFile = Path.Combine(dailyDir, $"{DateTime.Today:yyyy-MM-dd}.md");

        File.WriteAllLines(pastFile, new[] { $"# {pastDate:yyyy-MM-dd}", "- [ ] Past open task" });
        File.WriteAllLines(todayFile, new[] { $"# {DateTime.Today:yyyy-MM-dd}", "- [ ] Today task untouched" });

        var pastTasks = service.GetTasksForDate(pastDate);
        Assert.HasCount(1, pastTasks);

        // Toggle past task to completed
        bool toggled = await service.SetTaskCompletionAsync(pastTasks[0], true, pastDate);
        Assert.IsTrue(toggled);

        // Verify past file updated
        string pastContent = File.ReadAllText(pastFile);
        StringAssert.Contains(pastContent, "- [x] Past open task");

        // Verify today file was NOT touched
        string todayContent = File.ReadAllText(todayFile);
        StringAssert.Contains(todayContent, "- [ ] Today task untouched");

        // Add task to past date
        bool added = await service.AddTaskAsync("New past note item", pastDate);
        Assert.IsTrue(added);

        var updatedPastTasks = service.GetTasksForDate(pastDate);
        Assert.HasCount(2, updatedPastTasks);
        Assert.AreEqual("New past note item", updatedPastTasks[1].Text);

        // Delete item from past date
        bool deleted = await service.DeleteTaskAsync(updatedPastTasks[0], pastDate);
        Assert.IsTrue(deleted);

        var finalPastTasks = service.GetTasksForDate(pastDate);
        Assert.HasCount(1, finalPastTasks);
        Assert.AreEqual("New past note item", finalPastTasks[0].Text);
    }

    [TestMethod]
    public void Test_ViewModel_NavigationBounds_AndDayLabel()
    {
        var (service, config, configService) = CreateEnvironment();
        var vm = new CompanionViewModel(configService, service, config);

        // Starts at Today
        Assert.AreEqual(DateTime.Today, vm.SelectedDate);
        Assert.IsFalse(vm.CanGoNext);
        Assert.IsTrue(vm.CanGoPrev);
        Assert.AreEqual("TODAY", vm.DayLabel);

        // Move to Yesterday
        vm.PrevDayCommand.Execute(null);
        Assert.AreEqual(DateTime.Today.AddDays(-1), vm.SelectedDate);
        Assert.IsTrue(vm.CanGoNext);
        Assert.IsTrue(vm.CanGoPrev);
        Assert.AreEqual("YESTERDAY", vm.DayLabel);

        // Move to 2 days ago
        vm.PrevDayCommand.Execute(null);
        Assert.AreEqual(DateTime.Today.AddDays(-2), vm.SelectedDate);
        string expectedLabel = DateTime.Today.AddDays(-2).ToString("ddd, MMM d", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
        Assert.AreEqual(expectedLabel, vm.DayLabel);

        // Move back to 6 days ago (limit)
        while (vm.CanGoPrev)
        {
            vm.PrevDayCommand.Execute(null);
        }

        Assert.AreEqual(DateTime.Today.AddDays(-6), vm.SelectedDate);
        Assert.IsFalse(vm.CanGoPrev);

        // Attempting to go further back should remain at -6 days
        vm.PrevDayCommand.Execute(null);
        Assert.AreEqual(DateTime.Today.AddDays(-6), vm.SelectedDate);

        // Jump back to Today
        vm.TodayCommand.Execute(null);
        Assert.AreEqual(DateTime.Today, vm.SelectedDate);
        Assert.IsFalse(vm.CanGoNext);

        // Attempting to go forward past today is blocked
        vm.NextDayCommand.Execute(null);
        Assert.AreEqual(DateTime.Today, vm.SelectedDate);
    }
}
