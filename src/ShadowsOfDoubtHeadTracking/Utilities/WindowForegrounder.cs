// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ShadowsOfDoubtHeadTracking.Utilities;

/// <summary>
/// Places the game's render window at startup: centred on its monitor, and in
/// front of whatever the player had open. Shadows of Doubt regularly finishes
/// loading behind other windows, and in windowed mode it opens against the
/// top-left of the desktop.
///
/// The window is found by class name rather than through
/// <c>Process.MainWindowHandle</c>: with BepInEx's console enabled the console is
/// usually the process's main window, so the handle-based route foregrounds a log
/// window and leaves the game behind it.
///
/// Windows refuses SetForegroundWindow from a process that is not already
/// foreground (it silently no-ops), so this uses the long-standing
/// AttachThreadInput plus topmost-toggle dance that survives the
/// focus-stealing-prevention rules.
/// </summary>
public static class WindowForegrounder
{
    private const string UnityWindowClass = "UnityWndClass";

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint MONITOR_DEFAULTTONEAREST = 0x0002;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int bufferSize);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);

    [DllImport("user32.dll")]
    private static extern bool SetFocus(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    /// <summary>
    /// Locates the render window. Returns <c>IntPtr.Zero</c> when it does not
    /// exist yet, so the caller can keep retrying during startup.
    /// </summary>
    public static IntPtr FindRenderWindow()
    {
        uint processId = GetCurrentProcessId();
        IntPtr found = IntPtr.Zero;
        var className = new StringBuilder(64);

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint owner);
            if (owner != processId || !IsWindowVisible(hWnd)) return true;

            className.Length = 0;
            GetClassName(hWnd, className, className.Capacity);
            if (className.ToString() != UnityWindowClass) return true;

            found = hWnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>
    /// Centres the window on the work area of the monitor it currently occupies.
    /// A maximized window is left alone - it already fills the monitor, and
    /// moving it without resizing would push it off-screen.
    /// </summary>
    public static void CenterOnMonitor(IntPtr hWnd)
    {
        if (IsZoomed(hWnd)) return;

        // A failure here returns a zeroed rect, which reads as a zero-sized window
        // and centres it against a size it never had.
        if (!GetWindowRect(hWnd, out NativeRect window))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetWindowRect failed for the render window");
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST), ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetMonitorInfo failed for the render window's monitor");
        }

        int width = window.Right - window.Left;
        int height = window.Bottom - window.Top;
        int x = info.Work.Left + (info.Work.Right - info.Work.Left - width) / 2;
        int y = info.Work.Top + (info.Work.Bottom - info.Work.Top - height) / 2;

        // A window larger than the work area would otherwise land with its title
        // bar above the top of the screen, out of reach of the mouse.
        SetWindowPos(
            hWnd,
            IntPtr.Zero,
            Math.Max(x, info.Work.Left),
            Math.Max(y, info.Work.Top),
            0,
            0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Brings the render window forward and gives it keyboard focus.
    /// </summary>
    public static void BringToFront(IntPtr hWnd)
    {
        ShowWindowAsync(hWnd, IsIconic(hWnd) ? SW_RESTORE : SW_SHOW);

        // Topmost-toggle: forces the window to the top of the Z-order without
        // needing foreground rights. Topmost is dropped immediately so the window
        // does not stay pinned over everything else.
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        uint thisThread = GetCurrentThreadId();

        bool attached = foregroundThread != 0
            && foregroundThread != thisThread
            && AttachThreadInput(thisThread, foregroundThread, true);

        BringWindowToTop(hWnd);
        SetForegroundWindow(hWnd);
        SetFocus(hWnd);

        if (attached)
        {
            AttachThreadInput(thisThread, foregroundThread, false);
        }
    }
}
