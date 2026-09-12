using System.Globalization;
using System.IO;
using PixelCompanion.Converters;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

namespace PixelCompanion.Tests;

[TestClass]
public class SettingsViewModelTests
{
    private string _tempVault = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempVault = Path.Combine(Path.GetTempPath(), $"CC_SettingsTest_{Guid.NewGuid():N}");
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

    [TestMethod]
    public void Test_SettingsViewModel_ReminderMinutesBefore_SetterClamp()
    {
        // Code-level note: Non-numeric input ("abc") is guarded in UI via XAML ReminderMinutesValidationRule,
        // which displays the standard WPF Validation.ErrorTemplate red border and disables the Save button.
        // Full XAML binding validation runs inside the WPF presentation pipeline and is not unit-testable headless;
        // unit tests here verify the VM property clamping (-5→0, 200→120) and the validation rule logic directly.

        var config = new AppConfig
        {
            ObsidianVaultPath = _tempVault,
            ReminderMinutesBefore = 15
        };
        var configService = new ConfigService();
        using var obsidianService = new ObsidianService(configService, config);
        bool closed = false;
        var vm = new SettingsViewModel(configService, obsidianService, config, () => closed = true);

        Assert.AreEqual(15, vm.ReminderMinutesBefore);

        // Test setter clamp: negative value (-5) clamps to 0
        vm.ReminderMinutesBefore = -5;
        Assert.AreEqual(0, vm.ReminderMinutesBefore);

        // Test setter clamp: excessive value (200) clamps to 120
        vm.ReminderMinutesBefore = 200;
        Assert.AreEqual(120, vm.ReminderMinutesBefore);

        // Test normal valid value within range
        vm.ReminderMinutesBefore = 30;
        Assert.AreEqual(30, vm.ReminderMinutesBefore);

        // Verify Save persists clamped value
        vm.SaveCommand.Execute(null);
        Assert.IsTrue(closed);
        Assert.AreEqual(30, config.ReminderMinutesBefore);
    }

    [TestMethod]
    public void Test_ReminderMinutesValidationRule_ValidatesCorrectly()
    {
        var rule = new ReminderMinutesValidationRule();
        var culture = CultureInfo.InvariantCulture;

        // Valid integers in range [0, 120]
        Assert.IsTrue(rule.Validate("0", culture).IsValid);
        Assert.IsTrue(rule.Validate("15", culture).IsValid);
        Assert.IsTrue(rule.Validate("120", culture).IsValid);
        Assert.IsTrue(rule.Validate("  45  ", culture).IsValid);
        Assert.IsTrue(rule.Validate("۱۵", culture).IsValid); // Persian digits 15

        // Non-numeric strings
        Assert.IsFalse(rule.Validate("abc", culture).IsValid);
        Assert.IsFalse(rule.Validate("12a", culture).IsValid);
        Assert.IsFalse(rule.Validate("", culture).IsValid);
        Assert.IsFalse(rule.Validate("   ", culture).IsValid);
        Assert.IsFalse(rule.Validate(null!, culture).IsValid);

        // Out of range integers
        Assert.IsFalse(rule.Validate("-5", culture).IsValid);
        Assert.IsFalse(rule.Validate("121", culture).IsValid);
        Assert.IsFalse(rule.Validate("999", culture).IsValid);
    }
}
