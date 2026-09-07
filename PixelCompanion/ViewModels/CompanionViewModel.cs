using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.ViewModels;

public class CompanionViewModel : ViewModelBase
{
    private static readonly string TwIdle = "pack://application:,,,/ClickClack;component/Assets/Typewriter/idle.png";
    private static readonly string TwPress = "pack://application:,,,/ClickClack;component/Assets/Typewriter/press.png";

    private static readonly string PetIdle = "pack://application:,,,/ClickClack;component/Assets/Character/idle.png";
    private static readonly string PetIdle2 = "pack://application:,,,/ClickClack;component/Assets/Character/idle2.png";
    private static readonly string PetCurious = "pack://application:,,,/ClickClack;component/Assets/Character/curious.png";
    private static readonly string PetHappy = "pack://application:,,,/ClickClack;component/Assets/Character/happy.png";
    private static readonly string PetSleep = "pack://application:,,,/ClickClack;component/Assets/Character/sleep.png";
    private static readonly string PetCelebrate = "pack://application:,,,/ClickClack;component/Assets/Character/celebrate.png";

    private readonly ConfigService _configService;
    private readonly ObsidianService _obsidianService;
    private readonly AppConfig _config;

    private string _typewriterImage = TwIdle;
    private string _companionImage = PetIdle;
    private bool _isAlwaysOnTop;
    private bool _isPaperExtended;
    private bool _isAnimating;
    private string _statusMessage = string.Empty;
    private bool _hasTasks;
    private string _newTaskText = string.Empty;
    private bool _isAddingTask;

    private readonly DispatcherTimer _animTimer;
    private int _idleTickCount;
    private int _specialActionTick;
    private string? _temporaryPetState;
    private int _temporaryStateTicksRemaining;
    private readonly Random _random = new();

    public ObservableCollection<ObsidianTask> Tasks { get; } = new();

    public CompanionViewModel(ConfigService configService, ObsidianService obsidianService, AppConfig config)
    {
        _configService = configService;
        _obsidianService = obsidianService;
        _config = config;
        _isAlwaysOnTop = config.AlwaysOnTop;
        _isPaperExtended = config.IsPaperExtended;

        // Typewriter body is in front of the paper, displaying idle state
        _typewriterImage = TwIdle;

        if (_isPaperExtended)
        {
            _companionImage = PetHappy;
        }

        TypewriterClickCommand = new RelayCommand(OnTypewriterClicked);
        PetClickCommand = new RelayCommand(OnPetClicked);
        ToggleTaskCommand = new RelayCommand<ObsidianTask>(OnToggleTask);
        AddTaskCommand = new RelayCommand(OnAddTask);
        DeleteTaskCommand = new RelayCommand<ObsidianTask>(OnDeleteTask);
        OpenAddTaskCommand = new RelayCommand(() => IsAddingTask = true);
        CloseAddTaskCommand = new RelayCommand(() => { IsAddingTask = false; NewTaskText = string.Empty; });

        _obsidianService.TasksChanged += OnVaultTasksChanged;

        _specialActionTick = _random.Next(20, 45);

        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _animTimer.Tick += OnAnimTimerTick;
        _animTimer.Start();

        RefreshTasks();
    }

    public string TypewriterImage
    {
        get => _typewriterImage;
        set => SetProperty(ref _typewriterImage, value);
    }

