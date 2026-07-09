using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickNote.Models;

public class AppSettings : INotifyPropertyChanged
{
    private bool _autoStart;
    private bool _trayIconVisible = true;
    private string _defaultColor = "Yellow";

    public bool AutoStart
    {
        get => _autoStart;
        set { _autoStart = value; OnPropertyChanged(); }
    }

    public bool TrayIconVisible
    {
        get => _trayIconVisible;
        set { _trayIconVisible = value; OnPropertyChanged(); }
    }

    public string DefaultColor
    {
        get => _defaultColor;
        set { _defaultColor = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Predefined color themes for note windows.
/// Each theme defines Background, TitleBar, BottomBar, Border, and TextAccent colors.
/// </summary>
public static class NoteColorThemes
{
    public static readonly Dictionary<string, NoteColorTheme> All = new()
    {
        ["Yellow"] = new("#FFF9E6", "#FFE8A0", "#FFF0C8", "#E0D5A0", "#5A4E2F"),
        ["Blue"] = new("#E8F4FD", "#B8D9F0", "#D0E8F8", "#A0C8E0", "#2F4A5A"),
        ["Pink"] = new("#FDE8EF", "#F0B8C8", "#F8D0DC", "#E0A0B8", "#5A2F3F"),
        ["Green"] = new("#E8FDE8", "#B8F0B8", "#D0F8D0", "#A0E0A0", "#2F5A2F"),
        ["Gray"] = new("#F0F0F0", "#D8D8D8", "#E8E8E8", "#C0C0C0", "#3A3A3A"),
    };

    public static NoteColorTheme Get(string name)
        => All.TryGetValue(name, out var theme) ? theme : All["Yellow"];
}

public record NoteColorTheme(
    string Background,
    string TitleBar,
    string BottomBar,
    string Border,
    string TextAccent);
