using Microsoft.Win32;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
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
    public void Test_SetStartup_EnableAndDisable_ModifiesRegistryProperly()
    {
        // 1. Ensure clean state
        StartupService.SetStartup(false);
        Assert.IsFalse(StartupService.IsStartupEnabled());

        // 2. Enable startup
        bool enabled = StartupService.SetStartup(true);
        Assert.IsTrue(enabled, "SetStartup(true) should succeed");
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
}
