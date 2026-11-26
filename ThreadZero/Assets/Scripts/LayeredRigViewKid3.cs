using System;
using UnityEngine;

namespace ThreadZero
{
    // 11-26 卡44：rig_version 3（Resources/Art/V17/Kid3）。
    // ① 腿（胯→踝）和袖子（肩→腕）各是一整条贴图，铺细网格，用骨骼带动网格变形：关节前后一小段沿着同时和上下两节相切的圆弧走
    //    （外侧圆），内侧的肉沿弯角平分线收成一道折痕——不再是两节图叠在一起（关节会有一道硬线）。
    // ② 跑步不再用几条正弦曲线：run3.json 是 Gemini/卡44_跑步重做_学习后/run_ref.py 按动画关键姿势（触地→下沉→交错→蹬地→腾空）
    //    和真人跑步数据算出来、逐帧检查过的表；脚着地时钉在地面上，所以循环进度要按走过的距离算（WalkT）。
    // 其他动作沿用旧动作表（单节手臂 / 腿），换成三节时膝盖、手肘微弯，手腕和脚踝的方向不变。
    public static partial class LayeredRigView
    {
        [Serializable] public class StripEntry { public string file; public float[] joints; public int cols = 6, rows = 64; public float r0 = 3, r1 = 5; }
        [Serializable] public class Rig3Entry { public StripEntry leg, sleeve; public string run, attack; }
        /// 卡48 攻击动作表（Gemini/卡48_攻击动作_发力链/export.py 导出）：每个动作按时长等分采样，通道和 Build3 同一套约定。
        [Serializable] private class AttackTable { public string name; public int n; public float dur, hit; public float[] dx, dy, lean, head, shx, shxF, tw, A, E, W, Af, Ef, L0, K0, F0, L1, K1, F1, pack, fx, g2d; }
        [Serializable] private class AttackFile { public AttackTable[] attacks; }
        private static System.Collections.Generic.Dictionary<string, AttackTable> attacks3 = new System.Collections.Generic.Dictionary<string, AttackTable>();
        /// 普攻轮到第几招（GameAppAction 每出一刀加一）：匕首 ①正手斜劈 ②反手上挑 ③上步刺。
        public static int AttackVariant;
        private static readonly string[] DaggerChain = { "dagger_cut", "dagger_rise", "dagger_thrust" };
        [Serializable] private class RunTable { public int n; public float stride, stance; public float[] dy, lean, head, th0, k0, f0, th1, k1, f1, a0, e0, a1, e1; }
        private sealed class Strip
        {
            public Texture2D tex; public Vector2 J0, J1, J2; public float r0, r1; public int cols, rows;
            public Vector2[] pu, uv; public float[] wTop; public int[] tris; public Vector3[] outV;
        }
        /// 当前主角是不是 rig 3（整条腿 / 袖子网格变形）。
        public static bool K3 { get; private set; }
        private static Strip legStrip, sleeveStrip; private static RunTable run3; private static Part footN3, footF3;
        private static Vector2 lv1, lv2, sv1, sv2; private static float lR1, lR2;
        private static Vector2 handGrip3, weaponGrip3, starterWeaponGrip3;   // 握孔中心（相对手腕）、武器握柄中点（相对武器挂点）
        private static float weaponGripAngle = -999, starterGripAngle = -999;
        private static Vector2 handGripF3 = new Vector2(1f, 8f);   // 远侧拳头（手心那面）的握孔，相对手腕
        private static bool TwoHanded { get { return weaponClass == "heavy" || weaponClass == "pole"; } }
        private static float DefaultGrip2 { get { return weaponClass == "heavy" ? -9f : 22f; } }   // 另一只手在柄上：锤在柄尾（挂点往后 9），长杆在前面 22
        /// 握法（按武器类别）。握柄穿过拳头的方式是固定的：gamma = 武器相对拳头的角度；能动的是手腕：
        /// ω 在 wMin..wMax 之间（负 = 往小指那侧压，正 = 往拇指那侧翘），拳头跟着手腕一起转，所以握柄永远从拳头的同一个位置穿出来。
        /// 武器相对前臂的角度 θ = gamma + ω；动作要的 θ 超出范围时 Grip3 改手肘 / 手臂，武器朝向不变。
        /// 匕首 / 刀 / 重武器 = 锤握：刀从拇指那侧出来，往指尖那侧斜一点（约 80°）；枪 = 手枪式握把，枪管大致顺着拳头；锁链是软的，手腕不弯、不限。
        private static void GripSpec(out float gamma, out float wMin, out float wMax)
        {
            if (IsGun(weaponClass)) { gamma = 10; wMin = -35; wMax = 30; }
            else if (weaponClass == "chain") { gamma = 80; wMin = -720; wMax = 720; }
            else { gamma = 80; wMin = -35; wMax = 25; }
            if (weaponGripAngle > -900) gamma = weaponGripAngle;   // 武器自己的 part.json 填了 grip_angle 就用它（握柄斜着装的工具）
        }