    public string CompanionImage
    {
        get => _companionImage;
        set => SetProperty(ref _companionImage, value);
    }

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (SetProperty(ref _isAlwaysOnTop, value))
            {
                _config.AlwaysOnTop = value;
                _configService.Save(_config);
            }
        }
    }

    public bool IsPaperExtended
    {
        get => _isPaperExtended;
        set
        {
            if (SetProperty(ref _isPaperExtended, value))
            {
                _config.IsPaperExtended = value;
                _configService.Save(_config);
            }
        }
    }

    public bool IsAddingTask
    {
        get => _isAddingTask;
        set => SetProperty(ref _isAddingTask, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasTasks
    {
        get => _hasTasks;
        set => SetProperty(ref _hasTasks, value);
    }

    public string NewTaskText
    {
        get => _newTaskText;
        set => SetProperty(ref _newTaskText, value);
    }

    public ICommand TypewriterClickCommand { get; }
    public ICommand PetClickCommand { get; }
    public ICommand ToggleTaskCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand OpenAddTaskCommand { get; }
    public ICommand CloseAddTaskCommand { get; }

    public void RefreshTasks()
    {
        var rawTasks = _obsidianService.GetTodayTasks();

        void ApplyTasks()
        {
            Tasks.Clear();
            foreach (var task in rawTasks)
            {
                Tasks.Add(task);
            }

            HasTasks = Tasks.Count > 0;
            StatusMessage = _obsidianService.StatusMessage;
        }

        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null)
        {
            if (app.Dispatcher.CheckAccess())
            {
                ApplyTasks();
            }
            else
            {
                app.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)ApplyTasks);
            }
        }
        else
        {
            ApplyTasks();
        }
    }

    private void OnVaultTasksChanged()
    {
        RefreshTasks();
    }

    private void OnAnimTimerTick(object? sender, EventArgs e)
    {
        if (_isAnimating)
        {
            return;
        }

        if (_temporaryStateTicksRemaining > 0)
        {
            _temporaryStateTicksRemaining--;
            if (_temporaryPetState != null)
            {
                CompanionImage = _temporaryPetState;
                return;
            }
        }
        else
        {
            _temporaryPetState = null;
        }

        _idleTickCount++;

        if (_isPaperExtended)
        {
            CompanionImage = (_idleTickCount % 8 >= 4) ? PetHappy : PetIdle;
            return;
        }

        bool isBreathing = (_idleTickCount % 8) >= 4;
        string currentPose = isBreathing ? PetIdle2 : PetIdle;

        if (_idleTickCount >= _specialActionTick)
        {
            int actionType = _random.Next(3);
            if (actionType == 0)
            {
                _temporaryPetState = PetCurious;
                _temporaryStateTicksRemaining = 3;
            }
            else if (actionType == 1)
            {
                _temporaryPetState = PetSleep;
                _temporaryStateTicksRemaining = 8;
            }

            _specialActionTick = _idleTickCount + _random.Next(25, 60);
            return;
        }

        CompanionImage = currentPose;
    }

    public async void OnTypewriterClicked()
    {
        try
        {
            await OnTypewriterClickedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnTypewriterClicked error: {ex.Message}");
        }
    }

    public async Task OnTypewriterClickedAsync()
    {
        if (_isAnimating)
        {
            return;
        }

        _isAnimating = true;

        try
        {
            if (!_isPaperExtended)
            {
                TypewriterImage = TwPress;
                CompanionImage = PetCurious;
                await Task.Delay(100);

                TypewriterImage = TwIdle;
                CompanionImage = PetHappy;
                IsPaperExtended = true;
                IsAddingTask = false;
                RefreshTasks();

                await Task.Delay(80);
            }
            else
            {
                IsPaperExtended = false;
                IsAddingTask = false;

                TypewriterImage = TwPress;
                CompanionImage = PetCurious;
                await Task.Delay(100);

                TypewriterImage = TwIdle;
                CompanionImage = PetIdle;
                await Task.Delay(80);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnTypewriterClickedAsync error: {ex.Message}");
        }
        finally
        {
            _isAnimating = false;
        }
    }

    public void OnPetClicked()
    {
        _temporaryPetState = PetCelebrate;
        _temporaryStateTicksRemaining = 4;
        CompanionImage = PetCelebrate;
    }

    public async Task<bool> ToggleTaskAsync(ObsidianTask? task)
    {
        if (task == null)
        {
            return false;
        }

        try
        {
            bool targetState = !task.IsCompleted;
            bool success = await _obsidianService.SetTaskCompletionAsync(task, targetState);

            if (success)
            {
                if (targetState)
                {
                    // Only celebrate when completing a task
                    _temporaryPetState = PetCelebrate;
                    _temporaryStateTicksRemaining = 5;
                    CompanionImage = PetCelebrate;
                }
                RefreshTasks();
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleTaskAsync error: {ex.Message}");
        }

        return false;
    }

    private async void OnToggleTask(ObsidianTask? task)
    {
        try
        {
            await ToggleTaskAsync(task);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnToggleTask error: {ex.Message}");
        }
    }

    public async Task<bool> AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskText))
        {
            IsAddingTask = false;
            return false;
        }

        string textToAdd = NewTaskText.Trim();

        try
        {
            bool success = await _obsidianService.AddTaskAsync(textToAdd);
            if (success)
            {
                NewTaskText = string.Empty;
                IsAddingTask = false;
                RefreshTasks();

                // Tactile keypress feedback
                TypewriterImage = TwPress;
                await Task.Delay(80);
                TypewriterImage = TwIdle;
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddTaskAsync error: {ex.Message}");
        }

        return false;
    }

    private async void OnAddTask()
    {
        try
        {
            await AddTaskAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAddTask error: {ex.Message}");
        }
    }

    public async Task<bool> DeleteTaskAsync(ObsidianTask? task)
    {
        if (task == null)
        {
            return false;
        }

        try
        {
            bool success = await _obsidianService.DeleteTaskAsync(task);
            if (success)
            {
                RefreshTasks();
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteTaskAsync error: {ex.Message}");
        }

        return false;
    }

    private async void OnDeleteTask(ObsidianTask? task)
    {
        try
        {
            await DeleteTaskAsync(task);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnDeleteTask error: {ex.Message}");
        }
    }
}
