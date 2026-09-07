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
    private string? _currentDailyNotePath;

    public event Action? TasksChanged;

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
        TriggerDebouncedRefresh();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsSelfWriting)
        {
            return;
        }

        TriggerDebouncedRefresh();
    }

    private void TriggerDebouncedRefresh()
    {
        // Debounce notifications (400ms) with atomic exchange to avoid timer race conditions
        var newTimer = new System.Threading.Timer(_ =>
        {
            TasksChanged?.Invoke();
        }, null, 400, Timeout.Infinite);

        var oldTimer = Interlocked.Exchange(ref _debounceTimer, newTimer);
        oldTimer?.Dispose();
    }

    private string? PrepareDailyNotePath()
    {
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

        string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, DateTime.Now);
        return Path.Combine(targetDir, $"{dateString}.md");
    }

    public string? EnsureDailyNoteFileExists()
    {
        string? targetFilePath = PrepareDailyNotePath();
        if (targetFilePath == null)
        {
            return null;
        }

        if (!File.Exists(targetFilePath))
        {
            try
            {
                string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, DateTime.Now);
                var initialLines = new[] { $"# {dateString}", string.Empty };
                SafeWriteLinesAtomic(targetFilePath, initialLines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create daily note file: {ex.Message}");
            }
        }

        _currentDailyNotePath = targetFilePath;
        return targetFilePath;
    }

    public async Task<string?> EnsureDailyNoteFileExistsAsync()
    {
        string? targetFilePath = PrepareDailyNotePath();
        if (targetFilePath == null)
        {
            return null;
        }

        if (!File.Exists(targetFilePath))
        {
            try
            {
                string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, DateTime.Now);
                var initialLines = new[] { $"# {dateString}", string.Empty };
                await SafeWriteLinesAtomicAsync(targetFilePath, initialLines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create daily note file: {ex.Message}");
            }
        }

        _currentDailyNotePath = targetFilePath;
        return targetFilePath;
    }

    public List<ObsidianTask> GetTodayTasks()
    {
        if (string.IsNullOrWhiteSpace(_config.ObsidianVaultPath))
        {
            Status = VaultStatus.NotConfigured;
            StatusMessage = "No vault configured.\nRight-click tray -> Settings";
            return new List<ObsidianTask>();
        }

        // Daily rollover check: compare currently watched note path with expected path for today
        string dateString = DateFormatHelper.FormatDate(_config.DailyNoteDateFormat, DateTime.Now);
        string expectedDir = _config.ObsidianVaultPath;
        if (!string.IsNullOrWhiteSpace(_config.DailyNotesFolder))
        {
            expectedDir = Path.Combine(_config.ObsidianVaultPath, _config.DailyNotesFolder);
        }
        string expectedFilePath = Path.Combine(expectedDir, $"{dateString}.md");

        if (_currentDailyNotePath != null && !string.Equals(_currentDailyNotePath, expectedFilePath, StringComparison.OrdinalIgnoreCase))
        {
            SetupWatcher();
        }

        var filePath = EnsureDailyNoteFileExists();

        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.DailyNoteNotFound;
            StatusMessage = $"Could not access daily note\n({dateString}.md)";
            return new List<ObsidianTask>();
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
                StatusMessage = "No tasks for today.\nType below to add one!";
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

    private async Task<bool> SafeWriteLinesAtomicAsync(string targetFilePath, IEnumerable<string> lines, Encoding? encoding = null)
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
                    await writer.WriteLineAsync(line);
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
            System.Diagnostics.Debug.WriteLine($"SafeWriteLinesAtomicAsync failed for target {Path.GetFileName(targetFilePath)}: {ex.Message}");
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

    public async Task<bool> SetTaskCompletionAsync(ObsidianTask task, bool targetState)
    {
        var filePath = await EnsureDailyNoteFileExistsAsync();
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }

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
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }

        return false;
    }

    public async Task<bool> AddTaskAsync(string taskText)
    {
        if (string.IsNullOrWhiteSpace(taskText))
        {
            return false;
        }

        var filePath = await EnsureDailyNoteFileExistsAsync();
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }

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
            await Task.Delay(400);
            Interlocked.Decrement(ref _selfWritingCount);
        }
    }

    public async Task<bool> DeleteTaskAsync(ObsidianTask task)
    {
        var filePath = await EnsureDailyNoteFileExistsAsync();
        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.Error;
            StatusMessage = "Couldn't save your task to the daily note.";
            return false;
        }

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
    }
}
