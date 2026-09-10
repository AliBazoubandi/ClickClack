namespace PixelCompanion.Models;

public class AppConfig
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; } = 480;
    public double? WindowHeight { get; set; } = 420;
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string? ObsidianVaultPath { get; set; } = null;
    public string DailyNotesFolder { get; set; } = "Task-Manager";
    public string DailyNoteDateFormat { get; set; } = "yyyy-MM-dd";
    public bool IsPaperExtended { get; set; } = false;
    public double? PaperWindowLeft { get; set; }
    public double? PaperWindowTop { get; set; }
    public double? PaperWindowWidth { get; set; } = 380;
    public double? PaperWindowHeight { get; set; } = 520;
}
