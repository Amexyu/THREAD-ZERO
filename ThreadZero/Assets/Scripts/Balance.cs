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
        /// v0.20 线圈（线圈发射器 / 协议发射器）：电流从目标跳到它身后最近的一只怪，吃一半。原来纯单体——协议发射器面板最高，模拟里拿着它的种子却全卡在第五章成群的波次。
        public const float CoilJump = .5f, CoilJumpReach = 220;
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
        public const float MarkInterval=3.5f, MarkDuration=4, ArcDamage=.45f, MarkedArcMultiplier=2;   // v0.19 电弧 35% → 45%
        public const float ThrusterInterval=4, ThrusterDamage=.45f, AssaultMultiplier=1.5f;
        public const float GuardReduction=.12f, GuardChargeRatio=.8f, LeechRatio=.035f;   // v0.19 吸血 4.5% → 3.5%：后期吸血武器比别的强太多（第五章拿吸血 20 分钟过、拿电弧 2–3 小时）
        public const float ExecuteThreshold=.3f, ExecuteMultiplier=1.4f;
        /// v0.19 变强后再挑战：在墙上倒下后回刷前一关，战力（输出 × 生存的几何平均）涨 4% 或者刷满 5 次，才自动回去再挑战。
        public const float RetryGain = .04f; public const int RetryFarms = 5;
        /// v0.20 离线收益（企划：最多 2 小时、25% 效率、只给零件）：按挂机时的零件收入（击杀 + 掉落的分解价）算，离开不到 1 分钟不算。
        public const float OfflineMaxSeconds = 7200, OfflineEfficiency = .25f, OfflineMinSeconds = 60;
        /// v0.19 节奏：分解给的零件 × 这个倍率（零件主要来自分解）。零件少一点，强化 / 训练就慢一点，整体节奏跟着慢下来。
        public const float SalvageScale = .4f;
        /// v0.20 机械飞升材料「织网碎片」：回声层每过一层 1 个、首领层 5 个、每 25 层里程碑再 20 个；重复打主线首领 2 个。
        public const int ShardsEchoFloor = 1, ShardsEchoBoss = 5, ShardsMilestone = 20, ShardsBossRepeat = 2;
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
        // 词条：0 生命 1 攻击 2 防御 3 攻速 4 暴击 5 幸运；v0.18 特殊词条 6 暴击伤害 7 特效伤害 8 首领伤害 9 零件回收（百分比）。
        // 特殊词条只出在「特装」及以上，随等级涨得很慢（每级 +1%），有总上限。
        public static readonly float[] AffixBases={12,3,2,.04f,.025f,2, .08f,.07f,.06f,.06f};
        public const int BasicStats=6, AffixStats=10;
        public static bool SpecialStat(int stat) { return stat>=BasicStats; }
        public const float CritDamageCap=1.5f, EffectDamageCap=2f, BossDamageCap=1.5f, ScrapBonusCap=1f;
        // v0.21 芯片与改装件：精英 / 首领 / 深网首领层掉芯片的概率，首领掉改装件的概率，制作一件改装件要的核心；受到伤害减免的总上限（芯片 + 改造板）
        public const float ChipEliteChance=.04f, ChipBossChance=.25f, ChipEchoBossChance=.30f, ModBossChance=.2f, ReductionCap=.45f;
        public const int ModCraftCores=3;
        // v0.21 改造板「回收」分支每级：零件回收 +0.4%、幸运 +0.05
        public const float BoardScrap=.004f, BoardLuck=.05f;
        /// 洗炼：没锁的词条全部重抽（换属性 + 重抽数值）。锁一条费用翻倍、锁两条三倍……次数越多越贵（和重校共用次数）。
        public static int ReforgeCost(Quality quality,int rerolls,int locked) { return (14+6*(int)quality)*(1+Math.Max(0,Math.Min(9,rerolls)))*(1+Math.Max(0,locked)); }
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
        public static float SpecialLevelFactor(int level) { return 1+.01f*(Math.Max(1,Math.Min(MaxItemLevel,level))-1); }
        public static float AffixFactor(int stat,int level) { return SpecialStat(stat) ? SpecialLevelFactor(level) : AffixLevelFactor(level); }
        // v0.16：30 级以后（回声层阶段）升级费用 ×6，靠长期攒零件慢慢升。
        /// v0.19：原来 30 级起价格直接 ×6，正好卡在第二章进第三章，刷零件升级一下子没用了，运气差的种子在第三章卡几个小时。
        /// 改成随等级平滑变贵：10 级以下 ×1，30 级 ×2.25，50 级 ×3.5，90 级 ×6（和原来后期差不多）。
        public static int UpgradeCost(int level,Quality quality) { int l=Math.Max(1,level); return (int)Math.Round((6+l*2)*(1+.4f*(int)quality)*(1+Math.Max(0,l-10)/16f)); }
        public static int RerollCost(Quality quality,int rerolls) { return (8+4*(int)quality)*(1+Math.Max(0,Math.Min(9,rerolls))); }
        public static float LevelFactor(int level) { return Soft(level,ItemLevelGrowth); }
        public static float AffixValue(int stat,Quality quality,int level,float roll)
        {
            if(stat<0 || stat>=AffixBases.Length) throw new ArgumentOutOfRangeException(nameof(stat));
            QualityTier q=Tier(quality);
            return AffixBases[stat]*(q.affixMin+(q.affixMax-q.affixMin)*Math.Max(0,Math.Min(1,roll)))
                *(SpecialStat(stat) ? SpecialLevelFactor(level) : Soft(level,AffixLevelGrowth));
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
