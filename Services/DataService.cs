using System.IO;
using System.Text.Json;
using QuickNote.Models;

namespace QuickNote.Services;

public class DataService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickNote");
    private static readonly string DataFile = Path.Combine(DataDir, "notes.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;

    public DataService()
    {
        _debounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            SaveNow();
        };
    }

    public List<Note> Load()
    {
        if (!File.Exists(DataFile))
            return new List<Note>();

        try
        {
            var json = File.ReadAllText(DataFile);
            return JsonSerializer.Deserialize<List<Note>>(json, JsonOptions) ?? new List<Note>();
        }
        catch (Exception)
        {
            return new List<Note>();
        }
    }

    public void ScheduleSave(List<Note> notes)
    {
        _pendingNotes = notes;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private List<Note>? _pendingNotes;

    public void SaveNow()
    {
        if (_pendingNotes is null) return;
        _debounceTimer.Stop();
        try
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            var json = JsonSerializer.Serialize(_pendingNotes, JsonOptions);
            File.WriteAllText(DataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
        }
    }
}
