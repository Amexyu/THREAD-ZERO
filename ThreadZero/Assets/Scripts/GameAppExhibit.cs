using System;
using System.IO;
using UnityEngine;

namespace ThreadZero
{
    /// v0.21 展览模式（待定方案第 5 条的默认方案）：
    ///   - 开法：设置 →「展览模式」（选「用当前进度作起点」或「用预设：第三章开头」），或者启动参数 -exhibit；
    ///   - 每次启动、每次没人操作 3 分钟，都回到标题、从展览起点重新开始（展览存档单独一个文件，不碰正式存档；不算离线收益）；
    ///   - 标题去掉「新游戏」「退出游戏」；开发用的 F2–F9 关掉；
    ///   - 退出：按 Ctrl+Shift+E（标题和游戏里都行）再确认。用 -exhibit 启动的要去掉参数重开。
    public sealed partial class GameApp
    {
        public const string ExhibitPrefKey = "tz_exhibit";
        private const float ExhibitIdle = 180, ExhibitWarn = 30;
        private const string ExhibitSaveName = "thread-zero-save-exhibit.json";
        public static bool ExhibitMode { get; private set; }
        private static bool exhibitArg;
        private float lastInputAt; private Vector3 lastMouse;
        private bool exhibitPanel, exhibitExitPanel;
        private static string ExhibitBasePath { get { return Path.Combine(Application.persistentDataPath, "thread-zero-exhibit-base.json"); } }

        /// Awake 最开始：判断是不是展览模式；是的话把展览起点复制成这次用的存档（每次启动都从起点开始）。
        private static void DetectExhibit()
        {
            exhibitArg = Array.IndexOf(Environment.GetCommandLineArgs(), "-exhibit") >= 0;
            ExhibitMode = (exhibitArg || PlayerPrefs.GetInt(ExhibitPrefKey, 0) == 1) && !TZAutoTest.Running;
            if (!ExhibitMode) return;
            string work = Path.Combine(Application.persistentDataPath, ExhibitSaveName);
            try
            {
                string json = File.Exists(ExhibitBasePath) ? File.ReadAllText(ExhibitBasePath) : ExhibitPresetJson();
                foreach (string f in new[] { work, work + ".bak", work + ".tmp" }) if (File.Exists(f)) File.Delete(f);
                if (!string.IsNullOrEmpty(json)) File.WriteAllText(work, json);
            }
            catch (Exception e) { Debug.LogWarning("THREAD ZERO: 展览存档准备失败：" + e.Message); }
        }
        private static string ExhibitPresetJson() { TextAsset a = Resources.Load<TextAsset>("Exhibit/preset"); return a != null ? a.text : null; }

        /// 每帧：记最后一次有人操作的时间；在游戏里（不在标题）超过 3 分钟没人动就回到标题、重置。
        private void ExhibitTick()
        {
            if (!ExhibitMode) return;
            Vector3 m = Input.mousePosition;
            if (Input.anyKey || Input.mouseScrollDelta.sqrMagnitude > 0 || (m - lastMouse).sqrMagnitude > 9) { lastInputAt = Time.unscaledTime; lastMouse = m; }
            if (titleOpen || exhibitExitPanel) { lastInputAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - lastInputAt > ExhibitIdle) { Debug.Log("THREAD ZERO: 展览模式 3 分钟没人操作，回到标题并重置"); RestartGame(); }
        }
        /// 快到时间时顶部提示倒计时。
        private void ExhibitIdleBanner()
        {
            if (!ExhibitMode || titleOpen) return;
            float left = ExhibitIdle - (Time.unscaledTime - lastInputAt);
            if (left > ExhibitWarn) return;
            string text = "没人操作 · " + Mathf.CeilToInt(Mathf.Max(0, left)) + " 秒后回到标题重新开始（动一下鼠标就继续）";
            float w = Ui.Width(text, 16, true) + 60;
            Ui.Fill(800 - w / 2, 18, w, 48, Ui.Alpha(Ui.Ink, .9f)); Ui.Fill(800 - w / 2, 18, 6, 48, Ui.Orange);
            Ui.Text(800 - w / 2, 18, w, 48, text, 16, Ui.Panel, TextAnchor.MiddleCenter, true);
        }
        /// Ctrl+Shift+E：退出展览模式的确认。
        private bool ExhibitKey(Event e)
        {
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.E || !e.control || !e.shift) return false;
            if (ExhibitMode) exhibitExitPanel = true; e.Use(); return true;
        }

