using System.Text;
using System.Text.RegularExpressions;
using PixelCompanion.Models;

namespace PixelCompanion.Services;

public class ObsidianTaskParser
{
    // Matches: [indent][- or *][whitespace][[ ] or [x] or [X]][whitespace][task text]
    private static readonly Regex TaskRegex = new(
        @"^(\s*)([-*])\s+\[([ xX])\]\s*(.*)$",
        RegexOptions.Compiled);

    // Matches: ⏰ HH:mm or @HH:mm (24h) anywhere in task text
    private static readonly Regex DueTimeRegex = new(
        @"(?:⏰\s*|@)(\d{1,2}):(\d{2})",
        RegexOptions.Compiled);

    /// <summary>
    /// Normalizes Persian (۰-۹) and Arabic-Indic (٠-٩) digits to standard ASCII digits (0-9).
    /// </summary>
    public static string NormalizeDigits(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c >= '۰' && c <= '۹')
                sb.Append((char)('0' + (c - '۰')));
            else if (c >= '٠' && c <= '٩')
                sb.Append((char)('0' + (c - '٠')));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Attempts to extract the first valid 24-hour due time (⏰ HH:mm or @HH:mm) from the task text.
    /// Invalid times (e.g. 25:99) are ignored and return false.
    /// </summary>
    public static bool TryParseDueTime(string text, out TimeSpan dueTime)
    {
        dueTime = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string normalized = NormalizeDigits(text);
        var matches = DueTimeRegex.Matches(normalized);
        foreach (Match match in matches)
        {
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int hour) &&
                int.TryParse(match.Groups[2].Value, out int minute))
            {
                if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                {
                    dueTime = new TimeSpan(hour, minute, 0);
                    return true;
                }
            }
        }

        return false;
    }

    public List<ObsidianTask> ParseTasks(string[] lines)
    {
        var tasks = new List<ObsidianTask>();
        var occurrenceTracker = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = TaskRegex.Match(line);
            if (match.Success)
            {
                var indent = match.Groups[1].Value;
                var marker = match.Groups[2].Value;
                var check = match.Groups[3].Value;
                var text = match.Groups[4].Value;
                var trimmedText = text.Trim();

                string identityKey = $"{indent}|{marker}|{trimmedText}";
                occurrenceTracker.TryGetValue(identityKey, out int occIndex);
                occurrenceTracker[identityKey] = occIndex + 1;

                TimeSpan? dueTime = TryParseDueTime(trimmedText, out var dt) ? dt : null;

                tasks.Add(new ObsidianTask
                {
                    LineIndex = i,
                    OccurrenceIndex = occIndex,
                    RawLine = line,
                    Indent = indent,
                    ListMarker = marker,
                    IsCompleted = check.Equals("x", StringComparison.OrdinalIgnoreCase),
                    Text = trimmedText,
                    DueTime = dueTime
                });
            }
        }

        return tasks;
    }

    public bool TryParseTaskLine(string line, out string indent, out string marker, out bool isCompleted, out string text)
    {
        return TryParseTaskLine(line, out indent, out marker, out isCompleted, out text, out _);
    }

    public bool TryParseTaskLine(string line, out string indent, out string marker, out bool isCompleted, out string text, out TimeSpan? dueTime)
    {
        dueTime = null;
        var match = TaskRegex.Match(line);
        if (match.Success)
        {
            indent = match.Groups[1].Value;
            marker = match.Groups[2].Value;
            isCompleted = match.Groups[3].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
            text = match.Groups[4].Value.Trim();
            if (TryParseDueTime(text, out var dt))
            {
                dueTime = dt;
            }
            return true;
        }

        indent = string.Empty;
        marker = "-";
        isCompleted = false;
        text = string.Empty;
        return false;
    }

    public string BuildToggledLine(ObsidianTask task, bool newCompletedState)
    {
        char newCheckChar = newCompletedState ? 'x' : ' ';

        if (!string.IsNullOrEmpty(task.RawLine))
        {
            var match = TaskRegex.Match(task.RawLine);
            if (match.Success && match.Groups[3].Success)
            {
                int checkIndex = match.Groups[3].Index;
                char[] chars = task.RawLine.ToCharArray();
                if (checkIndex >= 0 && checkIndex < chars.Length)
                {
                    chars[checkIndex] = newCheckChar;
                    return new string(chars);
                }
            }
        }

        var checkMark = newCompletedState ? "x" : " ";
        return $"{task.Indent}{task.ListMarker} [{checkMark}] {task.Text}";
    }

    public string BuildNewTaskLine(string text)
    {
        return $"- [ ] {text.Trim()}";
    }

    public string BuildUpdatedTextLine(ObsidianTask task, string newText)
    {
        var trimmed = newText.Trim();
        var checkMark = task.IsCompleted ? "x" : " ";

        if (!string.IsNullOrEmpty(task.RawLine))
        {
            var match = TaskRegex.Match(task.RawLine);
            if (match.Success && match.Groups[4].Success)
            {
                string prefix = task.RawLine.Substring(0, match.Groups[4].Index);
                return $"{prefix}{trimmed}";
            }
        }

        return $"{task.Indent}{task.ListMarker} [{checkMark}] {trimmed}";
    }
}
