using System.Windows;
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

    public PaperWindow(ConfigService configService, AppConfig config, CompanionViewModel viewModel)
    {
        InitializeComponent();

        _configService = configService;
        _config = config;
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += PaperWindow_Loaded;
        Closing += PaperWindow_Closing;
        LocationChanged += PaperWindow_LocationChanged;
        KeyDown += PaperWindow_KeyDown;
    }

    private void PaperWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_config.PaperWindowLeft.HasValue && _config.PaperWindowTop.HasValue)
        {
            Left = _config.PaperWindowLeft.Value;
            Top = _config.PaperWindowTop.Value;
        }
        else
        {
            // Position near center-right of screen
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2 + 100;
            Top = workArea.Top + (workArea.Height - Height) / 2 - 50;
        }
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

    public bool AllowClose { get; set; }

    private void PaperWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
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

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            if (_viewModel.IsAddingTask && !IsChildOf(e.OriginalSource as DependencyObject, PaperTaskInput))
            {
                CommitAndFinishAddingTask();
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

    private void PaperSheet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsAddingTask && !IsChildOf(e.OriginalSource as DependencyObject, PaperTaskInput))
        {
            CommitAndFinishAddingTask();
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

    private void PaperTaskInput_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitAndFinishAddingTask();
    }

    private void CommitAndFinishAddingTask()
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.NewTaskText))
        {
            if (_viewModel.AddTaskCommand.CanExecute(null))
            {
                _viewModel.AddTaskCommand.Execute(null);
            }
        }
        _viewModel.NewTaskText = string.Empty;
        _viewModel.IsAddingTask = false;
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

    private void PaperTaskInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitAndFinishAddingTask();
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
}
