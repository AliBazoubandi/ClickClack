using System.Text.RegularExpressions;
using PixelCompanion.Models;

namespace PixelCompanion.Services;

public class ObsidianTaskParser
{
    // Matches: [indent][- or *][whitespace][[ ] or [x] or [X]][whitespace][task text]
    private static readonly Regex TaskRegex = new(
        @"^(\s*)([-*])\s+\[([ xX])\]\s*(.*)$",
        RegexOptions.Compiled);

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

                tasks.Add(new ObsidianTask
                {
                    LineIndex = i,
                    OccurrenceIndex = occIndex,
                    RawLine = line,
                    Indent = indent,
                    ListMarker = marker,
                    IsCompleted = check.Equals("x", StringComparison.OrdinalIgnoreCase),
                    Text = trimmedText
                });
            }
        }

        return tasks;
    }

    public bool TryParseTaskLine(string line, out string indent, out string marker, out bool isCompleted, out string text)
    {
        var match = TaskRegex.Match(line);
        if (match.Success)
        {
            indent = match.Groups[1].Value;
            marker = match.Groups[2].Value;
            isCompleted = match.Groups[3].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
            text = match.Groups[4].Value.Trim();
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
}
