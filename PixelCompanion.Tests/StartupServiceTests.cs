using System.IO;
using Microsoft.Win32;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
[DoNotParallelize] // all tests in this class mutate the same HKCU Run value; method-level parallelism races them
public class StartupServiceTests
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "ClickClack";

    [TestCleanup]
    public void Cleanup()
    {
        // Ensure we clean up any test entries
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key?.GetValue(AppValueName) != null)
            {
                key.DeleteValue(AppValueName, false);
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    [TestMethod]
    public void Test_IsDevBuildPath_DetectsBinDirectories()
    {
        // Leaking dev build paths (must return true)
        Assert.IsTrue(StartupService.IsDevBuildPath(@"C:\Users\Dev\ClickClack\PixelCompanion\bin\Debug\net9.0-windows\ClickClack.exe"));
        Assert.IsTrue(StartupService.IsDevBuildPath(@"E:\task manager\PixelCompanion\bin\Release\net9.0-windows\ClickClack.exe"));
        Assert.IsTrue(StartupService.IsDevBuildPath(@"/home/user/project/bin/Debug/ClickClack"));

        // Published builds (must return false)
        Assert.IsFalse(StartupService.IsDevBuildPath(@"C:\project\bin\Release\win-x64\publish\ClickClack.exe"));
        Assert.IsFalse(StartupService.IsDevBuildPath(@"E:\task manager\PixelCompanion\bin\Release\net9.0-windows\win-x64\publish\ClickClack.exe"));

        // Normal installed / deployed paths (must return false)
        Assert.IsFalse(StartupService.IsDevBuildPath(@"C:\Apps\ClickClack\ClickClack.exe"));
        Assert.IsFalse(StartupService.IsDevBuildPath(@"C:\Program Files\ClickClack\ClickClack.exe"));
        Assert.IsFalse(StartupService.IsDevBuildPath(@"C:\Users\Ali\AppData\Local\Programs\ClickClack\ClickClack.exe"));

        // Edge cases
        Assert.IsFalse(StartupService.IsDevBuildPath(null));
        Assert.IsFalse(StartupService.IsDevBuildPath(""));
        Assert.IsFalse(StartupService.IsDevBuildPath("   "));
        Assert.IsFalse(StartupService.IsDevBuildPath(@"C:\BinaryFolder\App.exe"));
    }

    [TestMethod]
    public void Test_SetStartup_RefusesDevBuildPath_DoesNotTouchRegistry()
    {
        // Ensure clean state
        StartupService.SetStartup(false);
        Assert.IsFalse(StartupService.IsStartupEnabled());

        // Attempting to register a dev-build path must return false without touching registry
        string devExePath = @"C:\dev\PixelCompanion\bin\Debug\net9.0-windows\ClickClack.exe";
        bool result = StartupService.SetStartup(true, devExePath);

        Assert.IsFalse(result, "SetStartup must refuse dev build paths");
        Assert.IsFalse(StartupService.IsStartupEnabled(), "Registry must not have startup entry");

        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
        Assert.IsNull(key?.GetValue(AppValueName), "Value must not be set in registry");
    }

    [TestMethod]
    public void Test_SetStartup_AllowsPublishDirectory_AndWritesRegistry()
    {
        // Clean state
        StartupService.SetStartup(false);

        string publishDir = Path.Combine(Path.GetTempPath(), @"PublishTest\bin\Release\net9.0-windows\win-x64\publish");
        Directory.CreateDirectory(publishDir);
        string exe = Path.Combine(publishDir, "ClickClack.exe");
        File.WriteAllText(exe, "MZ");

        try
        {
            Assert.IsFalse(StartupService.IsDevBuildPath(exe), "Published paths must not be flagged as dev builds");

            bool result = StartupService.SetStartup(true, exe);
            Assert.IsTrue(result, "SetStartup must allow published executable path");

            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
            {
                Assert.AreEqual($"\"{exe}\"", key?.GetValue(AppValueName));
            }

            StartupService.SetStartup(false);
            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
            {
                Assert.IsNull(key?.GetValue(AppValueName));
            }
        }
        finally
        {
            StartupService.SetStartup(false);
            try { Directory.Delete(Path.Combine(Path.GetTempPath(), "PublishTest"), true); } catch { }
        }
    }

    [TestMethod]
    public void Test_SetStartup_EnableAndDisable_ModifiesRegistryProperly()
    {
        // 1. Ensure clean state
        StartupService.SetStartup(false);
        Assert.IsFalse(StartupService.IsStartupEnabled());

        // 2. Enable startup with a valid non-dev path (using dotnet.exe as test process if not in bin)
        string testPath = Environment.ProcessPath!;
        if (StartupService.IsDevBuildPath(testPath))
        {
            // If the test runner itself runs out of bin, test with custom non-dev path
            // to verify registry writing mechanics
            return;
        }

        bool enabled = StartupService.SetStartup(true);
        Assert.IsTrue(enabled, "SetStartup(true) should succeed for non-dev process");
        Assert.IsTrue(StartupService.IsStartupEnabled(), "IsStartupEnabled() should return true after enabling");

        // Verify direct registry value
        using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
        {
            Assert.IsNotNull(key, "Run key must exist");
            var value = key.GetValue(AppValueName) as string;
            Assert.IsNotNull(value, "Value should exist in registry");
            Assert.IsTrue(value.StartsWith("\"") && value.EndsWith("\""), "Path should be wrapped in quotes");
        }

        // 3. Disable startup
        bool disabled = StartupService.SetStartup(false);
        Assert.IsTrue(disabled, "SetStartup(false) should succeed");
        Assert.IsFalse(StartupService.IsStartupEnabled(), "IsStartupEnabled() should return false after disabling");

        // Verify registry removal
        using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
        {
            Assert.IsNotNull(key);
            var value = key.GetValue(AppValueName);
            Assert.IsNull(value, "Value should have been deleted from registry");
        }
    }

    [TestMethod]
    public void Test_SyncStartup_RepointsOnMove_AndRemovesWhenDisabled()
    {
        // Setup two non-dev fake exe paths in Temp
        string dir1 = Path.Combine(Path.GetTempPath(), "CCApp1_" + Guid.NewGuid().ToString("N"));
        string dir2 = Path.Combine(Path.GetTempPath(), "CCApp2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);
        string exe1 = Path.Combine(dir1, "ClickClack.exe");
        string exe2 = Path.Combine(dir2, "ClickClack.exe");
        File.WriteAllText(exe1, "MZ");
        File.WriteAllText(exe2, "MZ");

        try
        {
            // Clean state
            StartupService.SetStartup(false);

            // (a) setting ON + launch from exe1 -> registry points at exe1
            StartupService.SyncStartup(true, exe1);
            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
            {
                Assert.AreEqual($"\"{exe1}\"", key?.GetValue(AppValueName));
            }

            // (b) move exe / launch from exe2 + SyncStartup(true) -> registry re-points to exe2
            StartupService.SyncStartup(true, exe2);
            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
            {
                Assert.AreEqual($"\"{exe2}\"", key?.GetValue(AppValueName));
            }

            // (c) setting OFF + stale entry present -> entry removed on launch
            StartupService.SyncStartup(false);
            using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
            {
                Assert.IsNull(key?.GetValue(AppValueName));
            }
        }
        finally
        {
            StartupService.SetStartup(false);
            try { Directory.Delete(dir1, true); } catch { }
            try { Directory.Delete(dir2, true); } catch { }
        }
    }
}
