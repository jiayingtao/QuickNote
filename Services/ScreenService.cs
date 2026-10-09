using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace QuickNote.Services;

/// <summary>
/// 多显示器屏幕检测：判断窗口是否仍落在某个显示器的可见区域内。
/// Windows 更新、分辨率/DPI 缩放变化或显示器增减后，持久化的窗口坐标
/// 可能落到所有屏幕之外，导致窗口"隐形"（不在任务栏时尤其无从察觉）。
/// </summary>
public static class ScreenService
{
    /// <summary>窗口与任一显示器的交叠面积低于窗口面积此比例时，视为在屏幕外。</summary>
    private const double VisibleRatioThreshold = 0.25;

    /// <summary>
    /// 窗口是否仍可见地落在某个显示器上：逐个枚举所有显示器（而非仅主屏
    /// 或多屏外包围盒），与任意一个显示器的交叠面积达到阈值即视为在屏上。
    /// </summary>
    public static bool IsOnAnyScreen(Window window)
    {
        var rect = GetWindowRect(window);
        if (rect.Width <= 0 || rect.Height <= 0) return true;
        double area = rect.Width * rect.Height;

        foreach (var monitor in EnumerateMonitorRects(window))
        {
            var overlap = Rect.Intersect(rect, monitor.Rect);
            if (!overlap.IsEmpty && overlap.Width * overlap.Height / area >= VisibleRatioThreshold)
                return true;
        }
        return false;
    }

    /// <summary>把屏幕外的窗口移回主屏可见区域（保留窗口尺寸，四周留 10px 边距）。</summary>
    public static void MoveToPrimaryScreen(Window window)
    {
        var target = EnumerateMonitorRects(window).FirstOrDefault(m => m.IsPrimary).Rect;
        if (target.IsEmpty) return;

        var rect = GetWindowRect(window);
        const double margin = 10;

        window.Left = Math.Clamp(rect.Left,
            target.Left + margin, Math.Max(target.Left + margin, target.Right - rect.Width - margin));
        window.Top = Math.Clamp(rect.Top,
            target.Top + margin, Math.Max(target.Top + margin, target.Bottom - rect.Height - margin));
    }

    private static Rect GetWindowRect(Window window)
    {
        double scale = GetTransformToDevice(window);
        return new Rect(window.Left * scale, window.Top * scale,
            window.Width * scale, window.Height * scale);
    }

    /// <summary>WPF 窗口坐标是 DIP，显示器枚举坐标是（虚拟化后的）物理像素，按窗口的 DPI 缩放换算。</summary>
    private static double GetTransformToDevice(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        return source?.CompositionTarget is CompositionTarget target
            ? target.TransformToDevice.M11
            : 1.0;
    }

    private static List<MonitorBounds> EnumerateMonitorRects(Window window)
    {
        double scale = GetTransformToDevice(window);
        var result = new List<MonitorBounds>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    result.Add(new MonitorBounds(
                        new Rect(rect.Left * scale, rect.Top * scale,
                            (rect.Right - rect.Left) * scale, (rect.Bottom - rect.Top) * scale),
                        (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
                }
                return true;
            }, IntPtr.Zero);

        return result;
    }

    private readonly record struct MonitorBounds(Rect Rect, bool IsPrimary);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lpRect, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    private const int MONITORINFOF_PRIMARY = 0x1;
}
