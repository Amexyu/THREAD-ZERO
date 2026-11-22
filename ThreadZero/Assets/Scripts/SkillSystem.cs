using System.Collections.Generic;

namespace ThreadZero
{
    // 装备技能系统：技能来自武器（模板 + 品质），角色没有固有技能。
    // 新武器加技能 = 在 SkillBook 里写一条 SkillDef 并登记到武器上；战斗逻辑（CombatSession.Skill*）不用改。
    // 动作/特效按 SkillDef.animation 去找对应的动作模组（LayeredRigView），每件高品质武器要有自己的模组。

    public enum SkillTrigger
    {
        Cooldown,       // 冷却好了就放：cooldown 秒
        EveryNAttacks,  // 每打 attacks 次普攻，下一次出手放技能
        Chance,         // 每次出手有 chance 概率放技能；连续 pity 次没出必出（0 = 不保底）
    }

    public sealed class SkillDef
    {
        public string id, displayName, animation;
        public SkillTrigger trigger;
        public float cooldown;
        public int attacks;
        public float chance;
        public int pity;
        public float duration;                // 整个技能动作占用的时间（秒），期间不普攻
        public float[] hitTimes;              // 每段命中的时间点（秒，从发动起算）
        public float[] hitDamage;             // 每段伤害 = 攻击力 × 倍率
        public bool canCrit = true;           // 每段独立判定暴击
        public bool holdOnLowTarget = true;   // 目标一刀普攻就能打死时不放，留给下一只
        public bool pierceLastHit;            // 最后一段穿透：射程内的怪都挨
        public bool pierceAll;                // v0.20 每一段都穿透（冲刺穿过怪群的技能）
        // v0.16 范围 / 连锁（按段生效）：splash = 以目标为中心左右多少像素内的怪都挨 splashMult 倍；
        // chain = 再弹到离目标最近的 N 只，每跳伤害 × chainFalloff。fx = 画面上用哪种技能特效。
        public float splash, splashMult = 1;
        public float push;                     // 命中后把被打到的怪往外推（锁链横扫）
        public int chain;
        public float chainFalloff = .75f;
        public string fx = "";
    }

    public static class SkillBook
    {
        /// 命中时刻 = 动作表里的拍子 × 时长（拍子见 LayeredRigView 的 SkillMoves.cs，两边要一起改）。
        private static float[] Beats(float duration, params float[] beats) { var r = new float[beats.Length]; for (int i = 0; i < beats.Length; i++) r[i] = beats[i] * duration; return r; }
        // ---- 技能定义 ----
        public static readonly SkillDef QuickCut = new SkillDef
        {
            id = "dagger_quickcut", displayName = "迅切连击", animation = "dagger_quickcut",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = DaggerSkill.Duration, hitTimes = DaggerSkill.Hits, hitDamage = new[] { .6f, .6f, .7f, .8f }, pierceLastHit = true,   // v0.20 刺 / 回旋斩 / 冲刺穿刺 / 上挑
        };

        // v0.16 第一章其余 4 把武器的技能（数值初版，阶段 2 统一调）。
        // 霰弹：冷却好了贴脸连轰两发，每发把周围的怪一起轰到。
        public static readonly SkillDef PointBlank = new SkillDef
        {
            id = "shotgun_pointblank", displayName = "贴脸轰击", animation = "pointblank", fx = "burst",
            trigger = SkillTrigger.Cooldown, cooldown = 6,
            duration = 1.3f, hitTimes = Beats(1.3f, .20f, .50f, .78f), hitDamage = new[] { 1f, 1f, 1.25f }, splash = 140, splashMult = .6f,   // v0.20 三枪连招
        };
        // 线圈：每 5 次普攻，一道电弧在怪群里弹 4 次。
        public static readonly SkillDef ChainArc = new SkillDef
        {
            id = "coil_chainarc", displayName = "连锁放电", animation = "chainarc", fx = "chain",
            trigger = SkillTrigger.EveryNAttacks, attacks = 5,
            duration = 1.3f, hitTimes = Beats(1.3f, .30f, .64f, .76f), hitDamage = new[] { .8f, .5f, 1.05f }, chain = 3, chainFalloff = .7f,   // v0.20 三段连招（动作表 SkillMoves.cs）
        };
        // 手炮：冷却好了打一发过压弹，大范围爆炸。
        public static readonly SkillDef Overpressure = new SkillDef
        {
            id = "cannon_overpressure", displayName = "过压弹", animation = "overpressure", fx = "blast",
            trigger = SkillTrigger.Cooldown, cooldown = 7,
            duration = 1.4f, hitTimes = Beats(1.4f, .52f), hitDamage = new[] { 3.3f }, splash = 220, splashMult = .8f,   // v0.20 跪射（.43 开炮、.52 落地）
        };
        // 重锤：每 4 次普攻，抡起来震地，前方一大片都挨。
        public static readonly SkillDef Quake = new SkillDef
        {
            id = "heavy_quake", displayName = "震地", animation = "quake", fx = "quake",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = 1.4f, hitTimes = Beats(1.4f, .52f, .78f), hitDamage = new[] { 1.7f, 1.1f }, splash = 280, splashMult = .9f, holdOnLowTarget = false,   // v0.20 砸地 + 撩锤
        };

