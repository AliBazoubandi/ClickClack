using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelCompanion.Models;
using PixelCompanion.Services;

namespace PixelCompanion.ViewModels;

public class CompanionViewModel : ViewModelBase
{
    private static readonly string TwIdle = "pack://application:,,,/ClickClack;component/Assets/Typewriter/idle.png";
    private static readonly string TwPress = "pack://application:,,,/ClickClack;component/Assets/Typewriter/press.png";

    public static readonly Dictionary<string, string[]> PetAnims = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idle"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/idle.png",
            "pack://application:,,,/ClickClack;component/Assets/Character/idle2.png"
        },
        ["celebrate"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/celebrate.png"
        },
        ["curious"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/curious.png"
        },
        ["wave"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/wave.png"
        },
        ["sneak"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/sneak.png"
        },
        ["hiding"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/hiding.png"
        },
        ["reading"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/reading.png"
        },
        ["eating"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/eating.png",
            "pack://application:,,,/ClickClack;component/Assets/Character/eating2.png"
        },
        ["thinking"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/thinking.png"
        },
        ["sleep"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/sleep.png"
        },
        ["happy"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/happy.png"
        },
        ["digging"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/digging.png"
        },
        ["flower"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/flower.png"
        },
        ["screen"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/screen.png"
        },
        ["shocked"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/shocked.png"
        },
        ["studying"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/studying.png"
        },
        ["teatime"] = new[]
        {
            "pack://application:,,,/ClickClack;component/Assets/Character/teatime.png"
        }
    };

    private static readonly string PetIdle = PetAnims["idle"][0];
    private static readonly string PetIdle2 = PetAnims["idle"][1];
    private static readonly string PetCurious = PetAnims["curious"][0];
    private static readonly string PetHappy = PetAnims["happy"][0];
    private static readonly string PetSleep = PetAnims["sleep"][0];
    private static readonly string PetCelebrate = PetAnims["celebrate"][0];
    private static readonly string PetStudying = PetAnims["studying"][0];
    private static readonly string PetEating = PetAnims["eating"][0];
    private static readonly string PetEating2 = PetAnims["eating"][1];

    public static readonly string[] RandomAmbientKeys =
    {
        "reading",
        "eating",
        "thinking",
        "wave",
        "hiding",
        "sneak",
        "studying",
        "teatime",
        "flower",
        "screen",
        "curious",
        "digging",
        "shocked"
    };

    private static readonly ConcurrentDictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    public static void PreloadAll()
    {
        try
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            }

            foreach (var list in PetAnims.Values)
            {
                foreach (var uriStr in list)
                {
                    GetBitmap(uriStr);
                }
            }

            GetBitmap(TwIdle);
            GetBitmap(TwPress);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset preloading note: {ex.Message}");
        }
    }

    public static BitmapImage? GetBitmap(string uriString)
    {
        if (ImageCache.TryGetValue(uriString, out var cached))
        {
            return cached;
        }

        try
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(uriString, UriKind.RelativeOrAbsolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.None;
            bitmap.EndInit();
            bitmap.Freeze();
            ImageCache[uriString] = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bitmap load note for '{uriString}': {ex.Message}");
            return null;
        }
    }

    private readonly ConfigService _configService;
    private readonly ObsidianService _obsidianService;
    private readonly AppConfig _config;

    private string _typewriterImage = TwIdle;
    private ImageSource? _companionImage;
    private ImageSource? _prevCompanionImage;
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

    private enum PetMood
    {
        AwakeIdle,
        Ambient,
        Eating,
        Sleeping
    }

    private PetMood _currentMood = PetMood.AwakeIdle;
    private int _moodTicksRemaining;
    private bool _isSleeping;
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private bool _eatingFrameToggle;
    private string? _paperWatchPose;
    private int _paperWatchTicksRemaining;
    private string? _temporaryPetState;
    private int _temporaryTicksRemaining;
    private readonly Random _random = new();

    internal bool IsSleeping => _isSleeping;

    internal void SimulateInactivity(TimeSpan elapsed)
    {
        _lastActivityTime = DateTime.UtcNow - elapsed;
    }

    internal void StepAnimationTimer()
    {
        OnAnimTimerTick(this, EventArgs.Empty);
    }

    public ObservableCollection<ObsidianTask> Tasks { get; } = new();

    public Action<string, string>? ShowReminderAction { get; set; }
    public Action? ClosePaperAction { get; set; }

    public CompanionViewModel(ConfigService configService, ObsidianService obsidianService, AppConfig config)
    {
        _configService = configService;
        _obsidianService = obsidianService;
        _config = config;
        _isAlwaysOnTop = config.AlwaysOnTop;
        _isPaperExtended = config.IsPaperExtended;

        // Typewriter body is in front of the paper, displaying idle state
        _typewriterImage = TwIdle;

        PreloadAll();

        if (_isPaperExtended)
        {
            _companionImage = GetBitmap(PetHappy);
        }
        else
        {
            _companionImage = GetBitmap(PetIdle);
        }
        _prevCompanionImage = _companionImage;

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
                RegisterUserActivity();
            }
        }, () => CanGoPrev);

        NextDayCommand = new RelayCommand(() =>
        {
            if (CanGoNext)
            {
                SelectedDate = SelectedDate.AddDays(1);
                RegisterUserActivity();
            }
        }, () => CanGoNext);

        TodayCommand = new RelayCommand(() =>
        {
            SelectedDate = DateTime.Today;
            RegisterUserActivity();
        });

        RolloverCommand = new AsyncRelayCommand(OnRolloverAsync);

        StartEditTaskCommand = new RelayCommand<ObsidianTask>(StartEditTask);
        SaveEditTaskCommand = new AsyncRelayCommand<ObsidianTask>(SaveEditTaskAsync);
        CancelEditTaskCommand = new RelayCommand<ObsidianTask>(CancelEditTask);

        _obsidianService.TasksChanged += OnVaultTasksChanged;

        _lastActivityTime = DateTime.UtcNow;
        _currentMood = PetMood.AwakeIdle;
        _moodTicksRemaining = _random.Next(20, 35);

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

    public event Action? PetStateCrossfadeRequested;

    public ImageSource? PrevCompanionImage
    {
        get => _prevCompanionImage;
        set => SetProperty(ref _prevCompanionImage, value);
    }

    public ImageSource? CompanionImage
    {
        get => _companionImage;
        set
        {
            if (Equals(_companionImage, value)) return;
            var old = _companionImage;
            _prevCompanionImage = old;
            OnPropertyChanged(nameof(PrevCompanionImage));
            SetProperty(ref _companionImage, value);
            PetStateCrossfadeRequested?.Invoke();
        }
    }

    public void SetCompanionImage(string uriString)
    {
        var bmp = GetBitmap(uriString);
        if (bmp != null)
        {
            CompanionImage = bmp;
        }
    }

    public void SetPetPose(string poseName, int frameIndex = 0)
    {
        if (PetAnims.TryGetValue(poseName, out var frames) && frames.Length > 0)
        {
            int idx = Math.Clamp(frameIndex, 0, frames.Length - 1);
            SetCompanionImage(frames[idx]);
        }
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
                if (!value)
                {
                    ClosePaperAction?.Invoke();
                }
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
                _temporaryTicksRemaining = 15; // 3.0 seconds
                SetCompanionImage(PetCurious);

                string timeSnippet = task.DueTime.HasValue ? $" ({task.DueTime.Value:hh\\:mm})" : string.Empty;
                ShowReminderAction?.Invoke("Task Reminder", $"{task.Text}{timeSnippet}");
            }
        }
    }

    public void RegisterUserActivity()
    {
        _lastActivityTime = DateTime.UtcNow;
        if (_isSleeping)
        {
            _isSleeping = false;
            _currentMood = PetMood.AwakeIdle;
            _moodTicksRemaining = _random.Next(20, 35);
            SetCompanionImage(PetIdle);
        }
    }

    private async Task OnRolloverAsync()
    {
        RegisterUserActivity();
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

        // 1. Temporary priority states (Task completion -> Happy, Pet clicked -> Celebrate, Reminder -> Curious)
        if (_temporaryTicksRemaining > 0)
        {
            _temporaryTicksRemaining--;
            if (!string.IsNullOrEmpty(_temporaryPetState))
            {
                SetCompanionImage(_temporaryPetState);
                return;
            }
        }
        else
        {
            _temporaryPetState = null;
        }

        // 2. Paper extended state: user is actively writing or checking tasks
        if (_isPaperExtended)
        {
            _lastActivityTime = DateTime.UtcNow;
            _isSleeping = false;

            _paperWatchTicksRemaining--;
            if (_paperWatchTicksRemaining <= 0)
            {
                // Inky stays happily attentive while paper is open
                string[] paperPoses = { PetHappy, PetCurious, PetStudying };
                _paperWatchPose = paperPoses[_random.Next(paperPoses.Length)];
                _paperWatchTicksRemaining = _random.Next(25, 45); // 5 to 9 seconds
            }

            SetCompanionImage(_paperWatchPose ?? PetHappy);
            return;
        }

        // 3. Inactivity Sleep Check
        // If typewriter is closed and user has been idle for >= 45 seconds, Inky sleeps
        if (!_isSleeping && (DateTime.UtcNow - _lastActivityTime).TotalSeconds >= 45)
        {
            _isSleeping = true;
            _currentMood = PetMood.Sleeping;
            SetCompanionImage(PetSleep);
            return;
        }

        if (_isSleeping)
        {
            SetCompanionImage(PetSleep);
            return;
        }

        // 4. Normal awake lifecycle (Idle <-> Random Ambient Activities)
        _moodTicksRemaining--;

        if (_currentMood == PetMood.Eating)
        {
            // Munching: alternate eating and eating2 every 2 ticks (400ms)
            if (_moodTicksRemaining % 2 == 0)
            {
                _eatingFrameToggle = !_eatingFrameToggle;
                string eatFrame = _eatingFrameToggle ? PetEating : PetEating2;
                SetCompanionImage(eatFrame);
            }

            if (_moodTicksRemaining <= 0)
            {
                // Done eating, return to idle
                _currentMood = PetMood.AwakeIdle;
                _moodTicksRemaining = _random.Next(20, 35); // 4-7 seconds
                SetCompanionImage(PetIdle);
            }
            return;
        }

        if (_currentMood == PetMood.Ambient)
        {
            if (_moodTicksRemaining <= 0)
            {
                // Ambient activity finished, transition back to idle
                _currentMood = PetMood.AwakeIdle;
                _moodTicksRemaining = _random.Next(20, 35); // 4-7 seconds
                SetCompanionImage(PetIdle);
            }
            return;
        }

        // We are in AwakeIdle
        if (_moodTicksRemaining <= 0)
        {
            // Time to pick a new random state!
            int roll = _random.Next(100);
            if (roll < 30)
            {
                // Gentle shift to idle2
                _currentMood = PetMood.AwakeIdle;
                _moodTicksRemaining = _random.Next(15, 25); // 3 to 5 seconds
                SetCompanionImage(PetIdle2);
            }
            else
            {
                // Pick a random ambient activity
                string chosenKey = RandomAmbientKeys[_random.Next(RandomAmbientKeys.Length)];
                if (chosenKey == "eating")
                {
                    _currentMood = PetMood.Eating;
                    _moodTicksRemaining = _random.Next(20, 30); // 4 to 6 seconds of eating
                    _eatingFrameToggle = false;
                    SetCompanionImage(PetEating);
                }
                else
                {
                    _currentMood = PetMood.Ambient;
                    _moodTicksRemaining = _random.Next(20, 35); // 4 to 7 seconds
                    if (PetAnims.TryGetValue(chosenKey, out var frames) && frames.Length > 0)
                    {
                        SetCompanionImage(frames[0]);
                    }
                }
            }
        }
    }

    public async Task OnTypewriterClickedAsync()
    {
        if (_isAnimating)
        {
            return;
        }

        RegisterUserActivity();
        _isAnimating = true;

        try
        {
            if (!_isPaperExtended)
            {
                TypewriterImage = TwPress;
                SetCompanionImage(PetCurious);
                await Task.Delay(100);

                TypewriterImage = TwIdle;
                SetCompanionImage(PetHappy);
                IsPaperExtended = true;
                IsAddingTask = false;
                RefreshTasks();

                await Task.Delay(80);
            }
            else
            {
                IsPaperExtended = false;
                IsAddingTask = false;
                ClosePaperAction?.Invoke();

                TypewriterImage = TwPress;
                SetCompanionImage(PetCurious);
                await Task.Delay(100);

                TypewriterImage = TwIdle;
                SetCompanionImage(PetIdle);
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
        _isSleeping = false;
        _lastActivityTime = DateTime.UtcNow;
        _temporaryPetState = PetCelebrate;
        _temporaryTicksRemaining = 15; // 3.0 seconds
        SetCompanionImage(PetCelebrate);
        SoundService.Play(SoundKind.Pop);
    }

    public async Task<bool> ToggleTaskAsync(ObsidianTask? task)
    {
        if (task == null)
        {
            return false;
        }

        RegisterUserActivity();

        try
        {
            bool targetState = !task.IsCompleted;
            bool success = await _obsidianService.SetTaskCompletionAsync(task, targetState, SelectedDate);

            if (success)
            {
                if (targetState)
                {
                    // When task checks done, show happy with pop sound
                    _isSleeping = false;
                    _lastActivityTime = DateTime.UtcNow;
                    _temporaryPetState = PetHappy;
                    _temporaryTicksRemaining = 18; // 3.6 seconds
                    SetCompanionImage(PetHappy);
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
        RegisterUserActivity();

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
        RegisterUserActivity();

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
        RegisterUserActivity();

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
        RegisterUserActivity();

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
        RegisterUserActivity();

        if (task == null) return;
        task.EditText = task.Text;
        task.IsEditing = false;
    }
}
