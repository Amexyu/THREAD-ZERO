using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // v0.17 背景动效：在静态分层背景上叠一层会动的东西——灯的呼吸和偶尔闪烁、水面反光、滴水、蒸汽、会动的道具（水泵的阀门轮和压力表指针）。
    // 每个场景一份配置「<场景>_fx.json」，和背景图放在同一个文件夹。坐标是图层图片里的比例坐标（0..1），跟着那一层一起卷动；
    // 道具按逻辑像素间隔摆放（不跟着图层平铺重复），所以不会每张图出现一次、也不会被接缝切断。
    // 画风规则：全部是硬边平涂色块，不用渐变光晕、光束、体积雾。
    // v0.18：加了落水（排水口水帘）、指示灯、窗前人影、雨、道具运动（摆动 / 平移 / 升降），首领场地（不卷动的远景）也能挂动效。
    [Serializable]
    public sealed class StageFxConfig
    {
        public StageFxLamp[] lamps = new StageFxLamp[0];
        public StageFxWater[] water = new StageFxWater[0];
        public StageFxPoint[] drips = new StageFxPoint[0];
        public StageFxPoint[] steam = new StageFxPoint[0];
        public StageFxProp[] props = new StageFxProp[0];
        public StageFxFlow[] flows = new StageFxFlow[0];
        public StageFxBlink[] blinks = new StageFxBlink[0];
        public StageFxWindow[] windows = new StageFxWindow[0];
        public StageFxRain[] rain = new StageFxRain[0];
        public StageFxBubbles[] bubbles = new StageFxBubbles[0];   // v0.37
    }

    /// 灯：u, v 是灯泡位置；r 是光照区半径（按图层高度的比例）。
    [Serializable] public sealed class StageFxLamp { public int layer = 1; public float u, v, r = .1f; public Color color; }   // color 不填（a = 0）就是暖白灯泡；屏幕之类填青色

    /// 水面：v0..v1 是水面所在的高度范围（按图层高度的比例），count 是反光线条数；color 不填（a = 0）就是默认的青白色。
    [Serializable] public sealed class StageFxWater { public int layer = 2; public float v0, v1; public int count = 14; public Color color; }

    /// 落水（水帘）：u0..u1 × v0..v1 是水帘范围；count 是往下流的亮纹条数；speed 是流速（图层高度 / 秒）。
    [Serializable] public sealed class StageFxFlow { public int layer = 1; public float u0, u1, v0, v1; public int count = 8; public float speed = .5f; }

    /// 指示灯：r 是灯半径（按图层高度的比例）；period 秒一个周期，duty 是亮着的比例，phase 错开相位；color 是亮时的颜色。
    [Serializable] public sealed class StageFxBlink { public int layer = 1; public float u, v, r = .006f, period = 1.2f, duty = .5f, phase; public Color color; }

    /// 亮着的窗：u0..u1 × v0..v1 是窗玻璃范围；大约每 every 秒有人影从窗前走过一次（每张平铺各自错开）。
    [Serializable] public sealed class StageFxWindow { public int layer = 1; public float u0, v0, u1, v1, every = 14; }

    /// v0.37 气泡：u0..u1 × v0..v1 的竖条里（冷却液柱、水缸），小气泡从底往上冒、左右轻晃，快到顶时变淡；speed 是上升速度（图层高度 / 秒）。
    [Serializable] public sealed class StageFxBubbles { public int layer = 1; public float u0, u1, v0, v1; public int count = 5; public float speed = .06f; }

    /// 雨：整屏画在这一层上面。count 根雨丝；angle 倾斜角（度，正数往左下落）；speed 下落速度（逻辑像素 / 秒）；len 雨丝长度；alpha 不透明度；v1 雨落到这一层的哪里为止。
    [Serializable] public sealed class StageFxRain { public int layer = 0; public int count = 60; public float angle = 12, speed = 1400, len = 46, width = 1.5f, alpha = .22f, v1 = 1; }

    /// 滴水 / 蒸汽的出处。fall：水滴落下的距离（按图层高度的比例）；every：多少秒一次。
    [Serializable] public sealed class StageFxPoint { public int layer = 1; public float u, v, fall = .2f, every = 3; }

    /// 会动的道具。x0 / spacing：第一台的位置和间隔（这一层卷动坐标下的逻辑像素）；baseV：底部在图层高度的哪里；height：道具高度（按图层高度的比例）。
    /// wheel*：阀门轮中心（道具图里的比例坐标）、直径（按道具高度的比例）、转速（度 / 秒）；hubR：轮子后面盖住原轴座的圆（按道具高度的比例）。
    /// gauge*：压力表中心和指针长度（按道具高度的比例），gaugeU < 0 表示没有压力表。
    [Serializable]
    public sealed class StageFxProp
    {
        public int layer = 1;
        public string body = "", wheel = "";
        public float x0 = 900, spacing = 2600, baseV = .78f, height = .26f;
        public float wheelU = .5f, wheelV = .5f, wheelSize = .3f, wheelSpeed = 25, hubR = 0;
        public float gaugeU = -1, gaugeV, gaugeR;
        /// 道具运动：""（不动）、"swing"（绕 pivotU / pivotV 摆动 amp 度）、"slide"（左右来回 amp 逻辑像素）、"lift"（上下来回 amp 逻辑像素）；period 是一个来回的秒数。
        public string motion = "";
        public float amp, period = 6, pivotU = .5f, pivotV;
    }

    public static class StageFx
    {
        private static readonly Dictionary<string, StageFxConfig> configs = new Dictionary<string, StageFxConfig>();
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly List<float> spots = new List<float>();
        private static readonly List<int> tiles = new List<int>();   // spots 里每个位置属于第几张平铺（用来让每张图上的同一处东西各自错开）
        private static Texture2D disc, soft;
        private static bool cover;      // 首领场地：图层不平铺、不卷动，铺满一个固定矩形
        /// v0.33 亮色背景（V17）：白天的配色。灯不再压暗周围（白天墙是亮的），水面反光、水帘亮纹、蒸汽换成白色，
        /// 水滴换成浅天蓝（白色水滴在浅色墙上看不见）。StageArt.Background 每次画之前按用的文件夹设好。
        public static bool Daylight;
        private static float coverX;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { configs.Clear(); textures.Clear(); }

        /// 读场景配置（path = 文件夹 + 场景字母）；没有配置返回 null，这个场景就没有动效。
        public static StageFxConfig Config(string path)
        {
            StageFxConfig c;
            if (configs.TryGetValue(path, out c)) return c;
            var text = Resources.Load<TextAsset>(path + "_fx");
            c = null;
            if (text != null)
            {
                try { c = JsonUtility.FromJson<StageFxConfig>(text.text); }
                catch (Exception e) { Debug.LogWarning("[StageFx] 配置读不了：" + path + "_fx " + e.Message); }
            }
            configs[path] = c;
            return c;
        }

        /// 某一层画完之后调用，画这一层上的动效。scroll / h / y 和 StageArt.Layer 用的一样。
        public static void Draw(StageFxConfig fx, string folder, int layer, Texture2D t, float scroll, float h, float y, float time)
        {
            if (fx == null || t == null) return;
            DrawAll(fx, folder, layer, h * t.width / t.height, scroll, h, y, time);   // W：这一层的一张图铺开有多宽（逻辑像素）
        }

        /// 不卷动、铺满矩形 r 的一层（首领场地的远景）上的动效。
        public static void DrawCover(StageFxConfig fx, string folder, int layer, Rect r, float time)
        {
            if (fx == null) return;
            cover = true; coverX = r.x;
            try { DrawAll(fx, folder, layer, r.width, 0, r.height, r.y, time); }
            finally { cover = false; }
        }

        private static void DrawAll(StageFxConfig fx, string folder, int layer, float W, float scroll, float h, float y, float time)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            Matrix4x4 matrix = GUI.matrix;
            for (int i = 0; i < fx.lamps.Length; i++) if (fx.lamps[i].layer == layer) Lamp(fx.lamps[i], i, W, scroll, h, y, time);
            for (int i = 0; i < fx.water.Length; i++) if (fx.water[i].layer == layer) Water(fx.water[i], i, W, scroll, h, y, time);
            for (int i = 0; i < fx.drips.Length; i++) if (fx.drips[i].layer == layer) Drip(fx.drips[i], i, W, scroll, h, y, time);
            for (int i = 0; i < fx.steam.Length; i++) if (fx.steam[i].layer == layer) Steam(fx.steam[i], i, W, scroll, h, y, time);
            if (fx.flows != null) for (int i = 0; i < fx.flows.Length; i++) if (fx.flows[i].layer == layer) Flow(fx.flows[i], i, W, scroll, h, y, time);
            if (fx.windows != null) for (int i = 0; i < fx.windows.Length; i++) if (fx.windows[i].layer == layer) Window(fx.windows[i], i, W, scroll, h, y, time);
            if (fx.bubbles != null) for (int i = 0; i < fx.bubbles.Length; i++) if (fx.bubbles[i].layer == layer) Bubbles(fx.bubbles[i], i, W, scroll, h, y, time);
            if (fx.blinks != null) for (int i = 0; i < fx.blinks.Length; i++) if (fx.blinks[i].layer == layer) Blink(fx.blinks[i], i, W, scroll, h, y, time);
            if (!cover) for (int i = 0; i < fx.props.Length; i++) if (fx.props[i].layer == layer) Prop(fx.props[i], folder, scroll, h, y, time);
            if (fx.rain != null) for (int i = 0; i < fx.rain.Length; i++) if (fx.rain[i].layer == layer) Rain(fx.rain[i], i, h, y, time);
            GUI.matrix = matrix;
            GUI.color = old;
        }

        // ---------- 灯：慢慢呼吸，每隔 9 秒左右闪两下 ----------
        // 光照区用柔边圆压暗（顺着原画里灯光的衰减），不额外画光晕，只改变原画的明暗。
        private static void Lamp(StageFxLamp l, int i, float W, float scroll, float h, float y, float time)
        {
            float r = l.r * h, by = y + l.v * h;
            Spots(l.u, W, scroll, r * 1.8f);
            for (int s = 0; s < spots.Count; s++)
            {
                float phase = Hash(i * 7 + tiles[s] * 53 + 1) * 10;
                float breathe = .5f + .5f * Mathf.Sin(time * 1.4f + phase);
                float k = Mathf.Repeat(time + phase * 3.1f, 9f);
                bool off = (k > 8.3f && k < 8.38f) || (k > 8.5f && k < 8.56f);
                float x = spots[s];
                float dim = Daylight ? .18f : 1f;   // 白天：灯光区只压暗一点点，不然亮墙上会出现一块脏斑
                SoftEllipse(x, by + r * .5f, r * 1.6f, r * 1.4f, new Color(.02f, .05f, .04f, (off ? .32f : .08f * (1 - breathe)) * dim));
                if (!off) Ellipse(x, by, r * .1f, r * .08f, l.color.a > 0 ? new Color(l.color.r, l.color.g, l.color.b, .3f + .35f * breathe) : new Color(1f, .95f, .75f, .3f + .35f * breathe));   // 灯泡
            }
        }

        // ---------- 水面：几道短反光线，慢慢漂移、忽明忽暗 ----------
        private static void Water(StageFxWater w, int i, float W, float scroll, float h, float y, float time)
        {
            float top = y + w.v0 * h, bottom = y + w.v1 * h;
            if (!cover) W = Mathf.Max(W, 1700f);   // 地面图很窄，反光如果跟着它平铺会一眼看出重复
            Color tint = w.color.a > 0 ? w.color : Daylight ? new Color(1f, 1f, 1f, 1.25f) : new Color(.78f, .9f, .86f, 1);
            for (int d = 0; d < w.count; d++)
            {
                int seed = i * 101 + d * 13;
                float a = Mathf.Sin(time * (.6f + .5f * Hash(seed + 5)) + Hash(seed + 4) * 6.28f);
                if (a <= 0) continue;
                float len = 18 + 46 * Hash(seed + 2), drift = (Hash(seed + 3) - .5f) * 24, thick = 2 + 2 * Hash(seed + 6);
                float ly = top + Hash(seed + 1) * (bottom - top);
                float u = Mathf.Repeat(Hash(seed) + drift * time / W, 1f);
                Color c = new Color(tint.r, tint.g, tint.b, .32f * a * a * tint.a);
                foreach (float x in Spots(u, W, scroll, len)) Box(x - len / 2, ly, len * (.6f + .4f * a), thick, c);
            }
        }

        // ---------- 滴水：管口鼓起 → 落下 → 两道小水花。每张平铺只有约三成的位置会滴，时间各自错开 ----------
        private static void Drip(StageFxPoint p, int i, float W, float scroll, float h, float y, float time)
        {
            const float g = 1900, swell = .35f, splash = .4f;   // 逻辑像素 / 秒²
            float fall = p.fall * h, tf = Mathf.Sqrt(2 * fall / g), every = Mathf.Max(tf + swell + splash + .2f, p.every), sy = y + p.v * h;
            Color c = Daylight ? new Color(.58f, .84f, .98f, .92f) : new Color(.8f, .92f, .88f, .85f);
            Spots(p.u, W, scroll, 40);
            for (int s = 0; s < spots.Count; s++)
            {
                float pick = Hash(i * 31 + tiles[s] * 977 + 3);
                if (pick > .3f) continue;
                float k = Mathf.Repeat(time + pick * 37 * every, every * (1 + pick)), x = spots[s];
                if (k < swell) { float q = k / swell; Ellipse(x, sy + 2, 2.5f * q + .5f, 3f * q + .5f, c); }
                else if (k < swell + tf) { float tt = k - swell; Ellipse(x, sy + 2 + .5f * g * tt * tt, 2.5f, 4.5f, c); }
                else if (k < swell + tf + splash)
                {
                    float q = (k - swell - tf) / splash, ly = sy + fall - 2 - 24 * q * (1 - q);
                    Color cs = new Color(c.r, c.g, c.b, .8f * (1 - q));
                    Box(x - 6 - 14 * q, ly, 5, 2, cs);
                    Box(x + 1 + 14 * q, ly, 5, 2, cs);
                }
            }
        }

        // ---------- 蒸汽：隔几秒冒一串小团，边升边变大变淡 ----------
        private static void Steam(StageFxPoint p, int i, float W, float scroll, float h, float y, float time)
        {
            const float life = 1.8f;
            float every = Mathf.Max(2f, p.every), sy = y + p.v * h;
            Spots(p.u, W, scroll, 80);
            for (int n = 0; n < spots.Count; n++)
            {
                float x = spots[n], k = Mathf.Repeat(time + Hash(i * 17 + tiles[n] * 131 + 5) * every * 3, every);
                for (int j = 0; j < 4; j++)
                {
                    float a = k - j * .22f;
                    if (a < 0 || a > life) continue;
                    float s = a / life, rad = 5 + 15 * s;
                    Ellipse(x + 10 * s + 4 * Mathf.Sin(a * 3 + j), sy - 70 * s, rad, rad * .85f, Daylight ? new Color(1f, 1f, 1f, .5f * (1 - s)) : new Color(.85f, .9f, .87f, .3f * (1 - s)));
                }
            }
        }

        // ---------- 会动的道具：水泵（阀门轮转、压力表指针抖） ----------
        private static void Prop(StageFxProp p, string folder, float scroll, float h, float y, float time)
        {
            Texture2D body = PropTexture(folder + p.body), wheel = string.IsNullOrEmpty(p.wheel) ? null : PropTexture(folder + p.wheel);
            if (body == null) return;
            float bh = p.height * h, bw = bh * body.width / body.height, bottom = y + p.baseV * h, top = bottom - bh;
            float spacing = Mathf.Max(bw + 200, p.spacing);
            int first = Mathf.FloorToInt((scroll - p.x0 - bw) / spacing);
            for (int n = first; n < first + 6; n++)
            {
                float left = p.x0 + n * spacing - scroll - bw / 2, dy = 0, swing = 0;
                if (left - Mathf.Abs(p.amp) > 1600) break;
                if (left + bw + Mathf.Abs(p.amp) < 0) continue;
                float wave = Mathf.Sin((time / Mathf.Max(.5f, p.period) + Hash(n * 7 + 11)) * Mathf.PI * 2);
                if (p.motion == "slide") left += p.amp * wave;
                else if (p.motion == "lift") dy = p.amp * (.5f - .5f * wave);   // 从最高处往下放到 amp，再收回
                else if (p.motion == "swing") swing = p.amp * wave;
                Matrix4x4 pm = GUI.matrix;
                if (swing != 0) GUIUtility.RotateAroundPivot(swing, Ui.R(left + p.pivotU * bw, top + dy + p.pivotV * bh, 0, 0).position);
                top += dy;
                GUI.color = Color.white;
                if (!Fade.Draw(new Rect(left, top, bw, bh), body, new Rect(0, 0, 1, 1), Color.white)) GUI.DrawTexture(Ui.R(left, top, bw, bh), body);
                if (p.gaugeU >= 0)
                {   // 压力表指针：在 -40° 附近抖
                    float gx = left + p.gaugeU * bw, gy = top + p.gaugeV * bh, gr = p.gaugeR * bh;
                    float ang = (-40 + 9 * Mathf.Sin(time * 5.3f + n) + 3 * Mathf.Sin(time * 23f)) * Mathf.Deg2Rad;
                    Ui.Stroke(gx, gy, gx + Mathf.Sin(ang) * gr, gy - Mathf.Cos(ang) * gr, Mathf.Max(1.5f, gr * .12f), new Color(.55f, .1f, .08f, 1));
                    Ellipse(gx, gy, Mathf.Max(1.5f, gr * .14f), Mathf.Max(1.5f, gr * .14f), new Color(.15f, .12f, .1f, 1));
                }
                if (wheel != null)
                {
                    float ws = p.wheelSize * bh, wx = left + p.wheelU * bw, wy = top + p.wheelV * bh;
                    if (p.hubR > 0) Ellipse(wx, wy, p.hubR * bh, p.hubR * bh, Daylight ? new Color(.44f, .5f, .58f, 1) : new Color(.2f, .24f, .22f, 1));   // 盖住原图上画死的轴座（白天的浅色水泵上用钢灰色）
                    Rect wr = Ui.R(wx - ws / 2, wy - ws / 2, ws, ws);
                    Matrix4x4 m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(time * p.wheelSpeed + n * 37, wr.center);
                    GUI.color = Color.white;
                    if (!Fade.Draw(new Rect(wx - ws / 2, wy - ws / 2, ws, ws), wheel, new Rect(0, 0, 1, 1), Color.white)) GUI.DrawTexture(wr, wheel);
                    GUI.matrix = m;
                }
                GUI.matrix = pm;
                top -= dy;
            }
        }

        // ---------- 落水：几道亮纹顺着水帘往下流，底部一串水花 ----------
        private static void Flow(StageFxFlow f, int i, float W, float scroll, float h, float y, float time)
        {
            float top = y + f.v0 * h, span = (f.v1 - f.v0) * h, width = (f.u1 - f.u0) * W, speed = Mathf.Max(.05f, f.speed) * h;
            if (span <= 0 || width <= 0) return;
            Spots(f.u0, W, scroll, width + 30);
            for (int s = 0; s < spots.Count; s++)
            {
                float left = spots[s];
                for (int d = 0; d < f.count; d++)
                {
                    int seed = i * 211 + d * 17;
                    float len = span * (.1f + .14f * Hash(seed + 1)), v = speed * (.8f + .5f * Hash(seed + 2));
                    float k = Mathf.Repeat(time * v + Hash(seed + 3) * (span + len), span + len) - len;   // 亮纹上端离水帘顶多远
                    float y0 = Mathf.Max(0, k), y1 = Mathf.Min(span, k + len);
                    if (y1 <= y0) continue;
                    float lx = left + (.06f + .88f * Hash(seed)) * width;
                    Box(lx, top + y0, 2 + 2 * Hash(seed + 4), y1 - y0, Daylight ? new Color(1f, 1f, 1f, .3f + .16f * Hash(seed + 5)) : new Color(.74f, .9f, .84f, .22f + .14f * Hash(seed + 5)));
                }
                for (int j = 0; j < 4; j++)
                {   // 水花：小扁圆往上冒、变大、变淡
                    float q = Mathf.Repeat(time * 1.6f + j * .27f + Hash(i * 5 + j), 1f), ex = left + width * (.15f + .23f * j);
                    Ellipse(ex, top + span - 2 - 10 * q, 5 + 7 * q, 2.5f + 2 * q, Daylight ? new Color(1f, 1f, 1f, .45f * (1 - q)) : new Color(.8f, .93f, .88f, .3f * (1 - q)));
                }
            }
        }

        // ---------- 气泡（v0.37）：液柱里的小气泡往上冒，左右轻晃，快到顶时变淡 ----------
        private static void Bubbles(StageFxBubbles b, int i, float W, float scroll, float h, float y, float time)
        {
            float top = y + b.v0 * h, span = (b.v1 - b.v0) * h, width = (b.u1 - b.u0) * W, speed = Mathf.Max(.01f, b.speed) * h;
            if (span <= 0 || width <= 0) return;
            Spots(b.u0, W, scroll, width + 10);
            for (int s = 0; s < spots.Count; s++)
            {
                float left = spots[s];
                for (int d = 0; d < b.count; d++)
                {
                    int seed = i * 307 + d * 23 + tiles[s] * 5;
                    float v = speed * (.7f + .6f * Hash(seed + 1));
                    float k = Mathf.Repeat(time * v + Hash(seed + 2) * span, span);   // 离液柱底部多高
                    float q = k / span, r = (1.6f + 2.2f * Hash(seed + 3)) * (1 + .35f * q);
                    float bx = left + (.2f + .6f * Hash(seed)) * width + Mathf.Sin(time * (2 + 2 * Hash(seed + 4)) + d) * width * .08f;
                    float by = top + span - k;
                    float a = Mathf.Min(1, q * 6) * (1 - q * q);   // 底部淡入、顶部淡出
                    if (a <= .02f) continue;
                    Ellipse(bx, by, r, r, Daylight ? new Color(.86f, .96f, 1f, .6f * a) : new Color(.8f, .93f, .9f, .45f * a));
                    Ellipse(bx - r * .3f, by - r * .3f, r * .38f, r * .38f, new Color(1f, 1f, 1f, .9f * a));   // 高光点
                }
            }
        }

        // ---------- 指示灯：亮 / 灭交替，硬边 ----------
        private static void Blink(StageFxBlink b, int i, float W, float scroll, float h, float y, float time)
        {
            float r = Mathf.Max(1.5f, b.r * h), by = y + b.v * h, period = Mathf.Max(.1f, b.period);
            Color on = b.color.a > 0 ? b.color : new Color(1f, .32f, .18f, 1), off = new Color(on.r * .28f, on.g * .28f, on.b * .28f, 1);
            Spots(b.u, W, scroll, r * 2);
            for (int s = 0; s < spots.Count; s++)
            {
                bool lit = Mathf.Repeat(time / period + b.phase + tiles[s] * .37f, 1f) < b.duty;
                Ellipse(spots[s], by, r * 1.4f, r * 1.4f, new Color(.07f, .07f, .07f, 1));   // 灯座
                Ellipse(spots[s], by, r, r, lit ? on : off);
                if (lit) Ellipse(spots[s] - r * .3f, by - r * .3f, r * .35f, r * .35f, new Color(1, 1, 1, .7f));   // 高光点
            }
        }

        // ---------- 窗前人影：偶尔有人从亮着的窗前走过 ----------
        private static void Window(StageFxWindow w, int i, float W, float scroll, float h, float y, float time)
        {
            float ww = (w.u1 - w.u0) * W, wh = (w.v1 - w.v0) * h, top = y + w.v0 * h, every = Mathf.Max(6, w.every);
            if (ww <= 0 || wh <= 0) return;
            const float walk = 3.2f;
            Color c = new Color(.17f, .09f, .05f, .62f);   // 隔着玻璃的暖色暗影，不用纯黑（纯黑像界面上的头像图标）
            if (Daylight) c = new Color(.2f, .27f, .4f, .32f);   // v0.33 白天：屋里比外面暗，人影是淡淡的蓝灰色
            Spots(w.u0, W, scroll, ww);
            for (int s = 0; s < spots.Count; s++)
            {
                int seed = i * 61 + tiles[s] * 389;
                float period = every * (.8f + .6f * Hash(seed + 1)), k = Mathf.Repeat(time + Hash(seed) * period * 3, period);
                if (k > walk) continue;
                float q = k / walk;
                if (Hash(seed + (int)((time + Hash(seed) * period * 3) / period) * 13) > .5f) q = 1 - q;   // 每次走的方向随机
                float bodyW = wh * .46f, cx = spots[s] - bodyW + q * (ww + bodyW * 2), bob = Mathf.Abs(Mathf.Sin(k * 6)) * wh * .025f;
                float lean = (Hash(seed + 7) - .5f) * wh * .08f;   // 有的低着头、有的仰一点，不是同一个剪影
                Ui.BeginClip(spots[s], top, ww, wh, 0);
                // 头、脖子、肩膀叠在一起成一整块，不留缝（留缝就成了头像图标）
                Ellipse(cx + lean, top + wh * .44f - bob, wh * .12f, wh * .145f, c);
                Box(cx + lean * .5f - wh * .06f, top + wh * .52f - bob, wh * .12f, wh * .12f, c);
                Ellipse(cx, top + wh * .72f - bob, bodyW * .5f, wh * .13f, c);
                Box(cx - bodyW * .5f, top + wh * .72f - bob, bodyW, wh * .4f, c);   // 身子（下面被窗框裁掉）
                Ui.EndClip();
            }
        }

        // ---------- 雨：整屏斜落的细线 ----------
        private static void Rain(StageFxRain r, int i, float h, float y, float time)
        {
            float bottom = y + r.v1 * h, slope = Mathf.Tan(r.angle * Mathf.Deg2Rad), len = Mathf.Max(4, r.len), span = bottom - y + len;
            if (span <= len) return;
            Color c = Daylight ? new Color(1f, 1f, 1f, Mathf.Min(1, r.alpha * 1.5f)) : new Color(.78f, .86f, .92f, r.alpha);   // v0.33 白天：太阳雨，亮白的细丝
            for (int d = 0; d < r.count; d++)
            {
                int seed = i * 977 + d * 29;
                float v = r.speed * (.85f + .3f * Hash(seed + 1));
                float k = Mathf.Repeat(time * v / span + Hash(seed + 2), 1f) * span - len;   // 雨丝上端离这一层顶多远
                float x = Mathf.Repeat(Hash(seed) * (1600 + 200) - k * slope, 1800) - 100;
                float y0 = Mathf.Max(0, k), y1 = Mathf.Min(bottom - y, k + len);
                if (y1 <= y0) continue;
                Ui.Stroke(x - (y0 - k) * slope, y + y0, x - (y1 - k) * slope, y + y1, r.width * (.7f + .6f * Hash(seed + 3)), c);
            }
        }

        // ---------- 小工具 ----------

        /// 图层里比例坐标 u 处的东西，在屏幕上出现的所有横坐标（图层横向平铺，每隔 W 出现一次）。
        private static List<float> Spots(float u, float W, float scroll, float margin)
        {
            spots.Clear();
            tiles.Clear();
            if (cover) { spots.Add(coverX + u * W); tiles.Add(0); return spots; }
            if (W < 1) return spots;
            int n = Mathf.FloorToInt(scroll / W);
            float x = u * W - (scroll - n * W);
            while (x > -margin) { x -= W; n--; }
            for (; x < 1600 + margin; x += W, n++)
                if (x > -margin) { spots.Add(x); tiles.Add(n); }
            return spots;
        }

        /// 纯色矩形（不经过界面主题的颜色映射）。
        private static void Box(float x, float y, float w, float h, Color c)
        {
            if (c.a <= .003f || w <= 0 || h <= 0) return;
            GUI.color = Fade.Tint(c, x + w / 2, y + h / 2);   // 11-25 褪色试验：纯色的水花、灯光也跟着褪色
            GUI.DrawTexture(Ui.R(x, y, w, h), Texture2D.whiteTexture);
        }

        private static void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            if (c.a <= .003f) return;
            GUI.color = Fade.Tint(c, cx, cy);
            GUI.DrawTexture(Ui.R(cx - rx, cy - ry, rx * 2, ry * 2), Disc());
        }

        private static void SoftEllipse(float cx, float cy, float rx, float ry, Color c)
        {
            if (c.a <= .003f) return;
            GUI.color = Fade.Tint(c, cx, cy);
            GUI.DrawTexture(Ui.R(cx - rx, cy - ry, rx * 2, ry * 2), Soft());
        }

        /// 中心实、往外渐淡的圆，只用来压暗灯光区（顺着原画灯光的衰减）。
        private static Texture2D Soft()
        {
            if (soft != null) return soft;
            const int n = 64;
            soft = new Texture2D(n, n, TextureFormat.RGBA32, false);
            soft.wrapMode = TextureWrapMode.Clamp;
            soft.filterMode = FilterMode.Bilinear;
            soft.hideFlags = HideFlags.HideAndDontSave;
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                {
                    float d = Mathf.Sqrt((xx - c) * (xx - c) + (yy - c) * (yy - c)) / c;
                    px[yy * n + xx] = new Color32(255, 255, 255, (byte)(255 * Mathf.Pow(Mathf.Clamp01(1 - d), 1.6f)));
                }
            soft.SetPixels32(px);
            soft.Apply(false, true);
            return soft;
        }

        /// 白色实心圆，只留 1 像素抗锯齿（硬边）。
        private static Texture2D Disc()
        {
            if (disc != null) return disc;
            const int n = 64;
            disc = new Texture2D(n, n, TextureFormat.RGBA32, false);
            disc.wrapMode = TextureWrapMode.Clamp;
            disc.filterMode = FilterMode.Bilinear;
            disc.hideFlags = HideFlags.HideAndDontSave;
            var px = new Color32[n * n];
            float c = (n - 1) / 2f, r = n / 2f - 1;
            for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                {
                    float d = Mathf.Sqrt((xx - c) * (xx - c) + (yy - c) * (yy - c));
                    px[yy * n + xx] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(r - d + .5f)));
                }
            disc.SetPixels32(px);
            disc.Apply(false, true);
            return disc;
        }

        /// 道具图用夹边模式，避免平铺模式下边缘渗出对面的像素。
        private static Texture2D PropTexture(string path)
        {
            Texture2D t;
            if (!textures.TryGetValue(path, out t))
            {
                t = Resources.Load<Texture2D>(path);
                if (t != null) t.wrapMode = TextureWrapMode.Clamp;
                textures[path] = t;
            }
            return t;
        }

        private static float Hash(int i)
        {
            unchecked
            {
                uint x = (uint)i * 2654435761u;
                x ^= x >> 13;
                x *= 0x5bd1e995u;
                x ^= x >> 15;
                return (x & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
