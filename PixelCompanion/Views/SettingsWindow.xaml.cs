using System.Windows;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

namespace PixelCompanion.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ConfigService configService, ObsidianService obsidianService, AppConfig config)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(configService, obsidianService, config, Close);
    }
}
