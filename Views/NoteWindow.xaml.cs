using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Linq;
using System.Windows.Media;
using QuickNote.Models;
using QuickNote.ViewModels;

namespace QuickNote.Views;

public partial class NoteWindow : Window
{
    private readonly NoteViewModel _vm;
    private Point _dragStartPoint;
    private object? _draggedItem;
    private bool _isDragging;
    private InsertionAdorner? _insertionAdorner;
    private DragSourceAdorner? _dragSourceAdorner;
    private ContentPresenter? _dragSourceContainer;

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
            var container = TodoList.ItemContainerGenerator.ContainerFromItem(_draggedItem) as ContentPresenter;
            ShowDragVisuals(container);
            DragDrop.DoDragDrop(itemsControl, _draggedItem, DragDropEffects.Move);
            ClearDragVisuals();
            RemoveInsertionAdorner();
            _isDragging = false;
            _draggedItem = null;
        }
    }

    private void TodoList_DragOver(object sender, DragEventArgs e)
    {
        if (!_isDragging || _draggedItem is not TodoItem draggedTodo)
        {
            // 非待办拖拽（如往便笺区拖文本）不拦截，保留控件默认拖放行为
            RemoveInsertionAdorner();
            return;
        }
        e.Handled = true;

        var oldIndex = _vm.Todos.IndexOf(draggedTodo);
        var (insertIndex, lineY) = GetInsertionInfo(e, oldIndex);

        // 插入位置与当前位置相同，等于没拖，隐藏指示线
        if (insertIndex == oldIndex || insertIndex == oldIndex + 1)
        {
            e.Effects = DragDropEffects.None;
            RemoveInsertionAdorner();
            return;
        }

        e.Effects = DragDropEffects.Move;
        ShowInsertionAdorner(lineY);
    }

    private void TodoList_DragLeave(object sender, DragEventArgs e)
    {
        // e.GetPosition 在 DragLeave 中是过期坐标（OLE 回调无位置参数），
        // 改用 GetCursorPos 取实时光标位置同步判断：仍在响应区内就是子元素间穿越，
        // 不动 Adorner（避免销毁重建导致闪烁）；真离开才立即移除
        if (!GetCursorPos(out var pt))
        {
            RemoveInsertionAdorner();
            return;
        }
        var pos = OuterBorder.PointFromScreen(new Point(pt.X, pt.Y));
        if (pos.X < 0 || pos.Y < 0 || pos.X >= OuterBorder.ActualWidth || pos.Y >= OuterBorder.ActualHeight)
            RemoveInsertionAdorner();
    }

    private void TodoList_Drop(object sender, DragEventArgs e)
    {
        if (!_isDragging || _draggedItem is not TodoItem draggedTodo)
        {
            // 非待办拖拽不拦截
            RemoveInsertionAdorner();
            return;
        }
        e.Handled = true;
        RemoveInsertionAdorner();

        var list = _vm.Todos;
        var oldIndex = list.IndexOf(draggedTodo);
        if (oldIndex < 0) return;

        var (insertIndex, _) = GetInsertionInfo(e, oldIndex);
        if (insertIndex == oldIndex || insertIndex == oldIndex + 1) return;
        if (oldIndex < insertIndex) insertIndex--;

        list.Move(oldIndex, insertIndex);
        _vm.ReorderTodos();
    }

    /// <summary>
    /// 根据鼠标位置计算插入索引及指示线的 Y 坐标（相对 TodoList）：
    /// 小幅度门限——鼠标在上邻居中线与下邻居中线之间时视为无法移动，
    /// 返回原位索引（调用方据此显示禁止标识）；门限之外，在所有可移动目标位置中
    /// 取与鼠标纵向距离最近者作为落点。
    /// </summary>
    private (int insertIndex, double lineY) GetInsertionInfo(DragEventArgs e, int oldIndex)
    {
        var pos = e.GetPosition(TodoList);
        int count = TodoList.Items.Count;
        if (count == 0) return (oldIndex, 0);

        // 索引 k 的指示线位置：k < count 时为第 k 项顶边，k == count 时为末项底边
        var lineYs = new double[count + 1];
        var mids = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (TodoList.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter container)
                return (oldIndex, 0);

            double top = container.TranslatePoint(new Point(0, 0), TodoList).Y;
            double height = container.ActualHeight;
            lineYs[i] = top;
            mids[i] = top + height / 2;
            if (i == count - 1)
                lineYs[count] = top + height;
        }

        // 小幅度门限：上邻居中线 ~ 下邻居中线，无邻居的一侧延伸到无穷远
        double upperBound = oldIndex > 0 ? mids[oldIndex - 1] : double.NegativeInfinity;
        double lowerBound = oldIndex >= 0 && oldIndex < count - 1 ? mids[oldIndex + 1] : double.PositiveInfinity;
        if (pos.Y >= upperBound && pos.Y <= lowerBound)
            return (oldIndex, 0);

        // 门限之外：在所有可移动目标位置中取纵向距离最近者
        int best = -1;
        double bestDist = double.MaxValue;
        for (int k = 0; k <= count; k++)
        {
            if (k == oldIndex || k == oldIndex + 1) continue; // 无操作位置不参与
            double dist = Math.Abs(pos.Y - lineYs[k]);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = k;
            }
        }

        return best < 0 ? (oldIndex, 0) : (best, lineYs[best]);
    }

    private void ShowInsertionAdorner(double lineY)
    {
        if (_insertionAdorner is null)
        {
            // 取窗口级 AdornerLayer（越过 ScrollViewer 内部图层），避免边缘位置被滚动视口裁剪
            var layer = AdornerLayer.GetAdornerLayer(OuterBorder);
            if (layer is null) return;
            _insertionAdorner = new InsertionAdorner(TodoList);
            layer.Add(_insertionAdorner);
        }
        _insertionAdorner.LineY = lineY;
    }

    private void RemoveInsertionAdorner()
    {
        if (_insertionAdorner is null) return;
        AdornerLayer.GetAdornerLayer(OuterBorder)?.Remove(_insertionAdorner);
        _insertionAdorner = null;
    }

    /// <summary>
    /// 拖拽开始时的源条目视觉标记：淡化源条目，并给其文字部分套虚线框。
    /// </summary>
    private void ShowDragVisuals(ContentPresenter? container)
    {
        if (container is null) return;
        var layer = AdornerLayer.GetAdornerLayer(OuterBorder);
        if (layer is null) return;

        _dragSourceContainer = container;
        container.Opacity = 0.4;

        // 虚线框只框住文字内容：优先取显示文本的 TextBlock，找不到则退回整个条目
        UIElement target = FindContentTextBlock(container) ?? (UIElement)container;
        _dragSourceAdorner = new DragSourceAdorner(target);
        layer.Add(_dragSourceAdorner);
    }

    /// <summary>
    /// 在条目视觉树中找显示待办文本的 TextBlock：可见且可命中（排除 placeholder），
    /// 且不在按钮内部（排除删除按钮的文本）。
    /// </summary>
    private static TextBlock? FindContentTextBlock(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb && tb.Visibility == Visibility.Visible &&
                tb.IsHitTestVisible && tb.FindVisualParent<System.Windows.Controls.Button>() is null)
                return tb;
            if (FindContentTextBlock(child) is TextBlock found)
                return found;
        }
        return null;
    }

    private void ClearDragVisuals()
    {
        if (_dragSourceAdorner is not null)
        {
            AdornerLayer.GetAdornerLayer(OuterBorder)?.Remove(_dragSourceAdorner);
            _dragSourceAdorner = null;
        }
        if (_dragSourceContainer is not null)
        {
            _dragSourceContainer.Opacity = 1.0;
            _dragSourceContainer = null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }
}

