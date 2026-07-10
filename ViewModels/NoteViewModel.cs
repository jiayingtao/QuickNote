using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using QuickNote.Models;

namespace QuickNote.ViewModels;

public class NoteViewModel : ViewModelBase
{
    private readonly Note _note;
    private readonly Action _onChanged;
    private bool _isEditingName;

    public NoteViewModel(Note note, Action onChanged)
    {
        _note = note;
        _onChanged = onChanged;

        Todos = note.Todos;
        SortCollectionByOrder(Todos);

        foreach (var todo in Todos)
            todo.PropertyChanged += (_, _) => OnDataChanged();
        Todos.CollectionChanged += (_, _) => OnDataChanged();

        AddTodoCommand = new RelayCommand(AddTodo);
        DeleteTodoCommand = new RelayCommand(p => DeleteTodo(p as TodoItem));
        ToggleEditNameCommand = new RelayCommand(() => IsEditingName = !IsEditingName);

        ToggleBoldCommand = new RelayCommand(() => ToggleEditingCommand(EditingCommands.ToggleBold));
        ToggleItalicCommand = new RelayCommand(() => ToggleEditingCommand(EditingCommands.ToggleItalic));
        ToggleUnderlineCommand = new RelayCommand(() => ToggleEditingCommand(EditingCommands.ToggleUnderline));
        ToggleBulletListCommand = new RelayCommand(() => ToggleEditingCommand(EditingCommands.ToggleBullets));
        ToggleNoteVisibilityCommand = new RelayCommand(() => IsNoteVisible = !IsNoteVisible);
    }

    public Note Model => _note;

    public string Name
    {
        get => _note.Name;
        set
        {
            _note.Name = value;
            OnPropertyChanged();
            OnDataChanged();
        }
    }

    public bool IsEditingName
    {
        get => _isEditingName;
        set { SetProperty(ref _isEditingName, value); OnPropertyChanged(nameof(IsEditingName)); }
    }

    public ObservableCollection<TodoItem> Todos { get; }

    /// <summary>Persisted XAML string for the note's rich text content.</summary>
    public string NoteContent
    {
        get => _note.NoteContent;
        set
        {
            _note.NoteContent = value;
            OnPropertyChanged();
            OnDataChanged();
        }
    }

    public ICommand AddTodoCommand { get; }
    public ICommand DeleteTodoCommand { get; }
    public ICommand ToggleEditNameCommand { get; }

    public ICommand ToggleBoldCommand { get; }
    public ICommand ToggleItalicCommand { get; }
    public ICommand ToggleUnderlineCommand { get; }
    public ICommand ToggleBulletListCommand { get; }
    public ICommand ToggleNoteVisibilityCommand { get; }

    public bool IsNoteVisible
    {
        get => _note.IsNoteVisible;
        set
        {
            if (_note.IsNoteVisible == value) return;
            _note.IsNoteVisible = value;
            OnPropertyChanged();
            OnDataChanged();
        }
    }

    public double NoteSplitRatio
    {
        get => _note.NoteSplitRatio;
        set
        {
            if (Math.Abs(_note.NoteSplitRatio - value) < 0.001) return;
            _note.NoteSplitRatio = value;
            OnPropertyChanged();
            OnDataChanged();
        }
    }

    public event Action<TodoItem>? FocusTodoRequested;

    private WeakReference<RichTextBox>? _activeEditor;

    /// <summary>Register the active RichTextBox so format commands can target it.</summary>
    public void RegisterEditor(RichTextBox editor)
        => _activeEditor = new WeakReference<RichTextBox>(editor);

    /// <summary>Load persisted note content into a RichTextBox.</summary>
    public void LoadDocumentTo(RichTextBox editor)
    {
        editor.Document = DeserializeDocument(_note.NoteContent);
    }

    /// <summary>Serialize the given FlowDocument to XAML and persist.</summary>
    public void SaveDocument(FlowDocument document)
    {
        var xaml = XamlWriter.Save(document);
        NoteContent = xaml;
    }

    private void ToggleEditingCommand(RoutedUICommand command)
    {
        if (_activeEditor is not null && _activeEditor.TryGetTarget(out var editor))
            command.Execute(null, editor);
    }

    private static FlowDocument DeserializeDocument(string xaml)
    {
        FlowDocument doc;
        if (string.IsNullOrEmpty(xaml))
            doc = new FlowDocument();
        else
        {
            try
            {
                doc = (FlowDocument)XamlReader.Parse(xaml);
            }
            catch
            {
                doc = new FlowDocument();
            }
        }
        ApplyTightLineSpacing(doc);
        return doc;
    }

    /// <summary>Apply tight line spacing to FlowDocument and all Paragraphs.</summary>
    public static void ApplyTightLineSpacing(FlowDocument doc)
    {
        doc.LineHeight = 13;
        doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        doc.PagePadding = new System.Windows.Thickness(0);

        foreach (var block in doc.Blocks)
        {
            if (block is Paragraph p)
            {
                p.LineHeight = 13;
                p.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                p.Margin = new System.Windows.Thickness(0);
                p.Padding = new System.Windows.Thickness(0);
            }
        }
    }

    private void AddTodo()
    {
        var item = new TodoItem { Content = "", Order = _note.NextTodoOrder };
        item.PropertyChanged += (_, _) => OnDataChanged();
        Todos.Add(item);
        OnDataChanged();
        FocusTodoRequested?.Invoke(item);
    }

    private void DeleteTodo(TodoItem? item)
    {
        if (item is null) return;
        Todos.Remove(item);
        ReorderTodos();
        OnDataChanged();
    }

    public void ReorderTodos()
    {
        for (int i = 0; i < Todos.Count; i++)
            Todos[i].Order = i;
    }

    public void SyncWindowPosition(double left, double top, double width, double height)
    {
        _note.WindowLeft = left;
        _note.WindowTop = top;
        _note.WindowWidth = width;
        _note.WindowHeight = height;
        OnDataChanged();
    }

    private static void SortCollectionByOrder<T>(ObservableCollection<T> collection) where T : class
    {
        var sorted = collection.OrderBy(item =>
        {
            var prop = item.GetType().GetProperty("Order");
            return prop?.GetValue(item) is int o ? o : 0;
        }).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var currentIndex = collection.IndexOf(sorted[i]);
            if (currentIndex != i)
                collection.Move(currentIndex, i);
        }
    }

    private void OnDataChanged() => _onChanged();

    /// <summary>Notify that data has changed and trigger a save.</summary>
    public void NotifyDataChanged() => OnDataChanged();
}
