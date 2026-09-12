using System.IO;
using System.Text;
using PixelCompanion.Models;

namespace PixelCompanion.Services;

public enum VaultStatus
{
    NotConfigured,
    VaultNotFound,
    DailyNoteNotFound,
    NoTasksToday,
    Loaded,
    Error
}

public class ObsidianService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly ObsidianTaskParser _parser;
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private int _selfWritingCount;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private string? _currentDailyNotePath;

    public event Action<string?>? TasksChanged;

    public VaultStatus Status { get; private set; } = VaultStatus.NotConfigured;
    public string StatusMessage { get; private set; } = string.Empty;
    public string? CurrentDailyNotePath => _currentDailyNotePath;

    private bool IsSelfWriting => Volatile.Read(ref _selfWritingCount) > 0;

    public ObsidianService(ConfigService configService, AppConfig config)
    {
        _configService = configService;
        _config = config;
        _parser = new ObsidianTaskParser();

        SetupWatcher();
    }

    public void SetupWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;

        if (string.IsNullOrWhiteSpace(_config.ObsidianVaultPath) || !Directory.Exists(_config.ObsidianVaultPath))
        {
            return;
        }

        try
        {
            string watchDir = _config.ObsidianVaultPath;
            if (!string.IsNullOrWhiteSpace(_config.DailyNotesFolder))
            {
                var subDir = Path.Combine(_config.ObsidianVaultPath, _config.DailyNotesFolder);
                if (!Directory.Exists(subDir))
                {
                    Directory.CreateDirectory(subDir);
                }
                watchDir = subDir;
            }

            _watcher = new FileSystemWatcher(watchDir)
            {
                Filter = "*.md",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
            _watcher.Error += OnWatcherError;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize FileSystemWatcher: {ex.Message}");
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"FileSystemWatcher error: {e.GetException()?.Message}");
        TriggerDebouncedRefresh(null);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsSelfWriting)
        {
            return;
        }

        TriggerDebouncedRefresh(e.FullPath);
    }

    private void TriggerDebouncedRefresh(string? filePath = null)
    {
        // Debounce notifications (400ms) with atomic exchange to avoid timer race conditions
        var newTimer = new System.Threading.Timer(_ =>
        {
            TasksChanged?.Invoke(filePath);
        }, null, 400, Timeout.Infinite);

        var oldTimer = Interlocked.Exchange(ref _debounceTimer, newTimer);
        oldTimer?.Dispose();
    }

    public string? PrepareDailyNotePath(DateTime? date = null)
    {
        DateTime targetDate = date ?? DateTime.Now;
        if (string.IsNullOrWhiteSpace(_config.ObsidianVaultPath))
        {
            return null;
        }

        if (!Directory.Exists(_config.ObsidianVaultPath))
        {
            try
            {
                Directory.CreateDirectory(_config.ObsidianVaultPath);
            }
            catch
            {
                return null;
            }
        }

        string targetDir = _config.ObsidianVaultPath;
        if (!string.IsNullOrWhiteSpace(_config.DailyNotesFolder))
        {
            targetDir = Path.Combine(_config.ObsidianVaultPath, _config.DailyNotesFolder);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
        }

        string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, targetDate);
        return Path.Combine(targetDir, $"{dateString}.md");
    }

    /// <summary>
    /// Scans backward up to 7 days for the most recent existing daily note in the vault folder
    /// with open (uncompleted) tasks, returning those tasks. Notes with zero open tasks are skipped
    /// so leftovers from earlier in the week are not abandoned. Returns empty list if no open tasks exist.
    /// </summary>
    public List<ObsidianTask> GetRolloverCandidates(DateTime targetDate)
    {
        for (int dayOffset = 1; dayOffset <= 7; dayOffset++)
        {
            DateTime pastDate = targetDate.AddDays(-dayOffset);
            string? pastPath = PrepareDailyNotePath(pastDate);
            if (pastPath != null && File.Exists(pastPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(pastPath);
                    var tasks = _parser.ParseTasks(lines);
                    var openTasks = tasks.Where(t => !t.IsCompleted).ToList();
                    if (openTasks.Count > 0)
                    {
                        return openTasks;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scanning past note for rollover: {ex.Message}");
                }
            }
        }

        return new List<ObsidianTask>();
    }

    private List<string> BuildInitialDailyNoteLines(DateTime targetDate)
    {
        string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, targetDate);
        var initialLines = new List<string> { $"# {dateString}", string.Empty };

        if (targetDate.Date == DateTime.Today && _config.RolloverEnabled)
        {
            var rolloverCandidates = GetRolloverCandidates(targetDate);
            if (rolloverCandidates.Count > 0)
            {
                initialLines.Add("## From yesterday");
                foreach (var task in rolloverCandidates)
                {
                    initialLines.Add(_parser.BuildNewTaskLine(task.Text));
                }
                initialLines.Add(string.Empty);
            }
        }

        return initialLines;
    }

    public string? EnsureDailyNoteFileExists(DateTime? date = null)
    {
        DateTime targetDate = date ?? DateTime.Now;
        string? targetFilePath = PrepareDailyNotePath(targetDate);
        if (targetFilePath == null)
        {
            return null;
        }

        if (!File.Exists(targetFilePath))
        {
            try
            {
                var initialLines = BuildInitialDailyNoteLines(targetDate);
                SafeWriteLinesAtomic(targetFilePath, initialLines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create daily note file: {ex.Message}");
            }
        }

        if (targetDate.Date == DateTime.Today)
        {
            _currentDailyNotePath = targetFilePath;
        }
        return targetFilePath;
    }

    // Design Decision & Documentation:
    // Inverted sync core + async wrapper pattern: EnsureDailyNoteFileExists and SafeWriteLinesAtomic perform
    // direct synchronous file I/O. Async twins delegate to the sync core via Task.FromResult to eliminate
    // duplicated rollover/heading/date/atomic-replace logic while avoiding sync-over-async deadlocks on the
    // WPF UI thread (zero blocking async calls).
    public Task<string?> EnsureDailyNoteFileExistsAsync(DateTime? date = null)
    {
        return Task.FromResult(EnsureDailyNoteFileExists(date));
    }

    public List<ObsidianTask> GetTasksForDate(DateTime date)
    {
        if (string.IsNullOrWhiteSpace(_config.ObsidianVaultPath))
        {
            Status = VaultStatus.NotConfigured;
            StatusMessage = "No vault configured.\nRight-click tray -> Settings";
            return new List<ObsidianTask>();
        }

        string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, date);
        string? expectedFilePath = PrepareDailyNotePath(date);

        if (date.Date == DateTime.Today)
        {
            if (_currentDailyNotePath != null && expectedFilePath != null && !string.Equals(_currentDailyNotePath, expectedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                SetupWatcher();
            }
            EnsureDailyNoteFileExists(date);
        }

        if (expectedFilePath == null || !File.Exists(expectedFilePath))
        {
            Status = VaultStatus.DailyNoteNotFound;
            StatusMessage = (date.Date == DateTime.Today)
                ? $"Could not access daily note\n({dateString}.md)"
                : $"No daily note found\nfor {dateString}";
            return new List<ObsidianTask>();
        }

        try
        {
            using var fileStream = new FileStream(expectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            var tasks = _parser.ParseTasks(lines.ToArray());

            if (tasks.Count == 0)
            {
                Status = VaultStatus.NoTasksToday;
                StatusMessage = (date.Date == DateTime.Today)
                    ? "No tasks for today.\nType below to add one!"
                    : $"No tasks recorded for\n{dateString}.";
            }
            else
            {
                Status = VaultStatus.Loaded;
                StatusMessage = string.Empty;
            }

            return tasks;
        }
        catch (Exception ex)
        {
            Status = VaultStatus.Error;
            StatusMessage = $"Error reading note:\n{ex.Message}";
            return new List<ObsidianTask>();
        }
    }

    public List<ObsidianTask> GetTodayTasks() => GetTasksForDate(DateTime.Now);

    /// <summary>
    /// Checks whether there are open tasks from a recent daily note (up to 7 days back)
    /// that have not yet been copied into today's note.
    /// </summary>
    public bool HasPendingRolloverTasks()
    {
        if (!_config.RolloverEnabled) return false;

        var candidates = GetRolloverCandidates(DateTime.Today);
        if (candidates.Count == 0) return false;

        string? todayPath = PrepareDailyNotePath(DateTime.Today);
        if (todayPath == null || !File.Exists(todayPath))
        {
            return candidates.Count > 0;
        }

        try
        {
            string[] lines = File.ReadAllLines(todayPath);
            var currentTasks = _parser.ParseTasks(lines);
            var existingTexts = new HashSet<string>(currentTasks.Select(t => t.Text.Trim()), StringComparer.Ordinal);
            return candidates.Any(t => !existingTexts.Contains(t.Text.Trim()));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Manually rolls over open tasks from the most recent past note into today's note,
    /// creating ## From yesterday if not present, and skipping duplicate tasks.
    /// </summary>
    public async Task<bool> RolloverNowAsync()
    {
        if (!_config.RolloverEnabled)
        {
            return false;
        }

        string? todayPath = await EnsureDailyNoteFileExistsAsync(DateTime.Today);
        if (todayPath == null || !File.Exists(todayPath))
        {
            return false;
        }

        var rolloverCandidates = GetRolloverCandidates(DateTime.Today);
        if (rolloverCandidates.Count == 0)
        {
            return false;
        }

        await _fileLock.WaitAsync();
        Interlocked.Increment(ref _selfWritingCount);
        try
        {
            var lines = new List<string>();
            using (var fileStream = new FileStream(todayPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            var currentTasks = _parser.ParseTasks(lines.ToArray());
            var existingTexts = new HashSet<string>(currentTasks.Select(t => t.Text.Trim()), StringComparer.Ordinal);

            var toAdd = rolloverCandidates.Where(t => !existingTexts.Contains(t.Text.Trim())).ToList();
            if (toAdd.Count == 0)
            {
                return false;
            }

            bool hasHeading = lines.Any(l => l.Trim().Equals("## From yesterday", StringComparison.OrdinalIgnoreCase));
            if (!hasHeading)
            {
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                {
                    lines.Add(string.Empty);
                }
                lines.Add("## From yesterday");
            }

            foreach (var task in toAdd)
            {
                lines.Add(_parser.BuildNewTaskLine(task.Text));
            }

            var encoding = DetectEncoding(todayPath);
            bool success = await SafeWriteLinesAtomicAsync(todayPath, lines, encoding);
            if (success)
            {
                TasksChanged?.Invoke(todayPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RolloverNowAsync failed: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }

        return false;
    }

    public int ResolveTargetIndex(IReadOnlyList<string> lines, ObsidianTask task)
    {
        // Fast path: LineIndex is within bounds and matches structured identity
        if (task.LineIndex >= 0 && task.LineIndex < lines.Count)
        {
            if (_parser.TryParseTaskLine(lines[task.LineIndex], out var indent, out var marker, out _, out var text))
            {
                if (string.Equals(indent, task.Indent, StringComparison.Ordinal) &&
                    string.Equals(marker, task.ListMarker, StringComparison.Ordinal) &&
                    string.Equals(text, task.Text, StringComparison.Ordinal))
                {
                    return task.LineIndex;
                }
            }
        }

        // Fallback: Scan lines, match by structured identity and occurrence index
        int currentOccurrence = 0;
        int firstMatchIndex = -1;
        int closestIndex = -1;
        int minDistance = int.MaxValue;

        for (int i = 0; i < lines.Count; i++)
        {
            if (_parser.TryParseTaskLine(lines[i], out var indent, out var marker, out _, out var text))
            {
                if (string.Equals(indent, task.Indent, StringComparison.Ordinal) &&
                    string.Equals(marker, task.ListMarker, StringComparison.Ordinal) &&
                    string.Equals(text, task.Text, StringComparison.Ordinal))
                {
                    if (firstMatchIndex == -1)
                    {
                        firstMatchIndex = i;
                    }

                    if (currentOccurrence == task.OccurrenceIndex)
                    {
                        return i;
                    }

                    int dist = Math.Abs(i - task.LineIndex);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestIndex = i;
                    }

                    currentOccurrence++;
                }
            }
        }

        if (closestIndex >= 0)
        {
            return closestIndex;
        }

        return firstMatchIndex;
    }

    public static Encoding DetectEncoding(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length >= 3)
                {
                    byte[] bom = new byte[3];
                    int read = fs.Read(bom, 0, 3);
                    if (read == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    {
                        return new UTF8Encoding(true);
                    }
                }
            }
            catch
            {
            }
        }

        return new UTF8Encoding(false);
    }

    private bool SafeWriteLinesAtomic(string targetFilePath, IEnumerable<string> lines, Encoding? encoding = null)
    {
        string? targetDir = Path.GetDirectoryName(targetFilePath);
        if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
        {
            return false;
        }

        string tempFilePath = Path.Combine(targetDir, $".tmp_{Guid.NewGuid():N}.tmp");

        try
        {
            var enc = encoding ?? DetectEncoding(targetFilePath);
            using (var writeStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(writeStream, enc))
            {
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }
            }

            if (File.Exists(targetFilePath))
            {
                try
                {
                    File.Replace(tempFilePath, targetFilePath, null, ignoreMetadataErrors: true);
                    return true;
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(tempFilePath, targetFilePath, overwrite: true);
                    return true;
                }
                catch (IOException ioEx)
                {
                    System.Diagnostics.Debug.WriteLine($"File.Replace fallback: {ioEx.Message}");
                    File.Move(tempFilePath, targetFilePath, overwrite: true);
                    return true;
                }
            }
            else
            {
                File.Move(tempFilePath, targetFilePath);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SafeWriteLinesAtomic failed for target {Path.GetFileName(targetFilePath)}: {ex.Message}");
            return false;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception cleanupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete temp file {Path.GetFileName(tempFilePath)}: {cleanupEx.Message}");
                }
            }
        }
    }

    private Task<bool> SafeWriteLinesAtomicAsync(string targetFilePath, IEnumerable<string> lines, Encoding? encoding = null)
    {
        return Task.FromResult(SafeWriteLinesAtomic(targetFilePath, lines, encoding));
    }

    public async Task<bool> SetTaskCompletionAsync(ObsidianTask task, bool targetState, DateTime? date = null)
    {
        var filePath = await EnsureDailyNoteFileExistsAsync(date);
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }

        await _fileLock.WaitAsync();
        Interlocked.Increment(ref _selfWritingCount);
        try
        {
            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            int targetIndex = ResolveTargetIndex(lines, task);

            if (targetIndex >= 0 && targetIndex < lines.Count)
            {
                lines[targetIndex] = _parser.BuildToggledLine(task, targetState);

                var encoding = DetectEncoding(filePath);
                bool writeSuccess = await SafeWriteLinesAtomicAsync(filePath, lines, encoding);
                if (writeSuccess)
                {
                    task.IsCompleted = targetState;
                    task.RawLine = lines[targetIndex];
                    Status = VaultStatus.Loaded;
                    StatusMessage = string.Empty;
                    return true;
                }
            }

            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write task update: {ex.Message}");
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
        }
        finally
        {
            _fileLock.Release();
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }

        return false;
    }

    public async Task<bool> AddTaskAsync(string taskText, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(taskText))
        {
            return false;
        }

        var filePath = await EnsureDailyNoteFileExistsAsync(date);
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }

        await _fileLock.WaitAsync();
        Interlocked.Increment(ref _selfWritingCount);
        try
        {
            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            var newLine = _parser.BuildNewTaskLine(taskText);
            lines.Add(newLine);

            var encoding = DetectEncoding(filePath);
            bool writeSuccess = await SafeWriteLinesAtomicAsync(filePath, lines, encoding);
            if (writeSuccess)
            {
                Status = VaultStatus.Loaded;
                StatusMessage = string.Empty;
                return true;
            }

            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add task: {ex.Message}");
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }
        finally
        {
            _fileLock.Release();
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }
    }

    public async Task<bool> DeleteTaskAsync(ObsidianTask task, DateTime? date = null)
    {
        var filePath = await EnsureDailyNoteFileExistsAsync(date);
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }

        await _fileLock.WaitAsync();
        Interlocked.Increment(ref _selfWritingCount);
        try
        {
            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            int targetIndex = ResolveTargetIndex(lines, task);

            if (targetIndex >= 0 && targetIndex < lines.Count)
            {
                lines.RemoveAt(targetIndex);

                var encoding = DetectEncoding(filePath);
                bool writeSuccess = await SafeWriteLinesAtomicAsync(filePath, lines, encoding);
                if (writeSuccess)
                {
                    Status = VaultStatus.Loaded;
                    StatusMessage = string.Empty;
                    return true;
                }
            }

            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete task: {ex.Message}");
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
        }
        finally
        {
            _fileLock.Release();
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }

        return false;
    }

    public async Task<bool> UpdateTaskTextAsync(ObsidianTask task, string newText, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(newText))
        {
            return false;
        }

        string trimmedNewText = newText.Trim();
        if (string.Equals(task.Text, trimmedNewText, StringComparison.Ordinal))
        {
            return true;
        }

        var filePath = await EnsureDailyNoteFileExistsAsync(date);
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task update to the daily note.";
            return false;
        }

        await _fileLock.WaitAsync();
        Interlocked.Increment(ref _selfWritingCount);
        try
        {
            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            int targetIndex = ResolveTargetIndex(lines, task);
            if (targetIndex >= 0 && targetIndex < lines.Count)
            {
                string newLine = _parser.BuildUpdatedTextLine(task, trimmedNewText);
                lines[targetIndex] = newLine;

                var encoding = DetectEncoding(filePath);
                bool writeSuccess = await SafeWriteLinesAtomicAsync(filePath, lines, encoding);
                if (writeSuccess)
                {
                    task.Text = trimmedNewText;
                    task.RawLine = newLine;
                    if (ObsidianTaskParser.TryParseDueTime(trimmedNewText, out var dt))
                    {
                        task.DueTime = dt;
                    }
                    else
                    {
                        task.DueTime = null;
                    }

                    Status = VaultStatus.Loaded;
                    StatusMessage = string.Empty;
                    return true;
                }
            }

            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task update to the daily note.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update task text: {ex.Message}");
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task update to the daily note.";
        }
        finally
        {
            _fileLock.Release();
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }

        return false;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        var oldTimer = Interlocked.Exchange(ref _debounceTimer, null);
        oldTimer?.Dispose();
        _fileLock.Dispose();
    }
}
