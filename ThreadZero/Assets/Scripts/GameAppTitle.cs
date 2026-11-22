using System;
using System.IO;
using UnityEngine;

namespace ThreadZero
{
    /// v0.21 标题画面与开场。启动先停在标题（继续 / 新游戏 / 设置 / 制作人员 / 退出，下面一排切语言），标题和开场期间战斗暂停；
    /// 新游戏：没有存档时直接放开场；有存档要先确认，原存档复制一份（.archived-时间）再清空、重开、放开场。
    /// 自动测试（TZAutoTest）和匕首审查不进标题。标题图 Resources/Art/V16/Title/title（Gemini 卡 28），没有时画动的织线。
    public sealed partial class GameApp
    {
        private bool titleOpen, titleConfirmNew, creditsOpen, hasSave;
        private float titleAt, openingAt = -1;
        private Vector2 creditsScroll;
        private Texture2D titleArt; private bool titleArtTried;
        private string[] creditsLines;
        private static bool pendingOpening;   // 新游戏重载场景以后直接放开场（静态字段跨场景重载还在）

        private static readonly string[] OpeningLines = {
            "城市的自动化设备失控以后，封锁线里只剩下机器，和回收员。",
            "我的病没有药可治。能换掉的零件，都在封锁区里。",
            "回收员编号：零。今天开始回收。" };
        private const float OpeningStep = 2.4f;

