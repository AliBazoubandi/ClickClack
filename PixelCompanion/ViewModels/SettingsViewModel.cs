using System.Windows.Forms;
using System.Windows.Input;
using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly ObsidianService _obsidianService;
    private readonly AppConfig _config;
    private readonly Action _closeAction;

    private string _vaultPath;
    private string _dailyNotesFolder;
    private string _dailyNoteDateFormat;

    public SettingsViewModel(
        ConfigService configService,
        ObsidianService obsidianService,
        AppConfig config,
        Action closeAction)
    {
        _configService = configService;
        _obsidianService = obsidianService;
        _config = config;
        _closeAction = closeAction;

        _vaultPath = string.IsNullOrWhiteSpace(config.ObsidianVaultPath) ? @"E:\obsidian\work" : config.ObsidianVaultPath;
        _dailyNotesFolder = string.IsNullOrWhiteSpace(config.DailyNotesFolder) ? "Task-Manger" : config.DailyNotesFolder;
        _dailyNoteDateFormat = string.IsNullOrWhiteSpace(config.DailyNoteDateFormat)
            ? "yyyy-MM-dd"
            : config.DailyNoteDateFormat;

        BrowseVaultCommand = new RelayCommand(OnBrowseVault);
        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(_closeAction);
    }

    public string VaultPath
    {
        get => _vaultPath;
        set => SetProperty(ref _vaultPath, value);
    }

    public string DailyNotesFolder
    {
        get => _dailyNotesFolder;
        set => SetProperty(ref _dailyNotesFolder, value);
    }

    public string DailyNoteDateFormat
    {
        get => _dailyNoteDateFormat;
        set
        {
            if (SetProperty(ref _dailyNoteDateFormat, value))
            {
                OnPropertyChanged(nameof(PreviewFileName));
            }
        }
    }

    public string PreviewFileName
    {
        get
        {
            try
            {
                var format = string.IsNullOrWhiteSpace(_dailyNoteDateFormat) ? "yyyy-MM-dd" : _dailyNoteDateFormat;
                return $"{DateTime.Now.ToString(format)}.md";
            }
            catch
            {
                return "Invalid date format";
            }
        }
    }

    public ICommand BrowseVaultCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private void OnBrowseVault()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select your Obsidian Vault Root Folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(_vaultPath) && System.IO.Directory.Exists(_vaultPath))
        {
            dialog.InitialDirectory = _vaultPath;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            VaultPath = dialog.SelectedPath;
        }
    }

    private void OnSave()
    {
        _config.ObsidianVaultPath = _vaultPath.Trim();
        _config.DailyNotesFolder = _dailyNotesFolder.Trim();
        _config.DailyNoteDateFormat = string.IsNullOrWhiteSpace(_dailyNoteDateFormat) ? "yyyy-MM-dd" : _dailyNoteDateFormat.Trim();

        _configService.Save(_config);
        _obsidianService.SetupWatcher();

        _closeAction();
    }
}
