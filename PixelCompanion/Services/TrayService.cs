using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PixelCompanion.Models;
using PixelCompanion.ViewModels;
using Application = System.Windows.Application;

namespace PixelCompanion.Services;

public class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CompanionViewModel _viewModel;
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly Action _showAction;
    private readonly Action _hideAction;
    private readonly Action _openSettingsAction;
    private readonly Action _exitAction;
    private readonly Action? _openPaperAction;
    private ToolStripMenuItem? _alwaysOnTopMenuItem;
    private ToolStripMenuItem? _startupMenuItem;
    private bool _isUpdatingStartupMenu;

    public TrayService(
        CompanionViewModel viewModel,
        ConfigService configService,
        AppConfig config,
        Action showAction,
        Action hideAction,
        Action openSettingsAction,
        Action exitAction,
        Action? openPaperAction = null)
    {
        _viewModel = viewModel;
        _configService = configService;
        _config = config;
        _showAction = showAction;
        _hideAction = hideAction;
        _openSettingsAction = openSettingsAction;
        _exitAction = exitAction;
        _openPaperAction = openPaperAction;

        _notifyIcon = new NotifyIcon
        {
            Text = "ClickClack (Obsidian)",
            Visible = true
        };

        LoadIcon();
        BuildContextMenu();

        _notifyIcon.DoubleClick += (s, e) =>
        {
            _showAction();
        };
    }

    private void LoadIcon()
    {
        try
        {
            // Try extracting icon directly from ClickClack.exe first if running as compiled executable
            if (!string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
            {
                try
                {
                    var exeIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                    if (exeIcon != null)
                    {
                        _notifyIcon.Icon = exeIcon;
                        return;
                    }
                }
                catch
                {
                    // Fall back to resource stream
                }
            }

            var iconUri = new Uri("pack://application:,,,/ClickClack;component/Assets/Icons/app.ico");
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                _notifyIcon.Icon = new Icon(stream);
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load tray icon from resources: {ex.Message}");
        }

        _notifyIcon.Icon = SystemIcons.Application;
    }

    private void BuildContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show ClickClack", null, (s, e) => _showAction());
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);

        var hideItem = new ToolStripMenuItem("Hide", null, (s, e) => _hideAction());

        _alwaysOnTopMenuItem = new ToolStripMenuItem("Always on Top")
        {
            Checked = _viewModel.IsAlwaysOnTop,
            CheckOnClick = true
        };
        _alwaysOnTopMenuItem.CheckedChanged += (s, e) =>
        {
            _viewModel.IsAlwaysOnTop = _alwaysOnTopMenuItem.Checked;
        };

        _startupMenuItem = new ToolStripMenuItem("Launch on Windows Startup")
        {
            Checked = _config.StartWithWindows || StartupService.IsStartupEnabled(),
            CheckOnClick = true
        };
        _startupMenuItem.CheckedChanged += (s, e) =>
        {
            if (_isUpdatingStartupMenu) return;

            bool isChecked = _startupMenuItem.Checked;
            if (isChecked)
            {
                if (!StartupService.SetStartup(true))
                {
                    _isUpdatingStartupMenu = true;
                    _startupMenuItem.Checked = false;
                    _isUpdatingStartupMenu = false;
                    _config.StartWithWindows = false;
                    _configService.Save(_config);
                    return;
                }
            }
            else
            {
                StartupService.SetStartup(false);
            }

            _config.StartWithWindows = _startupMenuItem.Checked;
            _configService.Save(_config);
        };

        var settingsItem = new ToolStripMenuItem("Settings...", null, (s, e) =>
        {
            _openSettingsAction();
        });

        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => _exitAction());

        contextMenu.Items.Add(showItem);
        if (_openPaperAction != null)
        {
            var paperItem = new ToolStripMenuItem("Open Tasks Paper", null, (s, e) => _openPaperAction());
            contextMenu.Items.Add(paperItem);
        }
        contextMenu.Items.Add(hideItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_alwaysOnTopMenuItem);
        contextMenu.Items.Add(_startupMenuItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    public void UpdateAlwaysOnTop(bool isAlwaysOnTop)
    {
        if (_alwaysOnTopMenuItem != null && _alwaysOnTopMenuItem.Checked != isAlwaysOnTop)
        {
            _alwaysOnTopMenuItem.Checked = isAlwaysOnTop;
        }
    }

    public void UpdateStartupState(bool isStartupEnabled)
    {
        if (_startupMenuItem != null && _startupMenuItem.Checked != isStartupEnabled)
        {
            _isUpdatingStartupMenu = true;
            _startupMenuItem.Checked = isStartupEnabled;
            _isUpdatingStartupMenu = false;
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
