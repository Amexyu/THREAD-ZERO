using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// 远程 / 重武器的攻击特效（逻辑坐标，跟 Ui 一样）：
    /// 霰弹 = 枪口爆焰 + 一把散开的弹丸；线圈 = 蓄能后一道电弧光束；手炮 = 发光炮弹 + 落点爆炸；重锤 = 落地冲击波。
    /// 只负责画面，伤害仍由 CombatSession 结算（远程命中在开火后约 0.12 秒，弹道时间与之对齐）。
    public static class WeaponFx
    {
        private enum K { Flash, Pellet, Beam, Shell, Blast, Spark, Smoke, Shock, Bolt, Arc, Puff, Rock, Crack, Dust, Ring, Glow, Lob, Streak }
        private sealed class Fx
        {
            public K kind; public float born, life, seed, size; public Vector2 a, b, dir; public Color color; public bool spawned;
        }
        private static readonly List<Fx> list = new List<Fx>();
        private static readonly Color Hot = new Color(1, .93f, .7f), Ember = new Color(1, .45f, .12f);
        private static readonly Color Cyan = new Color(.45f, 1, .92f), CyanDeep = new Color(.2f, .85f, .8f);
        private const float Travel = .12f;   // 与 CombatSession 远程 strikeDelay - 开火时刻 对齐

        public static void Clear() { list.Clear(); }
        private static readonly Color Stone = new Color(.4f, .38f, .37f), DustCol = new Color(.74f, .7f, .64f), Steam = new Color(.93f, .96f, .98f);

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

        // ---------- v0.20 技能专用 ----------
        /// 线圈大招开火：枪口一团光 + 冲击环 + 粗光束 + 命中点电火花。
        public static void CoilBlast(float now, Vector2 muzzle, Vector2 aim, Vector2 target)
        {
            Add(K.Glow, now, .22f, muzzle, muzzle, Cyan, 1.8f);
            Add(K.Flash, now, .14f, muzzle, muzzle + aim, Cyan, 1.9f);
            Add(K.Ring, now, .28f, muzzle, muzzle + aim, Cyan, 1.1f);
            Add(K.Ring, now + .05f, .3f, muzzle + aim * 26, muzzle + aim * 27, Cyan, .7f);
            Add(K.Beam, now, .3f, muzzle, target, Cyan, 1.8f);
            Add(K.Glow, now + .02f, .26f, target, target, Cyan, 1.6f);
            Add(K.Spark, now + .02f, .3f, target, target + Vector2.right, Cyan, 2f);
            Add(K.Bolt, now + .02f, .4f, target, target, Cyan, 1.3f);
        }
        /// 线圈蓄能（每帧直接画，不进列表）：三圈电环沿枪管往枪口推、电光往枪口收、枪口光球越来越亮。k = 蓄能进度 0..1。
        public static void DrawCharge(float now, Vector2 muzzle, Vector2 aim, float k) { DrawCharge(now, muzzle, aim, k, Cyan); }
        public static void DrawCharge(float now, Vector2 muzzle, Vector2 aim, float k, Color Cyan)
        {
            Glow(muzzle, 20 + 46 * k, Cyan, .3f + .6f * k);
            for (int i = 0; i < 3; i++)
            {
                float u = Mathf.Repeat(now * (1.6f + 2.2f * k) + i / 3f, 1);
                Vector2 c = muzzle - aim * (44 * (1 - u));
                float across = (8 + 7 * k) * (.6f + .4f * u), al = Mathf.Sin(u * Mathf.PI) * (.45f + .55f * k);
                RotRing(c, across * .32f, across, aim, 2.6f, A(Cyan, al));
                RotRing(c, across * .32f, across, aim, 1f, A(Color.white, al * .8f));
            }
            int sd = (int)(now * 28);
            var rng = new System.Random(sd);
            for (int i = 0; i < 5; i++)
            {
                float ang = (float)rng.NextDouble() * 360, r0 = 22 + 34 * (1 - k) + (float)rng.NextDouble() * 12;
                Vector2 d = Rotate(Vector2.right, ang);
                Lightning(muzzle + d * r0, muzzle + d * 6, 1.6f, A(Color.Lerp(Cyan, Color.white, .35f), .5f + .5f * k), 6, sd * 7 + i);
            }
            Ui.Disk(muzzle.x, muzzle.y, 3 + 7 * k, A(Color.white, .55f + .45f * k));
        }
        /// 连锁电弧：pts[0] 是被光束打中的那只，之后每跳晚 hop 秒，每一跳都是一道锯齿闪电 + 落点电火花。
        public static void Chain(float now, IList<Vector2> pts, float hop = .05f)
        {
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float t = now + .05f + i * hop;
                Add(K.Arc, t, .34f, pts[i], pts[i + 1], Cyan, 1);
                Add(K.Glow, t, .2f, pts[i + 1], pts[i + 1], Cyan, 1.1f);
                Add(K.Spark, t + .01f, .24f, pts[i + 1], pts[i + 1] + Vector2.right, Cyan, 1.3f);
                Add(K.Bolt, t + .01f, .36f, pts[i + 1], pts[i + 1], Cyan, 1);
            }
        }
        /// 落地扬尘：脚两边各喷一小团灰。
        public static void Land(float now, Vector2 feet)
        {
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 3; i++)
                {
                    var f = Add(K.Puff, now + i * .02f, Random.Range(.45f, .65f), feet + new Vector2(s * 10, -4), feet, DustCol, Random.Range(.7f, 1f));
                    f.dir = new Vector2(s * Random.Range(70f, 120f), Random.Range(-30f, -10f));
                }
        }
        /// 滑步：往身后留一串小灰团。
        public static void Skid(float now, Vector2 feet)
        {
            for (int i = 0; i < 5; i++)
            {
                var f = Add(K.Puff, now + i * .03f, Random.Range(.4f, .6f), feet + new Vector2(i * 9 - 10, -3), feet, DustCol, Random.Range(.6f, .9f));
                f.dir = new Vector2(Random.Range(-60f, -20f), Random.Range(-25f, -8f));
            }
        }
        /// 抛壳：一枚弹壳翻着跟头往后上方弹出去、落地。
        public static void Casing(float now, Vector2 at)
        {
            var f = Add(K.Rock, now, .6f, at, new Vector2(0, at.y + 120), new Color(.95f, .62f, .22f), .55f);
            f.dir = new Vector2(Random.Range(-140f, -90f), Random.Range(-260f, -200f));
        }
        /// 地面掀起：一柱碎石和尘往上喷（撩锤、上挑）。
        public static void Upheave(float now, Vector2 ground)
        {
            Add(K.Glow, now, .2f, ground + new Vector2(0, -20), ground, Hot, 1.8f);
            Add(K.Crack, now, .8f, ground + new Vector2(-30, 1), ground + new Vector2(40, 1), Ember, .8f);
            for (int i = 0; i < 9; i++)
            {
                var f = Add(K.Rock, now + Random.Range(0f, .05f), Random.Range(.55f, .8f), ground + new Vector2(Random.Range(-26f, 26f), -4), new Vector2(0, ground.y), Stone, Random.Range(1f, 1.5f));
                f.dir = new Vector2(Random.Range(-60f, 80f), Random.Range(-620f, -420f));
            }
            for (int i = 0; i < 5; i++)
            {
                var f = Add(K.Puff, now + i * .03f, Random.Range(.5f, .8f), ground + new Vector2(Random.Range(-20f, 20f), -10), ground, DustCol, Random.Range(1.1f, 1.6f));
                f.dir = new Vector2(Random.Range(-30f, 30f), Random.Range(-160f, -100f));
            }
        }
        /// 抛射的信号矢：高高划一道弧，拖一串火星（落点爆炸由技能命中那一下画）。
        public static void Lob(float now, Vector2 from, Vector2 to, float travel)
        {
            var f = Add(K.Lob, now, travel, from, to, new Color(1, .38f, .28f), Mathf.Max(90, Mathf.Abs(to.x - from.x) * .55f));
            Add(K.Flash, now, .1f, from, from + (to - from).normalized, new Color(1, .5f, .35f), 1.1f);
        }
        /// 平射的一发：短促的弩矢 / 钉子。
        public static void Bolt(float now, Vector2 from, Vector2 to, Color c) { Add(K.Flash, now, .08f, from, to, c, .9f); Add(K.Pellet, now, Travel, from, to, c, 1.5f); }
        /// 蒸汽：白汽往上冒（炮口拄地、过热）。
        public static void SteamPuff(float now, Vector2 at)
        {
            for (int i = 0; i < 7; i++)
            {
                var f = Add(K.Puff, now + i * .04f, Random.Range(.7f, 1f), at + Random.insideUnitCircle * 5, at, Steam, Random.Range(.8f, 1.2f));
                f.dir = Rotate(Vector2.up * -1, Random.Range(-20f, 20f)) * Random.Range(50f, 80f);
            }
        }
        /// 突刺穿过去的一道光：从 from 一下子拉到 to，再变细淡掉。
        public static void Pierce(float now, Vector2 from, Vector2 to)
        {
            Add(K.Streak, now, .3f, from, to, Hot, 1);
            Add(K.Spark, now + .02f, .2f, to, to + Vector2.right, Hot, 1.2f);
        }
        /// 信号弹炸开：一团红光 + 往四周飞再落下的火星。
        public static void FlareBurst(float now, Vector2 at, float size)
        {
            Color red = new Color(1, .36f, .26f);
            Add(K.Glow, now, .35f, at, at, red, 2.2f * size);
            Add(K.Blast, now, .45f, at, at, red, size);
            for (int i = 0; i < 10; i++)
            {
                var f = Add(K.Rock, now + Random.Range(0f, .04f), Random.Range(.5f, .8f), at, new Vector2(0, at.y + 140), new Color(1, .55f, .3f), .3f);
                f.dir = Rotate(Vector2.up * -1, Random.Range(-80f, 80f)) * Random.Range(160f, 300f);
            }
        }
        public static void Twinkle(float now, Vector2 at) { Add(K.Spark, now, .25f, at, at + Vector2.right, Color.white, 1.1f); }
        /// 打完的余电：线圈上还噼啪闪几下，几粒电火花往下掉（不往上飘，免得挡住大头的脸）。
        public static void Residue(float now, Vector2 muzzle, Vector2 aim)
        {
            Add(K.Bolt, now, .45f, muzzle, muzzle, Cyan, .45f);
            Add(K.Bolt, now + .12f, .4f, muzzle - aim * 22, muzzle - aim * 22, Cyan, .4f);
            for (int i = 0; i < 4; i++)
            {
                var f = Add(K.Rock, now + i * .05f, Random.Range(.35f, .5f), muzzle - aim * Random.Range(0f, 26f), new Vector2(0, muzzle.y + 160), Cyan, .25f);
                f.dir = new Vector2(Random.Range(-40f, 60f), Random.Range(-120f, -40f));
            }
        }
        /// 震地命中：原来的冲击波 + 地面一闪 + 往前裂开的地缝 + 飞起来又落下的碎石 + 贴地滚过去的尘浪。
        public static void QuakeHit(float now, Vector2 ground)
        {
            Slam(now, ground);
            Add(K.Glow, now, .24f, ground + new Vector2(0, -14), ground, Hot, 2.4f);
            Add(K.Crack, now, 1.1f, ground + new Vector2(4, 1), ground + new Vector2(330, 1), Ember, 1);
            Add(K.Crack, now + .03f, .9f, ground + new Vector2(-4, 2), ground + new Vector2(-120, 2), Ember, .7f);
            for (int i = 0; i < 8; i++)
            {
                var f = Add(K.Rock, now + Random.Range(0f, .05f), Random.Range(.6f, .85f), ground + new Vector2(Random.Range(-30f, 50f), -4), new Vector2(0, ground.y), Stone, Random.Range(1f, 1.6f));
                f.dir = new Vector2(Random.Range(-90f, 300f), Random.Range(-470f, -250f));
            }
            Add(K.Dust, now + .02f, .75f, ground + new Vector2(14, 0), ground + new Vector2(360, 0), DustCol, 1);
            Add(K.Dust, now + .02f, .6f, ground + new Vector2(-14, 0), ground + new Vector2(-130, 0), DustCol, .75f);
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
                    float w = (k < .15f ? k / .15f : 1 - (k - .15f) / .85f) * f.size;
                    Ui.Stroke(f.a.x, f.a.y, f.b.x, f.b.y, 22 * w, A(f.color, .25f * w));
                    Ui.Stroke(f.a.x, f.a.y, f.b.x, f.b.y, 9 * w, A(f.color, .9f * w));
                    Ui.Stroke(f.a.x, f.a.y, f.b.x, f.b.y, 3 * w, A(Color.white, Mathf.Min(1, w)));
                    Lightning(f.a, f.b, 2, A(Color.white, .9f * w), 14, (int)(now * 30));
                    Lightning(f.a, f.b, 2, A(f.color, .8f * w), 18, (int)(now * 30) + 7);
                    break;
                }
                case K.Bolt:   // 命中点残留的电弧
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float ang = (f.seed * 40 + j * 120 + now * 400) * Mathf.Deg2Rad;
                        Vector2 e = f.a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (30 + 20 * k) * f.size;
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
                case K.Lob:    // 抛物线弹道 + 一串火星拖尾
                {
                    for (int j = 6; j >= 0; j--)
                    {
                        float kk = Mathf.Max(0, k - j * .035f);
                        Vector2 q = Vector2.Lerp(f.a, f.b, kk) + new Vector2(0, -f.size * 4 * kk * (1 - kk));
                        float r = j == 0 ? 6 : 5 - j * .6f;
                        Ui.Disk(q.x, q.y, r, A(j == 0 ? Color.white : f.color, j == 0 ? 1 : .7f - j * .09f));
                    }
                    Vector2 p = Vector2.Lerp(f.a, f.b, k) + new Vector2(0, -f.size * 4 * k * (1 - k));
                    Glow(p, 30, f.color, .8f);
                    break;
                }
                case K.Streak: // 一道拉出去的光：前 20% 从 a 拉到 b，之后变细淡掉
                {
                    float grow = Mathf.Clamp01(k / .2f), w = k < .2f ? 1 : 1 - (k - .2f) / .8f;
                    Vector2 e = Vector2.Lerp(f.a, f.b, 1 - (1 - grow) * (1 - grow));
                    Ui.Stroke(f.a.x, f.a.y, e.x, e.y, 18 * w, A(f.color, .22f * w));
                    Ui.Stroke(f.a.x, f.a.y, e.x, e.y, 7 * w, A(f.color, .85f * w));
                    Ui.Stroke(f.a.x, f.a.y, e.x, e.y, 2.5f * w, A(Color.white, w));
                    break;
                }
                case K.Glow:
                {
                    float r = (34 + 26 * k) * f.size;
                    Glow(f.a, r * 1.4f, f.color, .95f * fade * fade);
                    if (k < .3f) Ui.Disk(f.a.x, f.a.y, r * .35f * (1 - k / .3f), A(Color.white, 1 - k / .3f));
                    break;
                }
                case K.Ring:   // 跟枪管垂直的一圈冲击环，往外扩
                {
                    float r = (10 + 46 * Mathf.Sqrt(k)) * f.size;
                    RotRing(f.a, r * .32f, r, f.dir, 5 * fade * f.size, A(f.color, fade));
                    RotRing(f.a, r * .24f, r * .78f, f.dir, 2 * fade, A(Color.white, fade * .8f));
                    break;
                }
                case K.Arc:    // 连锁电弧：一道锯齿闪电，前 40% 最亮、每 0.04 秒换一次形状
                {
                    float w = k < .4f ? 1 : 1 - (k - .4f) / .6f;
                    int sd = (int)(f.seed * 10) + (int)(now * 25);
                    float reach = Mathf.Clamp01(k / .12f);   // 电弧从起点“跳”到落点
                    Vector2 c = (f.a + f.b) * .5f + new Vector2(0, -Mathf.Max(64, Mathf.Abs(f.b.x - f.a.x) * .4f));
                    Vector2 end = Bez(f.a, c, f.b, reach);
                    ArcCurve(f.a, c, f.b, reach, 7 * w, A(f.color, .4f * w), 9, sd);
                    ArcCurve(f.a, c, f.b, reach, 3.4f * w, A(f.color, w), 9, sd);
                    ArcCurve(f.a, c, f.b, reach, 1.4f * w, A(Color.white, w), 9, sd);
                    Glow(end, 26 * w + 8, f.color, .6f * w);
                    if (reach >= 1)
                    {
                        Vector2 mid = Bez(f.a, c, f.b, .5f);
                        Lightning(mid, mid + Rotate(Vector2.up, 150 + f.seed % 60) * 30, 1.6f * w, A(f.color, .8f * w), 8, sd + 3);
                    }
                    break;
                }
                case K.Puff:   // 白汽：减速飘走、变大、淡掉
                {
                    float travel = 1 - Mathf.Pow(1 - k, 2);
                    Vector2 p = f.a + f.dir * (f.life * travel * .9f);
                    float r = (5 + 15 * Mathf.Sqrt(k)) * f.size, al = .5f * fade * Mathf.Min(1, k * 8);
                    Ui.Disk(p.x, p.y, r, A(f.color, al));
                    Ui.Disk(p.x - r * .25f, p.y - r * .25f, r * .55f, A(Color.white, al * .7f));
                    break;
                }
                case K.Rock:   // 碎石：抛出去、带重力、落地停住
                {
                    float t = k * f.life, g = 1250;
                    Vector2 p = f.a + f.dir * t + new Vector2(0, .5f * g * t * t);
                    bool landed = p.y > f.b.y;
                    if (landed) p.y = f.b.y;
                    if (f.size < .5f)   // 小号 = 电火花：亮点 + 一小段拖尾，不描黑边
                    {
                        float sa = 1 - k; Vector2 tail = p - (f.dir + new Vector2(0, g * t)).normalized * 7;
                        Ui.Stroke(tail.x, tail.y, p.x, p.y, 2.2f, A(f.color, sa)); Ui.Disk(p.x, p.y, 2f, A(Color.white, sa));
                        break;
                    }
                    float ang = f.seed * 20 + (landed ? 0 : t * 540 * (f.seed % 2 > 1 ? 1 : -1));
                    Vector2 d = Rotate(Vector2.right, ang), n = new Vector2(-d.y, d.x);
                    float s = 5.5f * f.size, al = k > .75f ? (1 - k) / .25f : 1;
                    Ui.Stroke(p.x - d.x * s, p.y - d.y * s, p.x + d.x * s, p.y + d.y * s, s * 1.5f + 3, A(Ui.Ink, al));
                    Ui.Stroke(p.x - n.x * s * .6f, p.y - n.y * s * .6f, p.x + n.x * s * .6f, p.y + n.y * s * .6f, s * 1.2f + 3, A(Ui.Ink, al));
                    Ui.Stroke(p.x - d.x * s, p.y - d.y * s, p.x + d.x * s, p.y + d.y * s, s * 1.5f, A(f.color, al));
                    Ui.Stroke(p.x - n.x * s * .6f, p.y - n.y * s * .6f, p.x + n.x * s * .6f, p.y + n.y * s * .6f, s * 1.2f, A(f.color, al));
                    Ui.Stroke(p.x - d.x * s * .6f, p.y - d.y * s * .6f - s * .3f, p.x + d.x * s * .2f, p.y + d.y * s * .2f - s * .3f, s * .45f, A(new Color(.75f, .72f, .68f), .7f * al));
                    break;
                }
                case K.Crack:  // 地缝：一路往前裂开，刚裂开时缝里透出火光，之后变成黑缝慢慢淡掉
                {
                    float grow = Mathf.Clamp01(k / .16f); grow = 1 - (1 - grow) * (1 - grow);
                    float hot = Mathf.Clamp01(1 - k / .35f), al = k > .6f ? (1 - k) / .4f : 1;
                    float len = (f.b.x - f.a.x) * grow, sgn = Mathf.Sign(f.b.x - f.a.x);
                    var rng = new System.Random((int)(f.seed * 1000));
                    Vector2 prev = f.a; int seg = Mathf.Max(2, (int)(Mathf.Abs(len) / 16));
                    for (int j = 1; j <= seg; j++)
                    {
                        float x = f.a.x + len * j / seg, w = (4.5f - 3 * j / (float)seg) * f.size;
                        Vector2 q = new Vector2(x, f.a.y + (float)(rng.NextDouble() * 2 - 1) * 3.5f);
                        Ui.Stroke(prev.x, prev.y, q.x, q.y, w + 2, A(Ui.Ink, .85f * al));
                        if (hot > 0) Ui.Stroke(prev.x, prev.y, q.x, q.y, w * .7f, A(Color.Lerp(f.color, Hot, .5f), hot));
                        if (j % 4 == 2)
                        {
                            Vector2 br = q + new Vector2(sgn * 14, (rng.NextDouble() < .5 ? -1 : 1) * 6);
                            Ui.Stroke(q.x, q.y, br.x, br.y, 2, A(Ui.Ink, .7f * al));
                        }
                        prev = q;
                    }
                    if (hot > 0) Glow(Vector2.Lerp(f.a, new Vector2(f.a.x + len, f.a.y), .4f), Mathf.Abs(len) * .5f + 20, f.color, .5f * hot);
                    break;
                }
                case K.Dust:   // 尘浪：一排灰团贴着地面往前滚，最前面的最大
                {
                    float e = 1 - (1 - k) * (1 - k);
                    float front = f.a.x + (f.b.x - f.a.x) * e, back = f.a.x + (f.b.x - f.a.x) * e * .35f;
                    for (int j = 0; j < 7; j++)
                    {
                        float u = j / 6f, x = Mathf.Lerp(back, front, u);
                        float r = (8 + 20 * u) * f.size * (.7f + .6f * Mathf.Sqrt(k)), al = (.18f + .3f * u) * fade;
                        Ui.Disk(x, f.a.y - r * .55f, r, A(f.color, al));
                        Ui.Disk(x - r * .2f, f.a.y - r * .75f, r * .5f, A(Color.white, al * .45f));
                    }
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
        private static Vector2 Bez(Vector2 a, Vector2 c, Vector2 b, float t) { float u = 1 - t; return u * u * a + 2 * u * t * c + t * t * b; }
        /// 沿二次曲线画锯齿电弧（只画到 upTo 为止）。
        private static void ArcCurve(Vector2 a, Vector2 c, Vector2 b, float upTo, float w, Color color, float amp, int seed)
        {
            if (w <= .2f) return;
            var rng = new System.Random(seed);
            const int seg = 14;
            Vector2 prev = a;
            for (int j = 1; j <= seg; j++)
            {
                float t = j / (float)seg; if (t > upTo + .0001f) break;
                Vector2 p = Bez(a, c, b, t), tan = Bez(a, c, b, Mathf.Min(1, t + .02f)) - Bez(a, c, b, Mathf.Max(0, t - .02f));
                Vector2 n = new Vector2(-tan.y, tan.x).normalized;
                if (j < seg) p += n * (float)(rng.NextDouble() * 2 - 1) * amp;
                Ui.Stroke(prev.x, prev.y, p.x, p.y, w, color);
                prev = p;
            }
        }
        /// 斜着的椭圆环：along 是沿 dir 方向的半轴，across 是垂直方向的半轴。
        private static void RotRing(Vector2 c, float along, float across, Vector2 dir, float w, Color color)
        {
            if (w <= .2f) return;
            Vector2 n = new Vector2(-dir.y, dir.x);
            const int seg = 24;
            Vector2 prev = c + dir * along;
            for (int j = 1; j <= seg; j++)
            {
                float a = j * 2 * Mathf.PI / seg;
                Vector2 p = c + dir * (Mathf.Cos(a) * along) + n * (Mathf.Sin(a) * across);
                Ui.Stroke(prev.x, prev.y, p.x, p.y, w, color);
                prev = p;
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