        /// Awake 读完档以后调用：决定先进标题、直接放开场，还是直接开始（测试）。
        private void SetupTitle(bool loadedSave)
        {
            hasSave = loadedSave;
            if (pendingOpening) { pendingOpening = false; StartOpening(); return; }
            titleOpen = !daggerReview && !TZAutoTest.Running;
            titleAt = clock;
        }
        private bool TitleBlocking { get { return titleOpen || openingAt >= 0; } }
        private void CloseTitle() { titleOpen = false; titleConfirmNew = creditsOpen = false; navigation.Close(); ApplyOffline(); }
        private void StartOpening() { titleOpen = false; openingAt = clock; navigation.Close(); }
        private void EndOpening() { openingAt = -1; Go(GameScreen.Action); ApplyOffline(); }
        /// 新游戏（有存档时）：复制一份原存档，清空，重开以后放开场。
        private void NewGame()
        {
            try { if (File.Exists(saves.path)) File.Copy(saves.path, saves.path + ".archived-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), false); }
            catch (Exception e) { Debug.LogWarning("THREAD ZERO: 新游戏前备份存档失败：" + e.Message); Notify("原存档备份失败，没有清空。", 5); titleConfirmNew = false; return; }
            saves.Delete(); pendingOpening = true; RestartGame();
        }
        /// 设置里「回到标题」。
        private void BackToTitle() { Save(); navigation.Close(); titleOpen = true; titleAt = clock; hasSave = true; }

        // ---------- 标题 ----------
        private void TitleScreen()
        {
            if (!titleArtTried) { titleArtTried = true; titleArt = Resources.Load<Texture2D>("Art/V16/Title/title"); }
            float t = clock - titleAt, fade = Mathf.Clamp01(t / .9f);
            Ui.FillScreen(Ui.Hex(0x101317));
            if (titleArt != null) GUI.DrawTexture(Ui.R(0, 0, 1600, 900), titleArt, ScaleMode.ScaleAndCrop);
            else TitleThreads(t);
            for (int i = 0; i < 32; i++) Ui.Fill(i * 24, 0, 24, 900, new Color(.06f, .07f, .09f, .9f * Mathf.Pow(1 - i / 32f, 1.5f)));   // 左边压暗，放标题和菜单
            Ui.FillScreen(new Color(0, 0, 0, 1 - fade));
            bool sub = titleConfirmNew || creditsOpen || navigation.HasModal || exhibitPanel || exhibitExitPanel;
            Ui.Blocked = sub;

            Ui.Text(92, 128, 900, 130, "零织协议", 104, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(98, 252, 800, 52, "THREAD ZERO", 42, Ui.Orange, TextAnchor.MiddleLeft, true, true);
            Ui.Fill(100, 312, 380, 4, Ui.Orange);
            Ui.Text(100, 324, 600, 26, "回收、改造、穿上它——直到第零号接上整张网。", 15, Ui.MuteOnInk, TextAnchor.MiddleLeft);

            float y = 384;
            if (ExhibitMode)
            {   // v0.21 展览模式：只留开始、设置、制作人员
                if (TitleButton(ref y, "开始体验", 1)) { if (hasSave) CloseTitle(); else StartOpening(); }
            }
            else
            {
                if (hasSave && TitleButton(ref y, "继续", 1)) CloseTitle();
                if (TitleButton(ref y, hasSave ? "新游戏" : "开始", hasSave ? 0 : 1)) { if (hasSave) titleConfirmNew = true; else StartOpening(); }
            }
            if (TitleButton(ref y, "设置", 0)) navigation.Open(GameModal.Settings);
            if (TitleButton(ref y, "制作人员", 0)) { creditsOpen = true; creditsScroll = Vector2.zero; }
            if (!ExhibitMode && TitleButton(ref y, "退出游戏", 0)) Application.Quit();
            y += 12;
            for (int i = 0; i < Loc.Names.Length; i++)   // 语言（名字本身不翻译）
                if (Ui.Button(100 + i * 92, y, 86, 40, Loc.Names[i], (int)Loc.Get() == i ? 2 : 0, true, 13)) Loc.Set((Loc.Lang)i);

            if (hasSave)
            {   // 右下：这个存档的记录
                string record = "机械飞升 " + Mathf.RoundToInt(model.Ascension * 100) + "% · 零号深网最高 " + model.state.echoBest + " 层 · 外观 " + model.state.collection.Count + " / " + Catalog.All.Length;
                string where = model.InEcho ? "零号深网 · 第 " + model.state.echoFloor + " 层" : Catalog.StageCode(model.state.region) + " " + Catalog.RegionNames[model.state.region];
                float rw = Mathf.Max(Ui.Width(record, 14), Ui.Width(where, 18, true)) + 48;
                Ui.Fill(1568 - rw, 770, rw, 82, new Color(.06f, .07f, .09f, .78f)); Ui.Fill(1568 - rw, 770, 4, 82, Ui.Orange);
                Ui.Text(1568 - rw + 24, 778, rw - 36, 34, where, 18, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(1568 - rw + 24, 812, rw - 36, 30, record, 14, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            }
            Ui.Text(100, 852, 600, 24, ExhibitMode ? "v0.21 · 毕业制作 · 展览模式" : "v0.21 · 毕业制作", 12, Ui.Alpha(Ui.MuteOnInk, .8f), TextAnchor.MiddleLeft);
            Ui.Blocked = exhibitPanel || exhibitExitPanel;
            if (titleConfirmNew) NewGamePanel();
            else if (creditsOpen) CreditsPanel();
            else if (navigation.HasModal) Modal();
            Ui.Blocked = false;
        }
        private bool TitleButton(ref float y, string label, int style)
        {
            bool hit = Ui.Button(100, y, 380, 56, label, style, true, 19);
            y += 66; return hit;
        }
        /// 没有标题图时的背景：几根慢慢摆动的青绿织线。
        private void TitleThreads(float t)
        {
            Color c = new Color(.25f, .9f, .8f, .22f);
            for (int k = 0; k < 9; k++)
            {
                float x0 = 640 + k * 110, phase = t * .35f + k * .9f;
                for (int s = 0; s < 18; s++)
                {
                    float y0 = s * 50, y1 = y0 + 50;
                    float a = x0 + Mathf.Sin(phase + y0 * .006f) * 40, b = x0 + Mathf.Sin(phase + y1 * .006f) * 40;
                    Ui.Stroke(a, y0, b, y1, 2, c);
                }
            }
        }
        private void NewGamePanel()
        {
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .6f));
            float x = 500, y = 300, w = 600, h = 300;
            Ui.Fill(x, y, w, h, Ui.Panel); Ui.Fill(x, y, w, 72, Ui.Ink); Ui.Fill(x, y, 8, 72, Ui.Orange); Ui.Frame(x, y, w, h, 2, Ui.Ink);
            Ui.Text(x + 32, y, w - 64, 72, "开始新游戏？", 22, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(x + 32, y + 92, w - 64, 96, "现在的进度会清空，从第一章重新开始。原存档会先复制一份留在存档文件夹里（文件名带 archived 和时间），不会真的丢。", 15, Ui.Body, TextAnchor.UpperLeft, false, false, true);
            if (Ui.Button(x + 32, y + h - 84, (w - 80) / 2, 56, "取消", 0, true, 16)) titleConfirmNew = false;
            if (Ui.Button(x + 48 + (w - 80) / 2, y + h - 84, (w - 80) / 2, 56, "清空并开始", 1, true, 16)) NewGame();
        }
        private void CreditsPanel()
        {
            if (creditsLines == null)
            {
                TextAsset a = Resources.Load<TextAsset>("Credits");
                creditsLines = (a != null ? a.text : "零织协议 / THREAD ZERO").Replace("\r", "").Split('\n');
            }
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .72f));
            float x = 400, y = 60, w = 800, h = 780;
            Ui.Fill(x, y, w, h, Ui.Panel); Ui.Fill(x, y, w, 72, Ui.Ink); Ui.Fill(x, y, 8, 72, Ui.Orange); Ui.Frame(x, y, w, h, 2, Ui.Ink);
            Ui.Text(x + 32, y, w - 160, 72, "制作人员", 22, Ui.Panel, TextAnchor.MiddleLeft, true);
            if (Ui.Button(x + w - 108, y + 14, 92, 44, "关闭", 3, true, 15)) { creditsOpen = false; return; }
            // 一行一行画（每行单独查翻译表）：「# 」开头是小标题，空行是间隔
            float cy = y + 96 - creditsScroll.y, bottom = y + h - 20;
            float total = 0;
            foreach (string raw in creditsLines) total += raw.StartsWith("# ") ? 40 : raw.Trim().Length == 0 ? 14 : 28;
            float maxScroll = Mathf.Max(0, total - (h - 116));
            if (Event.current.type == EventType.ScrollWheel) { creditsScroll.y = Mathf.Clamp(creditsScroll.y + Event.current.delta.y * 24, 0, maxScroll); Event.current.Use(); }
            Ui.BeginClip(x, y + 80, w, h - 96, 0);
            foreach (string raw in creditsLines)
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("# ")) { if (cy > y + 40 && cy < bottom) Ui.Text(x + 40, cy + 8, w - 80, 30, line.Substring(2), 13, Ui.Orange, TextAnchor.MiddleLeft, true); cy += 40; }
                else if (line.Trim().Length == 0) cy += 14;
                else { if (cy > y + 40 && cy < bottom) Ui.Text(x + 40, cy, w - 80, 28, line, 15, Ui.Ink, TextAnchor.MiddleLeft); cy += 28; }
            }
            Ui.EndClip();
        }

