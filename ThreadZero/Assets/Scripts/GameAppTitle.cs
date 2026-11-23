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
            float t = clock - titleAt, fade = Mathf.Clamp01(t / .9f), now = Time.unscaledTime;
            Ui.FillScreen(Ui.Hex(0x101317));
            float dx = Mathf.Sin(now * .11f) * 14, dy = Mathf.Sin(now * .083f + 1.3f) * 8;   // v0.22 背景慢慢漂（放大 4%，来回各十几像素），不再是一张死图
            if (titleArt != null)
            {
                GUI.DrawTexture(Ui.R(-32 + dx, -18 + dy, 1664, 936), titleArt, ScaleMode.ScaleAndCrop);
                TitleFx(now, dx, dy);
            }
            else TitleThreads(t);
            TitleRain(now);
            for (int i = 0; i < 36; i++) Ui.Fill(i * 24, 0, 24, 900, new Color(.05f, .06f, .08f, .88f * Mathf.Pow(1 - i / 36f, 1.6f)));   // 左边压暗，放标题和菜单
            for (int i = 0; i < 8; i++) Ui.Fill(0, 820 + i * 10, 1600, 10, new Color(.05f, .06f, .08f, .5f * (i + 1) / 8f));            // 底边压暗，放语言和版本
            Ui.FillScreen(new Color(0, 0, 0, 1 - fade));
            bool sub = titleConfirmNew || creditsOpen || navigation.HasModal || exhibitPanel || exhibitExitPanel;
            Ui.Blocked = sub;

            // ---- 标志：线缆织成的字（Tools/title/make_logo.py 生成，四种语言各一张）----
            int lang = (int)Loc.Get();
            Texture2D logo = TitleLogo(lang);
            float la = Mathf.Clamp01((t - .25f) / .7f), slide = (1 - Mathf.SmoothStep(0, 1, la)) * -26;
            if (logo != null)
            {
                Color old = GUI.color; GUI.color = new Color(1, 1, 1, la);
                GUI.DrawTexture(Ui.R(34 + slide, 84, 800, 220), logo, ScaleMode.StretchToFill, true);
                GUI.color = old;
                // 三股线上各有一小段光从屏幕左边流进针眼
                float[] rows = LogoRows[Mathf.Clamp(lang, 0, LogoRows.Length - 1)];
                for (int r = 0; r < 3; r++)
                {
                    float u = Mathf.Repeat(now * .32f + r * .37f, 1.6f); if (u > 1) continue;
                    float gx = Mathf.Lerp(-30, 96, u), ga = Mathf.Sin(u * Mathf.PI) * la;
                    Ui.Fill(gx, 84 + rows[r] - 1.5f, 26, 3, new Color(.82f, 1, .96f, .85f * ga));
                }
            }
            else Ui.FitText(92, 128, 760, 130, "零织协议", 104, Ui.Alpha(Ui.Panel, la), TextAnchor.MiddleLeft, true);
            float sy = 84 + LogoBottom[Mathf.Clamp(lang, 0, 3)] + 24;
            if (lang != 2) { Ui.Text(104, sy, 600, 36, "T H R E A D   Z E R O", 22, Ui.Alpha(Ui.Orange, la), TextAnchor.MiddleLeft, true, true); sy += 36; }
            Ui.FitText(104, sy, 620, 26, "回收、改造、穿上它——直到第零号接上整张网。", 15, Ui.Alpha(Ui.MuteOnInk, la), TextAnchor.MiddleLeft);

            // ---- 菜单：一列文字，第一项最大，带这个存档的进度 ----
            float y = 430; int row = 0;
            string record = hasSave ? (model.InEcho ? "零号深网 · 第 " + model.state.echoFloor + " 层" : Catalog.StageCode(model.state.region) + " " + Catalog.RegionNames[model.state.region]) : null;
            string record2 = hasSave ? "机械飞升 " + Mathf.RoundToInt(model.Ascension * 100) + "% · 零号深网最高 " + model.state.echoBest + " 层 · 外观 " + model.state.collection.Count + " / " + Catalog.All.Length : null;
            if (ExhibitMode)
            {   // v0.21 展览模式：只留开始、设置、制作人员
                if (TitleRow(ref y, ref row, t, "开始体验", true, null, null)) { if (hasSave) CloseTitle(); else StartOpening(); }
            }
            else
            {
                if (hasSave && TitleRow(ref y, ref row, t, "继续", true, record, record2)) CloseTitle();
                if (TitleRow(ref y, ref row, t, hasSave ? "新游戏" : "开始", !hasSave, null, null)) { if (hasSave) titleConfirmNew = true; else StartOpening(); }
            }
            if (TitleRow(ref y, ref row, t, "设置", false, null, null)) navigation.Open(GameModal.Settings);
            if (TitleRow(ref y, ref row, t, "制作人员", false, null, null)) { creditsOpen = true; creditsScroll = Vector2.zero; }
            if (!ExhibitMode && TitleRow(ref y, ref row, t, "退出游戏", false, null, null)) Application.Quit();

            // ---- 底边：版本（v0.22 语言切换收回设置里）----
            float ba = Mathf.Clamp01((t - 1.1f) / .5f);
            Ui.Text(1068, 836, 500, 26, ExhibitMode ? "v0.22 · 毕业制作 · 展览模式" : "v0.22 · 毕业制作", 12, Ui.Alpha(Ui.MuteOnInk, .7f * ba), TextAnchor.MiddleRight);
            Ui.Blocked = exhibitPanel || exhibitExitPanel;
            if (titleConfirmNew) NewGamePanel();
            else if (creditsOpen) CreditsPanel();
            else if (navigation.HasModal) Modal();
            Ui.Blocked = false;
        }
        // 标志里三股线的高度（从图的上沿算，按 800×220 的画法）：中文、日文、英文、韩文
        private static readonly float[][] LogoRows = { new[] { 94f, 120f, 146.5f }, new[] { 98.5f, 116f, 133.5f }, new[] { 93.5f, 106.5f, 120f }, new[] { 102.5f, 115.5f, 129f } };
        private static readonly float[] LogoBottom = { 178, 155, 136, 146 };   // 字的下沿（同上）
        private static readonly string[] LogoFiles = { "logo_zh", "logo_ja", "logo_en", "logo_ko" };
        private readonly Texture2D[] titleLogos = new Texture2D[4]; private readonly bool[] titleLogoTried = new bool[4];
        private Texture2D TitleLogo(int lang)
        {
            lang = Mathf.Clamp(lang, 0, 3);
            if (!titleLogoTried[lang]) { titleLogoTried[lang] = true; titleLogos[lang] = Resources.Load<Texture2D>("Art/V16/Title/" + LogoFiles[lang]); }
            return titleLogos[lang];
        }
        /// 菜单的一行。main：最大的那一项（橙色竖条 + 大字，下面可以带两行说明）；其余是小一号的字，鼠标移上去往右挪一点、前面长出一小段线。
        private bool TitleRow(ref float y, ref int row, float t, string label, bool main, string note, string note2)
        {
            float a = Mathf.Clamp01((t - .6f - row * .09f) / .4f), h = main ? (note != null ? 96 : 64) : 46, w = 520;
            bool hover = a > .9f && Ui.Hover(100, y, w, h);
            if (main)
            {
                Ui.Fill(100, y + 6, hover ? 10 : 6, note != null ? 84 : 52, Ui.Alpha(Ui.Orange, a));
                Ui.Text(126 + (hover ? 4 : 0), y, w - 30, 56, label, 34, Ui.Alpha(hover ? Ui.Orange : Ui.Panel, a), TextAnchor.MiddleLeft, true);
                if (note != null) Ui.FitText(128, y + 52, w - 30, 22, note, 15, Ui.Alpha(Ui.Panel, a * .9f), TextAnchor.MiddleLeft, true);
                if (note2 != null) Ui.FitText(128, y + 72, w - 30, 20, note2, 12, Ui.Alpha(Ui.MuteOnInk, a), TextAnchor.MiddleLeft);
            }
            else
            {
                if (hover) Ui.Fill(100, y + h / 2 - 1, 18, 2, new Color(.27f, .9f, .82f, a));
                Ui.Text(hover ? 128 : 104, y, w - 30, h, label, 21, Ui.Alpha(hover ? Ui.Panel : Ui.MuteOnInk, a), TextAnchor.MiddleLeft, hover);
            }
            bool hit = a > .9f && Ui.Click(100, y, w, h);
            if (hit && Ui.Clicked != null) Ui.Clicked();
            y += h + (main ? 14 : 2); row++; return hit;
        }
        // ---------- v0.22 标题背景上的动态（都是程序画的，位置按 1600×900 的图量出来，跟着背景一起漂）----------
        private static Texture2D titleGlow;
        private static Texture2D TitleGlowTex()
        {
            if (titleGlow != null) return titleGlow;
            const int n = 64; titleGlow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 31.5f, v = Mathf.Clamp01(1 - d);
                titleGlow.SetPixel(x, y, new Color(1, 1, 1, v * v));
            }
            titleGlow.Apply(); return titleGlow;
        }
        private static void Glow(float cx, float cy, float w, float h, Color c)
        {
            if (c.a <= .003f) return;
            Color old = GUI.color; GUI.color = c; GUI.DrawTexture(Ui.R(cx - w / 2, cy - h / 2, w, h), TitleGlowTex(), ScaleMode.StretchToFill, true); GUI.color = old;
        }
        // 天上的线缆（从左到右，终点都在高塔上），照着图描的折线
        private static readonly float[][] TitleCables = {
            new[] { 460f, 0f, 520f, 45f, 600f, 98f, 700f, 160f, 800f, 193f, 900f, 202f, 1000f, 182f, 1060f, 150f, 1118f, 114f },
            new[] { 340f, 0f, 400f, 60f, 500f, 140f, 600f, 200f, 700f, 235f, 800f, 240f, 900f, 228f, 1000f, 195f, 1118f, 124f },
            new[] { 80f, 0f, 150f, 50f, 250f, 110f, 350f, 170f, 450f, 205f, 550f, 228f, 650f, 240f, 750f, 250f, 900f, 245f, 1000f, 215f, 1118f, 134f },
            new[] { 0f, 25f, 100f, 85f, 200f, 125f, 300f, 150f, 400f, 185f, 500f, 215f, 600f, 235f, 700f, 255f, 800f, 262f, 900f, 258f, 1000f, 235f, 1118f, 148f },
            new[] { 800f, 400f, 900f, 345f, 1000f, 270f, 1118f, 162f },
            new[] { 850f, 420f, 950f, 360f, 1050f, 270f, 1120f, 188f } };
        private static Vector2 Along(float[] line, float u)
        {
            int n = line.Length / 2 - 1; float f = Mathf.Clamp01(u) * n; int i = Mathf.Min(n - 1, (int)f); float k = f - i;
            return new Vector2(Mathf.Lerp(line[i * 2], line[i * 2 + 2], k), Mathf.Lerp(line[i * 2 + 1], line[i * 2 + 3], k));
        }
        private void TitleFx(float now, float dx, float dy)
        {   // 11-24 03:15 用户：第一版（漂移 + 雨）的基础上加强一点就行，不要夸张。所以下面都压得很轻。
            System.Func<float, float, Vector2> at = (x, y) => new Vector2(-32 + dx + x * 1.04f, -18 + dy + y * 1.04f);
            Color teal = new Color(.45f, 1f, .95f);
            // 1）高塔慢慢呼吸（约 5 秒一个来回），主角的轮廓光跟着一起
            float breath = .5f + .5f * Mathf.Sin(now * 1.25f);
            Vector2 tower = at(1150, 300);
            Glow(tower.x, tower.y, 560, 900, new Color(teal.r, teal.g, teal.b, .03f + .07f * breath));
            Glow(tower.x, tower.y - 20, 110, 700, new Color(.8f, 1, 1, .04f + .1f * breath));
            Vector2 hero = at(1345, 480); Glow(hero.x - 30, hero.y, 220, 360, new Color(teal.r, teal.g, teal.b, .015f + .035f * breath));
            // 2）四根主线缆上各有一个小光点，慢慢流向高塔
            for (int c = 0; c < 4; c++)
            {
                float u = Mathf.Repeat(now * .09f + c * .27f, 1), a = Mathf.Clamp01(u * 5) * Mathf.Clamp01((1 - u) * 8);
                for (int tail = 2; tail >= 0; tail--)
                {
                    Vector2 q = Along(TitleCables[c], u - tail * .01f), s2 = at(q.x, q.y); float ta = a * (1 - tail / 3f);
                    Glow(s2.x, s2.y, tail == 0 ? 22 : 12, tail == 0 ? 22 : 12, new Color(.8f, 1, .97f, .45f * ta));
                }
            }
            // 3）路灯偶尔暗一下
            float flick = Mathf.PerlinNoise(now * 3f, 3.1f) > .8f ? .55f : 1;
            Vector2 lamp = at(1473, 287);
            Glow(lamp.x, lamp.y, 220, 150, new Color(.72f, .96f, 1f, .16f * flick));
        }
        /// 雨：90 根细线，位置和速度按序号定死（不用随机数，免得每帧跳）。
        private void TitleRain(float now)
        {
            for (int i = 0; i < 90; i++)
            {
                float h1 = Mathf.Repeat(i * .6180339f, 1), h2 = Mathf.Repeat(i * .3819660f + .17f, 1);
                float speed = 780 + 540 * h2, len = 22 + 38 * h2, yy = Mathf.Repeat(now * speed + h1 * 3100, 1000) - 60, xx = 60 + h1 * 1700 - yy * .16f;
                Ui.Stroke(xx, yy, xx - len * .16f, yy + len, 1.4f, new Color(.72f, .88f, 1, .08f + .12f * h2));
            }
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
                if (a > 0) Ui.FitText(80, 330 + i * 64, 1440, 48, OpeningLines[i], 26, Ui.Alpha(i == OpeningLines.Length - 1 ? Ui.Orange : Ui.Panel, a), TextAnchor.MiddleCenter, i == OpeningLines.Length - 1);
            }
            float ba = Mathf.Clamp01((t - end) / .6f);
            ProbeTextDraw();
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
        public string locProbe, probeText;
        /// 测试：在屏幕上用几种字号画一串字（查字体里某个字画得对不对）。
        public void TestProbeText(string t) { probeText = t; }
        private void ProbeTextDraw()
        {
            if (string.IsNullOrEmpty(probeText)) return;
            Ui.Fill(60, 560, 1480, 300, Ui.Ink);
            Ui.Text(80, 570, 1440, 50, probeText, 26, Color.white, TextAnchor.MiddleLeft);
            Ui.Text(80, 630, 1440, 50, probeText, 26, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Text(80, 690, 1440, 40, probeText, 15, Color.white, TextAnchor.MiddleLeft);
            Ui.Text(80, 740, 1440, 90, probeText, 48, Color.white, TextAnchor.MiddleLeft);
        }
        /// 测试：开场第二句在当前语言下前 8 个字的码位（查译文和字体对不对得上）。
        public void TestLocProbe() { string t = Loc.Tr(OpeningLines[1]); var sb = new System.Text.StringBuilder(); for (int i = 0; i < 8 && i < t.Length; i++) sb.Append(((int)t[i]).ToString("X4")).Append(' '); locProbe = sb.ToString(); }
        public void TestCloseTitle() { titleOpen = false; openingAt = -1; creditsOpen = titleConfirmNew = false; navigation.Close(); }
    }
}