/// <summary>
/// 拖拽排序时的插入位置指示线：一条水平线，两端带小三角。
/// 绘制在 AdornerLayer 上，不参与布局和命中测试。
/// </summary>
internal sealed class InsertionAdorner : Adorner
{
    private static readonly Pen LinePen;
    private static readonly SolidColorBrush LineBrush;
    private double _lineY;

    static InsertionAdorner()
    {
        LineBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8B, 0x7D, 0x5E));
        LineBrush.Freeze();
        LinePen = new Pen(LineBrush, 2);
        LinePen.Freeze();
    }

    public InsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public double LineY
    {
        get => _lineY;
        set
        {
            if (Math.Abs(_lineY - value) < 0.5) return;
            _lineY = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = AdornedElement.RenderSize.Width;
        dc.DrawLine(LinePen, new Point(4, _lineY), new Point(width - 4, _lineY));
        dc.DrawGeometry(LineBrush, null, CreateTriangle(4, _lineY, pointRight: true));
        dc.DrawGeometry(LineBrush, null, CreateTriangle(width - 4, _lineY, pointRight: false));
    }

    private static StreamGeometry CreateTriangle(double x, double y, bool pointRight)
    {
        double dir = pointRight ? 1 : -1;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, y - 4), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(x + dir * 5, y), false, false);
            ctx.LineTo(new Point(x, y + 4), false, false);
        }
        geometry.Freeze();
        return geometry;
    }
}

/// <summary>
/// 标记拖拽源条目的虚线圆角框，配合源条目淡化表示“正在被拖动”。
/// </summary>
internal sealed class DragSourceAdorner : Adorner
{
    private static readonly Pen DashPen;

    static DragSourceAdorner()
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8B, 0x7D, 0x5E));
        brush.Freeze();
        DashPen = new Pen(brush, 1.5) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) };
        DashPen.Freeze();
    }

    public DragSourceAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var el = (FrameworkElement)AdornedElement;
        // DesiredSize 是内容自然尺寸（含 Margin），据此收缩到实际文字宽高，不随列拉伸到行尾
        double width = Math.Min(
            Math.Max(el.DesiredSize.Width - el.Margin.Left - el.Margin.Right, 16),
            el.RenderSize.Width);
        double height = Math.Min(
            el.DesiredSize.Height - el.Margin.Top - el.Margin.Bottom,
            el.RenderSize.Height);
        var rect = new Rect(-3, -1, width + 6, height + 2);
        dc.DrawRoundedRectangle(null, DashPen, rect, 3, 3);
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
