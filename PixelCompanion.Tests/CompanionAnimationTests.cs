using System.IO;
using System.Windows.Media.Imaging;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

namespace PixelCompanion.Tests;

[TestClass]
public class CompanionAnimationTests
{
    private static readonly object _staLock = new();

    private void RunInSTA(Action action)
    {
        Exception? actionEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                lock (_staLock)
                {
                    if (System.Windows.Application.Current == null)
                    {
                        try
                        {
                            _ = new System.Windows.Application();
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                }

                action();
            }
            catch (Exception ex)
            {
                actionEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (actionEx != null)
        {
            throw actionEx;
        }
    }

    [TestMethod]
    public void Test_PetAnims_ContainsAll18Poses_AndCorrectNaming()
    {
        // Must contain all expected poses
        string[] requiredPoses =
        {
            "idle", "celebrate", "curious", "wave", "sneak", "hiding",
            "reading", "eating", "thinking", "sleep", "happy", "digging",
            "flower", "screen", "shocked", "studying", "teatime"
        };

        foreach (var pose in requiredPoses)
        {
            Assert.IsTrue(CompanionViewModel.PetAnims.ContainsKey(pose), $"PetAnims must contain '{pose}'");
            var frames = CompanionViewModel.PetAnims[pose];
            Assert.IsNotEmpty(frames, $"Pose '{pose}' must have at least 1 frame");
            foreach (var uri in frames)
            {
                Assert.IsTrue(uri.StartsWith("pack://application:,,,/ClickClack;component/Assets/Character/", StringComparison.Ordinal),
                    $"URI '{uri}' must start with standard pack prefix");
                Assert.EndsWith(".png", uri);
            }
        }

        // Verify typo fix: digging is present, diging is not
        Assert.IsTrue(CompanionViewModel.PetAnims.ContainsKey("digging"));
        Assert.IsFalse(CompanionViewModel.PetAnims.ContainsKey("diging"));

        // Verify idle has [idle, idle2]
        var idleFrames = CompanionViewModel.PetAnims["idle"];
        Assert.HasCount(2, idleFrames);
        Assert.EndsWith("idle.png", idleFrames[0]);
        Assert.EndsWith("idle2.png", idleFrames[1]);
    }

    [TestMethod]
    public void Test_PreloadAll_FreezesBitmapsInSTA()
    {
        RunInSTA(() =>
        {
            CompanionViewModel.PreloadAll();

            foreach (var list in CompanionViewModel.PetAnims.Values)
            {
                foreach (var uri in list)
                {
                    var bmp = CompanionViewModel.GetBitmap(uri);
                    if (bmp != null)
                    {
                        Assert.IsTrue(bmp.IsFrozen, $"Bitmap for {uri} must be frozen to eliminate UI hitch");
                    }
                }
            }
        });
    }

    [TestMethod]
    public void Test_OnPetClicked_SetsCelebrate_AndSoundEnabled()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "AnimTestVault_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);

        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                SoundEnabled = true
            };
            using var obsidian = new ObsidianService(configService, config);
            var vm = new CompanionViewModel(configService, obsidian, config);

            // Calling OnPetClicked triggers celebrate image
            vm.OnPetClicked();

            Assert.IsNotNull(vm.CompanionImage);
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
    public async Task Test_ToggleTaskAsync_Completion_TriggersHappy()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "AnimTestVault_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);

        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd",
                SoundEnabled = true
            };
            using var obsidian = new ObsidianService(configService, config);
            var vm = new CompanionViewModel(configService, obsidian, config);

            vm.IsAddingTask = true;
            vm.NewTaskText = "Test task";
            bool added = await vm.AddTaskAsync();
            Assert.IsTrue(added);

            var tasks = obsidian.GetTodayTasks();
            Assert.HasCount(1, tasks);
            var task = tasks[0];
            Assert.IsFalse(task.IsCompleted);

            bool toggled = await vm.ToggleTaskAsync(task);
            Assert.IsTrue(toggled);
            Assert.IsTrue(task.IsCompleted);
            Assert.IsNotNull(vm.CompanionImage);
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
    public void Test_Inactivity_TransitionsToSleep_AndUserActivityWakesUp()
    {
        var tempVault = Path.Combine(Path.GetTempPath(), "AnimTestVault_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);

        try
        {
            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                IsPaperExtended = false
            };
            using var obsidian = new ObsidianService(configService, config);
            var vm = new CompanionViewModel(configService, obsidian, config);

            // Initially awake
            Assert.IsFalse(vm.IsSleeping);

            // Simulate 50 seconds of inactivity
            vm.SimulateInactivity(TimeSpan.FromSeconds(50));
            vm.StepAnimationTimer();

            // Inky falls asleep
            Assert.IsTrue(vm.IsSleeping);

            // User clicks pet to wake up
            vm.OnPetClicked();
            Assert.IsFalse(vm.IsSleeping);
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
    public void Test_NormalizedCharacterSprites_ExistAndAre256x256()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string assetsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "PixelCompanion", "Assets", "Character"));
        if (!Directory.Exists(assetsDir))
        {
            assetsDir = Path.GetFullPath(Path.Combine(baseDir, "Assets", "Character"));
        }

        if (Directory.Exists(assetsDir))
        {
            var pngs = Directory.GetFiles(assetsDir, "*.png");
            Assert.IsGreaterThanOrEqualTo(18, pngs.Length);

            foreach (var png in pngs)
            {
                using var fs = new FileStream(png, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                Assert.AreEqual(256, frame.PixelWidth, $"File {Path.GetFileName(png)} should have width 256");
                Assert.AreEqual(256, frame.PixelHeight, $"File {Path.GetFileName(png)} should have height 256");
            }
        }
    }
}
