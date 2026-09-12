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
    private bool _startWithWindows;
    private bool _rolloverEnabled;
    private bool _remindersEnabled;
    private int _reminderMinutesBefore;
    private bool _soundEnabled;

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

        _vaultPath = config.ObsidianVaultPath ?? string.Empty;
        _dailyNotesFolder = string.IsNullOrWhiteSpace(config.DailyNotesFolder) ? "Task-Manager" : config.DailyNotesFolder;
        _dailyNoteDateFormat = string.IsNullOrWhiteSpace(config.DailyNoteDateFormat)
            ? DateFormatHelper.DefaultDateFormat
            : config.DailyNoteDateFormat;
        _startWithWindows = config.StartWithWindows || StartupService.IsStartupEnabled();
        _rolloverEnabled = config.RolloverEnabled;
        _remindersEnabled = config.RemindersEnabled;
        _reminderMinutesBefore = Math.Clamp(config.ReminderMinutesBefore, 0, 120);
        _soundEnabled = config.SoundEnabled;

        BrowseVaultCommand = new RelayCommand(OnBrowseVault);
        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(_closeAction);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool RolloverEnabled
    {
        get => _rolloverEnabled;
        set => SetProperty(ref _rolloverEnabled, value);
    }

    public bool RemindersEnabled
    {
        get => _remindersEnabled;
        set => SetProperty(ref _remindersEnabled, value);
    }

    public int ReminderMinutesBefore
    {
        get => _reminderMinutesBefore;
        set => SetProperty(ref _reminderMinutesBefore, Math.Clamp(value, 0, 120));
    }

    public bool SoundEnabled
    {
        get => _soundEnabled;
        set => SetProperty(ref _soundEnabled, value);
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
            if (DateFormatHelper.TryFormatDate(_dailyNoteDateFormat, DateTime.Now, out var formatted))
            {
                return $"{formatted}.md";
            }
            return $"Invalid date format (falls back to {DateFormatHelper.DefaultDateFormat})";
        }
    }

    public ICommand BrowseVaultCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private void OnBrowseVault()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select your Obsidian Vault or any folder to store tasks",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
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
        if (_reminderMinutesBefore < 0 || _reminderMinutesBefore > 120)
        {
            return;
        }

        _config.ObsidianVaultPath = string.IsNullOrWhiteSpace(_vaultPath) ? null : _vaultPath.Trim();
        _config.DailyNotesFolder = string.IsNullOrWhiteSpace(_dailyNotesFolder) ? "Task-Manager" : _dailyNotesFolder.Trim();

        if (DateFormatHelper.IsValidDateFormat(_dailyNoteDateFormat))
        {
            _config.DailyNoteDateFormat = _dailyNoteDateFormat.Trim();
        }
        else
        {
            // Do not persist invalid format, keep previous valid value or fallback to yyyy-MM-dd
            if (!DateFormatHelper.IsValidDateFormat(_config.DailyNoteDateFormat))
            {
                _config.DailyNoteDateFormat = DateFormatHelper.DefaultDateFormat;
            }
        }

        if (_startWithWindows)
        {
            if (!StartupService.SetStartup(true))
            {
                _startWithWindows = false;
                OnPropertyChanged(nameof(StartWithWindows));
            }
        }
        else
        {
            StartupService.SetStartup(false);
        }

        _config.StartWithWindows = _startWithWindows;
        _config.RolloverEnabled = _rolloverEnabled;
        _config.RemindersEnabled = _remindersEnabled;
        _config.ReminderMinutesBefore = Math.Clamp(_reminderMinutesBefore, 0, 120);
        _config.SoundEnabled = _soundEnabled;
        SoundService.Enabled = _soundEnabled;

        _configService.Save(_config);
        _obsidianService.SetupWatcher();

        _closeAction();
    }
}
