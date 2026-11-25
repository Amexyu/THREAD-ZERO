using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreadZero
{
    /// 11-24 挂机条的窗口控制：无边框、置顶、背景透明、点击穿透、摆到屏幕工作区底部、左右拖动。
    /// Unity 自己只能改大小和位置（Screen.SetResolution / Screen.MoveMainWindowTo），其余要调系统接口：
    ///   Windows 走 user32 + dwmapi。透明要求 D3D11 且关掉 DXGI flip model（Editor/IdleStripSettings.cs 设好；D3D12 下自动退回不透明）。
    ///   Mac 走 Objective-C 运行时（libobjc）和 CoreGraphics，不用另外编插件。
    /// 编辑器里什么都不做（不能把编辑器窗口改掉）。任何一步失败都只记在 Status 里、不抛异常：最坏的结果是一个不透明的普通小窗口。
    /// 21:11 起也管普通窗口：设置里换窗口大小后摆到屏幕中间、从挂机窗回来摆回原位；挂机窗里退出前先把窗口藏起来。
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
        /// 能不能做透明窗口：Windows 要 D3D11（D3D12 / Vulkan 的交换链透明不了）；Mac 用 Metal 图层。
        public static bool CanTransparent
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
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
        /// 无边框 / 置顶。Windows 顺便把窗口摆到 rect（屏幕坐标，像素，rect 是客户区）；Mac 由调用方再摆位置。
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
        /// 背景透明（相机清成 alpha 0 的地方透出桌面）。
        public static void SetTransparent(bool on)
        {
            if (!Native) return;
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                WinTransparent(on);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                MacTransparent(on);
#endif
            }
            catch (Exception e) { Status = "透明失败：" + e.Message; Debug.LogWarning("THREAD ZERO: " + Status); }
        }
        /// 点击穿透：开着时鼠标点到窗口上会落到后面的窗口。
        public static void SetClickThrough(bool on)
        {
            if (!Native) return;
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                WinClickThrough(on);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                SendBool(MacWindow(), Sel("setIgnoresMouseEvents:"), (byte)(on ? 1 : 0));
#endif
            }
            catch (Exception e) { Status = "穿透失败：" + e.Message; }
        }
        /// 鼠标在窗口客户区里的位置（像素，左上为原点）；窗口点击穿透时 Unity 收不到鼠标，只能问系统。
        public static bool CursorInWindow(out Vector2 p)
        {
            p = Vector2.zero;
            if (!Native) return false;
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return WinCursor(out p);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return MacCursor(out p);
#endif
            }
            catch (Exception) { }
            return false;
        }
        /// 鼠标左键现在是不是按着（拖动挂机条用；窗口外松开也能知道）。
        public static bool LeftButtonHeld()
        {
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return (GetAsyncKeyState(0x01) & 0x8000) != 0;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return CGEventSourceButtonState(0, 0);
#endif
            }
            catch (Exception) { }
            return Input.GetMouseButton(0);
        }
        /// 窗口现在在屏幕上的位置和大小（像素）；只有 Windows 能拿到，别的平台返回 false。
        public static bool WindowRect(out RectInt r)
        {
            r = new RectInt();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                IntPtr h = Hwnd(); RECT wr; if (h == IntPtr.Zero || !GetWindowRect(h, out wr)) return false;
                r = new RectInt(wr.left, wr.top, wr.right - wr.left, wr.bottom - wr.top); return true;
            }
            catch (Exception) { }
#endif
            return false;
        }
        /// 某个屏幕点所在显示器的工作区（像素，屏幕坐标）；只有 Windows 能按点找显示器，别的平台返回当前窗口的工作区。
        public static RectInt WorkAreaAt(Vector2Int p)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { RectInt r = WinWorkAreaAt(p.x, p.y); if (r.width > 0 && r.height > 0) return r; } catch (Exception) { }
#endif
            DisplayInfo d; return WorkArea(out d);
        }
        /// 拖动：记下按下时鼠标和窗口的位置（屏幕坐标），之后 DragTo 按差值挪窗口：横竖都行，限制在鼠标所在显示器的可用区域里（拖不出屏幕）。
        public static bool BeginDrag()
        {
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return WinBeginDrag();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return MacBeginDrag();
#endif
            }
            catch (Exception e) { Status = "拖动失败：" + e.Message; }
            return false;
        }
        public static void DragTo()
        {
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                WinDragTo();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                MacDragTo();
#endif
            }
            catch (Exception) { }
        }
        /// 11-24 21:12 退出前先把窗口藏起来（完全透明 + 隐藏），之后摆回全屏 / 原来的窗口时看不到闪；藏了就不再显示，只等退出。
        public static bool Hidden { get; private set; }
        public static void HideForQuit()
        {
            if (!Native || Hidden) return;
            Hidden = true;
            try
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                WinHide();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                IntPtr w = MacWindow(); if (w != IntPtr.Zero) SendDouble(w, Sel("setAlphaValue:"), 0.0);
#endif
            }
            catch (Exception e) { Status = "隐藏失败：" + e.Message; }
        }
        /// 普通窗口（带边框）摆到它所在显示器可用区域的正中间；比可用区域大就贴左上。只有 Windows 摆得了，别的平台返回 false。
        public static bool CenterWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return WinMoveTo(int.MinValue, int.MinValue); } catch (Exception e) { Status = "居中失败：" + e.Message; }
