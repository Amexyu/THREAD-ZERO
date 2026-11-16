using System;

namespace ThreadZero
{
    // Initial dagger only. Higher qualities require their own skill/animation definitions.
    public static class DaggerSkill
    {
        public const float Duration = .68f, BasicDuration = .35f, BasicHit = .14f, Cooldown = 5f;
        public static readonly int[] FrameMilliseconds = {120,40,50,20,20,30,20,20,40,30,30,50,20,20,50,30,30,60};
        public static readonly float[] Hits = {.23f,.32f,.51f};
        private static readonly int[] basicFrames = {11,12,13,14,15,16,17};
        private static readonly int[] basicTimes = {90,30,20,40,40,50,80};
        public static bool Matches(GameModel model)
        { Item item=model.Equipped(Slot.Weapon); return item!=null && item.baseId=="w1" && item.quality==Quality.Common; }
        public static int Frame(float age, bool combo)
        {
            if(float.IsNaN(age) || age<0) return 0;
            float ms=age*1000; int[] times=combo?FrameMilliseconds:basicTimes;
            for(int i=0;i<times.Length;i++) { ms-=times[i]; if(ms<-.001f) return combo?i:basicFrames[i]; }
            return 17;
        }
    }

    // 装备技能运行时（通用）。技能定义见 SkillSystem.cs。
    // 名字沿用 Dagger*：GameAppAction / 审核脚本按这些名字读当前出手的计时，含义是“当前这次出手（技能或匕首短刺）”。
    public sealed partial class CombatSession
    {
        public float DaggerAge {get;private set;} = 10;
        public SkillDef ActiveSkill {get;private set;}
        public bool DaggerCombo {get{return ActiveSkill!=null;}}
        public bool DaggerActive {get{return ActiveSkill!=null ? DaggerAge<ActiveSkill.duration : daggerBasic && DaggerAge<DaggerSkill.BasicDuration;}}
        public float SkillCooldown {get;private set;}
        public float DaggerCooldown {get{return SkillCooldown;}}
        public int SkillCharge {get;private set;}
        private int skillMisses, skillNextHit;
        private bool skillCanceled = true, daggerBasic;
        public Action<int,float> OnDaggerHit;
        public Action<SkillDef> OnSkillCast;

        public SkillDef EquippedSkill {get{return SkillBook.ForWeapon(model.Equipped(Slot.Weapon));}}
        /// 0..1，给 UI 显示充能/冷却进度。
        public float SkillReadiness
        {
            get
            {
                SkillDef s=EquippedSkill; if(s==null) return 0;
                if(s.trigger==SkillTrigger.Cooldown) return s.cooldown<=0?1:1-SkillCooldown/s.cooldown;
                if(s.trigger==SkillTrigger.EveryNAttacks) return s.attacks<=0?1:Math.Min(1f,(float)SkillCharge/s.attacks);
                return s.pity<=0?0:Math.Min(1f,(float)skillMisses/s.pity);
            }
        }

        // 换装、维修时清空：不能把旧武器攒好的技能带到新武器上。
        private void ResetDagger()
        {
            DaggerAge=10; ActiveSkill=null; daggerBasic=false; skillNextHit=0; skillCanceled=true;
            SkillCharge=0; skillMisses=0;
            SkillDef s=EquippedSkill; SkillCooldown=s!=null && s.trigger==SkillTrigger.Cooldown ? s.cooldown : 0;
        }
        // 目标死亡：停掉剩余段，直接进入收招。
        private void CancelDagger()
        {
            skillCanceled=true;
            if(DaggerActive) DaggerAge=Math.Max(DaggerAge,ActiveSkill!=null?RecoverAt(ActiveSkill):.18f);
        }
        private static float RecoverAt(SkillDef s)
        {
            float last=s.hitTimes!=null && s.hitTimes.Length>0 ? s.hitTimes[s.hitTimes.Length-1] : 0;
            return Math.Min(s.duration,last+.05f);
        }
        private bool AdvanceDagger(float dt, Stats stats)
        {
            SkillCooldown=Math.Max(0,SkillCooldown-dt);
            if(!DaggerActive) return false;
            DaggerAge+=dt;
            SkillDef s=ActiveSkill;
            if(s==null || s.hitTimes==null || skillCanceled || recovery>0 || s!=EquippedSkill) return false;
            while(skillNextHit<s.hitTimes.Length && DaggerAge+.00001f>=s.hitTimes[skillNextHit])
            {
                int hit=skillNextHit++;
                // 群怪：每段打当时最近的一只（前一只死了就打下一只）；射程内没有怪这一段落空。
                Enemy target=Target;
                if(target==null || !HeroInRange) continue;
                // 技能伤害独立于普攻：不推进三连电弧计数，不吃普攻专用处决倍率；汲取走共用伤害入口。
                float mult=s.hitDamage!=null && hit<s.hitDamage.Length ? s.hitDamage[hit] : 1;
                float damage=Mitigate(stats.attack*mult);
                lastCritical=s.canCrit && model.Chance(stats.crit);
                if(lastCritical) damage*=Balance.CritMultiplier;
                OnDaggerHit?.Invoke(hit,damage);
                DealDamage(target,damage,stats);
                // 最后一段穿透：射程再往前 60 像素内的怪都挨这一下。
                if(s.pierceLastHit && hit==s.hitTimes.Length-1) Area(target,Movement.HeroX+Movement.HeroRange+60,damage,99,stats);
            }
            return false;
        }
        // 每次可以出手时调用。true = 这次出手由这里接管（技能，或初始匕首专用短刺）；false = 走通用普攻。
        private bool BeginDagger(Stats stats)
        {
            SkillDef s=EquippedSkill;
            if(s!=null && SkillWanted(s,stats))
            {
                ActiveSkill=s; daggerBasic=false; DaggerAge=0; skillNextHit=0; skillCanceled=false;
                if(s.trigger==SkillTrigger.Cooldown) SkillCooldown=s.cooldown;
                SkillCharge=0; skillMisses=0;
                pendingStrike=false; OnSkillCast?.Invoke(s);
                attackTimer=Math.Max(1/(1+stats.haste),s.duration);
                return true;
            }
            if(s!=null) { if(s.trigger==SkillTrigger.EveryNAttacks) SkillCharge=Math.Min(s.attacks,SkillCharge+1); else if(s.trigger==SkillTrigger.Chance) skillMisses++; }
            if(!DaggerSkill.Matches(model)) return false;
            ActiveSkill=null; daggerBasic=true; DaggerAge=0; skillNextHit=0; skillCanceled=false;
            pendingStrike=true; strikeDelay=DaggerSkill.BasicHit; OnBasicAttack?.Invoke();
            attackTimer=Math.Max(1/(1+stats.haste),DaggerSkill.BasicDuration);
            return true;
        }
        private bool SkillWanted(SkillDef s, Stats stats)
        {
            if(s.trigger==SkillTrigger.Cooldown && SkillCooldown>.00001f) return false;
            if(s.trigger==SkillTrigger.EveryNAttacks && SkillCharge<s.attacks) return false;
            // 残血留招：目标一刀普攻就倒，就用普攻收尾，技能留给下一只（不消耗充能/冷却）。
            if(s.holdOnLowTarget && enemyHealth<=Mitigate(stats.attack)) return false;
            if(s.trigger==SkillTrigger.Chance) return (s.pity>0 && skillMisses>=s.pity) || model.Chance(s.chance);
            return true;
        }
    }
}
