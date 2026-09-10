using System.Windows;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;
using PixelCompanion.Views;

namespace PixelCompanion;

public partial class App : System.Windows.Application
{
    private ConfigService? _configService;
    private ObsidianService? _obsidianService;
    private AppConfig? _config;
    private CompanionViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private TrayService? _trayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _configService = new ConfigService();
        _config = _configService.Load();
        _obsidianService = new ObsidianService(_configService, _config);
        _viewModel = new CompanionViewModel(_configService, _obsidianService, _config);

        _mainWindow = new MainWindow(_configService, _config, _viewModel);

        if (_config.StartWithWindows)
        {
            StartupService.SyncStartup(true);
        }

        _trayService = new TrayService(
            _viewModel,
            _configService,
            _config,
            showAction: () =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
            },
            hideAction: () =>
            {
                _mainWindow?.Hide();
            },
            openSettingsAction: () =>
            {
                if (_configService != null && _obsidianService != null && _config != null)
                {
                    var settingsWin = new SettingsWindow(_configService, _obsidianService, _config);
                    settingsWin.ShowDialog();
                    _viewModel?.RefreshTasks();
                    _trayService?.UpdateStartupState(_config.StartWithWindows);
                }
            },
            exitAction: () =>
            {
                ShutdownApplication();
            },
            openPaperAction: () =>
            {
                _mainWindow?.OpenPaperWindow();
            });

        _mainWindow.Show();
    }

    private void ShutdownApplication()
    {
        _trayService?.Dispose();
        _trayService = null;
        _obsidianService?.Dispose();
        _obsidianService = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        _trayService = null;
        _obsidianService?.Dispose();
        _obsidianService = null;
        base.OnExit(e);
    }
}
