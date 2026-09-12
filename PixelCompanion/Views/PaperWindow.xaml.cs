using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;

namespace PixelCompanion.Views;

public partial class PaperWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly CompanionViewModel _viewModel;
    private int _isCommitting;

    public PaperWindow(ConfigService configService, AppConfig config, CompanionViewModel viewModel)
    {
        InitializeComponent();

        _configService = configService;
        _config = config;
        _viewModel = viewModel;
        DataContext = _viewModel;

        Topmost = _viewModel.IsAlwaysOnTop;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompanionViewModel.IsAlwaysOnTop))
            {
                Topmost = _viewModel.IsAlwaysOnTop;
            }
        };

        Loaded += PaperWindow_Loaded;
        Closing += PaperWindow_Closing;
        LocationChanged += PaperWindow_LocationChanged;
        SizeChanged += PaperWindow_SizeChanged;
        KeyDown += PaperWindow_KeyDown;
    }

    private void PaperWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var bounds = _configService.ValidateWindowBounds(
            _config.PaperWindowLeft,
            _config.PaperWindowTop,
            _config.PaperWindowWidth,
            _config.PaperWindowHeight,
            defaultWidth: 380,
            defaultHeight: 520,
            minWidth: 280,
            minHeight: 360);

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        SaveCurrentBounds();
    }

    private void PaperWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _config.PaperWindowLeft = Left;
            _config.PaperWindowTop = Top;
            _configService.Save(_config);
        }
    }

    private void PaperWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _config.PaperWindowWidth = Width;
            _config.PaperWindowHeight = Height;
            _configService.Save(_config);
        }
    }

    private void SaveCurrentBounds()
    {
        if (WindowState == WindowState.Normal)
        {
            _config.PaperWindowLeft = Left;
            _config.PaperWindowTop = Top;
            _config.PaperWindowWidth = Width;
            _config.PaperWindowHeight = Height;
            _configService.Save(_config);
        }
    }

    public bool AllowClose { get; set; }

    private void PaperWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveCurrentBounds();
        if (!AllowClose)
        {
            // Cancel close and hide instead, so state is preserved
            e.Cancel = true;
            Hide();
        }
    }

    private void PaperWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_viewModel.IsAddingTask)
            {
                _viewModel.IsAddingTask = false;
            }
            else
            {
                Hide();
            }
            e.Handled = true;
        }
    }

    private async void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            if (_viewModel.IsAddingTask && !IsChildOf(e.OriginalSource as DependencyObject, PaperTaskInput))
            {
                await CommitAndFinishAddingTask();
            }

            try
            {
                DragMove();
            }
            catch
            {
            }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private async void PaperSheet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsAddingTask && !IsChildOf(e.OriginalSource as DependencyObject, PaperTaskInput))
        {
            await CommitAndFinishAddingTask();
        }
    }

    private void ShowAddInputBtn_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _viewModel.IsAddingTask = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            PaperTaskInput?.Focus();
        });
    }

    private async void TasksScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsAddingTask)
        {
            var source = e.OriginalSource as DependencyObject;
            if (!IsChildOf(source, PaperTaskInput))
            {
                await CommitAndFinishAddingTask();
            }
        }
    }

    private async void PaperTaskInput_LostFocus(object sender, RoutedEventArgs e)
    {
        await CommitAndFinishAddingTask();
    }

    public async Task CommitAndFinishAddingTask()
    {
        if (Interlocked.CompareExchange(ref _isCommitting, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_viewModel.NewTaskText))
            {
                _viewModel.NewTaskText = string.Empty;
                _viewModel.IsAddingTask = false;
                return;
            }

            await _viewModel.AddTaskAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CommitAndFinishAddingTask error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isCommitting, 0);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    private async void PaperTaskInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await CommitAndFinishAddingTask();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _viewModel.NewTaskText = string.Empty;
            _viewModel.IsAddingTask = false;
        }
    }

    private bool IsChildOf(DependencyObject? element, DependencyObject? target)
    {
        if (target == null) return false;
        while (element != null && element != this)
        {
            if (element == target) return true;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private bool IsInteractiveElement(DependencyObject? element)
    {
        while (element != null && element != this)
        {
            if (element is System.Windows.Controls.TextBox ||
                element is System.Windows.Controls.Button ||
                element is System.Windows.Controls.CheckBox ||
                element is System.Windows.Controls.Primitives.ScrollBar)
            {
                return true;
            }
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
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
