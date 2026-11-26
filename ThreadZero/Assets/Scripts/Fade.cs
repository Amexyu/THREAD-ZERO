using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// 11-25 世界观「褪色之城」试验开关：舞台（背景三层、背景动效、机器人）先是黑白线稿，按进度把颜色刷回来；
    /// 每送回家一只机器人，它站的地方晕开一圈颜色。主角、漆、特效、界面都不受影响。
    /// On = false（默认）时完全不起作用，照旧走 GUI.DrawTexture。
    public static class Fade
    {
        public static bool On;
        /// 手动指定舞台饱和度（测试截图用）；false 时按关卡进度自动算。
        public static bool Manual;
        /// 舞台整体饱和度：0 = 线稿灰，1 = 原色。
        public static float StageSat = 1;
        public static float Tone = .55f;
        /// 正在画的这一层的卷动系数（远景 .25、中景 .6、地面和怪 1、首领场地 0），颜色圈跟着这一层一起卷。
        public static float LayerScroll = 1;
        public static float WorldX;
        /// 画怪时不叠颜色圈（怪的颜色只看自己的颜色条）。
        public static bool NoBlooms;
        private struct Bloom { public float sx, wx, y, r, born; }
        private static readonly List<Bloom> blooms = new List<Bloom>();
        private static readonly Vector4[] arr = new Vector4[16];
        private static Material mat; private static bool tried;
        public static void Clear() { blooms.Clear(); }
        /// 在舞台逻辑坐标 (x, y) 晕开一圈半径 r 的颜色（x 是当时在画面上的位置，之后跟着各层一起卷）。
        public static void Splash(float x, float y, float r)
        {
            if (blooms.Count >= arr.Length) blooms.RemoveAt(0);
            blooms.Add(new Bloom { sx = x, wx = WorldX, y = y, r = r, born = Time.unscaledTime });
        }
        private static bool Ready()
        {
            if (mat != null) return true;
            if (tried) return false;
            tried = true;
            Shader sh = Resources.Load<Shader>("Shaders/TZFade");
            if (sh == null || !sh.isSupported) { Debug.LogWarning("THREAD ZERO: Fade shader missing or unsupported"); return false; }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetFloat("_TZUseClip", 1);
            return true;
        }
        private static float Grow(float age) { float t = Mathf.Clamp01(age / .7f); return 1 - (1 - t) * (1 - t) * (1 - t); }
        /// 舞台上 (x, y) 这一点现在的饱和度（CPU 端，给纯色的灯光、水花用）。
        public static float SatAt(float x, float y)
        {
            float s = StageSat, now = Time.unscaledTime;
            if (NoBlooms) return s;
            foreach (Bloom b in blooms)
            {
                float bx = b.sx - (WorldX - b.wx) * LayerScroll, r = b.r * Grow(now - b.born);
                float d = new Vector2(x - bx, y - b.y).magnitude;
                s = Mathf.Max(s, Mathf.Clamp01((r - d) / Mathf.Max(r * .07f, 1)));
            }
            return Mathf.Clamp01(s);
        }
        /// 纯色也一起褪色（不开时原样返回）。
        public static Color Tint(Color c, float x, float y)
        {
            if (!On) return c;
            float s = SatAt(x, y), g = c.r * .299f + c.g * .587f + c.b * .114f;
            Color gray = new Color(g * .98f, g * .96f, g * .91f, c.a);
            return Color.Lerp(gray, c, s);
        }
        /// 用褪色材质画一张图。logical：逻辑坐标的矩形（和 Ui.R 的参数一样）；uv：贴图坐标（和 GUI.DrawTextureWithTexCoords 一样）。
        /// sat &lt; 0 用舞台饱和度。没开 / 不是重绘事件 / 着色器不可用时返回 false，调用方照旧画。
        public static bool Draw(Rect logical, Texture t, Rect uv, Color color, float sat = -1)
        {
            if (!On || t == null || Event.current == null || Event.current.type != EventType.Repaint || !Ready()) return false;
            float now = Time.unscaledTime; int n = 0;
            if (!NoBlooms)
                foreach (Bloom b in blooms)
                    arr[n++] = new Vector4(b.sx - (WorldX - b.wx) * LayerScroll, b.y, b.r * Grow(now - b.born), 1);
            mat.SetVectorArray("_TZBlooms", arr);
            mat.SetFloat("_TZBloomCount", n);
            mat.SetVector("_TZRect", new Vector4(logical.x, logical.y, logical.width, logical.height));
            mat.SetVector("_TZUV", new Vector4(uv.x, uv.y, Mathf.Abs(uv.width) < 1e-5f ? 1 : uv.width, Mathf.Abs(uv.height) < 1e-5f ? 1 : uv.height));
            mat.SetFloat("_TZSat", sat < 0 ? StageSat : sat);
            mat.SetFloat("_TZTone", Tone);
            Graphics.DrawTexture(Ui.R(logical.x, logical.y, logical.width, logical.height), t, uv, 0, 0, 0, 0, color, mat);
            return true;
        }
    }
}
