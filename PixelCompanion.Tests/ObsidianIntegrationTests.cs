using System.IO;
using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class ObsidianIntegrationTests
{
    [TestMethod]
    public void TaskParser_ExtractsTasksCorrectly()
    {
        var parser = new ObsidianTaskParser();
        var markdownLines = new[]
        {
            "# Sunday Notes",
            "## Focus",
            "- [ ] Finish API integration",
            "- [x] Study AI architecture",
            "- [X] Uppercase complete",
            "  - [ ] Nested child task",
            "* [ ] Asterisk marker task",
            "Just a regular bullet without checkbox",
            "A normal paragraph."
        };

        var tasks = parser.ParseTasks(markdownLines);

        Assert.HasCount(5, tasks);

        Assert.AreEqual("Finish API integration", tasks[0].Text);
        Assert.IsFalse(tasks[0].IsCompleted);
        Assert.AreEqual("-", tasks[0].ListMarker);
        Assert.AreEqual("", tasks[0].Indent);

        Assert.AreEqual("Study AI architecture", tasks[1].Text);
        Assert.IsTrue(tasks[1].IsCompleted);

        Assert.AreEqual("Uppercase complete", tasks[2].Text);
        Assert.IsTrue(tasks[2].IsCompleted);

        Assert.AreEqual("Nested child task", tasks[3].Text);
        Assert.IsFalse(tasks[3].IsCompleted);
        Assert.AreEqual("  ", tasks[3].Indent);

        Assert.AreEqual("Asterisk marker task", tasks[4].Text);
        Assert.AreEqual("*", tasks[4].ListMarker);
    }

    [TestMethod]
    public void TaskParser_TogglesLineStatePreservingFormat()
    {
        var parser = new ObsidianTaskParser();
        var task = new ObsidianTask
        {
            LineIndex = 3,
            RawLine = "  - [ ] Deploy server",
            Indent = "  ",
            ListMarker = "-",
            Text = "Deploy server",
            IsCompleted = false
        };

        var toggledOn = parser.BuildToggledLine(task, true);
        Assert.AreEqual("  - [x] Deploy server", toggledOn);

        var toggledOff = parser.BuildToggledLine(task, false);
        Assert.AreEqual("  - [ ] Deploy server", toggledOff);
    }

    [TestMethod]
    public void TaskParser_BuildsNewTaskLine()
    {
        var parser = new ObsidianTaskParser();
        var newLine = parser.BuildNewTaskLine("Record video demo");
        Assert.AreEqual("- [ ] Record video demo", newLine);
    }

    [TestMethod]
    public async Task ObsidianService_AddsAndDeletesTasksInVault()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "ObsidianTestVault_" + Guid.NewGuid().ToString("N"));
        var dailyNotesDir = Path.Combine(tempVault, "Task-Manger");

        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manger",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var service = new ObsidianService(configService, config);
            
            // 1. Auto-creation of directory and daily note
            var notePath = service.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);
            Assert.IsTrue(Directory.Exists(dailyNotesDir));
            Assert.IsTrue(File.Exists(notePath));

            // 2. Add task
            var addSuccess = await service.AddTaskAsync("Ship Phase 2");
            Assert.IsTrue(addSuccess);

            var tasks = service.GetTodayTasks();
            Assert.HasCount(1, tasks);
            Assert.AreEqual("Ship Phase 2", tasks[0].Text);
            Assert.IsFalse(tasks[0].IsCompleted);

            // 3. Toggle completion
            var toggleSuccess = await service.SetTaskCompletionAsync(tasks[0], true);
            Assert.IsTrue(toggleSuccess);
            Assert.IsTrue(tasks[0].IsCompleted);

            var fileText = await File.ReadAllTextAsync(notePath);
            Assert.Contains("- [x] Ship Phase 2", fileText);

            // 4. Toggle back to incomplete
            var untoggleSuccess = await service.SetTaskCompletionAsync(tasks[0], false);
            Assert.IsTrue(untoggleSuccess);
            Assert.IsFalse(tasks[0].IsCompleted);

            var uncheckText = await File.ReadAllTextAsync(notePath);
            Assert.Contains("- [ ] Ship Phase 2", uncheckText);

            // 5. Delete task
            var deleteSuccess = await service.DeleteTaskAsync(tasks[0]);
            Assert.IsTrue(deleteSuccess);

            var remainingTasks = service.GetTodayTasks();
            Assert.IsEmpty(remainingTasks);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_PreservesSurroundingContentWhenTogglingAndDeleting()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "ObsidianTestVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manger",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var service = new ObsidianService(configService, config);
            var notePath = service.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            // Write custom initial content with headers and notes
            var initialContent = "# Daily Log\n\nNotes from standup:\n- Everything going well\n\n## Tasks\n\n";
            await File.WriteAllTextAsync(notePath, initialContent);

            // Add three tasks
            await service.AddTaskAsync("Task Alpha");
            await service.AddTaskAsync("Task Beta");
            await service.AddTaskAsync("Task Gamma");

            var tasks = service.GetTodayTasks();
            Assert.HasCount(3, tasks);

            // Toggle middle task (Task Beta)
            await service.SetTaskCompletionAsync(tasks[1], true);

            // Delete first task (Task Alpha)
            await service.DeleteTaskAsync(tasks[0]);

            var currentTasks = service.GetTodayTasks();
            Assert.HasCount(2, currentTasks);
            Assert.AreEqual("Task Beta", currentTasks[0].Text);
            Assert.IsTrue(currentTasks[0].IsCompleted);
            Assert.AreEqual("Task Gamma", currentTasks[1].Text);
            Assert.IsFalse(currentTasks[1].IsCompleted);

            // Verify header and notes are preserved intact
            var text = await File.ReadAllTextAsync(notePath);
            Assert.Contains("# Daily Log", text);
            Assert.Contains("Notes from standup:", text);
            Assert.Contains("- Everything going well", text);
            Assert.Contains("- [x] Task Beta", text);
            Assert.Contains("- [ ] Task Gamma", text);
            Assert.DoesNotContain("Task Alpha", text);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
        }
    }
}
