using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PixelCompanion.ViewModels;
using Application = System.Windows.Application;

namespace PixelCompanion.Services;

public class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CompanionViewModel _viewModel;
    private readonly Action _showAction;
    private readonly Action _hideAction;
    private readonly Action _openSettingsAction;
    private readonly Action _exitAction;
    private readonly Action? _openPaperAction;
    private ToolStripMenuItem? _alwaysOnTopMenuItem;

    public TrayService(
        CompanionViewModel viewModel,
        Action showAction,
        Action hideAction,
        Action openSettingsAction,
        Action exitAction,
        Action? openPaperAction = null)
    {
        _viewModel = viewModel;
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

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
