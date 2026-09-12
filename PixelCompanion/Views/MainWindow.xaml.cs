using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

namespace PixelCompanion.Views;

public partial class MainWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly CompanionViewModel _viewModel;
    private PaperWindow? _paperWindow;

    private System.Windows.Point _dragStartPoint;
    private bool _isMouseDown;
    private bool _hasDragged;
    private string? _clickedTarget;

    // Resizing state
    private bool _isResizing;
    private string _resizeEdge = string.Empty;
    private POINT _resizeStartMouseScreen;
    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    public MainWindow(ConfigService configService, AppConfig config, CompanionViewModel viewModel)
    {
        InitializeComponent();

        _configService = configService;
        _config = config;
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var bounds = _configService.ValidateWindowBounds(
            _config.WindowLeft,
            _config.WindowTop,
            _config.WindowWidth,
            _config.WindowHeight);

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        SaveCurrentPosition();
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left >= -1000 && Top >= -1000)
        {
            _config.WindowLeft = Left;
            _config.WindowTop = Top;
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _config.WindowWidth = Width;
            _config.WindowHeight = Height;
            _configService.Save(_config);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveCurrentPosition();
        if (_paperWindow != null)
        {
            _paperWindow.AllowClose = true;
            _paperWindow.Close();
            _paperWindow = null;
        }
    }

    private void SaveCurrentPosition()
    {
        if (WindowState == WindowState.Normal)
        {
            _config.WindowLeft = Left;
            _config.WindowTop = Top;
            _config.WindowWidth = Width;
            _config.WindowHeight = Height;
            _configService.Save(_config);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't initiate drag if clicking inside interactive controls
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _isMouseDown = true;
        _hasDragged = false;
        _dragStartPoint = e.GetPosition(this);
    }

    private bool IsInteractiveElement(DependencyObject? element)
    {
        while (element != null && element != this)
        {
            if (element is System.Windows.Controls.TextBox ||
                element is System.Windows.Controls.Button ||
                element is System.Windows.Controls.CheckBox ||
                element is ScrollViewer)
            {
                return true;
            }
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isMouseDown && e.LeftButton == MouseButtonState.Pressed)
        {
            System.Windows.Point current = e.GetPosition(this);
            Vector diff = current - _dragStartPoint;

            if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4)
            {
                _hasDragged = true;
                _clickedTarget = null;

                try
                {
                    DragMove();
                }
                catch
                {
                }
                finally
                {
                    SaveCurrentPosition();
                }
            }
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMouseDown && !_hasDragged)
        {
            if (_clickedTarget == "Typewriter")
            {
                _viewModel.TypewriterClickCommand.Execute(null);
            }
            else if (_clickedTarget == "Companion")
            {
                _viewModel.OnPetClicked();
            }
        }

        _isMouseDown = false;
        _hasDragged = false;
        _clickedTarget = null;
        SaveCurrentPosition();
    }

    private void Typewriter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _clickedTarget = "Typewriter";
    }

    private void Companion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _clickedTarget = "Companion";
    }

    private void OpenPaperBtn_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenPaperWindow();
    }

    private void PaperCloseBtn_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _viewModel.TypewriterClickCommand.Execute(null);
    }

    public void OpenPaperWindow()
    {
        if (_paperWindow == null)
        {
            _paperWindow = new PaperWindow(_configService, _config, _viewModel);
        }
        _paperWindow.Show();
        _paperWindow.WindowState = WindowState.Normal;
        _paperWindow.Activate();
    }

    private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && sender is FrameworkElement grip && grip.Tag is string tag)
        {
            e.Handled = true;
            _isMouseDown = false;
            _hasDragged = false;
            _clickedTarget = null;

            _isResizing = true;
            _resizeEdge = tag;
            grip.CaptureMouse();

            GetCursorPos(out _resizeStartMouseScreen);
            _resizeStartLeft = Left;
            _resizeStartTop = Top;
            _resizeStartWidth = ActualWidth;
            _resizeStartHeight = ActualHeight;
        }
    }

    private void ResizeGrip_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isResizing && sender is FrameworkElement)
        {
            GetCursorPos(out POINT pt);
            double deltaX = pt.X - _resizeStartMouseScreen.X;
            double deltaY = pt.Y - _resizeStartMouseScreen.Y;

            double newLeft = _resizeStartLeft;
            double newTop = _resizeStartTop;
            double newWidth = _resizeStartWidth;
            double newHeight = _resizeStartHeight;

            double minW = MinWidth > 0 ? MinWidth : 260;
            double minH = MinHeight > 0 ? MinHeight : 200;

            switch (_resizeEdge)
            {
                case "Right":
                    newWidth = Math.Max(minW, _resizeStartWidth + deltaX);
                    break;
                case "Bottom":
                    newHeight = Math.Max(minH, _resizeStartHeight + deltaY);
                    break;
                case "Left":
                    double targetW = _resizeStartWidth - deltaX;
                    if (targetW >= minW)
                    {
                        newWidth = targetW;
                        newLeft = _resizeStartLeft + deltaX;
                    }
                    break;
                case "Top":
                    double targetH = _resizeStartHeight - deltaY;
                    if (targetH >= minH)
                    {
                        newHeight = targetH;
                        newTop = _resizeStartTop + deltaY;
                    }
                    break;
                case "BottomRight":
                    newWidth = Math.Max(minW, _resizeStartWidth + deltaX);
                    newHeight = Math.Max(minH, _resizeStartHeight + deltaY);
                    break;
                case "BottomLeft":
                    double blW = _resizeStartWidth - deltaX;
                    if (blW >= minW)
                    {
                        newWidth = blW;
                        newLeft = _resizeStartLeft + deltaX;
                    }
                    newHeight = Math.Max(minH, _resizeStartHeight + deltaY);
                    break;
                case "TopRight":
                    newWidth = Math.Max(minW, _resizeStartWidth + deltaX);
                    double trH = _resizeStartHeight - deltaY;
                    if (trH >= minH)
                    {
                        newHeight = trH;
                        newTop = _resizeStartTop + deltaY;
                    }
                    break;
                case "TopLeft":
                    double tlW = _resizeStartWidth - deltaX;
                    if (tlW >= minW)
                    {
                        newWidth = tlW;
                        newLeft = _resizeStartLeft + deltaX;
                    }
                    double tlH = _resizeStartHeight - deltaY;
                    if (tlH >= minH)
                    {
                        newHeight = tlH;
                        newTop = _resizeStartTop + deltaY;
                    }
                    break;
            }

            Left = newLeft;
            Top = newTop;
            Width = newWidth;
            Height = newHeight;
        }
    }

    private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing && sender is FrameworkElement grip)
        {
            _isResizing = false;
            _resizeEdge = string.Empty;
            grip.ReleaseMouseCapture();
            SaveCurrentPosition();
        }
    }

    private void TaskRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ObsidianTask task)
        {
            e.Handled = true;
            _viewModel.StartEditTask(task);
        }
    }

    private void EditTaskInput_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private async void EditTaskInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is ObsidianTask task)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await _viewModel.SaveEditTaskAsync(task);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _viewModel.CancelEditTask(task);
            }
        }
    }

    private async void EditTaskInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is ObsidianTask task && task.IsEditing)
        {
            await _viewModel.SaveEditTaskAsync(task);
        }
    }
}
