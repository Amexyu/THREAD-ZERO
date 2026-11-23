using System;

namespace ThreadZero
{
    // v0.14 章节制：一章 7 关，每关若干波，怪物成群涌上来。
    // state.region 现在表示“第一章的第几关”（0 = 1-1 … 6 = 1-7）；存档字段不变。
    public enum EnemyRole { Swarmer, Brute, Ranged }
    public enum StageKind { Normal, Elite, Boss }

    public sealed class WaveDef
    {
        public readonly int swarmers, brutes, ranged, elites;
        public WaveDef(int swarm, int brute, int range, int elite = 0) { swarmers = swarm; brutes = brute; ranged = range; elites = elite; }
        public int Count { get { return swarmers + brutes + ranged + elites; } }
    }

    public sealed class StageDef
    {
        public readonly string code; public readonly StageKind kind; public readonly int backdrop; public readonly WaveDef[] waves;
        public StageDef(string id, StageKind type, int scene, params WaveDef[] list) { code = id; kind = type; backdrop = scene; waves = list; }
    }

    public sealed class RoleStats
    {
        public readonly float health, attack, interval, speed, stopMin, stopMax, canvas, dropChance;
        public readonly int art;   // ActorView.Enemy 的“街区”参数：0 巡逻机 / 1 运输机 / 2 干扰机
        public RoleStats(float hp, float atk, float every, float move, float near, float far, float size, float drop, int look)
        { health = hp; attack = atk; interval = every; speed = move; stopMin = near; stopMax = far; canvas = size; dropChance = drop; art = look; }
    }

    public static class Chapters
    {
        public const int StagesPerChapter = 7;
        public const int MaxOnScreen = 8;          // 同屏最多 8 只，其余在画面外排队
        public const int MeleeAttackers = 3;       // 同时贴身攻击的近战怪最多 3 只（其余挤在后面等位置）
        public const float BossSummonInterval = 15, StageTransition = 2.0f;
        /// v0.20 首领叫来的小怪只有六成血：原来和普通冲锋怪一样硬，单体武器（钩索）在第五章首领战里三分之二的时间都在清小怪，打不死首领一直超时。
        public const float SummonHealth = .6f;

