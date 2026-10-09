using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using QuickNote.Models;
using QuickNote.Services;
using QuickNote.ViewModels;
using QuickNote.Views;

namespace QuickNote;

public partial class App : Application
{
    private const string MutexName = "QuickNote_SingleInstance_Mutex";
    private const string WakeEventName = "QuickNote_WakeUp_Event";

    private MainViewModel _mainVm = null!;
    private DataService _dataService = null!;
    private SettingsService _settingsService = null!;
    private AppSettings _appSettings = null!;
    private TaskbarIcon? _trayIcon;
    private MenuItem? _showAllMenuItem;
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
        var iconImage = new System.Windows.Media.Imaging.BitmapImage();
        iconImage.BeginInit();
        iconImage.StreamSource = stream;
        iconImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        iconImage.EndInit();

        var contextMenu = new ContextMenu();

        var newNoteItem = new MenuItem { Header = "新建便签" };
        newNoteItem.Click += (_, _) => CreateAndShowNewNote();
        contextMenu.Items.Add(newNoteItem);

        _showAllMenuItem = new MenuItem();
        _showAllMenuItem.Click += (_, _) => ShowAllWindows();
        contextMenu.Items.Add(_showAllMenuItem);

        var hideAllItem = new MenuItem { Header = "隐藏所有" };
        hideAllItem.Click += (_, _) => HideAllWindows();
        contextMenu.Items.Add(hideAllItem);

        contextMenu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "设置" };
        settingsItem.Click += (_, _) => OpenSettings();
        contextMenu.Items.Add(settingsItem);

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitApp();
        contextMenu.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "QuickNote",
            IconSource = iconImage,
            ContextMenu = contextMenu
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowAllWindows();
        _trayIcon.TrayLeftMouseDown += (_, _) => ShowAllWindows();

        UpdateTrayCount();

        EnsureTrayIconPromoted();
    }

    /// <summary>
    /// Windows may forget the tray icon visibility preference when the app auto-starts
    /// before Explorer fully initializes. This method waits briefly for the registry entry
    /// to be created, then restores the IsPromoted value from AppSettings (persisted on exit).
    /// </summary>
    private void EnsureTrayIconPromoted()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(500);
            try
            {
                var exePath = Environment.ProcessPath ?? "";
                using var baseKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Control Panel\NotifyIconSettings", writable: false);
                if (baseKey is null) return;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    using var subKey = baseKey.OpenSubKey(subKeyName, writable: true);
                    if (subKey is null) continue;

                    var path = subKey.GetValue("ExecutablePath") as string;
                    if (string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        var value = _appSettings.TrayIconVisible ? 1 : 0;
                        subKey.SetValue("IsPromoted", value, Microsoft.Win32.RegistryValueKind.DWord);
                        return;
                    }
                }
            }
            catch
            {
                // Best-effort: silently ignore if registry access fails
            }
        });
    }

    private void UpdateTrayCount()
    {
        if (_showAllMenuItem is null) return;
        var count = _mainVm.Notes.Count;
        _showAllMenuItem.Header = $"显示所有 ({count})";
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

        // 显示器配置变化（系统更新、分辨率/DPI 调整、拔插屏）可能使保存的坐标落到屏幕外
        if (!ScreenService.IsOnAnyScreen(window))
            ScreenService.MoveToPrimaryScreen(window);
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
        // 屏幕外窗口（显示器配置变化所致）先拉回主屏，再统一显示/错开/激活，
        // 否则窗口 IsVisible 为 true，下面只会 Activate 而不移动，用户看起来就是"没反应"
        foreach (var window in _windows.Values)
        {
            if (window.IsVisible && !ScreenService.IsOnAnyScreen(window))
                ScreenService.MoveToPrimaryScreen(window);
        }

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
        foreach (System.Windows.Window window in Application.Current.Windows)
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
        SaveTrayIconState();
        _mainVm.SaveNow();
        foreach (var window in _windows.Values)
            window.Close();
        Shutdown();
    }

    /// <summary>
    /// Read the current tray icon visibility from Windows registry and persist it
    /// to AppSettings, so the same state can be restored on next launch.
    /// </summary>
    private void SaveTrayIconState()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";
            using var baseKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\NotifyIconSettings", writable: false);
            if (baseKey is null) return;

            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                using var subKey = baseKey.OpenSubKey(subKeyName, writable: false);
                if (subKey is null) continue;

                var path = subKey.GetValue("ExecutablePath") as string;
                if (string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                {
                    var promoted = subKey.GetValue("IsPromoted") is int val ? val : 1;
                    _appSettings.TrayIconVisible = promoted != 0;
                    _settingsService.Save(_appSettings);
                    return;
                }
            }
        }
        catch
        {
            // Best-effort: silently ignore if registry access fails
        }
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