        private static float Ang(Vector2 v) { return Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg; }   // 和竖直向下的夹角，往前为正
        private static Strip MakeStrip(StripEntry e, float scale)
        {
            if (e == null || string.IsNullOrEmpty(e.file) || e.joints == null || e.joints.Length < 6) return null;
            var tex = Resources.Load<Texture2D>(Folder + System.IO.Path.GetFileNameWithoutExtension(e.file));
            if (tex == null) return null;
            if (scale <= 0) scale = 5;
            var s = new Strip { tex = tex, J0 = new Vector2(e.joints[0], e.joints[1]), J1 = new Vector2(e.joints[2], e.joints[3]), J2 = new Vector2(e.joints[4], e.joints[5]),
                                r0 = Mathf.Max(.1f, e.r0), r1 = Mathf.Max(.1f, e.r1), cols = Mathf.Max(1, e.cols), rows = Mathf.Max(1, e.rows) };
            int nv = (s.cols + 1) * (s.rows + 1); s.pu = new Vector2[nv]; s.uv = new Vector2[nv]; s.wTop = new float[nv]; s.outV = new Vector3[nv];
            float W = tex.width, H = tex.height;
            for (int r = 0; r <= s.rows; r++)
                for (int c = 0; c <= s.cols; c++)
                {
                    int i = r * (s.cols + 1) + c; float x = -.5f + W * c / s.cols, y = -.5f + H * r / s.rows;
                    s.pu[i] = new Vector2((x + .5f) / scale, (y + .5f) / scale);
                    s.uv[i] = new Vector2((x + .5f) / W, 1 - (y + .5f) / H);   // 贴图 v = 1 是最上面一行
                    s.wTop[i] = 1 - Sm((s.pu[i].y - (s.J0.y - s.r0)) / (2 * s.r0));
                }
            s.tris = new int[s.rows * s.cols * 6]; int k = 0;
            for (int r = 0; r < s.rows; r++)
                for (int c = 0; c < s.cols; c++)
                {
                    int i = r * (s.cols + 1) + c;
                    s.tris[k++] = i; s.tris[k++] = i + 1; s.tris[k++] = i + s.cols + 1;
                    s.tris[k++] = i + 1; s.tris[k++] = i + s.cols + 2; s.tris[k++] = i + s.cols + 1;
                }
            return s;
        }
        private static bool LoadK3()
        {
            legStrip = MakeStrip(m.rig3.leg, m.scale); sleeveStrip = MakeStrip(m.rig3.sleeve, m.scale);
            footN3 = Load(m.parts.foot); footF3 = Load(m.parts.foot_far) ?? footN3;
            run3 = null;
            var rt = string.IsNullOrEmpty(m.rig3.run) ? null : Resources.Load<TextAsset>(Folder + m.rig3.run);
            if (rt != null)
            {
                run3 = JsonUtility.FromJson<RunTable>(rt.text);
                if (run3 == null || run3.n <= 2 || run3.stride <= 0 || Bad(run3.dy) || Bad(run3.lean) || Bad(run3.head) || Bad(run3.th0) || Bad(run3.k0) || Bad(run3.f0) || Bad(run3.th1) || Bad(run3.k1) || Bad(run3.f1)
                    || Bad(run3.a0) || Bad(run3.e0) || Bad(run3.a1) || Bad(run3.e1)) { Debug.LogError("THREAD ZERO: rig3 run table broken in Resources/" + Folder); run3 = null; }
            }
            attacks3.Clear();
            var at = string.IsNullOrEmpty(m.rig3.attack) ? null : Resources.Load<TextAsset>(Folder + m.rig3.attack);
            if (at != null)
            {
                var af = JsonUtility.FromJson<AttackFile>(at.text);
                if (af != null && af.attacks != null)
                    foreach (var a in af.attacks)
                    {
                        if (a == null || string.IsNullOrEmpty(a.name) || a.n < 2 || BadA(a.dx, a.n) || BadA(a.dy, a.n) || BadA(a.lean, a.n) || BadA(a.head, a.n) || BadA(a.shx, a.n) || BadA(a.shxF, a.n) || BadA(a.tw, a.n)
                            || BadA(a.A, a.n) || BadA(a.E, a.n) || BadA(a.W, a.n) || BadA(a.Af, a.n) || BadA(a.Ef, a.n) || BadA(a.L0, a.n) || BadA(a.K0, a.n) || BadA(a.F0, a.n)
                            || BadA(a.L1, a.n) || BadA(a.K1, a.n) || BadA(a.F1, a.n) || BadA(a.pack, a.n) || BadA(a.fx, a.n)) { Debug.LogError("THREAD ZERO: rig3 attack table broken: " + (a != null ? a.name : "?")); continue; }
                        attacks3[a.name] = a;
                    }
            }
            if (legStrip == null || sleeveStrip == null || footN3 == null) { Debug.LogError("THREAD ZERO: rig3 strips / feet missing in Resources/" + Folder); return false; }
            lv1 = legStrip.J1 - legStrip.J0; lv2 = legStrip.J2 - legStrip.J1; lR1 = Ang(lv1); lR2 = Ang(lv2);
            var hp = m.parts.hand; handGrip3 = hp != null && hp.grip != null && hp.grip.Length >= 2 ? V(hp.grip, Vector2.zero) - V(hp.pivot, Vector2.zero) : new Vector2(1.3f, 6.2f);
            var wp = m.parts.weapon; starterWeaponGrip3 = wp != null && wp.grip != null && wp.grip.Length >= 2 ? V(wp.grip, Vector2.zero) - V(wp.pivot, Vector2.zero) : Vector2.zero;
            starterGripAngle = wp != null ? wp.grip_angle : -999;
            var hpf = m.parts.hand_far; handGripF3 = hpf != null && hpf.grip != null && hpf.grip.Length >= 2 ? V(hpf.grip, Vector2.zero) - V(hpf.pivot, Vector2.zero) : new Vector2(1f, 8f);
            sv1 = sleeveStrip.J1 - sleeveStrip.J0; sv2 = sleeveStrip.J2 - sleeveStrip.J1;
            return true;
        }
        private static bool Bad(float[] a) { return a == null || run3 == null || a.Length < run3.n; }
        private static bool BadA(float[] a, int n) { return a == null || a.Length < n; }

