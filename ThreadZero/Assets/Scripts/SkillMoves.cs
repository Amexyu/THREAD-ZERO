using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ThreadZero
{
    /// v0.20 技能连招的动作表：按“拍子”写整套姿势，比一根一根写 Kf 数组好改得多。
    ///
    /// 每行 = 一拍：时间点（0..1，占整个技能时长的比例）+ 若干「参数 值」。这一拍没写的参数保持上一拍的值不动。
    /// 行里可以加：
    ///   ~lin 匀速 / ~in 慢起 / ~out 快起慢停 / ~snap 到点直接跳（不写 = smoothstep），作用于“上一拍 → 这一拍”这一段；
    ///   !事件名  走到这一拍时触发一次画面事件（落地扬尘、抛壳、余电……，由 GameAppAction.MoveEvent 处理）。
    /// 参数：dx dy（右、下为正）sq（压扁，sx 自动 = 2 - sq）sx sy tilt（绕脚转）spin（绕腰转，空翻用）lean head pack
    ///       A Af（近 / 远手臂：0 垂下、90 平举、180 举过头）W（武器绝对角度，规则同上，可以转很多圈）shx L Lf（腿）
    ///       face（1 朝右、-1 转身朝左，中间经过 0 = 纸片转身）wx wy（武器离手的偏移，抛起来再接住）
    ///       zoom（镜头推近倍数）charge（蓄能光 0..1）shake（枪身抖动 0..1）fx ghost glint（0/1 开关：刃光拖尾 / 残影 / 刃尖一闪）
    public static partial class LayeredRigView
    {
        private sealed class Track { public string name; public float[] t, v; public int[] ease; }
        private sealed class Move { public Track[] tracks; public bool hasSx; public float[] evT; public string[] evName; }
        private const int EaseSmooth = 0, EaseLin = 1, EaseIn = 2, EaseOut = 3, EaseSnap = 4;
        private static Dictionary<Anim, Move> moves;

        private static Move MoveFor(Anim a)
        {
            if (moves == null)
            {
                moves = new Dictionary<Anim, Move>();
                foreach (var kv in MoveSource) moves[kv.Key] = Parse(kv.Value);
            }
            Move m; return moves.TryGetValue(a, out m) ? m : null;
        }
        public static bool HasMove(Anim a) { return MoveFor(a) != null; }

        private static float Num(string s) { return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
        private static Move Parse(string src)
        {
            var times = new List<float>(); var eases = new List<int>(); var rows = new List<Dictionary<string, float>>();
            var evT = new List<float>(); var evN = new List<string>();
            // Windows 上 git 会把换行签出成 \r\n：这几张表是写在 .cs 里的多行字符串，多出来的 \r 会让空行变成 "\r"，
            // 解析数字时 FormatException（行尾的 ~lin、!事件名 也会带上 \r 认不出来）。所以先把 \r 去掉。
            foreach (string raw in src.Replace("\r", "").Split('\n'))
            {
                string line = raw; int c = line.IndexOf("//", StringComparison.Ordinal); if (c >= 0) line = line.Substring(0, c);
                string[] tok = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length == 0) continue;
                float t = Num(tok[0]); int ease = EaseSmooth; var vals = new Dictionary<string, float>();
                for (int i = 1; i < tok.Length; i++)
                {
                    string k = tok[i];
                    if (k[0] == '~') { ease = k == "~lin" ? EaseLin : k == "~in" ? EaseIn : k == "~out" ? EaseOut : k == "~snap" ? EaseSnap : EaseSmooth; continue; }
                    if (k[0] == '!') { evT.Add(t); evN.Add(k.Substring(1)); continue; }
                    if (i + 1 >= tok.Length) break;
                    vals[k == "sq" ? "sy" : k] = Num(tok[++i]);
                }
                times.Add(t); eases.Add(ease); rows.Add(vals);
            }
            var names = new List<string>();
            foreach (var r in rows) foreach (string n in r.Keys) if (!names.Contains(n)) names.Add(n);
            // 每一拍都是一整套姿势：这一拍没写的参数 = 上一拍的值（第一次出现之前用第一次写的值）。
            // 所以“从上一拍过渡到这一拍”只动这一拍写了的参数，别的参数原地不动，不会从很早的一拍慢慢漂过来。
            var tracks = new List<Track>();
            foreach (string n in names)
            {
                float first = 0; for (int i = 0; i < rows.Count; i++) if (rows[i].ContainsKey(n)) { first = rows[i][n]; break; }
                float last = rows.Count > 0 && rows[0].ContainsKey(n) ? first : Neutral(n, first);   // 第一拍没写的：用中性值（不抛枪、不翻、不残影…）
                var vv = new float[rows.Count];
                for (int i = 0; i < rows.Count; i++) { if (rows[i].ContainsKey(n)) last = rows[i][n]; vv[i] = last; }
                tracks.Add(new Track { name = n, t = times.ToArray(), v = vv, ease = eases.ToArray() });
            }
            return new Move { tracks = tracks.ToArray(), hasSx = names.Contains("sx"), evT = evT.ToArray(), evName = evN.ToArray() };
        }
        /// 自检（冒烟测试调用）：每张动作表把换行换成 Windows 的 \r\n 再解析一遍，和原样解析的结果逐项比较。返回空字符串 = 全部一致。
        public static string CheckMoveLineEndings()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in MoveSource)
            {
                Move a, b;
                try { a = Parse(kv.Value); b = Parse(kv.Value.Replace("\r", "").Replace("\n", "\r\n")); }
                catch (Exception e) { sb.Append(kv.Key).Append("：").Append(e.GetType().Name).Append("；"); continue; }
                if (!SameMove(a, b)) sb.Append(kv.Key).Append(" 解析结果不一样；");
            }
            return sb.ToString();
        }
        private static bool SameMove(Move a, Move b)
        {
            if (a.tracks.Length != b.tracks.Length || a.evName.Length != b.evName.Length || a.hasSx != b.hasSx) return false;
            for (int i = 0; i < a.evName.Length; i++) if (a.evName[i] != b.evName[i] || a.evT[i] != b.evT[i]) return false;
            for (int i = 0; i < a.tracks.Length; i++)
            {
                Track x = a.tracks[i], y = b.tracks[i];
                if (x.name != y.name || x.v.Length != y.v.Length) return false;
                for (int j = 0; j < x.v.Length; j++) if (x.v[j] != y.v[j] || x.t[j] != y.t[j] || x.ease[j] != y.ease[j]) return false;
            }
            return true;
        }
        private static float Neutral(string n, float fallback)
        {
            switch (n)
            {
                case "fx": case "ghost": case "glint": case "wx": case "wy": case "spin": case "tilt": case "charge": case "shake":
                case "dx": case "dy": case "head": case "pack": case "L": case "Lf": case "shx": return 0;
                case "face": case "zoom": case "sx": case "sy": case "lean": return 1;
                default: return fallback;
            }
        }
        private static float Eval(Track k, float t)
        {
            float[] T = k.t, V = k.v; int n = T.Length;
            if (t <= T[0]) return V[0];
            for (int i = 1; i < n; i++)
                if (t <= T[i])
                {
                    float u = (t - T[i - 1]) / Mathf.Max(1e-5f, T[i] - T[i - 1]);
                    switch (k.ease[i])
                    {
                        case EaseLin: break;
                        case EaseIn: u = u * u; break;
                        case EaseOut: u = 1 - (1 - u) * (1 - u); break;
                        case EaseSnap: u = u >= 1 ? 1 : 0; break;
                        default: u = u * u * (3 - 2 * u); break;
                    }
                    return V[i - 1] + (V[i] - V[i - 1]) * u;
                }
            return V[n - 1];
        }
        private static float Step(Track k, float t)
        {
            float v = k.v[0];
            for (int i = 0; i < k.t.Length; i++) if (t >= k.t[i]) v = k.v[i];
            return v;
        }
        private static void Apply(Move m, ref Pose p, float t)
        {
            foreach (Track k in m.tracks)
            {
                switch (k.name)
                {
                    case "fx": p.fx = Step(k, t) > .5f; continue;
                    case "ghost": p.ghost = Step(k, t) > .5f; continue;
                    case "glint": p.glint = Step(k, t) > .5f; continue;
                }
                float v = Eval(k, t);
                switch (k.name)
                {
                    case "dx": p.dx = v; break; case "dy": p.dy = v; break; case "sx": p.sx = v; break; case "sy": p.sy = v; break;
                    case "tilt": p.tilt = v; break; case "spin": p.spin = v; break; case "lean": p.lean = v; break;
                    case "head": p.head = v; break; case "pack": p.pack = v; break;
                    case "A": p.A = v; break; case "Af": p.Af = v; break; case "W": p.W = v; break; case "shx": p.shx = v; break;
                    case "L": p.L = v; break; case "Lf": p.Lf = v; break;
                    case "face": p.face = v; break; case "wx": p.wx = v; break; case "wy": p.wy = v; break;
                    case "zoom": p.zoom = v; break; case "charge": p.charge = v; break; case "shake": p.shake = v; break;
                    case "reach": p.reach = v; break; case "hook": p.hook = v; p.hookOn = true; break;
                }
            }
            if (!m.hasSx) p.sx = 2 - p.sy;
            if (p.shake > 0) { p.W += Mathf.Sin(t * 260) * 2.6f * p.shake; p.dx += Mathf.Sin(t * 330) * 1.3f * p.shake; p.tilt += Mathf.Sin(t * 210) * .8f * p.shake; }
        }
        /// 动作从 prevT 走到 t 之间经过的事件（GameAppAction 在 Repaint 里调，按名字放特效）。
        public static void MoveEvents(Anim a, float prevT, float t, List<string> into)
        {
            Move m = MoveFor(a); if (m == null) return;
            for (int i = 0; i < m.evT.Length; i++) if (prevT < m.evT[i] && t >= m.evT[i]) into.Add(m.evName[i]);
        }
        /// 当前这一帧技能动作要的镜头推近 / 蓄能光（Draw 时更新；没在放技能就是 1 / 0）。
        public static float SkillZoom = 1, SkillCharge;
        public static Vector2 RootPos;

        // ---------------------------------------------------------------------------------------------
        // 动作表。命中拍（SkillBook 里 hitTimes = 拍子 × 时长）用 ★ 标出来，改拍子要两边一起改。
        private static readonly Dictionary<Anim, string> MoveSource = new Dictionary<Anim, string>
        {
            // 连锁放电（线圈）1.3 秒：转枪接住 → 扎马步蓄能 → ★.30 开火被顶退 → 原地转身一圈接弓步 → ★.64 补一枪 → ★.76 跳射收尾 → 转枪收回
            [Anim.ChainArc] = @"
0    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 face 1 zoom 1 charge 0 shake 0
.07  A 64 W 290 Af 24 dy -14 dx -4 sq 1.06 lean -2 head -2 ~out          // 把线圈往上一抛、在手里转
.14  A 88 W 455 Af 74 dy 0 dx -8 sq .88 lean 8 L 22 Lf -20 head -4 pack 4 !land   // 接住端平，落成马步
.22  charge .5 zoom 1.04 shake .4 sq .9
.29  charge 1 zoom 1.07 shake 1 sq .87 lean 10 head -6
.30  ~snap shake 0
.33  A 106 W 480 dx -34 dy -5 sq 1.05 lean -11 tilt -7 head 8 pack -8 charge 0 zoom 1 ghost 1 ~out   // ★ 开火：后坐把人顶出去
.43  A 92 W 458 dx -26 dy 0 sq .94 lean 2 tilt -1 head 0 pack 2 ghost 0 !skid
.49  face 1 dx -20 L 10 Lf -8 ~in
.545 face -1 dx -10 A 88 W 455 ~lin                                      // 原地转身一圈（纸片转身）
.60  face 1 dx 0 L 28 Lf -24 lean 12 sq .9 head -4 ~lin                   // 转回来接一个弓步
.64  A 100 W 470 dx -8 lean -3 head 5 sq 1.03 ~out                        // ★ 补一枪
.70  A 86 W 452 dx -2 lean 10 sq .9 head -3
.73  dy -22 sq 1.08 L -10 Lf 14 ~out                                      // 小跳
.76  A 110 W 486 dx -26 dy -30 lean -13 tilt -8 head 8 sq 1.05 ghost 1 ~out   // ★ 空中收尾一枪，后坐更大
.84  A 96 W 462 dx -20 dy 0 tilt -2 lean 0 ghost 0 sq .9 L 18 Lf -16 !land !residue
.95  A 40 W 800 Af 10 dx -6 sq 1 lean 2 L 4 Lf -4 ~out                    // 转枪收回
1    A 25 Af -1 W 818 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0
",
            // 震地（重锤）1.4 秒：抡锤转两圈 → 深蹲把锤拖到身后 → 高高跳起前空翻 → ★.52 砸地 → 锤还插在地里，蓄力 → ★.78 往上一撩把怪掀起来 → 扛回肩上
            [Anim.Quake] = @"
0    A 1 Af -1 W 168 dx 0 dy 0 sq 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 face 1 zoom 1
.05  A 90 W 100 Af 80 lean 6 sq .95 L 10 Lf -8 fx 1 ~out                 // 锤子平着甩出去
.09  face -1 dx 4 ~lin                                                    // 带着锤子转身
.13  face 1 dx 8 ~lin
.17  face -1 dx 10 ~lin                                                   // 再转一圈
.22  face 1 dx 12 A 140 W 232 sq .82 dy 6 lean -10 L -14 Lf 20 head 8 fx 0 glint 1 ~out !land   // 收成深蹲，锤拖到身后
.31  A 150 W 242 sq .84 dy 7 glint 0 zoom 1.04
.40  A 178 W 252 dy -100 dx 26 sq 1.14 lean -14 L -26 Lf 26 head 6 zoom 1.09 ~out   // 高高跳起，锤举过头
.47  spin 290 A 172 W 262 dy -86 dx 36 sq 1.02 ghost 1 ~in                // 空中前空翻
.52  spin 360 A 56 W 38 dy 0 dx 42 sq .8 lean 26 L 28 Lf -24 head -12 zoom 1 fx 1 ~in   // ★ 砸地
.59  sq 1.05 dy -3 ghost 0 fx 0
.67  A 50 W 30 sq .92 dy 4 lean 24 head -6
.74  A 26 W 8 sq .86 dy 9 lean 32 head -8                                 // 压低蓄力，锤还插在地里
.78  A 168 W 205 sq 1.14 dy -28 dx 48 lean -8 head 7 fx 1 ~out            // ★ 往上一撩
.86  A 172 W 210 dy -10 sq 1.02 fx 0
.94  A 62 W 172 dx 20 dy 0 sq .97 lean 4 L 6 Lf -4 !land                  // 扛回肩上
1    A 1 Af -1 W 168 dx 0 dy 0 sq 1 lean 1 spin 360 L 0 Lf 0 head 0 pack 0 face 1
",
            // 贴脸轰击（霰弹）1.3 秒：压低 → 滑步冲到怪脸前 → ★.20 往上轰一枪 → 单手甩枪上膛 → 起跳 → ★.50 空中往下轰 → 后坐掀成后空翻 → 落地端枪 → ★.78 收尾重轰被顶退 → 泵一下收枪
            [Anim.PointBlank] = @"
0    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 face 1
.04  sq .9 dy 4 lean 12 L -10 Lf 14 head -4 ~out
.14  dx 62 dy 2 sq .92 lean 16 L 26 Lf -22 A 70 W 100 Af 30 ghost 1 ~out !skid
.20  A 84 W 122 dx 56 lean -6 head 6 sq 1.04 ghost 0 ~out
.27  A 70 W 104 dx 54 lean 4 sq .96 !casing
.31  A 60 W 300 wy -6 ~lin
.38  A 66 W 458 wy 0 lean 6 ~out !twinkle
.44  dy -64 dx 60 sq 1.1 L -14 Lf 18 A 60 W 450 ~out
.50  A 52 W 416 dy -70 lean 10 head -6 sq 1.02 ~out
.56  dy -80 dx 44 spin -120 ~in
.66  spin -360 dy -10 dx 24 A 70 W 455 ghost 1 ~out
.70  dy 0 sq .86 L 20 Lf -18 lean 8 ghost 0 !land
.74  sq .92 A 86 W 462 lean 14 head -4
.78  A 100 W 488 dx 4 lean -12 tilt -6 head 8 sq 1.05 ~out
.86  A 80 W 462 dx -4 tilt -1 lean 0 sq .96 !casing
.95  A 30 W 458 dx -2 sq 1 L 2 Lf -2
1    A 25 Af -1 W 458 dx 0 dy 0 sq 1 lean 1 tilt 0 spin -360 head 0 L 0 Lf 0 pack 0
",
            // 过压弹（手炮）1.4 秒：把炮抡上肩 → 单膝跪地 → 另一只手掏弹拍进炮膛 → 蓄压发抖、镜头推近 → .43 开炮被推出一大段 → ★.52 落地爆炸 → 炮口拄地冒气 → 起身走回
            [Anim.Overpressure] = @"
0    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 face 1 zoom 1 charge 0 shake 0
.10  A 150 W 200 Af 60 dy -6 lean -8 head 4 sq 1.05 ~out
.18  A 102 W 100 Af 90 dy 10 sq .86 L 34 Lf -50 lean 8 head -4 ~out !thud
.26  Af 150 ~out
.32  Af 72 sq .84 ~in !twinkle
.34  charge .3 zoom 1.04 shake .3
.42  charge .65 zoom 1.08 shake 1 W 102
.43  ~snap shake 0 !fire
.47  A 150 W 136 dx -46 dy 8 lean -18 tilt -9 head 9 sq 1.04 charge 0 zoom 1 ghost 1 ~out !skid
.52  dx -52
.60  A 120 W 110 dx -50 tilt -3 lean -4 ghost 0 sq .9
.72  A 70 W 20 Af 20 dx -44 dy 6 lean 12 head -6 ~out
.80  A 64 W 14 dy 4 sq .94 !steam
.92  A 40 W 80 Af 0 dx -20 dy 0 sq 1 L 6 Lf -6 lean 2
1    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0
",
            // 钉射扫射（钉枪）1.3 秒：转枪端平 → ★.14/.22/.30/.38 边走边点射四钉 → 起步跪滑冲出去 → ★.62/.70 滑行中往上补两钉 → 枪口凑到嘴边吹一口 → 跳回原位
            [Anim.NailBurst] = @"
0    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 face 1
.06  A 70 W 300 Af 40 sq .95 ~out
.12  A 86 W 456 Af 62 L 14 Lf -12 lean 6 ~out
.14  A 93 W 470 dx 2 lean 1 ~out
.18  A 86 W 456 dx 8 L -10 Lf 12 lean 6
.22  A 93 W 471 dx 10 lean 1 ~out
.26  A 86 W 456 dx 16 L 14 Lf -12 lean 6
.30  A 94 W 473 dx 18 lean 0 ~out
.34  A 86 W 456 dx 24 L -10 Lf 12 lean 6
.38  A 96 W 477 dx 26 lean -1 ~out !casing
.46  dx 34 sq .9 L 30 Lf -26 lean -6 A 88 W 458 ~out
.54  dx 86 dy 6 sq .84 lean -14 tilt -8 L 46 Lf -40 head 4 ghost 1 ~out !skid
.62  A 108 W 494 dx 94 ghost 0 ~out
.66  A 98 W 470
.70  A 112 W 500 dx 98 ~out
.78  dx 96 dy 0 sq .96 tilt 0 lean 4 L 10 Lf -8 A 100 W 470
.86  A 140 W 160 head -6 lean 8 ~out
.91  head -2 !twinkle
.95  dx 40 dy -18 sq 1.06 A 60 W 452 ~in
1    A 25 Af -1 W 458 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 ~out
",
            // 信号爆矢（信号弩）1.3 秒：后空翻拉开 → 单膝跪地弩口朝天、点燃信号矢 → .30 抛射 → ★.44 落地炸开 → 转平 → .58 再平射一发（★.62）→ 起身走回
            [Anim.FlareBolt] = @"
0    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 face 1 zoom 1 charge 0
.04  sq .9 dy 4 L 10 Lf -8 ~out
.14  spin -360 dy -40 dx -30 sq 1.04 A 60 W 300 ghost 1 ~out
.20  dy 8 dx -40 sq .86 L 34 Lf -50 A 112 W 132 Af 96 lean -6 ghost 0 !land
.27  charge .6 zoom 1.05 !twinkle
.30  ~snap charge 0 !fire
.33  A 122 W 144 dx -46 lean -10 head 6 zoom 1 ~out
.44  head -2
.50  A 92 W 96 Af 80 lean 6 head -4
.55  charge .5
.58  ~snap charge 0 !fire2
.60  A 100 W 104 dx -52 lean -6 head 4 ~out
.70  A 90 W 96 dx -48 lean 2
.80  A 40 W 98 dx -30 dy 0 sq 1 L 6 Lf -6
1    A 25 Af -1 W 98 dx 0 dy 0 sq 1 lean 1 tilt 0 spin -360 head 0 L 0 Lf 0 pack 0
",
            // 贯穿突刺（撞杆矛）1.3 秒：矛举过头转两圈 → 收到身侧压低、矛尖一闪 → ★.36 冲刺穿过怪群 → 在对面转身 → ★.62 反向再刺回来 → 转回来跳起 → ★.84 矛杆砸地 → 走回
            [Anim.Thrust] = @"
0    A 1 Af -1 W 58 dx 0 dy 0 sq 1 sx 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 shx 0 face 1 zoom 1
.06  A 170 W 200 Af 150 dy -4 sq 1.05 sx .95 lean -4 ~out
.20  W 920 fx 1 ~lin
.26  A 30 W 1030 Af 20 sq .86 sx 1.1 dy 6 dx -14 lean -8 L -12 Lf 16 fx 0 glint 1 ~out
.30  glint 0 zoom 1.05
.36  A 97 W 1173 dx 116 dy 0 sq .94 sx 1.14 lean 18 tilt 6 L 34 Lf -30 shx 14 ghost 1 fx 1 zoom 1 ~out
.44  dx 124 sq 1 sx 1 lean 8 tilt 0 shx 0 ghost 0 fx 0
.47  face .01 ~lin
.50  face -1 A 60 W 1140 ~lin
.56  A 30 sq .9 sx 1.08 lean -6 ~out
.62  A 97 W 1173 dx 44 sq .94 sx 1.12 lean 16 shx 12 ghost 1 fx 1 ~out
.70  dx 34 sx 1 sq 1 shx 0 ghost 0 fx 0
.74  face .01 ~lin
.77  face 1 A 150 W 1260 dy -26 sq 1.08 sx .94 ~lin
.84  A 60 W 1300 dy 0 dx 22 sq .84 sx 1.1 lean 20 head -8 fx 1 ~in !thud
.92  A 20 W 1138 fx 0 sq .98 sx 1 dx 12
1    A 1 Af -1 W 1138 dx 0 dy 0 sq 1 sx 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 shx 0 face 1
",
            // 回旋切割（切割锯）1.2 秒：锯子举平压低 → 跳起来在空中横着转（每半圈切一下 ★.20/.30/.40/.50/.60）一路往前 → 转完举高 → ★.80 往下劈地 → 跳回原位
            [Anim.Whirl] = @"
0    A 1 Af -1 W 58 dx 0 dy 0 sq 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 face 1 zoom 1
.08  A 90 W 90 Af 70 sq .88 dy 6 lean 10 L -12 Lf 16 glint 1 ~out !twinkle
.14  dy -30 sq 1.08 glint 0 fx 1 ghost 1 ~out
.20  face -1 dx 20 dy -36 ~lin
.25  face 1 dx 30 ~lin
.30  face -1 dx 40 dy -40 ~lin
.35  face 1 dx 50 ~lin
.40  face -1 dx 60 dy -38 ~lin
.45  face 1 dx 68 ~lin
.50  face -1 dx 76 dy -30 ~lin
.55  face 1 dx 82 ~lin
.60  face -1 dx 86 dy -20 ~lin
.66  face 1 dx 88 A 170 W 190 dy -40 sq 1.1 ~lin
.80  A 40 W 20 dy 0 sq .82 lean 26 head -10 L 28 Lf -24 ~in !thud
.88  sq 1.02 fx 0 ghost 0
.94  A 20 W 64 dx 40 dy -10 sq 1 lean 4 L 0 Lf 0 ~out
1    A 1 Af -1 W 58 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 face 1
",
            // 吊钩横扫（吊臂钩索）1.4 秒：头顶套索甩两圈 → 往后一收 → 甩出去 ★.34 钩中往回一拽 → 链子收短 → ★.62 绕到身前贴地横扫 → 甩到头顶小跳 → ★.84 往前下方砸 → 收链
            // hook = 链子方向（0 朝下、90 朝后、180 朝上、270 朝前，可以一直往上加转圈）；reach = 链长（<0：放到目标那么远的几成）
            [Anim.HookCombo] = @"
0    A 1 Af -1 W 168 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 zoom 1 reach 4 hook 0
.05  A 175 Af 40 lean -8 head 6 reach 58 hook 180 ~out
.13  hook 540 ~lin
.21  hook 900 ~lin
.26  A 120 hook 990 reach 44 dx -8 sq .92 lean -10 head 0 L -10 Lf 14 ~lin
.33  A 92 hook 1350 reach -1 dx 8 lean 14 sq 1.05 L 22 Lf -20 ~out
.34  ~snap
.42  reach -.55 dx -14 lean -16 tilt -6 A 50 sq .94 ~in !skid
.48  A 150 reach 40 hook 1260 lean -6 tilt 0 sq 1 ~out
.62  hook 1440 reach 52 A 70 dx 6 lean 14 sq .9 L 24 Lf -20 fx 1 ~lin
.70  hook 1530 reach 46 A 120 fx 0 ~lin
.76  hook 1620 A 172 dy -20 sq 1.08 lean -8 ~lin
.84  hook 1755 reach -1 A 70 dy 0 sq .84 lean 22 head -8 ~in !thud
.92  reach 20 hook 1780 A 30 sq .98 lean 4
1    A 1 Af -1 W 168 dx 0 dy 0 sq 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 reach 4 hook 1800
",
            // 迅切连击（初始匕首）1.0 秒：收刀 → ★.22 刺 → 转身回旋斩 ★.36 → 收刀蓄势 → ★.55 冲刺穿过去 → 压低 → ★.74 上挑带小跳 → 后空翻落回
            [Anim.Combo] = @"
0    A 1 Af -1 W 58 dx 0 dy 0 sq 1 sx 1 lean 1 tilt 0 spin 0 head 0 L 0 Lf 0 pack 0 shx 0 face 1
.10  A -18 W 88 dx -8 sq .92 sx 1.08 lean -5 shx -4 L -8 Lf 8 head 5 glint 1 ~out
.22  A 92 W 94 dx 22 sq 1.03 sx .98 lean 7 shx 12 L 20 Lf -18 head -5 fx 1 ghost 1 glint 0 ~out
.28  A 30 W 80 dx 18 shx 4 lean 2 fx 0
.32  face .01 A 120 W 200 ~lin
.36  face 1 A 70 W 40 dx 30 lean 10 fx 1 ~lin
.42  A -10 W 70 dx 26 sq .9 sx 1.1 lean -6 fx 0 glint 1
.55  A 96 W 95 dx 76 sq .94 sx 1.12 lean 12 shx 15 L 30 Lf -26 head -7 fx 1 ghost 1 glint 0 ~out
.62  dx 80 sx 1 sq 1 fx 0
.68  A 20 W 40 sq .88 sx 1.1 dy 4 lean 14 L 16 Lf -10 ghost 0
.74  A 170 W 200 dy -34 sq 1.1 sx .92 lean -10 head 6 fx 1 ghost 1 ~out
.80  dy -40 spin -200 ~in
.88  dy -8 spin -360 dx 50 fx 0 ghost 0 sx 1 ~out
.92  dy 0 sq .9 sx 1.08 !land
1    A 1 Af -1 W 58 dx 0 dy 0 sq 1 sx 1 lean 1 tilt 0 head 0 L 0 Lf 0 pack 0 shx 0 face 1 spin -360
",
        };
    }
}