        // 第一章：1-3、1-6 精英，1-7 首领。场景套 0=A 废液沟渠 1=B 叠层棚户 2=C 泵站管廊 3=D 抽水总闸。
        public static readonly StageDef[] Chapter1 = {
            new StageDef("1-1", StageKind.Normal, 0, new WaveDef(2,0,0), new WaveDef(3,0,0), new WaveDef(4,0,0)),
            new StageDef("1-2", StageKind.Normal, 0, new WaveDef(3,0,0), new WaveDef(3,1,0), new WaveDef(3,2,0)),
            new StageDef("1-3", StageKind.Elite,  1, new WaveDef(3,1,0), new WaveDef(4,1,0), new WaveDef(2,0,0,1)),
            new StageDef("1-4", StageKind.Normal, 1, new WaveDef(3,1,0), new WaveDef(3,1,1), new WaveDef(3,2,1)),
            new StageDef("1-5", StageKind.Normal, 2, new WaveDef(3,1,1), new WaveDef(3,2,1), new WaveDef(8,0,0)),
            new StageDef("1-6", StageKind.Elite,  2, new WaveDef(3,1,1), new WaveDef(3,2,1), new WaveDef(3,0,0,2)),
            new StageDef("1-7", StageKind.Boss,   3, new WaveDef(0,0,0)),
        };
        // v0.16 第二章「物流高架」：2-3、2-6 精英，2-7 首领。场景套 4=A 高架货轨 5=B 货柜堆场 6=C 分拣仓内 7=D 总调度塔。
        public static readonly StageDef[] Chapter2 = {
            new StageDef("2-1", StageKind.Normal, 4, new WaveDef(4,1,0), new WaveDef(4,1,1), new WaveDef(5,2,1)),
            new StageDef("2-2", StageKind.Normal, 4, new WaveDef(4,2,1), new WaveDef(5,2,1), new WaveDef(6,2,2)),
            new StageDef("2-3", StageKind.Elite,  5, new WaveDef(4,2,1), new WaveDef(5,2,1), new WaveDef(3,1,0,1)),
            new StageDef("2-4", StageKind.Normal, 5, new WaveDef(5,2,2), new WaveDef(6,2,2), new WaveDef(6,3,2)),
            new StageDef("2-5", StageKind.Normal, 6, new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(10,0,1)),
            new StageDef("2-6", StageKind.Elite,  6, new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(3,1,1,2)),
            new StageDef("2-7", StageKind.Boss,   7, new WaveDef(0,0,0)),
        };
        // v0.18 第三章「信号禁区」：货运线之上的天线屋顶与中继塔，雨停了、天色第一次露出来。3-3、3-6 精英，3-7 首领「断讯监视者」。
        // 场景套 8=A 天线屋顶 9=B 中继塔廊 10=C 干扰站内 11=D 监视穹顶。怪的配比比第二章多一档重型和远程。
        public static readonly StageDef[] Chapter3 = {
            new StageDef("3-1", StageKind.Normal, 8,  new WaveDef(5,2,1), new WaveDef(5,2,2), new WaveDef(6,3,2)),
            new StageDef("3-2", StageKind.Normal, 8,  new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(7,3,2)),
            new StageDef("3-3", StageKind.Elite,  9,  new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(3,1,1,2)),
            new StageDef("3-4", StageKind.Normal, 9,  new WaveDef(6,3,2), new WaveDef(6,3,3), new WaveDef(7,3,3)),
            new StageDef("3-5", StageKind.Normal, 10, new WaveDef(6,3,2), new WaveDef(7,3,3), new WaveDef(12,0,2)),
            new StageDef("3-6", StageKind.Elite,  10, new WaveDef(6,3,2), new WaveDef(6,3,3), new WaveDef(4,1,1,2)),   // 3 只精英那版在模拟里是一堵墙（卡 1.5 小时），改回 2 只
            new StageDef("3-7", StageKind.Boss,   11, new WaveDef(0,0,0)),
        };
        // v0.18 第四章「镜面上城」：从不下雨的上层城区，镜面玻璃和冷光。首领「镜面审计官」（镜面反射）。场景套 12=A 镜面街区 13=B 空中花园 14=C 冷却中枢 15=D 审计之庭。
        public static readonly StageDef[] Chapter4 = {
            new StageDef("4-1", StageKind.Normal, 12, new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(6,3,3)),
            new StageDef("4-2", StageKind.Normal, 12, new WaveDef(6,3,2), new WaveDef(6,3,3), new WaveDef(7,3,3)),
            new StageDef("4-3", StageKind.Elite,  13, new WaveDef(6,3,2), new WaveDef(6,3,3), new WaveDef(4,1,1,2)),
            new StageDef("4-4", StageKind.Normal, 13, new WaveDef(6,3,3), new WaveDef(7,3,3), new WaveDef(8,3,3)),
            new StageDef("4-5", StageKind.Normal, 14, new WaveDef(6,3,3), new WaveDef(7,3,3), new WaveDef(12,1,2)),
            new StageDef("4-6", StageKind.Elite,  14, new WaveDef(7,3,3), new WaveDef(7,3,3), new WaveDef(4,1,2,2)),
            new StageDef("4-7", StageKind.Boss,   15, new WaveDef(0,0,0)),
        };
        // v0.18 第五章「零号织网」（结局章）：上城地板下把整座城织在一起的网。首领「织网母体」（织网收束）。场景套 16=A 织网边缘 17=B 协议回廊 18=C 母线机房 19=D 零号核心。
        public static readonly StageDef[] Chapter5 = {
            new StageDef("5-1", StageKind.Normal, 16, new WaveDef(6,3,2), new WaveDef(6,3,3), new WaveDef(7,3,3)),
            new StageDef("5-2", StageKind.Normal, 16, new WaveDef(6,3,3), new WaveDef(7,3,3), new WaveDef(8,3,3)),
            new StageDef("5-3", StageKind.Elite,  17, new WaveDef(6,3,2), new WaveDef(6,3,3), new WaveDef(4,1,1,2)),   // 11-23 原来比 5-6 的波次还大（7,3,3 / 4,2,1,2），成了全章最大的墙；改成和 4-3 一样
            new StageDef("5-4", StageKind.Normal, 17, new WaveDef(6,3,3), new WaveDef(7,3,3), new WaveDef(7,4,3)),
            new StageDef("5-5", StageKind.Normal, 18, new WaveDef(6,3,3), new WaveDef(7,3,3), new WaveDef(12,1,2)),   // 14 只冲锋的那版：单体武器的种子在这里卡 80 多次
            new StageDef("5-6", StageKind.Elite,  18, new WaveDef(6,3,3), new WaveDef(7,3,3), new WaveDef(4,1,2,2)),   // 波次和 3-6 / 4-6 一样大，难度只靠数值
            new StageDef("5-7", StageKind.Boss,   19, new WaveDef(0,0,0)),
        };
        private static readonly StageDef[][] Book = { Chapter1, Chapter2, Chapter3, Chapter4, Chapter5 };
        public const int ChapterCount = 5;
        /// 被打倒后去哪一关：在最前面那一关倒下就退回上一关刷；在已经打通的关（正在回刷）倒下就重打这一关，不再一路往下退。
        /// v0.18：数值测试发现一路往下退会让角色从 5-6 退回 5-1 刷好几个小时（掉落等级也跟着变低，越刷越慢）。
        /// region：全局关卡序号。0..6 = 1-1..1-7，7..13 = 2-1..2-7，14..20 = 3-1..3-7，21..27 第四章，28..34 第五章。
        public static int ChapterOf(int region) { return Math.Max(0, region) / StagesPerChapter + 1; }
        public static int FirstRegion(int chapter) { return (chapter - 1) * StagesPerChapter; }
        public static int BossRegion(int chapter) { return chapter * StagesPerChapter - 1; }
        public static int LastRegion { get { return ChapterCount * StagesPerChapter - 1; } }
        public static StageDef Stage(int region)
        {
            region = Math.Max(0, Math.Min(LastRegion, region));
            return Book[region / StagesPerChapter][region % StagesPerChapter];
        }
        /// 场景编号：每章 4 套（0..3 第一章 … 16..19 第五章）；%4 == 3 是首领场地。
        public static int SceneChapter(int scene) { return scene / 4 + 1; }

