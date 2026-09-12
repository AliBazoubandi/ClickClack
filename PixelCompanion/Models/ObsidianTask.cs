using PixelCompanion.ViewModels;

namespace PixelCompanion.Models;

public class ObsidianTask : ViewModelBase
{
    private bool _isCompleted;
    private string _text = string.Empty;

    public int LineIndex { get; set; }
    public int OccurrenceIndex { get; set; }
    public string RawLine { get; set; } = string.Empty;
    public string Indent { get; set; } = string.Empty;
    public string ListMarker { get; set; } = "-";

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    public TimeSpan? DueTime { get; set; }

    private bool _isEditing;
    private string _editText = string.Empty;

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value);
    }
}
