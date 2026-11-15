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

    public sealed partial class CombatSession
    {
        public float DaggerAge {get;private set;} = 10;
        public float DaggerCooldown {get;private set;}
        public bool DaggerCombo {get;private set;}
        public bool DaggerActive {get{return DaggerAge<(DaggerCombo?DaggerSkill.Duration:DaggerSkill.BasicDuration);}}
        private int daggerNextHit;
        private bool daggerCanceled;
        public Action<int,float> OnDaggerHit;

        private void ResetDagger()
        { DaggerAge=10; DaggerCooldown=DaggerSkill.Cooldown; daggerNextHit=0; daggerCanceled=true; }
        private void CancelDagger()
        {
            daggerCanceled=true;
            if(DaggerActive) DaggerAge=Math.Max(DaggerAge,DaggerCombo?.56f:.18f);
        }
        private bool AdvanceDagger(float dt, Stats stats)
        {
            DaggerCooldown=Math.Max(0,DaggerCooldown-dt);
            if(!DaggerActive) return false;
            DaggerAge+=dt;
            if(!DaggerCombo || daggerCanceled || !DaggerSkill.Matches(model) || recovery>0) return false;
            while(daggerNextHit<DaggerSkill.Hits.Length && DaggerAge+.00001f>=DaggerSkill.Hits[daggerNextHit])
            {
                int hit=daggerNextHit++;
                // Equipment skill damage is separate from basic-hit counters and basic-only procs.
                float damage=Mitigate(stats.attack*.8f); lastCritical=false;
                OnDaggerHit?.Invoke(hit,damage);
                if(DealDamage(damage,stats)) return true;
            }
            return false;
        }
        private bool BeginDagger(Stats stats)
        {
            if(!DaggerSkill.Matches(model)) return false;
            DaggerAge=0; daggerNextHit=0; daggerCanceled=false;
            DaggerCombo=DaggerCooldown<=.00001f;
            if(DaggerCombo) {DaggerCooldown=DaggerSkill.Cooldown; pendingStrike=false;}
            else {pendingStrike=true;strikeDelay=DaggerSkill.BasicHit; OnBasicAttack?.Invoke();}
            attackTimer=Math.Max(1/(1+stats.haste),DaggerCombo?DaggerSkill.Duration:DaggerSkill.BasicDuration);
            return true;
        }
    }
}
