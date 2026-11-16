using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // V13 layered rigid-part character (S1 starter outfit). Port of Tools/RigPrototype/rig.py.
    // Parts + parts.json live in Resources/Art/V13/Starter. Units: 1 unit = 4 texture px; y points down.
    // Angle convention (same as rig.py): 0 = limb hangs down, positive = swings forward (+x, facing right).
    public static class LayeredRigView
    {
        public enum Anim { Idle, Walk, Attack, Combo, Shoot, Smash }

        [Serializable] public class PartEntry { public string file; public float[] pivot; public float[] hand; public float[] tip; }
        [Serializable] public class PartSet { public PartEntry head, torso, arm, hand, leg_near, leg_far, pack, weapon; }
        [Serializable] public class RigOffsets { public float[] hip, head, pack, leg_near, leg_far; }
        [Serializable] public class Manifest { public float scale = 4; public string weapon_class; public float[] shoulder, shoulderF; public PartSet parts; public RigOffsets rig; }
        // 每件装备外观一个文件夹：Resources/Art/V13/Parts/<外观ID>/part.json + 图。只写这件装备负责的部件：
        // 头部 head；外套 torso + arm（可带 shoulder/shoulderF）；背部 pack；武器 weapon（带 tip，weapon_class = dagger/gun/heavy）。
        [Serializable] public class LookFile { public float scale = 4; public string weapon_class; public float[] shoulder, shoulderF; public PartEntry head, torso, arm, pack, weapon; }
        private sealed class Look { public Part head, torso, arm, pack, weapon; public bool hasShoulder; public Vector2 sh, shF, tip, handPt; public string cls; }

        private sealed class Part { public Texture2D tex; public Vector2 pivot, size; }

        private const string Folder = "Art/V13/Starter/", PartsFolder = "Art/V13/Parts/";
        private static Look starter;
        private static readonly Dictionary<string, Look> looks = new Dictionary<string, Look>();
        private static readonly HashSet<string> missingReported = new HashSet<string>();
        private static string weaponClass = "dagger";
        public const float PixelsPerUnit = 1.5f;   // ~140 units tall -> ~210 logical px (v0.13 battle scale)
        private static bool loaded, failed;
        public static Vector2 Muzzle, MuzzleDir = Vector2.right;
        public static string Kind { get { return weaponClass; } }
        // 每次进入 Play 都重新读部件和 part.json。项目若关了“进入播放模式时重载域”，静态缓存会一直留着旧的锚点/尺寸，
        // 换了新图后手和袖子就会错位。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { loaded = false; failed = false; looks.Clear(); missingReported.Clear(); }
        private static Manifest m;
        private static Part head, torso, arm, hand, legN, legF, pack, weapon;
        private static Vector2 handPt, tipPt, hip, headOff, packOff, legNOff, legFOff, sh, shF;

        // ---------- loading ----------
        private static Vector2 V(float[] a, Vector2 fallback) { return a != null && a.Length >= 2 ? new Vector2(a[0], a[1]) : fallback; }
        private static Part Load(PartEntry e) { return Load(Folder, e, m.scale); }
        private static Part Load(string folder, PartEntry e, float scale)
        {
            if (e == null || string.IsNullOrEmpty(e.file)) return null;
            var tex = Resources.Load<Texture2D>(folder + System.IO.Path.GetFileNameWithoutExtension(e.file));
            if (tex == null) return null;
            if (scale <= 0) scale = 4;
            return new Part { tex = tex, pivot = V(e.pivot, Vector2.zero), size = new Vector2(tex.width / scale, tex.height / scale) };
        }
        private static Look LoadLook(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Look l;
            if (looks.TryGetValue(id, out l)) return l;
            l = null;
            var json = Resources.Load<TextAsset>(PartsFolder + id + "/part");
            if (json != null)
            {
                var f = JsonUtility.FromJson<LookFile>(json.text);
                string folder = PartsFolder + id + "/";
                l = new Look { head = Load(folder, f.head, f.scale), torso = Load(folder, f.torso, f.scale), arm = Load(folder, f.arm, f.scale),
                               pack = Load(folder, f.pack, f.scale), weapon = Load(folder, f.weapon, f.scale), cls = f.weapon_class };
                l.hasShoulder = f.shoulder != null && f.shoulder.Length >= 2;
                if (l.hasShoulder) { l.sh = V(f.shoulder, Vector2.zero); l.shF = V(f.shoulderF, l.sh); }
                if (l.arm != null) l.handPt = f.arm.hand != null ? V(f.arm.hand, Vector2.zero) - l.arm.pivot : new Vector2(0, l.arm.size.y - 11 - l.arm.pivot.y);
                if (l.weapon != null) l.tip = f.weapon.tip != null ? V(f.weapon.tip, Vector2.zero) - l.weapon.pivot : new Vector2(0, l.weapon.size.y - l.weapon.pivot.y);
            }
            else if (missingReported.Add(id)) Debug.Log("THREAD ZERO: no layered art for " + id + " yet (Resources/" + PartsFolder + id + "/part.json); using the starter part");
            looks[id] = l;
            return l;
        }
        public static string WeaponClass(GameModel model)
        {
            EquipmentBase w = model.Appearance(Slot.Weapon);
            Look l = LoadLook(w.id);
            if (l != null && !string.IsNullOrEmpty(l.cls)) return l.cls;
            if (w.id == "w4") return "heavy";
            return w.attackStyle == AttackStyle.Melee ? "dagger" : "gun";
        }
        /// 按当前穿着（含幻化）换上各部位的外观；缺图的部位用初始外观顶替。
        private static void ApplyLook(GameModel model)
        {
            bool bareHead = model.Equipped(Slot.Head) == null && string.IsNullOrEmpty(model.state.appearance[(int)Slot.Head]);
            Look h = bareHead ? null : LoadLook(model.Appearance(Slot.Head).id);
            Look c = LoadLook(model.Appearance(Slot.Coat).id);
            Look b = LoadLook(model.Appearance(Slot.Back).id);
            Look w = LoadLook(model.Appearance(Slot.Weapon).id);
            head = h != null && h.head != null ? h.head : starter.head;
            bool coat = c != null && c.torso != null;
            torso = coat ? c.torso : starter.torso;
            arm = coat && c.arm != null ? c.arm : starter.arm;
            handPt = coat && c.arm != null ? c.handPt : starter.handPt;
            sh = coat && c.hasShoulder ? c.sh : starter.sh; shF = coat && c.hasShoulder ? c.shF : starter.shF;
            pack = b != null && b.pack != null ? b.pack : starter.pack;
            bool wpn = w != null && w.weapon != null;
            weapon = wpn ? w.weapon : starter.weapon; tipPt = wpn ? w.tip : starter.tip;
            weaponClass = WeaponClass(model);
        }
        public static bool Ready()
        {
            if (loaded) return !failed;
            loaded = true;
            var json = Resources.Load<TextAsset>(Folder + "parts");
            if (json == null) { failed = true; Debug.LogError("THREAD ZERO: missing Resources/" + Folder + "parts.json"); return false; }
            m = JsonUtility.FromJson<Manifest>(json.text);
            if (m.scale <= 0) m.scale = 4;
            head = Load(m.parts.head); torso = Load(m.parts.torso); arm = Load(m.parts.arm); hand = Load(m.parts.hand);
            legN = Load(m.parts.leg_near); legF = Load(m.parts.leg_far); pack = Load(m.parts.pack); weapon = Load(m.parts.weapon);
            failed = head == null || torso == null || arm == null || hand == null || legN == null || legF == null || pack == null || weapon == null;
            if (failed) { Debug.LogError("THREAD ZERO: V13 starter parts incomplete in Resources/" + Folder); return false; }
            Vector2 ap = arm.pivot;
            handPt = m.parts.arm.hand != null ? V(m.parts.arm.hand, Vector2.zero) - ap : new Vector2(0, arm.size.y - 11 - ap.y);
            tipPt = m.parts.weapon.tip != null ? V(m.parts.weapon.tip, Vector2.zero) - weapon.pivot : new Vector2(0, weapon.size.y - weapon.pivot.y);
            var rg = m.rig ?? new RigOffsets();
            hip = V(rg.hip, new Vector2(0, -21)); headOff = V(rg.head, new Vector2(2, -29)); packOff = V(rg.pack, new Vector2(-21, -20));
            legNOff = V(rg.leg_near, new Vector2(5, 0)); legFOff = V(rg.leg_far, new Vector2(-5, 0));
            sh = V(m.shoulder, new Vector2(11, -26)); shF = V(m.shoulderF, new Vector2(-4, -26));
            starter = new Look { head = head, torso = torso, arm = arm, pack = pack, weapon = weapon, sh = sh, shF = shF, tip = tipPt, handPt = handPt, cls = "dagger" };
            Debug.Log("THREAD ZERO: V13 layered starter rig loaded");
            return true;
        }

        // ---------- pose (identical numbers to rig.py) ----------
        private struct Pose { public float dx, dy, sx, sy, tilt, lean, head, pack, A, Af, L, Lf, W, shx; public bool fx, ghost, glint; }
        private static float Sm(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        private static float Kf(float t, float[] k)   // k = t0,v0,t1,v1,...
        {
            if (t <= k[0]) return k[1];
            for (int i = 0; i + 3 < k.Length; i += 2)
                if (t <= k[i + 2]) return k[i + 1] + (k[i + 3] - k[i + 1]) * Sm((t - k[i]) / (k[i + 2] - k[i]));
            return k[k.Length - 1];
        }
        private static Pose Compute(Anim anim, float t)
        {
            float ph = 2 * Mathf.PI * t;
            var p = new Pose { sx = 1, sy = 1, lean = 1, A = 8, Af = -8, W = float.NaN };
            if (anim == Anim.Idle)
            {
                p.dy = -5 * (.5f - .5f * Mathf.Cos(ph)); float sq = Mathf.Cos(ph);
                p.sy = 1 - .04f * sq; p.sx = 1 + .035f * sq; p.tilt = 1.6f * Mathf.Sin(ph);
                p.lean = .5f + 1.2f * Mathf.Sin(ph - .5f);
                p.head = -2.5f * Mathf.Sin(ph - 1f); p.pack = -2 * Mathf.Sin(ph - 1.6f);
                p.A = 1 + 3 * Mathf.Sin(ph - .7f); p.Af = -1 - 3 * Mathf.Sin(ph - .7f);
            }
            else if (anim == Anim.Walk)
            {
                float sv = Mathf.Abs(Mathf.Sin(ph)); p.dy = -9 * sv;
                float k = Sm((sv - .15f) / .45f);
                p.sy = .965f + .06f * k; p.sx = 2 - p.sy;
                p.tilt = 3.2f * Mathf.Sin(ph);
                p.lean = 1; p.head = -4 * Mathf.Sin(ph - 1f); p.pack = -4 * Mathf.Sin(ph - 1.6f);
                p.L = 7 * Mathf.Sin(ph) - 3 * sv; p.Lf = -7 * Mathf.Sin(ph) - 3 * sv;
                p.A = -6 * Mathf.Sin(ph - .6f) + 1; p.Af = 6 * Mathf.Sin(ph - .6f) - 1;
            }
            else if (anim == Anim.Shoot) ShootPose(ref p, t);   // 枪械：按枪种（霰弹 / 线圈 / 手炮）各有一套动作，开火都在 t≈.286
            else if (anim == Anim.Smash)   // 重武器：举过头顶 → 砸下（命中 t≈.41）
            {
                p.dx = Kf(t, new[] { 0f, 0, .3f, -8, .42f, 22, .7f, 22, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .25f, -6, .32f, -14, .42f, 0, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .3f, -14, .42f, 20, .7f, 16, 1, 1 });
                p.A = Kf(t, new[] { 0f, 1, .3f, 160, .42f, 70, .7f, 62, 1, 1 });
                p.W = Kf(t, new[] { 0f, 168, .3f, 230, .42f, 60, .7f, 70, 1, 168 });
                p.Af = Kf(t, new[] { 0f, -1, .3f, 150, .42f, 60, .7f, 50, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .3f, 8, .42f, -10, 1, 0 });
                p.pack = Kf(t, new[] { 0f, 0, .3f, 6, .46f, -8, .8f, 3, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .3f, -6, .42f, 26, .7f, 26, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .3f, 8, .42f, -20, .7f, -20, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .25f, .93f, .32f, 1.06f, .42f, .9f, .55f, .97f, .8f, 1, 1, 1 });
                p.sx = 2 - p.sy;
                p.fx = t > .32f && t < .5f;
            }
            else if (anim == Anim.Combo)   // 迅切连击 = three thrusts, the last one a dash; hits at t=.338/.471/.75
            {
                p.dx = Kf(t, new[] { 0f, 0, .16f, -8, .26f, -4, .338f, 20, .40f, 14, .471f, 32, .60f, 18, .75f, 66, .86f, 66, 1, 0 });
                p.A = Kf(t, new[] { 0f, 1, .16f, -18, .26f, -10, .338f, 92, .40f, 30, .471f, 94, .60f, -22, .75f, 96, .86f, 92, 1, 1 });
                p.W = Kf(t, new[] { 0f, 58, .16f, 88, .26f, 86, .338f, 94, .40f, 80, .471f, 94, .60f, 90, .75f, 95, .86f, 92, 1, 58 });
                p.shx = Kf(t, new[] { 0f, 0, .16f, -4, .26f, -3, .338f, 12, .40f, 6, .471f, 13, .60f, -4, .75f, 15, .86f, 13, 1, 0 });
                p.tilt = Kf(t, new[] { 0f, 0, .16f, -6, .338f, 6, .40f, 3, .471f, 7, .60f, -7, .75f, 11, .86f, 9, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .16f, -5, .338f, 6, .40f, 3, .471f, 7, .60f, -6, .75f, 10, .86f, 8, 1, 1 });
                p.Af = Kf(t, new[] { 0f, -1, .16f, 8, .338f, -12, .40f, -6, .471f, -14, .60f, 10, .75f, -20, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .16f, 5, .338f, -5, .471f, -5, .60f, 5, .75f, -7, 1, 0 });
                p.pack = Kf(t, new[] { 0f, 0, .16f, 3, .36f, -4, .48f, -4, .60f, 3, .78f, -6, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .16f, -8, .338f, 20, .40f, 16, .471f, 24, .60f, -8, .75f, 30, .86f, 30, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .16f, 8, .338f, -18, .40f, -14, .471f, -20, .60f, 8, .75f, -26, .86f, -26, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .16f, 3, .338f, 0, .60f, 3, .68f, -6, .75f, 0, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .16f, .92f, .30f, 1.03f, .40f, .97f, .47f, 1.03f, .60f, .91f, .72f, 1.06f, .80f, .95f, .92f, 1, 1, 1 });
                p.sx = 2 - p.sy;
                p.fx = (t > .29f && t < .36f) || (t > .42f && t < .49f) || (t > .66f && t < .80f);
                p.ghost = (t > .28f && t < .50f) || (t > .64f && t < .84f);
                p.glint = (t > .08f && t < .20f) || (t > .54f && t < .62f);
            }
            else
            {
                p.dx = Kf(t, new[] { 0f, 0, .28f, -8, .42f, 26, .62f, 26, 1, 0 });
                p.tilt = Kf(t, new[] { 0f, 0, .28f, -5, .42f, 8, .62f, 6, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .28f, -4, .42f, 6, .62f, 4, 1, 1 });
                p.A = Kf(t, new[] { 0f, 1, .28f, -16, .42f, 92, .62f, 86, 1, 1 });
                p.shx = Kf(t, new[] { 0f, 0, .28f, -3, .42f, 13, .62f, 11, 1, 0 });
                p.W = Kf(t, new[] { 0f, 58, .28f, 92, .42f, 95, .62f, 92, 1, 58 });
                p.Af = Kf(t, new[] { 0f, -1, .28f, 10, .42f, -14, .62f, -12, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .28f, 5, .42f, -6, 1, 0 });
                p.pack = Kf(t, new[] { 0f, 0, .28f, 3, .46f, -5, .7f, 2, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .28f, -8, .42f, 24, .62f, 24, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .28f, 6, .42f, -20, .62f, -20, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .28f, 2, .36f, -6, .46f, 0, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .28f, .95f, .36f, 1.04f, .46f, .95f, .7f, 1, 1, 1 });
                p.sx = 2 - p.sy;
                p.fx = t > .36f && t < .62f;
            }
            // 枪的待机/走路：低位持枪（手臂前抬 24°，枪口朝前略向下），和开枪动作首尾衔接。
            if (IsGun(weaponClass) && (anim == Anim.Idle || anim == Anim.Walk)) { p.A += 24; p.W = 98 + (p.A - 24) * .25f; }
            if (float.IsNaN(p.W)) p.W = IsGun(weaponClass) ? p.A * .5f + 92 : weaponClass == "heavy" ? 168 + p.A * .5f : p.A * .3f + 58;
            return p;
        }

        public const float FireT = .286f;   // = DollAnimation.Windup(远程) / 远程动作时长
        public static bool IsGun(string c) { return c == "gun" || c == "shotgun" || c == "coil" || c == "cannon"; }
        private static void ShootPose(ref Pose p, float t)
        {
            p.fx = false;
            if (weaponClass == "coil")          // 线圈发射器：稳稳端平蓄能（枪身轻颤）→ 放电，后坐很小
            {
                p.A = Kf(t, new[] { 0f, 25, .18f, 84, .286f, 86, .31f, 92, .5f, 86, .8f, 84, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .18f, 90, .286f, 90, .30f, 97, .5f, 91, .8f, 90, 1, 98 });
                p.dx = Kf(t, new[] { 0f, 0, .18f, 3, .286f, 4, .31f, -4, .55f, 0, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .18f, 5, .286f, 6, .31f, -2, .6f, 3, 1, 1 });
                p.shx = Kf(t, new[] { 0f, 0, .18f, 7, .286f, 8, .31f, 3, .6f, 6, 1, 0 });
                p.head = Kf(t, new[] { 0f, 0, .18f, -3, .31f, 3, .6f, 0, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .18f, .98f, .286f, .97f, .31f, 1.02f, .5f, 1, 1, 1 });
                p.L = Kf(t, new[] { 0f, 0, .18f, 5, .6f, 5, 1, 0 }); p.Lf = Kf(t, new[] { 0f, 0, .18f, -4, .6f, -4, 1, 0 });
                if (t > .1f && t < FireT) { float k = (t - .1f) / (FireT - .1f); p.W += Mathf.Sin(t * 190) * 1.6f * k; p.tilt += Mathf.Sin(t * 240) * k; }
            }
            else if (weaponClass == "cannon")   // 逆流手炮：压低重心、炮口上扬 → 巨大后坐把人推后一步 → 回稳
            {
                p.A = Kf(t, new[] { 0f, 25, .16f, 90, .286f, 92, .31f, 116, .5f, 100, .75f, 92, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .16f, 100, .286f, 100, .31f, 126, .5f, 106, .75f, 100, 1, 98 });
                p.dx = Kf(t, new[] { 0f, 0, .16f, 2, .286f, 2, .31f, -18, .55f, -8, .8f, -2, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .16f, 3, .286f, 4, .31f, 1, .45f, 5, .7f, 2, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .16f, 6, .286f, 7, .31f, -14, .55f, -2, .8f, 2, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .286f, 2, .31f, -8, .6f, -1, 1, 0 });
                p.head = Kf(t, new[] { 0f, 0, .16f, -4, .31f, 9, .6f, 0, 1, 0 });
                p.shx = Kf(t, new[] { 0f, 0, .16f, 6, .286f, 7, .31f, -4, .6f, 4, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .16f, .94f, .286f, .93f, .31f, 1.04f, .42f, .92f, .6f, .98f, 1, 1 });
                p.L = Kf(t, new[] { 0f, 0, .16f, 10, .31f, 6, .6f, 8, 1, 0 }); p.Lf = Kf(t, new[] { 0f, 0, .16f, -8, .31f, -10, .6f, -8, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .16f, 4, .31f, -4, 1, -1 });
            }
            else                                 // 勤务短铳（霰弹）/ 其他枪：腰射抬枪 → 猛烈上跳 → 甩枪退壳 → 收回
            {
                p.A = Kf(t, new[] { 0f, 25, .12f, 62, .286f, 66, .32f, 84, .46f, 72, .6f, 58, .75f, 68, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .12f, 95, .286f, 94, .31f, 118, .45f, 98, .6f, 74, .75f, 96, 1, 98 });
                p.dx = Kf(t, new[] { 0f, 0, .12f, 3, .286f, 3, .31f, -12, .5f, -4, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .12f, 1, .31f, 0, .4f, 2, .6f, 0, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .12f, 5, .286f, 5, .31f, -9, .5f, 0, .7f, 2, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .286f, 1, .31f, -5, .55f, 0, 1, 0 });
                p.head = Kf(t, new[] { 0f, 0, .12f, -3, .286f, -3, .31f, 7, .55f, 0, 1, 0 });
                p.shx = Kf(t, new[] { 0f, 0, .12f, 5, .286f, 6, .31f, -2, .55f, 4, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .12f, .97f, .286f, .97f, .31f, 1.03f, .4f, .95f, .55f, 1, 1, 1 });
                p.L = Kf(t, new[] { 0f, 0, .12f, 6, .31f, -4, .6f, 4, 1, 0 }); p.Lf = Kf(t, new[] { 0f, 0, .12f, -6, .31f, 4, .6f, -4, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .12f, 3, .31f, -3, 1, -1 });
            }
            p.sx = 2 - p.sy;
        }

        // ---------- matrices ----------
        private static Matrix4x4 T(Vector2 v) { return Matrix4x4.Translate(new Vector3(v.x, v.y, 0)); }
        private static Matrix4x4 T(float x, float y) { return Matrix4x4.Translate(new Vector3(x, y, 0)); }
        private static Matrix4x4 Rm(float a) { return Matrix4x4.Rotate(Quaternion.Euler(0, 0, -a)); }   // rig.py Rm(a)
        private static Matrix4x4 S(float x, float y) { return Matrix4x4.Scale(new Vector3(x, y, 1)); }

        private struct Bones { public Matrix4x4 torso, head, pack, armN, armF, hand, handF, wpn, legN, legF; }
        private static Bones Build(Pose p)
        {
            var b = new Bones();
            Matrix4x4 root = T(p.dx, p.dy) * Rm(-p.tilt) * S(p.sx, p.sy);
            b.torso = root * T(hip) * Rm(-p.lean);
            b.head = b.torso * T(headOff) * Rm(-p.head) * S(1 / p.sx, 1 / p.sy);   // head keeps its shape
            b.pack = b.torso * T(packOff) * Rm(-p.pack);
            b.armN = b.torso * T(sh.x + p.shx, sh.y) * Rm(p.A);
            b.armF = b.torso * T(shF) * Rm(p.Af);
            b.hand = b.armN * T(handPt);
            b.handF = b.armF * T(handPt);
            b.wpn = b.hand * Rm(p.W - p.A);
            float aN = legNOff.x >= legFOff.x ? p.L : p.Lf, aF = legNOff.x >= legFOff.x ? p.Lf : p.L;
            b.legN = root * T(hip + legNOff) * Rm(aN);
            b.legF = root * T(hip + legFOff) * Rm(aF);
            return b;
        }

        // ---------- drawing ----------
        // Drawn with GL quads in screen space (not GUI.matrix): IMGUI clips rotated/scaled rects incorrectly,
        // which tore the parts apart in the first Unity test.
        private static Material mat, flat;    // flat: texture alpha x vertex colour (solid afterimage)
        private static Texture2D dot;
        private static Matrix4x4 screenM;
        private static readonly float[] ComboHits = { .338f, .471f, .75f };
        private static readonly Color Orange = new Color(1, .29f, .07f);
        private static void Quad(Texture tex, Matrix4x4 M, float w, float h, bool mirror, Color color) { Quad(mat, tex, M, w, h, mirror, color); }
        private static void Quad(Material use, Texture tex, Matrix4x4 M, float w, float h, bool mirror, Color color)
        {
            use.mainTexture = tex; use.SetPass(0);
            Vector3 a = M.MultiplyPoint3x4(new Vector3(0, 0, 0)), b = M.MultiplyPoint3x4(new Vector3(w, 0, 0));
            Vector3 c = M.MultiplyPoint3x4(new Vector3(w, h, 0)), d = M.MultiplyPoint3x4(new Vector3(0, h, 0));
            float u0 = mirror ? 1 : 0, u1 = mirror ? 0 : 1;
            GL.Begin(GL.QUADS); GL.Color(color);
            GL.TexCoord2(u0, 1); GL.Vertex(a);      // texture v=1 is the top row
            GL.TexCoord2(u1, 1); GL.Vertex(b);
            GL.TexCoord2(u1, 0); GL.Vertex(c);
            GL.TexCoord2(u0, 0); GL.Vertex(d);
            GL.End();
        }
        private static void Blit(Matrix4x4 bone, Part part, bool mirror, Color color, Material use = null)
        {
            float px = mirror ? part.size.x - part.pivot.x : part.pivot.x;
            Quad(use ?? mat, part.tex, screenM * bone * T(-px, -part.pivot.y), part.size.x, part.size.y, mirror, color);
        }
        private static void Ghost(Bones g, Color c)
        {
            Blit(g.pack, pack, false, c, flat); Blit(g.legF, legF, false, c, flat); Blit(g.legN, legN, false, c, flat);
            Blit(g.torso, torso, false, c, flat); Blit(g.head, head, false, c, flat); Blit(g.armN, arm, false, c, flat);
            Blit(g.wpn, weapon, false, c, flat); Blit(g.hand, hand, false, c, flat);
        }
        private static void Dot(Vector2 c, float r, Color color)
        { Quad(dot, screenM * T(c.x - r, c.y - r), 2 * r, 2 * r, false, color); }
        private static void Star(Vector2 c, float r, float w, Color color)
        {
            for (int j = 0; j < 4; j++)
            {
                float ang = j * 45 * Mathf.Deg2Rad, rr = j % 2 == 0 ? r : r * .55f;
                Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
                Segment(c - d, c + d, j % 2 == 0 ? w : w * .6f, color);
            }
        }
        private static Vector2 Tip(Bones b) { Vector3 v = b.wpn.MultiplyPoint3x4(new Vector3(tipPt.x, tipPt.y, 0)); return new Vector2(v.x, v.y); }
        private static Vector2 TipAt(Anim anim, float t)
        {
            Vector3 v = Build(Compute(anim, t)).wpn.MultiplyPoint3x4(new Vector3(tipPt.x, tipPt.y, 0));
            return new Vector2(v.x, v.y);
        }
        private static void Segment(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a; float len = d.magnitude; if (len < .01f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Quad(Texture2D.whiteTexture, screenM * T(a) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, ang)) * T(0, -width / 2), len, width, false, color);
        }

        /// feetX/feetY in Ui logical coordinates; heightScale 1 = battle size.
        public static bool Draw(GameModel model, float feetX, float feetY, float heightScale, Anim anim, float t, Color tint)
        {
            if (!Ready()) return false;
            if (Event.current == null || Event.current.type != EventType.Repaint) return true;
            if (mat == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) { Debug.LogError("THREAD ZERO: Sprites/Default shader missing"); return false; }
                mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                Shader fs = Shader.Find("GUI/Text Shader");
                flat = new Material(fs != null ? fs : sh) { hideFlags = HideFlags.HideAndDontSave };
                dot = new Texture2D(64, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f;
                    dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - r) * 6)));
                }
                dot.Apply();
            }
            ApplyLook(model);
            t = anim == Anim.Idle || anim == Anim.Walk ? t - Mathf.Floor(t) : Mathf.Clamp01(t);
            Pose p = Compute(anim, t);
            Bones b = Build(p);
            // logical -> GUI point -> screen pixel (accounts for groups / GUI.matrix of the caller)
            Vector2 o = GUIUtility.GUIToScreenPoint(Ui.P(feetX, feetY));
            Vector2 o2 = GUIUtility.GUIToScreenPoint(Ui.P(feetX + 100, feetY));
            float unit = (o2.x - o.x) / 100f * PixelsPerUnit * heightScale;
            screenM = T(o) * S(unit, unit);
            Color far = new Color(tint.r * .62f, tint.g * .62f, tint.b * .62f, tint.a);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);   // y down, like GUI
            if (p.ghost)                           // orange afterimages while dashing
            {
                Ghost(Build(Compute(anim, Mathf.Max(0, t - .06f))), new Color(Orange.r, Orange.g, Orange.b, .12f * tint.a));
                Ghost(Build(Compute(anim, Mathf.Max(0, t - .03f))), new Color(Orange.r, Orange.g, Orange.b, .22f * tint.a));
            }
            Blit(b.pack, pack, false, tint);
            Blit(b.legF, legF, false, tint);
            Blit(b.armF, arm, true, far);          // far arm: behind torso, mirrored + darkened
            Blit(b.handF, hand, true, far);
            Blit(b.legN, legN, false, tint);
            Blit(b.torso, torso, false, tint);
            Blit(b.head, head, false, tint);
            if (p.fx)                              // tip trail sits behind arm and blade
                for (int i = 0; i < 9; i++)
                {
                    Vector2 a0 = TipAt(anim, Mathf.Max(0, t - i * .009f)), a1 = TipAt(anim, Mathf.Max(0, t - (i + 1) * .009f));
                    float al = .9f * (1 - i / 9f), w = (5 - i * .4f) * .9f;
                    Segment(a0, a1, w, new Color(1, .47f, .16f, al));
                    Segment(a0, a1, w / 3, new Color(1, 1, 1, al * .8f));
                }
            Blit(b.armN, arm, false, tint);
            Blit(b.wpn, weapon, false, tint);
            Blit(b.hand, hand, false, tint);
            // 枪口位置（逻辑坐标）给 WeaponFx 用：子弹 / 光束 / 炮弹都从这里出去。
            {
                Vector2 tp = Tip(b); Vector3 back = b.wpn.MultiplyPoint3x4(new Vector3(tipPt.x, tipPt.y - 6, 0));
                // 骨骼坐标是“部件单位”、以脚底为原点；换成 Ui 逻辑坐标 = 脚底 + 单位 × 每单位逻辑像素。
                Muzzle = new Vector2(feetX, feetY) + tp * PixelsPerUnit * heightScale;
                Vector2 d = tp - new Vector2(back.x, back.y); MuzzleDir = d.sqrMagnitude > .01f ? d.normalized : Vector2.right;
            }
            if (anim == Anim.Shoot && weaponClass == "coil" && t > .08f && t < FireT + .02f)   // 线圈蓄能：电光向枪口收拢
            {
                float k = Mathf.Clamp01((t - .08f) / (FireT - .08f)); Vector2 tp = Tip(b);
                Dot(tp, 4 + 9 * k, new Color(.55f, 1, .95f, .35f + .5f * k));
                for (int i = 0; i < 4; i++)
                {
                    float ang = (t * 900 + i * 90) * Mathf.Deg2Rad, r = 26 * (1 - k) + 6;
                    Vector2 q = tp + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    Segment(q, Vector2.Lerp(q, tp, .5f), 2, new Color(.6f, 1, .95f, .8f));
                }
            }
            if (p.glint)                           // blade flash on wind-up
            {
                float ph = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t < .3f ? (t - .08f) / .12f : (t - .54f) / .08f));
                Vector2 tp = Tip(b);
                Star(tp, 9 * ph, 2, Color.white); Star(tp, 5 * ph, 1, Orange);
            }
            if (anim == Anim.Combo)                // hit sparks: white core + radiating rays, bigger on the finisher
                for (int i = 0; i < 3; i++)
                {
                    float k = (t - ComboHits[i]) / .08f;
                    if (k < 0 || k >= 1) continue;
                    float big = i == 2 ? 1.7f : 1f, fade = 1 - k;
                    Vector2 hp = Tip(Build(Compute(anim, ComboHits[i])));
                    Dot(hp, 7 * (1 - k * .6f) * big, new Color(1, 1, 1, .94f * fade));
                    for (int j = 0; j < 8; j++)
                    {
                        float ang = (j * 45 + 22 * i + (j % 2 == 1 ? 0 : 10)) * Mathf.Deg2Rad;
                        float r0 = (6 + 14 * k) * big, r1 = r0 + (j % 2 == 0 ? 16 : 9) * big * fade;
                        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Segment(hp + dir * r0, hp + dir * r1, 3 * fade * big, new Color(Orange.r, Orange.g, Orange.b, fade));
                    }
                    if (i == 2)
                    {
                        float rx = 10 + 34 * k, ry = rx * .55f;
                        for (int j = 0; j < 24; j++)
                        {
                            float a0 = j / 24f * 2 * Mathf.PI, a1 = (j + 1) / 24f * 2 * Mathf.PI;
                            Segment(hp + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry), hp + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry), 2 * fade, new Color(1, 1, 1, .8f * fade));
                        }
                    }
                }
            GL.PopMatrix();
            return true;
        }

        // ---------- game hookups ----------
        public static bool Enabled { get { return !Input.GetKey(KeyCode.F7); } }   // hold F7 to compare with the old V12 frames
        public static bool CanDraw(GameModel model) { return Enabled && Application.isPlaying && Ready(); }
        /// 普通攻击动作按武器类别：短刃前刺 / 枪械射击 / 重武器下砸。
        public static Anim ClassAttack(GameModel model)
        {
            string c = WeaponClass(model);
            return IsGun(c) ? Anim.Shoot : c == "heavy" ? Anim.Smash : Anim.Attack;
        }

        /// Normalised time for the starter dagger: basic stab (contact at t=.42 = DaggerSkill.BasicHit), or the combo over DaggerSkill.Duration.
        public static float DaggerT(float age, bool combo)
        {
            if (combo) return Mathf.Clamp01(age / DaggerSkill.Duration);
            return Mathf.Clamp01(.42f + (age - DaggerSkill.BasicHit) / .33f);
        }
        public static Anim DaggerAnim(bool combo) { return combo ? Anim.Combo : Anim.Attack; }
    }
}