        /// 跑动循环进度（圈数）：rig 3 按导出的步长算（脚着地时钉在地面上，不打滑）；旧骨骼照旧每 24×2π 逻辑像素一圈。
        public static float WalkT(float travel)
        {
            if (Ready() && K3 && run3 != null) return travel / (run3.stride * PixelsPerUnit);
            return travel / 24f / (2 * Mathf.PI);
        }

        // ---------- pose ----------
        private static float Cr(float[] a, int i, float f, int n)   // 循环的 Catmull-Rom
        {
            float p0 = a[(i - 1 + n) % n], p1 = a[i % n], p2 = a[(i + 1) % n], p3 = a[(i + 2) % n];
            return .5f * (2 * p1 + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f * f * f);
        }
        private static Pose RunPose(float t)
        {
            var r = run3; float x = Mathf.Repeat(t, 1) * r.n; int i = Mathf.FloorToInt(x); float f = x - i; int n = r.n;
            var p = new Pose { sx = 1, sy = 1, face = 1, zoom = 1, W = float.NaN };
            p.dy = Cr(r.dy, i, f, n); p.lean = Cr(r.lean, i, f, n); p.head = Cr(r.head, i, f, n);
            p.L = Cr(r.th0, i, f, n); p.KN = Cr(r.k0, i, f, n); p.FN = Cr(r.f0, i, f, n);
            p.Lf = Cr(r.th1, i, f, n); p.KF = Cr(r.k1, i, f, n); p.FF = Cr(r.f1, i, f, n);
            p.A = Cr(r.a0, i, f, n); p.E = Cr(r.e0, i, f, n); p.Af = Cr(r.a1, i, f, n); p.Ef = Cr(r.e1, i, f, n);
            p.pack = -3.5f * Mathf.Cos(4 * Mathf.PI * (Mathf.Repeat(t, 1) - .125f));   // 背包跟着身体起伏晚半拍晃（身体最低点之后 0.05 圈，背包下摆最低）
            // 近侧手拿着武器：匕首握在拳头里（刀相对前臂的角度不变）；枪端在身前、重武器扛着，手臂只跟着轻轻摆
            if (IsGun(weaponClass)) { p.A = Mathf.Lerp(16, p.A, .25f); p.E = Mathf.Lerp(46, p.E, .25f); p.W = 98 + (p.A + p.E * .5f - 39) * .25f; }
            else if (weaponClass == "heavy" && !LegacyGrip) { p.A = Mathf.Lerp(-13, p.A, .15f); p.E = 24; p.W = p.A + p.E + 62; }   // 提着锤走 / 跑：锤头在身前低处，比站着时往前抬一点（走路身体一上一下，不抬的话锤头会扎进地里）；两只手握柄，锤重，只跟着身体轻轻晃
            else if (weaponClass == "heavy" || weaponClass == "chain") { p.A = Mathf.Lerp(-8, p.A, .35f); p.E = Mathf.Lerp(34, p.E, .35f); p.W = 168 + (p.A + p.E * .5f - 9) * .5f; }
            else if (LegacyGrip) p.W = p.A + p.E + 40;
            else if (weaponClass == "pole") { p.A = Mathf.Lerp(-18, p.A, .1f); p.E = 75; p.W = p.A + p.E + 50; }   // 长杆：两只手端着跑，杆子很长（杆尖把角度放大），手臂几乎不摆
            else
            {   // 匕首（锤握，刀从拇指那侧出来）：拿刀的手臂摆小一点、肘弯少一点，前臂最高到水平下方约 20°，不然刀会竖到脸前；
                // 手腕跟着压：手在后面时握持角约 85°（刀尖朝前），手到前面时往小指那侧压到约 55°，刀尖斜朝前上
                p.A = Mathf.Lerp(-25, p.A, .5f); p.E = Mathf.Lerp(60, p.E, .4f);
                float fa = p.A + p.E;
                p.W = fa + Mathf.Lerp(85, 55, Mathf.InverseLerp(19, 70, fa));
            }
            Grip3(ref p);
            return p;
        }
        public static bool OldAttack;   // 测试对照：用回旧的普攻动作（只往前戳）
        private static AttackTable AttackFor(Anim anim)
        {
            if (LegacyGrip || OldAttack) return null;
            AttackTable a;
            if (anim == Anim.Attack && weaponClass == "dagger" && attacks3.TryGetValue(DaggerChain[((AttackVariant % DaggerChain.Length) + DaggerChain.Length) % DaggerChain.Length], out a)) return a;
            if (anim == Anim.Smash && weaponClass == "heavy" && attacks3.TryGetValue("hammer_smash", out a)) return a;   // 双手抡锤
            if (anim == Anim.Attack && weaponClass == "pole" && attacks3.TryGetValue("pole_thrust", out a)) return a;    // 长杆突刺
            return null;
        }
        private static Pose AttackPose(AttackTable a, float t)
        {
            float x = Mathf.Clamp01(t) * (a.n - 1); int i = Mathf.Min(a.n - 2, Mathf.FloorToInt(x)); float f = x - i;
            System.Func<float[], float> g = v => v[i] + (v[i + 1] - v[i]) * f;
            var p = new Pose { sx = 1, sy = 1, face = 1, zoom = 1 };
            p.dx = g(a.dx); p.dy = g(a.dy); p.lean = g(a.lean); p.head = g(a.head); p.pack = g(a.pack); p.shx = g(a.shx); p.shxF = g(a.shxF); p.tw = g(a.tw);
            p.A = g(a.A); p.E = g(a.E); p.W = g(a.W); p.Af = g(a.Af); p.Ef = g(a.Ef);
            p.L = g(a.L0); p.KN = g(a.K0); p.FN = g(a.F0); p.Lf = g(a.L1); p.KF = g(a.K1); p.FF = g(a.F1);
            p.fx = g(a.fx) > .5f; p.g2d = a.g2d != null && a.g2d.Length >= a.n ? g(a.g2d) : 0;
            Grip3(ref p);
            return p;
        }
        private static Pose Compute3(Anim anim, float t)
        {
            if (anim == Anim.Walk && run3 != null) return RunPose(t);
            { var at = AttackFor(anim); if (at != null) return AttackPose(at, t); }   // 卡48：按发力链做的攻击动作表
            Pose p = Compute(anim, t);
            bool front = legNOff.x >= legFOff.x;   // 旧动作表里 L 是前面那条腿（和 Build 一样的换法）
            float lN = front ? p.L : p.Lf, lF = front ? p.Lf : p.L;
            float kb = anim == Anim.Idle || anim == Anim.Walk ? 8 : 6, eb = anim == Anim.Idle || anim == Anim.Walk ? 16 : 10;
            p.L = lN + kb * .5f; p.Lf = lF + kb * .5f; p.KN = p.KF = kb; p.FN = p.FF = kb * .5f;   // 膝盖微弯，脚踝落在原来的方向上，鞋的角度和旧动作一样
            p.A -= eb * .5f; p.Af -= eb * .5f; p.E = p.Ef = eb;                                    // 手肘微弯，手腕落在原来的方向上；武器角度 W 不变（Build3 里扣掉手的转角）
            if (anim == Anim.Idle && !LegacyGrip && weaponClass == "pole") { p.A -= 9; p.E = 75; p.W = p.A + p.E + 50; }
            else if (anim == Anim.Idle && !LegacyGrip && weaponClass == "heavy") { p.A -= 4; p.E = 20; p.W = p.A + p.E + 52; }   // 重武器待机：垂手低握，锤头在身前低处（以前竖在脸前挡住脸），另一只手握柄尾   // 长杆待机：后手在腰边、杆子斜指前上，前手握在杆上（Build3 里 IK）
            else if (anim == Anim.Idle && !LegacyGrip && !IsGun(weaponClass) && weaponClass != "heavy" && weaponClass != "chain") p.W = p.A + p.E + 68;   // 待机：匕首自然垂着拿，手腕往小指那侧压一点，刀尖朝前下
            Grip3(ref p);
            return p;
        }
        // 换动作时不硬切：起跑、停下、出手前后用很短的时间从上一个姿势过渡过去（待机 / 跑 0.14 秒，进攻击 0.07 秒，不耽误出手）
        /// 游戏自己的时钟（GameApp 每帧写进来；跟着慢放走，过渡在慢放录像里也看得到）；没写就用 Time.time。
        public static float AnimClock = -1;
        public static float LiveT0;   // 测试：大人物循环从哪个时刻开始算（TestPoseSheet 调用时记下）
        public static Vector2 LastGripPx, LastFarHandPx; private static int sheetWritten = -1;
        private static (Anim, Pose, Pose, float, float, bool) sheetBlend = (Anim.Idle, default(Pose), default(Pose), -1f, 0f, false);
        public static bool LegacyGrip;   // 测试对照：改之前的样子（远侧手 = 近侧手的复制，武器挂在手腕上、手腕不弯）
        public static bool NoBlend;   // 测试姿势表：一帧里画很多个姿势，不做过渡
        /// 测试：白底上画一个动作的 12 个时刻（检查握持、关节用）。GameApp.TestPoseSheet 打开。
        public static void PoseSheet(GameModel model, int anim)
        {
            Ui.Fill(0, 0, 1600, 900, Color.white);
            if (anim >= 100)   // 测试：一个大人物按游戏时钟循环播（攻击 → 待机），看连贯的动作；199 = 普攻三招轮流
            {
                // 一个循环 = 先站 0.12 秒 → 攻击（时长按这把武器的动作表：匕首 0.33、锤 / 长杆 0.68）→ 再站 0.3 秒；从 TestPoseSheet 调用那一刻开始算
                Anim la = anim == 199 ? Anim.Attack : (Anim)Mathf.Clamp(anim - 100, 0, (int)Anim.HookCombo);
                var at0 = anim == 199 ? null : AttackFor(la);
                float ad = at0 != null ? at0.dur : .33f, lead = .12f, cyc = lead + ad + .3f, now = (AnimClock >= 0 ? AnimClock : Time.time) - LiveT0, ph = Mathf.Repeat(now, cyc);
                if (anim == 199) AttackVariant = Mathf.FloorToInt(now / cyc);
                bool att = ph >= lead && ph < lead + ad;
                Ui.Fill(0, 838, 1600, 3, new Color(.55f, .55f, .55f));
                // 过渡状态和游戏里的主角分开存（不然两边的动作互相当成「上一个动作」，一直在过渡）
                var keep = (lastAnim3, lastPose3, fromPose3, blendStart3, blendDur3, havePose3);
                (lastAnim3, lastPose3, fromPose3, blendStart3, blendDur3, havePose3) = sheetBlend;
                Draw(model, 560, 840, 2.6f, att ? la : Anim.Idle, att ? (ph - lead) / ad : ph / cyc, Color.white);   // 待机呼吸也按循环长度走，录成 GIF 首尾接得上
                Ui.Text(24, 18, 600, 30, "t = " + (ph * 1000).ToString("000") + " ms" + (att && at0 != null && Mathf.Abs((ph - lead) - at0.hit * ad) < .006f ? "   命中" : ""), 20, new Color(.45f, .45f, .45f), TextAnchor.UpperLeft);
                sheetBlend = (lastAnim3, lastPose3, fromPose3, blendStart3, blendDur3, havePose3);
                (lastAnim3, lastPose3, fromPose3, blendStart3, blendDur3, havePose3) = keep;
                return;
            }
            Anim a = (Anim)Mathf.Clamp(anim, 0, (int)Anim.HookCombo);
            NoBlend = true; var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                float t = i / 12f, x = 150 + (i % 6) * 262, y = 405 + (i / 6) * 440;
                Draw(model, x, y, 1.45f, a, t, Color.white);
                sb.AppendLine(i + " " + LastGripPx.x.ToString("0") + " " + LastGripPx.y.ToString("0") + " " + LastFarHandPx.x.ToString("0") + " " + LastFarHandPx.y.ToString("0"));
                Ui.Text(x - 120, y + 10, 240, 22, a + "  t=" + t.ToString("0.00"), 15, Color.black, TextAnchor.MiddleCenter);
            }
            NoBlend = false;
            if (Event.current.type == EventType.Repaint && sheetWritten != anim)   // 每个动作写一次：各格握孔的屏幕坐标（截图裁切用）
            {
                sheetWritten = anim;
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/TZBridge/posesheet_" + anim + ".txt"), Screen.width + " " + Screen.height + "\n" + sb); } catch (System.Exception) { }
            }
        }
        private static Anim lastAnim3 = Anim.Idle; private static Pose lastPose3, fromPose3; private static float blendStart3 = -1, blendDur3; private static bool havePose3;
        private static Pose Blend3(Anim anim, Pose p)
        {
            if (NoBlend) return p;
            float now = AnimClock >= 0 ? AnimClock : Time.time;
            if (havePose3 && anim != lastAnim3) { fromPose3 = lastPose3; blendStart3 = now; blendDur3 = anim == Anim.Idle || anim == Anim.Walk ? .14f : .07f; }
            lastAnim3 = anim;
            if (blendStart3 >= 0)
            {
                float k = blendDur3 > 0 ? (now - blendStart3) / blendDur3 : 1;
                if (k >= 1 || k < 0) blendStart3 = -1; else p = LerpPose(fromPose3, p, Sm(k));
            }
            lastPose3 = p; havePose3 = true;
            return p;
        }
        private static Pose LerpPose(Pose a, Pose b, float k)
        {
            Pose r = b;
            r.dx = Mathf.Lerp(a.dx, b.dx, k); r.dy = Mathf.Lerp(a.dy, b.dy, k); r.sx = Mathf.Lerp(a.sx, b.sx, k); r.sy = Mathf.Lerp(a.sy, b.sy, k);
            r.tilt = Mathf.Lerp(a.tilt, b.tilt, k); r.lean = Mathf.Lerp(a.lean, b.lean, k); r.head = Mathf.Lerp(a.head, b.head, k); r.pack = Mathf.Lerp(a.pack, b.pack, k);
            // 角度走最近的那一边（攻击动作里手臂会转一整圈，结束时是 -369°，回待机 -9° 不能倒着再抡一圈）
            r.A = Mathf.LerpAngle(a.A, b.A, k); r.Af = Mathf.LerpAngle(a.Af, b.Af, k); r.L = Mathf.LerpAngle(a.L, b.L, k); r.Lf = Mathf.LerpAngle(a.Lf, b.Lf, k);
            r.W = Mathf.LerpAngle(a.W, b.W, k); r.shx = Mathf.Lerp(a.shx, b.shx, k); r.wx = Mathf.Lerp(a.wx, b.wx, k); r.wy = Mathf.Lerp(a.wy, b.wy, k);
            r.KN = Mathf.Lerp(a.KN, b.KN, k); r.KF = Mathf.Lerp(a.KF, b.KF, k); r.FN = Mathf.Lerp(a.FN, b.FN, k); r.FF = Mathf.Lerp(a.FF, b.FF, k);
            r.E = Mathf.Lerp(a.E, b.E, k); r.Ef = Mathf.Lerp(a.Ef, b.Ef, k);
            r.tw = Mathf.Lerp(a.tw, b.tw, k); r.shxF = Mathf.Lerp(a.shxF, b.shxF, k); r.g2d = Mathf.Lerp(a.g2d, b.g2d, k);
            return r;
        }
        /// 武器必须握在拳头里：武器相对前臂的角度只能在 GripSpec 给的范围里。超出时保持武器朝向 W 不变，先改手肘，再改整条手臂。
        private static void Grip3(ref Pose p)
        {
            if (float.IsNaN(p.W) || LegacyGrip) return;
            float gam, w0, w1; GripSpec(out gam, out w0, out w1); float lo = gam + w0, hi = gam + w1;
            float th = Mathf.DeltaAngle(0, p.W - p.A - p.E);   // 手臂 / 武器转过整圈时差 360°，按最近的算
            if (th > hi) { p.E = Mathf.Min(140, p.E + (th - hi)); th = Mathf.DeltaAngle(0, p.W - p.A - p.E); if (th > hi) p.A += th - hi; }
            else if (th < lo) { p.E = Mathf.Max(0, p.E - (lo - th)); th = Mathf.DeltaAngle(0, p.W - p.A - p.E); if (th < lo) p.A -= lo - th; }
        }
        private static Bones PoseBones(Anim anim, float t) { return K3 ? Build3(Compute3(anim, t)) : Build(Compute(anim, t)); }
        private static Bones Build3(Pose p)
        {
            var b = new Bones();
            Vector2 pivot = new Vector2(hip.x, hip.y - 18);
            Matrix4x4 flip = p.spin != 0 ? T(pivot) * Rm(-p.spin) * T(-pivot) : Matrix4x4.identity;
            Matrix4x4 root = T(p.dx, p.dy) * flip * Rm(-p.tilt) * S(p.sx * (p.face == 0 ? .001f : p.face), p.sy);
            b.root = root;
            b.torso = root * T(hip) * Rm(-p.lean);
            b.head = b.torso * T(headOff) * Rm(-p.head) * S(1 / p.sx, 1 / p.sy);
            b.pack = b.torso * T(packOff) * Rm(-p.pack);
            b.parN = b.torso * T(sh.x + p.shx, sh.y); b.armN = b.parN * Rm(p.A); b.foreN = b.armN * T(sv1) * Rm(p.E);
            float gam, w0, w1; GripSpec(out gam, out w0, out w1);
            float th = float.IsNaN(p.W) ? gam : weaponClass == "chain" ? p.W - p.A - p.E : Mathf.DeltaAngle(0, p.W - p.A - p.E), om = weaponClass == "chain" ? 0 : Mathf.Clamp(th - gam, w0, w1);   // ω：手腕弯多少（拳头跟着转）
            if (LegacyGrip) om = 0;
            b.hand = b.foreN * T(sv2) * Rm(om);
            b.parF = b.torso * T(shF + new Vector2(p.shxF, 0)); b.armF = b.parF * Rm(p.Af); b.foreF = b.armF * T(sv1) * Rm(p.Ef); b.handF = b.foreF * T(sv2);
            b.wpn = LegacyGrip ? b.hand * Rm(th) : b.hand * T(handGrip3) * Rm(th - om) * T(-weaponGrip3);   // 握柄中点放在拳头的握孔里，从拳头的固定位置穿过去
            if (p.wx != 0 || p.wy != 0) b.wpn = T(p.wx, p.wy) * b.wpn;
            if (TwoHanded && !LegacyGrip && !float.IsNaN(p.W)) GripFar(ref b, p, gam, w0, w1);   // 卡48：双手武器——远侧手握在柄上
            b.hipN = root * T(hip + legNOff + new Vector2(p.tw, 0)); b.legN = b.hipN * Rm(p.L - lR1); b.shinN = b.legN * T(lv1) * Rm(lR1 - lR2 - p.KN); b.footN = b.shinN * T(lv2) * Rm(p.FN);
            b.hipF = root * T(hip + legFOff - new Vector2(p.tw, 0)); b.legF = b.hipF * Rm(p.Lf - lR1); b.shinF = b.legF * T(lv1) * Rm(lR1 - lR2 - p.KF); b.footF = b.shinF * T(lv2) * Rm(p.FF);
            return b;
        }

