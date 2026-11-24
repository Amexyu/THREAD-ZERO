using System.Collections;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 轻量化挂机（用户：现在的放置游戏都在往轻量化走，不用占很多屏幕就能让它自己挂着）。两种都做，设置里切：
    ///   挂机条：贴在屏幕工作区最下面的一条，无边框、可置顶；只画战斗那条路，加关卡、收益和几个按钮。
    ///   小窗：右下角的小窗口，有标题栏可拖、可置顶；画面是战斗的缩小版（推近到主角和怪那一段）。
    /// 「展开」回到进挂机前的全屏 / 窗口。左边导航栏有「挂机」按钮（进上次用的那种）。
    /// 编辑器里不改窗口，只在 Game 视图里按真实比例画一块，方便截图检查。
    public sealed partial class GameApp
    {
        public enum CompactKind { Off = 0, Strip = 1, Corner = 2 }
        private CompactKind compact = CompactKind.Off;
        private bool compactView;        // ActionPage 只画舞台
        private bool compactTop = true;  // 置顶
        private bool compactBusy, savedWindow;
        private FullScreenMode savedMode; private int savedW, savedH; private Vector2Int savedPos;
        private const string CompactKindKey = "tz_compact_kind", CompactTopKey = "tz_compact_top";
        private const float StripShare = .25f;   // 挂机条高 = 工作区的 1/4：全宽时正好放下主角（含头顶血条）和一点地面

        private CompactKind LastCompact { get { return (CompactKind)Mathf.Clamp(PlayerPrefs.GetInt(CompactKindKey, 1), 1, 2); } }
        private void LoadCompactPrefs() { compactTop = PlayerPrefs.GetInt(CompactTopKey, 1) == 1; }

        /// 启动时：上次在挂机条 / 小窗里直接退出，Unity 会按那个小尺寸开窗；太小就恢复成全屏窗口。
        private void FixTinyWindowOnStart()
        {
            if (Application.isEditor || Screen.fullScreenMode != FullScreenMode.Windowed) return;
            if (Screen.height < 420 || Screen.width < 560)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        }

        public void EnterCompact(CompactKind kind)
        {
            if (kind == CompactKind.Off) { ExitCompact(); return; }
            if (ExhibitMode || compactBusy || compact == kind) return;
            PlayerPrefs.SetInt(CompactKindKey, (int)kind); PlayerPrefs.Save();
            bool first = compact == CompactKind.Off;
            navigation.Go(GameScreen.Action); hudShow = 0;
            compact = kind; CompactFrameRate(true);
            if (!Application.isEditor) StartCoroutine(PlaceCompact(kind, first));
        }
        public void ExitCompact()
        {
            if (compact == CompactKind.Off || compactBusy) return;
            compact = CompactKind.Off; CompactFrameRate(false);
            if (!Application.isEditor) StartCoroutine(RestoreWindow());
        }
        // 挂机条 / 小窗里画面降到 30 帧（关垂直同步），省电；回到大窗口恢复原来的设置
        private int savedVSync = -1;
        private void CompactFrameRate(bool on)
        {
            if (on) { if (savedVSync < 0) savedVSync = QualitySettings.vSyncCount; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 30; }
            else if (savedVSync >= 0) { QualitySettings.vSyncCount = savedVSync; savedVSync = -1; Application.targetFrameRate = 60; }
        }
        private void SetCompactTop(bool on)
        {
            compactTop = on; PlayerPrefs.SetInt(CompactTopKey, on ? 1 : 0); PlayerPrefs.Save();
            if (!Application.isEditor) WindowCtl.Apply(compact == CompactKind.Strip, on, new RectInt());
        }

        private IEnumerator PlaceCompact(CompactKind kind, bool remember)
        {
            compactBusy = true;
            if (remember && !savedWindow)
            {
                savedWindow = true; savedMode = Screen.fullScreenMode; savedW = Screen.width; savedH = Screen.height;
                try { savedPos = Screen.mainWindowPosition; } catch (System.Exception) { savedPos = Vector2Int.zero; }
            }
            DisplayInfo display; RectInt work = WindowCtl.WorkArea(out display);
            bool strip = kind == CompactKind.Strip;
            // 尺寸按工作区比例算（不管缩放倍数都差不多大）：挂机条 = 全宽 × 1/4 高，小窗 = 三成宽的 16:9
            int w = strip ? work.width : Mathf.Clamp(Mathf.RoundToInt(work.width * .3f), 480, 1400);
            int h = strip ? Mathf.Clamp(Mathf.RoundToInt(work.height * StripShare), 180, 560) : Mathf.RoundToInt(w * 9f / 16f);
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            yield return null; yield return null;
            if (WindowCtl.PlacesWindow)
            {   // Windows：一次摆好（屏幕坐标，客户区）
                RectInt sw = WindowCtl.ScreenWorkArea();
                int ww = strip ? sw.width : w, hh = strip ? Mathf.Clamp(Mathf.RoundToInt(sw.height * StripShare), 180, 560) : h;
                RectInt target = strip ? new RectInt(sw.x, sw.y + sw.height - hh, ww, hh) : new RectInt(sw.x + sw.width - ww - 24, sw.y + sw.height - hh - 24, ww, hh);
                WindowCtl.Apply(strip, compactTop, target);
            }
            else
            {   // Mac：先改边框 / 层级，再按内容区重设大小、挪到工作区底部 / 右下角
                WindowCtl.Apply(strip, compactTop, new RectInt());
                yield return null;
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                yield return null;
                Vector2Int pos = strip ? new Vector2Int(work.x, work.y + work.height - h) : new Vector2Int(work.x + work.width - w - 24, work.y + work.height - h - 52);
                try { Screen.MoveMainWindowTo(display, pos); } catch (System.Exception e) { WindowCtl.Status = "move: " + e.Message; }
            }
            compactBusy = false;
        }
        private IEnumerator RestoreWindow()
        {
            compactBusy = true;
            WindowCtl.Apply(false, false, new RectInt());
            yield return null;
            if (savedWindow)
            {
                Screen.SetResolution(savedW, savedH, savedMode);
                if (savedMode == FullScreenMode.Windowed)
                {
                    yield return null;
                    try { DisplayInfo d; WindowCtl.WorkArea(out d); Screen.MoveMainWindowTo(d, savedPos); } catch (System.Exception) { }
                }
            }
            else Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            savedWindow = false; compactBusy = false;
        }

        // ---------- 画 ----------
        private void CompactGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { paused = !paused; e.Use(); }
            Rect area = new Rect(0, 0, Screen.width, Screen.height);
            if (Application.isEditor)
            {   // 编辑器预览：Game 视图里按真实比例画一块，周围当桌面
                Ui.FillScreen(Ui.Hex(0x2B3038));
                float gw = Screen.width, gh = Screen.height;
                if (compact == CompactKind.Strip) { float hh = Mathf.Round(gh * StripShare); area = new Rect(0, gh - hh, gw, hh); }
                else { float ww = Mathf.Round(gw * .3f), hh = Mathf.Round(ww * 9 / 16f); area = new Rect(gw - ww - 24, gh - hh - 24, ww, hh); }
            }
            GUI.BeginGroup(area);
            float W = area.width, H = area.height;
            bool strip = compact == CompactKind.Strip;
            float k, lx, ly;
            if (strip) { k = W / 1600f; ly = GroundY + 16 - H / k; lx = 0; }   // 地面贴着下沿（留一点地面），往上刚好露出主角头顶的血条
            else { const float span = 1250; k = W / span; ly = 900 - H / k; lx = Mathf.Clamp(lastHeroFeetX - 330, 0, 1600 - span); }
            Ui.PushView(0, 0, lx, ly, k);
            compactView = true;
            try { ActionPage(); } finally { compactView = false; Ui.PopView(); }
            float s = Mathf.Clamp(H / (strip ? 260f : 324f), .7f, 2.4f);
            Ui.PushView(0, 0, 0, 0, s);
            if (strip) StripHud(W / s, H / s); else CornerHud(W / s, H / s);
            Ui.PopView();
            GUI.EndGroup();
        }

        private string CompactRate() { return "+" + Mathf.RoundToInt(model.state.scrapRate * 60).ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
        private string CompactStatus()
        {
            if (combat.recovery > 0) return "维修中 · " + combat.recovery.ToString("F1") + " 秒";
            if (paused) return "已暂停 · 空格继续";
            if (model.Farming) return "回刷补给中";
            return null;
        }
        private string CompactHint()
        {
            if (navigation.Modal == GameModal.Offline) return "离线带回 " + offlineGain.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " 零件 · 展开看详情";   // 11-24 小窗里不弹窗
            if (unseen.Count > 0) return "掉落 " + unseen.Count + " 件未看";
            if (model.AnyPerkBuyable) return "改造板有节点能点";
            if (model.AnyImplantUpgradable) return "义体能升级";
            if (model.AnyRobotAssemblable) return "机库能组装机器人";
            return null;
        }
        private void CompactStage(float x, float y, float codeSize, float nameSize)
        {   // 名字和波次分开画（各自查翻译表）
            int code = CurrentCode;
            Ui.ShadowText(x, y, 90, codeSize + 8, CodeLabel(code), codeSize, Ui.Panel, TextAnchor.MiddleLeft, true, true);
            float nx = x + codeSize * 2.4f, ny = y + (codeSize - nameSize) / 2 + 2;
            string name = NameLabel(code); float nw = Ui.Width(name, nameSize, true);
            Ui.ShadowText(nx, ny, nw + 8, nameSize + 8, name, nameSize, Ui.Panel, TextAnchor.MiddleLeft, true);
            string wave = combat.BossEnemy == null ? "第 " + combat.wave + " / " + combat.WaveCount + " 波" : "首领";
            Ui.ShadowText(nx + nw + 12, ny, 200, nameSize + 8, wave, nameSize - 2, combat.BossEnemy == null ? Ui.MuteOnInk : Ui.Orange, TextAnchor.MiddleLeft, true);
        }
        private void CompactHeroBar(float x, float y, float w)
        {
            float v = Mathf.Clamp01(combat.health / Mathf.Max(1, model.Total().health));
            Ui.Fill(x, y, w, 6, Ui.Alpha(Ui.Ink, .9f)); Ui.Fill(x + 1, y + 1, (w - 2) * v, 4, v > .3f ? Ui.Hex(0x45E6D6) : Ui.Orange);
        }
        private void CompactBossBar(float x, float y, float w)
        {
            Enemy b = combat.BossEnemy; if (b == null) return;
            float v = Mathf.Clamp01(b.health / Mathf.Max(1, b.maxHealth));
            Ui.Fill(x, y, w, 8, Ui.Alpha(Ui.Ink, .8f)); Ui.Fill(x + 1, y + 1, (w - 2) * v, 6, Ui.Orange);
        }
        /// 挂机条：左上关卡，右上收益和按钮；首领时中间一条血。
        private void StripHud(float w, float h)
        {
            Ui.Fill(0, 0, w, 3, Ui.Orange);
            Ui.Fill(0, 3, 470, 54, Ui.Alpha(Ui.Ink, .78f));
            CompactStage(16, 6, 24, 16);
            CompactHeroBar(16, 44, 300);   // 主角血条（挂机条里头顶那条看不到）
            float rx = w - 640;
            Ui.Fill(rx, 3, 640, 44, Ui.Alpha(Ui.Ink, .78f));
            Ui.Text(rx + 16, 3, 44, 44, "零件", 14, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            Ui.Text(rx + 58, 3, 120, 44, CompactRate(), 22, Ui.Panel, TextAnchor.MiddleLeft, true, true);
            Ui.Text(rx + 150, 3, 40, 44, "/ 分", 14, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            string hint = CompactStatus() ?? CompactHint();
            if (hint != null) Ui.FitText(rx + 196, 3, 168, 44, hint, 14, Ui.Panel, TextAnchor.MiddleLeft);
            CompactButtons(w - 268, 10, 30, true);
            CompactBossBar(w * .35f, 18, w * .3f);
        }
        /// 小窗：上沿关卡和收益，下沿提示和按钮。
        private void CornerHud(float w, float h)
        {
            Ui.Fill(0, 0, w, 38, Ui.Alpha(Ui.Ink, .8f));
            CompactStage(12, 5, 20, 14);
            Ui.Text(w - 240, 0, 228, 38, "零件 " + CompactRate() + " / 分", 14, Ui.Panel, TextAnchor.MiddleRight, true);
            CompactBossBar(12, 42, w - 24);
            Ui.Fill(0, h - 42, w, 42, Ui.Alpha(Ui.Ink, .8f));
            string hint = CompactStatus() ?? CompactHint();
            if (hint != null) Ui.FitText(12, h - 42, w - 300, 42, hint, 14, Ui.Panel, TextAnchor.MiddleLeft);
            CompactButtons(w - 268, h - 36, 30, false);
        }
        /// 置顶 / 换另一种 / 展开（共 256 宽）。
        private void CompactButtons(float x, float y, float h, bool strip)
        {
            if (Ui.Button(x, y, 72, h, "置顶", compactTop ? 1 : 3, true, 13)) SetCompactTop(!compactTop);
            if (Ui.Button(x + 80, y, 92, h, strip ? "换小窗" : "换挂机条", 3, !compactBusy, 13)) EnterCompact(strip ? CompactKind.Corner : CompactKind.Strip);
            if (Ui.Button(x + 180, y, 76, h, "展开", 3, !compactBusy, 13)) ExitCompact();
        }

        // ---------- 测试（桥接 call TestCompact 1 / 2 / 0）----------
        public void TestCompact(int kind) { if (kind <= 0) ExitCompact(); else EnterCompact((CompactKind)kind); }
    }
}
