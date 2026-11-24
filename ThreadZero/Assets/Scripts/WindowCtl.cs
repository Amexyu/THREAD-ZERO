using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 挂机小窗的窗口控制：无边框、置顶、摆到屏幕工作区的底部 / 右下角。
    /// Unity 自己只能改大小和位置（Screen.SetResolution / Screen.MoveMainWindowTo），无边框和置顶要调系统接口：
    /// Windows 走 user32；Mac 走 Objective-C 运行时（libobjc，不用另外编插件）。编辑器里什么都不做（不能把编辑器窗口改掉）。
    /// 任何一步失败都只记在 Status 里、不抛异常：最坏的结果是一个普通的有边框小窗口。
    public static class WindowCtl
    {
        public static string Status = "";
        public static bool Native
        {
            get
            {
#if UNITY_EDITOR
                return false;
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                return true;
#else
                return false;
#endif
            }
        }

        /// 当前窗口所在屏幕的可用区域（去掉任务栏 / 程序坞 / 菜单栏），单位和 Screen.SetResolution 一样（像素）。
        public static RectInt WorkArea(out DisplayInfo display)
        {
            display = default(DisplayInfo);
            try
            {
                display = Screen.mainWindowDisplayInfo;
                if (display.workArea.width > 0 && display.workArea.height > 0) return display.workArea;
                if (display.width > 0) return new RectInt(0, 0, display.width, display.height);
            }
            catch (Exception e) { Status = "workArea: " + e.Message; }
            return new RectInt(0, 0, Display.main.systemWidth, Display.main.systemHeight);
        }

        /// Windows 上大小和位置也由 Apply 一次摆好（SetWindowPos）；Mac 上交给 Unity 的 SetResolution / MoveMainWindowTo。
        public static bool PlacesWindow
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        /// 屏幕坐标的工作区：Windows 用系统的（多显示器、任务栏在哪边都对），别的平台用 Unity 的。
        public static RectInt ScreenWorkArea()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { RectInt r = WinWorkArea(); if (r.width > 0 && r.height > 0) return r; } catch (Exception) { }
#endif
            DisplayInfo d; return WorkArea(out d);
        }
        /// 无边框 / 置顶。Windows 顺便把窗口摆到 rect（屏幕坐标，像素，rect 是客户区）；Mac 由调用方再用 MoveMainWindowTo 摆位置。
        public static void Apply(bool borderless, bool topmost, RectInt screenRect)
        {
            if (!Native) { Status = "编辑器里不改窗口"; return; }
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                Win(borderless, topmost, screenRect);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                Mac(borderless, topmost);
#endif
            }
            catch (Exception e) { Status = "窗口设置失败：" + e.Message; Debug.LogWarning("THREAD ZERO: " + Status); }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool AdjustWindowRect(ref RECT r, uint style, bool menu);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L, WS_MINIMIZEBOX = 0x00020000L, WS_MAXIMIZEBOX = 0x00010000L, WS_SYSMENU = 0x00080000L;
        private const uint SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40, SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1;
        private static readonly IntPtr TOPMOST = new IntPtr(-1), NOTOPMOST = new IntPtr(-2);
        private static IntPtr hwnd;
        private static IntPtr Hwnd()
        {
            if (hwnd == IntPtr.Zero) hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) hwnd = FindWindow("UnityWndClass", Application.productName);
            if (hwnd == IntPtr.Zero) hwnd = FindWindow("UnityWndClass", null);
            return hwnd;
        }
        /// Windows：当前窗口所在显示器的工作区（像素，屏幕坐标）。拿不到就用 Unity 的。
        public static RectInt WinWorkArea()
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return new RectInt(0, 0, 0, 0);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(MonitorFromWindow(h, 2), ref info)) return new RectInt(0, 0, 0, 0);
            return new RectInt(info.rcWork.left, info.rcWork.top, info.rcWork.right - info.rcWork.left, info.rcWork.bottom - info.rcWork.top);
        }
        private static void Win(bool borderless, bool topmost, RectInt r)
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) { Status = "找不到窗口"; return; }
            long style = GetWindowLongPtr(h, GWL_STYLE).ToInt64();
            long frame = WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
            style = borderless ? style & ~frame : style | frame;
            SetWindowLongPtr(h, GWL_STYLE, new IntPtr(style));
            uint flags = SWP_FRAMECHANGED | SWP_SHOWWINDOW;
            if (r.width > 0 && r.height > 0)
            {   // 给的是客户区：有边框时按边框把整个窗口放大一圈
                var rc = new RECT { left = r.x, top = r.y, right = r.x + r.width, bottom = r.y + r.height };
                if (!borderless) AdjustWindowRect(ref rc, (uint)style, false);
                SetWindowPos(h, topmost ? TOPMOST : NOTOPMOST, rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top, flags);
            }
            else SetWindowPos(h, topmost ? TOPMOST : NOTOPMOST, 0, 0, 0, 0, flags | SWP_NOMOVE | SWP_NOSIZE);
            Status = (borderless ? "无边框" : "有边框") + (topmost ? " · 置顶" : "");
        }
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr target, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendIndex(IntPtr target, IntPtr selector, ulong index);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendLong(IntPtr target, IntPtr selector, long value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendULong(IntPtr target, IntPtr selector, ulong value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendBool(IntPtr target, IntPtr selector, byte value);
        private static IntPtr Sel(string name) { return sel_registerName(name); }
        private static IntPtr MacWindow()
        {
            IntPtr app = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
            if (app == IntPtr.Zero) return IntPtr.Zero;
            IntPtr w = Send(app, Sel("mainWindow"));
            if (w == IntPtr.Zero) w = Send(app, Sel("keyWindow"));
            if (w == IntPtr.Zero) { IntPtr list = Send(app, Sel("windows")); if (list != IntPtr.Zero && Send(list, Sel("count")) != IntPtr.Zero) w = SendIndex(list, Sel("objectAtIndex:"), 0); }
            return w;
        }
        private static void Mac(bool borderless, bool topmost)
        {
            IntPtr w = MacWindow(); if (w == IntPtr.Zero) { Status = "找不到窗口"; return; }
            // NSWindowStyleMask：无边框 0；平时 标题 1 | 关闭 2 | 最小化 4 | 可缩放 8
            SendULong(w, Sel("setStyleMask:"), borderless ? 0UL : 15UL);
            SendLong(w, Sel("setLevel:"), topmost ? 3 : 0);   // NSFloatingWindowLevel = 3，NSNormalWindowLevel = 0
            SendBool(w, Sel("setHasShadow:"), 1);
            SendBool(w, Sel("setMovableByWindowBackground:"), (byte)(borderless ? 1 : 0));
            Status = (borderless ? "无边框" : "有边框") + (topmost ? " · 置顶" : "");
        }
#endif
    }
}
