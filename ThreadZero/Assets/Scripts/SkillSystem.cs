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
        // ---- 技能定义 ----
        public static readonly SkillDef QuickCut = new SkillDef
        {
            id = "dagger_quickcut", displayName = "迅切连击", animation = "dagger_quickcut",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = DaggerSkill.Duration, hitTimes = DaggerSkill.Hits, hitDamage = new[] { .8f, .8f, .8f }, pierceLastHit = true,
        };

        // v0.16 第一章其余 4 把武器的技能（数值初版，阶段 2 统一调）。
        // 霰弹：冷却好了贴脸连轰两发，每发把周围的怪一起轰到。
        public static readonly SkillDef PointBlank = new SkillDef
        {
            id = "shotgun_pointblank", displayName = "贴脸轰击", animation = "shoot", fx = "burst",
            trigger = SkillTrigger.Cooldown, cooldown = 6,
            duration = .8f, hitTimes = new[] { .22f, .5f }, hitDamage = new[] { 1.3f, 1.3f }, splash = 140, splashMult = .6f,
        };
        // 线圈：每 5 次普攻，一道电弧在怪群里弹 4 次。
        public static readonly SkillDef ChainArc = new SkillDef
        {
            id = "coil_chainarc", displayName = "连锁放电", animation = "shoot", fx = "chain",
            trigger = SkillTrigger.EveryNAttacks, attacks = 5,
            duration = .65f, hitTimes = new[] { .3f }, hitDamage = new[] { 1.6f }, chain = 4, chainFalloff = .75f,
        };
        // 手炮：冷却好了打一发过压弹，大范围爆炸。
        public static readonly SkillDef Overpressure = new SkillDef
        {
            id = "cannon_overpressure", displayName = "过压弹", animation = "shoot", fx = "blast",
            trigger = SkillTrigger.Cooldown, cooldown = 7,
            duration = .85f, hitTimes = new[] { .42f }, hitDamage = new[] { 2.6f }, splash = 220, splashMult = .8f,
        };
        // 重锤：每 4 次普攻，抡起来震地，前方一大片都挨。
        public static readonly SkillDef Quake = new SkillDef
        {
            id = "heavy_quake", displayName = "震地", animation = "smash", fx = "quake",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = .95f, hitTimes = new[] { .5f }, hitDamage = new[] { 2f }, splash = 280, splashMult = .9f, holdOnLowTarget = false,
        };

        // v0.16 第二章武器技能
        public static readonly SkillDef PierceThrust = new SkillDef
        {   // 撞杆矛：每 4 次普攻，一记长突刺穿透整排
            id = "pole_thrust", displayName = "贯穿突刺", animation = "attack", fx = "burst",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = .7f, hitTimes = new[] { .3f }, hitDamage = new[] { 2f }, pierceLastHit = true,
        };
        public static readonly SkillDef NailBurst = new SkillDef
        {   // 钉枪：冷却好了连射四钉
            id = "nail_burst", displayName = "钉射扫射", animation = "shoot", fx = "burst",
            trigger = SkillTrigger.Cooldown, cooldown = 6,
            duration = .85f, hitTimes = new[] { .16f, .32f, .48f, .64f }, hitDamage = new[] { .75f, .75f, .75f, .75f },
        };
        public static readonly SkillDef HookSweep = new SkillDef
        {   // 吊臂钩索：每 4 次甩钩，贴地抡一整圈，扫中一片并把它们推开
            id = "hook_sweep", displayName = "吊钩横扫", animation = "sweep", fx = "sweep",
            trigger = SkillTrigger.EveryNAttacks, attacks = 4,
            duration = .95f, hitTimes = new[] { .5f }, hitDamage = new[] { 1.8f }, splash = 300, splashMult = .9f, holdOnLowTarget = false, push = 90,
        };
        public static readonly SkillDef SawSpin = new SkillDef
        {   // 切割锯：冷却好了连切四下，旁边的怪也挨
            id = "saw_spin", displayName = "回旋切割", animation = "attack", fx = "burst",
            trigger = SkillTrigger.Cooldown, cooldown = 5,
            duration = .75f, hitTimes = new[] { .18f, .3f, .42f, .54f }, hitDamage = new[] { .6f, .6f, .6f, .6f }, splash = 120, splashMult = .5f,
        };
        public static readonly SkillDef FlareBolt = new SkillDef
        {   // 信号弩：冷却好了射一支信号爆矢，落点爆炸
            id = "flare_bolt", displayName = "信号爆矢", animation = "shoot", fx = "blast",
            trigger = SkillTrigger.Cooldown, cooldown = 7,
            duration = .8f, hitTimes = new[] { .4f }, hitDamage = new[] { 2.4f }, splash = 200, splashMult = .7f,
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
            return null;
        }
    }
}
