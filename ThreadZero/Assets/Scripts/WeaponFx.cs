using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// 远程 / 重武器的攻击特效（逻辑坐标，跟 Ui 一样）：
    /// 霰弹 = 枪口爆焰 + 一把散开的弹丸；线圈 = 蓄能后一道电弧光束；手炮 = 发光炮弹 + 落点爆炸；重锤 = 落地冲击波。
    /// 只负责画面，伤害仍由 CombatSession 结算（远程命中在开火后约 0.12 秒，弹道时间与之对齐）。
    public static class WeaponFx
    {
        private enum K { Flash, Pellet, Beam, Shell, Blast, Spark, Smoke, Shock, Bolt }
        private sealed class Fx
        {
            public K kind; public float born, life, seed, size; public Vector2 a, b, dir; public Color color; public bool spawned;
        }
        private static readonly List<Fx> list = new List<Fx>();
        private static readonly Color Hot = new Color(1, .93f, .7f), Ember = new Color(1, .45f, .12f);
        private static readonly Color Cyan = new Color(.45f, 1, .92f), CyanDeep = new Color(.2f, .85f, .8f);
        private const float Travel = .12f;   // 与 CombatSession 远程 strikeDelay - 开火时刻 对齐

        public static void Clear() { list.Clear(); }

        private static Fx Add(K kind, float now, float life, Vector2 a, Vector2 b, Color c, float size = 1)
        {
            var f = new Fx { kind = kind, born = now, life = life, a = a, b = b, color = c, size = size, seed = Random.value * 100 };
            Vector2 d = b - a; f.dir = d.sqrMagnitude > .01f ? d.normalized : Vector2.right;
            list.Add(f); return f;
        }

        /// 开火：kind = LayeredRigView.Kind（shotgun / coil / cannon / gun）。
        public static void Fire(string kind, float now, Vector2 muzzle, Vector2 aim, Vector2 target)
        {
            if (kind == "coil")
            {
                Add(K.Flash, now, .1f, muzzle, muzzle + aim, Cyan, 1.1f);
                Add(K.Beam, now, .24f, muzzle, target, Cyan);
                Add(K.Spark, now + .02f, .26f, target, target + Vector2.right, Cyan, 1.4f);
                Add(K.Bolt, now + .02f, .3f, target, target, Cyan);
            }
            else if (kind == "cannon")
            {
                Add(K.Flash, now, .12f, muzzle, muzzle + aim, Ember, 1.6f);
                for (int i = 0; i < 3; i++) Add(K.Smoke, now + i * .03f, .7f, muzzle + Random.insideUnitCircle * 8, muzzle, new Color(.55f, .58f, .6f), 1.3f);
                Add(K.Shell, now, Travel, muzzle, target, CyanDeep);
            }
            else if (kind == "shotgun")
            {
                Add(K.Flash, now, .09f, muzzle, muzzle + aim, Hot, 1.4f);
                for (int i = 0; i < 2; i++) Add(K.Smoke, now + .02f, .55f, muzzle + aim * 10 + Random.insideUnitCircle * 6, muzzle, new Color(.6f, .62f, .64f), .9f);
                float dist = Mathf.Max(80, (target - muzzle).magnitude);
                Vector2 baseDir = (target - muzzle).normalized;
                for (int i = 0; i < 7; i++)
                {
                    float ang = Mathf.Lerp(-9, 9, i / 6f) + Random.Range(-2f, 2f);
                    Vector2 d = Rotate(baseDir, ang);
                    Vector2 end = muzzle + d * (dist + Random.Range(-30f, 50f));
                    Add(K.Pellet, now + Random.Range(0, .015f), Travel * Random.Range(.8f, 1.05f), muzzle + d * 12, end, Hot);
                }
            }
            else
            {
                Add(K.Flash, now, .08f, muzzle, muzzle + aim, Hot, 1);
                Add(K.Pellet, now, Travel, muzzle, target, Hot, 1.3f);
            }
        }

        /// 重武器落地：在锤头着地点出冲击波。
        public static void Slam(float now, Vector2 ground)
        {
            Add(K.Shock, now, .5f, ground, ground, new Color(1, .72f, .35f));
            Add(K.Flash, now, .1f, ground + new Vector2(0, -10), ground + new Vector2(0, -20), Hot, 1.6f);
            for (int i = 0; i < 4; i++) Add(K.Smoke, now + i * .02f, .6f, ground + new Vector2(Random.Range(-60f, 60f), -6), ground, new Color(.66f, .64f, .6f), 1.2f);
        }

        public static void Draw(float now)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Fx f = list[i];
                float age = now - f.born;
                if (age < 0) continue;
                float k = age / f.life;
                if (k >= 1)
                {
                    if (!f.spawned) { f.spawned = true; OnEnd(f, now); }
                    list.RemoveAt(i); continue;
                }
                Paint(f, k, now);
            }
        }

        private static void OnEnd(Fx f, float now)
        {
            if (f.kind == K.Pellet) Add(K.Spark, now, .16f, f.b, f.b + f.dir, f.color, .7f);
            else if (f.kind == K.Shell)
            {
                Add(K.Blast, now, .45f, f.b, f.b, CyanDeep, 1);
                for (int i = 0; i < 3; i++) Add(K.Smoke, now + .05f + i * .04f, .8f, f.b + Random.insideUnitCircle * 26, f.b, new Color(.5f, .55f, .55f), 1.6f);
            }
        }

        private static void Paint(Fx f, float k, float now)
        {
            float fade = 1 - k;
            switch (f.kind)
            {
                case K.Flash:
                {
                    float r = 26 * f.size * (1 - k * .4f);
                    Glow(f.a, r * 1.6f, f.color, .8f * fade);
                    Ui.Disk(f.a.x, f.a.y, r * .45f, A(Color.white, fade));
                    for (int j = -1; j <= 1; j++)
                    {
                        Vector2 d = Rotate(f.dir, j * 22);
                        float len = (j == 0 ? 42 : 24) * f.size * (1 - k * .3f);
                        Ui.Stroke(f.a.x, f.a.y, f.a.x + d.x * len, f.a.y + d.y * len, (j == 0 ? 7 : 4) * f.size * fade, A(f.color, fade));
                    }
                    break;
                }
                case K.Pellet:
                {
                    Vector2 p = Vector2.Lerp(f.a, f.b, k), q = p - f.dir * 30 * f.size;
                    Ui.Stroke(q.x, q.y, p.x, p.y, 3 * f.size, A(Ember, .7f));
                    Ui.Stroke(Vector2.Lerp(q, p, .5f).x, Vector2.Lerp(q, p, .5f).y, p.x, p.y, 2 * f.size, A(f.color, 1));
                    break;
                }
                case K.Beam:
                {
                    float w = k < .15f ? k / .15f : 1 - (k - .15f) / .85f;
                    Ui.Stroke(f.a.x, f.a.y, f.b.x, f.b.y, 22 * w, A(f.color, .25f * w));
                    Ui.Stroke(f.a.x, f.a.y, f.b.x, f.b.y, 9 * w, A(f.color, .9f * w));
                    Ui.Stroke(f.a.x, f.a.y, f.b.x, f.b.y, 3 * w, A(Color.white, w));
                    Lightning(f.a, f.b, 2, A(Color.white, .9f * w), 14, (int)(now * 30));
                    Lightning(f.a, f.b, 2, A(f.color, .8f * w), 18, (int)(now * 30) + 7);
                    break;
                }
                case K.Bolt:   // 命中点残留的电弧
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float ang = (f.seed * 40 + j * 120 + now * 400) * Mathf.Deg2Rad;
                        Vector2 e = f.a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (30 + 20 * k);
                        Lightning(f.a, e, 2, A(f.color, fade), 8, (int)(now * 24) + j);
                    }
                    break;
                }
                case K.Shell:
                {
                    Vector2 p = Vector2.Lerp(f.a, f.b, k) + new Vector2(0, -36 * Mathf.Sin(Mathf.PI * k));
                    for (int j = 1; j <= 5; j++)
                    {
                        float kk = Mathf.Max(0, k - j * .05f);
                        Vector2 q = Vector2.Lerp(f.a, f.b, kk) + new Vector2(0, -36 * Mathf.Sin(Mathf.PI * kk));
                        Ui.Disk(q.x, q.y, 7 - j, A(f.color, .5f - j * .08f));
                    }
                    Glow(p, 26, f.color, .6f);
                    Ui.Disk(p.x, p.y, 9, Ui.Ink);
                    Ui.Disk(p.x, p.y, 6.5f, f.color);
                    Ui.Disk(p.x - 2, p.y - 2, 2.5f, Color.white);
                    break;
                }
                case K.Blast:
                {
                    float r = 18 + 78 * Mathf.Sqrt(k);
                    Glow(f.a, r * 1.8f, Ember, .9f * fade);
                    if (k < .35f) Ui.Disk(f.a.x, f.a.y, r * .8f, A(Hot, 1 - k / .35f));
                    Ring(f.a, r, r * .8f, 6 * fade, A(f.color, fade));
                    Ring(f.a, r * .7f, r * .55f, 3 * fade, A(Color.white, fade * .8f));
                    for (int j = 0; j < 9; j++)
                    {
                        float ang = (f.seed * 13 + j * 40) * Mathf.Deg2Rad;
                        Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * .8f);
                        Vector2 a = f.a + d * r * .6f, b = f.a + d * (r * .6f + 26 * fade);
                        Ui.Stroke(a.x, a.y, b.x, b.y, 4 * fade, A(j % 2 == 0 ? Ember : Hot, fade));
                    }
                    break;
                }
                case K.Spark:
                {
                    for (int j = 0; j < 6; j++)
                    {
                        float ang = (f.seed * 17 + j * 60) * Mathf.Deg2Rad;
                        Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float r0 = 4 + 14 * k * f.size, r1 = r0 + 12 * f.size * fade;
                        Ui.Stroke(f.a.x + d.x * r0, f.a.y + d.y * r0, f.a.x + d.x * r1, f.a.y + d.y * r1, 3 * fade, A(f.color, fade));
                    }
                    Ui.Disk(f.a.x, f.a.y, 5 * f.size * fade, A(Color.white, fade));
                    break;
                }
                case K.Smoke:
                {
                    Vector2 p = f.a + new Vector2(-10 * k, -30 * k);
                    Ui.Disk(p.x, p.y, (8 + 14 * k) * f.size, A(f.color, .35f * fade));
                    break;
                }
                case K.Shock:
                {
                    float rx = 30 + 170 * Mathf.Sqrt(k);
                    Ring(f.a, rx, rx * .16f, 5 * fade, A(f.color, fade));
                    Ring(f.a, rx * .65f, rx * .1f, 3 * fade, A(Color.white, fade * .8f));
                    for (int j = 0; j < 5; j++)   // 地面裂纹
                    {
                        float x = (j - 2) * 26 + (f.seed % 7);
                        Ui.Stroke(f.a.x + x, f.a.y, f.a.x + x * 1.8f + 10, f.a.y + 2, 3, A(Ui.Ink, .6f * fade));
                    }
                    break;
                }
            }
        }

        // ---------- helpers ----------
        private static Color A(Color c, float a) { c.a = Mathf.Clamp01(a) * c.a; return c; }
        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
        private static void Glow(Vector2 p, float r, Color c, float a)
        {
            QualityVisual.Halo(Ui.P(p.x, p.y), r * Ui.K, c, a);
        }
        private static void Ring(Vector2 c, float rx, float ry, float w, Color color)
        {
            if (w <= .2f) return;
            const int n = 28;
            for (int j = 0; j < n; j++)
            {
                float a0 = j * 2 * Mathf.PI / n, a1 = (j + 1) * 2 * Mathf.PI / n;
                Ui.Stroke(c.x + Mathf.Cos(a0) * rx, c.y + Mathf.Sin(a0) * ry, c.x + Mathf.Cos(a1) * rx, c.y + Mathf.Sin(a1) * ry, w, color);
            }
        }
        private static void Lightning(Vector2 a, Vector2 b, float w, Color color, float amp, int seed)
        {
            var rng = new System.Random(seed);
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized;
            int seg = Mathf.Clamp((int)(d.magnitude / 26), 3, 16);
            Vector2 prev = a;
            for (int j = 1; j <= seg; j++)
            {
                float t = j / (float)seg;
                Vector2 p = a + d * t + (j == seg ? Vector2.zero : n * (float)(rng.NextDouble() * 2 - 1) * amp);
                Ui.Stroke(prev.x, prev.y, p.x, p.y, w, color);
                prev = p;
            }
        }
    }
}