        public static readonly RoleStats[] Roles = {
            //            血     攻    间隔  速度  停步近 停步远 画布  掉落  外形
            new RoleStats(.45f, .5f,  1.3f, 200, 150, 210, 160, .25f, 0),   // 冲锋：快、血少、成群
            new RoleStats(1.4f, 1f,   2f,   110, 165, 195, 220, .50f, 1),   // 重型：慢、血厚
            new RoleStats(.7f,  .6f,  1.9f, 140, 360, 430, 200, .40f, 2),   // 远程：站在后排
        };
        public const float EliteHealth = 2.6f, EliteAttack = 1.4f, EliteCanvas = 1.2f;
        public const float BossHealth = 6, BossAttack = 1.4f, BossInterval = 1.6f, BossSpeed = 120, BossStop = 200, BossCanvas = 380, FinalBossScale = 1.22f;
        public const int BossArt = 1;
        public const int EliteDrops = 2, BossDrops = 3;   // v0.16：精英 2 件（1 件保底特装），首领 3 件（专属 + 保底特装）

        // 第一章数值（首版，模拟见 Docs/Chapter1_Swarm_v14.md）：关卡越往后越强，但比旧的“区域”曲线平缓。
        // v0.16 敌人血量 ×2.4：1× 速度下第一章约 1.5–2 小时（含在卡点回刷），见 Tools/sim/pacing_sim.py。
        public const float HealthScale = 2.4f;
        // 第二章整体再乘一档（2-1 约等于回声层 8–10 层，2-7 约 25 层）。首版数值，实测后再调。
        public const float Chapter2Health = 2.2f, Chapter2Attack = 1.5f;
        // v0.18 每章的整体倍率（第三章首版，由自动数值测试 Editor/Balance/BalanceSim.cs 校过，见 Docs/数值测试报告.md）。
        // 第三章起，30 级以上升级贵 6 倍，角色变强变慢：章节入口的跳变要比第二章小，再在章内逐关加码（Ramp：每往后一关多乘多少）。
        // 第三章起：入口只比上一章末尾强一点（约 1.2 倍），章内每关血量 +4~5%、攻击 +2% 慢慢加码（再多后期装备的成长跟不上，章末会撞墙）；进新章那一下跳得多一点，靠刷前两关的新一档装备跟上——角色在章内成长很快，这样难度跟得上，又不会在章节入口撞墙。
        // v0.21 数值重测：改造板节点、套装奖励、机库、芯片加进模拟后主线只要 2:20（目标 7:00），第二章起加码。
        // 第一次全加 12–25%：第三～五章还是几乎不倒、每关却要磨 2–5 分钟——新系统主要加的是生存（前排机器人、护盾回路、复合装甲、义体），所以后几章主要加攻击、血少加。
        public static readonly float[] ChapterHealth = { 1, Chapter2Health * 1.3f, 2.6f * 1.2f, 3.6f * 1.15f, 5f * 1.2f }, ChapterAttack = { 1, Chapter2Attack * 1.08f, 1.6f * 2f, 2.55f * 1.9f, 3.6f * 3f };   // v0.20 第五章原来 3.6 / 2.65：带着第四章 4-6 墙练出来的装备进第五章，整章 18 分钟就穿了（目标 2:15）   // 后期少堆血、多给攻击：一关别打超过两分半，难度来自“会不会倒”而不是“磨多久”   // 第五章：装备档次只比第四章高 11%，怪的本章倍率也只能比第四章高一点（关卡序号本身已经让怪涨了 25%）
        public static readonly float[] ChapterHealthRamp = { 0, 0, .05f, .04f, .04f }, ChapterAttackRamp = { 0, 0, .02f, .02f, .02f };
        private static int ChapterIndex(int region) { return Math.Max(0, Math.Min(ChapterHealth.Length - 1, ChapterOf(region) - 1)); }
        private static float Ramp(float[] ramp, int region) { return 1 + ramp[ChapterIndex(region)] * (Math.Max(0, region) % StagesPerChapter); }
        /// v0.19 墙：每章的 x-3、x-6（精英）和 x-7（首领）比前后的关强一截，要在前一关刷一会儿才过得去；其余关平滑。
        /// 血量 × (1 + WallShape × ChapterWall)，攻击乘它的平方根（墙主要是“打不动”，不是“一碰就倒”）。
        public static readonly float[] WallShape = { 0, 0, .5f, 0, .3f, .45f, 1.5f };   // 11-23 x-6 1.2 → .9：每章第 6 关比首领还难（5-6 一关占全章六成时间）
        //   // 首领原来 1.4：后几章首领战老是拖过 3 分钟超时
        public static readonly float[] ChapterWall = { .8f, .5f, .35f, .78f, .35f };   // v0.20 夜间：第四章 .85 → .78、血 3.8 → 3.6（2:41 太慢；.7 / 3.5 又只要 1:00）。第五章墙 .4 → .35（试过 .35–.55）、攻击 3.1 → 3.6：难度从 5-6 墙挪到整章（墙 .45 以上时 5-6 像抛硬币，面板更高的种子也连倒十几次）
        /// x-3 / x-6 精英和 x-7 首领（墙）：回刷时跳过它们。
        public static bool IsWallStage(int region) { return WallShape[Math.Max(0, region) % StagesPerChapter] >= .4f; }
        /// v0.20 第四、五章首领：墙少放在血量上、多放在攻击上（原来很多种子 4 分钟打不死首领，超时一次就白耗 4 分钟，比被打倒还慢）。
        private static bool LateBoss(int region) { return region % StagesPerChapter == StagesPerChapter - 1 && ChapterOf(region) >= 4; }
        private static float WallHealth(int region) { float w = Wall(region); return LateBoss(region) ? 1 + (w - 1) * .55f : w; }
        private static float WallAttack(int region) { float w = Wall(region); return LateBoss(region) ? 1 + (w - 1) * .5f : (float)Math.Sqrt(w); }
        public static float Wall(int region) { return 1 + WallShape[Math.Max(0, region) % StagesPerChapter] * ChapterWall[ChapterIndex(region)] * (region % StagesPerChapter == StagesPerChapter - 1 ? BossWall[ChapterIndex(region)] : 1); }
        /// 11-23 首领要比本章的精英关难（用户拍板）：精英关的墙降一档，首领的墙按章再乘这个倍率。
        public static readonly float[] BossWall = { 1f, .95f, 1.45f, 1.1f, 2.3f };
        public static float Health(int region, int wave) { return (50 + 10 * wave) * (1 + .16f * region) * HealthScale * ChapterHealth[ChapterIndex(region)] * Ramp(ChapterHealthRamp, region) * WallHealth(region); }
        public static float Attack(int region, int wave) { return (4 + 1.2f * wave) * (1 + .14f * region) * ChapterAttack[ChapterIndex(region)] * Ramp(ChapterAttackRamp, region) * WallAttack(region) * AttackTune; }
        /// 11-23 装备平衡：汲取加了回复上限以后，所有配装都不再能靠吸血硬扛，敌人攻击整体下调来补（数值测试校过）。
        public const float AttackTune = .7f;
        public static float Defense(int region) { return region * 3; }

