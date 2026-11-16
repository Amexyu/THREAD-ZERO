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

        // ---- 武器 → 技能 ----  键 "模板ID|品质" 优先，其次 "模板ID"（该模板所有品质共用）
        private static readonly Dictionary<string, SkillDef> byWeapon = new Dictionary<string, SkillDef>
        {
            { "w1|Common", QuickCut },
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
