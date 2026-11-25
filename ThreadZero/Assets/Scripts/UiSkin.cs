using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 v0.29 界面皮肤：白色家电塑料面板、带厚度的按键、液晶屏、深色收藏卡、挂杆夹子这些零件。
    /// 贴图在 Resources/UI/Skin，由 Tools/ui_skin/make_skin.py 生成（每逻辑单位 2 像素，画法和例图同一套）；
    /// 这里按 9 宫格拉伸画，边宽用逻辑单位给（跟着 Ui.K 缩放），所以任何分辨率下圆角和描边粗细都和例图一致。
    /// 颜色是固定的（不跟 F3 配色走）：卡片永远是深色，柜子永远是白色塑料。
    public static class Skin
    {
        public const float TexScale = 2f;
        public static readonly Color TextDark = Ui.Hex(0x24252A), TextMid = Ui.Hex(0x6E6C67), TextLight = Ui.Hex(0x9A978F);
        public static readonly Color OnCard = Ui.Hex(0xF4F4F1), OnCardMute = Ui.Hex(0xA6AAB0), Ink2 = Ui.Hex(0x17181A);
        public static readonly Color PageBack = Ui.Hex(0x26272B), Rubber = Ui.Hex(0x2E3034);
        public static readonly Color[] SlotColors = { Ui.Hex(0xFF7A3D), Ui.Hex(0x4FC3F7), Ui.Hex(0x9CCC65), Ui.Hex(0xBA8CFF), Ui.Hex(0xFFD54F) };   // 武 头 衣 背 腿
        public static readonly string[] QualityEnglish = { "STANDARD", "IMPROVED", "SPECIAL", "MILSPEC", "CLASSIFIED", "PROTOTYPE", "ZERO" };

        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static Texture2D Tex(string name)
        {
            Texture2D t;
            if (cache.TryGetValue(name, out t) && t != null) return t;
            t = Resources.Load<Texture2D>("UI/Skin/" + name);
            cache[name] = t;
            return t;
        }
        private static bool Painting { get { return Event.current != null && Event.current.type == EventType.Repaint; } }

        // ---------- 画图 ----------
        /// 9 宫格：贴图四边各 border 逻辑单位不拉伸（矩形太小时按比例收）。
        public static void Nine(string name, float x, float y, float w, float h, float border, Color tint) { Nine(name, x, y, w, h, border, border, border, border, tint); }
        public static void Nine(string name, float x, float y, float w, float h, float left, float top, float right, float bottom, Color tint)
        {
            if (!Painting || w <= 0 || h <= 0) return;
            Texture2D t = Tex(name); if (t == null) return;
            float sx = Mathf.Min(1, w / Mathf.Max(.001f, left + right)), sy = Mathf.Min(1, h / Mathf.Max(.001f, top + bottom));
            float l = left * sx, r = right * sx, tp = top * sy, b = bottom * sy;
            Slices(t, x, y, w, h, l, tp, r, b, l * TexScale / t.width, r * TexScale / t.width, tp * TexScale / t.height, b * TexScale / t.height, tint);
        }
        /// 胶囊：半径 = 短边的一半。贴图是横放的胶囊，四角各取「高度一半」那么大的圆角，所以竖着画、画得很细都是正圆头。
        private static void Capsule(string name, float x, float y, float w, float h, Color tint)
        {
            if (!Painting || w <= 0 || h <= 0) return;
            Texture2D t = Tex(name); if (t == null) return;
            float r = Mathf.Min(w, h) / 2, su = t.height / 2f / t.width, sv = .5f;
            Slices(t, x, y, w, h, r, r, r, r, su, su, sv, sv, tint);
        }
        /// 9 宫格的实际绘制：l / tp / r / b 是画出来的边宽（逻辑单位），ul / ur / vt / vb 是贴图里对应的边宽（0–1）。
        private static void Slices(Texture2D t, float x, float y, float w, float h, float l, float tp, float r, float b, float ul, float ur, float vt, float vb, Color tint)
        {
            float[] xs = { x, x + l, x + w - r, x + w }, ys = { y, y + tp, y + h - b, y + h };
            float[] us = { 0, ul, 1 - ur, 1 }, vs = { 1, 1 - vt, vb, 0 };
            Color old = GUI.color; GUI.color = tint;
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    if (xs[i + 1] - xs[i] <= 0 || ys[j + 1] - ys[j] <= 0) continue;
                    GUI.DrawTextureWithTexCoords(Ui.R(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]), t, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]), true);
                }
            GUI.color = old;
        }
        public static void Sprite(string name, float x, float y, float w, float h, Color tint)
        {
            if (!Painting || w <= 0 || h <= 0) return;
            Texture2D t = Tex(name); if (t == null) return;
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTexture(Ui.R(x, y, w, h), t, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }
        /// 按贴图本身的大小画（逻辑单位 = 像素 / 2）。
        public static void Sprite(string name, float x, float y, Color tint)
        {
            Texture2D t = Tex(name); if (t == null) return;
            Sprite(name, x, y, t.width / TexScale, t.height / TexScale, tint);
        }
        /// 平铺底纹（tile_ 开头的贴图），tileW / tileH 是一格的逻辑大小。
        public static void Tile(string name, float x, float y, float w, float h, float tileW, float tileH, Color tint, float offsetX = 0, float offsetY = 0)
        {
            if (!Painting || w <= 0 || h <= 0) return;
            Texture2D t = Tex(name); if (t == null) return;
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTextureWithTexCoords(Ui.R(x, y, w, h), t, new Rect(offsetX / tileW, offsetY / tileH, w / tileW, h / tileH), true);
            GUI.color = old;
        }
        /// 绕 (cx, cy) 旋转 angle 度（顺时针为正）画后面的东西，用完 End(old)。
        public static Matrix4x4 Rotate(float angle, float cx, float cy)
        {
            Matrix4x4 old = GUI.matrix;
            if (Mathf.Abs(angle) > .01f) GUI.matrix = old * Ui.RotateLocal(angle, Ui.P(cx, cy));
            return old;
        }
        public static void End(Matrix4x4 old) { GUI.matrix = old; }

        /// 描边字（四个斜角各垫一层深色）。
        public static void OutlineText(float x, float y, float w, float h, string value, float size, Color color, Color outline, TextAnchor anchor, bool bold = true, bool numeric = false, float d = 1.2f, bool wrap = false)
        {
            if (string.IsNullOrEmpty(value)) return;
            Ui.Text(x - d, y - d, w, h, value, size, outline, anchor, bold, numeric, wrap);
            Ui.Text(x + d, y - d, w, h, value, size, outline, anchor, bold, numeric, wrap);
            Ui.Text(x - d, y + d, w, h, value, size, outline, anchor, bold, numeric, wrap);
            Ui.Text(x + d, y + d * 1.6f, w, h, value, size, outline, anchor, bold, numeric, wrap);
            Ui.Text(x, y, w, h, value, size, color, anchor, bold, numeric, wrap);
        }
        /// 单行字放不下就缩（和 Ui.FitText 一样，但用描边）。
        public static float Fit(string value, float size, float w, bool bold = true, bool numeric = false)
        {
            float tw = Ui.Width(value, size, bold, numeric);
            return tw > w ? Mathf.Max(8, size * w / tw) : size;
        }
        public static void Spaced(float x, float y, float w, float h, string value, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {   // 间距英文：字母之间插空格（IMGUI 没有字间距）
            if (string.IsNullOrEmpty(value)) return;
            var sb = new System.Text.StringBuilder(value.Length * 2);
            for (int i = 0; i < value.Length; i++) { if (i > 0) sb.Append(value[i] == ' ' ? "  " : " "); if (value[i] != ' ') sb.Append(value[i]); }
            Ui.Text(x, y, w, h, sb.ToString(), size, color, anchor, true, true);
        }

        // ---------- 面板 ----------
        public static void Plastic(float x, float y, float w, float h) { Nine("panel_plastic", x - 24, y - 24, w + 48, h + 48, 52, Color.white); }
        public static void Plastic(float x, float y, float w, float h, Color tint) { Nine("panel_plastic", x - 24, y - 24, w + 48, h + 48, 52, tint); }
        public static void Recess(float x, float y, float w, float h) { Nine("panel_recess", x, y, w, h, 34, Color.white); }
        public static void Metal(float x, float y, float w, float h) { Nine("panel_metal", x - 20, y - 20, w + 40, h + 40, 46, Color.white); }
        public static void Paper(float x, float y, float w, float h) { Nine("panel_paper", x - 12, y - 12, w + 24, h + 24, 22, Color.white); }
        public static void Glass(float x, float y, float w, float h) { Nine("panel_glass", x, y, w, h, 24, Color.white); }
        public static void Screw(float cx, float cy) { Sprite("screw", cx - 6, cy - 6, 12, 12, Color.white); }
        public static void Rail(float x, float y, float w)
        {   // 杆在 y ~ y+8；两端支架
            Nine("rail", x, y - 3, w, 22, 8, 0, 8, 0, Color.white);
            Sprite("rail_end", x - 7, y - 8, 16, 24, Color.white); Sprite("rail_end", x + w - 9, y - 8, 16, 24, Color.white);
        }
        public static void Hook(float cx, float y) { Sprite("hook", cx - 15, y - 2, 30, 14, Color.white); }
        /// 夹子：卡片上沿在 cardTop。
        public static void Clip(float cx, float cardTop) { Sprite("clip", cx - 17, cardTop - 30, 34, 44, Color.white); }
        public static void Peg(float cx, float y) { Sprite("peg", cx - 5, y - 2, 10, 14, Color.white); }
        public static void Led(float x, float y, float w, Color color)
        {   // 发光灯带（中线在 y）
            Nine("led", x - 14, y - 22, w + 28, 44, 22, 0, 22, 0, color);
            Nine("led", x - 14, y - 22, w + 28, 44, 22, 0, 22, 0, Ui.Alpha(Color.white, .45f));
        }
        public static void Hazard(float x, float y, float w, float h, bool grey = false, float shift = 0)
        {
            Tile(grey ? "tile_hazard_grey" : "tile_hazard", x, y, w, h, 18, 18, Color.white, shift, 0);
        }
        public static void Pill(float x, float y, float w, float h, Color fill) { Capsule("pill", x, y, w, h, fill); }
        /// 大胶囊（镜子这种几百像素的形状）：用 512 像素的大圆 disk_big 切四角，放大不糊。
        public static void Stadium(float x, float y, float w, float h, Color fill) { Capsule("disk_big", x, y, w, h, fill); }
        public static void PillLine(float x, float y, float w, float h, Color line) { Capsule("pill_line", x, y, w, h, line); }
        public static void Round(float x, float y, float w, float h, Color fill) { Nine("round4", x, y, w, h, 6, fill); }
        public static void RoundLine(float x, float y, float w, float h, Color line) { Nine("round4_line", x, y, w, h, 6, line); }
        public static void Disk(float cx, float cy, float r, Color fill) { Sprite("disk", cx - r, cy - r, r * 2, r * 2, fill); }
        public static void Glow(float cx, float cy, float r, Color color, float opacity) { QualityVisual.Halo(Ui.P(cx, cy), r * Ui.K, color, opacity); }
        public static void Barcode(float x, float y, float w, float h, Color color) { Sprite("barcode", x, y, w, h, color); }
        public static void Tape(float x, float y, float w, float h, float angle)
        {
            Matrix4x4 m = Rotate(angle, x + w / 2, y + h / 2); Sprite("tape", x, y, w, h, Ui.Hex(0xFFF6D6) * new Color(1, 1, 1, .62f)); End(m);
        }
        public static void Stamp(float cx, float cy, string label, Color color, float angle, float size)
        {
            float w = Ui.Width(label, size, true) + 30, h = size + 18;
            Matrix4x4 m = Rotate(angle, cx, cy);
            Nine("stamp_frame", cx - w / 2 - 4, cy - h / 2 - 4, w + 8, h + 8, 12, Ui.Alpha(color, .9f));
            Ui.Text(cx - w / 2, cy - h / 2, w, h, label, size, Ui.Alpha(color, .88f), TextAnchor.MiddleCenter, true);
            End(m);
        }
        /// 圆贴纸（白边、投影、里面一层颜色、中间字），angle 顺时针。
        public static void Sticker(float cx, float cy, float r, Color fill, string label, float angle, float size, Color textColor, bool numeric = true)
        {
            Matrix4x4 m = Rotate(angle, cx, cy);
            float s = r / 14f;
            Sprite("sticker", cx - 20 * s, cy - 20 * s, 40 * s, 40 * s, Color.white);
            Disk(cx, cy, r, fill);
            Ui.Text(cx - r, cy - r, r * 2, r * 2, label, size, textColor, TextAnchor.MiddleCenter, true, numeric);
            End(m);
        }
        /// 方贴纸（白边圆角块 + 颜色 + 字）。
        public static void Tag(float x, float y, float w, float h, Color fill, string label, float angle, float size, Color textColor)
        {
            Matrix4x4 m = Rotate(angle, x + w / 2, y + h / 2);
            Nine("tag_back", x - 4, y - 4, w + 8, h + 10, 8, Color.white);
            Round(x, y, w, h, fill);
            Ui.Text(x, y, w, h, label, Fit(label, size, w - 8), textColor, TextAnchor.MiddleCenter, true);
            End(m);
        }
        public static void Gem(float cx, float cy, float r, Color fill, bool on)
        {
            float s = r / 5f;
            Sprite("gem_out", cx - 7 * s, cy - 7 * s, 14 * s, 14 * s, Ink2);
            Sprite("gem", cx - 7 * s, cy - 7 * s, 14 * s, 14 * s, on ? fill : Ui.Hex(0x3E4045));
            if (on) Sprite("gem_hi", cx - 7 * s, cy - 7 * s, 14 * s, 14 * s, new Color(1, 1, 1, .35f));
        }
        /// 圆形角标（黑边、亮色底、一个字）。
        public static void Badge(float cx, float cy, float r, Color fill, string glyph)
        {
            Disk(cx, cy + 1.2f, r + 1.6f, new Color(0, 0, 0, .45f));
            Disk(cx, cy, r + 1.6f, Ink2);
            Disk(cx, cy, r, fill);
            Sprite("ring", cx - r + 1.2f, cy - r + 1.2f, r * 2 - 2.4f, r * 2 - 2.4f, new Color(1, 1, 1, .4f));
            Ui.Text(cx - r, cy - r, r * 2, r * 2, glyph, r * 1.08f, Ink2, TextAnchor.MiddleCenter, true);
        }

        // ---------- 按键 ----------
        public enum Key { White, Orange, Dark, Ghost }
        private static readonly string[] KeyNames = { "white", "orange", "dark", "ghost" };
        /// 带厚度的按键；selected = 按下去的样子（标签、开关）。返回是否点了。
        public static bool Keycap(float x, float y, float w, float h, string label, Key kind, bool enabled = true, float size = 15, bool selected = false, bool numeric = false)
        {
            if (!enabled) kind = Key.Ghost;
            bool hover = enabled && Ui.Hover(x, y, w, h);
            bool down = selected || (hover && Input.GetMouseButton(0));
            Nine("key_" + KeyNames[(int)kind] + (down ? "_down" : ""), x - 8, y - 8, w + 16, h + 16, 26, Color.white);
            if (hover && !down) Nine("key_mask", x - 8, y - 8, w + 16, h + 16, 26, new Color(1, 1, 1, kind == Key.Dark ? .08f : .16f));
            float oy = down ? 3 : 0, th = down ? 0 : 4.5f;
            Color tc = !enabled ? TextLight : kind == Key.White ? TextDark : kind == Key.Ghost ? TextLight : Color.white;
            if (!string.IsNullOrEmpty(label))
                Ui.Text(x, y + oy, w, h - oy - th, label, Fit(label, size, w - 14, true, numeric), tc, TextAnchor.MiddleCenter, true, numeric);
            bool hit = enabled && Ui.Click(x, y, w, h);
            if (hit) Ui.Clicked?.Invoke();
            return hit;
        }
        /// 液晶屏：右对齐的发光数字 + 左边一个小标签。
        public static void Lcd(float x, float y, float w, float h, string value, string label, Color color, float size)
        {
            Nine("lcd", x, y, w, h, 12, Color.white);
            float fit = Fit(value, size, w - (string.IsNullOrEmpty(label) ? 20 : Ui.Width(label, 10) + 30), true, true);
            Ui.Text(x + 10, y + 1, w - 20, h, value, fit, Ui.Alpha(color, .35f), TextAnchor.MiddleRight, true, true);   // 一层淡的当光晕
            Ui.Text(x + 10, y, w - 20, h, value, fit, color, TextAnchor.MiddleRight, true, true);
            if (!string.IsNullOrEmpty(label)) Ui.Text(x + 10, y, w - 20, h, label, 10, Ui.Hex(0x8E8F8D), TextAnchor.MiddleLeft, true);
        }
        /// 背景：深炭灰 + 细网格 + 两角网点。
        public static void PageBackground()
        {
            Ui.FillScreen(PageBack);
            Tile("tile_grid", 0, 0, 1600, 900, 40, 40, new Color(1, 1, 1, .04f));
            Sprite("fx_halftone_big", 0, 520, 760, 380, new Color(1, 1, 1, .06f));
            Texture2D t = Tex("fx_halftone_big");
            if (t != null && Painting)
            {   // 右上角：同一张图翻转
                Color old = GUI.color; GUI.color = new Color(1, 1, 1, .05f);
                GUI.DrawTextureWithTexCoords(Ui.R(840, 0, 760, 380), t, new Rect(1, 1, -1, -1), true);
                GUI.color = old;
            }
        }
    }
}
