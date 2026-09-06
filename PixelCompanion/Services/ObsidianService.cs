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
    private bool _isSelfWriting;
    private string? _currentDailyNotePath;

    public event Action? TasksChanged;

    public VaultStatus Status { get; private set; } = VaultStatus.NotConfigured;
    public string StatusMessage { get; private set; } = string.Empty;
    public string? CurrentDailyNotePath => _currentDailyNotePath;

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
            _watcher.Renamed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize FileSystemWatcher: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_isSelfWriting)
        {
            return;
        }

        // Debounce notifications (400ms)
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            TasksChanged?.Invoke();
        }, null, 400, Timeout.Infinite);
    }

    public string? EnsureDailyNoteFileExists()
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

        string dateFormat = string.IsNullOrWhiteSpace(_config.DailyNoteDateFormat)
            ? "yyyy-MM-dd"
            : _config.DailyNoteDateFormat;

        string dateString = DateTime.Now.ToString(dateFormat);
        string targetFilePath = Path.Combine(targetDir, $"{dateString}.md");

        if (!File.Exists(targetFilePath))
        {
            try
            {
                File.WriteAllText(targetFilePath, $"# {dateString}\n\n", Encoding.UTF8);
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

        var filePath = EnsureDailyNoteFileExists();

        if (filePath == null || !File.Exists(filePath))
        {
            Status = VaultStatus.DailyNoteNotFound;
            string dateStr = DateTime.Now.ToString(_config.DailyNoteDateFormat ?? "yyyy-MM-dd");
            StatusMessage = $"Could not access daily note\n({dateStr}.md)";
            return new List<ObsidianTask>();
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream, Encoding.UTF8);

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

    public async Task<bool> SetTaskCompletionAsync(ObsidianTask task, bool targetState)
    {
        var filePath = EnsureDailyNoteFileExists();
        if (filePath == null || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            _isSelfWriting = true;

            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            int targetIndex = -1;

            if (task.LineIndex >= 0 && task.LineIndex < lines.Count && lines[task.LineIndex].Contains(task.Text))
            {
                targetIndex = task.LineIndex;
            }
            else
            {
                targetIndex = lines.FindIndex(l => l.Contains(task.Text));
            }

            if (targetIndex >= 0)
            {
                lines[targetIndex] = _parser.BuildToggledLine(task, targetState);

                using (var writeStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(writeStream, Encoding.UTF8))
                {
                    foreach (var line in lines)
                    {
                        await writer.WriteLineAsync(line);
                    }
                }

                task.IsCompleted = targetState;
                task.RawLine = lines[targetIndex];
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write task update: {ex.Message}");
        }
        finally
        {
            await Task.Delay(400);
            _isSelfWriting = false;
        }

        return false;
    }

    public async Task<bool> AddTaskAsync(string taskText)
    {
        if (string.IsNullOrWhiteSpace(taskText))
        {
            return false;
        }

        var filePath = EnsureDailyNoteFileExists();
        if (filePath == null || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            _isSelfWriting = true;

            var newLine = _parser.BuildNewTaskLine(taskText);

            using (var writeStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(writeStream, Encoding.UTF8))
            {
                await writer.WriteLineAsync(newLine);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add task: {ex.Message}");
            return false;
        }
        finally
        {
            await Task.Delay(400);
            _isSelfWriting = false;
        }
    }

    public async Task<bool> DeleteTaskAsync(ObsidianTask task)
    {
        var filePath = EnsureDailyNoteFileExists();
        if (filePath == null || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            _isSelfWriting = true;

            var lines = new List<string>();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream, Encoding.UTF8))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lines.Add(line);
                }
            }

            int targetIndex = -1;
            if (task.LineIndex >= 0 && task.LineIndex < lines.Count && lines[task.LineIndex].Contains(task.Text))
            {
                targetIndex = task.LineIndex;
            }
            else
            {
                targetIndex = lines.FindIndex(l => l.Contains(task.Text));
            }

            if (targetIndex >= 0)
            {
                lines.RemoveAt(targetIndex);

                using (var writeStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(writeStream, Encoding.UTF8))
                {
                    foreach (var line in lines)
                    {
                        await writer.WriteLineAsync(line);
                    }
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete task: {ex.Message}");
        }
        finally
        {
            await Task.Delay(400);
            _isSelfWriting = false;
        }

        return false;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
