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

                tasks.Add(new ObsidianTask
                {
                    LineIndex = i,
                    RawLine = line,
                    Indent = indent,
                    ListMarker = marker,
                    IsCompleted = check.Equals("x", StringComparison.OrdinalIgnoreCase),
                    Text = text.Trim()
                });
            }
        }

        return tasks;
    }

    public string BuildToggledLine(ObsidianTask task, bool newCompletedState)
    {
        var checkMark = newCompletedState ? "x" : " ";
        return $"{task.Indent}{task.ListMarker} [{checkMark}] {task.Text}";
    }

    public string BuildNewTaskLine(string text)
    {
        return $"- [ ] {text.Trim()}";
    }
}
