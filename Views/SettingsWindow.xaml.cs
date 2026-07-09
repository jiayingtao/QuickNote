using System.Windows;
using System.Windows.Input;
using QuickNote.Models;
using QuickNote.Services;

namespace QuickNote.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;

    public SettingsWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        AutoStartCheckBox.IsChecked = settings.AutoStart;
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Will be applied on save
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoStart = AutoStartCheckBox.IsChecked == true;

        _settingsService.Save(_settings);
        _settingsService.ApplyAutoStart(_settings.AutoStart);

        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
