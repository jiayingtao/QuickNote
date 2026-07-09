using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using QuickNote.Models;
using QuickNote.Services;
using QuickNote.ViewModels;
using QuickNote.Views;

// Explicit alias: Application refers to WPF's Application
using WpfApplication = System.Windows.Application;

namespace QuickNote;

public partial class App : WpfApplication
{
    private const string MutexName = "QuickNote_SingleInstance_Mutex";
    private const string WakeEventName = "QuickNote_WakeUp_Event";

    private MainViewModel _mainVm = null!;
    private DataService _dataService = null!;
    private SettingsService _settingsService = null!;
    private AppSettings _appSettings = null!;
    private NotifyIcon? _trayIcon;
    private ToolStripItem? _showAllMenuItem;
    private readonly Dictionary<Note, NoteWindow> _windows = new();
    private Mutex? _mutex;
    private EventWaitHandle? _wakeEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance check via Mutex
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is running – wake it up and exit
            try
            {
                using var wakeEvent = EventWaitHandle.OpenExisting(WakeEventName);
                wakeEvent.Set();
            }
            catch
            {
                // Ignore if the event doesn't exist
            }
            Shutdown();
            return;
        }

        // First instance: create wake-up event and start listener
        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        new Thread(ListenForWakeUp) { IsBackground = true }.Start();

        base.OnStartup(e);

        _dataService = new DataService();
        _mainVm = new MainViewModel(_dataService);
        _settingsService = new SettingsService();
        _appSettings = _settingsService.Load();

        SetupTrayIcon();

        if (_mainVm.Notes.Count == 0)
        {
            _mainVm.CreateNote();
        }

        foreach (var note in _mainVm.Notes.ToList())
        {
            ShowNoteWindow(note);
        }
    }

    private void ListenForWakeUp()
    {
        while (_wakeEvent is not null)
        {
            try
            {
                _wakeEvent.WaitOne();
                Dispatcher.Invoke(() => ShowAllWindows());
            }
            catch
            {
                break;
            }
        }
    }

    private void SetupTrayIcon()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("QuickNote.Assets.app.ico");
        _trayIcon = new NotifyIcon
        {
            Text = "QuickNote",
            Icon = new Icon(stream!),
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("新建便签", null, (_, _) => CreateAndShowNewNote());
        _showAllMenuItem = contextMenu.Items.Add("显示所有", null, (_, _) => ShowAllWindows());
        contextMenu.Items.Add("隐藏所有", null, (_, _) => HideAllWindows());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("设置", null, (_, _) => OpenSettings());
        contextMenu.Items.Add("退出", null, (_, _) => ExitApp());

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (_, _) => ShowAllWindows();

        UpdateTrayCount();
    }

    private void UpdateTrayCount()
    {
        if (_showAllMenuItem is null) return;
        var count = _mainVm.Notes.Count;
        _showAllMenuItem.Text = $"显示所有 ({count})";
    }

    private void ShowNoteWindow(Note note)
    {
        if (_windows.ContainsKey(note)) return;

        var vm = _mainVm.CreateNoteViewModel(note);
        var window = new NoteWindow(vm);
        window.OnRequestNewNote = CreateAndShowNewNote;
        window.OnRequestDelete = DeleteNoteWindow;

        _windows[note] = window;
        window.Show();
    }

    private void CreateAndShowNewNote(string colorTheme = "Yellow")
    {
        var note = _mainVm.CreateNote();
        note.ColorTheme = colorTheme;
        note.WindowLeft = 100 + _mainVm.Notes.Count * 40;
        note.WindowTop = 100 + _mainVm.Notes.Count * 40;
        ShowNoteWindow(note);
        UpdateTrayCount();
    }

    private void ShowAllWindows()
    {
        var visibleWindows = _windows.Values
            .Where(w => w.IsVisible && w.WindowState != WindowState.Minimized)
            .ToList();

        foreach (var (note, window) in _windows)
        {
            bool wasHidden = !window.IsVisible || window.WindowState == WindowState.Minimized;

            if (!window.IsVisible)
                window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            if (wasHidden)
            {
                while (OverlapsAny(window, visibleWindows))
                {
                    window.Left += 40;
                    window.Top += 40;
                }
                visibleWindows.Add(window);
            }

            window.Activate();
        }
    }

    private static bool OverlapsAny(NoteWindow win, List<NoteWindow> others)
    {
        const double threshold = 20;
        foreach (var other in others)
        {
            if (ReferenceEquals(other, win)) continue;
            if (Math.Abs(win.Left - other.Left) < threshold &&
                Math.Abs(win.Top - other.Top) < threshold)
                return true;
        }
        return false;
    }

    private void HideAllWindows()
    {
        foreach (var (_, window) in _windows)
        {
            window.Hide();
        }
    }

    private void OpenSettings()
    {
        foreach (System.Windows.Window window in WpfApplication.Current.Windows)
        {
            if (window is SettingsWindow settingsWin)
            {
                settingsWin.Activate();
                return;
            }
        }

        var settingsWindow = new SettingsWindow(_appSettings, _settingsService);
        settingsWindow.Closed += (_, _) =>
        {
            _appSettings = _settingsService.Load();
        };
        settingsWindow.Show();
    }

    private void DeleteNoteWindow(NoteWindow window)
    {
        window.Close();
        var vm = (NoteViewModel)window.DataContext;
        var note = vm.Model;
        _mainVm.DeleteNote(note);
        _windows.Remove(note);
        UpdateTrayCount();
    }

    private void ExitApp()
    {
        _trayIcon!.Visible = false;
        _mainVm.SaveNow();
        foreach (var window in _windows.Values)
            window.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _mainVm?.SaveNow();
        _mutex?.Dispose();
        _wakeEvent?.Dispose();
        base.OnExit(e);
    }
}
