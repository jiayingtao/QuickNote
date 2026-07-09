using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickNote.Models;

public class TodoItem : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _content = string.Empty;
    private bool _isCompleted;
    private bool _isEditing = true;
    public int Order { get; set; }

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