#endif
            return false;
        }
        /// 普通窗口的左上角（含边框，屏幕像素）挪到 p，限制在那一块屏幕的可用区域里（标题栏不会跑到屏幕外）。只有 Windows。
        public static bool MoveWindowTo(Vector2Int p)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return WinMoveTo(p.x, p.y); } catch (Exception e) { Status = "摆窗口失败：" + e.Message; }
#endif
            return false;
        }
        /// 窗口现在所在的那块显示器（整块，屏幕像素）；primary = true 时给主显示器。只有 Windows；多显示器时用来把全屏放回原来那块屏幕。
        public static bool MonitorRect(out RectInt r, bool primary = false)
        {
            r = new RectInt();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                IntPtr mon;
                if (primary) mon = MonitorFromPoint(new POINT { x = 0, y = 0 }, 1);   // MONITOR_DEFAULTTOPRIMARY
                else { IntPtr h = Hwnd(); if (h == IntPtr.Zero) return false; mon = MonitorFromWindow(h, 2); }
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(mon, ref info)) return false;
                r = new RectInt(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right - info.rcMonitor.left, info.rcMonitor.bottom - info.rcMonitor.top);
                return r.width > 0 && r.height > 0;
            }
            catch (Exception) { }
#endif
            return false;
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct MARGINS { public int left, right, top, bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool AdjustWindowRect(ref RECT r, uint style, bool menu);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        private const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        private const long WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L, WS_MINIMIZEBOX = 0x00020000L, WS_MAXIMIZEBOX = 0x00010000L, WS_SYSMENU = 0x00080000L;
        private const long WS_EX_LAYERED = 0x00080000L, WS_EX_TRANSPARENT = 0x00000020L;
        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40;
        private static readonly IntPtr TOPMOST = new IntPtr(-1), NOTOPMOST = new IntPtr(-2);
        private static IntPtr hwnd;
        private static bool winTransparent;
        private static int dragCursorX, dragCursorY, dragWindowX, dragWindowY;
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
        /// Windows：某个屏幕点所在（或最近的）显示器的工作区。
        private static RectInt WinWorkAreaAt(int x, int y)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(MonitorFromPoint(new POINT { x = x, y = y }, 2), ref info)) return new RectInt(0, 0, 0, 0);
            return new RectInt(info.rcWork.left, info.rcWork.top, info.rcWork.right - info.rcWork.left, info.rcWork.bottom - info.rcWork.top);
        }
        private static void Win(bool borderless, bool topmost, RectInt r)
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) { Status = "找不到窗口"; return; }
            long style = GetWindowLongPtr(h, GWL_STYLE).ToInt64();
            long frame = WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
            style = borderless ? style & ~frame : style | frame;
            SetWindowLongPtr(h, GWL_STYLE, new IntPtr(style));
            uint flags = SWP_FRAMECHANGED | (Hidden ? SWP_NOACTIVATE : SWP_SHOWWINDOW);   // 退出前藏起来了就别再显示
            if (r.width > 0 && r.height > 0)
            {   // 给的是客户区：有边框时按边框把整个窗口放大一圈
                var rc = new RECT { left = r.x, top = r.y, right = r.x + r.width, bottom = r.y + r.height };
                if (!borderless) AdjustWindowRect(ref rc, (uint)style, false);
                SetWindowPos(h, topmost ? TOPMOST : NOTOPMOST, rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top, flags);
            }
            else SetWindowPos(h, topmost ? TOPMOST : NOTOPMOST, 0, 0, 0, 0, flags | SWP_NOMOVE | SWP_NOSIZE);
            Status = (borderless ? "无边框" : "有边框") + (topmost ? " · 置顶" : "");
        }
        /// 透明：把 DWM 的边框扩到整个客户区，窗口按每个像素的 alpha 和桌面混合（相机要清成 alpha 0）。
        private static void WinTransparent(bool on)
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return;
            var m = on ? new MARGINS { left = -1, right = -1, top = -1, bottom = -1 } : new MARGINS();
            int hr = DwmExtendFrameIntoClientArea(h, ref m);
            winTransparent = on && hr == 0;
            if (!on) WinClickThrough(false);
            Status = on ? (hr == 0 ? "透明" : "透明失败 " + hr) : "不透明";
        }
        /// 点击穿透：WS_EX_LAYERED + WS_EX_TRANSPARENT（鼠标落到后面的窗口）；关掉时两个都去掉（透明靠 DWM，不靠分层窗口）。
        private static void WinClickThrough(bool on)
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return;
            long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            long want = on ? ex | WS_EX_LAYERED | WS_EX_TRANSPARENT : ex & ~(WS_EX_LAYERED | WS_EX_TRANSPARENT);
            if (Hidden) want |= WS_EX_LAYERED;   // 退出前藏窗口靠分层窗口的透明度，别去掉
            if (want != ex) SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(want));
        }
        /// 退出前藏窗口：分层窗口透明度 0（Unity 摆回全屏时就算又把窗口显示出来也看不见），再隐藏（任务栏按钮马上消失）。
        private static void WinHide()
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return;
            long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex | WS_EX_LAYERED));
            SetLayeredWindowAttributes(h, 0, 0, 0x2);   // LWA_ALPHA
            ShowWindow(h, 0);   // SW_HIDE
        }
        /// 挪普通窗口（x 传 int.MinValue = 摆到正中间）：按窗口外框算，限制在目标点所在显示器的可用区域里。
        private static bool WinMoveTo(int x, int y)
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return false;
            RECT wr; if (!GetWindowRect(h, out wr)) return false;
            int w = wr.right - wr.left, hgt = wr.bottom - wr.top;
            bool center = x == int.MinValue;
            RectInt work = center ? WinWorkArea() : WinWorkAreaAt(x + Math.Min(w / 2, 80), y + 16);
            if (work.width <= 0) work = WinWorkArea();
            if (work.width <= 0) return false;
            if (center) { x = work.x + (work.width - w) / 2; y = work.y + (work.height - hgt) / 2; }
            x = Mathf.Clamp(x, work.x, Mathf.Max(work.x, work.x + work.width - w));
            y = Mathf.Clamp(y, work.y, Mathf.Max(work.y, work.y + work.height - hgt));
            return SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
        private static bool WinCursor(out Vector2 p)
        {
            p = Vector2.zero;
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return false;
            POINT pt; if (!GetCursorPos(out pt)) return false;
            if (!ScreenToClient(h, ref pt)) return false;
            RECT rc; if (!GetClientRect(h, out rc)) return false;
            p = new Vector2(pt.x, pt.y);
            return pt.x >= 0 && pt.y >= 0 && pt.x < rc.right && pt.y < rc.bottom;
        }
        private static bool WinBeginDrag()
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return false;
            POINT pt; RECT wr; if (!GetCursorPos(out pt) || !GetWindowRect(h, out wr)) return false;
            dragCursorX = pt.x; dragCursorY = pt.y; dragWindowX = wr.left; dragWindowY = wr.top; return true;
        }
        private static void WinDragTo()
        {
            IntPtr h = Hwnd(); if (h == IntPtr.Zero) return;
            POINT pt; RECT wr; if (!GetCursorPos(out pt) || !GetWindowRect(h, out wr)) return;
            RectInt work = WinWorkAreaAt(pt.x, pt.y); if (work.width <= 0) work = WinWorkArea();
            int w = wr.right - wr.left, hgt = wr.bottom - wr.top;
            int x = Mathf.Clamp(dragWindowX + pt.x - dragCursorX, work.x, Mathf.Max(work.x, work.x + work.width - w));
            int y = Mathf.Clamp(dragWindowY + pt.y - dragCursorY, work.y, Mathf.Max(work.y, work.y + work.height - hgt));
            if (x != wr.left || y != wr.top) SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        private const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        [StructLayout(LayoutKind.Sequential)] private struct CGPoint { public double x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct CGRect { public double x, y, w, h; }
        [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr target, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendIndex(IntPtr target, IntPtr selector, ulong index);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendLong(IntPtr target, IntPtr selector, long value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendULong(IntPtr target, IntPtr selector, ulong value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendBool(IntPtr target, IntPtr selector, byte value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendDouble(IntPtr target, IntPtr selector, double value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendPtr(IntPtr target, IntPtr selector, IntPtr value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendPoint(IntPtr target, IntPtr selector, CGPoint value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern CGRect SendRect(IntPtr target, IntPtr selector);          // arm64：NSRect 走寄存器
        [DllImport(ObjC, EntryPoint = "objc_msgSend_stret")] private static extern CGRect SendRectStret(IntPtr target, IntPtr selector);   // x86_64：大结构体走 _stret
        [DllImport(CG)] private static extern IntPtr CGEventCreate(IntPtr source);
        [DllImport(CG)] private static extern CGPoint CGEventGetLocation(IntPtr ev);
        [DllImport(CG)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CGEventSourceButtonState(int state, int button);
        [DllImport(CG)] private static extern uint CGMainDisplayID();
        [DllImport(CG)] private static extern CGRect CGDisplayBounds(uint display);
        [DllImport(CF)] private static extern void CFRelease(IntPtr obj);
        private static IntPtr Sel(string name) { return sel_registerName(name); }
        private static double dragCursorX, dragCursorY, dragWindowX, dragWindowY;
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
            SendULong(w, Sel("setCollectionBehavior:"), borderless ? 1UL : 0UL);   // 挂机条在每个桌面（Space）都显示
            Status = (borderless ? "无边框" : "有边框") + (topmost ? " · 置顶" : "");
        }
        private static void MacTransparent(bool on)
        {
            IntPtr w = MacWindow(); if (w == IntPtr.Zero) return;
            SendBool(w, Sel("setOpaque:"), (byte)(on ? 0 : 1));
            IntPtr color = Send(objc_getClass("NSColor"), Sel(on ? "clearColor" : "windowBackgroundColor"));
            if (color != IntPtr.Zero) SendPtr(w, Sel("setBackgroundColor:"), color);
            SendBool(w, Sel("setHasShadow:"), (byte)(on ? 0 : 1));
            IntPtr view = Send(w, Sel("contentView"));
            if (view != IntPtr.Zero)
            {
                IntPtr layer = Send(view, Sel("layer"));   // Unity 的 Metal 图层：不透明关掉才会按 alpha 混合
                if (layer != IntPtr.Zero) SendBool(layer, Sel("setOpaque:"), (byte)(on ? 0 : 1));
            }
            Status = on ? "透明" : "不透明";
        }
        private static CGRect Frame(IntPtr w)
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? SendRect(w, Sel("frame")) : SendRectStret(w, Sel("frame"));
        }
        private static CGPoint MouseQuartz()
        {   // 全局坐标，主屏左上为原点，单位是点
            IntPtr ev = CGEventCreate(IntPtr.Zero); if (ev == IntPtr.Zero) return new CGPoint();
            CGPoint p = CGEventGetLocation(ev); CFRelease(ev); return p;
        }
        private static bool MacCursor(out Vector2 p)
        {
            p = Vector2.zero;
            IntPtr w = MacWindow(); if (w == IntPtr.Zero) return false;
            CGRect f = Frame(w); if (f.w <= 0 || f.h <= 0) return false;
            double mainH = CGDisplayBounds(CGMainDisplayID()).h;
            double top = mainH - (f.y + f.h);   // Cocoa 窗口坐标是左下为原点，换成左上
            CGPoint m = MouseQuartz();
            double scale = Screen.width / f.w;
            p = new Vector2((float)((m.x - f.x) * scale), (float)((m.y - top) * scale));
            return m.x >= f.x && m.x < f.x + f.w && m.y >= top && m.y < top + f.h;
        }
        private static bool MacBeginDrag()
        {
            IntPtr w = MacWindow(); if (w == IntPtr.Zero) return false;
            CGPoint m = MouseQuartz(); CGRect f = Frame(w);
            dragCursorX = m.x; dragCursorY = m.y; dragWindowX = f.x; dragWindowY = f.y; return true;
        }
        private static void MacDragTo()
        {
            IntPtr w = MacWindow(); if (w == IntPtr.Zero) return;
            CGRect f = Frame(w);
            IntPtr screen = Send(w, Sel("screen"));
            CGRect vis = screen != IntPtr.Zero ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? SendRect(screen, Sel("visibleFrame")) : SendRectStret(screen, Sel("visibleFrame"))) : new CGRect();
            CGPoint m = MouseQuartz();
            double x = dragWindowX + m.x - dragCursorX, y = dragWindowY - (m.y - dragCursorY);
            if (vis.w > 0) { x = Math.Max(vis.x, Math.Min(vis.x + vis.w - f.w, x)); y = Math.Max(vis.y, Math.Min(vis.y + vis.h - f.h, y)); }
            if (Math.Abs(x - f.x) >= .5 || Math.Abs(y - f.y) >= .5) SendPoint(w, Sel("setFrameOrigin:"), new CGPoint { x = x, y = y });
        }
#endif
    }
}
