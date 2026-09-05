namespace PixelCompanion.Models;

public class AppConfig
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; } = 460;
    public double? WindowHeight { get; set; } = 360;
    public bool AlwaysOnTop { get; set; } = true;
    public string? ObsidianVaultPath { get; set; } = @"E:\obsidian\work";
    public string DailyNotesFolder { get; set; } = "Task-Manger";
    public string DailyNoteDateFormat { get; set; } = "yyyy-MM-dd";
    public bool IsPaperExtended { get; set; } = false;
    public double? PaperWindowLeft { get; set; }
    public double? PaperWindowTop { get; set; }
    public double? PaperWindowWidth { get; set; } = 380;
    public double? PaperWindowHeight { get; set; } = 520;
}