        private void ExhibitPanels()
        {
            if (!exhibitPanel && !exhibitExitPanel) return;
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .6f));
            float x = 440, y = 250, w = 720, h = exhibitPanel ? 360 : 300;
            Ui.Fill(x, y, w, h, Ui.Panel); Ui.Fill(x, y, w, 72, Ui.Ink); Ui.Fill(x, y, 8, 72, Ui.Orange); Ui.Frame(x, y, w, h, 2, Ui.Ink);
            if (exhibitExitPanel)
            {
                Ui.Text(x + 32, y, w - 64, 72, "退出展览模式？", 22, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 32, y + 92, w - 64, 90, exhibitArg ? "这次是用启动参数 -exhibit 打开的：去掉参数重新启动才会退出。" : "退出后回到正式存档，开发键恢复。展览起点会留着，下次开启还能用。", 15, Ui.Body, TextAnchor.UpperLeft, false, false, true);
                if (Ui.Button(x + 32, y + h - 84, (w - 80) / 2, 56, "取消", 0, true, 16)) exhibitExitPanel = false;
                if (!exhibitArg && Ui.Button(x + 48 + (w - 80) / 2, y + h - 84, (w - 80) / 2, 56, "退出展览模式", 1, true, 16))
                { PlayerPrefs.SetInt(ExhibitPrefKey, 0); PlayerPrefs.Save(); exhibitExitPanel = false; RestartGame(); }
                return;
            }
            Ui.Text(x + 32, y, w - 64, 72, "开启展览模式", 22, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(x + 32, y + 92, w - 64, 150, "开启后：每次启动、每次 3 分钟没人操作，都回到标题、从起点重新开始；展览用的进度单独存，不会动正式存档；标题去掉「新游戏」「退出游戏」，开发用的 F2–F9 关掉。\n退出：按 Ctrl+Shift+E。", 15, Ui.Body, TextAnchor.UpperLeft, false, false, true);
            bool preset = ExhibitPresetJson() != null;
            float bw = (w - 96) / 3, by = y + h - 84;
            if (Ui.Button(x + 32, by, bw, 56, "取消", 0, true, 15)) exhibitPanel = false;
            if (Ui.Button(x + 48 + bw, by, bw, 56, preset ? "用预设：第三章开头" : "没有预设存档", 0, preset, 15))
            { try { if (File.Exists(ExhibitBasePath)) File.Delete(ExhibitBasePath); } catch (Exception) { } StartExhibit(); }
            if (Ui.Button(x + 64 + bw * 2, by, bw, 56, "用当前进度作起点", 1, true, 15))
            {
                Save(); model.state.savedAt = 0;
                try { File.WriteAllText(ExhibitBasePath, JsonUtility.ToJson(model.state, true)); StartExhibit(); }
                catch (Exception e) { Notify("写展览起点失败：" + e.Message, 5); exhibitPanel = false; }
            }
        }
        private void StartExhibit() { Save(); PlayerPrefs.SetInt(ExhibitPrefKey, 1); PlayerPrefs.Save(); exhibitPanel = false; navigation.Close(); RestartGame(); }

        // ---------- 测试 ----------
        public void TestExhibitPanel(int which) { exhibitPanel = which == 0; exhibitExitPanel = which == 1; }
        public void TestExhibitBanner() { ExhibitMode = true; lastMouse = Input.mousePosition; lastInputAt = Time.unscaledTime - ExhibitIdle + 12; }
        public void TestExhibitOff() { ExhibitMode = false; exhibitPanel = exhibitExitPanel = false; }
    }
}