        // ---------- v0.16 无尽回声层 ----------
        // 打通 1-7 后开放。层数无限：普通层 2 波；每 5 层精英层；每 10 层首领层（回声版闸门看守）。
        // v0.19 起强度和掉落沿主线走（见 EchoRegion）。
        // v0.19 回声层接上主线：第 F 层的强度 = 主线第 6 + 0.6 × (F − 1) 关（第 1 层 ≈ 1-7，第 48 层 ≈ 5-7，不算墙），之后每层继续变强；
        // 掉落等级也沿着这条线走（5-7 掉 84–87 级，往后每层约 +1.2 级），所以打完主线去回声层一直有收益。
        public const float EchoRegionsPerFloor = .6f, DeepHealthGrowth = 1.07f, DeepAttackGrowth = 1.045f;
        public static float EchoRegion(int floor) { return EchoAnchor + EchoRegionsPerFloor * Math.Max(0, floor - 1); }
        /// 主线第 region 关对应回声层第几层（往下取整）。
        public static int FloorForRegion(int region) { return 1 + (int)Math.Floor(Math.Max(0, region - EchoAnchor) / EchoRegionsPerFloor); }
        private static float BaseHealthAt(int region, int wave) { return (50 + 10 * wave) * (1 + .16f * region) * HealthScale * ChapterHealth[ChapterIndex(region)] * Ramp(ChapterHealthRamp, region); }
        private static float BaseAttackAt(int region, int wave) { return (4 + 1.2f * wave) * (1 + .14f * region) * ChapterAttack[ChapterIndex(region)] * Ramp(ChapterAttackRamp, region); }
        /// 沿着主线的强度曲线取值（两关之间插值）；过了最后一关按 deep 每关继续涨。
        private static float AlongMain(float r, Func<int, float> f, float deep)
        {
            if (r >= LastRegion) return f(LastRegion) * (float)Math.Pow(deep, r - LastRegion);
            int a = (int)Math.Floor(r); float k = r - a; return f(a) * (1 - k) + f(a + 1) * k;
        }
        public static float EchoHealth(int floor, int wave) { return AlongMain(EchoRegion(floor), reg => BaseHealthAt(reg, wave), DeepHealthGrowth); }
        public static float EchoAttack(int floor, int wave) { return AlongMain(EchoRegion(floor), reg => BaseAttackAt(reg, wave), DeepAttackGrowth); }
        public static float EchoDefense(int floor) { float r = EchoRegion(floor); return Defense((int)Math.Min(LastRegion, r)) + Math.Max(0, r - LastRegion); }
        public static StageKind EchoKind(int floor) { return floor % 10 == 0 ? StageKind.Boss : floor % 5 == 0 ? StageKind.Elite : StageKind.Normal; }
        /// 场景：每 10 层换一个（A → B → C 轮换），章节跟着这一层对应的主线强度走；过了 5-7 五章轮换。首领层用那一章的首领场地。
        public static int EchoScene(int floor)
        {
            float r = EchoRegion(floor); int block = (Math.Max(1, floor) - 1) / 10;
            int ch = r <= LastRegion ? ChapterIndex((int)r) : block % ChapterCount;
            return EchoKind(floor) == StageKind.Boss ? ch * 4 + 3 : ch * 4 + block % 3;
        }
        /// 回声层强度以 1-7 为起点（和玩家当前在哪一章无关）。
        public const int EchoAnchor = 6;
        public static StageDef EchoStage(int floor, bool swarmBoost)
        {
            StageKind kind = EchoKind(floor);
            if (kind == StageKind.Boss) return new StageDef("E" + floor, kind, EchoScene(floor), new WaveDef(0, 0, 0));
            int sw = Math.Min(9, 3 + floor / 10), br = Math.Min(3, 1 + floor / 20), ra = floor < 3 ? 0 : Math.Min(3, 1 + floor / 25);
            if (swarmBoost) sw = (int)Math.Ceiling(sw * 1.5f);
            int elites = kind == StageKind.Elite ? Math.Min(3, 1 + floor / 30) : 0;
            return new StageDef("E" + floor, kind, EchoScene(floor), new WaveDef(sw, br, ra), new WaveDef(sw, br, ra, elites));
        }
        public static int EchoDropLevel(int floor, bool boss, bool elite, double roll)
        {   // v0.19：沿主线的掉落等级走；过了 5-7 每关 +2 级（每层约 +1.2 级），到装备上限为止
            float r = EchoRegion(floor); int lo;
            if (r <= LastRegion) { int a = (int)Math.Round(r); lo = DropLevels[a, 0]; }
            else lo = DropLevels[LastRegion, 0] + (int)Math.Round((r - LastRegion) * 2);
            int hi = lo + 3;
            if (boss) lo = hi; else if (elite) lo = lo + 1;
            return Math.Min(Balance.MaxItemLevel, lo + (int)Math.Floor(roll * (hi - lo + 1 - 1e-9)));
        }

