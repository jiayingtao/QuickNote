using QuickNote.Models;
using QuickNote.Services;

namespace QuickNote.ViewModels;

public class MainViewModel
{
    private readonly DataService _dataService;
    private readonly List<Note> _notes;

    public MainViewModel(DataService dataService)
    {
        _dataService = dataService;
        _notes = dataService.Load();
    }

    public List<Note> Notes => _notes;

    public NoteViewModel CreateNoteViewModel(Note note)
        => new(note, RequestSave);

    public Note CreateNote()
    {
        var note = new Note
        {
            Name = "便签",
            WindowLeft = 100 + _notes.Count * 30,
            WindowTop = 100 + _notes.Count * 30
        };
        _notes.Add(note);
        RequestSave();
        return note;
    }

    public void DeleteNote(Note note)
    {
        _notes.Remove(note);
        RequestSave();
    }

    public void RequestSave() => _dataService.ScheduleSave(_notes);

    public void SaveNow() => _dataService.SaveNow();
}
