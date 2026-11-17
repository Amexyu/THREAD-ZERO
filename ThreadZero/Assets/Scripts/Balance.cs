using System;

namespace ThreadZero
{
    public sealed class QualityTier
    {
        public readonly string name, colorHex;
        public readonly float multiplier, affixMin, affixMax, glow, pulseSeconds;
        public readonly int affixCount, particles;
        public QualityTier(string title, string color, float factor, int rolls, float min, float max, float light, float pulse, int sparks)
        { name=title; colorHex=color; multiplier=factor; affixCount=rolls; affixMin=min; affixMax=max; glow=light; pulseSeconds=pulse; particles=sparks; }
    }

    // One source of truth for numerical rules, tests, generated tables, and visual grading.
    public static class Balance
    {
        public const int Revision = 3;
        // v0.15 武器类型的攻击方式：霰弹 = 多颗弹丸（多段伤害，可分散到身后的怪），手炮 = 落点爆炸范围伤害，重锤 = 前方横扫范围伤害。
        public const int ShotgunPellets = 6; public const float ShotgunPellet = .2f, ShotgunSpread = 150;
        public const float CannonSplash = .6f, CannonRadius = 120;
        public const float HeavySplash = .7f, HeavyReach = 140;
        public const float BaseHealth=140, BaseAttack=16, BaseDefense=4, BaseCrit=.05f;
        public const float TrainingHealth=8, TrainingAttack=1.5f;
        // v0.16 挂机长线：训练不封顶（费用线性涨）；装备最高 300 级，100 级以后每级收益减半。
        public const int TrainingCap=9999, MaxItemLevel=300, SoftLevel=100;
        public const float CritCap=.75f, CritMultiplier=1.75f, HasteCap=2, LuckCap=30;
        // v0.14：等级权重调高（每级 +8% / 词条 +6%），高两关的量产装备大致能打平低两关的特装。
        public const float ItemLevelGrowth=.08f, AffixLevelGrowth=.06f;
        public const double NormalDropChance=.65, FeaturedBossChance=.35, TargetBossChance=.4;
        public const int RarePity=12, EpicPity=45, LegendaryPity=120, TargetBossGuarantee=8;
        public const float DroneInterval=2.4f, DroneDamage=.3f, MarkedDroneMultiplier=1.5f;
        public const float MarkInterval=3.5f, MarkDuration=4, ArcDamage=.35f, MarkedArcMultiplier=2;
        public const float ThrusterInterval=4, ThrusterDamage=.45f, AssaultMultiplier=1.5f;
        public const float GuardReduction=.12f, GuardChargeRatio=.8f, LeechRatio=.045f;
        public const float ExecuteThreshold=.3f, ExecuteMultiplier=1.4f;
        public const float WaveRecovery=.04f, BossRecovery=1;
        // v0.9 路段制：每关 5 个路段。1、2、4 是怪群，3 是精英，5 是首领。
        public const int EliteWave=3, BossWave=5;
        public const float PackHealthShare=.34f, PackPressure=.1f, EliteHealth=1.3f, EliteAttack=1.1f, EliteRecovery=.15f;
        public const float ArcChain=.5f, ThrusterPierce=.6f;
        public static int PackSize(int region,int wave)
        {
            if(wave==BossWave || wave==EliteWave) return 1;
            return (wave==1?2:3)+(region>=3?1:0);
        }
        public static readonly float[] AffixBases={12,3,2,.04f,.025f,2};
        // v0.16 等级与稀有度分开（借鉴 Task Bar Hero）：等级决定强度，稀有度只给小倍率 + 词条条数（之后再加专属效果）。
        // 目标手感：差 1 档品质 ≈ 差 1.5 级，高等级蓝装能打过低等级金装。具体数值留到阶段 2 统一调。
        // 词条数值只看等级（affixMin/Max 各档相同），不再按品质放大。
        public static readonly QualityTier[] Qualities={
            new QualityTier("量产", "9CABB8", 1,    0,.85f,1.15f,0,   0,  0),
            new QualityTier("改良", "6BD494", 1.04f,1,.85f,1.15f,0,   0,  0),
            new QualityTier("特装", "59A6FF", 1.08f,2,.85f,1.15f,.18f,3.2f,0),
            new QualityTier("军规", "BF80FF", 1.12f,3,.85f,1.15f,.32f,2.8f,1),
            new QualityTier("机密", "FFC24D", 1.16f,4,.85f,1.15f,.5f, 2.4f,3),
            // 第六档。不进普通掉落：只来自首领的极低概率，或把「机密」装备用核心突破。
            new QualityTier("原型", "FF4D5E", 1.20f,5,.85f,1.15f,.5f, 2.4f,4)
        };
        public static QualityTier Tier(Quality quality)
        {
            if((int)quality<0 || (int)quality>=Qualities.Length) throw new ArgumentOutOfRangeException(nameof(quality));
            return Qualities[(int)quality];
        }
        // v0.10 许可等级：每通过一关首领，上限提高 5 级，最高 30。装备每级的涨幅随之调小，
        // 第 6 关装备的强度与旧版（6 级、每级 15%）大致相同。
        public const int LicenseStep=5, MaxLicense=30, AscendCores=8;
        public const double RepeatCoreChance=.25;   // v0.16 首领首通必掉核心，之后每次 25%
        public const double PrototypeBossChance=.02;
        public static readonly int[] ChipSlots={0,0,1,1,2,3};
        private static float Soft(int level,float g) { int l=Math.Max(1,Math.Min(MaxItemLevel,level)); return 1+g*(Math.Min(l,SoftLevel)-1)+g*.5f*Math.Max(0,l-SoftLevel); }
        public static float AffixLevelFactor(int level) { return Soft(level,AffixLevelGrowth); }
        // v0.16：30 级以后（回声层阶段）升级费用 ×6，靠长期攒零件慢慢升。
        public static int UpgradeCost(int level,Quality quality) { return (int)Math.Round((6+Math.Max(1,level)*2)*(1+.4f*(int)quality)*(level>=30?6:1)); }
        public static int RerollCost(Quality quality,int rerolls) { return (8+4*(int)quality)*(1+Math.Max(0,Math.Min(9,rerolls))); }
        public static float LevelFactor(int level) { return Soft(level,ItemLevelGrowth); }
        public static float AffixValue(int stat,Quality quality,int level,float roll)
        {
            if(stat<0 || stat>=AffixBases.Length) throw new ArgumentOutOfRangeException(nameof(stat));
            QualityTier q=Tier(quality);
            return AffixBases[stat]*(q.affixMin+(q.affixMax-q.affixMin)*Math.Max(0,Math.Min(1,roll)))
                *Soft(level,AffixLevelGrowth);
        }
        public static int TrainingCost(int level) { return 20+level*15; }
        public static float EnemyHealth(int region,int wave) { return (48+wave*12+region*65)*(wave==5?2.3f:1); }
        public static float EnemyAttack(int region,int wave) { return (5+region*6+wave*1.5f)*(wave==5?1.35f:1); }
        public static float EnemyDefense(int region,bool boss) { return region*10+(boss?12:0); }
        public static double[] QualityWeights(float luck)
        {
            luck=Math.Max(0,Math.Min(LuckCap,luck));
            return new double[]{50,28,15+luck*.2,6+luck*.1,1+luck*.025};
        }
    }
}