        // 协议异常：每打过一个回声首领层（10 的倍数且刷新最高层），三选一叠加，最多 3 个。只在回声层生效。
        public sealed class Anomaly { public readonly string id, name, desc; public Anomaly(string a, string b, string c) { id = a; name = b; desc = c; } }
        public static readonly Anomaly[] Anomalies = {
            new Anomaly("armor", "厚甲", "怪物生命 +50%；普通怪掉落率 ×1.4"),
            new Anomaly("overload", "过载", "怪物出手快 25%；幸运 +15，高品质更常见"),
            new Anomaly("swarm", "狂潮", "每波冲锋怪 +50%；零件 ×1.5"),
            new Anomaly("elite", "精英化", "普通怪有 12% 变成精英（精英掉 2 件）"),
        };
        public static Anomaly FindAnomaly(string id) { foreach (Anomaly a in Anomalies) if (a.id == id) return a; return null; }
        public const int MaxAnomalies = 3, MilestoneEvery = 25, MilestoneCores = 2;

        // 掉落等级段：每关 [min, max]。普通怪在段内随机，精英偏上半段，首领取上限附近。
        public static readonly int[,] DropLevels = { { 1, 3 }, { 2, 4 }, { 4, 6 }, { 5, 7 }, { 7, 9 }, { 8, 11 }, { 12, 15 },
                                                     { 16, 19 }, { 18, 21 }, { 20, 23 }, { 22, 25 }, { 24, 27 }, { 26, 29 }, { 30, 33 },
                                                     { 34, 37 }, { 36, 39 }, { 38, 41 }, { 40, 43 }, { 42, 45 }, { 44, 47 }, { 48, 51 },
                                                     { 52, 55 }, { 54, 57 }, { 56, 59 }, { 58, 61 }, { 60, 63 }, { 62, 65 }, { 66, 69 },
                                                     { 70, 73 }, { 72, 75 }, { 74, 77 }, { 76, 79 }, { 78, 81 }, { 80, 83 }, { 84, 87 } };
        public static int DropLevel(int region, bool boss, bool elite, double roll)
        {
            int r = Math.Max(0, Math.Min(DropLevels.GetLength(0) - 1, region));
            int lo = DropLevels[r, 0], hi = DropLevels[r, 1];
            if (boss) lo = Math.Max(lo, hi - 1);
            else if (elite) lo = (lo + hi + 1) / 2;
            return lo + (int)Math.Floor(roll * (hi - lo + 1 - 1e-9));
        }
    }
}
