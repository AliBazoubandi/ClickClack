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
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelCompanion");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                {
                    // If vault path was empty or old test path, ensure it defaults to E:\obsidian\work
                    if (string.IsNullOrWhiteSpace(config.ObsidianVaultPath) || config.ObsidianVaultPath.Contains("TestVault"))
                    {
                        config.ObsidianVaultPath = @"E:\obsidian\work";
                        config.DailyNotesFolder = "Task-Manger";
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

    public WindowBounds ValidateWindowBounds(double? savedLeft, double? savedTop, double? savedWidth, double? savedHeight)
    {
        var workArea = SystemParameters.WorkArea;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        // Size clamping
        double minWidth = 260;
        double minHeight = 200;
        double maxWidth = Math.Max(minWidth, workArea.Width);
        double maxHeight = Math.Max(minHeight, workArea.Height);

        double width = savedWidth ?? 460;
        double height = savedHeight ?? 360;

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
