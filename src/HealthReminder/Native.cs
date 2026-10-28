using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Strongthium.HealthReminder;

/// <summary>
/// What WPF leaves to Windows: putting a window exactly over a screen or in the middle of one, in physical pixels
/// (WPF works in its own units, scaled per screen), keeping it on top, and flashing it in the taskbar.
/// </summary>
internal static partial class Native
{
    private static readonly IntPtr Topmost = new(-1);
    private const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010;

    public static IntPtr Handle(Window window) => new WindowInteropHelper(window).EnsureHandle();

    /// <summary>Puts <paramref name="window"/> over <paramref name="bounds"/> (physical pixels), on top; just under
    /// <paramref name="under"/> if given, so that one stays in front.</summary>
    public static void Cover(Window window, System.Drawing.Rectangle bounds, Window? under = null) =>
        SetWindowPos(Handle(window), under is null ? Topmost : Handle(under), bounds.X, bounds.Y, bounds.Width, bounds.Height, NoActivate);

    /// <summary>Moves the window to the middle of the working area of the screen it's on, and to the top.</summary>
    public static void Center(Window window)
    {
        var handle = Handle(window);
        if (!GetWindowRect(handle, out var rect)) return;
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(MonitorFromWindow(handle, 2), ref info)) return;   // MONITOR_DEFAULTTONEAREST
        var area = info.Work;
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        SetWindowPos(handle, Topmost, area.Left + Math.Max(0, (area.Right - area.Left - width) / 2),
            area.Top + Math.Max(0, (area.Bottom - area.Top - height) / 2), 0, 0, NoSize | NoActivate);
    }

    /// <summary>A window hook that swallows WM_DPICHANGED, so WPF leaves a window's size and place to us.</summary>
    public static IntPtr IgnoreDpiChanges(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x02E0) handled = true;
        return IntPtr.Zero;
    }

    /// <summary>Puts the window back at the top of the topmost windows.</summary>
    public static void BringToTop(Window window) => SetWindowPos(Handle(window), Topmost, 0, 0, 0, 0, NoSize | NoMove);

    /// <summary>Flashes the caption and the taskbar button until the window is in front.</summary>
    public static void Flash(Window window)
    {
        var info = new FlashInfo { Size = (uint)Marshal.SizeOf<FlashInfo>(), Window = Handle(window), Flags = 3 | 12 };   // FLASHW_ALL | FLASHW_TIMERNOFG
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr window, out Rect rect);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashInfo info);
}
