using System.Collections;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 轻量化挂机：透明挂机窗。来回改了几轮（用户在 Windows 打包版上试）：
    ///   19:14「挂机条太占屏幕，帧数不好，得改形式」→ 看例图选了透明窗口（背景透出桌面，只画角色、敌人、特效和脚下一条细轨道）。
    ///   20:00「标签太大，也不用常态显示」→ 标签平时收起，鼠标停半秒才滑上来；有事在主角头顶冒气泡；挂机窗里不震屏、不推镜头、不顿帧。
    ///   20:49「太长了；只能在底边拖、还要先悬停出标签才能拖，麻烦；要能拖到屏幕任何地方，但不能拖出屏幕」→
    ///     宽度只框主角和前面一段（960 逻辑单位，M 档约 480 像素宽）；按住窗口任何地方（按钮除外）就能拖，横竖都行，限制在屏幕可用区域里；
    ///     位置记在本机。窗口里的点击都归游戏（不再点击穿透），这样才能随手拖。
    ///   21:11「打开时先出右下角小窗再变全屏；挂机条状态没法直接退出；挂机时为什么自动重刷当前关」→
    ///     菜单加「退出」（Alt+F4 也一样）：先藏窗口、摆回进挂机前的全屏 / 窗口再退，下次直接按原样开（GameAppWindow.cs）；
    ///     回刷时标签写「回刷 · 再 N 次挑战 x-y」，开始回刷的气泡写倒在哪关、回刷哪关。
    /// 窗口：无边框、置顶、背景透明（Windows：DWM + D3D11 + 关 flip model；Mac：NSWindow 不透明关掉）。
    /// 高 = 工作区高的 11% / 14% / 18%（S / M / L）。透明在某台电脑上不灵（看到黑底）时，菜单里关掉「透明」就画回关卡背景。
    /// 「展开」回到进挂机前的全屏 / 窗口。左边导航栏「挂机」和设置「显示 · 挂机条」都能进。
    /// 编辑器里不改窗口，只在 Game 视图里按真实比例画一块（周围当桌面），也能拖，方便截图检查。
    public sealed partial class GameApp
    {
        public enum CompactKind { Off = 0, Strip = 1 }
        private CompactKind compact = CompactKind.Off;
        private bool compactView;        // ActionPage 只画舞台
        private bool compactTop = true;  // 置顶
        private bool compactBusy, savedWindow;
        private FullScreenMode savedMode; private int savedW, savedH; private Vector2Int savedPos;
        private RectInt savedOuter, savedMon; private bool savedOuterOk, savedMonOk;   // Windows：进挂机前普通窗口的外框、所在的那块显示器（屏幕像素），展开 / 退出时摆回去
        private int bareStage;   // 0 正常；1 / 2 背景填黑 / 白（桥接 set bareStage，抠样图）；3 不画背景（透明挂机窗）
        private int stripSize = 1;          // 0 S / 1 M / 2 L
        private bool stripClear = true;     // 背景透明；关掉 = 画回关卡背景
        private bool stripMenu, stripDrag;
        private bool stripHasPos; private Vector2Int stripPos;   // 拖过以后记下的位置（屏幕像素，窗口左上角）；没拖过就放右下角
        private Vector2 editorPos = new Vector2(-1, -1);          // 编辑器预览里那块的位置（Game 视图像素）
        // 20:00 用户：「这个东西太大了，也不需要常态显示」→ 平时不显示，鼠标停半秒浮出一行小标签；有事冒气泡
        private bool stripShow, stripForce, stripEditorInside, lastFarming;
        private float stripAnim, stripHoverT, stripAwayT;
        private string toastText, lastNotice; private float toastAt = -10;
        private readonly System.Collections.Generic.Queue<string> stripToasts = new System.Collections.Generic.Queue<string>();
        private Color savedClear; private bool clearSaved;
        private RectInt stripRect; private float stripCheckAt;   // 挂机窗应该在的位置（屏幕像素）；每半秒对一次，跑偏了就摆回来
        private const string CompactTopKey = "tz_compact_top", StripSizeKey = "tz_strip_size", StripPosKey = "tz_strip_pos", StripClearKey = "tz_strip_clear";
        private static readonly float[] StripShares = { .11f, .14f, .18f };
        private const float ViewTop = GroundY - 290, ViewH = 300;   // 舞台里露出来的高度（逻辑坐标）：头顶的血条、无人机、跳字到脚下一点
        private const float ViewSpan = 960, ViewLead = 300;          // 露出来的宽度：主角左边 300、右边 660（敌人从右边进来）

        private CompactKind LastCompact { get { return CompactKind.Strip; } }
        private void LoadCompactPrefs()
        {
            compactTop = PlayerPrefs.GetInt(CompactTopKey, 1) == 1;
            stripSize = Mathf.Clamp(PlayerPrefs.GetInt(StripSizeKey, 1), 0, StripShares.Length - 1);
            stripClear = PlayerPrefs.GetInt(StripClearKey, 1) == 1;
            string[] xy = PlayerPrefs.GetString(StripPosKey, "").Split(',');
            int px = 0, py = 0;
            stripHasPos = xy.Length == 2 && int.TryParse(xy[0], out px) && int.TryParse(xy[1], out py);
            if (stripHasPos) stripPos = new Vector2Int(px, py);
        }
        private void SaveStripPrefs()
        {
            PlayerPrefs.SetInt(CompactTopKey, compactTop ? 1 : 0); PlayerPrefs.SetInt(StripSizeKey, stripSize);
            PlayerPrefs.SetInt(StripClearKey, stripClear ? 1 : 0);
            if (stripHasPos) PlayerPrefs.SetString(StripPosKey, stripPos.x + "," + stripPos.y);
            PlayerPrefs.Save();
        }
        /// 现在是不是透明着画（编辑器里按设置预览；打包版还要看显卡接口能不能透明）。
        private bool StripTransparent { get { return stripClear && (Application.isEditor || WindowCtl.CanTransparent); } }

        public void EnterCompact(CompactKind kind)
        {
            if (kind == CompactKind.Off) { ExitCompact(); return; }
            if (ExhibitMode || compactBusy || compact != CompactKind.Off) return;
            navigation.Go(GameScreen.Action); hudShow = 0; stripMenu = false; stripDrag = false;
            compact = CompactKind.Strip;
            stripShow = false; stripAnim = 0; stripHoverT = 0; stripAwayT = 9; stripToasts.Clear(); toastText = null;
            lastNotice = StripNotice(); lastFarming = model.Farming;
            if (PlayerPrefs.GetInt("tz_strip_tip2", 0) == 0) { PlayerPrefs.SetInt("tz_strip_tip2", 1); PlayerPrefs.Save(); StripToast("按住能拖到任何地方 · 鼠标停一下出菜单"); }
            if (!Application.isEditor) StartCoroutine(PlaceStrip(true));
        }
        public void ExitCompact()
        {
            if (compact == CompactKind.Off || compactBusy) return;
            compact = CompactKind.Off; stripMenu = false; stripDrag = false; stripToasts.Clear(); toastText = null;
            if (!Application.isEditor) StartCoroutine(RestoreWindow());
        }
        /// 挂机窗的大小（像素）：高按工作区高的比例，宽按露出来的那段舞台的比例。
        private Vector2Int StripSizePx(RectInt work)
        {
            int h = Mathf.Clamp(Mathf.RoundToInt(work.height * StripShares[stripSize]), 90, 420);
            return new Vector2Int(Mathf.Min(work.width, Mathf.RoundToInt(h * ViewSpan / ViewH)), h);
        }
        /// 挂机窗的位置和大小（屏幕像素）：拖过就用记下的位置，没拖过放右下角（贴着任务栏）；都限制在工作区里。
        private RectInt StripRect(RectInt work)
        {
            Vector2Int size = StripSizePx(work);
            int x = stripHasPos ? stripPos.x : work.x + work.width - size.x - 16;
            int y = stripHasPos ? stripPos.y : work.y + work.height - size.y;
            x = Mathf.Clamp(x, work.x, Mathf.Max(work.x, work.x + work.width - size.x));
            y = Mathf.Clamp(y, work.y, Mathf.Max(work.y, work.y + work.height - size.y));
            return new RectInt(x, y, size.x, size.y);
        }
        /// 放挂机窗的那块工作区：Windows 上按记下的位置找它在哪个显示器（多显示器、换过显示器都对），别的平台用 Unity 的。
        private RectInt StripWork(out DisplayInfo display)
        {
            RectInt work = WindowCtl.WorkArea(out display);
            if (!WindowCtl.PlacesWindow) return work;
            if (stripHasPos) { RectInt at = WindowCtl.WorkAreaAt(new Vector2Int(stripPos.x + 40, stripPos.y + 40)); if (at.width > 0) return at; }
            return WindowCtl.ScreenWorkArea();
        }
        private IEnumerator PlaceStrip(bool remember)
        {
            compactBusy = true;
            if (remember && !savedWindow)
            {
                savedWindow = true; savedMode = Screen.fullScreenMode; savedW = Screen.width; savedH = Screen.height;
                try { savedPos = Screen.mainWindowPosition; } catch (System.Exception) { savedPos = Vector2Int.zero; }
                savedOuterOk = savedMode == FullScreenMode.Windowed && WindowCtl.WindowRect(out savedOuter);
                savedMonOk = WindowCtl.MonitorRect(out savedMon);   // 挂机窗能拖到别的显示器：回来时全屏要回到原来那块
                RememberNormalWindow();   // 万一没摆回去就退了（崩溃、被结束），下次启动按这个恢复
            }
            DisplayInfo display; RectInt work = StripWork(out display);
            RectInt r = StripRect(work);
            stripRect = r;
            // Unity 的 SetResolution 改大小时顶着左上角往下长，而且比我们摆窗口晚一两帧（20:28 用户：调整大小之后会出屏幕）。
            // 所以只有从全屏 / 大窗口进来时才用它（要先退出全屏），并等它真的改完；Windows 上换大小直接 SetWindowPos；之后每半秒再对一次位置。
            if (remember || !WindowCtl.PlacesWindow)
            {
                Screen.SetResolution(r.width, r.height, FullScreenMode.Windowed);
                for (int i = 0; i < 30 && (Screen.width != r.width || Screen.height != r.height || Screen.fullScreenMode != FullScreenMode.Windowed); i++) yield return null;
                yield return null;
            }
            StripCamera(StripTransparent);
            if (WindowCtl.PlacesWindow) WindowCtl.Apply(true, compactTop, r);   // Windows：无边框、置顶、大小和位置一次摆好
            else
            {   // Mac：先改边框 / 层级，再按内容区重设大小、挪到工作区右下角（隔几帧再挪一次，防止 Unity 改大小时又挪走）
                WindowCtl.Apply(true, compactTop, new RectInt());
                yield return null;
                Screen.SetResolution(r.width, r.height, FullScreenMode.Windowed);
                for (int i = 0; i < 30 && (Screen.width != r.width || Screen.height != r.height); i++) yield return null;
                for (int n = 0; n < 2; n++)
                {
                    try { Screen.MoveMainWindowTo(display, new Vector2Int(r.x, r.y)); } catch (System.Exception e) { WindowCtl.Status = "move: " + e.Message; }
                    for (int i = 0; i < 6; i++) yield return null;
                }
            }
            yield return null;
            WindowCtl.SetTransparent(StripTransparent);
            WindowCtl.SetClickThrough(false);   // 挂机窗里的点击都归游戏（按住任何地方都能拖）
            stripCheckAt = Time.unscaledTime + .5f;
            compactBusy = false;
        }
        private IEnumerator RestoreWindow()
        {
            compactBusy = true;
            WindowCtl.SetClickThrough(false);
            WindowCtl.SetTransparent(false); StripCamera(false);
            WindowCtl.Apply(false, false, new RectInt());
            yield return null;
            yield return StartCoroutine(ApplyNormal(NormalWindow(), 1f));   // 进挂机前的全屏 / 窗口，回到原来那块屏幕（GameAppWindow.cs）
            savedWindow = false; compactBusy = false;
            LeftStrip();
        }
        /// 透明时相机清成 alpha 0（窗口这些地方透出桌面）；出来时还原。
        private void StripCamera(bool on)
        {
            Camera cam = Camera.main; if (cam == null) return;
            if (on) { if (!clearSaved) { savedClear = cam.backgroundColor; clearSaved = true; } cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); }
            else if (clearSaved) { cam.backgroundColor = savedClear; clearSaved = false; }
        }
        private void SetCompactTop(bool on)
        {
            compactTop = on; SaveStripPrefs();
            if (!Application.isEditor && compact != CompactKind.Off) WindowCtl.Apply(true, on, new RectInt());
        }
        private void SetStripSize(int size)
        {
            size = Mathf.Clamp(size, 0, StripShares.Length - 1); if (size == stripSize) return;
            if (!Application.isEditor && compact != CompactKind.Off && stripRect.height > 0)
            {   // 换大小时左下角不动（往上、往右长），再限制在工作区里
                DisplayInfo d; RectInt work = StripWork(out d);
                int oldBottom = stripRect.y + stripRect.height;
                stripSize = size; Vector2Int sz = StripSizePx(work);
                stripHasPos = true; stripPos = new Vector2Int(stripRect.x, oldBottom - sz.y);
            }
            stripSize = size; SaveStripPrefs();
            if (!Application.isEditor && compact != CompactKind.Off && !compactBusy) StartCoroutine(PlaceStrip(false));
        }
        private void SetStripClear(bool on)
        {
            stripClear = on; SaveStripPrefs();
            if (Application.isEditor || compact == CompactKind.Off) return;
            StripCamera(StripTransparent); WindowCtl.SetTransparent(StripTransparent);
        }

        /// 每帧（Update）：拖动中跟着鼠标挪窗口（限制在屏幕可用区域里）；鼠标在窗口上停半秒浮出标签（移开 1 秒收起）；
        /// 有事（掉了军规以上、改造板能点、离线收益、开始回刷）时排队冒气泡；每半秒对一次窗口位置。
        private void StripTick()
        {
            if (compact == CompactKind.Off) { stripAnim = 0; stripShow = false; return; }
            float dt = Time.unscaledDeltaTime;
            bool inside = false; Vector2 p;
            if (Application.isEditor) inside = stripEditorInside;
            else if (!compactBusy) inside = WindowCtl.CursorInWindow(out p);
            if (stripDrag && !Application.isEditor)
            {
                if (WindowCtl.LeftButtonHeld()) WindowCtl.DragTo();
                else
                {
                    stripDrag = false;
                    RectInt cur; if (WindowCtl.WindowRect(out cur)) { stripRect = cur; stripHasPos = true; stripPos = new Vector2Int(cur.x, cur.y); SaveStripPrefs(); }
                }
            }
            if (!Application.isEditor && !compactBusy && !stripDrag && Time.unscaledTime >= stripCheckAt)
            {   // 跑偏了（Unity 自己改大小、换了显示器、任务栏挪了）就摆回来
                stripCheckAt = Time.unscaledTime + .5f;
                RectInt cur;
                if (WindowCtl.PlacesWindow && WindowCtl.WindowRect(out cur) && (Mathf.Abs(cur.x - stripRect.x) > 2 || Mathf.Abs(cur.y - stripRect.y) > 2 || Mathf.Abs(cur.width - stripRect.width) > 2 || Mathf.Abs(cur.height - stripRect.height) > 2))
                    WindowCtl.Apply(true, compactTop, stripRect);
            }
            stripHoverT = inside ? stripHoverT + dt : 0;
            stripAwayT = inside ? 0 : stripAwayT + dt;
            if (stripMenu && stripAwayT > 2f && !stripForce) stripMenu = false;   // 菜单开着鼠标走开 2 秒就收
            stripShow = stripForce || stripMenu || stripHoverT >= .45f || (stripShow && stripAwayT < 1f);
            stripAnim = Mathf.MoveTowards(stripAnim, stripShow ? 1 : 0, dt * (stripShow ? 6f : 4f));
            // 气泡：通知变了就排一条（掉落未看的数字一直在变，不算；掉落另在 OnDrop 里按品质排）
            string notice = StripNotice();
            if (notice != lastNotice) { if (notice != null) StripToast(notice); lastNotice = notice; }
            bool farming = model.Farming;
            if (farming && !lastFarming) StripToast(FarmBubble());   // 21:11 用户问为什么自动重刷：气泡写清倒在哪关、回刷哪关
            lastFarming = farming;
            if (toastText != null && Time.unscaledTime - toastAt > 4.2f)
            {
                if (stripToasts.Count > 0) { toastText = stripToasts.Dequeue(); toastAt = Time.unscaledTime; }
                else toastText = null;
            }
        }
        private string StripNotice()
        {
            if (navigation.Modal == GameModal.Offline) return "离线带回 " + offlineGain.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " 零件 · 展开看详情";
            if (model.AnyPerkBuyable) return "改造板有节点能点";
            if (model.AnyImplantUpgradable) return "义体能升级";
            if (model.AnyRobotAssemblable) return "机库能组装机器人";
            return null;
        }
        /// 挂机窗头顶的小气泡（不接收点击），一条 4 秒，最多排 4 条。
        private void StripToast(string text)
        {
            if (compact == CompactKind.Off || string.IsNullOrEmpty(text) || text == toastText || stripToasts.Contains(text) || stripToasts.Count >= 4) return;
            if (toastText == null) { toastText = text; toastAt = Time.unscaledTime; } else stripToasts.Enqueue(text);
        }

        // ---------- 画 ----------
        private void CompactGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { paused = !paused; e.Use(); }
            Rect area = new Rect(0, 0, Screen.width, Screen.height);
            if (Application.isEditor)
            {   // 编辑器预览：Game 视图当桌面，按真实比例画一块（默认右下角），也能拖
                Ui.FillScreen(Ui.Hex(0x2B3038));
                float gh = Screen.height, sh = Mathf.Round(gh * StripShares[stripSize]), sw = Mathf.Min(Screen.width, Mathf.Round(sh * ViewSpan / ViewH));
                if (editorPos.x < 0) editorPos = new Vector2(Screen.width - sw - 16, gh - sh);
                if (stripDrag)
                {
                    if (e.type == EventType.MouseDrag) { editorPos += e.delta; e.Use(); }
                    else if (e.rawType == EventType.MouseUp) stripDrag = false;
                }
                editorPos = new Vector2(Mathf.Clamp(editorPos.x, 0, Mathf.Max(0, Screen.width - sw)), Mathf.Clamp(editorPos.y, 0, Mathf.Max(0, gh - sh)));
                area = new Rect(Mathf.Round(editorPos.x), Mathf.Round(editorPos.y), sw, sh);
                if (e.type == EventType.Repaint) stripEditorInside = area.Contains(e.mousePosition);
            }
            bool startDrag = e.type == EventType.MouseDown && e.button == 0 && (!Application.isEditor || area.Contains(e.mousePosition));
            GUI.BeginGroup(area);
            float W = area.width, H = area.height, k = H / ViewH;
            float lx = Mathf.Clamp(lastHeroFeetX - ViewLead, 0, 1600 - ViewSpan);   // 跟着主角：左边留 300，右边看得到走过来的怪
            bool clear = StripTransparent;
            int keepBare = bareStage; if (clear && bareStage == 0) bareStage = 3;
            bool keepSnap = Ui.Snap; Ui.Snap = false;   // 缩得很小时按整像素对齐会一跳一跳的
            Ui.PushView(0, 0, lx, ViewTop, k);
            compactView = true;
            try
            {
                if (clear) StripRail();
                ActionPage();
            }
            finally { compactView = false; bareStage = keepBare; Ui.Snap = keepSnap; Ui.PopView(); }
            float s = Mathf.Clamp(H / 160f, .8f, 1.4f);   // 标签字号跟窗口高度走，但封顶
            Ui.PushView(0, 0, 0, 0, s);
            StripHud(W / s, H / s, s, lx);
            Ui.PopView();
            GUI.EndGroup();
            // 按住窗口里任何地方就开始拖（按钮在上面已经把自己的点击用掉了，这里只剩空白处的）：横竖都行，限制在屏幕可用区域里
            if (startDrag && e.type == EventType.MouseDown)
            {
                stripMenu = false;
                stripDrag = Application.isEditor || WindowCtl.BeginDrag();
                e.Use();
            }
        }
        /// 透明时脚下的一条细轨道（不然角色像飘在桌面上）：深色细线 + 跟着走动的橙色刻度。
        private void StripRail()
        {
            Ui.Fill(0, GroundY + 2, 1600, 4, Ui.Alpha(Ui.Ink, .6f));
            for (int i = 0; i < 42; i++) Ui.Fill(i * 40 - Wrap(worldX, 40), GroundY + 6, 14, 3, Ui.Alpha(Ui.Orange, .45f));
        }

        private string CompactRate() { return "+" + Mathf.RoundToInt(model.state.scrapRate * 60).ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
        private string CompactStatus()
        {
            if (combat.recovery > 0) return "维修中 · " + combat.recovery.ToString("F1") + " 秒";
            if (paused) return "已暂停 · 空格继续";
            if (model.Farming) return "回刷 · 再 " + FarmRunsLeft() + " 次挑战 " + Catalog.StageCode(model.state.retryRegion);
            return null;
        }
        /// 墙前回刷（GameModel.CanAdvanceAfterClear）：倒在最前面那一关以后，退到前面最近的普通关刷，战力涨 4% 或刷满 5 次就自动回去再挑战。
        /// 和全屏顶上那行一样按「刷满几次」写（战力涨够会提前回去）。
        private int FarmRunsLeft() { return Mathf.Max(1, Balance.RetryFarms - model.state.retryFarms); }
        private string FarmBubble() { return "倒在 " + Catalog.StageCode(model.state.retryRegion) + " · 先回刷 " + Catalog.StageCode(model.state.region) + " 变强"; }
        private string FarmTip()
        {
            int gain = Mathf.RoundToInt((model.Power() / Mathf.Max(1, model.state.retryPower) - 1) * 100);
            return "回刷补给 · 战力 " + (gain >= 0 ? "+" : "") + gain + "%（+" + Mathf.RoundToInt(Balance.RetryGain * 100) + "% 或再刷 " + FarmRunsLeft() + " 次后再挑战）";
        }
        private static float Ease01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        /// 左下一行小标签（平时收在下面，悬停才滑上来）：关卡号、名字、波次、每分钟零件（或状态）、「展开」「≡」；
        /// 「≡」在标签上面再出一行：大小 S / M / L、置顶、透明、倍速。w、h 是按 s 缩放后的单位，lx 是露出来那段舞台的左边。
        private void StripHud(float w, float h, float s, float lx)
        {
            StripBubble(w, h, s, lx);
            const float th = 28, x0 = 6;
            int code = CurrentCode;
            string codeText = CodeLabel(code), name = NameLabel(code);
            string wave = combat.BossEnemy == null ? combat.wave + "/" + combat.WaveCount : "首领";
            string status = CompactStatus(), rate = CompactRate();
            float cw = Ui.Width(codeText, 17, true), nw = Ui.Width(name, 12, false), ww = Ui.Width(wave, 12, true);
            float uw = Ui.Width("零件/分", 11, false), rw = status != null ? Ui.Width(status, 12, true) : Ui.Width(rate, 14, true) + 4 + uw;
            float room = w - x0 * 2 - 72;   // 窗口窄，放不下名字就不写名字
            bool showName = 9 + cw + 8 + nw + 8 + ww + 12 + rw + 10 <= room;
            float tw = 9 + cw + 8 + (showName ? nw + 8 : 0) + ww + 12 + rw + 10 + 72;
            float ty = h - th - 6 + (1 - Ease01(stripAnim)) * (th + 14);
            if (stripAnim <= .001f) return;
            Ui.Fill(x0, ty, tw, th, Ui.Alpha(Ui.Ink, .9f));
            Ui.Fill(x0, ty, 3, th, Ui.Orange);
            float tx = x0 + 9;
            Ui.ShadowText(tx, ty + 1, cw + 6, th - 2, codeText, 17, Ui.Panel, TextAnchor.MiddleLeft, true, true); tx += cw + 8;
            if (showName) { Ui.Text(tx, ty, nw + 4, th, name, 12, Ui.MuteOnInk, TextAnchor.MiddleLeft); tx += nw + 8; }
            Ui.Text(tx, ty, ww + 4, th, wave, 12, combat.BossEnemy == null ? Ui.MuteOnInk : Ui.Orange, TextAnchor.MiddleLeft, true); tx += ww + 12;
            if (status != null) Ui.Text(tx, ty, rw + 6, th, status, 12, Ui.Orange, TextAnchor.MiddleLeft, true);
            else
            {
                float aw = Ui.Width(rate, 14, true);
                Ui.Text(tx, ty, aw + 4, th, rate, 14, Ui.Orange, TextAnchor.MiddleLeft, true);
                Ui.Text(tx + aw + 4, ty + 1, uw + 4, th, "零件/分", 11, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            }
            float bx = x0 + tw - 70;
            if (Ui.Button(bx, ty + 4, 40, 20, "展开", 3, !compactBusy, 11)) ExitCompact();
            if (Ui.Button(bx + 44, ty + 4, 22, 20, "≡", stripMenu ? 1 : 3, true, 14)) stripMenu = !stripMenu;
            if (unseen.Count > 0)
            {   // 角标：未看的掉落
                string badge = unseen.Count > 99 ? "99+" : unseen.Count.ToString();
                float bw = Mathf.Max(18, Ui.Width(badge, 10, true) + 8);
                Ui.Fill(x0 + tw - bw + 4, ty - 9, bw, 15, Ui.Orange);
                Ui.Text(x0 + tw - bw + 4, ty - 9, bw, 15, badge, 10, Ui.OnAccent, TextAnchor.MiddleCenter, true);
            }
            if (stripMenu) StripMenu(x0, ty, th, w);
            // 悬停在标签上：上面一行写现在要做的事（菜单开着时不写，免得挡住）；回刷时写战力涨了多少、还差几次回去挑战
            if (stripAnim > .9f && !stripMenu && !stripDrag && Ui.Hover(x0, ty, tw - 72, th))
            {
                string tip = StripNotice() ?? (model.Farming ? FarmTip() : unseen.Count > 0 ? "掉落 " + unseen.Count + " 件未看" : "按住能拖到任何地方 · 鼠标停一下出菜单");
                float tipw = Mathf.Min(w - x0 - 4, Ui.Width(tip, 11, false) + 18);
                Ui.Fill(x0, ty - 26, tipw, 20, Ui.Alpha(Ui.Ink, .9f));
                Ui.FitText(x0 + 9, ty - 26, tipw - 12, 20, tip, 11, Ui.Panel, TextAnchor.MiddleLeft);
            }
        }
        // 菜单的几组（从左到右）：大小 S / M / L、置顶、透明、倍速 1× / 2× / 3×、退出。一行放不下就往上再折一行（S 档、小屏幕上窗口很窄）。
        private static readonly float[] MenuGroupW = { 108, 52, 52, 128, 44 };
        private static readonly string[] StripSizeNames = { "S", "M", "L" };   // 单字「小 / 中 / 大」不进翻译表：翻译表兜底会按片段替换，单字会把别的句子换坏
        private readonly float[] menuGX = new float[5], menuRowW = new float[5]; private readonly int[] menuGRow = new int[5];
        private static float MenuY(float ty, float th, int row) { return ty - (th + 6) - row * (th + 4); }
        private void StripMenu(float x0, float ty, float th, float w)
        {
            const float pad = 9, gap = 8, rpad = 6;
            float maxW = Mathf.Max(MenuGroupW[3] + pad + rpad, w - x0 - 4);
            int rows = 0; float cx = pad;
            for (int g = 0; g < MenuGroupW.Length; g++)
            {
                if (cx > pad && cx + MenuGroupW[g] + rpad > maxW) { menuRowW[rows] = cx - gap + rpad; rows++; cx = pad; }
                menuGX[g] = x0 + cx; menuGRow[g] = rows; cx += MenuGroupW[g] + gap;
            }
            menuRowW[rows] = cx - gap + rpad;
            for (int r = 0; r <= rows; r++) Ui.Fill(x0, MenuY(ty, th, r), menuRowW[r], th, Ui.Alpha(Ui.Ink, .9f));
            float gx = menuGX[0], y = MenuY(ty, th, menuGRow[0]);
            Ui.Text(gx, y, 34, th, "大小", 11, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            for (int i = 0; i < 3; i++)
                if (Ui.Button(gx + 31 + i * 27, y + 4, 23, 20, StripSizeNames[i], stripSize == i ? 1 : 3, !compactBusy, 11)) SetStripSize(i);
            if (Ui.Button(menuGX[1], MenuY(ty, th, menuGRow[1]) + 4, 52, 20, "置顶", compactTop ? 1 : 3, true, 11)) SetCompactTop(!compactTop);
            if (Ui.Button(menuGX[2], MenuY(ty, th, menuGRow[2]) + 4, 52, 20, "透明", stripClear ? 1 : 3, true, 11)) SetStripClear(!stripClear);
            // 20:28 用户：没找到调倍速的地方 → 菜单里也放一份（和全屏底栏一样：3× 打过第一章首领才开）
            gx = menuGX[3]; y = MenuY(ty, th, menuGRow[3]);
            Ui.Text(gx, y, 36, th, "倍速", 11, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            bool fastOpen = model.state.bosses > 0;
            if (!fastOpen && speed > 2) speed = 2;
            for (int i = 0; i < 3; i++)
                if (Ui.Button(gx + 36 + i * 32, y + 4, 28, 20, (i + 1) + "×", speed == i + 1 ? 1 : 3, i < 2 || fastOpen, 11, true)) speed = i + 1;
            // 21:12 用户：挂机条状态没法直接退出 → 这里退（进度照常保存）；退之前先摆回进挂机前的全屏 / 窗口（GameAppWindow.cs）
            if (Ui.Button(menuGX[4], MenuY(ty, th, menuGRow[4]) + 4, 44, 20, "退出", 3, !quitting, 11)) { stripMenu = false; Application.Quit(); }
        }
        /// 主角头顶的小气泡：4 秒，开头淡入、结尾淡出；不接收点击。
        private void StripBubble(float w, float h, float s, float lx)
        {
            if (toastText == null) return;
            float age = Time.unscaledTime - toastAt; if (age < 0 || age > 4f) return;
            float a = Mathf.Clamp01(age / .2f) * Mathf.Clamp01((4f - age) / .4f);
            float k = h / ViewH;   // 每个逻辑单位折成多少个 s 单位
            float hx = (lastHeroFeetX - lx) * k, tipY = (GroundY - 236 - ViewTop) * k;
            float bw = Mathf.Min(w - 8, Ui.Width(toastText, 12, true) + 22), bh = 24;
            float bx = Mathf.Clamp(hx - bw / 2, 4, Mathf.Max(4, w - bw - 4)), by = Mathf.Max(2, tipY - bh - 4);
            Ui.Fill(bx, by, bw, bh, Ui.Alpha(Ui.Ink, .9f * a));
            Ui.Fill(bx, by, 3, bh, Ui.Alpha(Ui.Orange, a));
            Ui.Fill(Mathf.Clamp(hx, bx + 8, bx + bw - 8) - 3, by + bh, 6, 4, Ui.Alpha(Ui.Ink, .9f * a));   // 小尖角（指向主角）
            Ui.FitText(bx + 11, by, bw - 14, bh, toastText, 12, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft, true);
        }

        // ---------- 测试（桥接 call TestCompact 1 / 0、TestStrip 大小 透明 菜单、TestStripShow、TestStripToast、TestStripMove）----------
        public void TestCompact(int kind) { if (kind <= 0) ExitCompact(); else EnterCompact(CompactKind.Strip); }
        public void TestStrip(int size, int clear, int menu) { stripSize = Mathf.Clamp(size, 0, 2); stripClear = clear != 0; stripMenu = menu != 0; }
        public void TestStripShow(int on) { stripForce = on != 0; }
        public void TestStripToast(int kind) { StripToast(kind == 0 ? "掉落 · " + Catalog.QualityNames[3] : kind == 1 ? "改造板有节点能点" : FarmBubble()); }
        /// 测试：假装倒在最前面那一关、退到前面回刷（桥接 call TestStripFarm 刷了几次）
        public void TestStripFarm(int farms)
        {
            int fell = Mathf.Max(1, model.state.unlockedRegion);
            model.state.retryRegion = fell; model.state.retryPower = model.Power() * .98f; model.state.retryFarms = Mathf.Clamp(farms, 0, Balance.RetryFarms - 1);
            if (model.ChangeRegion(GameModel.FarmStage(fell))) combat.ResetRegion();
        }
        public void TestStripMove(int x, int y) { editorPos = new Vector2(x, y); }
    }
}
