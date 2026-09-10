using System.IO;
using System.Text.Json;
using System.Windows;
using PixelCompanion.Models;

namespace PixelCompanion.Services;

public struct WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class ConfigService
{
    public static readonly string DefaultOldConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelCompanion");

    public static readonly string DefaultConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClickClack");

    private readonly string _configDirectory;
    private readonly string _oldConfigDirectory;
    private readonly string _configFilePath;
    private readonly string _oldConfigFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ConfigService() : this(DefaultConfigDirectory, DefaultOldConfigDirectory)
    {
    }

    public ConfigService(string configDirectory, string? oldConfigDirectory = null)
    {
        _configDirectory = configDirectory;
        _oldConfigDirectory = oldConfigDirectory ?? DefaultOldConfigDirectory;
        _configFilePath = Path.Combine(_configDirectory, "config.json");
        _oldConfigFilePath = Path.Combine(_oldConfigDirectory, "config.json");
    }

    public string ConfigDirectory => _configDirectory;
    public string ConfigFilePath => _configFilePath;

    public bool MigrateLegacyConfigDirectory()
    {
        try
        {
            if (!File.Exists(_configFilePath) && File.Exists(_oldConfigFilePath))
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                }

                File.Copy(_oldConfigFilePath, _configFilePath, overwrite: false);
                if (File.Exists(_configFilePath))
                {
                    try
                    {
                        File.Delete(_oldConfigFilePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete old config file: {ex.Message}");
                    }
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to migrate legacy config directory: {ex.Message}");
        }

        return false;
    }

    public AppConfig Load()
    {
        try
        {
            MigrateLegacyConfigDirectory();

            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                {
                    if (MigrateDailyNotesFolder(config))
                    {
                        Save(config);
                    }
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
        }

        return new AppConfig();
    }

    public bool MigrateDailyNotesFolder(AppConfig config)
    {
        if (string.Equals(config.DailyNotesFolder, "Task-Manger", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(config.ObsidianVaultPath) && Directory.Exists(config.ObsidianVaultPath))
            {
                string oldPath = Path.Combine(config.ObsidianVaultPath, config.DailyNotesFolder);
                string newPath = Path.Combine(config.ObsidianVaultPath, "Task-Manager");

                bool oldExists = Directory.Exists(oldPath);
                bool newExists = Directory.Exists(newPath);

                if (newExists)
                {
                    // Case B: New directory already exists
                    config.DailyNotesFolder = "Task-Manager";
                    return true;
                }
                else if (oldExists)
                {
                    // Case C: Legacy data must be preserved
                    return false;
                }
                else
                {
                    // Case A: Neither exists
                    config.DailyNotesFolder = "Task-Manager";
                    return true;
                }
            }
            else
            {
                // Case A: Vault path not set or directory missing
                config.DailyNotesFolder = "Task-Manager";
                return true;
            }
        }

        return false;
    }

    public void Save(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    public WindowBounds ValidateWindowBounds(
        double? savedLeft,
        double? savedTop,
        double? savedWidth,
        double? savedHeight,
        double defaultWidth = 480,
        double defaultHeight = 420,
        double minWidth = 260,
        double minHeight = 200)
    {
        var workArea = SystemParameters.WorkArea;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        // Size clamping
        double maxWidth = Math.Max(minWidth, workArea.Width);
        double maxHeight = Math.Max(minHeight, workArea.Height);

        double width = savedWidth ?? defaultWidth;
        double height = savedHeight ?? defaultHeight;

        width = Math.Clamp(width, minWidth, maxWidth);
        height = Math.Clamp(height, minHeight, maxHeight);

        // Position clamping
        double left;
        double top;

        if (savedLeft.HasValue && savedTop.HasValue)
        {
            left = savedLeft.Value;
            top = savedTop.Value;

            bool isVisible = left >= virtualLeft - width + 60 &&
                             left <= virtualLeft + virtualWidth - 60 &&
                             top >= virtualTop &&
                             top <= virtualTop + virtualHeight - 60;

            if (!isVisible)
            {
                left = Math.Max(20, workArea.Right - width - 30);
                top = Math.Max(20, workArea.Bottom - height - 30);
            }
        }
        else
        {
            left = Math.Max(20, workArea.Right - width - 30);
            top = Math.Max(20, workArea.Bottom - height - 30);
        }

        return new WindowBounds
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height
        };
    }
}
