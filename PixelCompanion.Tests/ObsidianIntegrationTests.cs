using System.IO;
using System.Text;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

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
    public void TaskParser_MetadataPreservation_TogglesCheckboxOnly()
    {
        var parser = new ObsidianTaskParser();
        string rawLine = "- [ ] Deploy server #urgent 10:00";
        var tasks = parser.ParseTasks(new[] { rawLine });
        Assert.HasCount(1, tasks);
        Assert.IsFalse(tasks[0].IsCompleted);

        var toggledLine = parser.BuildToggledLine(tasks[0], true);
        Assert.AreEqual("- [x] Deploy server #urgent 10:00", toggledLine);

        // Toggle back to incomplete
        tasks[0].RawLine = toggledLine;
        tasks[0].IsCompleted = true;
        var untoggledLine = parser.BuildToggledLine(tasks[0], false);
        Assert.AreEqual("- [ ] Deploy server #urgent 10:00", untoggledLine);
    }

    [TestMethod]
    public void TaskParser_NestedTasks_PreservesIndentation()
    {
        var parser = new ObsidianTaskParser();
        var lines = new[]
        {
            "- [ ] Root task",
            "  - [ ] Child task 2-space",
            "    - [x] Grandchild task 4-space"
        };

        var tasks = parser.ParseTasks(lines);
        Assert.HasCount(3, tasks);
        Assert.AreEqual("", tasks[0].Indent);
        Assert.AreEqual("  ", tasks[1].Indent);
        Assert.AreEqual("    ", tasks[2].Indent);
        Assert.IsTrue(tasks[2].IsCompleted);

        var toggledChild = parser.BuildToggledLine(tasks[1], true);
        Assert.AreEqual("  - [x] Child task 2-space", toggledChild);
    }

    [TestMethod]
    public void TaskParser_BuildsNewTaskLine()
    {
        var parser = new ObsidianTaskParser();
        var newLine = parser.BuildNewTaskLine("Record video demo");
        Assert.AreEqual("- [ ] Record video demo", newLine);
    }

    [TestMethod]
    public async Task ObsidianService_DuplicateTaskIdentity_TogglesExactTargetLine()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "ObsidianTestVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var service = new ObsidianService(configService, config);
            var notePath = service.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            var initialContent = "# Tasks\n\n- [ ] Ship Phase 2\n- [ ] Ship Phase 2\n";
            await File.WriteAllTextAsync(notePath, initialContent);

            var tasks = service.GetTodayTasks();
            Assert.HasCount(2, tasks);
            Assert.AreEqual("Ship Phase 2", tasks[0].Text);
            Assert.AreEqual("Ship Phase 2", tasks[1].Text);
            Assert.AreEqual(0, tasks[0].OccurrenceIndex);
            Assert.AreEqual(1, tasks[1].OccurrenceIndex);

            // Toggle the SECOND task
            var toggleSuccess = await service.SetTaskCompletionAsync(tasks[1], true);
            Assert.IsTrue(toggleSuccess);

            var linesAfterToggle = await File.ReadAllLinesAsync(notePath);
            int firstTaskLineIndex = -1;
            int secondTaskLineIndex = -1;
            for (int i = 0; i < linesAfterToggle.Length; i++)
            {
                if (linesAfterToggle[i].Contains("Ship Phase 2"))
                {
                    if (firstTaskLineIndex == -1)
                    {
                        firstTaskLineIndex = i;
                    }
                    else
                    {
                        secondTaskLineIndex = i;
                    }
                }
            }

            Assert.AreNotEqual(-1, firstTaskLineIndex);
            Assert.AreNotEqual(-1, secondTaskLineIndex);
            Assert.AreEqual("- [ ] Ship Phase 2", linesAfterToggle[firstTaskLineIndex]);
            Assert.AreEqual("- [x] Ship Phase 2", linesAfterToggle[secondTaskLineIndex]);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_AddsAndDeletesTasksInVault()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "ObsidianTestVault_" + Guid.NewGuid().ToString("N"));
        var dailyNotesDir = Path.Combine(tempVault, "Task-Manager");

        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
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
                try { Directory.Delete(tempVault, true); } catch { }
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
                DailyNotesFolder = "Task-Manager",
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
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void DateFormatHelper_FallbackToDefaultOnInvalidFormat()
    {
        // Valid format
        bool valid = DateFormatHelper.TryFormatDate("yyyy-MM-dd", new DateTime(2026, 9, 7), out string validResult);
        Assert.IsTrue(valid);
        Assert.AreEqual("2026-09-07", validResult);

        // Invalid format strings (single % or single backslash throws FormatException in .NET DateTime.ToString)
        bool invalid1 = DateFormatHelper.TryFormatDate("%", new DateTime(2026, 9, 7), out string fallbackResult1);
        Assert.IsFalse(invalid1);
        Assert.AreEqual("2026-09-07", fallbackResult1);

        bool invalid2 = DateFormatHelper.TryFormatDate(null, new DateTime(2026, 9, 7), out string fallbackResult2);
        Assert.IsFalse(invalid2);
        Assert.AreEqual("2026-09-07", fallbackResult2);
    }

    [TestMethod]
    public void ConfigService_TaskMangerMigration_Behaviors()
    {
        var configService = new ConfigService();
        var tempVault = Path.Combine(Path.GetTempPath(), "MigrationTestVault_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);

        try
        {
            // Case A: Neither old nor new directory exists -> migrates to Task-Manager
            var configA = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manger"
            };
            bool migratedA = configService.MigrateDailyNotesFolder(configA);
            Assert.IsTrue(migratedA);
            Assert.AreEqual("Task-Manager", configA.DailyNotesFolder);

            // Case B: New directory exists -> migrates to Task-Manager
            string newDirPath = Path.Combine(tempVault, "Task-Manager");
            Directory.CreateDirectory(newDirPath);
            var configB = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manger"
            };
            bool migratedB = configService.MigrateDailyNotesFolder(configB);
            Assert.IsTrue(migratedB);
            Assert.AreEqual("Task-Manager", configB.DailyNotesFolder);
            Directory.Delete(newDirPath);

            // Case C: Old directory exists, new directory does NOT exist -> preserves legacy Task-Manger
            string oldDirPath = Path.Combine(tempVault, "Task-Manger");
            Directory.CreateDirectory(oldDirPath);
            var configC = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manger"
            };
            bool migratedC = configService.MigrateDailyNotesFolder(configC);
            Assert.IsFalse(migratedC);
            Assert.AreEqual("Task-Manger", configC.DailyNotesFolder);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void ConfigService_ValidateWindowBounds_CorrectsOffscreenAndInvalidBounds()
    {
        var configService = new ConfigService();

        // Very negative coordinates
        var bounds1 = configService.ValidateWindowBounds(-99999, -99999, 480, 420);
        Assert.IsGreaterThanOrEqualTo(0.0, bounds1.Left);
        Assert.IsGreaterThanOrEqualTo(0.0, bounds1.Top);
        Assert.AreEqual(480, bounds1.Width);
        Assert.AreEqual(420, bounds1.Height);

        // Huge coordinates far off-screen
        var bounds2 = configService.ValidateWindowBounds(99999, 99999, 480, 420);
        Assert.IsLessThanOrEqualTo(System.Windows.SystemParameters.VirtualScreenWidth, bounds2.Left);
        Assert.IsLessThanOrEqualTo(System.Windows.SystemParameters.VirtualScreenHeight, bounds2.Top);

        // Custom default width & height for PaperWindow
        var paperBounds = configService.ValidateWindowBounds(null, null, null, null, defaultWidth: 380, defaultHeight: 520, minWidth: 280, minHeight: 360);
        Assert.AreEqual(380, paperBounds.Width);
        Assert.AreEqual(520, paperBounds.Height);
    }

    [TestMethod]
    public async Task SingleFireAdd_GuardsAgainstDuplicateTaskCreation()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "SingleFireTestVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var viewModel = new CompanionViewModel(configService, obsidianService, config);

            viewModel.IsAddingTask = true;
            viewModel.NewTaskText = "Single Fire Test Task";

            // First add succeeds
            bool firstResult = await viewModel.AddTaskAsync();
            Assert.IsTrue(firstResult);
            Assert.AreEqual(string.Empty, viewModel.NewTaskText);
            Assert.IsFalse(viewModel.IsAddingTask);

            // Subsequent immediate add without text returns false and does not add duplicate
            bool secondResult = await viewModel.AddTaskAsync();
            Assert.IsFalse(secondResult);

            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(1, tasks);
            Assert.AreEqual("Single Fire Test Task", tasks[0].Text);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void TestVaultIsolation_NeverTouchesRealUserVault()
    {
        var config = new AppConfig();
        // Fresh install must have null/empty ObsidianVaultPath
        Assert.IsNull(config.ObsidianVaultPath);
        Assert.AreEqual("Task-Manager", config.DailyNotesFolder);
    }

    [TestMethod]
    public async Task ObsidianService_SurfaceWriteFailure_WhenFileIsLocked()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "WriteFailureVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            var viewModel = new CompanionViewModel(configService, obsidianService, config);

            // Add an initial task so we can attempt to toggle it
            await obsidianService.AddTaskAsync("Locked File Test");
            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(1, tasks);

            // Lock the daily note exclusively with FileShare.None so replacing/writing will fail deterministically on Windows
            using (var lockStream = new FileStream(notePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Attempting to toggle task completion while file is locked
                bool toggleResult = await viewModel.ToggleTaskAsync(tasks[0]);
                Assert.IsFalse(toggleResult);

                // Verify ObsidianService surfaced error status and friendly status message
                Assert.AreEqual(VaultStatus.Error, obsidianService.Status);
                Assert.AreEqual("Couldn't save your task to the daily note.", obsidianService.StatusMessage);

                // Verify CompanionViewModel also received the status message
                Assert.AreEqual("Couldn't save your task to the daily note.", viewModel.StatusMessage);
            }
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_PreservesUtf8Bom_WhenPresent()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "BomTestVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            // 1. Newly created daily note should be UTF-8 without BOM
            byte[] initialBytes = await File.ReadAllBytesAsync(notePath);
            bool hasInitialBom = initialBytes.Length >= 3 && initialBytes[0] == 0xEF && initialBytes[1] == 0xBB && initialBytes[2] == 0xBF;
            Assert.IsFalse(hasInitialBom, "Newly created daily note should use standard UTF-8 without BOM");

            // 2. Explicitly write content with UTF-8 BOM
            var utf8BomEncoding = new UTF8Encoding(true);
            await File.WriteAllTextAsync(notePath, "# Notes with BOM\n\n- [ ] Task with BOM\n", utf8BomEncoding);

            byte[] writtenBytes = await File.ReadAllBytesAsync(notePath);
            bool writtenHasBom = writtenBytes.Length >= 3 && writtenBytes[0] == 0xEF && writtenBytes[1] == 0xBB && writtenBytes[2] == 0xBF;
            Assert.IsTrue(writtenHasBom, "Setup file must have UTF-8 BOM");

            // 3. Toggle task completion via service
            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(1, tasks);
            bool toggleSuccess = await obsidianService.SetTaskCompletionAsync(tasks[0], true);
            Assert.IsTrue(toggleSuccess);

            // 4. Verify modified file preserved the UTF-8 BOM
            byte[] afterToggleBytes = await File.ReadAllBytesAsync(notePath);
            bool afterToggleHasBom = afterToggleBytes.Length >= 3 && afterToggleBytes[0] == 0xEF && afterToggleBytes[1] == 0xBB && afterToggleBytes[2] == 0xBF;
            Assert.IsTrue(afterToggleHasBom, "Modified file must preserve the existing UTF-8 BOM");
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public void ObsidianTaskParser_BuildUpdatedTextLine_PreservesIndentAndMarker()
    {
        var parser = new ObsidianTaskParser();
        var task = new ObsidianTask
        {
            RawLine = "  * [ ] remind me to go to meeting @16:30",
            Indent = "  ",
            ListMarker = "*",
            IsCompleted = false,
            Text = "remind me to go to meeting @16:30"
        };

        string updated = parser.BuildUpdatedTextLine(task, "new meeting @17:45");
        Assert.AreEqual("  * [ ] new meeting @17:45", updated);

        task.IsCompleted = true;
        task.RawLine = "  * [x] remind me to go to meeting @16:30";
        string updatedCompleted = parser.BuildUpdatedTextLine(task, "new meeting @17:45");
        Assert.AreEqual("  * [x] new meeting @17:45", updatedCompleted);
    }

    [TestMethod]
    public async Task ObsidianService_UpdateTaskTextAsync_UpdatesFileContentAndRecalculatesDueTime()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "UpdateTaskVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            // Add original task with due time @16:30
            await obsidianService.AddTaskAsync("remind me to go to meeting @16:30");
            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(1, tasks);
            var task = tasks[0];
            Assert.AreEqual(new TimeSpan(16, 30, 0), task.DueTime);

            // Update to new meeting @17:45
            bool updateResult = await obsidianService.UpdateTaskTextAsync(task, "new meeting @17:45");
            Assert.IsTrue(updateResult);

            // Verify task object in memory updated
            Assert.AreEqual("new meeting @17:45", task.Text);
            Assert.AreEqual(new TimeSpan(17, 45, 0), task.DueTime);

            // Verify persisted markdown content
            string fileContent = await File.ReadAllTextAsync(notePath);
            StringAssert.Contains(fileContent, "- [ ] new meeting @17:45");
            Assert.DoesNotContain("16:30", fileContent);

            // Update again to remove due time
            bool updateNoTime = await obsidianService.UpdateTaskTextAsync(task, "just a regular meeting");
            Assert.IsTrue(updateNoTime);
            Assert.IsNull(task.DueTime);
            Assert.AreEqual("just a regular meeting", task.Text);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task CompanionViewModel_InlineEditWorkflow_SavesAndCancelsCorrectly()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "VmEditVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            obsidianService.EnsureDailyNoteFileExists();
            var viewModel = new CompanionViewModel(configService, obsidianService, config);

            await obsidianService.AddTaskAsync("remind me to go to meeting @16:30");
            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(1, tasks);
            var task = tasks[0];

            // 1. Start Edit
            viewModel.StartEditTask(task);
            Assert.IsTrue(task.IsEditing);
            Assert.AreEqual("remind me to go to meeting @16:30", task.EditText);

            // 2. Cancel Edit
            task.EditText = "some unfinished draft";
            viewModel.CancelEditTask(task);
            Assert.IsFalse(task.IsEditing);
            Assert.AreEqual("remind me to go to meeting @16:30", task.Text);
            Assert.AreEqual("remind me to go to meeting @16:30", task.EditText);

            // 3. Start Edit and Save
            viewModel.StartEditTask(task);
            task.EditText = "new meeting @17:45";
            bool saved = await viewModel.SaveEditTaskAsync(task);
            Assert.IsTrue(saved);
            Assert.IsFalse(task.IsEditing);

            var updatedTasks = obsidianService.GetTodayTasks();
            Assert.HasCount(1, updatedTasks);
            Assert.AreEqual("new meeting @17:45", updatedTasks[0].Text);
            Assert.AreEqual(new TimeSpan(17, 45, 0), updatedTasks[0].DueTime);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_UpdateTaskTextAsync_UpdatesRawLine_SubsequentToggleHitsSameLine()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "ToggleAfterEditVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            await obsidianService.AddTaskAsync("First task");
            await obsidianService.AddTaskAsync("Second task @10:00");
            await obsidianService.AddTaskAsync("Third task");

            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(3, tasks);
            var task2 = tasks[1];
            Assert.AreEqual("Second task @10:00", task2.Text);

            // Edit Task 2
            bool editResult = await obsidianService.UpdateTaskTextAsync(task2, "Renamed second task @11:00");
            Assert.IsTrue(editResult);
            Assert.AreEqual("Renamed second task @11:00", task2.Text);
            Assert.AreEqual("- [ ] Renamed second task @11:00", task2.RawLine);

            // Subsequent toggle on task2 hits the same line via ResolveTargetIndex
            bool toggleResult = await obsidianService.SetTaskCompletionAsync(task2, true);
            Assert.IsTrue(toggleResult);
            Assert.IsTrue(task2.IsCompleted);

            // Verify file content has task2 completed and tasks 1 & 3 untouched
            string fileContent = await File.ReadAllTextAsync(notePath);
            StringAssert.Contains(fileContent, "- [ ] First task");
            StringAssert.Contains(fileContent, "- [x] Renamed second task @11:00");
            StringAssert.Contains(fileContent, "- [ ] Third task");
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_UpdateTaskTextAsync_RecomputesDueTime_WhenAlarmAddedRemovedChanged()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "DueTimeRecomputeVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            await obsidianService.AddTaskAsync("Meeting with team");
            var tasks = obsidianService.GetTodayTasks();
            var task = tasks[0];
            Assert.IsNull(task.DueTime);

            // 1. Add ⏰
            await obsidianService.UpdateTaskTextAsync(task, "Meeting with team ⏰ 14:15");
            Assert.AreEqual(new TimeSpan(14, 15, 0), task.DueTime);

            // 2. Change ⏰
            await obsidianService.UpdateTaskTextAsync(task, "Meeting with team ⏰ 16:30");
            Assert.AreEqual(new TimeSpan(16, 30, 0), task.DueTime);

            // 3. Change to @
            await obsidianService.UpdateTaskTextAsync(task, "Meeting with team @18:00");
            Assert.AreEqual(new TimeSpan(18, 0, 0), task.DueTime);

            // 4. Remove due time
            await obsidianService.UpdateTaskTextAsync(task, "Meeting with team finished");
            Assert.IsNull(task.DueTime);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_UpdateTaskTextAsync_EmptyOrWhitespaceEdit_RejectedWithoutTouchingFile()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "EmptyEditVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            await obsidianService.AddTaskAsync("Original task");
            var tasks = obsidianService.GetTodayTasks();
            var task = tasks[0];

            string contentBefore = await File.ReadAllTextAsync(notePath);

            // Empty string edit rejected
            bool emptyResult = await obsidianService.UpdateTaskTextAsync(task, string.Empty);
            Assert.IsFalse(emptyResult);

            // Whitespace string edit rejected
            bool whitespaceResult = await obsidianService.UpdateTaskTextAsync(task, "    ");
            Assert.IsFalse(whitespaceResult);

            // Verify task untouched
            Assert.AreEqual("Original task", task.Text);

            // Verify file content completely unchanged
            string contentAfter = await File.ReadAllTextAsync(notePath);
            Assert.AreEqual(contentBefore, contentAfter);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_UpdateTaskTextAsync_OnPastDate_WritesThatDateFileOnly()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "PastDateEditVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var todayPath = obsidianService.EnsureDailyNoteFileExists(DateTime.Today);
            Assert.IsNotNull(todayPath);
            await obsidianService.AddTaskAsync("Today task", DateTime.Today);

            DateTime yesterday = DateTime.Today.AddDays(-1);
            var yesterdayPath = obsidianService.EnsureDailyNoteFileExists(yesterday);
            Assert.IsNotNull(yesterdayPath);
            await obsidianService.AddTaskAsync("Yesterday task @09:00", yesterday);

            var yesterdayTasks = obsidianService.GetTasksForDate(yesterday);
            Assert.HasCount(1, yesterdayTasks);
            var yesterdayTask = yesterdayTasks[0];

            // Edit yesterday's task on past date
            bool updateResult = await obsidianService.UpdateTaskTextAsync(yesterdayTask, "Yesterday revised task @10:30", yesterday);
            Assert.IsTrue(updateResult);

            // Verify yesterday's file has updated text
            string yesterdayContent = await File.ReadAllTextAsync(yesterdayPath);
            StringAssert.Contains(yesterdayContent, "Yesterday revised task @10:30");
            Assert.DoesNotContain("Yesterday task @09:00", yesterdayContent);

            // Verify today's file was NOT modified and still contains today task
            string todayContent = await File.ReadAllTextAsync(todayPath);
            StringAssert.Contains(todayContent, "Today task");
            Assert.DoesNotContain("Yesterday", todayContent);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task ObsidianService_UpdateTaskTextAsync_PreservesCheckboxStateAndIndent()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "PreserveIndentVault_" + Guid.NewGuid().ToString("N"));
        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd"
            };

            using var obsidianService = new ObsidianService(configService, config);
            var notePath = obsidianService.EnsureDailyNoteFileExists();
            Assert.IsNotNull(notePath);

            // Write custom indented completed and uncompleted tasks
            await File.WriteAllTextAsync(notePath, "# Notes\n\n  * [x] Completed indented item\n    - [ ] Nested open item\n");

            var tasks = obsidianService.GetTodayTasks();
            Assert.HasCount(2, tasks);

            var completedTask = tasks[0];
            Assert.IsTrue(completedTask.IsCompleted);
            Assert.AreEqual("  ", completedTask.Indent);
            Assert.AreEqual("*", completedTask.ListMarker);

            // Edit completed indented item
            bool edit1 = await obsidianService.UpdateTaskTextAsync(completedTask, "Updated completed item");
            Assert.IsTrue(edit1);
            Assert.IsTrue(completedTask.IsCompleted);
            Assert.AreEqual("  * [x] Updated completed item", completedTask.RawLine);

            var nestedTask = tasks[1];
            Assert.IsFalse(nestedTask.IsCompleted);
            Assert.AreEqual("    ", nestedTask.Indent);
            Assert.AreEqual("-", nestedTask.ListMarker);

            // Edit nested open item
            bool edit2 = await obsidianService.UpdateTaskTextAsync(nestedTask, "Updated nested open item");
            Assert.IsTrue(edit2);
            Assert.IsFalse(nestedTask.IsCompleted);
            Assert.AreEqual("    - [ ] Updated nested open item", nestedTask.RawLine);

            string fileContent = await File.ReadAllTextAsync(notePath);
            StringAssert.Contains(fileContent, "  * [x] Updated completed item");
            StringAssert.Contains(fileContent, "    - [ ] Updated nested open item");
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        }
    }
}