        // v0.16 第二章武器技能
        public static readonly SkillDef PierceThrust = new SkillDef
        {   // 撞杆矛：每 4 次普攻，一记长突刺穿透整排
            id = "pole_thrust", displayName = "贯穿突刺", animation = "thrust", fx = "burst",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = 1.3f, hitTimes = Beats(1.3f, .36f, .62f, .84f), hitDamage = new[] { 1.1f, .9f, .8f }, pierceAll = true,   // v0.20 来回两刺 + 砸地
        };
        public static readonly SkillDef NailBurst = new SkillDef
        {   // 钉枪：冷却好了连射四钉
            id = "nail_burst", displayName = "钉射扫射", animation = "nailburst", fx = "burst",
            trigger = SkillTrigger.Cooldown, cooldown = 6,
            duration = 1.3f, hitTimes = Beats(1.3f, .14f, .22f, .30f, .38f, .62f, .70f), hitDamage = new[] { .55f, .55f, .55f, .55f, .6f, .6f },   // v0.20 边走边点射 + 跪滑补两钉
        };
        public static readonly SkillDef HookSweep = new SkillDef
        {   // 吊臂钩索：每 4 次甩钩，贴地抡一整圈，扫中一片并把它们推开
            id = "hook_sweep", displayName = "吊钩横扫", animation = "sweep", fx = "sweep",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = 1.4f, hitTimes = Beats(1.4f, .34f, .62f, .84f), hitDamage = new[] { .7f, .9f, .9f }, splash = 300, splashMult = .9f, holdOnLowTarget = false, push = 40,   // v0.20 套索钩拽 + 横扫 + 下砸
        };
        public static readonly SkillDef SawSpin = new SkillDef
        {   // 切割锯：冷却好了连切四下，旁边的怪也挨
            id = "saw_spin", displayName = "回旋切割", animation = "whirl", fx = "burst",
            trigger = SkillTrigger.Cooldown, cooldown = 5,
            duration = 1.2f, hitTimes = Beats(1.2f, .20f, .30f, .40f, .50f, .60f, .80f), hitDamage = new[] { .45f, .45f, .45f, .45f, .45f, .8f }, splash = 120, splashMult = .5f,   // v0.20 空中旋风五切 + 劈地
        };
        public static readonly SkillDef FlareBolt = new SkillDef
        {   // 信号弩：冷却好了射一支信号爆矢，落点爆炸
            id = "flare_bolt", displayName = "信号爆矢", animation = "flarebolt", fx = "flare",
            trigger = SkillTrigger.Cooldown, cooldown = 7,
            duration = 1.3f, hitTimes = Beats(1.3f, .44f, .62f), hitDamage = new[] { 1.8f, 1.2f }, splash = 200, splashMult = .7f,   // v0.20 后空翻跪射：抛射一发 + 平射一发
        };

        // ---- 武器 → 技能 ----  键 "模板ID|品质" 优先，其次 "模板ID"（该模板所有品质共用）
        private static readonly Dictionary<string, SkillDef> byWeapon = new Dictionary<string, SkillDef>
        {
            { "w1", QuickCut },
            { "w0", PointBlank },
            { "w2", ChainArc },
            { "w3", Overpressure },
            { "w4", Quake },
            { "w5", PierceThrust },
            { "w6", NailBurst },
            { "w7", HookSweep },
            { "w8", SawSpin },
            { "w9", FlareBolt },
        };

        public static SkillDef ForWeapon(Item item)
        {
            if (item == null) return null;
            SkillDef def;
            if (byWeapon.TryGetValue(item.baseId + "|" + item.quality, out def)) return def;
            if (byWeapon.TryGetValue(item.baseId, out def)) return def;
            string mech = Catalog.MechOf(item.baseId);   // v0.18 新武器借旧武器的技能
            if (mech != item.baseId && byWeapon.TryGetValue(mech, out def)) return def;
            return null;
        }
    }
}
