using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // v0.7 界面绘制工具：所有坐标按 1600x900 逻辑画布书写，绘制时换算为屏幕像素。
    // 文字按实际像素字号生成（不靠 GUI.matrix 放大），任何分辨率下都保持清晰。
    public static class Ui
    {
        public static readonly Color Ground = Hex(0xE8E9E4), Panel = Hex(0xF6F6F2), Stage = Hex(0xF3F3EF), Floor = Hex(0xDCDED8);
        public static readonly Color Ink = Hex(0x121416), Tile = Hex(0x1C1F22), Tile2 = Hex(0x24282B), Steel = Hex(0x3A3F43);
        public static readonly Color Rule = Hex(0xC4C7C1), Hair = Hex(0xD5D7D1), Ghost = Hex(0xE3E5DF), Grey = Hex(0x8B9093);
        public static readonly Color Mute = Hex(0x565C60), MuteOnInk = Hex(0xB9BEC0), Body = Hex(0x3A3F43);
        public static readonly Color Orange = Hex(0xFF4A12), OrangeInk = Hex(0xB5300A), Good = Hex(0x1E7A46);
        // 橙色按钮上的字、橙色按钮悬停色：单独定义，换配色时不和 Ink 混在一起
        public static readonly Color OnAccent = Hex(0x121417), OrangeHover = Hex(0xFF6A3A);

        public static float K = 1;
        public static bool Blocked;
        /// 11-24 画的位置要不要对齐整像素：平时对齐（细线不发虚）；挂机条缩得很小时关掉，不然走动一跳一跳的。
        public static bool Snap = true;
        /// v0.33 舞台是亮色背景（晴天涂鸦）时由行动页打开：ShadowText 的投影换成一圈描边（浅色字配深蓝描边，深色字配白描边），
        /// 关卡名、波次、伤害数字、血量数字在浅色墙上也看得清。每帧 Begin 时关掉，只在画舞台那一段打开。
        public static bool OnBright;
        private static readonly Color BrightOutline = Hex(0x1B2140);
        private static Vector2 origin;
        private static readonly Stack<Vector2> origins = new Stack<Vector2>();
        private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        private static float styleScale;
        private static Font textFont, numberFont, textBold, numberBold;
        private static Texture2D hazard;

        public static Color Hex(int rgb)
        { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1); }
        public static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }

        public static void Begin()
        {
            K = Mathf.Max(.25f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            origin = new Vector2(Mathf.Round((Screen.width - 1600 * K) / 2), Mathf.Round((Screen.height - 900 * K) / 2));
            origins.Clear(); clipOffsets.Clear(); clipOffset = Vector2.zero; views.Clear();
            Blocked = false; OnBright = false;
            if (Mathf.Abs(styleScale - K) > .0001f) { styles.Clear(); styleScale = K; }
            if (textFont == null || numberFont == null) { LoadFonts(); styles.Clear(); }
        }
        /// v0.21 字体（11-22 用户在样本里选了 B）：正文思源黑体（Noto Sans CJK，按语言用 SC / JP / KR 各自的字形，粗体是单独的字体文件，
        /// 不靠 Unity 假加粗），数字和英文编号用 Oxanium。都打包在 Resources/Fonts（OFL 授权），Windows 和 Mac 上一个样。
        /// 字体由 Tools/fonts/build_fonts.py 生成（裁成用得到的字）；万一没读到，退回系统字体。
        private static void LoadFonts()
        {
            Loc.Lang lang = Loc.Get();
            string region = lang == Loc.Lang.Ja ? "JP" : lang == Loc.Lang.Ko ? "KR" : "SC";
            textFont = Resources.Load<Font>("Fonts/NotoSans" + region + "-Regular");
            textBold = Resources.Load<Font>("Fonts/NotoSans" + region + "-Bold");
            numberFont = Resources.Load<Font>("Fonts/Oxanium-SemiBold");
            numberBold = Resources.Load<Font>("Fonts/Oxanium-Bold");
            if (textFont != null && numberFont != null) return;
            var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
            if (textFont == null) { textFont = Font.CreateDynamicFontFromOSFont(Pick(installed, "Microsoft YaHei", "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "SimHei", "Arial"), 18); textBold = null; }
            if (numberFont == null) { numberFont = Font.CreateDynamicFontFromOSFont(Pick(installed, "Bahnschrift", "DIN Alternate", "Arial Narrow", "Arial"), 18); numberBold = null; }
        }
        /// 换语言以后重新建字体和字号缓存。
        public static void ResetFonts() { textFont = null; numberFont = null; textBold = null; numberBold = null; styles.Clear(); }
        /// 数字字体（Oxanium）只有拉丁字母、数字和几个符号；带别的字（中日韩、箭头、◆）的「数字」文字改用正文字体。
        private static bool NumberSafe(string s)
        {
            foreach (char c in s) if ((c < ' ' || c > '~') && "−×·…–—".IndexOf(c) < 0) return false;
            return true;
        }
        private static string[] Pick(HashSet<string> installed, params string[] names)
        {
            var list = new List<string>();
            foreach (string name in names) if (installed.Contains(name)) list.Add(name);
            if (list.Count == 0) list.Add("Arial");
            return list.ToArray();
        }

        public static Rect R(float x, float y, float w, float h)
        {
            float x0 = origin.x + x * K, y0 = origin.y + y * K, x1 = origin.x + (x + w) * K, y1 = origin.y + (y + h) * K;
            if (Snap) { x0 = Mathf.Round(x0); y0 = Mathf.Round(y0); x1 = Mathf.Round(x1); y1 = Mathf.Round(y1); }
            if (w > 0 && x1 - x0 < 1) x1 = x0 + 1;
            if (h > 0 && y1 - y0 < 1) y1 = y0 + 1;
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }
        /// 绕本地坐标点旋转，用法：GUI.matrix = old * Ui.RotateLocal(angle, pivot)。
        /// GUIUtility.RotateAroundPivot 把支点当屏幕坐标：GUI.matrix 带平移（界面滑入）时图会飞走。
        /// GUI.matrix 作用在“去掉裁剪组偏移之后”的坐标上，所以支点要加上当前裁剪组的累计偏移（滚动列表里尤其明显）。
        public static Matrix4x4 RotateLocal(float angle, Vector2 pivot)
        {
            Vector2 u = pivot + clipOffset;
            return Matrix4x4.Translate(u) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, angle)) * Matrix4x4.Translate(-u);
        }
        private static Vector2 clipOffset;
        private static readonly Stack<Vector2> clipOffsets = new Stack<Vector2>();
        public static Vector2 P(float x, float y) { return new Vector2(origin.x + x * K, origin.y + y * K); }
        /// v0.20 技能镜头推近：以 (px, py) 为中心把之后画的东西放大 z 倍，ZoomEnd 复原。只在舞台裁剪区里用。
        private static float zoomSavedK = -1; private static Vector2 zoomSavedOrigin;
        public static void ZoomBegin(float px, float py, float z)
        {
            zoomSavedK = K; zoomSavedOrigin = origin;
            origin = new Vector2(origin.x + px * K * (1 - z), origin.y + py * K * (1 - z)); K *= z;
        }
        public static void ZoomEnd() { if (zoomSavedK > 0) { K = zoomSavedK; origin = zoomSavedOrigin; zoomSavedK = -1; } }
        /// 11-24 挂机小窗：之后画的东西里，逻辑坐标 (lx, ly) 落在屏幕上的 (left, top)，缩放 k（文字也按 k 出字号）。PopView 复原。
        private static readonly Stack<Vector3> views = new Stack<Vector3>();
        public static void PushView(float left, float top, float lx, float ly, float k)
        {
            views.Push(new Vector3(origin.x, origin.y, K));
            K = k; origin = new Vector2(Mathf.Round(left - lx * k), Mathf.Round(top - ly * k));
        }
        public static void PopView() { if (views.Count == 0) return; Vector3 v = views.Pop(); origin = new Vector2(v.x, v.y); K = v.z; }
        /// 在当前裁剪区域里把整块内容挪一下（震屏用；EndClip 时自动复原）。
        public static void Nudge(float dx, float dy) { origin += new Vector2(Mathf.Round(dx * K), Mathf.Round(dy * K)); }

        // 裁剪区域：内部仍然使用页面逻辑坐标，scrollY 为向上滚动的逻辑距离。
        public static void BeginClip(float x, float y, float w, float h, float scrollY)
        {
            Rect area = R(x, y, w, h);
            GUI.BeginGroup(area);
            clipOffsets.Push(clipOffset); clipOffset += area.position;
            origins.Push(origin);
            origin = new Vector2(origin.x - area.x, origin.y - area.y - Mathf.Round(scrollY * K));
        }
        public static void EndClip()
        {
            if (origins.Count > 0) origin = origins.Pop();
            if (clipOffsets.Count > 0) clipOffset = clipOffsets.Pop();
            GUI.EndGroup();
        }

        public static void Fill(float x, float y, float w, float h, Color color)
        {   // 细于 3 像素的填充当作分隔线
            color = UiTheme.Map(color, w <= 3 || h <= 3 ? UiTheme.Role.Line : UiTheme.Role.Fill);
            FillRaw(x, y, w, h, color);
        }
        private static void FillRaw(float x, float y, float w, float h, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(R(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = old;
        }
        public static void FillScreen(Color color)
        {
            color = UiTheme.Map(color, UiTheme.Role.Fill);
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
        public static void Frame(float x, float y, float w, float h, float thickness, Color color)
        {
            color = UiTheme.Map(color, UiTheme.Role.Line);
            FillRaw(x, y, w, thickness, color); FillRaw(x, y + h - thickness, w, thickness, color);
            FillRaw(x, y + thickness, thickness, h - thickness * 2, color); FillRaw(x + w - thickness, y + thickness, thickness, h - thickness * 2, color);
        }
        public static void Stroke(float x1, float y1, float x2, float y2, float width, Color color)
        { CharacterView.Line(P(x1, y1), P(x2, y2), Mathf.Max(1, width * K), UiTheme.Map(color, UiTheme.Role.Text)); }
        public static void Disk(float x, float y, float radius, Color color)
        { Vector2 p = P(x, y); CharacterView.Circle(p.x, p.y, radius * K, UiTheme.Map(SameRgb(color, Ink) ? new Color(Tile.r, Tile.g, Tile.b, color.a) : color, UiTheme.Role.Fill)); }
        private static bool SameRgb(Color a, Color b) { return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < .004f; }
        // shift：逻辑像素的水平偏移，用于让条纹随地面滚动。
        public static void Hazard(float x, float y, float w, float h, float shift = 0)
        {
            if (hazard == null)
            {
                hazard = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                hazard.wrapMode = TextureWrapMode.Repeat; hazard.filterMode = FilterMode.Bilinear;
                var pixels = new Color[1024];
                for (int j = 0; j < 32; j++) for (int i = 0; i < 32; i++) pixels[j * 32 + i] = ((i + j) % 32) < 16 ? Ink : Orange;
                hazard.SetPixels(pixels); hazard.Apply();
            }
            Rect area = R(x, y, w, h); float period = Mathf.Max(8, 34 * K);
            GUI.DrawTextureWithTexCoords(area, hazard, new Rect(shift / 34f, 0, area.width / period, area.height / period));
        }

        private static GUIStyle Style(bool numeric, int pixels, bool bold, TextAnchor anchor, bool wrap)
        {
            int key = (numeric ? 1 : 0) | (bold ? 2 : 0) | (wrap ? 4 : 0) | ((int)anchor << 3) | (pixels << 8);
            GUIStyle style;
            if (!styles.TryGetValue(key, out style))
            {
                style = new GUIStyle();
                Font real = numeric ? (bold && numberBold != null ? numberBold : numberFont) : (bold && textBold != null ? textBold : textFont);
                style.font = real; style.fontSize = pixels;
                style.fontStyle = bold && real == (numeric ? numberFont : textFont) ? FontStyle.Bold : FontStyle.Normal;   // 有单独的粗体文件就不再假加粗
                style.alignment = anchor; style.wordWrap = wrap; style.richText = false; style.clipping = TextClipping.Overflow;
                styles[key] = style;
            }
            return style;
        }
        private static int Pixels(float size) { return Mathf.Clamp(Mathf.RoundToInt(size * K), 8, 480); }
        public static void Text(float x, float y, float w, float h, string value, float size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool numeric = false, bool wrap = false)
        {
            if (string.IsNullOrEmpty(value)) return;
            value = Loc.Tr(value);   // v0.21 四语：画字前换成当前语言
            if (numeric && !NumberSafe(value)) numeric = false;
            GUIStyle style = Style(numeric, Pixels(size), bold, anchor, wrap);
            style.normal.textColor = UiTheme.Map(color, UiTheme.Role.Text);
            GUI.Label(R(x, y, w, h), value, style);
        }
        /// v0.20 直接压在战斗背景上的字（关卡号、波数、伤害数字、首领名）：先画一层深色投影再画字。
        /// 第四章「镜面上城」背景很亮（象牙白、天空），浅色字原来几乎看不见。投影色不在配色映射表里，任何配色方案下都是深色。
        /// v0.21 四语：单行文字，译文比 w 宽时把字号缩到放得下（最小 9）。
        public static void FitText(float x, float y, float w, float h, string value, float size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool numeric = false)
        {
            if (string.IsNullOrEmpty(value)) return;
            float tw = Width(value, size, bold, numeric);
            Text(x, y, w, h, value, tw > w ? Mathf.Max(9, size * w / tw) : size, color, anchor, bold, numeric);
        }
        /// 11-23 多行文字的高度（逻辑像素）：按宽度 w 自动换行后占多高。
        public static float Height(string value, float size, float w, bool bold = false)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            value = Loc.Tr(value);
            return Style(false, Pixels(size), bold, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(value), w * K) / K;
        }
        /// 11-23 多行文字：换行后比 h 高就把字号一点点缩到放得下（最小 9）。译文比中文长很多的说明文字用这个。
        public static void WrapText(float x, float y, float w, float h, string value, float size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            if (string.IsNullOrEmpty(value)) return;
            float s = size; while (s > 9 && Height(value, s, w, bold) > h + .5f) s -= .5f;
            Text(x, y, w, h, value, s, color, anchor, bold, false, true);
        }
        public static void ShadowText(float x, float y, float w, float h, string value, float size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool numeric = false, float strength = .6f)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (OnBright) { OutlineText(x, y, w, h, value, size, color, anchor, bold, numeric, strength); return; }
            float d = Mathf.Max(1.1f, size * .045f);
            Color shade = new Color(.02f, .02f, .03f, color.a * strength);
            Text(x + d, y + d, w, h, value, size, shade, anchor, bold, numeric);
            if (size >= 24) Text(x - d * .5f, y - d * .5f, w, h, value, size, Alpha(shade, shade.a * .45f), anchor, bold, numeric);   // 大字左上再补一圈淡的，读起来像描边
            Text(x, y, w, h, value, size, color, anchor, bold, numeric);
        }
        /// 描边字：在字的四周（小字 4 个方向、大字 8 个方向）各画一遍描边色，再画字。描边色按字的实际颜色（换配色以后的）挑：
        /// 浅色字用深蓝，深色字用白色。描边稍微往下偏一点，像贴纸的厚度。
        public static void OutlineText(float x, float y, float w, float h, string value, float size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool numeric = false, float strength = .6f)
        {
            if (string.IsNullOrEmpty(value)) return;
            Color shown = UiTheme.Map(color, UiTheme.Role.Text);
            bool lightText = shown.r * .3f + shown.g * .59f + shown.b * .11f > .5f;
            Color line = lightText ? BrightOutline : new Color(1, 1, 1);
            line.a = color.a * Mathf.Clamp01(strength + .35f);
            float o = Mathf.Max(1.3f, size * .06f), drop = o * .35f;
            int n = size >= 24 ? 8 : 4;
            for (int i = 0; i < n; i++)
            {
                float an = (n == 4 ? Mathf.PI / 4 : 0) + i * Mathf.PI * 2 / n;
                Text(x + Mathf.Cos(an) * o, y + Mathf.Sin(an) * o + drop, w, h, value, size, line, anchor, bold, numeric);
            }
            Color fill = color; fill.a = Mathf.Min(1, color.a * 1.18f);   // 字本身稍微实一点，不让描边透出来发灰
            Text(x, y, w, h, value, size, fill, anchor, bold, numeric);
        }
        public static float Width(string value, float size, bool bold = false, bool numeric = false)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            value = Loc.Tr(value);
            if (numeric && !NumberSafe(value)) numeric = false;
            GUIStyle style = Style(numeric, Pixels(size), bold, TextAnchor.UpperLeft, false);
            return style.CalcSize(new GUIContent(value)).x / K;
        }

        public static bool Hover(float x, float y, float w, float h)
        { return !Blocked && Event.current != null && R(x, y, w, h).Contains(Event.current.mousePosition); }
        public static bool Click(float x, float y, float w, float h)
        { return !Blocked && GUI.Button(R(x, y, w, h), GUIContent.none, GUIStyle.none); }

        // kind: 0 描边按钮，1 主要按钮（橙色），2 深色按钮（选中的标签），3 深色底上的描边按钮。
        /// v0.21 按钮按下时（放点击音）。
        public static System.Action Clicked;
        public static bool Button(float x, float y, float w, float h, string label, int kind = 0, bool enabled = true, float size = 16, bool numeric = false)
        {
            bool hover = enabled && Hover(x, y, w, h);
            Color fill, text, edge;
            if (!enabled) { fill = kind == 3 ? Tile2 : Rule; text = kind == 3 ? Grey : Mute; edge = kind == 3 ? Steel : Grey; }
            else if (kind == 1) { fill = hover ? OrangeHover : Orange; text = OnAccent; edge = OnAccent; }
            else if (kind == 2) { fill = hover ? Tile2 : Ink; text = Panel; edge = Ink; }
            else if (kind == 3) { fill = hover ? Panel : Ink; text = hover ? Ink : Panel; edge = Panel; }
            else { fill = hover ? Ink : Panel; text = hover ? Panel : Ink; edge = Ink; }
            Fill(x, y, w, h, fill); Frame(x, y, w, h, 2, edge);
            // v0.21 四语：译文比中文长时把字缩小到放得下（最小 9）
            float fit = size, tw = Width(label, size, true, numeric);
            if (tw > w - 12) fit = Mathf.Max(9, size * (w - 12) / tw);
            Text(x, y, w, h, label, fit, text, TextAnchor.MiddleCenter, true, numeric);
            bool hit = enabled && Click(x, y, w, h);
            if (hit) Clicked?.Invoke();
            return hit;
        }

        // 装备格：近黑底 + 图标 + 底部品质色条。返回是否悬停，点击由调用方处理。
        /// 11-23 装备卡片按品质分档（画在图标上面、文字下面）：
        ///   特装：底部一层很淡的品质色；军规：再加一圈细边框；机密：边框加粗 + 四角标 + 每隔几秒一道斜光扫过；原型：边框呼吸 + 扫光更勤。
        public static void QualityCard(float x, float y, float w, float h, Quality quality)
        {
            int q = (int)quality; if (q < 2) return;
            Color c = QualityVisual.Colors[q]; float now = Time.unscaledTime;
            float gh = h * .34f;
            for (int i = 0; i < 4; i++) FillRaw(x, y + h - gh * (i + 1) / 4f, w, gh / 4f, Alpha(c, (q >= 4 ? .13f : .08f) * (1 - i / 4f)));
            if (q < 3) return;
            float pulse = q >= 5 ? .7f + .3f * Mathf.Sin(now * 3.2f) : 1, t = q >= 4 ? 2 : 1;
            Color line = Alpha(c, (q >= 4 ? .9f : .55f) * pulse);
            FillRaw(x, y, w, t, line); FillRaw(x, y + t, t, h - t, line); FillRaw(x + w - t, y + t, t, h - t, line);
            if (q < 4) return;
            float n = Mathf.Min(w, h) * .16f;   // 四角标
            FillRaw(x, y, n, 4, c); FillRaw(x, y, 4, n, c); FillRaw(x + w - n, y, n, 4, c); FillRaw(x + w - 4, y, 4, n, c);
            if (q >= 6)
            {   // v0.22 零号：里面再套一圈细框，四角标换成白色，边框上有一个光点绕着走
                FillRaw(x + 5, y + 5, w - 10, 1, Alpha(c, .5f)); FillRaw(x + 5, y + 6, 1, h - 12, Alpha(c, .5f)); FillRaw(x + w - 6, y + 6, 1, h - 12, Alpha(c, .5f));
                FillRaw(x, y, n * .5f, 4, Color.white); FillRaw(x, y, 4, n * .5f, Color.white); FillRaw(x + w - n * .5f, y, n * .5f, 4, Color.white); FillRaw(x + w - 4, y, 4, n * .5f, Color.white);
                float per = 2 * (w + h), d = Mathf.Repeat(now * 90 + x + y, per), px = d < w ? x + d : d < w + h ? x + w - 3 : d < 2 * w + h ? x + w - (d - w - h) : x, py = d < w ? y : d < w + h ? y + (d - w) : d < 2 * w + h ? y + h - 8 : y + h - (d - 2 * w - h);
                FillRaw(Mathf.Clamp(px - 5, x, x + w - 10), Mathf.Clamp(py - 1, y, y + h - 8), 10, 3, Color.white);
            }
            float period = q >= 6 ? 1.6f : q >= 5 ? 2.2f : 3.6f, ph = Mathf.Repeat(now + (x * .013f + y * .007f), period) / .55f;   // 斜光：各张卡错开一点
            if (ph < 1)
            {
                float sw = w * .22f, sx = x - sw + (w + sw) * ph, x0 = Mathf.Max(x, sx), x1 = Mathf.Min(x + w, sx + sw);
                if (x1 > x0) { FillRaw(x0, y, x1 - x0, h, new Color(1, 1, 1, .1f)); float m0 = Mathf.Max(x, sx + sw * .35f), m1 = Mathf.Min(x + w, sx + sw * .65f); if (m1 > m0) FillRaw(m0, y, m1 - m0, h, new Color(1, 1, 1, .12f)); }
            }
        }
        public static void ItemTile(float x, float y, float w, float h, EquipmentBase item, Quality quality, bool showQuality, bool dim, float pad, bool hover)
        {
            Fill(x, y, w, h, hover ? Tile2 : Tile);
            float side = Mathf.Min(w, h - (showQuality ? 4 : 0)) - pad * 2;
            if (item != null) PaperDollView.DrawEquipmentIcon(item, R(x + (w - side) / 2, y + (h - (showQuality ? 4 : 0) - side) / 2, side, side), dim);
            if (showQuality) { if (!dim) QualityCard(x, y, w, h, quality); Fill(x, y + h - 4, w, 4, QualityVisual.Colors[(int)quality]); }
        }

        public static void IconAction(float cx, float cy, Color color, Color background)
        {
            Disk(cx, cy, 9, color); Disk(cx, cy, 7, background);
            Fill(cx - 1, cy - 13, 2, 7, color); Fill(cx - 1, cy + 6, 2, 7, color); Fill(cx - 13, cy - 1, 7, 2, color); Fill(cx + 6, cy - 1, 7, 2, color);
        }
        public static void IconGrid(float cx, float cy, Color color)
        {
            for (int i = 0; i < 4; i++) Frame(cx - 11 + (i % 2) * 13, cy - 11 + (i / 2) * 13, 9, 9, 2, color);
        }
        public static void IconHanger(float cx, float cy, Color color, Color background)
        {
            Disk(cx, cy - 8, 4.5f, color); Disk(cx, cy - 8, 2.5f, background);
            Fill(cx - 1, cy - 4, 2, 4, color);
            Stroke(cx, cy, cx - 12, cy + 8, 2, color); Stroke(cx, cy, cx + 12, cy + 8, 2, color);
            Fill(cx - 12, cy + 8, 24, 2, color);
        }
        public static void IconWrench(float cx, float cy, Color color, Color background)
        {
            Stroke(cx - 10, cy + 10, cx + 4, cy - 4, 4, color);
            Disk(cx + 6, cy - 6, 7, color); Disk(cx + 9, cy - 9, 3.5f, background);
        }
        public static void IconGear(float cx, float cy, Color color, Color background)
        {
            Fill(cx - 1.5f, cy - 11, 3, 22, color); Fill(cx - 11, cy - 1.5f, 22, 3, color);
            Stroke(cx - 8, cy - 8, cx + 8, cy + 8, 3, color); Stroke(cx - 8, cy + 8, cx + 8, cy - 8, 3, color);
            Disk(cx, cy, 7.5f, color); Disk(cx, cy, 3.5f, background);
        }
        public static void IconLock(float x, float y, Color color)
        { Frame(x + 2, y, 8, 8, 2, color); Fill(x, y + 6, 12, 8, color); }
        public static void IconCheck(float cx, float cy, Color color)
        { Stroke(cx - 8, cy, cx - 2, cy + 6, 3, color); Stroke(cx - 3, cy + 6, cx + 9, cy - 6, 3, color); }
        public static void IconChevron(float cx, float cy, Color color)
        { Stroke(cx - 7, cy - 3, cx, cy + 4, 3, color); Stroke(cx, cy + 4, cx + 7, cy - 3, 3, color); }
        public static void IconPause(float cx, float cy, Color color)
        { Fill(cx - 7, cy - 8, 5, 16, color); Fill(cx + 2, cy - 8, 5, 16, color); }
    }
}
