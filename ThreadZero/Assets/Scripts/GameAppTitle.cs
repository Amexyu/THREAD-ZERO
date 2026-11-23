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
        // ---------- 菜单的悬停效果（11-24）：一根青线从屏幕左边穿过一个橙色针眼，接到选项上，和标志的「线穿过 0」是同一个意思 ----------
        private readonly float[] titleHover = new float[8], titleHoverAt = new float[8];
        private readonly bool[] titleWasHover = new bool[8];
        private int titleForceHover = -1;   // 测试用：强制某一行处于悬停
        public void TestTitleHover(int row) { titleForceHover = row; }
        private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x) * (1 - x); }
        private bool TitleRow(ref float y, ref int row, float t, string label, bool main, string note, string note2)
        {
            float a = Mathf.Clamp01((t - .6f - row * .09f) / .4f), h = main ? (note != null ? 96 : 64) : 46, w = 520;
            int r = Mathf.Min(row, titleHover.Length - 1);
            bool hover = a > .9f && (titleForceHover == row || Ui.Hover(100, y, w, h));
            float now = Time.unscaledTime;
            if (Event.current.type == EventType.Repaint)
            {   // 进入 0.22 秒、离开 0.3 秒；记下这次进入的时间，用来放一次「穿线」
                float dt = Mathf.Clamp(now - titleHoverAt[r], 0, .1f); titleHoverAt[r] = now;
                titleHover[r] = Mathf.Clamp01(titleHover[r] + (hover ? dt / .22f : -dt / .3f));
                if (hover && !titleWasHover[r]) titleHoverEnter[r] = now;
                titleWasHover[r] = hover;
            }
            float p = EaseOut(titleHover[r]), since = now - titleHoverEnter[r], cy = y + (main ? 28 : h / 2);
            Color teal = new Color(.35f, .95f, .86f), tealHi = new Color(.85f, 1f, .97f);
            if (p > .001f)
            {
                // 底下一条从左往右淡掉的光带，宽度跟着展开
                float bw = 420 * p;
                for (int i = 0; i < 12; i++) Ui.Fill(96 + bw * i / 12f, y + 2, bw / 12f + 1, h - 4, new Color(teal.r, teal.g, teal.b, .13f * p * (1 - i / 12f) * (1 - i / 12f)));
                Ui.Fill(96, y + 2, bw * .6f, 1, new Color(teal.r, teal.g, teal.b, .35f * p));
                // 线：从屏幕左边拉到针眼，再穿过针眼接到字前
                float x0 = Mathf.Lerp(100, 0, p), xe = 112;
                Ui.Fill(x0, cy - 1, xe - x0, 2, new Color(teal.r, teal.g, teal.b, .85f * p));
                // 进入时一颗亮点沿着线跑一次
                float run = Mathf.Clamp01(since / .35f);
                if (hover && run < 1) { float rx = Mathf.Lerp(0, 118, EaseOut(run)); Ui.Fill(rx - 10, cy - 2, 20, 4, new Color(tealHi.r, tealHi.g, tealHi.b, .9f * (1 - run * .6f))); }
            }
            if (main)
            {
                float bar = Mathf.Lerp(6, 10, p);
                Ui.Fill(100, y + 6, bar, note != null ? 84 : 52, Ui.Alpha(Ui.Orange, a));
                if (p > .001f) Ui.Fill(100 + bar, cy - 1, 14 * p, 2, new Color(teal.r, teal.g, teal.b, .85f * p));   // 线接进橙色竖条
                float lx = 126 + 10 * p;
                TitleGlitch(lx, y, w - 30, 56, label, 34, Ui.Alpha(Ui.Panel, a), since, hover);
                if (note != null) Ui.FitText(lx + 2, y + 52, w - 30, 22, note, 15, Ui.Alpha(Ui.Panel, a * Mathf.Lerp(.85f, 1, p)), TextAnchor.MiddleLeft, true);
                if (note2 != null) Ui.FitText(lx + 2, y + 72, w - 30, 20, note2, 12, Ui.Alpha(Color.Lerp(Ui.MuteOnInk, Ui.Panel, p * .5f), a), TextAnchor.MiddleLeft);
            }
            else
            {
                // 橙色针眼：一个竖着的小圆角块，线从它中间穿过去
                if (p > .001f)
                {
                    float eh = 16 * p;
                    Ui.Fill(104, cy - eh / 2, 8, eh, Ui.Alpha(Ui.Orange, p)); Ui.Fill(106, cy - eh / 2 + 3, 4, Mathf.Max(0, eh - 6), new Color(.06f, .07f, .09f, p));
                    Ui.Fill(103, cy - 1, 10, 2, new Color(teal.r, teal.g, teal.b, .9f * p));
                }
                float lx = 104 + 24 * p;
                TitleGlitch(lx, y, w - 30, h, label, 21, Color.Lerp(Ui.Alpha(Ui.MuteOnInk, a), Ui.Alpha(Ui.Panel, a), p), since, hover);
                // 右边一个小箭头滑进来
                if (p > .01f) Ui.Text(lx + Ui.Width(label, 21) + 14 + 10 * (1 - p), y, 30, h, "›", 22, new Color(teal.r, teal.g, teal.b, p), TextAnchor.MiddleLeft, true);
            }
            bool hit = a > .9f && Ui.Click(100, y, w, h);
            if (hit && Ui.Clicked != null) Ui.Clicked();
            y += h + (main ? 14 : 2); row++; return hit;
        }
        private readonly float[] titleHoverEnter = new float[8];
        /// 字：刚移上去的 0.12 秒里，左右各错开一层青 / 橙（像信号抖了一下），之后就是普通的字。
        private static void TitleGlitch(float x, float y, float w, float h, string label, float size, Color c, float since, bool hover)
        {
            bool bold = size > 30 || hover;
            if (hover && since < .12f)
            {
                float k = 1 - since / .12f, off = 3 * k;
                Ui.Text(x - off, y, w, h, label, size, new Color(.35f, .95f, .86f, .7f * k), TextAnchor.MiddleLeft, bold);
                Ui.Text(x + off, y, w, h, label, size, new Color(1f, .29f, .07f, .6f * k), TextAnchor.MiddleLeft, bold);
            }
            Ui.Text(x, y, w, h, label, size, c, TextAnchor.MiddleLeft, bold);
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
            // 1）高塔：第五章「织网心室在模仿一颗跳动的心脏」，所以塔光不是匀速的呼吸，而是很慢的心跳（扑通—扑通—停），约 3.2 秒一次
            float hb = Mathf.Repeat(now / 3.2f, 1), beat = Mathf.Exp(-(hb - .05f) * (hb - .05f) / .0022f) + .65f * Mathf.Exp(-(hb - .2f) * (hb - .2f) / .0028f);
            float breath = .25f + .75f * Mathf.Clamp01(beat);
            Vector2 tower = at(1150, 300);
            Glow(tower.x, tower.y, 560, 900, new Color(teal.r, teal.g, teal.b, .03f + .09f * breath));
            Glow(tower.x, tower.y - 20, 110, 700, new Color(.8f, 1, 1, .03f + .12f * breath));
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
            TitleHero(now, dx, dy, breath);
            // 3）路灯偶尔暗一下
            float flick = Mathf.PerlinNoise(now * 3f, 3.1f) > .8f ? .55f : 1;
            Vector2 lamp = at(1473, 287);
            Glow(lamp.x, lamp.y, 220, 150, new Color(.72f, .96f, 1f, .16f * flick));
        }
        // ---------- 主角（11-24）：从背景里抠出来单独画，底下的背景换成「去人」的那一版，所以可以动 ----------
        // 人物层 Art/V16/Title/hero.png 是原图（2752×1536）里 (2132,566)–(2540,1480) 这一块；脚底在 y = 1036，下面是影子和倒影。
        private Texture2D titleHero; private bool titleHeroTried; private static Material titleHeroMat;
        private const float HeroX0 = 2132, HeroY0 = 566, HeroX1 = 2540, HeroY1 = 1480, HeroFeet = 1036, ArtW = 2752, ArtH = 1536;
        private static Vector2 ArtToLogic(float x, float y, float dx, float dy) { return new Vector2(-32 + dx + x * 1664f / ArtW, -18 + dy + y * 936f / ArtH); }
        private void TitleHero(float now, float dx, float dy, float beat)
        {
            if (!titleHeroTried) { titleHeroTried = true; titleHero = Resources.Load<Texture2D>("Art/V16/Title/hero"); }
            if (titleHero == null) return;
            float gust = Mathf.PerlinNoise(now * .18f, 1.7f);                    // 和雨用同一阵风
            float breathe = .5f + .5f * Mathf.Sin(now * Mathf.PI * 2 / 4.4f);  // 呼吸：约 4.4 秒一次，肩背起伏
            if (Event.current.type == EventType.Repaint)
            {
                if (titleHeroMat == null) titleHeroMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                const int NX = 16, NY = 36;
                var pts = new Vector3[(NX + 1) * (NY + 1)];
                for (int j = 0; j <= NY; j++)
                    for (int i = 0; i <= NX; i++)
                    {
                        float X = Mathf.Lerp(HeroX0, HeroX1, i / (float)NX), Y = Mathf.Lerp(HeroY0, HeroY1, j / (float)NY), ox = 0, oy = 0;
                        // 呼吸：脚底不动，越往上抬得越多；影子和倒影反过来跟着动
                        if (Y < HeroFeet) oy = -breathe * (HeroFeet - Y) * .009f; else oy = breathe * (Y - HeroFeet) * .005f;
                        // 风：头发（越往右上越动）和飘起来的衣角（越往右越动）
                        float wh = Mathf.Clamp01((X - 2210) / 200) * Mathf.Clamp01((770 - Y) / 160);
                        float wt = Mathf.Clamp01((X - 2360) / 140) * Mathf.Clamp01(1 - Mathf.Abs(Y - 836) / 90);
                        float wr = Mathf.Clamp01((X - 2380) / 140) * Mathf.Clamp01(1 - Mathf.Abs(Y - 1250) / 140) * .5f;   // 倒影里的衣角也跟着摆一点
                        float w = wh + wt * 1.3f + wr;
                        if (w > 0)
                        {
                            float fl = Mathf.Sin(now * 8.5f + Y * .035f + X * .012f) * (.5f + gust), fl2 = Mathf.Sin(now * 12.7f + X * .05f + Y * .02f);
                            ox += w * (gust * 12 - 4 + fl * 5);
                            oy += w * (fl2 * 3.5f + Mathf.Sin(now * 5.9f + X * .03f) * 2) * (Y > HeroFeet ? -1 : 1);
                        }
                        Vector2 l = ArtToLogic(X + ox, Y + oy, dx, dy);
                        Vector2 sp = GUIUtility.GUIToScreenPoint(Ui.P(l.x, l.y));
                        pts[j * (NX + 1) + i] = new Vector3(sp.x, sp.y, 0);
                    }
                GL.PushMatrix(); GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
                titleHeroMat.mainTexture = titleHero; titleHeroMat.SetPass(0);
                GL.Begin(GL.QUADS); GL.Color(Color.white);
                for (int j = 0; j < NY; j++)
                    for (int i = 0; i < NX; i++)
                    {
                        float u0 = i / (float)NX, u1 = (i + 1) / (float)NX, v0 = 1 - j / (float)NY, v1 = 1 - (j + 1) / (float)NY;
                        GL.TexCoord2(u0, v0); GL.Vertex(pts[j * (NX + 1) + i]);
                        GL.TexCoord2(u1, v0); GL.Vertex(pts[j * (NX + 1) + i + 1]);
                        GL.TexCoord2(u1, v1); GL.Vertex(pts[(j + 1) * (NX + 1) + i + 1]);
                        GL.TexCoord2(u0, v1); GL.Vertex(pts[(j + 1) * (NX + 1) + i]);
                    }
                GL.End(); GL.PopMatrix();
            }
            // 背包上的橙色指示灯：慢慢亮、短暂熄一下（像设备在待机）
            float led = Mathf.Repeat(now / 2.6f, 1), on = led < .82f ? 1 : .15f;
            Vector2 lp = ArtToLogic(2330, 841 - breathe * (HeroFeet - 841) * .009f, dx, dy);
            Glow(lp.x, lp.y, 38, 26, new Color(1f, .35f, .08f, .55f * on)); Glow(lp.x, lp.y, 10, 6, new Color(1f, .8f, .5f, .9f * on));
            // 脚边积水里的涟漪
            for (int i = 0; i < 7; i++)
            {
                float life = .9f + .4f * Hash(i, .2f), ph = now / life + Hash(i, 4.1f) * 5, k = Mathf.Floor(ph), u = ph - k;
                Vector2 c = ArtToLogic(Mathf.Lerp(2060, 2620, Hash(i, k)), Mathf.Lerp(1050, 1420, Hash(i, k + .5f)), dx, dy);
                float rx = 3 + 20 * u, ry = rx * .28f, al = .32f * (1 - u) * (1 - u);
                for (int sgm = 0; sgm < 14; sgm++)
                {
                    float a0 = sgm / 14f * Mathf.PI * 2, a1 = (sgm + 1) / 14f * Mathf.PI * 2;
                    Ui.Stroke(c.x + Mathf.Cos(a0) * rx, c.y + Mathf.Sin(a0) * ry, c.x + Mathf.Cos(a1) * rx, c.y + Mathf.Sin(a1) * ry, 1.2f, new Color(.7f, .95f, 1f, al));
                }
            }
        }
        /// 雨（11-24 改：原来每根雨都按固定的位置、速度循环，一眼就看得出规律）。
        /// 现在每根雨每落一次都重新随机：横向位置、速度、长度、深浅；风向随时间慢慢变（有阵风），雨量也一阵大一阵小。
        private static float Hash(float a, float b) { float v = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f; return v - Mathf.Floor(v); }
        private void TitleRain(float now)
        {
            float gust = Mathf.PerlinNoise(now * .18f, 1.7f), density = Mathf.Lerp(.55f, 1f, Mathf.PerlinNoise(now * .11f, 8.3f));
            for (int i = 0; i < 130; i++)
            {
                bool far = i < 85;
                float spd = (far ? 640 : 1050) * (.8f + .45f * Hash(i, .5f)), period = 1060 / spd;
                float ph = now / period + Hash(i, 1.3f) * 9, k = Mathf.Floor(ph), u = ph - k;
                if (Hash(i, k + .7f) > density) continue;                              // 这一轮这根不下
                float y = -70 + u * 1060, x0 = -120 + Hash(i, k) * 1900;
                float wind = .07f + .2f * gust + .05f * (Hash(i, k + .3f) - .5f);         // 每根略有不同，整体随阵风摆
                float len = far ? 16 + 26 * Hash(i, k + .9f) : 34 + 46 * Hash(i, k + 1.9f);
                float al = far ? .045f + .09f * Hash(i, k + 2.3f) : .08f + .12f * Hash(i, k + 3.1f);
                float x = x0 - y * wind;
                Ui.Stroke(x, y, x - len * wind, y + len, far ? 1.1f : 1.7f, new Color(.72f, .88f, 1, al));
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
