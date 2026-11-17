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
        private static Vector2 origin;
        private static readonly Stack<Vector2> origins = new Stack<Vector2>();
        private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        private static float styleScale;
        private static Font textFont, numberFont;
        private static Texture2D hazard;

        public static Color Hex(int rgb)
        { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1); }
        public static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }

        public static void Begin()
        {
            K = Mathf.Max(.25f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            origin = new Vector2(Mathf.Round((Screen.width - 1600 * K) / 2), Mathf.Round((Screen.height - 900 * K) / 2));
            origins.Clear(); clipOffsets.Clear(); clipOffset = Vector2.zero;
            Blocked = false;
            if (Mathf.Abs(styleScale - K) > .0001f) { styles.Clear(); styleScale = K; }
            if (textFont == null || numberFont == null)
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                textFont = Font.CreateDynamicFontFromOSFont(Pick(installed, "Microsoft YaHei", "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "SimHei", "Arial"), 18);
                numberFont = Font.CreateDynamicFontFromOSFont(Pick(installed, "Bahnschrift", "DIN Alternate", "Arial Narrow", "Arial", "Microsoft YaHei"), 18);
                styles.Clear();
            }
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
            float x0 = Mathf.Round(origin.x + x * K), y0 = Mathf.Round(origin.y + y * K);
            float x1 = Mathf.Round(origin.x + (x + w) * K), y1 = Mathf.Round(origin.y + (y + h) * K);
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
                style.font = numeric ? numberFont : textFont; style.fontSize = pixels;
                style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
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
            GUIStyle style = Style(numeric, Pixels(size), bold, anchor, wrap);
            style.normal.textColor = UiTheme.Map(color, UiTheme.Role.Text);
            GUI.Label(R(x, y, w, h), value, style);
        }
        public static float Width(string value, float size, bool bold = false, bool numeric = false)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            GUIStyle style = Style(numeric, Pixels(size), bold, TextAnchor.UpperLeft, false);
            return style.CalcSize(new GUIContent(value)).x / K;
        }

        public static bool Hover(float x, float y, float w, float h)
        { return !Blocked && Event.current != null && R(x, y, w, h).Contains(Event.current.mousePosition); }
        public static bool Click(float x, float y, float w, float h)
        { return !Blocked && GUI.Button(R(x, y, w, h), GUIContent.none, GUIStyle.none); }

        // kind: 0 描边按钮，1 主要按钮（橙色），2 深色按钮（选中的标签），3 深色底上的描边按钮。
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
            Text(x, y, w, h, label, size, text, TextAnchor.MiddleCenter, true, numeric);
            return enabled && Click(x, y, w, h);
        }

        // 装备格：近黑底 + 图标 + 底部品质色条。返回是否悬停，点击由调用方处理。
        public static void ItemTile(float x, float y, float w, float h, EquipmentBase item, Quality quality, bool showQuality, bool dim, float pad, bool hover)
        {
            Fill(x, y, w, h, hover ? Tile2 : Tile);
            float side = Mathf.Min(w, h - (showQuality ? 4 : 0)) - pad * 2;
            if (item != null) PaperDollView.DrawEquipmentIcon(item, R(x + (w - side) / 2, y + (h - (showQuality ? 4 : 0) - side) / 2, side, side), dim);
            if (showQuality) Fill(x, y + h - 4, w, 4, QualityVisual.Colors[(int)quality]);
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
