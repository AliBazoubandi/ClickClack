using System.IO;
using Microsoft.Win32;

namespace PixelCompanion.Services;

public static class StartupService
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "ClickClack";

    /// <summary>
    /// Checks whether ClickClack is currently registered in Windows Run registry.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            if (key == null) return false;

            var value = key.GetValue(AppValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read startup registry: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks whether an executable path is a development build (e.g. within a \bin\ directory),
    /// which should be refused from registering as a persistent Windows startup entry.
    /// </summary>
    public static bool IsDevBuildPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // A published directory is a production/standalone deployment, not an IDE/dotnet-run dev build
        if (path.Contains(@"\publish\", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/publish/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Enables or disables automatic startup on Windows boot.
    /// </summary>
    public static bool SetStartup(bool enable, string? customExePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return false;

            if (enable)
            {
                string? exePath = customExePath ?? Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                {
                    if (IsDevBuildPath(exePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Startup registration refused for dev-build path: {exePath}");
                        return false;
                    }

                    // Enclose path in quotes in case directory contains spaces
                    key.SetValue(AppValueName, $"\"{exePath}\"");
                    return true;
                }
                return false;
            }
            else
            {
                if (key.GetValue(AppValueName) != null)
                {
                    key.DeleteValue(AppValueName, false);
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write startup registry: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Synchronizes the registry entry with the current executable path if enabled,
    /// or removes it if disabled. This ensures moving the single-file executable updates the startup target.
    /// </summary>
    public static void SyncStartup(bool shouldBeEnabled, string? customExePath = null)
    {
        if (shouldBeEnabled)
        {
            SetStartup(true, customExePath);
        }
        else if (IsStartupEnabled())
        {
            SetStartup(false);
        }
    }
}
