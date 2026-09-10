using System.IO;
using System.Text.Json;
using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class ConfigMigrationTests
{
    private string _testRoot = null!;
    private string _oldDir = null!;
    private string _newDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ClickClackConfigTests_" + Guid.NewGuid().ToString("N"));
        _oldDir = Path.Combine(_testRoot, "PixelCompanion");
        _newDir = Path.Combine(_testRoot, "ClickClack");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    [TestMethod]
    public void Test_Fresh_NoDirs_LoadsDefaultConfig()
    {
        // Arrange: neither old nor new directory exists
        Assert.IsFalse(Directory.Exists(_oldDir));
        Assert.IsFalse(Directory.Exists(_newDir));

        var service = new ConfigService(_newDir, _oldDir);

        // Act
        var config = service.Load();

        // Assert
        Assert.IsNotNull(config);
        Assert.IsFalse(File.Exists(service.ConfigFilePath));
        Assert.IsFalse(File.Exists(Path.Combine(_oldDir, "config.json")));
        Assert.AreEqual(service.ConfigDirectory, _newDir);
    }

    [TestMethod]
    public void Test_Migrate_OldOnly_MovesFileAndDeletesOld()
    {
        // Arrange: old directory has config.json, new directory does not exist
        Directory.CreateDirectory(_oldDir);
        var oldConfig = new AppConfig
        {
            ObsidianVaultPath = @"C:\TestVault",
            StartWithWindows = true,
            WindowWidth = 550
        };
        var oldConfigPath = Path.Combine(_oldDir, "config.json");
        File.WriteAllText(oldConfigPath, JsonSerializer.Serialize(oldConfig, new JsonSerializerOptions { WriteIndented = true }));

        Assert.IsTrue(File.Exists(oldConfigPath));
        Assert.IsFalse(Directory.Exists(_newDir));

        var service = new ConfigService(_newDir, _oldDir);

        // Act
        var loadedConfig = service.Load();

        // Assert: config.json migrated to new directory and deleted from old directory
        Assert.IsFalse(File.Exists(oldConfigPath), "Old config file must be deleted upon successful migration.");
        Assert.IsTrue(File.Exists(service.ConfigFilePath), "New config file must exist in new directory.");
        Assert.AreEqual(@"C:\TestVault", loadedConfig.ObsidianVaultPath);
        Assert.IsTrue(loadedConfig.StartWithWindows);
        Assert.AreEqual(550, loadedConfig.WindowWidth);
    }

    [TestMethod]
    public void Test_Keep_NewAlreadyExists_OldUntouched()
    {
        // Arrange: both old and new directories have config.json
        Directory.CreateDirectory(_oldDir);
        Directory.CreateDirectory(_newDir);

        var oldConfig = new AppConfig
        {
            ObsidianVaultPath = @"C:\OldVault",
            WindowWidth = 400
        };
        var newConfig = new AppConfig
        {
            ObsidianVaultPath = @"C:\NewVault",
            WindowWidth = 600
        };

        var oldConfigPath = Path.Combine(_oldDir, "config.json");
        var newConfigPath = Path.Combine(_newDir, "config.json");

        File.WriteAllText(oldConfigPath, JsonSerializer.Serialize(oldConfig, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(newConfigPath, JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true }));

        var oldContentBefore = File.ReadAllText(oldConfigPath);

        var service = new ConfigService(_newDir, _oldDir);

        // Act
        var loadedConfig = service.Load();

        // Assert: new config loaded, old config untouched
        Assert.IsTrue(File.Exists(oldConfigPath), "Old config file must remain untouched when new config already exists.");
        Assert.AreEqual(oldContentBefore, File.ReadAllText(oldConfigPath), "Old config content must not be modified.");
        Assert.AreEqual(@"C:\NewVault", loadedConfig.ObsidianVaultPath);
        Assert.AreEqual(600, loadedConfig.WindowWidth);
    }
}
