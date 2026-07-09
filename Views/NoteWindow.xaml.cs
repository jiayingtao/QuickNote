using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Windows.Media;
using QuickNote.Models;
using QuickNote.ViewModels;

using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace QuickNote.Views;

public partial class NoteWindow : Window
{
    private readonly NoteViewModel _vm;
    private Point _dragStartPoint;
    private object? _draggedItem;
    private bool _isDragging;

    public Action<string>? OnRequestNewNote { get; set; }
    public Action<NoteWindow>? OnRequestDelete { get; set; }

    private TodoItem? _pendingFocusTodo;

    public NoteWindow(NoteViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        Left = _vm.Model.WindowLeft;
        Top = _vm.Model.WindowTop;
        Width = _vm.Model.WindowWidth;
        Height = _vm.Model.WindowHeight;

        ApplyColorTheme(_vm.Model.ColorTheme);

        _vm.FocusTodoRequested += item => _pendingFocusTodo = item;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteViewModel.IsNoteVisible))
            {
                UpdateNoteAreaVisibility();
                if (_vm.IsNoteVisible)
                    Dispatcher.BeginInvoke(() => ApplySplitRatio(),
                        System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        UpdateNoteAreaVisibility();
        ApplySplitRatio();
    }

    private void UpdateNoteAreaVisibility()
    {
        if (_vm.IsNoteVisible)
        {
            SplitterRow.Height = new GridLength(5);
            NoteAreaRow.MinHeight = 80;
        }
        else
        {
            SplitterRow.Height = new GridLength(0);
            NoteAreaRow.Height = new GridLength(0);
            NoteAreaRow.MinHeight = 0;
        }
    }

    private void ApplySplitRatio()
    {
        if (!_vm.IsNoteVisible) return;
        double ratio = _vm.NoteSplitRatio;
        double total = TodoAreaRow.ActualHeight + NoteAreaRow.ActualHeight;
        if (total <= 0) return;
        // Use Star multipliers to apply the stored ratio
        double todoStar = 1.0 - ratio;
        double noteStar = ratio;
        if (todoStar < 0.1) todoStar = 0.1;
        if (noteStar < 0.1) noteStar = 0.1;
        TodoAreaRow.Height = new GridLength(todoStar, GridUnitType.Star);
        NoteAreaRow.Height = new GridLength(noteStar, GridUnitType.Star);
    }

    private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        double total = TodoAreaRow.ActualHeight + NoteAreaRow.ActualHeight;
        if (total <= 0) return;
        _vm.NoteSplitRatio = NoteAreaRow.ActualHeight / total;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void NameTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _vm.IsEditingName = true;
            Dispatcher.BeginInvoke(() =>
            {
                NameEditBox.Focus();
                NameEditBox.SelectAll();
            });
        }
    }

    private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.IsEditingName = false;
            Keyboard.ClearFocus();
        }
    }

    private void NameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _vm.IsEditingName = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"确定要删除便签「{_vm.Name}」吗？此操作不可恢复。",
            "删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            OnRequestDelete?.Invoke(this);
        }
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string colorName)
        {
            _vm.Model.ColorTheme = colorName;
            ApplyColorTheme(colorName);
            _vm.NotifyDataChanged();
        }
    }

    private void TodoItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is System.Windows.Controls.TextBlock tb
            && tb.DataContext is TodoItem todo && !todo.IsEditing)
        {
            todo.IsEditing = true;
            e.Handled = true;

            if (tb.Parent is System.Windows.Controls.Panel panel)
            {
                var editBox = panel.Children.OfType<System.Windows.Controls.TextBox>()
                    .FirstOrDefault();
                if (editBox != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Keyboard.Focus(editBox);
                        editBox.CaretIndex = editBox.Text.Length;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }
    }

    private void TodoTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb
            && tb.DataContext is TodoItem todo
            && todo == _pendingFocusTodo)
        {
            _pendingFocusTodo = null;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Keyboard.Focus(tb);
                tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TodoTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is TodoItem todo)
            todo.IsEditing = false;
    }

    private void TodoTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is TodoItem todo)
            {
                todo.IsEditing = false;
                e.Handled = true;
            }
            return;
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (sender is System.Windows.Controls.TextBox tb && !string.IsNullOrEmpty(tb.Text))
            {
                if (tb.DataContext is TodoItem currentItem)
                    currentItem.IsEditing = false;
                _vm.AddTodoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void NoteEditor_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.LoadDocumentTo(NoteEditor);
        _vm.RegisterEditor(NoteEditor);
    }

    private void NoteEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.SaveDocument(NoteEditor.Document);
    }

    private void NewNoteButton_Click(object sender, RoutedEventArgs e)
    {
        OnRequestNewNote?.Invoke(_vm.Model.ColorTheme);
    }

    public void ApplyColorTheme(string themeName)
    {
        var theme = NoteColorThemes.Get(themeName);

        var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Background)!);
        var titleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TitleBar)!);
        var bottomBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.BottomBar)!);
        var borderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Border)!);
        var textBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TextAccent)!);

        OuterBorder.Background = bgBrush;
        OuterBorder.BorderBrush = borderBrush;
        TitleBarBorder.Background = titleBrush;
        BottomBarBorder.Background = bottomBrush;
        Splitter.Background = borderBrush;

        OuterBorder.Tag = textBrush;
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        _vm.SyncWindowPosition(Left, Top, Width, Height);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _vm.SyncWindowPosition(Left, Top, Width, Height);
    }

    private void TodoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject).FindVisualParent<System.Windows.Controls.CheckBox>() is not null)
        {
            _draggedItem = null;
            return;
        }
        if ((e.OriginalSource as DependencyObject).FindVisualParent<System.Windows.Controls.TextBox>() is not null)
        {
            _draggedItem = null;
            return;
        }
        _dragStartPoint = e.GetPosition(null);
        _draggedItem = (e.OriginalSource as DependencyObject).FindVisualParent<ContentPresenter>()?.DataContext;
    }

    private void TodoList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _draggedItem = null;
    }

    private void TodoList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem is null) return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDragging = true;
            var itemsControl = sender as ItemsControl;
            if (itemsControl is null) return;
            DragDrop.DoDragDrop(itemsControl, _draggedItem, DragDropEffects.Move);
            _isDragging = false;
            _draggedItem = null;
        }
    }

    private void TodoList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void TodoList_Drop(object sender, DragEventArgs e)
    {
        if (!_isDragging || _draggedItem is not TodoItem draggedTodo) return;

        var targetItem = (e.OriginalSource as DependencyObject)
            .FindVisualParent<ContentPresenter>()?.DataContext as TodoItem;

        if (targetItem is null || targetItem == draggedTodo) return;

        var list = _vm.Todos;
        var oldIndex = list.IndexOf(draggedTodo);
        var newIndex = list.IndexOf(targetItem);

        if (oldIndex < 0 || newIndex < 0) return;

        list.Move(oldIndex, newIndex);
        _vm.ReorderTodos();
    }
}

internal static class DependencyObjectExtensions
{
    public static T? FindVisualParent<T>(this DependencyObject? child) where T : DependencyObject
    {
        while (child is not null and not T)
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        return child as T;
    }
}
