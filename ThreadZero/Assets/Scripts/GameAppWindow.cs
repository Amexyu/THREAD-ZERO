using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 21:11 窗口：全屏 / 窗口大小、启动时恢复、挂机窗里退出。用户这一轮提的：
    ///   「打开的时候是在右下角小窗口，随后才变成全屏」——Unity 按上次退出时的窗口开新窗口；在挂机窗里直接关掉（Alt+F4），下次就先按挂机窗的大小和位置开，
    ///     脚本跑起来才改回全屏。现在挂机窗里退出（菜单「退出」、Alt+F4、任务栏关窗口）都先把窗口藏起来、摆回进挂机前的全屏 / 窗口，再真正退出；
    ///     万一没摆回去（崩溃、被结束进程），进挂机时记下的样子留在本机，下次启动照着恢复。
    ///   「挂机条状态无法直接退出程序」——挂机窗的菜单里加「退出」（GameAppCompact.cs）。
    ///   「窗口大小没法调，只有全屏和窗口；应该靠调分辨率？」——设置加「窗口大小」一行：常用 16:9 尺寸里放得下的最大 4 档，点了就切成那么大的窗口、摆到屏幕中间；
    ///     窗口模式下也能直接拖边框改大小（PlayerSettings 已开 resizableWindow），拖出来的大小记在本机，下次点「窗口」还是这个大小。
    ///     「全屏」是无边框全屏，按所在屏幕的原生分辨率（原来 Screen.fullScreen = true 会沿用窗口的分辨率再放大，发糊）。
    /// 编辑器里不改窗口（不能把编辑器的窗口改掉），设置里照样画出来方便截图检查。
    public sealed partial class GameApp
    {
        private const string InStripKey = "tz_in_strip", NormKey = "tz_norm", WinSizeKey = "tz_win_size";
        private bool quitting, quitReady, windowBusy, applyOk; private float quitAt, winCheckAt; private Vector2Int lastWinSize;

        // ---------- 进挂机前的样子 ----------
        private struct NormWin { public FullScreenMode mode; public int w, h; public bool hasPos, hasMon; public Vector2Int pos; public RectInt mon; }
        /// 进挂机前的全屏 / 窗口：这次进挂机时记下的；没有（崩溃后重开）就读本机记的；都没有就全屏。
        /// 全屏按原来那块显示器的分辨率（挂机窗可能被拖到了别的显示器上，用户有竖着的副屏）；不知道原来是哪块就按现在这块。
        private NormWin NormalWindow()
        {
            var n = new NormWin { mode = FullScreenMode.FullScreenWindow };
            if (savedWindow)
            {
                n.mode = savedMode; n.w = savedW; n.h = savedH;
                n.hasPos = savedOuterOk; n.pos = new Vector2Int(savedOuter.x, savedOuter.y); n.hasMon = savedMonOk; n.mon = savedMon;
            }
            else
            {   // 本机记的：模式,宽,高,窗口x,窗口y,屏幕x,屏幕y,屏幕宽,屏幕高（没有的写 -）
                string[] p = PlayerPrefs.GetString(NormKey, "").Split(',');
                int[] v = new int[9]; bool[] ok = new bool[9];
                for (int i = 0; i < 9 && i < p.Length; i++) ok[i] = int.TryParse(p[i], out v[i]);
                if (ok[0] && ok[1] && ok[2]) { n.mode = (FullScreenMode)v[0]; n.w = v[1]; n.h = v[2]; }
                if (ok[3] && ok[4]) { n.hasPos = true; n.pos = new Vector2Int(v[3], v[4]); }
                if (ok[5] && ok[6] && ok[7] && ok[8] && v[7] > 0 && v[8] > 0) { n.hasMon = true; n.mon = new RectInt(v[5], v[6], v[7], v[8]); }
            }
            if (n.mode != FullScreenMode.Windowed || n.w < 320 || n.h < 200)
            {
                n.mode = FullScreenMode.FullScreenWindow; n.hasPos = false;
                // 不知道原来在哪块屏幕（老版本在挂机窗里关掉的）：回主显示器，别在挂机窗被拖去的副屏上全屏
                if (!n.hasMon) n.hasMon = WindowCtl.MonitorRect(out n.mon, true);
                if (n.hasMon) { n.w = n.mon.width; n.h = n.mon.height; } else NativeSize(out n.w, out n.h);
            }
            return n;
        }
        private static void NativeSize(out int w, out int h)
        {
            w = Display.main.systemWidth; h = Display.main.systemHeight;
            try { DisplayInfo d = Screen.mainWindowDisplayInfo; if (d.width > 0 && d.height > 0) { w = d.width; h = d.height; } } catch (System.Exception) { }
        }
        /// 进挂机时：记下原来的样子，并标记「正在挂机窗里」（正常展开 / 退出时清掉）。
        private void RememberNormalWindow()
        {
            PlayerPrefs.SetString(NormKey, (int)savedMode + "," + savedW + "," + savedH + "," + (savedOuterOk ? savedOuter.x + "," + savedOuter.y : "-,-")
                + "," + (savedMonOk ? savedMon.x + "," + savedMon.y + "," + savedMon.width + "," + savedMon.height : "-,-,-,-"));
            PlayerPrefs.SetInt(InStripKey, 1); PlayerPrefs.Save();
        }
        private static void LeftStrip() { PlayerPrefs.SetInt(InStripKey, 0); PlayerPrefs.Save(); }
        /// 摆回进挂机前的样子：窗口不在原来那块显示器上就先挪过去（全屏是在窗口所在的那块屏幕上铺开的），再改全屏 / 窗口大小，窗口再摆回原位。
        /// 结果写在 applyOk（大小和模式在 timeout 秒内改好了没有）。
        private IEnumerator ApplyNormal(NormWin n, float timeout)
        {
            applyOk = false;
            RectInt now;
            if (n.hasMon && WindowCtl.MonitorRect(out now) && (now.x != n.mon.x || now.y != n.mon.y) && WindowCtl.MoveWindowTo(new Vector2Int(n.mon.x + 8, n.mon.y + 8)))
            { yield return null; yield return null; }
            Screen.SetResolution(n.w, n.h, n.mode);
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until && (Screen.width != n.w || Screen.height != n.h || Screen.fullScreenMode != n.mode)) yield return null;
            applyOk = Screen.width == n.w && Screen.height == n.h && Screen.fullScreenMode == n.mode;
            yield return null;
            PlaceNormalWindow(n);
        }
        /// 普通窗口摆回原位（Windows 按外框位置，限制在屏幕里）；没有原位就摆到中间。
        private void PlaceNormalWindow(NormWin n)
        {
            if (n.mode != FullScreenMode.Windowed || Application.isEditor) return;
            if (WindowCtl.PlacesWindow) { if (!(n.hasPos && WindowCtl.MoveWindowTo(n.pos))) WindowCtl.CenterWindow(); return; }
            if (!savedWindow) { CenterNormalWindow(); return; }
            try { DisplayInfo d; WindowCtl.WorkArea(out d); Screen.MoveMainWindowTo(d, savedPos); } catch (System.Exception) { }
        }
        private void CenterNormalWindow()
        {
            if (Application.isEditor || WindowCtl.CenterWindow()) return;
            try
            {
                DisplayInfo d; RectInt work = WindowCtl.WorkArea(out d);
                Screen.MoveMainWindowTo(d, new Vector2Int(work.x + Mathf.Max(0, (work.width - Screen.width) / 2), work.y + Mathf.Max(0, (work.height - Screen.height) / 2)));
            }
            catch (System.Exception) { }
        }

        // ---------- 启动 ----------
        /// 上次在挂机窗里没摆回去就退了（崩溃、被结束；老版本在挂机窗里 Alt+F4 也是），Unity 会按挂机窗的大小开：照记下的恢复。
        /// 老版本没有记号：按形状认（挂机窗又扁又矮），恢复成全屏。
        private void FixWindowOnStart()
        {
            if (Application.isEditor) return;
            bool windowed = Screen.fullScreenMode == FullScreenMode.Windowed;
            bool wasStrip = PlayerPrefs.GetInt(InStripKey, 0) == 1;
            bool stripShaped = windowed && !PlayerPrefs.HasKey(InStripKey) && Screen.width > Screen.height * 2.6f;
            bool broken = windowed && (Screen.width < 320 || Screen.height < 200);
            if (!wasStrip && !stripShaped && !broken) return;
            LeftStrip();
            StartCoroutine(StartupRestore());
        }
        private IEnumerator StartupRestore()
        {
            windowBusy = true;
            yield return null;   // 等窗口建好
            yield return StartCoroutine(ApplyNormal(NormalWindow(), 1.5f));
            windowBusy = false;
        }

        // ---------- 退出 ----------
        /// Application.wantsToQuit：在挂机窗里要退（菜单「退出」、Alt+F4、任务栏关窗口）→ 先不退，摆好窗口再退。
        private bool WantsToQuit()
        {
            if (quitReady || Application.isEditor || compact == CompactKind.Off) return true;
            if (!quitting) { quitting = true; quitAt = Time.realtimeSinceStartup; StartCoroutine(QuitFromStrip()); }
            return false;
        }
        private IEnumerator QuitFromStrip()
        {
            WindowCtl.HideForQuit();   // 先藏起来：下面摆回全屏 / 大窗口的过程看不到
            float until = Time.realtimeSinceStartup + 2f;
            while (compactBusy && Time.realtimeSinceStartup < until) yield return null;
            compact = CompactKind.Off; stripMenu = false; stripDrag = false; stripToasts.Clear(); toastText = null;
            compactBusy = true;
            WindowCtl.SetTransparent(false); StripCamera(false);
            WindowCtl.Apply(false, false, new RectInt());
            yield return null;
            yield return StartCoroutine(ApplyNormal(NormalWindow(), 1.5f));
            yield return null;
            savedWindow = false; compactBusy = false;
            if (applyOk) LeftStrip(); else PlayerPrefs.Save();   // 没摆回去就留着记号，下次启动再恢复
            quitReady = true;
            Application.Quit();
        }
        private void OnDestroy() { Application.wantsToQuit -= WantsToQuit; }   // 新游戏会重载场景：别让旧的这份还挂在退出事件上

        /// 每帧（Update）：退出卡住的保险；窗口模式下记住窗口大小（拖边框改的也算）。
        private void WindowTick()
        {
            if (quitting && !quitReady && Time.realtimeSinceStartup - quitAt > 6f) { quitReady = true; Application.Quit(); }
            if (Application.isEditor || quitting || compact != CompactKind.Off || compactBusy || windowBusy || Time.unscaledTime < winCheckAt) return;
            winCheckAt = Time.unscaledTime + 1f;
            if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width < 320 || Screen.height < 200) return;
            if (Screen.width == lastWinSize.x && Screen.height == lastWinSize.y) return;
            lastWinSize = new Vector2Int(Screen.width, Screen.height);
            PlayerPrefs.SetString(WinSizeKey, lastWinSize.x + "," + lastWinSize.y); PlayerPrefs.Save();
        }

        // ---------- 设置：全屏 / 窗口 / 窗口大小 ----------
        private static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(960, 540), new Vector2Int(1152, 648), new Vector2Int(1280, 720), new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160),
        };
        private readonly List<Vector2Int> sizeChoices = new List<Vector2Int>(); private float sizeChoicesAt = -10;
        /// 放得下的（连标题栏、边框和任务栏一起）最大 4 档；每秒重算一次（窗口可能被拖到别的显示器）。
        private List<Vector2Int> WindowSizeChoices()
        {
            if (sizeChoices.Count > 0 && Time.unscaledTime - sizeChoicesAt < 1f) return sizeChoices;
            sizeChoicesAt = Time.unscaledTime; sizeChoices.Clear();
            RectInt work = WindowCtl.ScreenWorkArea();
            for (int i = WindowSizes.Length - 1; i >= 0 && sizeChoices.Count < 4; i--)
                if (WindowSizes[i].x <= work.width - 20 && WindowSizes[i].y <= work.height - 60) sizeChoices.Insert(0, WindowSizes[i]);
            if (sizeChoices.Count == 0) sizeChoices.Add(WindowSizes[0]);
            return sizeChoices;
        }
        private void SetFullscreen()
        {
            if (windowBusy || Application.isEditor) return;
            int w, h; NativeSize(out w, out h);
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        }
        /// 「窗口」：上次的窗口大小（拖边框改的也算）；没有就用放得下的最大一档（1080p 屏幕上是 1600×900）。
        private void SetWindowedMode()
        {
            if (!Screen.fullScreen || windowBusy) return;
            Vector2Int s = SavedWindowSize();
            SetWindowSize(s.x, s.y);
        }
        private Vector2Int SavedWindowSize()
        {
            string[] p = PlayerPrefs.GetString(WinSizeKey, "").Split(',');
            RectInt work = WindowCtl.ScreenWorkArea(); int w, h;
            if (p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w >= 320 && h >= 200 && w <= work.width && h <= work.height) return new Vector2Int(w, h);
            List<Vector2Int> c = WindowSizeChoices(); return c[c.Count - 1];
        }
        private void SetWindowSize(int w, int h) { if (!windowBusy && !Application.isEditor) StartCoroutine(PlaceWindowed(w, h)); }
        private IEnumerator PlaceWindowed(int w, int h)
        {
            windowBusy = true;
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            for (int i = 0; i < 30 && (Screen.width != w || Screen.height != h || Screen.fullScreenMode != FullScreenMode.Windowed); i++) yield return null;
            yield return null;
            CenterNormalWindow();
            lastWinSize = new Vector2Int(Screen.width, Screen.height);
            PlayerPrefs.SetString(WinSizeKey, w + "," + h); PlayerPrefs.Save();
            windowBusy = false;
        }
        /// 设置里「窗口大小」一行：x、row 是这一行的左上，w 是弹窗宽。
        private void WindowSizeRow(float x, float row, float w)
        {
            Ui.Text(x + 32, row, 300, 48, "窗口大小", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            bool windowed = !Screen.fullScreen;
            List<Vector2Int> sizes = WindowSizeChoices();
            float left = x + w - 32 - sizes.Count * 108 + 4, lw = Ui.Width("窗口大小", 17, true) + 12;
            if (windowed) Ui.FitText(x + 32 + lw, row, left - x - 44 - lw, 48, "当前 " + Screen.width + "×" + Screen.height, 12, Ui.Mute, TextAnchor.MiddleLeft);
            for (int i = 0; i < sizes.Count; i++)
            {
                Vector2Int s = sizes[i];
                bool on = windowed && Screen.width == s.x && Screen.height == s.y;
                if (Ui.Button(left + i * 108, row, 104, 48, s.x + "×" + s.y, on ? 2 : 0, !windowBusy, 14, true)) SetWindowSize(s.x, s.y);
            }
        }
    }
}