        /// 双手武器：远侧拳头的握孔对准柄上第二个握点（沿柄离挂点 g2d，往武器头为正），拳头和柄的角度同近侧手；两节 IK 求远侧手臂（手肘往前屈）。
        private static void GripFar(ref Bones b, Pose p, float gam, float w0, float w1)
        {
            Vector2 axis = tipPt.sqrMagnitude > 1e-6f ? tipPt.normalized : new Vector2(0, 1);
            float d2 = p.g2d != 0 ? p.g2d : DefaultGrip2;
            Matrix4x4 inv = b.parF.inverse, wl = inv * b.wpn;
            Vector2 g2 = P(wl, axis * d2), o = P(wl, Vector2.zero), wdir = P(wl, new Vector2(0, 1)) - o;
            float hfr = Ang(wdir) - gam;                                    // 远侧拳头的朝向（让柄从拳头同样的位置穿过去）
            Vector2 wrist = g2 - P(Rm(hfr), handGripF3);
            float l1 = sv1.magnitude, l2 = sv2.magnitude, dist = Mathf.Clamp(wrist.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f), phi = Ang(wrist);
            float al = Mathf.Acos(Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist), -1, 1)) * Mathf.Rad2Deg;
            float be = Mathf.Acos(Mathf.Clamp((l2 * l2 + dist * dist - l1 * l1) / (2 * l2 * dist), -1, 1)) * Mathf.Rad2Deg;
            float af = phi - al - Ang(sv1), ef = phi + be - Ang(sv2) - af;
            float omF = Mathf.Clamp(Mathf.DeltaAngle(af + ef, hfr), w0, w1);
            b.armF = b.parF * Rm(af); b.foreF = b.armF * T(sv1) * Rm(ef); b.handF = b.foreF * T(sv2) * Rm(omF);
        }

        // ---------- drawing ----------
        private static Vector2 Rot2(Vector2 v, float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        private static float Cross2(Vector2 d, Vector2 v) { return d.x * v.y - d.y * v.x; }
        /// 网格顶点 → 屏幕：par = 父骨（胯 / 肩，不转），m1 = 第一节（原点在 J0），m2 = 第二节（原点在 J1）；都已经乘过 screenM。
        private static void Deform(Strip s, Matrix4x4 par, Matrix4x4 m1, Matrix4x4 m2)
        {
            Vector2 PJ0 = P(m1, Vector2.zero), PJ1 = P(m2, Vector2.zero), PJ2 = P(m2, s.J2 - s.J1);
            Vector2 u1 = PJ1 - PJ0, u2 = PJ2 - PJ1;
            if (u1.sqrMagnitude < 1e-8f || u2.sqrMagnitude < 1e-8f) { for (int i = 0; i < s.pu.Length; i++) { Vector2 q = P(m1, s.pu[i] - s.J0); s.outV[i] = new Vector3(q.x, q.y, 0); } return; }
            u1.Normalize(); u2.Normalize();
            float phi = Mathf.Atan2(u1.x * u2.y - u1.y * u2.x, Vector2.Dot(u1, u2));
            Vector2 lat0 = new Vector2(m1.m00, m1.m10); float rpx = s.r1 * new Vector2(m1.m01, m1.m11).magnitude;
            Vector2 P1 = PJ1 - u1 * rpx; bool straight = Mathf.Abs(phi) < .5f * Mathf.Deg2Rad; Vector2 O = Vector2.zero;
            if (!straight) { float R = rpx / Mathf.Tan(Mathf.Abs(phi) * .5f); O = P1 + Rot2(u1, Mathf.Sign(phi) * Mathf.PI * .5f) * R; }
            bool crease = Mathf.Abs(phi) > 10 * Mathf.Deg2Rad; Vector2 d = Vector2.zero; float sUp = 0;
            if (crease) { d = (u2 - u1).normalized; sUp = Mathf.Sign(Cross2(d, -u1)); }
            for (int i = 0; i < s.pu.Length; i++)
            {
                Vector2 q = s.pu[i], o; float yk = q.y - s.J1.y;
                if (yk < -s.r1) { float w = s.wTop[i]; o = P(par, q - s.J0) * w + P(m1, q - s.J0) * (1 - w); }   // 最上面一小段（藏在短裤 / 袖窿里）和父骨过渡
                else if (yk > s.r1) o = P(m2, q - s.J1);
                else
                {
                    float t = (yk + s.r1) / (2 * s.r1), xl = q.x - s.J1.x;
                    if (straight) o = P1 + u1 * (2 * rpx * t) + lat0 * xl;
                    else { float al = phi * t; o = O + Rot2(P1 - O, al) + Rot2(lat0, al) * xl; }
                }
                if (crease)   // 内侧挤成一道折痕：越过弯角平分线的点投影回线上（不留缺口、不互相穿插）
                {
                    Vector2 rel = o - PJ1; float c = Cross2(d, rel);
                    if (c != 0 && Vector2.Dot(rel, d) > 0)
                    {
                        float side = Mathf.Sign(c);
                        if (yk < 0 ? side == -sUp : side == sUp) o = PJ1 + d * Vector2.Dot(rel, d);
                    }
                }
                s.outV[i] = new Vector3(o.x, o.y, 0);
            }
        }
        private static void DrawStrip(Strip s, Matrix4x4 par, Matrix4x4 m1, Matrix4x4 m2, Color color, Material use = null)
        {
            if (s == null) return;
            Deform(s, screenM * par, screenM * m1, screenM * m2);
            Material mt = use ?? mat; mt.mainTexture = s.tex; mt.SetPass(0);
            GL.Begin(GL.TRIANGLES); GL.Color(color);
            int[] tr = s.tris; Vector3[] v = s.outV; Vector2[] uv = s.uv;
            for (int k = 0; k < tr.Length; k++) { int i = tr[k]; GL.TexCoord2(uv[i].x, uv[i].y); GL.Vertex(v[i]); }
            GL.End();
        }
        /// 身体后面那一层：远侧袖子、远侧手、远侧腿、远侧鞋、近侧腿、近侧鞋（顺序和卡44 预览一样）。
        private static void DrawBack3(Bones b, Color tint, Color far)
        {
            DrawStrip(sleeveStrip, b.parF, b.armF, b.foreF, far); if (LegacyGrip) Blit(b.handF, hand, false, far); else if (!TwoHanded) Blit(b.handF, FarHand, FarHandMirrored, Color.Lerp(far, tint, .45f));   // 远侧手的皮肤少压暗一点（压到 .72 像戴了棕色手套）；双手武器的远侧手在 FarGrip3 里画（身体上面）
            Blit(b.pack, pack, false, tint); GearScan(b.pack, pack, false, qBack, tint);   // 背包挂在背上：挡住远侧手臂，被身体挡住
            DrawStrip(legStrip, b.hipF, b.legF, b.shinF, far); Blit(b.footF, footF3, false, far);
            DrawStrip(legStrip, b.hipN, b.legN, b.shinN, tint); Blit(b.footN, footN3, false, tint);
        }
        private static void DrawNearArm3(Bones b, Color tint) { DrawStrip(sleeveStrip, b.parN, b.armN, b.foreN, tint); }
        /// 双手武器：远侧手绕到身前握着柄，画在身体 / 头上面、近侧手臂和武器下面（不然手在身体轮廓里面时被衣服整个挡住，看着像单手拿）。
        private static void FarGrip3(Bones b, Color tint, Color far) { if (TwoHanded && !LegacyGrip) Blit(b.handF, FarHand, FarHandMirrored, Color.Lerp(far, tint, .45f)); }
        private static void Ghost3(Bones g, Color c)
        {
            Blit(g.pack, pack, false, c, flat);
            DrawStrip(legStrip, g.hipF, g.legF, g.shinF, c, flat); Blit(g.footF, footF3, false, c, flat);
            DrawStrip(legStrip, g.hipN, g.legN, g.shinN, c, flat); Blit(g.footN, footN3, false, c, flat);
            Blit(g.torso, torso, false, c, flat); Blit(g.head, head, false, c, flat);
            DrawStrip(sleeveStrip, g.parN, g.armN, g.foreN, c, flat);
            Blit(g.wpn, weapon, false, c, flat); Blit(g.hand, NearHand, false, c, flat);
        }
    }
}