        // ---------- 开场 ----------
        private void OpeningScreen()
        {
            if (!titleArtTried) { titleArtTried = true; titleArt = Resources.Load<Texture2D>("Art/V16/Title/title"); }
            float t = clock - openingAt, end = 1f + OpeningLines.Length * OpeningStep;
            Ui.FillScreen(Ui.Hex(0x0B0D10));
            if (titleArt != null)
            {   // 标题图压暗、慢慢推近
                float z = 1.04f + t * .006f; Color old = GUI.color; GUI.color = new Color(.32f, .34f, .38f, Mathf.Clamp01(t / 2f));
                GUI.DrawTexture(Ui.R(800 - 800 * z, 450 - 450 * z, 1600 * z, 900 * z), titleArt, ScaleMode.ScaleAndCrop); GUI.color = old;
            }
            for (int i = 0; i < OpeningLines.Length; i++)
            {
                float a = Mathf.Clamp01((t - 1f - i * OpeningStep) / .9f);
                if (a > 0) Ui.Text(0, 330 + i * 64, 1600, 48, OpeningLines[i], 26, Ui.Alpha(i == OpeningLines.Length - 1 ? Ui.Orange : Ui.Panel, a), TextAnchor.MiddleCenter, i == OpeningLines.Length - 1);
            }
            float ba = Mathf.Clamp01((t - end) / .6f);
            if (ba > 0 && Ui.Button(650, 600, 300, 60, "开始回收", 1, true, 19)) { EndOpening(); return; }
            if (Ui.Button(1440, 24, 136, 44, "跳过 ›", 0, true, 14)) { EndOpening(); return; }
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Escape))
            { if (t >= end || e.keyCode == KeyCode.Escape) EndOpening(); else openingAt = clock - end; e.Use(); }   // 没放完先跳到全部显示，再按一次开始
        }

        // ---------- 测试 ----------
        public void TestTitle(int saved) { hasSave = saved != 0; titleOpen = true; titleAt = clock - 5; titleConfirmNew = creditsOpen = false; navigation.Close(); }
        public void TestCredits() { TestTitle(1); creditsOpen = true; }
        public void TestOpening(float at) { titleOpen = false; openingAt = clock - at; }
        public void TestCloseTitle() { titleOpen = false; openingAt = -1; creditsOpen = titleConfirmNew = false; navigation.Close(); }
    }
}
