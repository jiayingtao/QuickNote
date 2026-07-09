using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace QuickNote.Models;

public class Note : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _name = "新便签";
    private string _colorTheme = "Yellow";
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 360;
    public double WindowHeight { get; set; } = 500;

    public string ColorTheme
    {
        get => _colorTheme;
        set { _colorTheme = value; OnPropertyChanged(); }
    }

    public ObservableCollection<TodoItem> Todos { get; set; } = new();
    public string NoteContent { get; set; } = string.Empty;
    public bool IsNoteVisible { get; set; } = true;
    public double NoteSplitRatio { get; set; } = 0.45;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public int NextTodoOrder => Todos.Count > 0 ? Todos.Max(t => t.Order) + 1 : 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
