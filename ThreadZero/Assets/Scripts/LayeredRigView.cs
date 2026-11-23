using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // V13 layered rigid-part character (S1 starter outfit). Port of Tools/RigPrototype/rig.py.
    // Parts + parts.json live in Resources/Art/V13/Starter. Units: 1 unit = 4 texture px; y points down.
    // Angle convention (same as rig.py): 0 = limb hangs down, positive = swings forward (+x, facing right).
    public static partial class LayeredRigView
    {
        public enum Anim { Idle, Walk, Attack, Combo, Shoot, Smash, Throw, Sweep, Quake, Thrust, Whirl, PointBlank, Overpressure, NailBurst, FlareBolt, ChainArc, HookCombo }   // Throw/Sweep：锁链甩钩 / 横扫；Quake 起：技能专属动作（v0.19）
        /// 技能专属动作：SkillDef.animation → 动作。不在表里的技能还是按武器类别的普攻动作逐段对时（返回 Idle 表示“没有专属动作”）。
        public static Anim SkillAnim(string id)
        {
            switch (id)
            {
                case "quake": return Anim.Quake; case "thrust": return Anim.Thrust; case "whirl": return Anim.Whirl;
                case "pointblank": return Anim.PointBlank; case "overpressure": return Anim.Overpressure; case "nailburst": return Anim.NailBurst;
                case "flarebolt": return Anim.FlareBolt; case "chainarc": return Anim.ChainArc; case "sweep": return Anim.HookCombo;
                default: return Anim.Idle;
            }
        }
        private static float Lin(float t, float[] k) { if (t <= k[0]) return k[1]; for (int i = 0; i + 3 < k.Length; i += 2) if (t <= k[i + 2]) return k[i + 1] + (k[i + 3] - k[i + 1]) * (t - k[i]) / (k[i + 2] - k[i]); return k[k.Length - 1]; }

        [Serializable] public class PartEntry { public string file; public float[] pivot; public float[] hand; public float[] tip; public float[] eye; }
        [Serializable] public class PartSet { public PartEntry head, torso, arm, arm_far, hand, hand_far, leg_near, leg_far, pack, weapon; }
        [Serializable] public class RigOffsets { public float[] hip, head, pack, leg_near, leg_far; }
        [Serializable] public class Manifest { public float scale = 4; public string weapon_class; public float[] shoulder, shoulderF; public PartSet parts; public RigOffsets rig; }
        // 每件装备外观一个文件夹：Resources/Art/V13/Parts/<外观ID>/part.json + 图。只写这件装备负责的部件：
        // 头部 head；外套 torso + arm（可带 shoulder/shoulderF）；背部 pack；武器 weapon（带 tip，weapon_class = dagger/gun/heavy）。
        [Serializable] public class LookFile { public float scale = 4; public string weapon_class, chain; public float[] shoulder, shoulderF, pack_off; public PartEntry head, torso, arm, arm_far, pack, weapon, leg_near, leg_far, hook; }   // chain：锁链类程序画的那段链子的样子（rust 铁链 / fiber 光纤 / cable 线缆，默认铁链）
        private sealed class Look { public Part head, torso, arm, armFar, pack, weapon, legN, legF, hook; public bool hasShoulder, hasPackOff, hasEye; public Vector2 eye; public Vector2 sh, shF, tip, handPt, handPtF, packOff; public string cls, chain; }

        private sealed class Part { public Texture2D tex; public Vector2 pivot, size; }

        // v0.20 机械飞升第一阶段起（飞升度 30%）：两只手换成机械手（Resources/Art/V16/Ascension/，Gemini 卡26）。
        // 手不属于任何装备栏，换了能和所有衣服搭。没有图就照旧用初始手。
        [Serializable] private class AscensionParts { public float scale = 4; public PartEntry hand, hand_far, fins, fins_zero; public float[] fins_at; }
        private const string AscensionFolder = "Art/V16/Ascension/";
        private static Part mechHand, mechHandFar, mechFins, mechFinsZero; private static Vector2 finsAt; private static bool mechTried, mechOn; private static int mechStage;
        private static void LoadMechHands()
        {
            if (mechTried) return; mechTried = true;
            var ta = Resources.Load<TextAsset>(AscensionFolder + "ascension");
            if (ta == null) return;
            var set = JsonUtility.FromJson<AscensionParts>(ta.text);
            if (set == null) return;
            mechHand = Load(AscensionFolder, set.hand, set.scale); mechHandFar = Load(AscensionFolder, set.hand_far, set.scale);
            mechFins = Load(AscensionFolder, set.fins, set.scale); mechFinsZero = Load(AscensionFolder, set.fins_zero, set.scale); finsAt = V(set.fins_at, new Vector2(4, -22));   // 第二阶段：背上的散热鳍，挂在背包骨骼上
        }
        private static Part NearHand { get { return mechOn && mechHand != null ? mechHand : hand; } }
        private static Part FarHand { get { return mechOn && mechHandFar != null ? mechHandFar : handFar ?? hand; } }
        private static bool FarHandMirrored { get { return !(mechOn && mechHandFar != null) && handFar == null; } }

        private const string Folder = "Art/V13/Starter/", PartsFolder = "Art/V13/Parts/";
        private static Look starter;
        private static readonly Dictionary<string, Look> looks = new Dictionary<string, Look>();
        private static readonly HashSet<string> missingReported = new HashSet<string>();
        private static string weaponClass = "dagger";
        public const float PixelsPerUnit = 1.5f;   // ~140 units tall -> ~210 logical px (v0.13 battle scale)
        private static bool loaded, failed;
        public static Vector2 Muzzle, MuzzleDir = Vector2.right;
        /// 义体位置（Ui 逻辑坐标），装义体的提示特效对准这里：近侧肩膀、近侧眼睛。
        public static Vector2 ArmAnchor, EyeAnchor;
        private static readonly Vector2 DefaultEye = new Vector2(17, -27);   // 头部骨骼里近侧眼睛的位置（初始头部；头部 part.json 可用 "eye" 覆盖）
        private static Vector2 EyeInHead = DefaultEye;
        public static string Kind { get { return weaponClass; } }
        // 每次进入 Play 都重新读部件和 part.json。项目若关了“进入播放模式时重载域”，静态缓存会一直留着旧的锚点/尺寸，
        // 换了新图后手和袖子就会错位。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { loaded = false; failed = false; looks.Clear(); missingReported.Clear(); }
        private static Manifest m;
        private static Part handFar;   // 远侧手：单独的图（没有就用近侧手镜像压暗）
        private static Part hookPart;
        private static string chainStyle;   // v0.20 卡27：光纤钩索 fiber、线缆鞭 cable，其余铁链
        private static Part head, torso, arm, armFar, hand, legN, legF, pack, weapon;   // armFar：远侧手臂单独的图（没有就用近侧镜像压暗）
        private static Vector2 starterPackOff;
        private static Vector2 handPtF, handPt, tipPt, hip, headOff, packOff, legNOff, legFOff, sh, shF;

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
            id = Catalog.ArtOf(id);   // v0.18 美术未到的装备借旧装备的图
            Look l;
            if (looks.TryGetValue(id, out l)) return l;
            l = null;
            var json = Resources.Load<TextAsset>(PartsFolder + id + "/part");
            if (json != null)
            {
                var f = JsonUtility.FromJson<LookFile>(json.text);
                string folder = PartsFolder + id + "/";
                l = new Look { head = Load(folder, f.head, f.scale), torso = Load(folder, f.torso, f.scale), arm = Load(folder, f.arm, f.scale), armFar = Load(folder, f.arm_far, f.scale), legN = Load(folder, f.leg_near, f.scale), legF = Load(folder, f.leg_far, f.scale), hook = Load(folder, f.hook, f.scale),
                               pack = Load(folder, f.pack, f.scale), weapon = Load(folder, f.weapon, f.scale), cls = f.weapon_class, chain = f.chain };
                l.hasShoulder = f.shoulder != null && f.shoulder.Length >= 2;
                l.hasEye = f.head != null && f.head.eye != null && f.head.eye.Length >= 2; if (l.hasEye) l.eye = V(f.head.eye, Vector2.zero);
                l.hasPackOff = f.pack_off != null && f.pack_off.Length >= 2; if (l.hasPackOff) l.packOff = V(f.pack_off, Vector2.zero);
                if (l.hasShoulder) { l.sh = V(f.shoulder, Vector2.zero); l.shF = V(f.shoulderF, l.sh); }
                if (l.arm != null) l.handPt = f.arm.hand != null ? V(f.arm.hand, Vector2.zero) - l.arm.pivot : new Vector2(0, l.arm.size.y - 11 - l.arm.pivot.y);
                if (l.armFar != null) l.handPtF = f.arm_far.hand != null ? V(f.arm_far.hand, Vector2.zero) - l.armFar.pivot : new Vector2(0, l.armFar.size.y - 11 - l.armFar.pivot.y);
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
            if (Catalog.ArtOf(w.id) == "w4") return "heavy";
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
            Look lg = LoadLook(model.Appearance(Slot.Legs).id);   // v0.16 下装：近侧腿 + 远侧腿，缺图用初始裤子
            bool pants = lg != null && lg.legN != null && lg.legF != null;
            legN = pants ? lg.legN : starter.legN; legF = pants ? lg.legF : starter.legF;
            head = h != null && h.head != null ? h.head : starter.head;
            EyeInHead = h != null && h.head != null && h.hasEye ? h.eye : DefaultEye;
            bool coat = c != null && c.torso != null;
            torso = coat ? c.torso : starter.torso;
            arm = coat && c.arm != null ? c.arm : starter.arm;
            handPt = coat && c.arm != null ? c.handPt : starter.handPt;
            Look farSrc = coat && c.arm != null ? c : starter;
            armFar = farSrc.armFar; handPtF = armFar != null ? farSrc.handPtF : handPt;
            sh = coat && c.hasShoulder ? c.sh : starter.sh; shF = coat && c.hasShoulder ? c.shF : starter.shF;
            packOff = coat && c.hasPackOff ? c.packOff : starterPackOff;   // 背包贴着这件外套的后背（外套宽窄不同）
            pack = b != null && b.pack != null ? b.pack : starter.pack;
            bool wpn = w != null && w.weapon != null;
            weapon = wpn ? w.weapon : starter.weapon; tipPt = wpn ? w.tip : starter.tip;
            hookPart = wpn ? w.hook : null;   // 锁链类武器：链子程序画，末端吊钩单独一张图
            chainStyle = wpn ? w.chain : null;
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
            hip = V(rg.hip, new Vector2(0, -21)); headOff = V(rg.head, new Vector2(2, -29)); packOff = starterPackOff = V(rg.pack, new Vector2(-21, -20));
            legNOff = V(rg.leg_near, new Vector2(5, 0)); legFOff = V(rg.leg_far, new Vector2(-5, 0));
            sh = V(m.shoulder, new Vector2(11, -26)); shF = V(m.shoulderF, new Vector2(-4, -26));
            armFar = Load(m.parts.arm_far); handFar = Load(m.parts.hand_far);
            handPtF = armFar == null ? handPt : m.parts.arm_far.hand != null ? V(m.parts.arm_far.hand, Vector2.zero) - armFar.pivot : new Vector2(0, armFar.size.y - 11 - armFar.pivot.y);
            starter = new Look { legN = legN, legF = legF, armFar = armFar, handPtF = handPtF, head = head, torso = torso, arm = arm, pack = pack, weapon = weapon, sh = sh, shF = shF, tip = tipPt, handPt = handPt, cls = "dagger" };
            Debug.Log("THREAD ZERO: V13 layered starter rig loaded");
            return true;
        }

        // ---------- pose (identical numbers to rig.py) ----------
        private struct Pose { public float dx, dy, sx, sy, tilt, lean, head, pack, A, Af, L, Lf, W, shx, spin, face, wx, wy, zoom, charge, shake, reach, hook; public bool fx, ghost, glint, hookOn; }
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
            var p = new Pose { sx = 1, sy = 1, lean = 1, A = 8, Af = -8, W = float.NaN, face = 1, zoom = 1 };
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
            else if (HasMove(anim)) Apply(MoveFor(anim), ref p, t);   // v0.20 技能连招：按拍子写的动作表（SkillMoves.cs）
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
            else if (anim == Anim.Throw)   // 锁链甩钩：手往后收 → 往前送出（命中 t≈.42）→ 停一下让链子收回 → 复位
            {
                p.dx = Kf(t, new[] { 0f, 0, .3f, -10, .42f, 14, .62f, 12, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .3f, -8, .42f, 10, .62f, 8, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .3f, -4, .42f, 5, .7f, 3, 1, 0 });
                p.A = Kf(t, new[] { 0f, 1, .3f, -40, .42f, 92, .62f, 88, .85f, 40, 1, 1 });
                p.shx = Kf(t, new[] { 0f, 0, .3f, -4, .42f, 12, .62f, 10, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .3f, 20, .42f, -16, .7f, -10, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .3f, 4, .42f, -4, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .3f, -6, .42f, 18, .7f, 16, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .3f, 6, .42f, -14, .7f, -12, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .3f, .95f, .42f, 1.03f, .55f, .98f, 1, 1 });
                p.sx = 2 - p.sy;
            }
            else if (anim == Anim.Sweep)   // 吊钩横扫：手举过头抡一整圈（命中 t≈.42 时吊钩扫过身前）
            {
                p.A = Kf(t, new[] { 0f, 1, .2f, -50, .32f, 150, .42f, 100, .6f, 60, 1, 1 });
                p.lean = Kf(t, new[] { 0f, 1, .2f, -10, .42f, 14, .7f, 6, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .2f, -8, .42f, 9, .7f, 3, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .2f, 3, .42f, -4, .6f, 0, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .2f, 30, .42f, -30, .7f, -10, 1, -1 });
                p.L = Kf(t, new[] { 0f, 0, .2f, -8, .42f, 16, .7f, 12, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .2f, 8, .42f, -14, .7f, -10, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .2f, .94f, .42f, 1.05f, .6f, .97f, 1, 1 });
                p.sx = 2 - p.sy;
            }
            else if (anim == Anim.Quake)   // 震地：蹲下把锤抡到背后 → 高高跳起 → 整个人砸下来（命中 t≈.58）→ 落地压扁 → 收锤
            {
                p.dx = Kf(t, new[] { 0f, 0, .22f, -4, .40f, 10, .58f, 26, .72f, 24, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .22f, 5, .40f, -58, .50f, -40, .58f, 0, .64f, 4, .76f, 0, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .22f, -10, .40f, -16, .58f, 26, .74f, 18, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .22f, -3, .40f, -6, .58f, 6, .74f, 3, 1, 0 });
                p.A = Kf(t, new[] { 0f, 1, .22f, 150, .40f, 178, .58f, 56, .74f, 60, 1, 1 });
                p.W = Kf(t, new[] { 0f, 168, .22f, 232, .40f, 252, .58f, 38, .74f, 44, 1, 168 });
                p.Af = Kf(t, new[] { 0f, -1, .22f, 140, .40f, 168, .58f, 52, .74f, 46, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .22f, 8, .40f, 10, .58f, -12, .74f, -6, 1, 0 });
                p.pack = Kf(t, new[] { 0f, 0, .30f, 8, .50f, -10, .64f, 6, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .22f, -10, .40f, -24, .58f, 28, .74f, 26, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .22f, 12, .40f, 26, .58f, -24, .74f, -22, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .22f, .86f, .32f, 1.1f, .52f, 1.04f, .60f, .82f, .70f, .98f, 1, 1 });
                p.sx = 2 - p.sy;
                p.ghost = t > .44f && t < .58f;   // 下砸时的残影
                p.fx = t > .48f && t < .66f;      // 锤头轨迹
            }
            else if (anim == Anim.Thrust)  // 贯穿突刺：后撤蓄力（矛尖一闪）→ 整个人冲出去一大段、矛伸到底（命中 t≈.47）→ 停在对面 → 走回原位
            {
                p.dx = Kf(t, new[] { 0f, 0, .30f, -16, .47f, 86, .62f, 92, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .30f, 3, .40f, -5, .47f, 0, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .30f, -9, .47f, 17, .62f, 12, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .30f, -4, .47f, 7, .62f, 5, 1, 0 });
                p.A = Kf(t, new[] { 0f, 1, .30f, -22, .47f, 97, .62f, 92, .85f, 30, 1, 1 });
                p.W = Kf(t, new[] { 0f, 58, .30f, 86, .47f, 93, .62f, 92, .85f, 70, 1, 58 });
                p.shx = Kf(t, new[] { 0f, 0, .30f, -5, .47f, 15, .62f, 13, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .30f, 18, .47f, -24, .62f, -18, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .30f, 5, .47f, -6, 1, 0 });
                p.pack = Kf(t, new[] { 0f, 0, .30f, 4, .50f, -7, .7f, 3, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .30f, -12, .47f, 32, .62f, 30, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .30f, 14, .47f, -30, .62f, -28, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .30f, .9f, .40f, 1.0f, .47f, .94f, .62f, 1, 1, 1 });
                p.sx = Kf(t, new[] { 0f, 1, .30f, 1.08f, .40f, .92f, .47f, 1.1f, .62f, 1, 1, 1 });   // 冲刺时拉长
                p.ghost = t > .30f && t < .55f;
                p.glint = t > .12f && t < .27f;   // 蓄力时矛尖一闪
                p.fx = t > .36f && t < .58f;
            }
            else if (anim == Anim.Whirl)   // 回旋切割：把锯臂抡成风车，连转三圈，每次锯片转到正前方切一下（命中 t≈.24/.40/.56/.72）
            {
                p.A = Lin(t, new[] { 0f, 1, .08f, -60, .24f, 90, .40f, 450, .56f, 810, .72f, 1170, .86f, 1400, 1, 1441 });   // 匀速转，不用缓动
                p.W = p.A + 18;
                p.dx = Kf(t, new[] { 0f, 0, .08f, -6, .24f, 10, .72f, 22, 1, 0 });
                p.dy = -3 * Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .24f) / .16f)));   // 每切一下轻轻一蹬
                p.lean = Kf(t, new[] { 0f, 1, .08f, -6, .24f, 12, .72f, 12, 1, 1 });
                p.tilt = t > .2f && t < .8f ? 4 * Mathf.Sin(2 * Mathf.PI * (t - .24f) / .16f) : Kf(t, new[] { 0f, 0, .08f, -4, .2f, 0, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .08f, 24, .24f, 30, .72f, 30, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .08f, 5, .24f, -4, .72f, -4, 1, 0 });
                p.pack = 3 * Mathf.Sin(2 * Mathf.PI * t * 3);
                p.L = Kf(t, new[] { 0f, 0, .08f, -8, .24f, 18, .72f, 18, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .08f, 8, .24f, -18, .72f, -18, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .08f, .92f, .24f, 1.02f, .72f, 1.02f, .86f, .96f, 1, 1 });
                p.sx = 2 - p.sy;
                p.ghost = t > .2f && t < .8f;
                p.fx = t > .18f && t < .8f;
            }
            else if (anim == Anim.PointBlank)   // 贴脸轰击：冲到怪脸前一枪（t≈.275）→ 推一下泵 → 再一枪（t≈.625）被后坐顶得跳回来
            {
                p.dx = Kf(t, new[] { 0f, 0, .2f, 40, .275f, 34, .45f, 44, .625f, 40, .7f, 22, .85f, 8, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .2f, 3, .3f, 1, .6f, 2, .68f, -9, .8f, 0, 1, 0 });
                p.A = Kf(t, new[] { 0f, 25, .2f, 82, .275f, 96, .38f, 72, .55f, 84, .625f, 100, .72f, 80, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .2f, 92, .275f, 114, .38f, 86, .55f, 92, .625f, 120, .72f, 100, 1, 98 });
                p.Af = Kf(t, new[] { 0f, -1, .2f, 40, .3f, 30, .4f, 62, .5f, 40, .625f, 20, 1, -1 });
                p.lean = Kf(t, new[] { 0f, 1, .2f, 8, .275f, -4, .45f, 6, .625f, -12, .75f, -4, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .2f, 3, .275f, -3, .45f, 2, .625f, -6, .8f, 0, 1, 0 });
                p.head = Kf(t, new[] { 0f, 0, .2f, -3, .275f, 4, .45f, -2, .625f, 6, .8f, 0, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .2f, 22, .45f, 18, .625f, 10, .7f, -8, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .2f, -18, .45f, -14, .625f, -6, .7f, 10, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .2f, .94f, .275f, 1.03f, .4f, .97f, .625f, 1.05f, .7f, .92f, .85f, 1, 1, 1 }); p.sx = 2 - p.sy;
                p.ghost = t > .04f && t < .2f;
            }
            else if (anim == Anim.Overpressure) // 过压弹：单膝跪地、另一只手托住炮管蓄压（炮身发抖）→ 开炮（t≈.494）被后坐推出去一大段 → 站起来
            {
                p.dy = Kf(t, new[] { 0f, 0, .25f, 10, .494f, 10, .6f, 6, .85f, 0, 1, 0 });
                p.dx = Kf(t, new[] { 0f, 0, .25f, 4, .494f, 4, .56f, -30, .7f, -34, .9f, -8, 1, 0 });
                p.A = Kf(t, new[] { 0f, 25, .25f, 88, .494f, 90, .53f, 122, .7f, 98, .9f, 60, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .25f, 96, .494f, 96, .53f, 134, .7f, 104, .9f, 98, 1, 98 });
                p.Af = Kf(t, new[] { 0f, -1, .25f, 78, .494f, 80, .53f, 96, .7f, 70, 1, -1 });
                p.lean = Kf(t, new[] { 0f, 1, .25f, 8, .494f, 10, .53f, -16, .7f, -6, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .494f, 2, .53f, -9, .7f, -3, 1, 0 });
                p.head = Kf(t, new[] { 0f, 0, .25f, -4, .53f, 9, .7f, 2, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .25f, 34, .7f, 30, .9f, 8, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .25f, -50, .7f, -46, .9f, -10, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .25f, .88f, .494f, .86f, .53f, 1, .6f, .9f, .85f, 1, 1, 1 }); p.sx = 2 - p.sy;
                if (t > .15f && t < .494f) { float k = (t - .15f) / .344f; p.W += Mathf.Sin(t * 210) * 1.8f * k; p.tilt += Mathf.Sin(t * 260) * .8f * k; }
                p.ghost = t > .494f && t < .66f;
            }
            else if (anim == Anim.NailBurst)    // 钉射扫射：站稳，枪口从低往高一路扫，连打四钉（t≈.188/.376/.565/.753），每钉一下后坐
            {
                float kick = 0;
                foreach (float h in NailHits) if (t >= h && t < h + .09f) kick = Mathf.Max(kick, 1 - (t - h) / .09f);
                p.W = Kf(t, new[] { 0f, 98, .12f, 82, .8f, 114, 1, 98 }) + 9 * kick;
                p.A = Kf(t, new[] { 0f, 25, .12f, 78, .8f, 104, 1, 25 }) + 5 * kick;
                p.dx = Kf(t, new[] { 0f, 0, .12f, 4, .8f, 0, 1, 0 }) - 4 * kick;
                p.lean = Kf(t, new[] { 0f, 1, .12f, 7, .8f, 4, 1, 1 }) - 3 * kick;
                p.Af = Kf(t, new[] { 0f, -1, .12f, 60, .8f, 70, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .12f, 3, .8f, -4, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .12f, 16, .8f, 16, 1, 0 }); p.Lf = Kf(t, new[] { 0f, 0, .12f, -14, .8f, -14, 1, 0 });
                p.sy = 1 - .03f * kick; p.sx = 2 - p.sy; p.tilt = -2 * kick;
            }
            else if (anim == Anim.FlareBolt)    // 信号爆矢：后仰把弩举高、爆矢点燃（矢头一闪）→ 抛射（t≈.5）→ 抬头看着它落下去
            {
                p.A = Kf(t, new[] { 0f, 25, .2f, 132, .45f, 138, .5f, 150, .65f, 120, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .2f, 138, .45f, 142, .5f, 158, .65f, 124, 1, 98 });
                p.lean = Kf(t, new[] { 0f, 1, .2f, -10, .45f, -12, .5f, -16, .7f, -4, 1, 1 });
                p.dy = Kf(t, new[] { 0f, 0, .2f, 3, .45f, 4, .55f, 0, 1, 0 });
                p.dx = Kf(t, new[] { 0f, 0, .2f, -4, .45f, -4, .5f, -12, .75f, -4, 1, 0 });
                p.Af = Kf(t, new[] { 0f, -1, .2f, 120, .45f, 126, .5f, 100, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .2f, -10, .5f, -12, .75f, 0, 1, 0 });
                p.L = Kf(t, new[] { 0f, 0, .2f, 14, .7f, 12, 1, 0 }); p.Lf = Kf(t, new[] { 0f, 0, .2f, -16, .7f, -14, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .2f, .95f, .45f, .94f, .5f, 1.03f, .65f, .98f, 1, 1 }); p.sx = 2 - p.sy;
                p.glint = t > .22f && t < .44f;
            }
            else if (anim == Anim.ChainArc)     // v0.20 连锁放电：扎稳马步把线圈端平（.16）→ 蓄能，枪身越抖越厉害 → 开火（t≈.46）被后坐顶回去 → 看着电弧跳完 → 排气（.6）→ 收枪
            {
                const float F = .4615f;
                p.L = Kf(t, new[] { 0f, 0, .16f, 20, F, 20, .56f, 24, .85f, 20, 1, 0 });
                p.Lf = Kf(t, new[] { 0f, 0, .16f, -18, F, -18, .56f, -24, .85f, -18, 1, 0 });
                p.sy = Kf(t, new[] { 0f, 1, .16f, .93f, F - .02f, .91f, F + .03f, 1.04f, .56f, .95f, .85f, .96f, 1, 1 }); p.sx = 2 - p.sy;
                p.dx = Kf(t, new[] { 0f, 0, .16f, 4, F, 2, F + .06f, -24, .62f, -20, .85f, -14, 1, 0 });
                p.dy = Kf(t, new[] { 0f, 0, .16f, 4, F, 5, F + .04f, -3, .56f, 3, .85f, 3, 1, 0 });
                p.lean = Kf(t, new[] { 0f, 1, .16f, 8, F, 10, F + .04f, -9, .62f, 2, .85f, 4, 1, 1 });
                p.tilt = Kf(t, new[] { 0f, 0, .16f, 2, F, 2, F + .04f, -6, .62f, -1, 1, 0 });
                p.A = Kf(t, new[] { 0f, 25, .16f, 88, F, 90, F + .04f, 104, .62f, 92, .85f, 86, 1, 25 });
                p.W = Kf(t, new[] { 0f, 98, .16f, 95, F, 95, F + .04f, 118, .62f, 100, .85f, 98, 1, 98 });
                p.Af = Kf(t, new[] { 0f, -1, .16f, 72, F, 76, F + .04f, 58, .62f, 70, .85f, 66, 1, -1 });
                p.head = Kf(t, new[] { 0f, 0, .16f, -4, F, -5, F + .04f, 7, .62f, 0, 1, 0 });
                p.pack = Kf(t, new[] { 0f, 0, .16f, 3, F, 2, F + .06f, -8, .7f, 2, 1, 0 });
                if (t > .16f && t < F) { float k = (t - .16f) / (F - .16f); p.W += Mathf.Sin(t * 260) * 2.4f * k; p.dx += Mathf.Sin(t * 310) * 1.2f * k; p.tilt += Mathf.Sin(t * 200) * .8f * k; }
                p.ghost = t > F && t < F + .1f;
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
            if (float.IsNaN(p.W)) p.W = IsGun(weaponClass) ? p.A * .5f + 92 : weaponClass == "heavy" || weaponClass == "chain" ? 168 + p.A * .5f : p.A * .3f + 58;
            return p;
        }

        public const float FireT = .286f;   // = DollAnimation.Windup(远程) / 远程动作时长
        private static readonly float[] NailHits = { .188f, .376f, .565f, .753f };   // 钉射扫射四钉（= SkillBook.NailBurst 的命中时间 ÷ 时长）
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
            Vector2 pivot = new Vector2(hip.x, hip.y - 18);   // 空翻绕腰转，不是绕脚
            Matrix4x4 flip = p.spin != 0 ? T(pivot) * Rm(-p.spin) * T(-pivot) : Matrix4x4.identity;
            Matrix4x4 root = T(p.dx, p.dy) * flip * Rm(-p.tilt) * S(p.sx * (p.face == 0 ? .001f : p.face), p.sy);   // face = -1 转身朝左
            b.torso = root * T(hip) * Rm(-p.lean);
            b.head = b.torso * T(headOff) * Rm(-p.head) * S(1 / p.sx, 1 / p.sy);   // head keeps its shape
            b.pack = b.torso * T(packOff) * Rm(-p.pack);
            b.armN = b.torso * T(sh.x + p.shx, sh.y) * Rm(p.A);
            b.armF = b.torso * T(shF) * Rm(p.Af);
            b.hand = b.armN * T(handPt);
            b.handF = b.armF * T(handPtF);
            b.wpn = b.hand * Rm(p.W - p.A);
            if (p.wx != 0 || p.wy != 0) b.wpn = T(p.wx, p.wy) * b.wpn;   // 武器抛起来离手
            float aN = legNOff.x >= legFOff.x ? p.L : p.Lf, aF = legNOff.x >= legFOff.x ? p.Lf : p.L;
            b.legN = root * T(hip + legNOff) * Rm(aN);
            b.legF = root * T(hip + legFOff) * Rm(aF);
            return b;
        }

        // ---------- drawing ----------
        // Drawn with GL quads in screen space (not GUI.matrix): IMGUI clips rotated/scaled rects incorrectly,
        // which tore the parts apart in the first Unity test.
        private static Material mat, flat;    // flat: texture alpha x vertex colour (solid afterimage)
        private static Texture2D dot, soft;   // soft：边缘慢慢淡掉的圆（光晕用）
        private static Matrix4x4 screenM;
        private static readonly float[] ComboHits = { .22f, .36f, .55f, .74f };   // = DaggerSkill.Hits ÷ Duration
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
            Blit(g.wpn, weapon, false, c, flat); Blit(g.hand, NearHand, false, c, flat);
        }
        private static void Dot(Vector2 c, float r, Color color)
        { Quad(dot, screenM * T(c.x - r, c.y - r), 2 * r, 2 * r, false, color); }
        private static void Glow(Vector2 c, float r, Color color)
        { Quad(soft, screenM * T(c.x - r, c.y - r), 2 * r, 2 * r, false, color); }
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
        // 义体臂：肩、肘、腕三个发光关节，沿手臂两条青绿「线」一明一暗地流动。
        private static void MechArmGlow(Bones b)
        {
            Vector3 s3 = b.armN.MultiplyPoint3x4(Vector3.zero), w3 = b.hand.MultiplyPoint3x4(Vector3.zero);
            Vector2 sh = new Vector2(s3.x, s3.y), wr = new Vector2(w3.x, w3.y), el = Vector2.Lerp(sh, wr, .5f);
            Vector2 n = (wr - sh).sqrMagnitude > .01f ? new Vector2(-(wr - sh).y, (wr - sh).x).normalized : Vector2.right;
            float now = Time.realtimeSinceStartup, pulse = .55f + .45f * Mathf.Sin(now * 5);
            Color line = new Color(.3f, 1, .88f, .75f), glow = new Color(.3f, 1, .88f, .35f * pulse);
            Segment(sh + n * 2.2f, wr + n * 2.2f, 1.2f, line);
            Segment(sh - n * 2.2f, wr - n * 2.2f, 1.2f, new Color(.3f, 1, .88f, .45f));
            float flow = now * 1.4f % 1;   // 一个亮点沿线从肩流到腕
            Dot(Vector2.Lerp(sh, wr, flow) + n * 2.2f, 2.2f, new Color(.85f, 1, .97f, .9f));
            foreach (Vector2 j in new[] { sh, el, wr })
            {
                Dot(j, 6.5f, glow);
                Dot(j, 3.2f, new Color(.12f, .16f, .18f, 1));
                Dot(j, 2f, new Color(.4f, 1, .9f, .6f + .4f * pulse));
            }
        }
        // 中继脊柱：沿后背（外套后沿）四节椎节，深色底板 + 青色芯，一个亮点从腰往脖子流；脖子根一个接口。
        private static readonly Vector2 SpineLow = new Vector2(-16.5f, -13), SpineHigh = new Vector2(-12.5f, -31);
        private static void SpineGlow(Bones b)
        {
            float now = Time.realtimeSinceStartup, flow = now * .9f % 1;
            Vector2 prev = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                float k = i / 3f; Vector2 l = Vector2.Lerp(SpineLow, SpineHigh, k);
                Vector3 c3 = b.torso.MultiplyPoint3x4(new Vector3(l.x, l.y, 0)); Vector2 c = new Vector2(c3.x, c3.y);
                if (i > 0) Segment(prev, c, 1.1f, new Color(.3f, 1, .88f, .55f));
                float lit = Mathf.Clamp01(1 - Mathf.Abs(flow - k) * 4);
                Dot(c, 3.0f, new Color(.1f, .13f, .15f, 1));
                Dot(c, 2.2f, new Color(.55f, .6f, .62f, 1));
                Dot(c, 1.3f, new Color(.3f + .6f * lit, 1, .88f + .1f * lit, .65f + .35f * lit));
                prev = c;
            }
            Vector3 n3 = b.torso.MultiplyPoint3x4(new Vector3(SpineHigh.x + 1.5f, SpineHigh.y - 3.5f, 0));
            Dot(new Vector2(n3.x, n3.y), 2.4f, new Color(.1f, .13f, .15f, 1));
            Dot(new Vector2(n3.x, n3.y), 1.3f, new Color(1, .42f, .12f, .9f));
        }
        // 调度义眼：近侧眼睛位置一枚橙色镜头（压在面罩 / 护目镜上），外圈刻度慢慢转，镜头一明一暗。
        private static void EyeGlow(Bones b)
        {
            Vector3 c3 = b.head.MultiplyPoint3x4(new Vector3(EyeInHead.x, EyeInHead.y, 0)); Vector2 c = new Vector2(c3.x, c3.y);
            float now = Time.realtimeSinceStartup, pulse = .6f + .4f * Mathf.Sin(now * 3.2f);
            Color hot = new Color(1, .42f, .12f);
            Dot(c, 8.5f, new Color(hot.r, hot.g, hot.b, .28f * pulse));                       // 光晕
            Dot(c, 5.4f, new Color(.08f, .09f, .1f, 1));                                       // 镜筒
            Dot(c, 4f, new Color(.55f, .58f, .6f, 1));
            Dot(c, 3f, new Color(.1f, .1f, .12f, 1));
            Dot(c, 2.1f, new Color(hot.r, hot.g, hot.b, .75f + .25f * pulse));                 // 镜头
            Dot(c + new Vector2(-.8f, -.8f), .7f, new Color(1, .95f, .85f, .9f));
            for (int i = 0; i < 4; i++)                                                          // 转动的刻度
            {
                float a = now * .9f + i * Mathf.PI / 2; Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Segment(c + d * 4.6f, c + d * 6.2f, .8f, new Color(.95f, .55f, .25f, .8f));
            }
            Segment(c + new Vector2(5.4f, 0), c + new Vector2(11, 3.5f), .9f, new Color(.3f, 1, .88f, .6f));   // 接到耳后的一根「线」
        }
        // ---------- 锁链武器（吊臂钩索）----------
        // 链子不是硬杆：待机 / 走路时吊钩垂在手下，轻轻晃；出手时吊钩追着「硬武器的尖端」走但慢半拍（0.08 秒），
        // 链子始终绷直成固定长度 —— 抡起来先甩出去，落下时吊钩最后砸到。链环程序画，吊钩用单独的图。
        // 待机时只放出一小段链子，吊钩垂在手边不碰地；出手时把链子放长甩出去（长度随动作 0→1→0）
        private const float ChainIdle = 4, ChainOut = 30, HookScale = .55f;
        /// 甩钩时链子要放多长（逻辑像素，手到目标的距离；行动页每帧按当前目标设置）。
        public static float ChainReachPx = 260;
        private static float lastScale = 1;
        private static Vector2 DrawChain(Bones b, Anim anim, float t, Color tint)
        {
            Vector3 h3 = b.hand.MultiplyPoint3x4(Vector3.zero); Vector2 hand = new Vector2(h3.x, h3.y);
            float now = Time.realtimeSinceStartup;
            Vector2 dir;
            if (anim == Anim.Idle || anim == Anim.Walk)
            {
                float sway = (Mathf.Sin(now * 2.1f) * 7 + (anim == Anim.Walk ? Mathf.Sin(t * Mathf.PI * 2) * 12 : 0)) * Mathf.Deg2Rad;
                dir = new Vector2(-Mathf.Sin(sway), Mathf.Cos(sway));
            }
            else
            {
                Vector2 lag = TipAt(anim, Mathf.Max(0, t - .08f)) - hand;
                dir = lag.sqrMagnitude > .01f ? lag.normalized : new Vector2(0, 1);
                if (t < .05f) dir = Vector2.Lerp(new Vector2(0, 1), dir, t / .05f).normalized;
            }
            bool idle = anim == Anim.Idle || anim == Anim.Walk;
            float len = idle ? ChainIdle : Mathf.Lerp(ChainIdle + 4, ChainOut, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
            if (anim == Anim.Throw)
            {   // 甩钩：吊钩沿直线飞向目标（链子放到目标的距离），命中后停一下再收回来
                float reach = Mathf.Max(ChainIdle + 6, ChainReachPx / (PixelsPerUnit * Mathf.Max(.1f, lastScale)));
                len = Kf(t, new[] { 0f, ChainIdle, .3f, ChainIdle, .42f, reach, .56f, reach, .82f, ChainIdle, 1, ChainIdle });
                float droop = t > .42f && t < .82f ? (t - .42f) / .4f * .35f : 0;   // 收回时链子往下垂一点
                dir = new Vector2(1, .06f + droop).normalized;
            }
            else if (anim == Anim.Sweep)
            {   // 横扫：吊钩绕着手画一整圈（身后 → 头顶 → 身前贴地 → 收回），链子放到最长
                float sw = Kf(t, new[] { 0f, 0, .2f, 140, .32f, 220, .42f, 300, .6f, 380, .85f, 360, 1, 360 }) * Mathf.Deg2Rad;
                dir = new Vector2(-Mathf.Sin(sw), Mathf.Cos(sw));
                len = Kf(t, new[] { 0f, ChainIdle, .2f, ChainOut + 14, .6f, ChainOut + 14, .85f, ChainIdle, 1, ChainIdle });
            }
            if (!idle)
            {   // v0.20 动作表里写了 hook / reach 的（吊钩连招）：链子方向和长度按表走。reach < 0 = 放到目标那么远的几成
                Pose cp = Compute(anim, t);
                if (cp.hookOn)
                {
                    float hk = cp.hook * Mathf.Deg2Rad; dir = new Vector2(-Mathf.Sin(hk), Mathf.Cos(hk)); if (cp.face < 0) dir.x = -dir.x;
                    float toTarget = Mathf.Max(ChainIdle + 6, ChainReachPx / (PixelsPerUnit * Mathf.Max(.1f, lastScale)));
                    len = cp.reach > 0 ? cp.reach : cp.reach < 0 ? toTarget * -cp.reach : len;
                }
            }
            Vector2 end = hand + dir * len;
            // 链子微微下垂：中点往重力方向偏，绷得越平越不垂
            Vector2 sag = new Vector2(0, 3.5f * Mathf.Abs(dir.x));
            Vector2 ctrl = (hand + end) * .5f + sag;
            int n = Mathf.Max(3, Mathf.RoundToInt(len / 2.4f)); Vector2 prev = hand;
            bool fiber = chainStyle == "fiber", cable = chainStyle == "cable";
            Color dark = fiber ? new Color(.1f * tint.r, .11f * tint.g, .13f * tint.b, tint.a) : cable ? new Color(.06f * tint.r, .06f * tint.g, .07f * tint.b, tint.a) : new Color(.12f * tint.r, .1f * tint.g, .09f * tint.b, tint.a);
            Color rust = fiber ? new Color(.3f * tint.r, .33f * tint.g, .37f * tint.b, tint.a) : cable ? new Color(.17f * tint.r, .17f * tint.g, .19f * tint.b, tint.a) : new Color(.62f * tint.r, .38f * tint.g, .24f * tint.b, tint.a);
            Color hi = fiber || cable ? new Color(.45f, 1, .95f, .9f * tint.a) : new Color(.9f, .7f, .5f, .8f * tint.a);
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n; Vector2 q = (1 - u) * (1 - u) * hand + 2 * (1 - u) * u * ctrl + u * u * end;
                if (fiber || cable)
                {   // 光纤：深灰外皮 + 一道细青光，光点沿着线缆往钩头流；线缆：黑胶皮，隔一段一个青色小灯
                    Segment(prev, q, cable ? 4f : 3.4f, dark); Segment(prev, q, cable ? 2.6f : 2f, rust);
                    if (fiber)
                    {
                        Segment(prev, q, .6f, new Color(.45f, 1, .95f, .5f * tint.a));
                        float flow = Mathf.Repeat(u * 3 - Time.unscaledTime * 1.6f, 1);
                        if (flow < .12f) Glow((prev + q) * .5f, 2.6f, new Color(.45f, 1, .95f, (.12f - flow) / .12f * .8f * tint.a));
                    }
                    else if (i % 6 == 3) Dot((prev + q) * .5f, 1f, hi);
                }
                else
                {
                    Segment(prev, q, 3.6f, dark); Segment(prev, q, i % 2 == 0 ? 2.2f : 1.4f, rust);
                    if (i % 2 == 0) Dot((prev + q) * .5f, .55f, hi);
                }
                prev = q;
            }
            Vector2 tan = end - ((1 - .9f) * (1 - .9f) * hand + 2 * .1f * .9f * ctrl + .81f * end); if (tan.sqrMagnitude < .0001f) tan = dir;
            float ang = Mathf.Atan2(-tan.x, tan.y) * Mathf.Rad2Deg;
            Matrix4x4 hm = T(end) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, ang)) * Matrix4x4.Scale(new Vector3(HookScale, HookScale, 1));
            GearEdge(hm, hookPart, tint); Blit(hm, hookPart, false, tint); GearSweep(hm, hookPart, tint);
            hookM = hm; hookTip = new Vector2(0, hookPart.size.y - hookPart.pivot.y);
            Vector3 tip3 = hm.MultiplyPoint3x4(new Vector3(0, hookPart.size.y - hookPart.pivot.y, 0));
            return new Vector2(tip3.x, tip3.y);   // 吊钩尖端：当作「枪口 / 锤头」位置给特效用
        }
        // v0.20 机械飞升第三阶段（90%）：后脑接口拉出三根线缆垂到背包上沿。程序画，两头跟着头和背包的骨骼走，
        //       动作里头一转线缆就跟着弯；外皮深色，里面一道青绿光纤，有一点光沿着线缆往下流。
        private static void NeckCables(Bones b, Color tint)
        {
            Vector3 h3 = b.head.MultiplyPoint3x4(new Vector3(-20, -12, 0)), p3 = b.pack.MultiplyPoint3x4(new Vector3(-9, -28, 0));
            Vector2 a = new Vector2(h3.x, h3.y), e = new Vector2(p3.x, p3.y);
            Color dark = new Color(.07f * tint.r, .08f * tint.g, .09f * tint.b, tint.a), dim = new Color(.18f, .62f, .56f, tint.a), glow = new Color(.55f, 1f, .92f, tint.a);
            float time = Time.unscaledTime;
            for (int c = 0; c < 3; c++)
            {
                // 三根从后脑沿头发边缘往上排开、插到背包顶上往后排开；中间往背后垂下来（整个人约 150 单位高，垂度要够大才看得出是线缆）
                Vector2 s0 = a + new Vector2(-c * 3.5f, -c * 5f), t0 = e + new Vector2(-c * 5f, c * 3f);
                Vector2 ctrl = (s0 + t0) * .5f + new Vector2(-26 - c * 7f, 16 + c * 6f);
                Vector2 prev = s0; const int n = 12;
                float pulse = Mathf.Repeat(time * .7f + c * .37f, 1.4f);
                for (int i = 1; i <= n; i++)
                {
                    float u = i / (float)n; Vector2 q = (1 - u) * (1 - u) * s0 + 2 * (1 - u) * u * ctrl + u * u * t0;
                    Segment(prev, q, c == 0 ? 4.8f : 4f, dark);
                    Segment(prev, q, 1.7f, Color.Lerp(dim, glow, Mathf.Clamp01(1 - Mathf.Abs(u - pulse) * 5)));
                    prev = q;
                }
            }
            for (int c = 0; c < 3; c++) { Vector2 t0 = e + new Vector2(-c * 5f, c * 3f); Dot(t0, 2.6f, dark); Dot(t0, 1.3f, glow); }   // 背包上的三个插口（后脑那头被头发盖住）
        }
        // v0.20 飞升完成（100%）：全身光纹。近侧腿、躯干上各一两道电路一样的细光线（直线 + 45° 拐角，末端一个节点），
        // 一个亮点从脚往上依次流过腿 → 躯干，流到哪段哪段亮；身后一圈青光一明一暗、几粒光点往上飘；散热鳍从红色变成青色（fins_zero.png）。
        // 坐标是各骨骼自己的局部坐标（部件单位）。
        // 手臂不画：100% 时五个义体都满级，近侧手臂一定是机械臂，它自己有发光关节。
        private static readonly Vector2[] TraceLeg = { new Vector2(1.5f, 4), new Vector2(1.5f, 11), new Vector2(3.5f, 13), new Vector2(3.5f, 20) };
        private static readonly Vector2[] TraceTorso = { new Vector2(3, -2), new Vector2(3, -9), new Vector2(6, -12), new Vector2(6, -19) };
        private static readonly Vector2[] TraceTorso2 = { new Vector2(-3, -3), new Vector2(-3, -11), new Vector2(-1, -13), new Vector2(-1, -17) };
        private static float BodyFlow { get { return Mathf.Repeat(Time.unscaledTime * .45f, 1.25f); } }   // 0→1 流完全身，1→1.25 停一下
        private static void Trace(Matrix4x4 bone, Vector2[] pts, float from, float to, Color tint)
        {
            float flow = BodyFlow, k = (flow - from) / (to - from);   // 亮点走到这条线的哪儿（0..1）
            bool on = k > -.2f && k < 1.25f;
            Color dim = new Color(.25f, .85f, .8f, .7f * tint.a), hot = new Color(.85f, 1, .98f, tint.a);
            Vector2 prev = P(bone, pts[0]);
            Dot(prev, .75f, dim);
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 q = P(bone, pts[i]);
                float u = (i - .5f) / (pts.Length - 1), lit = on ? Mathf.Clamp01(1 - Mathf.Abs(u - k) * 3.2f) : 0;
                Segment(prev, q, 2.6f, new Color(.3f, 1, .9f, (.14f + .3f * lit) * tint.a));   // 光晕
                Segment(prev, q, 1f, Color.Lerp(dim, hot, lit));
                prev = q;
            }
            float endLit = on ? Mathf.Clamp01(1 - Mathf.Abs(1 - k) * 3.2f) : 0;
            Glow(prev, 3.2f, new Color(.3f, 1, .9f, (.2f + .5f * endLit) * tint.a));
            Dot(prev, 1.5f, new Color(.08f, .1f, .12f, tint.a));
            Dot(prev, 1f, Color.Lerp(dim, hot, endLit));
        }
        private static Vector2 P(Matrix4x4 bone, Vector2 p) { Vector3 v = bone.MultiplyPoint3x4(new Vector3(p.x, p.y, 0)); return new Vector2(v.x, v.y); }
        private static void Motes(Bones b, Color tint)
        {   // 整个人大约 150 个部件单位高（Q 版大头：胯 26、脖子 65、头顶约 150），光晕和光点按这个尺寸摆
            float now = Time.unscaledTime, breath = .5f + .5f * Mathf.Sin(now * 1.6f); Vector2 c = P(b.torso, new Vector2(-4, -48));
            Glow(c, 105, new Color(.25f, 1, .88f, (.13f + .06f * breath) * tint.a));   // 身后一圈青色的光，慢慢一明一暗
            Glow(c, 62, new Color(.5f, 1, .95f, (.09f + .05f * breath) * tint.a));
            for (int i = 0; i < 10; i++)
            {
                float ph = Mathf.Repeat(now * .22f + i / 10f, 1);   // 往上飘的进度（脚边 → 头顶上方）
                float x = (Mathf.Repeat(i * .618f, 1) - .5f) * 96 + Mathf.Sin(now * 1.3f + i * 2.1f) * 4;   // 左右各 50 单位以内，衣柜大图里也不出框
                Vector2 q = c + new Vector2(x, 72 - ph * 170);
                float a = Mathf.Sin(ph * Mathf.PI) * tint.a;
                Glow(q, 7, new Color(.4f, 1, .9f, .55f * a)); Dot(q, 1.8f, new Color(.9f, 1, .98f, .95f * a));
            }
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
            lastScale = heightScale;
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
                soft = new Texture2D(64, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float r = Mathf.Clamp01(new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f);
                    soft.SetPixel(x, y, new Color(1, 1, 1, (1 - r) * (1 - r)));
                }
                soft.Apply();
            }
            ApplyLook(model); ReadGear(model);
            t = anim == Anim.Idle || anim == Anim.Walk ? t - Mathf.Floor(t) : Mathf.Clamp01(t);
            Pose p = Compute(anim, t);
            SkillZoom = p.zoom; SkillCharge = p.charge;
            RootPos = new Vector2(feetX, feetY) + new Vector2(p.dx, p.dy) * PixelsPerUnit * heightScale;   // 脚底实际位置（落地扬尘用）
            Bones b = Build(p);
            // logical -> GUI point -> screen pixel (accounts for groups / GUI.matrix of the caller)
            Vector2 o = GUIUtility.GUIToScreenPoint(Ui.P(feetX, feetY));
            Vector2 o2 = GUIUtility.GUIToScreenPoint(Ui.P(feetX + 100, feetY));
            float unit = (o2.x - o.x) / 100f * PixelsPerUnit * heightScale;
            screenM = T(o) * S(unit, unit);
            Color far = new Color(tint.r * .62f, tint.g * .62f, tint.b * .62f, tint.a);
            // v0.16b 义体装在持械的近侧手臂上（远侧手臂大多藏在身体后面，看不出来）：冷钢色 + 青绿发光关节 + 线缆。
            bool mech = model != null && model.state.implants.Contains("arm_l");
            mechStage = model != null ? model.AscensionStage : 0; mechOn = mechStage >= 1; if (mechOn) LoadMechHands();
            Color armTint = mech ? new Color(tint.r * .62f, tint.g * .74f, tint.b * .86f, tint.a) : tint;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);   // y down, like GUI
            if (p.ghost)                           // afterimages while dashing: orange, electric skills cyan
            {
                Color gc = anim == Anim.ChainArc ? new Color(.45f, 1, .92f) : qWeapon >= 4 ? QualityVisual.Colors[qWeapon] : Orange;   // v0.20 机密起冲刺残影换成武器品质色
                Ghost(Build(Compute(anim, Mathf.Max(0, t - .06f))), new Color(gc.r, gc.g, gc.b, .12f * tint.a));
                Ghost(Build(Compute(anim, Mathf.Max(0, t - .03f))), new Color(gc.r, gc.g, gc.b, .22f * tint.a));
            }
            if (mechStage >= 4) Motes(b, tint);   // v0.20 飞升完成：身体后面往上飘的光点
            if (mechStage >= 2 && mechFins != null) Blit(b.pack * T(finsAt), mechStage >= 4 && mechFinsZero != null ? mechFinsZero : mechFins, false, tint);   // v0.20 飞升第二阶段：散热鳍画在背包后面，从背包左边露出来
            ArmorEdges(b, tint);   // 11-23 防具的品质光边：画在整个人后面，只露出轮廓外的一圈
            Blit(b.pack, pack, false, tint); GearScan(b.pack, pack, false, qBack, tint);
            Blit(b.legF, legF, false, tint); GearScan(b.legF, legF, false, qLegs, tint);
            if (mechStage >= 4) Trace(b.legF, TraceLeg, .06f, .44f, new Color(1, 1, 1, tint.a * .55f));   // 远侧腿也有一道（压暗，被近侧腿挡住一半）
            Blit(b.armF, armFar ?? arm, armFar == null, far);   // 远侧手臂：有单独的图就用（已经画成远侧，不镜像）；没有就近侧镜像压暗
            GearScan(b.armF, armFar ?? arm, armFar == null, qCoat, far);
            Blit(b.handF, FarHand, FarHandMirrored, far);
            Blit(b.legN, legN, false, tint); GearScan(b.legN, legN, false, qLegs, tint);
            if (mechStage >= 4) Trace(b.legN, TraceLeg, 0, .38f, tint);   // 腿上的光纹画在躯干前面，上端被外套盖住
            Blit(b.torso, torso, false, tint); GearScan(b.torso, torso, false, qCoat, tint);
            if (mechStage >= 4) { Trace(b.torso, TraceTorso, .34f, 1, tint); Trace(b.torso, TraceTorso2, .4f, 1, tint); }
            if (model != null && model.state.implants.Contains("spine")) SpineGlow(b);   // 第三章义体：后背一列发光的中继椎节
            if (mechStage >= 3) NeckCables(b, tint);   // v0.20 飞升第三阶段：后脑接出的线缆（画在身体上面、头下面，线缆根部被头盖住）
            Blit(b.head, head, false, tint); GearScan(b.head, head, false, qHead, tint);
            if (p.fx)                              // tip trail sits behind arm and blade
                for (int i = 0; i < 9; i++)
                {
                    Vector2 a0 = TipAt(anim, Mathf.Max(0, t - i * .009f)), a1 = TipAt(anim, Mathf.Max(0, t - (i + 1) * .009f));
                    float al = .9f * (1 - i / 9f), w = (5 - i * .4f) * .9f;
                    Color tc = GearColor(new Color(1, .47f, .16f));   // v0.20 特装起刀光换成武器品质色
                    Segment(a0, a1, w, new Color(tc.r, tc.g, tc.b, al));
                    Segment(a0, a1, w / 3, new Color(1, 1, 1, al * .8f));
                }
            Blit(b.armN, arm, false, armTint); GearScan(b.armN, arm, false, qCoat, armTint);
            Vector2 chainEnd = Vector2.zero; bool chained = weaponClass == "chain" && hookPart != null;
            if (chained) chainEnd = DrawChain(b, anim, t, tint);
            else { GearTrail(anim, t, tint); GearEdge(b.wpn, weapon, tint); Blit(b.wpn, weapon, false, tint); GearSweep(b.wpn, weapon, tint); }   // v0.20 装备光效
            Blit(b.hand, NearHand, false, armTint);
            if (chained) GearMotes(hookM, hookTip, tint); else GearMotes(b.wpn, tipPt, tint);
            if (mech) MechArmGlow(b);
            if (model != null && model.state.implants.Contains("eye_r")) EyeGlow(b);
            {
                Vector3 s3 = b.armN.MultiplyPoint3x4(new Vector3(0, 8, 0)), e3 = b.head.MultiplyPoint3x4(new Vector3(EyeInHead.x, EyeInHead.y, 0));
                ArmAnchor = new Vector2(feetX, feetY) + new Vector2(s3.x, s3.y) * PixelsPerUnit * heightScale;
                EyeAnchor = new Vector2(feetX, feetY) + new Vector2(e3.x, e3.y) * PixelsPerUnit * heightScale;
            }
            // 枪口位置（逻辑坐标）给 WeaponFx 用：子弹 / 光束 / 炮弹都从这里出去。
            {
                Vector2 tp = chained ? chainEnd : Tip(b); Vector3 back = b.wpn.MultiplyPoint3x4(new Vector3(tipPt.x, tipPt.y - 6, 0));
                // 骨骼坐标是“部件单位”、以脚底为原点；换成 Ui 逻辑坐标 = 脚底 + 单位 × 每单位逻辑像素。
                Muzzle = new Vector2(feetX, feetY) + tp * PixelsPerUnit * heightScale;
                Vector2 d = tp - new Vector2(back.x, back.y); MuzzleDir = d.sqrMagnitude > .01f ? d.normalized : Vector2.right;
            }
            bool coilCharge = anim == Anim.Shoot && weaponClass == "coil" && t > .08f && t < FireT + .02f, arcCharge = false;   // v0.20 连锁放电的蓄能改由 WeaponFx.DrawCharge 画
            if (coilCharge || arcCharge)   // 线圈蓄能：电光向枪口收拢（连锁放电举过头顶时更久）
            {
                float k = coilCharge ? Mathf.Clamp01((t - .08f) / (FireT - .08f)) : Mathf.Clamp01((t - .12f) / .34f); Vector2 tp = Tip(b);
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
                Star(tp, 9 * ph, 2, Color.white); Star(tp, 5 * ph, 1, GearColor(Orange));
            }
            if (anim == Anim.Combo)                // hit sparks: white core + radiating rays, bigger on the finisher
                for (int i = 0; i < ComboHits.Length; i++)
                {
                    float k = (t - ComboHits[i]) / .08f;
                    if (k < 0 || k >= 1) continue;
                    float big = i == ComboHits.Length - 1 ? 1.7f : 1f, fade = 1 - k;
                    Vector2 hp = Tip(Build(Compute(anim, ComboHits[i])));
                    Dot(hp, 7 * (1 - k * .6f) * big, new Color(1, 1, 1, .94f * fade));
                    for (int j = 0; j < 8; j++)
                    {
                        float ang = (j * 45 + 22 * i + (j % 2 == 1 ? 0 : 10)) * Mathf.Deg2Rad;
                        float r0 = (6 + 14 * k) * big, r1 = r0 + (j % 2 == 0 ? 16 : 9) * big * fade;
                        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Color rc = GearColor(Orange); Segment(hp + dir * r0, hp + dir * r1, 3 * fade * big, new Color(rc.r, rc.g, rc.b, fade));
                    }
                    if (i == ComboHits.Length - 1)
                    {
                        float rx = 10 + 34 * k, ry = rx * .55f;
                        for (int j = 0; j < 24; j++)
                        {
                            float a0 = j / 24f * 2 * Mathf.PI, a1 = (j + 1) / 24f * 2 * Mathf.PI;
                            Segment(hp + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry), hp + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry), 2 * fade, new Color(1, 1, 1, .8f * fade));
                        }
                    }
                }
            ModFx(b, tint);   // v0.21 改装件的光
            GL.PopMatrix();
            return true;
        }

        // ---------- game hookups ----------
        public static bool Enabled { get { return true; } }   // 旧的「按住 F7 看 V12 旧形象」对比已去掉
        public static bool CanDraw(GameModel model) { return Enabled && Application.isPlaying && Ready(); }
        /// 普通攻击动作按武器类别：短刃前刺 / 枪械射击 / 重武器下砸。
        public static Anim ClassAttack(GameModel model)
        {
            string c = WeaponClass(model);
            return IsGun(c) ? Anim.Shoot : c == "chain" ? Anim.Throw : c == "heavy" ? Anim.Smash : Anim.Attack;
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
