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

    private DateTime _selectedDate = DateTime.Today;
    private bool _canRollover;

    private readonly DispatcherTimer _animTimer;
    private readonly DispatcherTimer _reminderTimer;
    private readonly HashSet<string> _notifiedReminderKeys = new(StringComparer.Ordinal);
    private DateTime _lastReminderCheckDate = DateTime.Today;

    private int _idleTickCount;
    private int _specialActionTick;
    private string? _temporaryPetState;
    private int _temporaryStateTicksRemaining;
    private readonly Random _random = new();

    public ObservableCollection<ObsidianTask> Tasks { get; } = new();

    public Action<string, string>? ShowReminderAction { get; set; }

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

        TypewriterClickCommand = new AsyncRelayCommand(OnTypewriterClickedAsync);
        PetClickCommand = new RelayCommand(OnPetClicked);
        ToggleTaskCommand = new AsyncRelayCommand<ObsidianTask>(ToggleTaskAsync);
        AddTaskCommand = new AsyncRelayCommand(async () => await AddTaskAsync());
        DeleteTaskCommand = new AsyncRelayCommand<ObsidianTask>(DeleteTaskAsync);
        OpenAddTaskCommand = new RelayCommand(() => IsAddingTask = true);
        CloseAddTaskCommand = new RelayCommand(() => { IsAddingTask = false; NewTaskText = string.Empty; });

        PrevDayCommand = new RelayCommand(() =>
        {
            if (CanGoPrev)
            {
                SelectedDate = SelectedDate.AddDays(-1);
            }
        }, () => CanGoPrev);

        NextDayCommand = new RelayCommand(() =>
        {
            if (CanGoNext)
            {
                SelectedDate = SelectedDate.AddDays(1);
            }
        }, () => CanGoNext);

        TodayCommand = new RelayCommand(() =>
        {
            SelectedDate = DateTime.Today;
        });

        RolloverCommand = new AsyncRelayCommand(OnRolloverAsync);

        StartEditTaskCommand = new RelayCommand<ObsidianTask>(StartEditTask);
        SaveEditTaskCommand = new AsyncRelayCommand<ObsidianTask>(SaveEditTaskAsync);
        CancelEditTaskCommand = new RelayCommand<ObsidianTask>(CancelEditTask);

        _obsidianService.TasksChanged += OnVaultTasksChanged;

        _specialActionTick = _random.Next(20, 45);

        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _animTimer.Tick += OnAnimTimerTick;
        _animTimer.Start();

        _reminderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _reminderTimer.Tick += (s, e) => CheckReminders(DateTime.Now);
        _reminderTimer.Start();

        RefreshTasks();
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var normalized = value.Date;
            if (SetProperty(ref _selectedDate, normalized))
            {
                OnPropertyChanged(nameof(DayLabel));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoPrev));
                CommandManager.InvalidateRequerySuggested();
                RefreshTasks();
            }
        }
    }

    public bool CanGoPrev => SelectedDate > DateTime.Today.AddDays(-6);

    public bool CanGoNext => SelectedDate < DateTime.Today;

    public string DayLabel
    {
        get
        {
            if (SelectedDate == DateTime.Today) return "TODAY";
            if (SelectedDate == DateTime.Today.AddDays(-1)) return "YESTERDAY";
            return SelectedDate.ToString("ddd, MMM d", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
        }
    }

    public bool CanRollover
    {
        get => _canRollover;
        set => SetProperty(ref _canRollover, value);
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
                SoundService.Play(SoundKind.Slide);
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
    public ICommand PrevDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand RolloverCommand { get; }
    public ICommand StartEditTaskCommand { get; }
    public ICommand SaveEditTaskCommand { get; }
    public ICommand CancelEditTaskCommand { get; }

    public void RefreshTasks()
    {
        var rawTasks = _obsidianService.GetTasksForDate(SelectedDate);

        void ApplyTasks()
        {
            Tasks.Clear();
            foreach (var task in rawTasks)
            {
                Tasks.Add(task);
            }

            HasTasks = Tasks.Count > 0;
            StatusMessage = _obsidianService.StatusMessage;
            CanRollover = _config.RolloverEnabled && SelectedDate == DateTime.Today && _obsidianService.HasPendingRolloverTasks();
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

    private void OnVaultTasksChanged(string? changedPath)
    {
        if (changedPath != null)
        {
            string? currentPath = _obsidianService.PrepareDailyNotePath(SelectedDate);
            if (currentPath != null && !string.Equals(changedPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        RefreshTasks();
    }

    public void CheckReminders(DateTime now)
    {
        if (!_config.RemindersEnabled)
        {
            return;
        }

        if (now.Date != _lastReminderCheckDate)
        {
            _notifiedReminderKeys.Clear();
            _lastReminderCheckDate = now.Date;
        }

        var todayTasks = (SelectedDate.Date == DateTime.Today)
            ? Tasks.ToList()
            : _obsidianService.GetTodayTasks();

        var dueTasks = ReminderService.DueSoon(todayTasks, now, _config.ReminderMinutesBefore);
        foreach (var task in dueTasks)
        {
            string key = ReminderService.GetReminderKey(task, now.Date);
            if (_notifiedReminderKeys.Add(key))
            {
                _temporaryPetState = PetCurious;
                _temporaryStateTicksRemaining = 6;
                CompanionImage = PetCurious;

                string timeSnippet = task.DueTime.HasValue ? $" ({task.DueTime.Value:hh\\:mm})" : string.Empty;
                ShowReminderAction?.Invoke("Task Reminder", $"{task.Text}{timeSnippet}");
            }
        }
    }

    private async Task OnRolloverAsync()
    {
        bool rolled = await _obsidianService.RolloverNowAsync();
        if (rolled)
        {
            RefreshTasks();
            SoundService.Play(SoundKind.Slide);
        }
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
            bool success = await _obsidianService.SetTaskCompletionAsync(task, targetState, SelectedDate);

            if (success)
            {
                if (targetState)
                {
                    // Only celebrate and pop sound when completing a task
                    _temporaryPetState = PetCelebrate;
                    _temporaryStateTicksRemaining = 5;
                    CompanionImage = PetCelebrate;
                    SoundService.Play(SoundKind.Pop);
                }
                RefreshTasks();
                return true;
            }
            else
            {
                StatusMessage = _obsidianService.StatusMessage;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleTaskAsync error: {ex.Message}");
            StatusMessage = _obsidianService.StatusMessage;
        }

        return false;
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
            bool success = await _obsidianService.AddTaskAsync(textToAdd, SelectedDate);
            if (success)
            {
                SoundService.Play(SoundKind.Clack);
                NewTaskText = string.Empty;
                IsAddingTask = false;
                RefreshTasks();

                // Tactile keypress feedback
                TypewriterImage = TwPress;
                await Task.Delay(80);
                TypewriterImage = TwIdle;
                return true;
            }
            else
            {
                StatusMessage = _obsidianService.StatusMessage;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddTaskAsync error: {ex.Message}");
            StatusMessage = _obsidianService.StatusMessage;
        }

        return false;
    }

    public async Task<bool> DeleteTaskAsync(ObsidianTask? task)
    {
        if (task == null)
        {
            return false;
        }

        try
        {
            bool success = await _obsidianService.DeleteTaskAsync(task, SelectedDate);
            if (success)
            {
                RefreshTasks();
                return true;
            }
            else
            {
                StatusMessage = _obsidianService.StatusMessage;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteTaskAsync error: {ex.Message}");
            StatusMessage = _obsidianService.StatusMessage;
        }

        return false;
    }

    public void StartEditTask(ObsidianTask? task)
    {
        if (task == null) return;
        foreach (var t in Tasks)
        {
            if (t.IsEditing && t != task)
            {
                t.IsEditing = false;
            }
        }
        task.EditText = task.Text;
        task.IsEditing = true;
    }

    public async Task<bool> SaveEditTaskAsync(ObsidianTask? task)
    {
        if (task == null) return false;

        string newText = task.EditText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newText))
        {
            task.IsEditing = false;
            return false;
        }

        if (string.Equals(task.Text, newText, StringComparison.Ordinal))
        {
            task.IsEditing = false;
            return true;
        }

        try
        {
            bool success = await _obsidianService.UpdateTaskTextAsync(task, newText, SelectedDate);
            if (success)
            {
                SoundService.Play(SoundKind.Clack);
                task.IsEditing = false;
                RefreshTasks();
                return true;
            }
            else
            {
                StatusMessage = _obsidianService.StatusMessage;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveEditTaskAsync error: {ex.Message}");
            StatusMessage = _obsidianService.StatusMessage;
        }

        return false;
    }

    public void CancelEditTask(ObsidianTask? task)
    {
        if (task == null) return;
        task.EditText = task.Text;
        task.IsEditing = false;
    }
}
