using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// 怪物攻击特效。位置用世界 x（随镜头滚动），y 用逻辑坐标；Draw 时传入镜头 x。只负责画面，伤害在 CombatSession 结算。
    public static class EnemyFx
    {
        private enum K { Claw, Shock, Muzzle, Burst, Flood, Pulse, Roar, Shield, Spray, Target, Crate, Sweep }
        private sealed class Fx { public K kind; public float born, life, x, y, x2, seed; public Color color; }
        private static readonly List<Fx> list = new List<Fx>();
        public static readonly Color Bolt = new Color(.98f, .42f, .25f), Beacon = new Color(.72f, .45f, 1f), Warn = new Color(1, .28f, .2f);
        private static readonly Color Hot = new Color(1, .93f, .75f), Water = new Color(.35f, .72f, .86f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Clear() { list.Clear(); }
        private static void Add(K k, float now, float life, float x, float y, Color c, float x2 = 0)
        { list.Add(new Fx { kind = k, born = now, life = life, x = x, y = y, x2 = x2, color = c, seed = Random.value * 100 }); }

        public static void Claw(float now, float x, float y) { Add(K.Claw, now, .22f, x, y, Hot); }
        public static void Shock(float now, float x, float groundY, bool big)
        { Add(K.Shock, now, big ? .6f : .45f, x, groundY, new Color(1, .6f, .3f), big ? 1.4f : 1); Add(K.Burst, now, .18f, x, groundY - 30, Hot, big ? 1.2f : .8f); }
        public static void Muzzle(float now, float x, float y, Color c) { Add(K.Muzzle, now, .14f, x, y, c); }
        public static void Burst(float now, float x, float y, Color c, float size = 1) { Add(K.Burst, now, .32f, x, y, c, size); }
        public static void Flood(float now, float fromX, float toX, float groundY)
        {
            Add(K.Flood, now, 1.5f, fromX, groundY, Water, toX);
            for (int i = 0; i < 26; i++) Add(K.Spray, now + Random.Range(0f, .55f), Random.Range(.45f, .8f), fromX, groundY, Color.white, toX);   // 浪头甩出的水花
            Add(K.Burst, now + .42f, .4f, toX, groundY - 110, new Color(.7f, .95f, 1f), 2.4f);   // 打到角色身上的那一下
        }
        /// 第二章首领「吊箱砸落」：先在角色脚下亮出落点（前摇期间），再从画面上方砸下一只货柜。
        public static void Target(float now, float x, float groundY, float life) { Add(K.Target, now, life, x, groundY, Warn); }
        public static void Crate(float now, float x, float groundY)
        {
            Add(K.Crate, now, 1.3f, x, groundY, new Color(.78f, .36f, .16f));
            Add(K.Shock, now + .2f, .6f, x, groundY, new Color(1, .6f, .3f), 1.6f);
            Add(K.Burst, now + .2f, .3f, x, groundY - 40, Hot, 1.6f);
        }
        /// 吊钩横扫：以角色为中心，一道扁平的弧光从身后扫到身前（像链子甩过留下的轨迹），x2 = 半径。
        public static void Sweep(float now, float x, float y, float radius) { Add(K.Sweep, now, .45f, x, y, new Color(1, .62f, .25f), radius); }
        public static void Shield(float now, float x, float y) { Add(K.Shield, now, .35f, x, y, new Color(.45f, .85f, 1f)); }
        public static void Pulse(float now, float x, float y) { Add(K.Pulse, now, .8f, x, y, Beacon); }
        public static void Roar(float now, float x, float y) { Add(K.Roar, now, .7f, x, y, Warn); }

        public static void Draw(float now, float camX)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Fx f = list[i]; float k = (now - f.born) / f.life;
                if (k < 0) continue;
                if (k >= 1) { list.RemoveAt(i); continue; }
                Paint(f, k, f.x - camX, f.x2 - camX);
            }
        }

        private static void Paint(Fx f, float k, float x, float x2)
        {
            float fade = 1 - k; Vector2 p = new Vector2(x, f.y);
            switch (f.kind)
            {
                case K.Claw:   // 扑咬：三道斜爪痕，从右上划到左下
                    for (int j = 0; j < 3; j++)
                    {
                        float o = (j - 1) * 16, len = Mathf.Min(1, k * 4) * 62;
                        Vector2 a = p + new Vector2(28 + o, -34 + o * .3f), d = new Vector2(-.62f, .78f);
                        Ui.Stroke(a.x, a.y, a.x + d.x * len, a.y + d.y * len, 7 * fade, A(Warn, .8f * fade));
                        Ui.Stroke(a.x, a.y, a.x + d.x * len, a.y + d.y * len, 3 * fade, A(Color.white, fade));
                    }
                    break;
                case K.Shock:  // 砸地：扁圆冲击环 + 地面裂纹
                {
                    float rx = (30 + 150 * Mathf.Sqrt(k)) * f.x2;
                    Ring(p, rx, rx * .16f, 6 * fade, A(f.color, fade));
                    Ring(p, rx * .62f, rx * .1f, 3 * fade, A(Color.white, .8f * fade));
                    for (int j = 0; j < 5; j++) { float o = (j - 2) * 24 + f.seed % 7; Ui.Stroke(x + o, f.y, x + o * 1.7f + 8, f.y + 2, 3, A(Ui.Ink, .5f * fade)); }
                    for (int j = 0; j < 4; j++) Ui.Disk(x + (j - 1.5f) * 40 * (1 + k), f.y - 8 - 26 * k, (8 + 14 * k) * f.x2, A(new Color(.62f, .6f, .56f), .35f * fade));
                    break;
                }
                case K.Muzzle:
                    Glow(p, 40, f.color, .9f * fade);
                    Ui.Disk(x, f.y, 10 * fade + 2, A(Color.white, fade));
                    Ui.Stroke(x, f.y, x - 34 * (1 - k * .5f), f.y, 6 * fade, A(f.color, fade));
                    break;
                case K.Burst:
                {
                    float s = f.x2 <= 0 ? 1 : f.x2, r = (10 + 40 * Mathf.Sqrt(k)) * s;
                    Glow(p, r * 1.8f, f.color, .8f * fade);
                    Ring(p, r, r, 5 * fade, A(f.color, fade));
                    for (int j = 0; j < 7; j++)
                    {
                        float ang = (f.seed * 11 + j * 51) * Mathf.Deg2Rad; Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Vector2 a = p + d * r * .7f, b = p + d * (r * .7f + 18 * fade * s);
                        Ui.Stroke(a.x, a.y, b.x, b.y, 3 * fade, A(j % 2 == 0 ? f.color : Hot, fade));
                    }
                    if (k < .3f) Ui.Disk(x, f.y, r * .5f, A(Color.white, 1 - k / .3f));
                    break;
                }
                case K.Flood:  // 开闸放水：一堵带浪头的水墙从首领脚下冲过角色，停一下再退去
                {
                    float push = 1 - Mathf.Pow(1 - Mathf.Clamp01(k / .4f), 3);                 // 前 40% 冲出去
                    float front = Mathf.Lerp(x - 40, x2 - 340, push);
                    float wf = k < .7f ? 1 : 1 - (k - .7f) / .3f;                              // 最后 30% 退去变淡
                    float sink = k < .7f ? 0 : (k - .7f) / .3f * 70;
                    float now = Time.realtimeSinceStartup;
                    // 水体：一条条竖条拼出起伏的浪面；越靠浪头越高
                    for (float sx = front; sx < x + 80; sx += 14)
                    {
                        float fromFront = (sx - front) / 260f, crest = fromFront < 1 ? Mathf.Lerp(1.45f, 1, fromFront) : 1;
                        float h = (118 + Mathf.Sin(sx * .045f + now * 7) * 10) * crest - sink;
                        if (h <= 2) continue;
                        Ui.Fill(sx, f.y - h, 15, h, A(new Color(.30f, .62f, .74f), .62f * wf));
                        Ui.Fill(sx, f.y - h * .55f, 15, h * .55f, A(new Color(.16f, .40f, .52f), .55f * wf));
                        Ui.Fill(sx, f.y - h, 15, 5, A(new Color(.78f, .95f, 1f), .8f * wf));            // 浪面高光
                        if (((int)(sx / 14) + (int)(now * 12)) % 3 == 0) Ui.Disk(sx + 7, f.y - h + 2, 7 + 4 * Mathf.Sin(sx + now * 9), A(Color.white, .75f * wf));   // 泡沫
                    }
                    // 浪头卷边：一串大小不一的白色泡沫
                    float ch = 118 * 1.45f - sink;
                    for (int j = 0; j < 9; j++)
                    {
                        float yy = f.y - ch * (j / 8f), r = 16 - j * .9f + Mathf.Sin(now * 10 + j) * 2;
                        Ui.Disk(front - 6 + Mathf.Sin(j * 1.7f + now * 8) * 6, yy, r, A(Color.white, .85f * wf));
                    }
                    // 水里的波纹
                    for (int j = 0; j < 5; j++)
                    {
                        float ry = f.y - 18 - j * 18, rx = front + 40 + ((now * 220 + j * 97) % Mathf.Max(60, x - front));
                        Ui.Stroke(rx, ry, rx + 40, ry, 2, A(new Color(.8f, .95f, 1f), .5f * wf));
                    }
                    break;
                }
                case K.Spray:  // 浪头甩出的水滴：跟着浪头位置抛出，抛物线落下
                {
                    float push = 1 - Mathf.Pow(1 - Mathf.Clamp01((k * .7f + .1f) / .4f), 3);
                    float sx0 = Mathf.Lerp(x - 40, x2 - 340, push), vx = -60 - (f.seed % 1) * 160, vy = -260 - (f.seed % 3) * 60;
                    float tt = k * f.life, px = sx0 + vx * tt, py = f.y - 150 + vy * tt + 600 * tt * tt;
                    Ui.Disk(px, py, 3 + (f.seed % 4), A(Color.white, .9f * fade));
                    break;
                }
                case K.Shield: // 护盾：角色前方一面竖向弧形护盾闪一下
                {
                    float r = 70 + 14 * k;
                    for (int j = 0; j < 10; j++)
                    {
                        float a0 = (-70 + j * 14) * Mathf.Deg2Rad, a1 = (-56 + j * 14) * Mathf.Deg2Rad;
                        Ui.Stroke(x + Mathf.Cos(a0) * r * .35f, f.y + Mathf.Sin(a0) * r, x + Mathf.Cos(a1) * r * .35f, f.y + Mathf.Sin(a1) * r, 5 * fade, A(f.color, .9f * fade));
                    }
                    Glow(p, 80, f.color, .35f * fade);
                    break;
                }
                case K.Pulse:  // 信号塔：两圈紫色信号环
                    for (int j = 0; j < 2; j++)
                    {
                        float kk = Mathf.Clamp01(k * 1.3f - j * .3f); if (kk <= 0) continue;
                        float r = 30 + CombatSession.PulseRange * .5f * kk;
                        Ring(p, r, r * .55f, 4 * (1 - kk), A(f.color, (1 - kk) * .9f));
                    }
                    break;
                case K.Sweep:  // 弧光：头部在前 0.25 秒从身后（180°）扫到身前（360°），尾巴跟着变淡；整条弧压扁贴地
                {
                    float head = Mathf.Lerp(180, 372, Mathf.Clamp01(k / .55f)), fadeAll = k < .55f ? 1 : 1 - (k - .55f) / .45f;
                    float rx = f.x2, ry = f.x2 * .28f;
                    for (int j = 0; j < 22; j++)
                    {
                        float a0 = (head - j * 7) * Mathf.Deg2Rad, a1 = (head - (j + 1) * 7) * Mathf.Deg2Rad;
                        if (head - (j + 1) * 7 < 170) break;
                        float tail = 1 - j / 22f;
                        Vector2 p0 = p + new Vector2(Mathf.Cos(a0) * rx, -Mathf.Sin(a0) * ry), p1 = p + new Vector2(Mathf.Cos(a1) * rx, -Mathf.Sin(a1) * ry);
                        Ui.Stroke(p0.x, p0.y, p1.x, p1.y, 14 * tail, A(f.color, .55f * tail * fadeAll));
                        Ui.Stroke(p0.x, p0.y, p1.x, p1.y, 4 * tail, A(Color.white, .85f * tail * fadeAll));
                    }
                    break;
                }
                case K.Target: // 落点预警：红色扁圆在地上收紧、闪烁，越临近越亮
                {
                    float r = Mathf.Lerp(150, 90, k), blink = .55f + .45f * Mathf.Sin(k * 40);
                    Ring(p, r, r * .2f, 5, A(f.color, (.4f + .6f * k) * blink));
                    Ring(p, r * .45f, r * .09f, 3, A(f.color, .8f * blink));
                    Ui.Stroke(x - r * .6f, f.y, x + r * .6f, f.y, 2, A(f.color, .5f * blink));
                    break;
                }
                case K.Crate:  // 吊箱：从上方 0.2 秒砸到地面，压住一下，再淡出
                {
                    float fall = Mathf.Clamp01(k / .155f), w = 250, h = 130;
                    float bottom = Mathf.Lerp(f.y - 760, f.y + 6, fall * fall), a = k < .75f ? 1 : 1 - (k - .75f) / .25f;
                    float x0 = x - w / 2, top = bottom - h;
                    Ui.Stroke(x, top - 400, x, top, 4, A(Ui.Ink, .7f * a));                         // 吊索
                    Ui.Fill(x0 - 4, top - 4, w + 8, h + 8, A(Ui.Ink, a));                              // 描边
                    Ui.Fill(x0, top, w, h, A(f.color, a));
                    for (int j = 1; j < 9; j++) Ui.Fill(x0 + j * w / 9 - 2, top + 8, 4, h - 16, A(new Color(.55f, .24f, .1f), a));   // 瓦楞
                    Ui.Fill(x0, top, w, 10, A(new Color(.95f, .55f, .25f), a));
                    for (int j = 0; j < 5; j++) Ui.Fill(x0 + 12 + j * 50, top + h - 22, 26, 12, A(j % 2 == 0 ? new Color(1, .82f, .1f) : Ui.Ink, a));   // 警示条
                    break;
                }
                case K.Roar:   // 首领过载：红色震波
                {
                    float r = 40 + 220 * k;
                    Ring(p, r, r * .7f, 8 * fade, A(f.color, fade));
                    Glow(p, 120, f.color, .5f * fade);
                    break;
                }
            }
        }

        // ---------- helpers ----------
        public static Color A(Color c, float a) { c.a = Mathf.Clamp01(a) * c.a; return c; }
        public static void Glow(Vector2 p, float r, Color c, float a) { QualityVisual.Halo(Ui.P(p.x, p.y), r * Ui.K, c, a); }
        public static void Ring(Vector2 c, float rx, float ry, float w, Color color)
        {
            if (w <= .2f) return;
            const int n = 28;
            for (int j = 0; j < n; j++)
            {
                float a0 = j * 2 * Mathf.PI / n, a1 = (j + 1) * 2 * Mathf.PI / n;
                Ui.Stroke(c.x + Mathf.Cos(a0) * rx, c.y + Mathf.Sin(a0) * ry, c.x + Mathf.Cos(a1) * rx, c.y + Mathf.Sin(a1) * ry, w, color);
            }
        }
    }
}
